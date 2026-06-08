using System.Collections;
using UnityEngine;

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

        // ── DEF-100: build interior glow + light + cheap vortex if not wired ─────
        private void EnsureVisuals()
        {
            // Interior glow quad — additive URP/Unlit transparent, deep-violet idle.
            if (glowPlane == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "PortalGlowPlane";
                var qcol = quad.GetComponent<Collider>();
                if (qcol != null) Destroy(qcol);
                quad.transform.SetParent(transform, false);
                // Fill the arch interior, lifted to mid-arch height, facing outward (+Z).
                quad.transform.localPosition = new Vector3(0f, 2.0f, 0.02f);
                quad.transform.localScale = new Vector3(2.4f, 3.6f, 1f);

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
                    var mat = new Material(glowShader);
                    // URP/Unlit transparent setup (Surface=Transparent, Blend=Additive).
                    if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface", 1f);  // transparent
                    if (mat.HasProperty("_Blend"))    mat.SetFloat("_Blend", 1f);    // additive
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", idleGlowColor);
                    if (mat.HasProperty("_Color"))     mat.SetColor("_Color", idleGlowColor);
                    glowPlane.sharedMaterial = mat;
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
                // Slightly behind the glow plane, larger so its soft edge bleeds past
                // the arch interior like a halo.
                halo.transform.localPosition = new Vector3(0f, 2.0f, -0.05f);
                halo.transform.localScale = new Vector3(3.4f, 4.6f, 1f);

                _haloPlane = halo.GetComponent<MeshRenderer>();
                _haloPlane.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _haloPlane.receiveShadows = false;

                Shader haloShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Sprites/Default");
                if (haloShader != null)
                {
                    _haloMat = new Material(haloShader);
                    if (_haloMat.HasProperty("_Surface")) _haloMat.SetFloat("_Surface", 1f);  // transparent
                    if (_haloMat.HasProperty("_Blend"))   _haloMat.SetFloat("_Blend", 1f);    // additive
                    _haloMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _haloMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    _haloPlane.sharedMaterial = _haloMat;
                }
            }

            // Collect the arch frame renderers ONCE (everything except our own VFX
            // children + the prompt) so UpdateGlow can pulse their emission via a
            // shared MPB — never instantiating the shared 264 arch material.
            CollectArchRenderers();
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
                    var pmat = new Material(pShader);
                    if (pmat.HasProperty("_Surface")) pmat.SetFloat("_Surface", 1f);
                    if (pmat.HasProperty("_Blend"))   pmat.SetFloat("_Blend", 1f); // additive
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
                // Skip our own code-built glow plane (and any obvious VFX child).
                if (r == glowPlane) continue;
                if (r.gameObject.name == "PortalGlowPlane" || r.gameObject.name == "PortalVortex") continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null) continue;
                    string sn = m.shader.name;
                    bool isMagentaError = sn == "Hidden/InternalErrorShader";
                    bool isNonUrpStandard = sn == "Standard" || sn.StartsWith("Legacy Shaders/");
                    if (!isMagentaError && !isNonUrpStandard) continue;

                    var nm = new Material(urpLit);
                    nm.name = (m.name ?? "Portal") + " (URP DEF-94)";
                    if (nm.HasProperty("_BaseColor")) nm.SetColor("_BaseColor", violet);
                    if (nm.HasProperty("_Color"))     nm.SetColor("_Color", violet);
                    // Carry a basecolor texture across if the source had one.
                    Texture tex = null;
                    if (m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex == null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
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

            if (inRange && !_active) OnHeroApproach();
            else if (!inRange && _active) OnHeroLeave();
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
                    // Keep the 264 violet BASE; only ramp EMISSION so colour stays on-identity.
                    _archMpb.SetColor(BaseColor, ArchBaseColor(ArcaneViolet));
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
                _haloPlane.transform.localScale = new Vector3(3.4f * swell, 4.6f * swell, 1f);
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
        }

        /// <summary>DEF-100: revert to the idle glow when the hero leaves the 3 m radius.</summary>
        public void OnHeroLeave()
        {
            if (!_active) return;
            _active = false;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(false));
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
