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

        private const string TargetScene = "Village2";

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
            // DEF-232: Tripo hero FBXs import facing +X; the root faces +Z — correct -90° yaw
            // (mirrors HeroBodySwapper). Pass it as LocalRotation so Skin orients the body
            // BEFORE fitting/seating (the post-Skin rotate pattern offset the mesh on the
            // seated hero); the companion faces its travel direction and stays centred.
            var vis = VisualFactory.Skin(go.transform, "Heroes/" + slug,
                new SkinOptions
                {
                    FitHeight = 1.8f,
                    StripColliders = true,
                    FixTripoMaterials = true,
                    LocalRotation = Quaternion.Euler(0f, -90f, 0f),
                });
            if (vis != null)
            {
                // DEF-275 BLUE-ORB / STRAY-LIGHT FIX: the hero FBXs at Resources/Heroes/*
                // import with importCameras:1 + importLights:1 (see their .meta), so the
                // instantiated mesh carries the FBX's EMBEDDED camera, audio listener and
                // any embedded lights/emissive helper nodes. The player's HeroBodySwapper
                // strips those after Skin — but the companion path did NOT, so the live
                // companion dragged a stray Camera (renders a second view), an extra
                // AudioListener (Unity warns + audio breaks) and embedded Light nodes into
                // the scene. Those embedded helpers are exactly the "big blue orb" + "3
                // hearts" the owner saw appear the moment the companion started working
                // (DEF-275) — they were never the capsule fallback (the mesh loads fine).
                // Strip them here, mirroring the hero, so the companion is JUST the body.
                StripEmbeddedFbxArtifacts(vis);

                SetLayerRecursive(vis, 2);   // Ignore Raycast — never blocks gameplay rays
                var anim = vis.GetComponent<Animator>() ?? vis.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Heroes/" + slug);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;   // idle/walk, not a T-pose

                // The Tripo/CC5 FBX slots import with NO basecolor bound (materialImportMode
                // leaves _BaseMap empty), so without this the Mage/Cleric body renders as a
                // flat blue/grey blob — the OTHER half of the "blue orb". Bind the same
                // per-class diffuse the player hero uses so the companion reads as a person.
                BindClassDiffuse(vis, hero);
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

        /// <summary>HeroClass → Resources/Heroes FBX slug. DEF-275: the Cleric now has its
        /// OWN dedicated body (Resources/Heroes/Cleric.fbx + Cleric.controller), matching the
        /// player's HeroBodySwapper.SlugFor — so the Cleric companion (Thrain → no; Elara)
        /// renders the textured cleric body, not the untextured blue Mage robe.</summary>
        private static string SlugFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return "Knight";
                case HeroClass.Ranger: return "Ranger";
                case HeroClass.Mage:   return "Mage";
                case HeroClass.Cleric: return "Cleric";
                default:               return "Knight";
            }
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>
        /// DEF-275: defensive strip of helper components a character FBX can carry. The
        /// Heroes FBXs import with importCameras:1 + importLights:1, so any future re-export
        /// that DOES embed a camera/light node would drag a stray Camera, AudioListener,
        /// Light or Rigidbody into the live scene (camera-soup / stray-render). The player's
        /// HeroBodySwapper already strips Camera+AudioListener after Skin; the companion path
        /// did NOT, so it mirrors that here (plus Light + Rigidbody for safety). Null-safe and
        /// visual-only — the SkinnedMeshRenderers (the actual body) are untouched.
        /// </summary>
        private static void StripEmbeddedFbxArtifacts(GameObject vis)
        {
            if (vis == null) return;
            foreach (var cam in vis.GetComponentsInChildren<Camera>(true))
                if (cam != null) Object.Destroy(cam);
            foreach (var al in vis.GetComponentsInChildren<AudioListener>(true))
                if (al != null) Object.Destroy(al);
            foreach (var lt in vis.GetComponentsInChildren<Light>(true))
                if (lt != null) Object.Destroy(lt);
            foreach (var rb in vis.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) Object.Destroy(rb);
        }

        /// <summary>
        /// DEF-275: binds the per-class basecolor diffuse onto the companion body so it
        /// reads textured instead of a flat blue/grey blob (the imported FBX slots have no
        /// _BaseMap bound). Paths mirror HeroBodySwapper.ApplyExtractedTexture — the same
        /// reliable plain Resources/Heroes/* folders. Falls back to the signature class tint
        /// (TintFor) when no diffuse is found, so the body is never left solid white/blue.
        /// </summary>
        private static void BindClassDiffuse(GameObject vis, HeroClass hero)
        {
            if (vis == null) return;

            string texPath = hero switch
            {
                HeroClass.Mage   => "Heroes/Textures/fantasywizard3dmodel_basecolor",
                HeroClass.Knight => "Heroes/Textures/medievalknight3dmodel_basecolor",
                HeroClass.Ranger => "Heroes/Ranger_tex/remesh_12_combined_Bake_Diffuse",
                HeroClass.Cleric => "Heroes/Cleric_tex/HumanCleric_basecolor",
                _                => null,
            };
            Texture2D tex = string.IsNullOrEmpty(texPath) ? null : Resources.Load<Texture2D>(texPath);
            Color tint = TintFor(hero);

            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    // Already has a real diffuse? Leave it (preserve any working texture).
                    Texture existing = null;
                    if (m.HasProperty("_BaseMap")) existing = m.GetTexture("_BaseMap");
                    if (existing == null && m.HasProperty("_MainTex")) existing = m.GetTexture("_MainTex");
                    if (existing != null) continue;

                    if (tex != null)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                        if (m.HasProperty("_Color"))     m.SetColor("_Color", Color.white);
                    }
                    else
                    {
                        // No diffuse available → paint the signature class tint so the body
                        // reads coloured instead of a flat blue/grey blob.
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                        if (m.HasProperty("_Color"))     m.SetColor("_Color", tint);
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        // ── Resolution ───────────────────────────────────────────────────────

        /// <summary>
        /// The class the COMPANION spawns as. The companion is a comrade, NOT a mirror
        /// of the player (WO-280), so by default we map the player's chosen hero class
        /// to a DIFFERENT companion class via the canon mapping. A tutorial / story-beat
        /// override (e.g. "meet Sylas") wins when set.
        /// </summary>
        private static HeroClass ResolveChosenHero()
        {
            // WO-277: a tutorial / story-beat override wins so the companion is the
            // exact mapped (different) class that beat wants while it plays.
            if (s_heroClassOverride.HasValue) return s_heroClassOverride.Value;

            // WO-280: default companion = a DIFFERENT class from the player's hero
            // (the canon hero→companion mapping), so it never spawns as a hero clone.
            return CompanionClassFor(ResolvePlayerHero());
        }

        /// <summary>
        /// The player's CHOSEN hero class (not the playable downgrade Patricia uses).
        /// Reads GameStateService; defaults to Knight when no class is chosen yet.
        /// </summary>
        private static HeroClass ResolvePlayerHero()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            return HeroClass.Knight;
        }

        /// <summary>
        /// Canon hero→companion mapping (WO-280) — the companion is always a DIFFERENT
        /// class from the player's hero. Delegates to the single source of truth in
        /// <see cref="CompanionSpawner.CompanionClassFor"/> so the FTUE, the Sylas beat
        /// and the default spawn all agree:
        ///   Mage→Knight(Grom) · Knight→Ranger(Sylas) · Ranger→Cleric(Elara) · Cleric→Mage(Thrain).
        /// </summary>
        private static HeroClass CompanionClassFor(HeroClass player)
            => CompanionSpawner.CompanionClassFor(player);

        // Name-based hero lookup (matches AmbientNPC / VillageNpcInjector).
        private static Transform ResolveHero()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
