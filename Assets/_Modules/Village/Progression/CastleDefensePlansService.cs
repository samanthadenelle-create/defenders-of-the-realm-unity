// =============================================================================
// CastleDefensePlansService -- WO-1013 "Castle Defense Plans": the ONE authored
// drop that unlocks the Arcane Spire after the player survives wave 2.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Self-bootstraps like EchoWorkforceBootstrap (RuntimeInitializeOnLoadMethod,
// no scene authoring, no VillageSceneBuilder re-save). One persistent service;
// a cheap 1 s scan (the EchoWaveUnlockBridge cadence) decides everything from
// PERSISTED STATE, so every acceptance shape falls out of one rule:
//   spawn IFF GameState.WavesCompleted >= 2 AND the unlock is not collected
//         AND no prop is already standing AND this scene runs the town wave loop.
// - "survive wave 2": WavesCompleted is the persisted lifetime wave-clear counter
//   (EchoService increments it off WaveManager.OnWaveCleared). Waves only run
//   post-onboarding, so the gate is WAVES, not tutorial completion -- skip-tutorial
//   players are covered for free (WO-1013 acceptance).
// - "persists until collected": the prop is deterministically re-spawned from
//   state on every scene entry / restart until the flag flips. Nothing is saved
//   about the prop itself.
// - "wave 3+ drop nothing scripted": once collected the flag closes the rule
//   forever. ShouldSpawnDrop is pure so the regression pins the truth table.
//
// The prop mirrors the DungeonTreasureCache visual grammar (primitives + point
// light -- no chest prefab ships under Resources/) and the ComposedKeyPickup
// trigger grammar (sphere trigger + hero check) via CastleDefensePlansPickup.
// No banner, no modal, no announcement chrome (WO-1013 SS3): it glints at the
// gate, nothing more.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Installs the single persistent <see cref="CastleDefensePlansService"/>.</summary>
    public static class CastleDefensePlansBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (CastleDefensePlansService.Instance != null) return;
            var go = new GameObject("CastleDefensePlansService");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CastleDefensePlansService>();
        }
    }

    /// <summary>
    /// Owns the WO-1013 plans-drop lifecycle: decides from persisted state whether
    /// the drop should stand, spawns/repairs the prop at the gate, and emits the
    /// [Flow:Progression] funnel lines (drop-spawned / first-spire-built; the
    /// collected/unlocked lines live in <see cref="CastleDefensePlansPickup"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastleDefensePlansService : MonoBehaviour
    {
        /// <summary>Waves the player must have SURVIVED before the plans drop (WO-1013 SS1).</summary>
        public const int RequiredWavesSurvived = 2;

        public static CastleDefensePlansService Instance { get; private set; }

        private GameObject _prop;
        private float _nextScan;
        private const float ScanInterval = 1.0f;   // the EchoWaveUnlockBridge cadence

        // first-spire-built funnel: emit ONCE, on the transition observed this session
        // (a save that already built one stays silent -- baseline taken on first scan).
        private bool _baselineTaken;
        private bool _firstSpireTraced;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The ONE spawn rule, pure so the guardrail regression pins it headless:
        /// spawn IFF enough waves survived AND not yet collected AND no live prop.
        /// Collected-forever is what makes wave 3+ (and every wave after) drop
        /// nothing scripted -- this is not a drop system (WO-1013 SS3).
        /// </summary>
        public static bool ShouldSpawnDrop(int wavesCompleted, bool unlocked, bool propAlive)
            => !unlocked && !propAlive && wavesCompleted >= RequiredWavesSurvived;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) return;

            // -- first-spire-built funnel (transition-only, once per session) --------
            if (!_baselineTaken)
            {
                _firstSpireTraced = state.HasEverBuilt(CastleDefensePlansPickup.SpireCatalogId);
                _baselineTaken = true;
            }
            if (!_firstSpireTraced && state.HasEverBuilt(CastleDefensePlansPickup.SpireCatalogId))
            {
                _firstSpireTraced = true;
                FlowTrace.Step("Progression",
                    "first-spire-built: tower_arcane_spire committed through the normal build flow " +
                    "(everBuiltStructureIds) -- WO-1013 funnel complete");
            }

            // -- the one spawn rule --------------------------------------------------
            bool unlocked = ProgressionUnlocks.IsUnlocked(CastleDefensePlansPickup.SpireCatalogId);
            if (!ShouldSpawnDrop(state.WavesCompleted, unlocked, _prop != null)) return;

            // Only the defended town runs the village wave loop; a raid/dungeon/battle
            // scene has no village WaveManager and must never grow the drop.
            if (FindAnyObjectByType<WaveManager>() == null) return;

            Guard.Try("Progression", "spawn plans drop", () => SpawnDrop(state.WavesCompleted));
        }

        // =====================================================================
        //  SPAWN
        // =====================================================================

        private void SpawnDrop(int wavesCompleted)
        {
            Vector3 seat = ResolveGateSeat(out string seatSource);

            _prop = new GameObject("CastleDefensePlans_Drop");
            // Scene-owned on purpose (NOT under this DDOL service): the prop dies with
            // the scene and the scan deterministically re-spawns it from state.
            _prop.transform.position = seat;

            BuildVisual(_prop.transform);

            var sphere = _prop.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.6f;
            // Kinematic body so the trigger fires regardless of how the hero rig is
            // composed (CharacterController vs collider+rigidbody).
            var rb = _prop.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _prop.AddComponent<CastleDefensePlansPickup>();

            FlowTrace.Step("Progression",
                $"plans-drop-spawned @ {seat} (seat={seatSource}, wavesCompleted={wavesCompleted}, " +
                "one authored drop -- persists until collected; WO-1013)");
        }

        /// <summary>
        /// Seat the drop AT THE GATE (WO-1013 SS1): prefer the first cardinal
        /// <see cref="Gate"/> (lowest gateId ordinal -- deterministic), pulled 3.5 m
        /// toward the village centre so it lands inside the perimeter; else the
        /// SpawnPoint tag nearest the centre (canon: 12 m outside each gate) pulled
        /// 8 m inward; else a fixed near-centre fallback. Ground-snapped by raycast.
        /// </summary>
        private static Vector3 ResolveGateSeat(out string source)
        {
            Vector3 seat;
            var gates = FindObjectsByType<Gate>(FindObjectsSortMode.None);
            if (gates != null && gates.Length > 0)
            {
                Gate pick = gates[0];
                for (int i = 1; i < gates.Length; i++)
                {
                    if (gates[i] == null) continue;
                    if (pick == null || string.CompareOrdinal(
                            gates[i].GateId ?? "", pick.GateId ?? "") < 0)
                        pick = gates[i];
                }
                seat = PullTowardCentre(pick.transform.position, 3.5f);
                source = $"gate:{pick.GateId ?? pick.name}";
            }
            else
            {
                var spawns = GameObject.FindGameObjectsWithTag("SpawnPoint");
                if (spawns != null && spawns.Length > 0)
                {
                    GameObject nearest = spawns[0];
                    float best = float.MaxValue;
                    for (int i = 0; i < spawns.Length; i++)
                    {
                        if (spawns[i] == null) continue;
                        float d = spawns[i].transform.position.sqrMagnitude;
                        if (d < best) { best = d; nearest = spawns[i]; }
                    }
                    // SpawnPoints sit 12 m OUTSIDE each gate (canon SS7) -- pull well inside.
                    seat = PullTowardCentre(nearest.transform.position, 8f);
                    source = "spawnpoint:" + nearest.name;
                }
                else
                {
                    seat = new Vector3(0f, 0f, 10f);   // near the Heart, on the approach
                    source = "fallback:heart-approach";
                }
            }
            return GroundSnap(seat);
        }

        private static Vector3 PullTowardCentre(Vector3 from, float metres)
        {
            var flat = new Vector3(from.x, 0f, from.z);
            if (flat.sqrMagnitude < 0.01f) return from;
            var dir = (-flat).normalized;   // toward the Heart at (0,0,0)
            return from + dir * metres;
        }

        private static Vector3 GroundSnap(Vector3 seat)
        {
            if (Physics.Raycast(seat + Vector3.up * 20f, Vector3.down, out var hit, 60f))
                return hit.point + Vector3.up * 0.15f;
            return seat + Vector3.up * 0.3f;
        }

        // =====================================================================
        //  VISUAL -- primitives + glint (the DungeonTreasureCache grammar; no
        //  chest prefab ships under Resources/). No announcement chrome.
        // =====================================================================

        private static void BuildVisual(Transform parent)
        {
            // A small satchel-of-plans: leather body, strap, and a rolled scroll on top.
            AddDecor(parent, PrimitiveType.Cube, new Vector3(0f, 0.30f, 0f),
                new Vector3(0.85f, 0.50f, 0.55f), new Color(0.42f, 0.29f, 0.13f));      // leather body
            AddDecor(parent, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f),
                new Vector3(0.90f, 0.12f, 0.60f), new Color(0.86f, 0.71f, 0.30f));      // gilt strap
            AddDecor(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f),
                new Vector3(0.14f, 0.42f, 0.14f), new Color(0.92f, 0.87f, 0.72f),
                euler: new Vector3(0f, 0f, 90f));                                        // rolled plans
            var lightGo = new GameObject("Plans_Glint");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.5f);
            light.range = 8f;
            light.intensity = 1.8f;

            var glint = parent.gameObject.AddComponent<CastlePlansGlint>();
            glint.Bind(light);
        }

        private static void AddDecor(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 scale, Color color, Vector3 euler = default)
        {
            Guard.Try("Progression", "build plans decor", () =>
            {
                var go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = scale;
                if (euler != Vector3.zero) go.transform.localEulerAngles = euler;
                // Strip the primitive's solid collider: the pickup's own sphere TRIGGER is
                // the one collider this prop owns (a solid box at the gate would snag paths).
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    if (sh != null) rend.material = new Material(sh) { color = color };
                    else rend.material.color = color;
                }
            });
        }
    }

    /// <summary>
    /// The drop's GLINT (WO-1013 SS1 "it glints; no banner announces it"): a gentle
    /// bob + slow spin + light pulse. Pure presentation, runtime-only, self-contained
    /// (the IconGlowPulse pattern). Unscaled time so it breathes through any pause.
    /// </summary>
    internal sealed class CastlePlansGlint : MonoBehaviour
    {
        private Light _light;
        private float _baseY;
        private float _baseIntensity = 1.8f;

        public void Bind(Light light)
        {
            _light = light;
            if (_light != null) _baseIntensity = _light.intensity;
        }

        private void Start() => _baseY = transform.position.y;

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var p = transform.position;
            p.y = _baseY + Mathf.Lerp(0f, 0.14f, k);
            transform.position = p;
            transform.Rotate(0f, 30f * Time.unscaledDeltaTime, 0f, Space.World);
            if (_light != null)
                _light.intensity = Mathf.Lerp(_baseIntensity * 0.75f, _baseIntensity * 1.25f, k);
        }
    }
}
