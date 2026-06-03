// =============================================================================
// StoryCompanionInjector — spawns the per-hero STORY COMPANION on village load,
// WITHOUT a scene edit (WO-227 / DEF-119, scoped slice).
// -----------------------------------------------------------------------------
// A self-bootstrapping DontDestroyOnLoad singleton — the SAME pattern as
// VillageNpcInjector — that, every time the "Village" scene loads, reads the
// player's chosen HeroClass from GameStateService and spawns the matching
// StoryCompanion near the hero. No Village.unity edit, no VillageSceneBuilder
// change (both carry serialization-corruption risk and are explicitly off-limits).
//
// The companion is a CODE-BUILT PLACEHOLDER (a slim capsule body tinted per
// companion + a TownsfolkBubble + a NavMeshAgent + the StoryCompanion driver) —
// no new art, per the scoped brief. A future WO swaps in a real model the same
// way VillageNpcInjector swaps the townsfolk.
//
// Idempotent: only one companion is ever live; a re-load rebuilds it for the
// (possibly changed) chosen hero. Runs only in the "Village" scene.
// =============================================================================

using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime spawner for the per-hero <see cref="StoryCompanion"/>.</summary>
    public sealed class StoryCompanionInjector : MonoBehaviour
    {
        public static StoryCompanionInjector Instance { get; private set; }

        private const string TargetScene = "Village";

        // The live companion, so a re-load replaces rather than duplicates it.
        private StoryCompanion _companion;

        // WO-277 (tutorial): the FTUE wants the companion to be a DIFFERENT class
        // from the player (a comrade, not a mirror). When the tutorial sets this
        // override, Spawn() builds the companion for the OVERRIDE class instead of
        // the player's chosen class — so there's exactly ONE companion and it's the
        // mapped class, not two. Cleared (back to player-class) when the tutorial
        // completes. Null = normal behaviour (companion = the player's hero class).
        private static HeroClass? s_heroClassOverride;

        /// <summary>The live story companion's transform, or null if none is spawned.</summary>
        public Transform CompanionTransform => _companion != null ? _companion.transform : null;

        /// <summary>The live story companion, or null if none is spawned.</summary>
        public StoryCompanion Companion => _companion;

        /// <summary>
        /// WO-277 — force the spawned companion to be <paramref name="heroClass"/>
        /// instead of the player's chosen class (the tutorial maps it to a DIFFERENT
        /// class), then re-spawn so the change takes effect immediately. Pass null to
        /// clear the override and restore the player-class companion.
        /// </summary>
        public void SetHeroClassOverride(HeroClass? heroClass)
        {
            s_heroClassOverride = heroClass;
            if (SceneManager.GetActiveScene().name == TargetScene) Spawn();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("StoryCompanionInjector").AddComponent<StoryCompanionInjector>();
        }

        private void Awake()
        {
            // NOTE: Destroy(this) — NOT Destroy(gameObject). This injector lives on
            // its OWN object so that is moot here, but we keep the safe idiom in case
            // it is ever co-located (per the singleton-dedup-destroys-host landmine).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Spawn();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Spawn();
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        private void Spawn()
        {
            // Replace any prior companion (e.g. a re-load after a class change).
            if (_companion != null) { Destroy(_companion.gameObject); _companion = null; }

            HeroClass hero = ResolveChosenHero();
            Transform heroT = ResolveHero();

            // Place a couple of metres off the hero's shoulder; snap onto the
            // baked NavMesh so the agent can path. Falls back to a sensible spot.
            Vector3 basePos = heroT != null
                ? heroT.position - heroT.forward * 2.5f + heroT.right * 1.5f
                : new Vector3(2f, 0f, 2f);
            if (NavMesh.SamplePosition(basePos, out var hit, 6f, NavMesh.AllAreas))
                basePos = hit.position;

            var go = BuildPlaceholder(hero, basePos);

            var comp = go.GetComponent<StoryCompanion>();
            if (comp != null)
            {
                comp.Configure(hero);                                  // before Start()
                var bubble = go.GetComponentInChildren<TownsfolkBubble>();
                if (bubble != null) comp.SetBubble(bubble);
                if (heroT != null) comp.SetHero(heroT);
                _companion = comp;
            }

            Debug.Log($"[StoryCompanionInjector] spawned {CompanionDialogue.NameFor(hero)} " +
                      $"for {hero} hero" + (heroT != null ? "." : " (no hero found yet — will resolve)."));
        }

        // ── Placeholder build (code-only, no art) ────────────────────────────

        /// <summary>
        /// Builds the code-built placeholder companion: a slim capsule body tinted
        /// per companion, on the Ignore-Raycast layer with no collider that could
        /// shove the hero, plus a NavMeshAgent (auto-disabled off-NavMesh), a
        /// TownsfolkBubble, and the StoryCompanion driver.
        /// </summary>
        private static GameObject BuildPlaceholder(HeroClass hero, Vector3 pos)
        {
            var go = new GameObject("StoryCompanion (" + hero + ")");
            go.transform.position = pos;
            // Ignore Raycast (layer 2) — keeps it clear of gameplay raycasts; we
            // strip the collider too so it can never physically block the hero.
            go.layer = 2;

            // SKIN: a real hero mesh stands in for the companion (same VisualFactory path the
            // enemies use) — the matching class body from Resources/Heroes, so the companion
            // reads as a person, not a capsule. Swap to a bespoke CC5 companion model here when
            // those land. Falls back to the tinted-capsule placeholder if the mesh is missing.
            string slug = SlugFor(hero);
            var vis = VisualFactory.Skin(go.transform, "Heroes/" + slug,
                new SkinOptions { FitHeight = 1.8f, StripColliders = true, FixTripoMaterials = true });
            if (vis != null)
            {
                // Tripo hero FBXs import facing +X; the root faces +Z — correct -90 deg yaw
                // (mirrors HeroBodySwapper) so the companion faces its travel direction.
                vis.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                SetLayerRecursive(vis, 2);   // Ignore Raycast — never blocks gameplay rays
                var anim = vis.GetComponent<Animator>() ?? vis.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Heroes/" + slug);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;   // idle/walk, not a T-pose
            }
            else
            {
                // Fallback — the original tinted-capsule placeholder.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                var col = body.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);   // no collider -> never shoves anyone
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                body.transform.localScale = new Vector3(0.55f, 1.0f, 0.55f);
                body.layer = 2;
                TintBody(body.GetComponent<Renderer>(), hero);
            }

            // Speech bubble — self-building world-space bubble (same class the
            // ambient townsfolk use). It builds its panel/text in its own Awake.
            go.AddComponent<TownsfolkBubble>();

            // NavMeshAgent so it paths the baked village NavMesh. StoryCompanion
            // disables it on Start if we are not on a NavMesh (plain-lerp fallback).
            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 2.0f;
            agent.radius = 0.35f;
            agent.baseOffset = 0f;

            // The driver last, so Configure(...) (called by the injector after this
            // returns) lands before its Start() runs next frame.
            go.AddComponent<StoryCompanion>();
            return go;
        }

        /// <summary>Tints the placeholder body with the companion's signature hue.</summary>
        private static void TintBody(Renderer renderer, HeroClass hero)
        {
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader) { name = "StoryCompanion_" + hero };
            Color c = TintFor(hero);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            renderer.sharedMaterial = mat;
        }

        /// <summary>Signature tint per companion — echoes the HeroCatalog card hues.</summary>
        private static Color TintFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return new Color(0.98f, 0.84f, 0.40f); // Grom — holy gold
                case HeroClass.Ranger: return new Color(0.41f, 0.74f, 0.48f); // Sylas — wood-green
                case HeroClass.Mage:   return new Color(0.45f, 0.75f, 1.00f); // Thrain — icy blue
                case HeroClass.Cleric: return new Color(1.00f, 0.93f, 0.70f); // Elara — warm white-gold
                default:               return Color.white;
            }
        }

        /// <summary>HeroClass → Resources/Heroes FBX slug (Cleric shares the Mage body).</summary>
        private static string SlugFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return "Knight";
                case HeroClass.Ranger: return "Ranger";
                case HeroClass.Mage:   return "Mage";
                case HeroClass.Cleric: return "Mage";
                default:               return "Knight";
            }
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }

        // ── Resolution ───────────────────────────────────────────────────────

        /// <summary>
        /// The player's CHOSEN hero class (not the playable downgrade Patricia uses).
        /// Reads GameStateService; defaults to Knight when no class is chosen yet.
        /// </summary>
        private static HeroClass ResolveChosenHero()
        {
            // WO-277: the tutorial override wins so the companion is the mapped
            // (different) class while the FTUE plays.
            if (s_heroClassOverride.HasValue) return s_heroClassOverride.Value;

            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            return HeroClass.Knight;
        }

        // Name-based hero lookup (matches AmbientNPC / VillageNpcInjector).
        private static Transform ResolveHero()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
