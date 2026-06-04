// =============================================================================
// DragonCinematicFlyby — random cinematic dragon fly-bys across Elarion.
// -----------------------------------------------------------------------------
// Owner direction 2026-05-20: "Being a boss we could have him randomly fly
// across the city. cinematic not interactive."
//
// The Black Dragon (Syndrath the Devourer) is the apex wave-boss; its full
// combat is implemented by DragonBoss.cs (orbits, dive-swoops, fire breath,
// HP-gated phases). Outside the wave encounter we want the dragon to read as
// a looming presence in the sky — a sky cameo, not a fight. This component
// schedules random straight-line fly-bys across the village at altitude:
// enter off-camera, cross the city, exit the far side, destroy.
//
//   1. Every [_minIntervalSeconds, _maxIntervalSeconds] seconds it picks a
//      random heading (0..360°) and launches one flyby.
//   2. The Boss_Dragon prefab is instantiated, its DragonBoss combat brain is
//      DISABLED (reflection — DragonBoss is in this same asmdef but we treat
//      it generically so this file stays narrow), and a tiny FlightDriver
//      MonoBehaviour Lerps it from one edge of the city to the other.
//   3. The Animator's Speed parameter is fed _speed so the wings keep beating.
//   4. If the wave-boss DragonBoss is currently alive (we sweep the scene by
//      reflection), the flyby is SKIPPED — the boss is already on screen.
//
// MODULE ISOLATION. Lives in DeNelle.Village so VillageSceneBuilder (in
// DeNelle.Editor) can add it via reflection (FindType + AddComponent) without
// the editor asmdef referencing this runtime type at compile time — same
// pattern DragonBoss is wired through.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeNelle.Village.Cinematics
{
    /// <summary>
    /// Schedules random cinematic fly-bys of the Black Dragon across Elarion.
    /// Non-interactive sky cameos that foreshadow the apex wave-boss without
    /// interrupting gameplay. Boss combat lives in <c>DragonBoss</c>; this is
    /// JUST for sky cameos.
    /// </summary>
    /// <remarks>
    /// Owner-ratified 2026-05-20. The flyby skips itself while the real
    /// wave-boss <c>DragonBoss</c> is alive in the scene so the cameo never
    /// fights with the encounter.
    /// </remarks>
    public sealed class DragonCinematicFlyby : MonoBehaviour
    {
        // ── Schedule ──────────────────────────────────────────────────────────

        [Header("Schedule")]
        [Tooltip("Minimum seconds between fly-bys.")]
        [SerializeField] private float _minIntervalSeconds = 35f;

        [Tooltip("Maximum seconds between fly-bys.")]
        [SerializeField] private float _maxIntervalSeconds = 80f;

        // ── Flight ────────────────────────────────────────────────────────────

        [Header("Flight")]
        [Tooltip("Altitude (Y, world units) the dragon flies at across the city. " +
                 "Higher than DragonBoss orbit height (22-28) so it reads as 'passing over'.")]
        [SerializeField] private float _altitude = 38f;

        [Tooltip("Total distance travelled across the city, world units. " +
                 "The spawn point sits at -_crossDistance/2 from this transform " +
                 "along the random heading; exit point is +_crossDistance/2.")]
        [SerializeField] private float _crossDistance = 140f;

        [Tooltip("Flight speed in m/s. Fast enough to read as a flyby, slow enough to see.")]
        [SerializeField] private float _speed = 22f;

        // ── Prefab ────────────────────────────────────────────────────────────

        [Header("Prefab")]
        [Tooltip("Path under Assets/ to the Black Dragon prefab. " +
                 "Loaded once via AssetDatabase / Resources fallback at first flyby.")]
        [SerializeField] private string _dragonPrefabPath =
            "Assets/Prefabs/Village/Generated/Boss_Dragon.prefab";

        // ── Animator ──────────────────────────────────────────────────────────
        // Mirrors DragonBoss / DragonAnimatorSetup so the Fly clip blends in.
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");

        // ── Cached prefab + runtime state ─────────────────────────────────────
        private GameObject _prefab;
        private float _nextFlybyAt;
        private bool _scheduled;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Start()
        {
            ScheduleNext();
        }

        private void Update()
        {
            if (!_scheduled) return;
            if (Time.time < _nextFlybyAt) return;

            _scheduled = false;

            if (IsBossDragonAlive())
            {
                // The real wave-boss is on screen — don't double the dragon.
                // Re-roll the schedule and try again next cycle.
                ScheduleNext();
                return;
            }

            BeginFlyby();
            ScheduleNext();
        }

        // ---------------------------------------------------------------------
        // Scheduling
        // ---------------------------------------------------------------------

        /// <summary>
        /// Pushes the next flyby out by a random interval in
        /// [<see cref="_minIntervalSeconds"/>, <see cref="_maxIntervalSeconds"/>].
        /// </summary>
        private void ScheduleNext()
        {
            float lo = Mathf.Max(0.1f, _minIntervalSeconds);
            float hi = Mathf.Max(lo, _maxIntervalSeconds);
            _nextFlybyAt = Time.time + UnityEngine.Random.Range(lo, hi);
            _scheduled = true;
        }

        // ---------------------------------------------------------------------
        // Flyby spawn
        // ---------------------------------------------------------------------

        /// <summary>
        /// Spawns the Boss_Dragon prefab, strips its combat brain, attaches the
        /// straight-line <see cref="FlightDriver"/>, and lets it cross the city.
        /// </summary>
        private void BeginFlyby()
        {
            var prefab = ResolvePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[DragonCinematicFlyby] Boss_Dragon prefab missing at '" +
                                 _dragonPrefabPath + "' — skipping flyby.");
                return;
            }

            // Random heading in the XZ plane.
            float headingDeg = UnityEngine.Random.Range(0f, 360f);
            float headingRad = headingDeg * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Cos(headingRad), 0f, Mathf.Sin(headingRad));

            Vector3 centre = transform.position;
            Vector3 start = new Vector3(
                centre.x - forward.x * (_crossDistance * 0.5f),
                _altitude,
                centre.z - forward.z * (_crossDistance * 0.5f));
            Vector3 end = new Vector3(
                centre.x + forward.x * (_crossDistance * 0.5f),
                _altitude,
                centre.z + forward.z * (_crossDistance * 0.5f));

            // Instantiate as a plain scene object — no PrefabUtility link, so
            // when FlightDriver Destroys it nothing trickles back to the asset.
            var dragon = UnityEngine.Object.Instantiate(prefab, start, Quaternion.identity);
            dragon.name = "DragonCinematicFlyby_Dragon";

            // Strip the combat brain so the cameo cannot orbit / dive / strike.
            DisableDragonBossComponent(dragon);

            // Face the travel direction so the wings line up with motion.
            dragon.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // Drive the wing animation by setting Speed on the Animator
            // (DragonAnimatorSetup blends Idle ↔ Fly off this parameter).
            var animator = dragon.GetComponentInChildren<Animator>();
            // WO-163: only drive "Speed" if the controller declares it (driving an
            // absent param logs an error — once here, and every frame in FlightDriver).
            bool hasSpeed = DragonCinematicFlyby_AnimSpeed.Declares(animator);
            if (animator != null && hasSpeed)
                animator.SetFloat(AnimSpeed, _speed);

            // Attach the straight-line driver.
            float duration = _crossDistance / Mathf.Max(0.01f, _speed);
            var driver = dragon.AddComponent<FlightDriver>();
            driver.Initialise(start, end, duration, animator, _speed, hasSpeed);

            Debug.Log(string.Format(
                "[DragonCinematicFlyby] Fly-by started: heading={0:0}deg duration={1:0.0}s",
                headingDeg, duration));
        }

        // ---------------------------------------------------------------------
        // Prefab + combat-brain helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Lazily resolves the Boss_Dragon prefab. Editor uses AssetDatabase;
        /// builds fall back to Resources.Load on a bare-name lookup so the same
        /// component works without #if UNITY_EDITOR everywhere.
        /// </summary>
        private GameObject ResolvePrefab()
        {
            if (_prefab != null) return _prefab;

#if UNITY_EDITOR
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_dragonPrefabPath);
            if (_prefab != null) return _prefab;
