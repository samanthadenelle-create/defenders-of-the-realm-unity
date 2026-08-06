using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Attach to any dungeon portal GameObject. Drives idle vortex VFX,
    /// activation VFX on approach, and entry/exit burst.
    ///
    /// DEF-100 (2026-06-03): made SELF-SUFFICIENT. On the built/baked portals the
    /// serialized refs (vortexParticles / portalLight / glowPlane) are NULL, so the
    /// portal read as a flat dead prop. This controller now BUILDS its own interior
    /// glow quad + point light + cheap looping vortex particle system in code when
    /// the refs are missing — mirroring the project's runtime-injector pattern
    /// (BuildingSignInjector / CampVisual build visuals in code, not in the scene).
    /// It also runs an own-frame proximity check (3 m) so it needs ZERO scene wiring:
    /// just AddComponent it to a portal and it lights up + reacts to the hero.
    ///
    /// DEF-94 (2026-06-03): defensive runtime magenta-material fix — if the portal's
    /// own arch renderers carry a magenta error / non-URP Standard material (missing
    /// URP material after a bake), they're reassigned to URP/Lit tinted deep violet.
    /// Mobile-cheap: one light, low particle counts, no per-frame allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalVFXController : MonoBehaviour
    {
        [Header("Particle Systems")]
        [Tooltip("Looping swirling vortex — plays when portal is active.")]
        public ParticleSystem vortexParticles;
        [Tooltip("One-shot burst played when hero steps through.")]
        public ParticleSystem entryBurstParticles;

        [Header("Light")]
        public Light portalLight;
        [Range(0.5f, 5f)] public float idleLightIntensity   = 1.8f;
        [Range(1f, 8f)]   public float activeLightIntensity = 4.5f;

        [Header("Glow Plane")]
        [Tooltip("Optional additive quad inside the portal arch for interior glow.")]
        public MeshRenderer glowPlane;
        public Color idleGlowColor   = new Color(0.3f, 0f, 0.8f, 0.4f);
        public Color activeGlowColor = new Color(0.6f, 0.2f, 1f, 0.9f);

        [Header("Transition")]
        // DEF-100: proximity is owned by THIS controller now (3 m, criterion 3),
        // independent of any host trigger radius.
        public float activationRadius = 3f;
        public float flashDuration    = 0.22f;

        [Header("WO-272 Glow Layer")]
        [Tooltip("Idle emissive HDR intensity multiplier on the arch + glow.")]
        [Range(0.5f, 4f)] public float idleGlowIntensity   = 1.4f;
        [Tooltip("Active (hero near) emissive HDR intensity multiplier.")]
        [Range(1f, 8f)]   public float activeGlowIntensity = 3.6f;
        [Tooltip("Breathing pulse: cycles per second of the emissive ramp.")]
        [Range(0.05f, 2f)] public float pulseSpeed = 0.55f;
        [Tooltip("Pulse depth — fraction of the emissive intensity that breathes.")]
        [Range(0f, 0.6f)] public float pulseDepth = 0.30f;
        [Tooltip("How fast the glow eases between idle and active (per second).")]
        [Range(1f, 12f)]  public float glowEaseSpeed = 4.5f;

        private bool _active = false;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor     = Shader.PropertyToID("_BaseColor");

        // ── WO-272 glow-layer state (no per-frame allocations) ───────────────────
        // Arch frame renderers that carry the 264 violet base/dim emission; we ramp
        // a pulsing emissive on top via a single cached MaterialPropertyBlock so we
        // never instantiate the shared arch materials.
        private Renderer[] _archRenderers;
        private MaterialPropertyBlock _archMpb;
        private MeshRenderer _haloPlane;          // brighter additive core/halo behind glowPlane
        private Vector2 _haloBaseSize = new Vector2(3.4f, 4.6f);  // fitted in EnsureVisuals; the breathing swell scales THIS
        private Material _haloMat;                // owned instance, cached
        private Material _glowMat;                // cached glowPlane material instance
        private float _glowLevel = 0f;            // 0 = idle, 1 = active (smoothed)
        private float _pulsePhase = 0f;
        // Canonical arch emissive hue for the pulse (agrees with the 264 base, which
        // is ArcaneViolet-led). A touch brighter than ArcaneViolet so the additive
        // ramp reads as light, not paint.
        private static readonly Color GlowHue = new Color(0.55f, 0.18f, 1f);

        // ── DEF-94: canonical portal arch colour ─────────────────────────────────
        // The portal arch must read as an ARCANE / magical gateway — a deep violet —
        // NOT the soft pastel DungeonDef.AccentColor (peach / pale-green), which was
        // being used as the arch BASE colour and made portals render the wrong hue.
        // AccentColor is the per-dungeon IDENTITY cue, so we keep it as a subtle tint
        // mixed into the violet (and on the emission), preserving identity while the
        // structure itself reads unmistakably as a portal. WO-272's additive glow
        // layers on top of this base.
        public static readonly Color ArcaneViolet = new Color(0.32f, 0.06f, 0.78f);

        /// <summary>
        /// Canonical portal-arch base colour: arcane violet with a light wash of the
        /// per-dungeon <paramref name="accent"/> so each portal keeps its identity cue
        /// without losing the magical-gateway read. Used by every portal arch builder
        /// (world spawner + village entrance bootstrap) so they all agree.
        /// </summary>
        public static Color ArchBaseColor(Color accent)
        {
            // 80% arcane violet, 20% accent — the violet dominates so the portal never
            // reads as a plain tan/green frame; the accent stays a recognisable hint.
            return Color.Lerp(ArcaneViolet, accent, 0.20f);
        }

        /// <summary>Per-dungeon emissive accent for the arch — accent-led but kept
        /// dim so the additive WO-272 glow (added later) is what actually "glows".</summary>
        public static Color ArchEmissionColor(Color accent)
        {
            return Color.Lerp(ArcaneViolet, accent, 0.5f) * 0.45f;
        }

        // ── DEF-100 self-bootstrap + proximity state ─────────────────────────────
        private Transform _hero;
        private float _nextHeroRefresh;
        private const float HeroRefreshInterval = 1.0f; // lazy re-find, no per-frame scan
        private float _nextProximityCheck;
        private const float ProximityInterval = 0.15f;
        private Coroutine _transition;

        // ── WO-893: the SECONDARY flame mouth accent ─────────────────────────────
        // Registry section 7 keeps the procedural vortex as the portal and adds a
        // MediumFlames accent at the mouth. The accent is a Family A LOOP, and a loop
        // played fire-and-forget permanently consumes one of VFXManager's 20 global slots -
        // so it is held by ONE handle and stopped on every exit path.
        //
        // IT IS ALSO GATED ON PROXIMITY, and that is the design, not a budget dodge. There
        // is one portal per discovered dungeon in the overworld and each already holds a
        // Dungeon_Portal_Gate rune-ring loop; a second permanent loop per portal would
        // double a per-map cost against a global cap of 20. Holding the accent only while
        // the hero is inside activationRadius bounds the whole feature to ~1 concurrent
        // loop, AND folds the flame into the "the portal wakes as you approach" language
        // the arch already speaks - which is also what finally gives OnHeroLeave a job.
        private VFXHandle _accent;

        // ── WO-893: making OnHeroExit reachable ──────────────────────────────────
        // OnHeroExit was written and CALLED BY NOTHING - portal ENTER fired a burst and
        // portal EXIT was dead code, so a round trip was visibly asymmetric (an
        // asymmetric transition reads as a bug, not as a style). The mirror beat is
        // EMERGING, and emerging happens in the HUB after the dungeon scene unloads, so no
        // in-dungeon call site could ever have reached it.
        //
        // The dungeon exit stamps this static on its way out; the first portal the hero is
        // standing near in the next scene CLAIMS it and plays Portal_Exit. Time.time is
        // wall-clock since process start and is NOT reset by a scene load, so the stamp
        // survives the fade + load. If the hero surfaces nowhere near a portal the stamp
        // simply lapses and nothing plays - a missed flourish, never a stuck flag.
        private static float s_returnPendingUntil;
        private const float ReturnWindowSeconds = 12f;   // covers fade-out + load + fade-in
        private const float ReturnClaimRadius   = 14f;   // generous: the hub may seat the hero off-arch

        /// <summary>
        /// WO-893: called by the dungeon RETURN exit as it routes home. Arms the
        /// materialise beat for whichever portal the hero turns up next to in the hub.
        /// Static and stateless on purpose - the portal that will play it does not exist
        /// yet when this is called, because its scene has not been loaded.
        /// </summary>
        public static void NotifyReturnedThroughPortal()
        {
            s_returnPendingUntil = Time.time + ReturnWindowSeconds;
            FlowTrace.Step("Portal",
                $"return-through-portal ARMED for {ReturnWindowSeconds:0}s - the first portal within " +
                $"{ReturnClaimRadius:0} m of the hero after the load plays Portal_Exit (materialise).");
        }

        private void Start()
        {
            EnsureVisuals();
            FixMagentaArchMaterials(); // DEF-94 (defensive, no-op if uncertain)

            if (vortexParticles != null) vortexParticles.Play();
            if (portalLight    != null) portalLight.intensity = idleLightIntensity;
            // WO-272: UpdateGlow() now owns the glow plane + arch emission every frame
            // (idle glow ALWAYS visible, criterion 1). Prime it once so frame 0 isn't dark.
            UpdateGlow();
        }

        // -------------------------------------------------------------------------
        // WO-869: THE BLUE BLOCKS. This is the second half of the owner's Seeker capture
        // (docs/ui-review/2026-08-04-seeker/08-portal-magenta.png) - the solid blue-violet
        // rectangles sitting inside the magenta arch, which the review read as "a second set
        // of broken materials". They were not broken materials. They are THESE TWO QUADS
        // (PortalGlowPlane, and the larger PortalHaloPlane behind it) rendering FULLY OPAQUE.
        //
        // PROVEN CAUSE, from the URP shader contract: `_Surface` and `_Blend` are ShaderGUI
        // properties. Writing them on a runtime-created material changes NOTHING about render
        // state - in the editor it is `LitGUI.SetMaterialKeywords` that reads them and writes
        // the ACTUAL state, and that GUI never runs at runtime. So this material kept URP
        // Unlit's defaults (_SrcBlend=One, _DstBlend=Zero, _ZWrite=1) and drew as an opaque
        // slab. The alpha 0.4 in idleGlowColor was simply discarded. Same defect in the halo
        // and in BuildCheapVortex.
        //
        // FIX: write the REAL state - _SrcBlend/_DstBlend/_ZWrite + the keyword + the RenderType
        // tag + the transparent queue. This is not a new idea in this repo: VFXManager.
        // ConfigureUrpParticleBlend already does exactly this for the pooled particle path, and
        // it is the proven precedent. One shared helper so glow, halo and vortex cannot drift.
        // -------------------------------------------------------------------------

        // URP fixed-function blend factors (UnityEngine.Rendering.BlendMode values).
        private const int BLEND_ONE       = 1;
        private const int BLEND_SRC_ALPHA = 5;

        /// <summary>
        /// Put <paramref name="m"/> into genuine TRANSPARENT-ADDITIVE render state. Sets the
        /// state URP actually reads at runtime, not just the ShaderGUI-facing _Surface/_Blend
        /// floats (which alone leave the material opaque - the WO-869 blue-block bug).
        /// </summary>
        private static void ConfigureAdditive(Material m)
        {
            if (m == null) return;
            // Keep the GUI-facing values coherent for anyone inspecting the material...
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // 1 = Transparent
            if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", 2f);     // 2 = Additive
            // ...but THESE are what actually make it transparent at runtime.
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", BLEND_SRC_ALPHA);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", BLEND_ONE);
            if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            // Double-sided: the rebuilt arch is a walk-THROUGH threshold with two pillar rings,
            // so the hero routinely sees it from behind. A back-face-culled quad would make the
            // portal surface vanish from one side, which is the opposite of a landmark.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.doubleSidedGI = true;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;   // 3000
        }

        // -- The threshold surface, FITTED to whatever arch it is attached to -----
        // WO-869 rebuilt the arch (wider + taller + two depth rings), so the old hardcoded
        // 2.4 x 3.6 quad at y=2.0 is the wrong size and the wrong place - it would sit low and
        // narrow inside a 3.6 x 6 m opening. Measure the host's own arch renderers and fit to
        // them, so this controller stays generic and the arch geometry can be retuned freely
        // without ever coming back here. Falls back to the historical numbers when there are
        // no arch renderers to measure (a bare DungeonPortal with no built frame).
        private Vector3 _thresholdCentreLocal = new Vector3(0f, 2.0f, 0f);
        private Vector2 _thresholdSize        = new Vector2(2.4f, 3.6f);

        private void MeasureThreshold()
        {
            if (_archRenderers == null || _archRenderers.Length == 0) return;
            bool any = false;
            Bounds b = default;
            foreach (var r in _archRenderers)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;

            // Local-space centre of the opening: horizontally centred on the frame, and at
            // roughly 45% of its height (the visual middle of a doorway sits below the
            // geometric middle once the lintel/keystone mass is included).
            Vector3 localCentre = transform.InverseTransformPoint(b.center);
            _thresholdCentreLocal = new Vector3(0f, Mathf.Max(0.5f, b.size.y * 0.45f), 0f);
            // Fill the opening but stay inside the pillars/lintel so the frame still frames it.
            _thresholdSize = new Vector2(Mathf.Max(0.5f, b.size.x * 0.78f),
                                         Mathf.Max(0.5f, b.size.y * 0.72f));
            FlowTrace.Step("Portal",
                $"MeasureThreshold: arch bounds size={b.size} -> threshold size={_thresholdSize} " +
                $"at localY={_thresholdCentreLocal.y:0.00} (localCentre.y={localCentre.y:0.00}).");
        }

        // ── DEF-100: build interior glow + light + cheap vortex if not wired ─────
        private void EnsureVisuals()
        {
            // WO-869: collect + measure the arch FIRST so the threshold surface can be fitted
            // to it (the old code collected renderers last and hardcoded the quad size).
            CollectArchRenderers();
            MeasureThreshold();

            // Interior glow quad — additive URP/Unlit transparent, deep-violet idle.
            if (glowPlane == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "PortalGlowPlane";
                var qcol = quad.GetComponent<Collider>();
                if (qcol != null) Destroy(qcol);
                quad.transform.SetParent(transform, false);
                // Suspended at the CENTRE of the threshold (z=0, between the two pillar rings)
                // so it reads as a surface hanging inside a doorway, not a decal stuck on the
                // front face of a frame. That placement is what sells "this leads somewhere".
                quad.transform.localPosition = _thresholdCentreLocal;
                quad.transform.localScale = new Vector3(_thresholdSize.x, _thresholdSize.y, 1f);

                glowPlane = quad.GetComponent<MeshRenderer>();
                glowPlane.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glowPlane.receiveShadows = false;

                // Prefer an additive/unlit URP shader so the glow reads as emissive
                // light, not a lit surface. Fall back gracefully.
                Shader glowShader = Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                    ?? Shader.Find("Sprites/Default");
                if (glowShader != null)
                {
                    var mat = new Material(glowShader) { name = "PortalThreshold_Additive" };
                    ConfigureAdditive(mat);   // WO-869: real transparent state, not just _Surface/_Blend
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", idleGlowColor);
                    if (mat.HasProperty("_Color"))     mat.SetColor("_Color", idleGlowColor);
                    glowPlane.sharedMaterial = mat;
                }
                else
                {
                    FlowTrace.Warn("Portal",
                        "EnsureVisuals: no URP Unlit/Particles-Unlit/Sprites shader resolvable - the portal " +
                        "threshold quad keeps its DEFAULT material, which renders as an opaque block under URP.");
                }
            }

            // Point light — deep-violet, idle intensity.
            if (portalLight == null)
            {
                var lgo = new GameObject("PortalLight");
                lgo.transform.SetParent(transform, false);
                lgo.transform.localPosition = new Vector3(0f, 2.0f, 0.5f);
                portalLight = lgo.AddComponent<Light>();
                portalLight.type = LightType.Point;
                portalLight.color = new Color(0.45f, 0.05f, 1f); // deep violet
                portalLight.range = 9f;
                portalLight.intensity = idleLightIntensity;
                portalLight.shadows = LightShadows.None; // mobile-cheap
            }

            // Cheap looping vortex — kept tiny (<=24 particles) so it's mobile-safe.
            if (vortexParticles == null)
            {
                var pgo = new GameObject("PortalVortex");
                pgo.transform.SetParent(transform, false);
                pgo.transform.localPosition = new Vector3(0f, 2.0f, 0.1f);
                vortexParticles = pgo.AddComponent<ParticleSystem>();
                BuildCheapVortex(vortexParticles);
            }

            // ── WO-272: brighter additive core/halo behind the interior glow ─────
            // A larger, softer additive quad that sits just behind the glow plane so
            // the portal reads as a luminous well, not a flat lit panel. It pulses +
            // brightens with proximity (driven in UpdateGlow). One extra transparent
            // quad — mobile-cheap, shadows off.
            if (_haloPlane == null)
            {
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                halo.name = "PortalHaloPlane";
                var hcol = halo.GetComponent<Collider>();
                if (hcol != null) Destroy(hcol);
                halo.transform.SetParent(transform, false);
                // Just behind the threshold surface and larger, so its soft edge bleeds past the
                // opening like a halo. Fitted to the measured arch (WO-869) - the old hardcoded
                // 3.4 x 4.6 was sized for the retired 1.8 x 4 m stick frame.
                halo.transform.localPosition = _thresholdCentreLocal + new Vector3(0f, 0f, -0.07f);
                _haloBaseSize = new Vector2(_thresholdSize.x * 1.40f, _thresholdSize.y * 1.28f);
                halo.transform.localScale = new Vector3(_haloBaseSize.x, _haloBaseSize.y, 1f);

                _haloPlane = halo.GetComponent<MeshRenderer>();
                _haloPlane.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _haloPlane.receiveShadows = false;

                Shader haloShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Sprites/Default");
                if (haloShader != null)
                {
                    _haloMat = new Material(haloShader) { name = "PortalHalo_Additive" };
                    ConfigureAdditive(_haloMat);   // WO-869: this quad was the LARGER blue block
                    _haloPlane.sharedMaterial = _haloMat;
                }
            }

            // The arch renderers were collected + measured at the top of this method (WO-869),
            // so UpdateGlow can pulse their emission via a shared MPB - never instantiating the
            // shared 264 arch material.
            _archMpb ??= new MaterialPropertyBlock();
            // Cache the glow-plane material instance once (reading .material clones it
            // on first touch; we keep the reference so we never re-clone per frame).
            if (glowPlane != null) _glowMat = glowPlane.material;
        }

        // Arch renderers = our host's mesh frame, excluding the code-built glow /
        // halo / vortex children and the TextMesh prompt (which must stay readable).
        private void CollectArchRenderers()
        {
            var all = GetComponentsInChildren<MeshRenderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            foreach (var r in all)
            {
                if (r == null) continue;
                if (r == glowPlane || r == _haloPlane) continue;
                string n = r.gameObject.name;
                if (n == "PortalGlowPlane" || n == "PortalHaloPlane" || n == "PortalVortex") continue;
                if (r.GetComponent<TextMesh>() != null) continue; // never tint the prompt
                list.Add(r);
            }
            _archRenderers = list.ToArray();
        }

        private void BuildCheapVortex(ParticleSystem ps)
        {
            if (ps == null) return;
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.0f;
            main.startSize = 0.35f;
            main.maxParticles = 24;            // mobile-cheap cap
            main.startColor = new Color(0.55f, 0.15f, 1f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 10f;       // low

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.0f;

            // Gentle swirl so it reads as a vortex without a velocity module storm.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.2f, 1f), 0f),
                        new GradientColorKey(new Color(0.35f, 0f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.7f, 0.3f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            // Additive URP particle material so it glows.
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                if (pShader != null)
                {
                    // WO-869: same opaque-quad defect as the glow/halo planes - _Surface/_Blend
                    // alone left these particles drawing as solid billboard squares.
                    var pmat = new Material(pShader) { name = "PortalVortex_Additive" };
                    ConfigureAdditive(pmat);
                    if (pmat.HasProperty("_BaseColor")) pmat.SetColor("_BaseColor", new Color(0.55f, 0.15f, 1f, 1f));
                    psr.sharedMaterial = pmat;
                }
                psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psr.receiveShadows = false;
            }
        }

        // ── DEF-94: defensive runtime magenta / non-URP material fix ─────────────
        // Only touches THIS portal's own arch renderers (excludes our code-built
        // glow / vortex children). LogWarning + no-op if it can't confidently find
        // a magenta or Standard material — never recolours arbitrary meshes.
        private void FixMagentaArchMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[PortalVFXController] URP/Lit not found — skipping DEF-94 magenta fix.");
                return;
            }

            Color violet = ArcaneViolet; // DEF-94: single canonical arcane-violet source
            int fixedCount = 0;

            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null) continue;
                // Skip our own code-built VFX children. WO-869 added PortalHaloPlane to this
                // list: it was missing, and now that the predicate above is the full authority
                // (not three name tests) a device that cannot compile URP/Unlit would have had
                // its additive halo "recovered" into an OPAQUE violet Lit slab - i.e. the guard
                // would have recreated the very blue-block artefact this WO is removing.
                if (r == glowPlane || r == _haloPlane) continue;
                if (r.gameObject.name == "PortalGlowPlane" || r.gameObject.name == "PortalVortex"
                    || r.gameObject.name == "PortalHaloPlane") continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    // WO-869 - THE ANDROID BLIND SPOT, and the most likely reason this fixer
                    // waved the Seeker's magenta arch straight through.
                    //
                    // The old test compared the shader NAME against three literals (the internal
                    // error shader, Standard, and the Legacy prefix) - and nothing else:
                    // NAME-ONLY, with no `!shader.isSupported` branch and no null-material
                    // branch. A shader that compiles in the Editor but FAILS to compile against
                    // the device graphics API KEEPS ITS NAME - so on the phone it is still called
                    // "Universal Render Pipeline/Lit", sails past all three name tests, and renders
                    // MAGENTA anyway. That is exactly the editor-fine / device-magenta split the
                    // owner captured. A NULL material slot has the same outcome (URP draws the
                    // engine default = magenta) and `m == null -> continue` skipped it entirely.
                    //
                    // MagentaGuard.IsBrokenShader is the SINGLE AUTHORITY for this predicate and
                    // it has both branches. Its own docstring warns that local copies drift, names
                    // the two that already had (GhostPreview, EquipmentController) - both since
                    // deleted - and ShaderPredicateSingleAuthorityRegression exists to catch a
                    // third. This file was that third copy. It now calls the authority.
                    if (m == null || DeNelle.Core.MagentaGuard.IsBrokenShader(m.shader))
                    {
                        FlowTrace.Once("Portal", $"arch-broken:{r.name}:{i}",
                            $"DEF-94/WO-869: arch slot {i} on '{r.name}' is broken - material=" +
                            $"'{(m != null ? m.name : "NULL")}' shader=" +
                            $"'{(m != null && m.shader != null ? m.shader.name : "NULL")}' supported=" +
                            $"{(m != null && m.shader != null ? m.shader.isSupported.ToString() : "n/a")} -> recovering to URP/Lit violet.");
                    }
                    else continue;

                    var nm = new Material(urpLit);
                    // `m` may legitimately be NULL now (a null slot is one of the magenta
                    // classes the widened test above deliberately catches) - everything that
                    // reads the source is null-guarded from here down.
                    nm.name = ((m != null ? m.name : null) ?? "Portal") + " (URP DEF-94)";
                    if (nm.HasProperty("_BaseColor")) nm.SetColor("_BaseColor", violet);
                    if (nm.HasProperty("_Color"))     nm.SetColor("_Color", violet);
                    // Carry a basecolor texture across if the source had one.
                    Texture tex = null;
                    if (m != null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex == null && m != null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex != null)
                    {
                        if (nm.HasProperty("_BaseMap")) nm.SetTexture("_BaseMap", tex);
                        if (nm.HasProperty("_MainTex")) nm.SetTexture("_MainTex", tex);
                    }
                    mats[i] = nm;
                    changed = true;
                    fixedCount++;
                }
                if (changed) r.sharedMaterials = mats;
            }

            if (fixedCount == 0)
                Debug.Log("[PortalVFXController] DEF-94: no magenta/Standard arch material found — no-op.");
            else
                Debug.Log($"[PortalVFXController] DEF-94: reassigned {fixedCount} arch material(s) to URP/Lit violet.");
        }

        // ── DEF-100: own-frame proximity (3 m) drives idle ⇄ active ──────────────
        private void Update()
        {
            // WO-272: drive the animated glow every frame (cheap, allocation-free).
            UpdateGlow();

            if (Time.time < _nextProximityCheck) return;
            _nextProximityCheck = Time.time + ProximityInterval;

            EnsureHeroRef();
            if (_hero == null) return;

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= activationRadius * activationRadius;

            // WO-893: claim a pending "the hero just came back through a portal" stamp.
            // Checked on the SAME throttled tick as proximity so it costs nothing extra,
            // and cleared FIRST so two portals near each other cannot both play it.
            if (s_returnPendingUntil > 0f && Time.time <= s_returnPendingUntil &&
                distSqr <= ReturnClaimRadius * ReturnClaimRadius)
            {
                s_returnPendingUntil = 0f;
                FlowTrace.Step("Portal",
                    $"claimed the return stamp at {Mathf.Sqrt(distSqr):0.0} m - playing Portal_Exit " +
                    "(the materialise beat that mirrors OnHeroEnter; it had no caller before WO-893).");
                OnHeroExit();
            }

            if (inRange && !_active) OnHeroApproach();
            else if (!inRange && _active) OnHeroLeave();
        }

        // ── WO-893: accent lifecycle. EVERY exit path stops the held loop. ───────

        private void OnEnable()
        {
            // A scene unload can tear down the VFXManager and its pool while this portal
            // object survives (additive loads, DDOL hosts), stranding the held instance.
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloadedStopAccent;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloadedStopAccent;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloadedStopAccent;
            StopAccent("OnDisable");
        }

        private void OnDestroy() => StopAccent("OnDestroy");

        private void OnSceneUnloadedStopAccent(UnityEngine.SceneManagement.Scene _)
            => StopAccent("sceneUnloaded");

        /// <summary>
        /// Hold the SECONDARY flame accent at the portal mouth while the hero is close.
        /// Idempotent: a live handle is reused, never stacked. A refused start (global loop
        /// cap / no manager / no catalogued prefab) is a silent no-op with a throttled
        /// trace - the portal still reads through its vortex, glow, halo and light, so a
        /// missing accent degrades the flourish and never the affordance.
        /// </summary>
        private void StartAccent()
        {
            if (_accent != null && _accent.IsAlive) return;
            _accent = null;

            var mgr = VFXManager.Instance;
            if (mgr == null) return;

            // Parented to the arch and seated at the threshold centre so the flame licks
            // the MOUTH of the opening. Kept low and inside the frame deliberately: the
            // phone is landscape at 2670x1200, so a tall flame is exactly the part that
            // leaves the screen.
            _accent = mgr.PlayEnvironment(VFXType.Env_DungeonPortal, transform);
            if (_accent == null)
            {
                FlowTrace.Throttle("Portal", "accent-refused", 5f,
                    "Env_DungeonPortal accent REFUSED (global loop cap or quality gate) - the portal " +
                    "keeps its procedural vortex, which is the primary read anyway.");
                return;
            }

            var mod = _accent.Modulator;
            if (mod != null)
            {
                // Seat the room-scale pack recipe onto an arch mouth and clear any
                // modulation left by this pooled instance's previous owner.
                mod.SetScaleMul(1f);
                mod.SetSimulationSpeed(1f);
                mod.SetEmissionScale(1f);
            }

            FlowTrace.Step("Portal",
                "flame mouth accent HELD (secondary to the procedural vortex; released when the hero leaves).");
        }

        /// <summary>Release the accent loop. Idempotent; safe with nothing held.</summary>
        private void StopAccent(string reason)
        {
            if (_accent == null) return;
            _accent.Stop();
            _accent = null;
            FlowTrace.Step("Portal", $"flame mouth accent released ({reason}) - loop slot returned.");
        }

        // ── WO-272: animated arcane-violet glow layered over the 264 base ────────
        // Per-frame, no allocations: eases _glowLevel toward idle/active, builds a
        // breathing emissive intensity (sine pulse), then writes it to the arch
        // (via the shared MPB), the interior glow plane, and the additive halo. The
        // 264 base already painted the arch deep violet with DIM emission — this is
        // the ramp that makes it actually glow + react.
        private void UpdateGlow()
        {
            // Ease toward the proximity target (1 active, 0 idle) — smooth, framerate-safe.
            float target = _active ? 1f : 0f;
            _glowLevel = Mathf.MoveTowards(_glowLevel, target, glowEaseSpeed * Time.deltaTime);

            // Breathing pulse. Idle breathes gently; near the hero it breathes a
            // little stronger (depth scales up with proximity) — alive, not a rave.
            _pulsePhase += Time.deltaTime * pulseSpeed * (1f + 0.5f * _glowLevel);
            float wave = (Mathf.Sin(_pulsePhase * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            float depth = pulseDepth * (0.6f + 0.4f * _glowLevel);
            float pulse = 1f - depth + wave * depth;                            // ~[1-depth..1]

            float baseIntensity = Mathf.Lerp(idleGlowIntensity, activeGlowIntensity, _glowLevel);
            float intensity = baseIntensity * pulse;

            // Emissive arch colour: arcane-violet hue carried at HDR intensity.
            Color emissive = GlowHue * intensity;

            // 1) Pulse the arch frame emission via the shared MPB (no material clone).
            if (_archRenderers != null && _archRenderers.Length > 0)
            {
                _archMpb ??= new MaterialPropertyBlock();
                for (int i = 0; i < _archRenderers.Length; i++)
                {
                    var r = _archRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_archMpb);
                    // WO-869: this used to ALSO write _BaseColor here, every frame. A
                    // MaterialPropertyBlock overrides the material value, so that write silently
                    // killed DungeonWorldPortalSpawner.ApplyDim - the fog-of-war reveal that is
                    // supposed to keep an UNDISCOVERED portal at UndiscoveredDim (0.12) and fade
                    // it up when the hero finds it. Every portal rendered full-bright violet from
                    // frame 0 instead, so "stumble on a hidden arch" was not actually hidden.
                    // Only EMISSION is ramped here now; BASE COLOUR belongs to the discovery layer.
                    _archMpb.SetColor(EmissionColor, emissive);
                    r.SetPropertyBlock(_archMpb);
                }
            }

            // 2) Interior glow plane (additive URP/Unlit) — the additive contribution
            // IS its _BaseColor, so we pulse that. Tint lerps idle->active hue; the
            // alpha carries the per-frame breathing brightness.
            if (_glowMat != null)
            {
                Color glowTint = Color.Lerp(idleGlowColor, activeGlowColor, _glowLevel);
                // Fold the pulse into RGB so an additive blend visibly breathes.
                Color glowRgb = new Color(glowTint.r, glowTint.g, glowTint.b, 1f) * (0.55f + 0.45f * pulse);
                glowRgb.a = glowTint.a;
                if (_glowMat.HasProperty(BaseColor)) _glowMat.SetColor(BaseColor, glowRgb);
                if (_glowMat.HasProperty("_Color")) _glowMat.SetColor("_Color", glowRgb);
                // Harmless on Unlit, helps if a fallback Lit/Sprites shader is in use.
                if (_glowMat.HasProperty(EmissionColor))
                    _glowMat.SetColor(EmissionColor, GlowHue * (intensity * 0.9f));
            }

            // 3) Additive halo — brighter core that swells + brightens with proximity.
            if (_haloMat != null)
            {
                // Soft additive: alpha low so it reads as a bloom-y halo, scaled by glow.
                float haloA = Mathf.Lerp(0.16f, 0.42f, _glowLevel) * pulse;
                Color halo = new Color(GlowHue.r, GlowHue.g, GlowHue.b, haloA);
                if (_haloMat.HasProperty("_BaseColor")) _haloMat.SetColor("_BaseColor", halo);
                if (_haloMat.HasProperty("_Color"))     _haloMat.SetColor("_Color", halo);
            }
            if (_haloPlane != null)
            {
                // Gentle breathing swell (±6% near hero) so the halo feels alive.
                float swell = 1f + (wave - 0.5f) * 0.12f * (0.5f + _glowLevel);
                _haloPlane.transform.localScale = new Vector3(_haloBaseSize.x * swell, _haloBaseSize.y * swell, 1f);
            }
        }

        private void EnsureHeroRef()
        {
            if (_hero != null) return;            // cached — no per-frame scan
            if (Time.time < _nextHeroRefresh) return;
            _nextHeroRefresh = Time.time + HeroRefreshInterval;

            var p = GameObject.FindWithTag("Player");
            // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
            if (p == null) p = SafeFindWithTag("HeroTarget");
            if (p != null) _hero = p.transform;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        public void OnHeroApproach()
        {
            if (_active) return;
            _active = true;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(true));
            StartAccent();   // WO-893: the mouth flame is part of "the portal wakes up"
        }

        /// <summary>DEF-100: revert to the idle glow when the hero leaves the 3 m radius.</summary>
        public void OnHeroLeave()
        {
            if (!_active) return;
            _active = false;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(false));
            StopAccent("hero left the activation radius");   // WO-893
        }

        public void OnHeroEnter()
        {
            entryBurstParticles?.Play();
            // Reconciled to the real APIs: VFXManager.Play is static; the project's
            // camera shake is CameraShakeBridge.Shake(intensity, duration) (there is
            // no CameraShakeManager/ShakeTier). Medium tier ≈ 0.3 intensity / 0.3s.
            VFXManager.Play(VFXType.Portal_Enter, transform.position);
            CameraShakeBridge.Shake(0.3f, 0.3f);
            StartCoroutine(ScreenFlashRoutine());
        }

        /// <summary>
        /// The MATERIALISE beat - the hero emerging from this portal, mirroring
        /// <see cref="OnHeroEnter"/>. WO-893 gave it a caller for the first time (see
        /// <see cref="NotifyReturnedThroughPortal"/>): it was written and reachable from
        /// nothing, so a portal round trip flashed on the way in and was silent on the way
        /// back. The two bursts share ONE recipe and differ only by MOTION SIGN - enter
        /// throws outward, exit is drawn inward - which is a mirror the owner can read with
        /// all colour removed. Still public so a future in-world emergence can call it.
        /// </summary>
        public void OnHeroExit()
        {
            entryBurstParticles?.Play();
            VFXManager.Play(VFXType.Portal_Exit, transform.position);
        }

        // Ramp the POINT LIGHT toward active or back to idle. The arch/glow/halo
        // emission ramp is owned by UpdateGlow() (WO-272), keyed off _active via the
        // smoothed _glowLevel — so this coroutine no longer touches the glow plane.
        private IEnumerator TransitionRoutine(bool toActive)
        {
            float fromLight = portalLight != null ? portalLight.intensity : idleLightIntensity;
            float toLight   = toActive ? activeLightIntensity : idleLightIntensity;

            float elapsed = 0f, rampTime = 0.5f;
            while (elapsed < rampTime)
            {
                float t = elapsed / rampTime;
                if (portalLight != null)
                    portalLight.intensity = Mathf.Lerp(fromLight, toLight, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (portalLight != null) portalLight.intensity = toLight;
            _transition = null;
        }

        private IEnumerator ScreenFlashRoutine()
        {
            // "ScreenFlash" may be undefined (FindWithTag throws on an undefined tag).
            var flash = SafeFindWithTag("ScreenFlash");
            if (flash == null) yield break;
            var img = flash.GetComponent<UnityEngine.UI.Image>();
            if (img == null) yield break;
            img.color = Color.white;
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                img.color = Color.Lerp(Color.white, Color.clear, elapsed / flashDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            img.color = Color.clear;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
