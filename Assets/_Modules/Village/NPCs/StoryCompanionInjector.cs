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

using System.Collections.Generic;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
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

        // The live companions, keyed by class, so a re-load / roster change replaces
        // rather than duplicates them. WO (party-of-4): the injector now spawns ONE
        // body PER party-roster member (up to all four classes), not just a single
        // companion — so as Sylas → Elara → Grom join, the field fills to a party of
        // hero + 3 companions, each following + fighting + showing its own party frame.
        private readonly Dictionary<HeroClass, StoryCompanion> _companions =
            new Dictionary<HeroClass, StoryCompanion>();

        // WO-277 (tutorial): the FTUE wants the companion to be a DIFFERENT class
        // from the player (a comrade, not a mirror). When the tutorial sets this
        // override, Spawn() builds the companion for the OVERRIDE class instead of
        // the player's chosen class — so there's exactly ONE companion and it's the
        // mapped class, not two. Cleared (back to player-class) when the tutorial
        // completes. Null = normal behaviour (companion = the player's hero class).
        private static HeroClass? s_heroClassOverride;

        /// <summary>
        /// A representative live companion's transform (the override-class one when a
        /// story beat is framing it, else any spawned companion), or null if none is
        /// spawned. Used by the FTUE / Sylas-meeting framing which spawns exactly one.
        /// </summary>
        public Transform CompanionTransform
        {
            get { var c = Companion; return c != null ? c.transform : null; }
        }

        /// <summary>
        /// A representative live story companion — the override-class companion while a
        /// story beat frames it (FTUE / first-meeting), else the first spawned one.
        /// Null when none is spawned.
        /// </summary>
        public StoryCompanion Companion
        {
            get
            {
                if (s_heroClassOverride.HasValue &&
                    _companions.TryGetValue(s_heroClassOverride.Value, out var o) && o != null)
                    return o;
                foreach (var kv in _companions)
                    if (kv.Value != null) return kv.Value;
                return null;
            }
        }

        /// <summary>
        /// WO-277 — force the spawned companion to be <paramref name="heroClass"/>
        /// instead of the player's chosen class (the tutorial maps it to a DIFFERENT
        /// class), then re-spawn so the change takes effect immediately. Pass null to
        /// clear the override and restore the player-class companion.
        /// </summary>
        public void SetHeroClassOverride(HeroClass? heroClass)
        {
            FlowTrace.Step("Roster", $"SetHeroClassOverride({(heroClass.HasValue ? heroClass.Value.ToString() : "null")}) -> Spawn() (override path)");
            s_heroClassOverride = heroClass;
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Spawn();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) { FlowTrace.Step("Roster", "Bootstrap skipped — injector Instance already exists"); return; }
            FlowTrace.Step("Roster", "Bootstrap — creating StoryCompanionInjector");
            new GameObject("StoryCompanionInjector").AddComponent<StoryCompanionInjector>();
        }

        private void Awake()
        {
            // NOTE: Destroy(this) — NOT Destroy(gameObject). This injector lives on
            // its OWN object so that is moot here, but we keep the safe idiom in case
            // it is ever co-located (per the singleton-dedup-destroys-host landmine).
            if (Instance != null && Instance != this) { FlowTrace.Warn("Roster", "Awake — duplicate injector instance -> Destroy(this)"); Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            FlowTrace.Step("Roster", $"Awake — injector live; activeScene={SceneManager.GetActiveScene().name} isHub={HubScenes.IsHub(SceneManager.GetActiveScene().name)}");

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // WO-301 — re-spawn from the roster the moment a companion joins (the
            // tutorial-complete AddToParty fires PlayerChanged), so the companion
            // appears immediately rather than only on the next scene load.
            var svc = GameStateService.Instance;
            if (svc != null) svc.PlayerChanged.AddListener(OnRosterMaybeChanged);

            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Spawn();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            var svc = GameStateService.Instance;
            if (svc != null) svc.PlayerChanged.RemoveListener(OnRosterMaybeChanged);
        }

        // WO-301 — the party roster changed (a companion joined/left). Re-evaluate the
        // spawn against the roster while we're in the village (Spawn itself gates on
        // the roster + replaces any prior companion, so this is idempotent).
        private void OnRosterMaybeChanged()
        {
            FlowTrace.Step("Roster", $"OnRosterMaybeChanged (PlayerChanged fired) -> Spawn() in scene={SceneManager.GetActiveScene().name}");
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Spawn();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FlowTrace.Step("Roster", $"OnSceneLoaded({scene.name}, {mode}) isHub={HubScenes.IsHub(scene.name)} -> Spawn() if hub");
            if (HubScenes.IsHub(scene.name)) Spawn();
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        private void Spawn()
        {
            // The set of companion classes that SHOULD be live this frame.
            //   • Story-beat override (FTUE / first-meeting): exactly that one class,
            //     so the beat can frame a single companion before the roster is written.
            //   • Normal play: ONE per persisted party-roster member — the party fills
            //     to hero + 3 as Sylas → Elara → Grom join (WO: party-of-4).
            var desired = new HashSet<HeroClass>();
            if (s_heroClassOverride.HasValue)
            {
                desired.Add(s_heroClassOverride.Value);
            }
            else
            {
                foreach (HeroClass cls in PartyRosterClasses())
                    desired.Add(cls);
            }
            FlowTrace.Step("Roster", $"Spawn(): override={(s_heroClassOverride.HasValue ? s_heroClassOverride.Value.ToString() : "null")} " +
                $"desired=[{string.Join(",", desired)}] currentlyLive=[{string.Join(",", _companions.Keys)}]");

            // Despawn any live companion no longer desired (roster shrank / override changed).
            var stale = new List<HeroClass>();
            foreach (var kv in _companions)
                if (kv.Value == null || !desired.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var cls in stale)
            {
                if (_companions.TryGetValue(cls, out var c) && c != null)
                {
                    FlowTrace.Warn("Roster", $"duplicate/stale {CompanionDialogue.NameFor(cls)} ({cls}) -> Destroy body (no longer desired)");
                    Destroy(c.gameObject);
                }
                _companions.Remove(cls);
            }

            if (desired.Count == 0)
            {
                Debug.Log("[StoryCompanionInjector] No party companions to spawn (roster empty) — none shown.");
                return;
            }

            Transform heroT = ResolveHero();
            foreach (HeroClass cls in desired)
            {
                bool alreadyLive = _companions.TryGetValue(cls, out var existing) && existing != null;
                FlowTrace.Step("Roster", $"guard for {cls}: alreadyPresent={alreadyLive} " +
                    $"(checked _companions dict: hasKey={_companions.ContainsKey(cls)}, value!=null={(existing != null)})");
                if (alreadyLive) continue; // already live
                FlowTrace.Step("Roster", $"spawn request: id={cls} name={CompanionDialogue.NameFor(cls)} caller=Spawn()");
                SpawnOne(cls, heroT);
            }
        }

        /// <summary>Spawns one companion body for <paramref name="cls"/> at the hero's shoulder.</summary>
        private void SpawnOne(HeroClass cls, Transform heroT)
        {
            // Place a couple of metres off the hero's shoulder, fanned per class so
            // multiple companions don't stack on the same point; snap onto the baked
            // NavMesh so the agent can path. Falls back to a sensible spot.
            float fan = 1.5f + 0.9f * _companions.Count;
            Vector3 basePos = heroT != null
                ? heroT.position - heroT.forward * 2.5f + heroT.right * fan
                : new Vector3(2f + _companions.Count, 0f, 2f);
            if (NavMesh.SamplePosition(basePos, out var hit, 6f, NavMesh.AllAreas))
                basePos = hit.position;

            var go = BuildPlaceholder(cls, basePos);

            var comp = go.GetComponent<StoryCompanion>();
            if (comp != null)
            {
                comp.Configure(cls);                                   // before Start()
                var bubble = go.GetComponentInChildren<TownsfolkBubble>();
                if (bubble != null) comp.SetBubble(bubble);
                if (heroT != null) comp.SetHero(heroT);
                _companions[cls] = comp;
            }

            FlowTrace.Step("Roster", $"INSTANTIATED companion {CompanionDialogue.NameFor(cls)} ({cls}) " +
                $"(instanceCount now {_companions.Count}) go={(go != null ? go.name : "null")}");

            Debug.Log($"[StoryCompanionInjector] spawned {CompanionDialogue.NameFor(cls)} " +
                      $"({cls}) into the party" + (heroT != null ? "." : " (no hero found yet — will resolve)."));
        }

        /// <summary>
        /// WO (party-of-4) — the companion classes in the persisted party roster
        /// (<see cref="GameState.PartyMemberIds"/>; ids are HeroClass names). Empty
        /// when no service / no roster. Skips ids that don't parse to a HeroClass.
        /// </summary>
        private static IEnumerable<HeroClass> PartyRosterClasses()
        {
            var svc = GameStateService.Instance;
            var ids = svc != null && svc.State != null ? svc.State.PartyMemberIds : null;
            if (ids == null) yield break;
            foreach (var id in ids)
                if (System.Enum.TryParse(id, out HeroClass cls)) yield return cls;
        }

        // ── Arena-defense spawn (WO-389) ─────────────────────────────────────

        /// <summary>
        /// WO-389 — spawn ONE friendly Arena DEFENDER body of class
        /// <paramref name="cls"/> at <paramref name="worldPos"/>, parented under
        /// <paramref name="parent"/> (the raid outpost root). Reuses the SAME
        /// <see cref="BuildPlaceholder"/> path the story companions use (skin +
        /// NavMeshAgent + hitbox + the <see cref="StoryCompanion"/> driver), so the
        /// defender is automatically FRIENDLY: it targets Hostile raiders via
        /// TargetManager and is hit back through its IDamageableStructure hitbox —
        /// zero new combat/targeting code.
        ///
        /// DIFFERENCE from a story companion (the two things the Arena needs):
        ///   1. NO hero-leash. A story companion trails the player and gates combat
        ///      to <c>LeashFromHero</c> of the hero. An Arena defender must HOLD its
        ///      placed cell, NOT chase (and definitely not follow the ATTACKING hero,
        ///      who is the enemy here). We pin its "hero to trail" to a STATIONARY
        ///      guard-post transform at the spawn cell — so it stands on its post and
        ///      engages any raider that comes within range of the post (guard-post
        ///      tether, mirroring OutpostDefender.GuardPost / EnemyOutpost.MakeAnchor).
        ///   2. NO speech bubble (we never call SetBubble) — defenders are silent.
        /// Returns the spawned defender GameObject, or null on failure.
        /// </summary>
        internal static GameObject SpawnDefender(HeroClass cls, Vector3 worldPos, Transform parent)
        {
            var go = BuildPlaceholder(cls, worldPos);
            if (go == null) return null;
            if (parent != null) go.transform.SetParent(parent, true);

            var comp = go.GetComponent<StoryCompanion>();
            if (comp != null)
            {
                comp.Configure(cls);   // before Start(): drives the name + class kit

                // GUARD POST tether (NOT the hero-leash): a stationary transform at the
                // spawn cell that the companion "trails". It is parented to the OUTPOST
                // ROOT (not the companion) so it stays FIXED at the cell — the defender
                // holds its post and only engages hostiles within range of it, and never
                // resolves/chases the attacking hero (SetHero is non-null, so the
                // name-based hero auto-resolve is skipped). No bubble = silent.
                var post = new GameObject("DefenderGuardPost (" + cls + ")").transform;
                post.SetParent(parent != null ? parent : go.transform, true);
                post.position = worldPos;
                comp.SetHero(post);
            }
            return go;
        }

        /// <summary>
        /// WO-389 (#3, ATTACK flow) — spawn ONE friendly Arena ATTACKER body of class
        /// <paramref name="cls"/> at <paramref name="worldPos"/>, hero-leashed to the
        /// player <paramref name="captain"/> so it FOLLOWS the Captain into the enemy
        /// castle and auto-fights. This is the EXACT OPPOSITE of <see cref="SpawnDefender"/>:
        /// a defender is pinned to a stationary guard post (HOLD its cell), whereas an
        /// attacker trails the live hero (StoryCompanion's ORIGINAL leash behaviour) and
        /// engages hostile garrison via the shared TargetManager — zero new combat code.
        ///
        /// Reuses the SAME <see cref="BuildPlaceholder"/> path (skin + NavMeshAgent +
        /// hitbox + the StoryCompanion driver), so the attacker is automatically FRIENDLY
        /// (it targets Hostile raiders/garrison and is hit back via IDamageableStructure).
        /// No speech bubble (silent, like the defender). Returns the spawned GameObject,
        /// or null on failure.
        /// </summary>
        internal static GameObject SpawnAttacker(HeroClass cls, Vector3 worldPos, Transform captain)
        {
            var go = BuildPlaceholder(cls, worldPos);
            if (go == null) return null;

            var comp = go.GetComponent<StoryCompanion>();
            if (comp != null)
            {
                comp.Configure(cls);            // before Start(): name + class kit
                // HERO-LEASH to the Captain (the player hero) — the unit FOLLOWS the
                // Captain and fights the hostile garrison near them. Non-null SetHero
                // skips the name-based auto-resolve. No bubble = silent.
                if (captain != null) comp.SetHero(captain);
            }
            return go;
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
                // GetComponent returns a Unity fake-null on miss, so `??` does NOT fall
                // through (it would assign the fake-null and the next access throws).
                if (!vis.TryGetComponent(out Animator anim)) anim = vis.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Heroes/" + slug);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;   // idle/walk, not a T-pose
                // WO-53 (perf): off-screen animator culling. BuildPlaceholder is the SINGLE
                // body-build path for every companion (party companions + Arena defenders +
                // Arena attackers), so culling here covers all of them. CullUpdateTransforms
                // keeps the state machine running while skipping transform/mesh writes when
                // off-camera — NOT CullCompletely (can desync gameplay-driven anim events).
                if (anim != null) anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                // CRITICAL: same as HeroBodySwapper — the Idle/Walk clips carry baked
                // root curves; with applyRootMotion ON the companion drives itself in a
                // slow circle ("that circle thing the hero does"). StoryCompanion moves
                // the transform itself (NavMeshAgent / lerp), so root motion must be OFF.
                anim.applyRootMotion = false;

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

            // No speech bubble on the story companion (owner: remove his text
            // bubble). The ambient townsfolk keep theirs via AmbientNPC; the story
            // companion speaks through the Yarn dialogue system instead. The
            // GetComponentInChildren<TownsfolkBubble> in Inject() is null-guarded,
            // so SetBubble simply no-ops now.

            // NavMeshAgent so it paths the baked village NavMesh. StoryCompanion
            // disables it on Start if we are not on a NavMesh (plain-lerp fallback).
            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 2.0f;
            agent.radius = 0.35f;
            agent.baseOffset = 0f;

            // HITBOX (WO: companion stakes) — a child capsule collider on the Default
            // layer so the enemy contact-attack lane (Enemy.ProbeForStructure does a
            // SphereCast with QueryTriggerInteraction.Ignore) can actually HIT the
            // companion. It must be NON-trigger and OFF the Ignore-Raycast layer (2)
            // for the probe to find it; GetComponentInParent<IDamageableStructure> on
            // the hit resolves to the StoryCompanion on the root. Kept slim + the
            // NavMeshAgent yields (avoidancePriority 60) so it never shoves the hero.
            var hitbox = new GameObject("CompanionHitbox");
            hitbox.layer = 0;                                  // Default — probe-visible
            hitbox.transform.SetParent(go.transform, false);
            hitbox.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var cap = hitbox.AddComponent<CapsuleCollider>();
            cap.isTrigger = false;
            cap.radius = 0.35f;
            cap.height = 1.9f;
            cap.center = Vector3.zero;

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
                // WO-310: repointed to MATCH HeroBodySwapper (WO-286 authoritative). The old
                // Ranger_tex/Cleric_tex folders don't exist → Resources.Load null → green tint
                // fallback (the reported bug). All four atlases live in Heroes/Textures/.
                HeroClass.Mage   => "Heroes/Textures/tripo_mat_9b343081_Pbr_Diffuse",
                HeroClass.Knight => "Heroes/Textures/medievalknight3dmodel_basecolor",
                HeroClass.Ranger => "Heroes/Textures/ranger_basecolor",
                HeroClass.Cleric => "Heroes/Textures/Cleric_basecolor",
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

                    if (tex != null)
                    {
                        // WO-310: when we HAVE the correct per-class atlas, bind it
                        // AUTHORITATIVELY — do NOT skip a slot just because something is
                        // already on _BaseMap. FixTripoMaterials runs first and can leave a
                        // wrong/embedded FBX texture (or a tinted fallback) on the slot; the
                        // old "if existing, leave it" guard then preserved that wrong atlas,
                        // which is exactly how the Ranger read GREEN. The known-good class
                        // diffuse must win so the companion matches the player hero.
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                        if (m.HasProperty("_Color"))     m.SetColor("_Color", Color.white);
                    }
                    else
                    {
                        // No class diffuse resolved. Preserve any real texture the importer/
                        // TripoMaterialFixer already bound; only paint the signature class tint
                        // when the slot is genuinely untextured, so the body reads coloured
                        // instead of a flat blue/grey/green blob.
                        Texture existing = null;
                        if (m.HasProperty("_BaseMap")) existing = m.GetTexture("_BaseMap");
                        if (existing == null && m.HasProperty("_MainTex")) existing = m.GetTexture("_MainTex");
                        if (existing != null) continue;
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
        /// WO-301 — true when the companion of class <paramref name="companion"/> is in
        /// the persisted party roster (id = the companion's HeroClass name). The roster
        /// is the single source of truth for "is this companion deployed", so spawn keys
        /// off it rather than a one-off flag. No service / empty roster ⇒ not in party.
        /// </summary>
        private static bool IsCompanionInParty(HeroClass companion)
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.IsInParty(companion.ToString());
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