#endif
            // Built-player fallback — caller can drop the prefab under any
            // Resources folder and reference it by name.
            string baseName = System.IO.Path.GetFileNameWithoutExtension(_dragonPrefabPath);
            _prefab = Resources.Load<GameObject>(baseName);
            return _prefab;
        }

        /// <summary>
        /// Disables the <c>DragonBoss</c> combat component on the spawned
        /// instance (if present) so the cinematic flyby never orbits, dives,
        /// or attacks. Resolved by type name so this file does not take a
        /// hard compile-time reference on the combat class.
        /// </summary>
        private static void DisableDragonBossComponent(GameObject instance)
        {
            // Sweep both the root and any children — the prefab puts DragonBoss
            // on the root, but a future refactor could move it anywhere on the rig.
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                var t = b.GetType();
                if (t.Name == "DragonBoss")
                    b.enabled = false;
            }
        }

        /// <summary>
        /// True if the real wave-boss DragonBoss is currently alive in the
        /// scene. Resolved by type name + IsAlive property via reflection so
        /// the cinematic file stays narrow.
        /// </summary>
        private static bool IsBossDragonAlive()
        {
            Type bossType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bossType = asm.GetType("DeNelle.Village.DragonBoss", false);
                if (bossType != null) break;
            }
            if (bossType == null) return false;

            var live = UnityEngine.Object.FindObjectsByType(
                bossType, FindObjectsInactive.Exclude);
            if (live == null || live.Length == 0) return false;

            var isAlive = bossType.GetProperty("IsAlive",
                BindingFlags.Public | BindingFlags.Instance);
            if (isAlive == null) return live.Length > 0; // exists at all -> safe to skip

            for (int i = 0; i < live.Length; i++)
            {
                object v = isAlive.GetValue(live[i], null);
                if (v is bool bv && bv) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Tiny straight-line flight driver attached to a cinematic-flyby dragon
    /// instance. Lerps from <c>start</c> to <c>end</c> over <c>duration</c>
    /// seconds, holds the Animator's Speed parameter steady, then destroys the
    /// instance. Sibling component in the same file so the cinematic owns it
    /// end-to-end without growing a third class file.
    /// </summary>
    public sealed class FlightDriver : MonoBehaviour
    {
        private Vector3 _start;
        private Vector3 _end;
        private float _duration;
        private float _elapsed;
        private Animator _animator;
        private float _speed;
        private bool _ready;
        private bool _hasSpeed;   // WO-163: controller declares "Speed"?

        /// <summary>
        /// Configures the trajectory. <paramref name="duration"/> must be &gt; 0.
        /// </summary>
        public void Initialise(Vector3 start, Vector3 end, float duration,
            Animator animator, float speed, bool hasSpeed)
        {
            _start = start;
            _end = end;
            _duration = Mathf.Max(0.01f, duration);
            _animator = animator;
            _speed = speed;
            _hasSpeed = hasSpeed;
            _elapsed = 0f;
            transform.position = _start;
            // Face travel direction.
            Vector3 dir = _end - _start; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-5f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            transform.position = Vector3.Lerp(_start, _end, t);

            // Keep the wings beating across the whole pass.
            if (_animator != null && _hasSpeed)
                _animator.SetFloat(DragonCinematicFlyby_AnimSpeed.Hash, _speed);

            if (t >= 1f)
                UnityEngine.Object.Destroy(gameObject);
        }
    }

    /// <summary>
    /// Tiny holder for the cached Animator "Speed" hash so both
    /// <see cref="DragonCinematicFlyby"/> and <see cref="FlightDriver"/> can
    /// reference one stable value without re-hashing per frame.
    /// </summary>
    internal static class DragonCinematicFlyby_AnimSpeed
    {
        public static readonly int Hash = Animator.StringToHash("Speed");

        /// <summary>
        /// WO-163: true when <paramref name="animator"/>'s controller declares the
        /// "Speed" param. Driving an absent param logs "Parameter does not exist"
        /// (every frame in FlightDriver). Scanned once at fly-by spawn.
        /// </summary>
        public static bool Declares(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (var p in animator.parameters)
                if (p.nameHash == Hash) return true;
            return false;
        }
    }
}
