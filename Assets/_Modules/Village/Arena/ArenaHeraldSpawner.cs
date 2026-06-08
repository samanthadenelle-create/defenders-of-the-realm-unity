// =============================================================================
// ArenaHeraldSpawner — the in-village ENTRY POINT that makes the Arena reachable.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// THE GAP IT CLOSES: the Arena MVP (ArenaPanel.Open / ArenaMode) was fully built
// but NOTHING opened it -- it was unreachable. This places a discoverable "Arena
// Herald" marker (a code-built glowing banner) near the village Heart and, when the
// hero comes close, offers an Interact prompt that calls ArenaPanel.Open(). The
// player walks up to it and taps to open the opponent-select / wager screen.
//
// PATTERN REUSE (CLAUDE.md SS9 -- no new system, no scene bake):
//   * Self-bootstraps via RuntimeInitializeOnLoadMethod(AfterSceneLoad) -- NO scene
//     edit, NO prefab dependency, NO bake. Mirrors DungeonWorldPortalSpawner /
//     CampSystem / NodeDiscoverySystem exactly.
//   * Proximity interaction reuses the SHARED MobileInteractButton (touch) plus the
//     desktop [F] key -- the same dual-input affordance every village structure uses
//     (DEF-203). Suppressed automatically in Build Mode + while a modal panel is open
//     (MobileInteractButton.Suppressed / PanelManager.AnyOpen).
//   * Panel lifecycle MIRRORS ShopPanel's entry (NPCCommandBridge.CmdOpenShop):
//     FindFirstObjectByType<ArenaPanel>() or create a host GameObject, then Open().
//
// DDOL singleton: Destroy(this), NOT the host (CLAUDE.md "singleton dedup destroys
// host"). Village -> Core only; cross-module reads are null-conditional.
// Canon: village is Elarion. ASCII-only runtime strings.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Places a discoverable "Arena Herald" marker in the village and opens
    /// <see cref="ArenaPanel"/> when the hero interacts with it (touch button or [F]).
    /// Self-bootstrapping; reuses MobileInteractButton + the ShopPanel open pattern.
    /// </summary>
    public sealed class ArenaHeraldSpawner : MonoBehaviour
    {
        public static ArenaHeraldSpawner Instance { get; private set; }

        // ── Tunables (code-only; no SO authoring) ────────────────────────────
        [Tooltip("Where the Arena herald stands, relative to the village Heart (0,0,0). " +
                 "A few metres off the plaza so it reads as its own landmark.")]
        public Vector3 HeraldOffset = new Vector3(8f, 0f, 6f);

        [Tooltip("How close (metres) the hero must be for the Interact prompt to arm.")]
        public float InteractRadius = 4.5f;

        [Tooltip("Visual height of the placeholder banner pole (metres).")]
        public float BannerHeight = 3.2f;

        private const float PlaceRetryInterval = 1.0f;

        private bool _placed;
        private float _retryTimer;
        private Transform _heraldRoot;
        private Transform _hero;
        private ArenaPanel _panel;

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("ArenaHeraldSpawner");
            go.AddComponent<ArenaHeraldSpawner>();
            Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // Destroy(this), not the host -- DDOL singleton (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_placed)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    _retryTimer = Mathf.Max(0.25f, PlaceRetryInterval);
                    TryPlace();
                }
                return;
            }

            EnsureHero();
            TickProximity();
        }

        // =====================================================================
        // Placement — one herald near the village Heart. Waits for the village
        // scene's hero to exist so we only place inside the village (not e.g. the
        // intro / dungeon scenes that have no "Player").
        // =====================================================================
        private void TryPlace()
        {
            // Only place in a scene that actually has a hero (the village / outer
            // world). This keeps the herald out of the intro, dungeons, etc.
            var hero = SafeFindWithTag("Player");
            if (hero == null) return;

            _heraldRoot = BuildHerald(HeraldOffset);
            _placed = true;
            Debug.Log($"[ArenaHeraldSpawner] Arena herald placed at {_heraldRoot.position}. " +
                      "Walk up + Interact (Tap / F) to open the Arena.");
        }

        // =====================================================================
        // Proximity — arm the shared Interact prompt while the hero is in range;
        // [F] or the touch button opens the Arena. Mirrors every village structure.
        // =====================================================================
        private void TickProximity()
        {
            if (_heraldRoot == null || _hero == null) return;

            float sqr = InteractRadius * InteractRadius;
            if ((_heraldRoot.position - _hero.position).sqrMagnitude > sqr) return;

            // Touch path: the shared bottom-centre button (auto-suppressed in build
            // mode + while a modal is open). Tapping it opens the Arena.
            MobileInteractButton.Request(this, "Enter Arena", OpenArena);

            // Desktop path: [F]. Skip while build mode suppresses interaction.
            if (!MobileInteractButton.Suppressed && Input.GetKeyDown(KeyCode.F))
                OpenArena();
        }

        // =====================================================================
        // Open the Arena — MIRRORS ShopPanel's entry (NPCCommandBridge.CmdOpenShop):
        // find-or-create the panel host, then Open().
        // =====================================================================
        private void OpenArena()
        {
            if (_panel == null) _panel = FindFirstObjectByType<ArenaPanel>();
            if (_panel == null)
            {
                var host = new GameObject("ArenaPanelHost");
                _panel = host.AddComponent<ArenaPanel>();
            }
            _panel.Open();
            Debug.Log("[ArenaHeraldSpawner] Opened the Arena panel.");
        }

        // =====================================================================
        // Build a code-built placeholder banner so the herald reads as a landmark
        // with no art dependency (BuildArch pattern from DungeonWorldPortalSpawner).
        // =====================================================================
        private Transform BuildHerald(Vector3 offset)
        {
            var root = new GameObject("ArenaHerald");
            DontDestroyOnLoad(root);
            root.transform.position = offset; // Heart is at world origin (0,0,0).

            // Face the banner back toward the Heart so the hero reads its front.
            Vector3 toHeart = -new Vector3(offset.x, 0f, offset.z);
            if (toHeart.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(toHeart.normalized);

            Color accent = new Color(0.85f, 0.20f, 0.20f); // arena crimson

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material mat = lit != null ? new Material(lit) : null;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", accent);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", accent * 0.6f);
            }

            // Pole + a banner flag panel near the top.
            MakeBox(root.transform, new Vector3(0f, BannerHeight * 0.5f, 0f),
                    new Vector3(0.22f, BannerHeight, 0.22f), mat);
            MakeBox(root.transform, new Vector3(0.55f, BannerHeight * 0.82f, 0f),
                    new Vector3(1.0f, BannerHeight * 0.5f, 0.08f), mat);

            // WO-370: persistent magical aura so the monument reads as special.
            BuildAura(root.transform, accent);

            return root.transform;
        }

        // =====================================================================
        // WO-370 — persistent ambient AURA on the monument. Lightweight + WebGL-safe:
        //   * a soft colored point Light (gentle pulse) for the magical glow,
        //   * a slow looping particle system of rising spell-motes around the banner.
        // Fully code-built (no prefab / VFXCatalog dependency) so it always shows
        // regardless of VFX quality gating, mirroring the herald's no-art philosophy.
        // =====================================================================
        private void BuildAura(Transform parent, Color accent)
        {
            var auraGo = new GameObject("ArenaAura");
            auraGo.transform.SetParent(parent, false);
            auraGo.transform.localPosition = new Vector3(0f, BannerHeight * 0.6f, 0f);

            // ── Glow: a soft point Light tinted to the arena accent, gently pulsing.
            var lightGo = new GameObject("AuraGlow");
            lightGo.transform.SetParent(auraGo.transform, false);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Color.Lerp(accent, Color.white, 0.35f);
            glow.range = 7f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None; // cheap; no shadow cost on mobile/WebGL
            lightGo.AddComponent<AuraPulse>();

            // ── Spell-motes: a slow upward swirl of glowing particles around the banner.
            var motesGo = new GameObject("AuraMotes");
            motesGo.transform.SetParent(auraGo.transform, false);
            var ps = motesGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(accent, Color.white, 0.4f), accent);
            main.gravityModifier = -0.04f; // gentle drift upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40; // low cap — cheap for mobile / WebGL

            var em = ps.emission;
            em.rateOverTime = 7f;

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 0.9f;
            sh.rotation = new Vector3(-90f, 0f, 0f); // emit upward from the base

            // Gentle fade-out so motes dissolve magically rather than pop.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Use an additive unlit material if URP particle shader is available; the
            // default ParticleSystem renderer material otherwise (still renders fine).
            var psr = motesGo.GetComponent<ParticleSystemRenderer>();
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("Sprites/Default");
            if (pShader != null && psr != null)
            {
                var pMat = new Material(pShader);
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", accent);
                psr.sharedMaterial = pMat;
            }

            ps.Play();
        }

        private static void MakeBox(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HeraldPart";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // marker only; proximity is distance-checked, not a trigger
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null && r != null) r.sharedMaterial = mat;
        }

        // =====================================================================
        // Helpers.
        // =====================================================================
        private void EnsureHero()
        {
            if (_hero != null) return;
            var p = SafeFindWithTag("Player");
            _hero = p != null ? p.transform : null;
        }

        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }
    }

    /// <summary>
    /// WO-370 — gently pulses a Light's intensity for a living magical glow.
    /// Tiny, allocation-free, frame-rate independent. Added by ArenaHeraldSpawner
    /// to the Arena monument's aura glow light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class AuraPulse : MonoBehaviour
    {
        [Tooltip("Mid-point light intensity.")]
        public float BaseIntensity = 1.6f;

        [Tooltip("How far intensity swings above/below the base.")]
        public float Amplitude = 0.5f;

        [Tooltip("Pulse speed (radians/sec).")]
        public float Speed = 1.6f;

        private Light _light;
        private float _phase;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) BaseIntensity = _light.intensity;
            _phase = Random.value * Mathf.PI * 2f; // de-sync if more than one ever exists
        }

        private void Update()
        {
            if (_light == null) return;
            _phase += Time.deltaTime * Speed;
            _light.intensity = BaseIntensity + Mathf.Sin(_phase) * Amplitude;
        }
    }
}
