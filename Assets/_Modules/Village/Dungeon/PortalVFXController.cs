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

        private bool _active = false;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

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
            SetGlowColor(idleGlowColor); // idle glow ALWAYS visible (criterion 1)
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

            Color violet = new Color(0.35f, 0f, 0.8f);
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
            if (Time.time < _nextProximityCheck) return;
            _nextProximityCheck = Time.time + ProximityInterval;

            EnsureHeroRef();
            if (_hero == null) return;

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= activationRadius * activationRadius;

            if (inRange && !_active) OnHeroApproach();
            else if (!inRange && _active) OnHeroLeave();
        }

        private void EnsureHeroRef()
        {
            if (_hero != null) return;            // cached — no per-frame scan
            if (Time.time < _nextHeroRefresh) return;
            _nextHeroRefresh = Time.time + HeroRefreshInterval;

            var p = GameObject.FindWithTag("Player");
            if (p == null) p = GameObject.FindWithTag("HeroTarget");
            if (p != null) _hero = p.transform;
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

        // Ramp light + glow toward active (≥1.5× idle) or back to idle.
        private IEnumerator TransitionRoutine(bool toActive)
        {
            float fromLight = portalLight != null ? portalLight.intensity : idleLightIntensity;
            float toLight   = toActive ? activeLightIntensity : idleLightIntensity;
            Color fromGlow  = toActive ? idleGlowColor : activeGlowColor;
            Color toGlow    = toActive ? activeGlowColor : idleGlowColor;

            float elapsed = 0f, rampTime = 0.5f;
            while (elapsed < rampTime)
            {
                float t = elapsed / rampTime;
                if (portalLight != null)
                    portalLight.intensity = Mathf.Lerp(fromLight, toLight, t);
                SetGlowColor(Color.Lerp(fromGlow, toGlow, t));
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (portalLight != null) portalLight.intensity = toLight;
            SetGlowColor(toGlow);
            _transition = null;
        }

        private IEnumerator ScreenFlashRoutine()
        {
            var flash = GameObject.FindWithTag("ScreenFlash");
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

        private void SetGlowColor(Color c)
        {
            if (glowPlane == null) return;
            var mat = glowPlane.material;
            if (mat == null) return;
            mat.color = c;
            if (mat.HasProperty(EmissionColor))
                mat.SetColor(EmissionColor, c * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
