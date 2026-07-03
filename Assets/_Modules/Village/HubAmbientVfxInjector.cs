// =============================================================================
// HubAmbientVfxInjector -- runtime, NON-DESTRUCTIVE ambient VFX depth for the
// home hub (MainCastle_Hall). FIRST-PASS BONES the owner finesses by eye.
// -----------------------------------------------------------------------------
// Owner brief (2026-06-23): "add an aura around the tree in town, maybe
// something on towers on corner of castle -- free effects add depth."
//
// WHAT IT DOES (on every MainCastle_Hall load, idempotent):
//   1. TREE AURA -- a soft, slow upward drift of glowing motes around the Tree
//      of Life (Heart of Elarion) at the plaza centre, giving the centrepiece a
//      living glow.
//   2. CORNER-TOWER ACCENTS -- a small warm flame/glow loop at the top of each
//      of the 4 castle corner towers, so the silhouette reads alive at a glance.
//
// WHY A RUNTIME INJECTOR (not a scene edit / regen) -- same rationale as
//   CastleVendorNpcInjector / CastleCompanionIntroducerInjector: re-saving
//   MainCastle_Hall.unity carries the project's scene-resave corruption risk
//   (CLAUDE.md SS3 "NEVER hand-edit"). So this self-bootstrapping DDOL singleton
//   FINDS the tree + tower transforms at runtime and parents looping
//   ParticleSystems to them, WITHOUT ever touching the .unity file.
//
// WHY SELF-BUILT PROCEDURAL PARTICLES (not a VFX prefab) -- the committed VFX
//   prefabs (Assets/Resources/VFX/Projectiles/*) are oneshot impacts/projectiles;
//   there is NO committed aura/glow LOOP prefab, and the richer aura packs
//   (Spells Pack / Mirza Beig) are GITIGNORED -- referencing them would break a
//   clean clone (CLAUDE.md SS4). So every effect here is built in code from a
//   Unity ParticleSystem and rendered with the project's committed URP-safe
//   material helper (AbilityVfxKit.ApplyParticleMaterial -- the same one the
//   procedural VFX fallback uses). Zero asset dependency => safe on a fresh clone,
//   mobile-cheap (a few small, low-rate systems), and EVERY knob is a tunable
//   const below so the owner dials look/feel by eye tomorrow.
//
// FLAG-GATED -- FeatureFlags.HubAmbientVfx (default ON so the owner SEES the
//   draft; PlayerPrefs "ff.hubambientvfx" = 0 turns it off, per-zone toggles below).
//
// Village -> Core only (FeatureFlags + FlowTrace/Guard). No cross-asmdef ref, no
// reflection. ASCII only.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive ambient VFX depth on the castle hub (tree aura + corner-tower accents).</summary>
    public sealed class HubAmbientVfxInjector : MonoBehaviour
    {
        public static HubAmbientVfxInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";
        private const string HolderName  = "HubAmbientVFX (runtime)";

        // =====================================================================
        //  TUNABLES -- the owner dials these by eye. Grouped per zone. All are
        //  plain consts (no inspector drag-drop, per the never-dragdrop rule) so
        //  a value change is a one-line edit + rebuild. Colours are RGBA 0..1.
        // =====================================================================

        // ---- Master per-zone toggles (cheap kill switches alongside the flag) ----
        private const bool EnableTreeAura    = true;   // (1) glow around the Tree of Life
        private const bool EnableTowerAccents = true;  // (2) flame/glow at each corner tower top

        // ---- (1) TREE AURA ---------------------------------------------------
        // A soft column of slowly rising, gently glowing motes hugging the trunk +
        // drifting up through the canopy. Tuned soft + sparse so it reads "alive,"
        // not "on fire."
        private const float TreeAuraHeight       = 9.0f;   // vertical extent of the emit column (m)
        private const float TreeAuraRadius       = 3.2f;   // horizontal radius of the emit cylinder (m)
        private const float TreeAuraBaseYOffset  = 0.5f;   // lift the column base off the ground (m)
        private const float TreeAuraRate         = 14f;    // particles / second (low = cheap + tasteful)
        private const float TreeAuraRiseSpeed    = 0.55f;  // upward drift speed (m/s)
        private const float TreeAuraLifetime     = 4.0f;   // seconds a mote lives (long, slow fade)
        private const float TreeAuraSizeMin      = 0.18f;  // mote size range (m)
        private const float TreeAuraSizeMax      = 0.42f;
        // Soft mystical teal-green glow -- the "life force" read. Alpha < 1 = additive-soft.
        // BLOOM-AWARE retune (2026-07-02): post-processing is now LIVE project-wide
        // (WorldFeelInjector: bloom 0.45 / threshold 0.9). This colour was authored
        // blind to bloom; a mild HDR lift (~1.25x, peaks just over the 0.9 threshold
        // after the alpha fade) lets a few motes catch a gentle halo -- "alive",
        // never a blowout. Owner dials by eye.
        private static readonly Color TreeAuraColor = new Color(0.58f, 1.20f, 0.90f, 0.50f);

        // ---- (2) CORNER-TOWER ACCENTS ---------------------------------------
        // A small warm flickering flame/ember glow perched at the top of each
        // corner tower (brazier-like). Small + few so 4 of them stay mobile-cheap.
        private const float TowerAccentTopYOffset = 1.0f;  // raise above the measured tower top (m)
        private const float TowerAccentRate       = 10f;   // particles / second per tower
        private const float TowerAccentRiseSpeed  = 0.9f;  // ember rise speed (m/s)
        private const float TowerAccentLifetime   = 1.6f;  // seconds an ember lives
        private const float TowerAccentSizeMin    = 0.22f; // ember size range (m)
        private const float TowerAccentSizeMax    = 0.55f;
        private const float TowerAccentSpread     = 0.5f;  // emit-sphere radius at the tower top (m)
        // Warm torch-amber. Slightly higher alpha than the tree so the points read.
        // BLOOM-AWARE (2026-07-02): mild HDR lift so the brazier points catch the live
        // bloom like real embers (threshold 0.9) -- see TreeAuraColor note.
        private static readonly Color TowerAccentColor = new Color(1.35f, 0.80f, 0.28f, 0.70f);
        // Fallback tower-top height used only if the tower's renderer bounds can't
        // be measured (e.g. pack not imported). CastleHubBuilder's Tower_Castle_Round
        // is a few metres tall; this is a safe stand-in so the glow is never buried.
        private const float TowerTopFallbackHeight = 6.0f;

        // Name conventions baked by CastleHubBuilder (verified against that builder):
        //   - corner towers: "CornerTower_1".."CornerTower_4" (line ~131)
        //   - tree mesh child: "TreeOfLife_Visual" under "HeartOfElarion" (WireCastleHeart ~2476)
        private const string TowerNamePrefix = "CornerTower_";
        private const string TreeVisualName  = "TreeOfLife_Visual";
        private const string HeartAnchorName = "HeartOfElarion";

        // Fallback Heart/tree centre if the controller/anchor isn't up yet -- matches
        // CastleVendorNpcInjector.HeartCenter() + CastleHubBuilder's authored placement.
        private static readonly Vector3 HeartCenterFallback = new Vector3(0f, 0f, 12f);

        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(HubAmbientVfxInjector)).AddComponent<HubAmbientVfxInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Inject();
        }

        private void Inject()
        {
            // Flag gate -- togglable without a rebuild (PlayerPrefs "ff.hubambientvfx").
            if (!FeatureFlags.HubAmbientVfx)
            {
                FlowTrace.Step("HubVfx", "HubAmbientVfx flag OFF -- ambient hub VFX skipped.");
                return;
            }

            // Idempotent: clear any prior runtime holder so a re-load doesn't double-spawn.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            var holder = new GameObject(HolderName);

            int treeCount  = EnableTreeAura    ? AttachTreeAura(holder.transform)      : 0;
            int towerCount = EnableTowerAccents ? AttachTowerAccents(holder.transform) : 0;

            if (treeCount == 0 && towerCount == 0)
                FlowTrace.Warn("HubVfx",
                    "HubAmbientVfxInjector: attached 0 ambient effects -- no tree and no corner towers found in the hub.");
            else
                FlowTrace.Step("HubVfx",
                    $"HubAmbientVfxInjector: ambient depth attached (tree aura={treeCount}, tower accents={towerCount}).");
        }

        // ---- (1) TREE AURA ---------------------------------------------------
        private int AttachTreeAura(Transform holder)
        {
            Transform tree = ResolveTreeTransform(out Vector3 basePos);
            int attached = 0;

            Guard.Try("HubVfx", "attach tree aura", () =>
            {
                // Parent to the tree so the aura rides along if the tree ever moves;
                // fall back to a free node at the Heart centre if no tree mesh found.
                Transform parent = tree != null ? tree : holder;

                var go = new GameObject("TreeOfLife_AmbientAura");
                go.transform.SetParent(parent, worldPositionStays: false);
                // World-space placement at the trunk base (tree mesh is scaled ~7x, so
                // build the system in WORLD simulation space and size by world metres --
                // independent of the parent's lossy scale).
                go.transform.position = basePos + Vector3.up * TreeAuraBaseYOffset;
                go.transform.rotation = Quaternion.identity;

                BuildColumnAura(go,
                    height:    TreeAuraHeight,
                    radius:    TreeAuraRadius,
                    rate:      TreeAuraRate,
                    riseSpeed: TreeAuraRiseSpeed,
                    lifetime:  TreeAuraLifetime,
                    sizeMin:   TreeAuraSizeMin,
                    sizeMax:   TreeAuraSizeMax,
                    color:     TreeAuraColor);

                attached = 1;
                FlowTrace.Step("HubVfx", $"tree aura attached at {go.transform.position} (parent='{parent.name}').");
            });

            if (attached == 0)
                FlowTrace.Warn("HubVfx", "tree aura NOT attached -- tree transform unresolved or build threw.");
            return attached;
        }

        // ---- (2) CORNER-TOWER ACCENTS ---------------------------------------
        private int AttachTowerAccents(Transform holder)
        {
            int attached = 0;
            var all = FindObjectsByType<Transform>();
            foreach (var t in all)
            {
                if (t == null || t.name == null) continue;
                if (!t.name.StartsWith(TowerNamePrefix)) continue;

                Transform tower = t;
                Guard.Try("HubVfx", $"attach tower accent '{tower.name}'", () =>
                {
                    Vector3 top = TowerTopWorld(tower) + Vector3.up * TowerAccentTopYOffset;

                    var go = new GameObject($"TowerAccent_{tower.name}");
                    go.transform.SetParent(tower, worldPositionStays: false);
                    go.transform.position = top;
                    go.transform.rotation = Quaternion.identity;

                    BuildPointAccent(go,
                        spread:    TowerAccentSpread,
                        rate:      TowerAccentRate,
                        riseSpeed: TowerAccentRiseSpeed,
                        lifetime:  TowerAccentLifetime,
                        sizeMin:   TowerAccentSizeMin,
                        sizeMax:   TowerAccentSizeMax,
                        color:     TowerAccentColor);

                    attached++;
                    FlowTrace.Step("HubVfx", $"tower accent attached on '{tower.name}' at {top}.");
                });
            }

            if (attached == 0)
                FlowTrace.Warn("HubVfx",
                    $"no corner-tower accents attached -- no transforms named '{TowerNamePrefix}*' found in the hub.");
            return attached;
        }

        // =====================================================================
        //  Particle builders -- self-contained, URP-safe, mobile-cheap.
        // =====================================================================

        // Soft rising column (the tree aura): a tall, wide cylinder emitter with
        // slow upward drift and a long fade.
        private static void BuildColumnAura(GameObject go, float height, float radius, float rate,
                                            float riseSpeed, float lifetime, float sizeMin, float sizeMax, Color color)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = true;
            main.duration        = 5f;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(riseSpeed * 0.6f, riseSpeed);
            main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor      = new ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = -0.02f;                 // very slight buoyancy
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 120;                    // hard cap (cheap)

            var emission = ps.emission;
            emission.rateOverTime = rate;

            // Emit from a vertical cylinder hugging the trunk/canopy. Unity's Cone
            // with a large radius + angle 0 approximates an upward cylinder column.
            var shape = ps.shape;
            shape.enabled    = true;
            shape.shapeType  = ParticleSystemShapeType.Cone;
            shape.angle      = 0f;                          // straight up (no spread cone)
            shape.radius     = radius;
            shape.length     = height;
            shape.rotation   = new Vector3(-90f, 0f, 0f);   // cone axis -> world +Y (up)

            FadeOverLife(ps);
            ApplyMaterial(ps, color);
            ps.Play();
        }

        // Small point glow (the tower accent): a tight sphere emitter with quick
        // rise + flicker -- a brazier/ember read.
        private static void BuildPointAccent(GameObject go, float spread, float rate, float riseSpeed,
                                             float lifetime, float sizeMin, float sizeMax, Color color)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = true;
            main.duration        = 3f;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(riseSpeed * 0.5f, riseSpeed);
            main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor      = new ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = -0.05f;                 // embers float up
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 40;                     // hard cap (cheap, x4 towers)

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = spread;

            FadeOverLife(ps);
            ApplyMaterial(ps, color);
            ps.Play();
        }

        // Fade alpha to zero over a particle's life so motes/embers dissolve softly
        // (no hard pop). Keeps colour, ramps alpha 1 -> 0.
        private static void FadeOverLife(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),    // fade IN from nothing
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.7f),
                    new GradientAlphaKey(0f, 1f),    // fade OUT to nothing
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        // Use the project's committed, URP-safe particle material helper so the
        // system renders correctly (never the magenta default) with zero asset deps.
        private static void ApplyMaterial(ParticleSystem ps, Color color)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            // AbilityVfxKit (DeNelle.Village) resolves URP Particles/Unlit with a wide
            // fallback chain and logs (never silently magenta). Tint the material too.
            if (!AbilityVfxKit.ApplyParticleMaterial(r))
                FlowTrace.Warn("HubVfx", "ApplyParticleMaterial returned false -- no usable shader; particle left unassigned (no magenta).");
            else if (r.sharedMaterial != null)
                r.sharedMaterial.color = color;
        }

        // =====================================================================
        //  Transform resolution -- name + component lookups, null-safe.
        // =====================================================================

        // Resolve the visible Tree-of-Life transform (preferred) and the world-space
        // base position of the trunk. Order: TreeOfLife_Visual by name -> Heart anchor
        // by name -> HeartController component -> authored fallback (0,0,12).
        private Transform ResolveTreeTransform(out Vector3 basePos)
        {
            // 1. The visible tree mesh by name (CastleHubBuilder names it TreeOfLife_Visual).
            var all = FindObjectsByType<Transform>();
            Transform treeVisual = null;
            Transform heartAnchor = null;
            foreach (var t in all)
            {
                if (t == null || t.name == null) continue;
                if (treeVisual == null && t.name == TreeVisualName) treeVisual = t;
                if (heartAnchor == null && t.name == HeartAnchorName) heartAnchor = t;
            }

            if (treeVisual != null)
            {
                basePos = TreeBaseWorld(treeVisual);
                return treeVisual;
            }

            // 2. Heart anchor node by name.
            if (heartAnchor != null)
            {
                basePos = heartAnchor.position;
                return heartAnchor;
            }

            // 3. HeartController component (same lookup CastleVendorNpcInjector uses).
            var heart = FindAnyObjectByType<HeartController>();
            if (heart != null)
            {
                basePos = heart.transform.position;
                return heart.transform;
            }

            // 4. Authored fallback -- functional but no parent (free node at the centre).
            basePos = HeartCenterFallback;
            return null;
        }

        // World-space base (feet) of the tree from its renderer bounds, so the aura
        // column starts at the trunk base regardless of the mesh's pivot/scale.
        private static Vector3 TreeBaseWorld(Transform tree)
        {
            if (TryMeasureBounds(tree, out Bounds b))
                return new Vector3(b.center.x, b.min.y, b.center.z);
            return tree.position;
        }

        // World-space top of a tower from its renderer bounds (for the brazier glow).
        private static Vector3 TowerTopWorld(Transform tower)
        {
            if (TryMeasureBounds(tower, out Bounds b))
                return new Vector3(b.center.x, b.max.y, b.center.z);
            // No renderer (pack not imported?) -- stack a safe fallback height on the pivot.
            return tower.position + Vector3.up * TowerTopFallbackHeight;
        }

        // Encapsulate all child renderer bounds. Returns false if none (pack missing).
        private static bool TryMeasureBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var rends = root.GetComponentsInChildren<Renderer>();
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
