// =============================================================================
// WaveManager — the Avalon village wave loop (Week-4 slice).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/waves/ -> WaveManager.cs.
// Port spec Part 5 Week 4: "countdown timer between waves, then spawn per
// data/waves.json. Wave 1 = 8 Hollow Walkers from the north gate."
//
// RESPONSIBILITIES
//   1. Load the canonical waves.json + enemies.json (WaveDataLoader).
//   2. Run a Prepare-Phase countdown before each wave (WaveDef.CountdownSeconds).
//   3. On countdown-zero, spawn the wave's batches at the WaveSpawnPoints — one
//      Enemy MonoBehaviour per spawned enemy, configured from the EnemyDef.
//   4. Watch every live enemy for an INNER-WALL-RING BREACH; when one or more
//      enemies cross the ring, hand the breaching roster to the ATB scene via
//      SceneRouter.GoBattle(BattleParams) and pause the wave loop.
//   5. When the ATB scene returns to the Village, resume the loop.
//
// This is a SUB-MonoBehaviour of the Village scene (port spec Part 3:
// "controller orchestrates ... wave manager ... Sub-systems each have their own
// MonoBehaviour"). VillageController WIRING IS THE INTEGRATOR'S JOB — this file
// does NOT touch VillageController. The integrator adds a WaveManager component
// to the Village scene, drops the WaveSpawnPoints + Heart into its serialized
// fields (or lets it auto-discover them), and the loop runs itself.
//
// NAVMESH: spawned enemies use NavMeshAgents. ** The village scene needs a baked
// NavMesh ** for enemies to move — see docs/port-notes/week4-waves.md.
//
// BREACH HAND-OFF: matches the REAL SceneRouter API —
//   SceneRouter.GoBattle(new BattleParams { Wave, BreachedIds, ParticipatingPetIds })
// GoBattle stashes the params on SceneRouter.PendingBattle and fades into the
// ATBBattle scene; BattleController reads PendingBattle on the far side. After
// the battle BattleController.ReturnAfterResult fades back to the Village scene,
// at which point a fresh WaveManager.Start() resumes the loop.
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    /// <summary>The phase the wave loop is currently in.</summary>
    public enum WavePhase
    {
        /// <summary>Data not loaded yet / loop not started.</summary>
        Idle = 0,
        /// <summary>Counting down the calm Prepare Phase before the next wave.</summary>
        Countdown = 1,
        /// <summary>Spawning + fighting the active wave's enemies.</summary>
        Active = 2,
        /// <summary>A breach handed off to the ATB scene — the loop is paused.</summary>
        Breached = 3,
        /// <summary>Every wave in the schedule has been cleared.</summary>
        Complete = 4,
    }

    /// <summary>A UnityEvent carrying the countdown seconds remaining (HUD binds this).</summary>
    [System.Serializable]
    public sealed class WaveCountdownEvent : UnityEvent<float> { }

    /// <summary>A UnityEvent carrying a wave ordinal (1-based).</summary>
    [System.Serializable]
    public sealed class WaveNumberEvent : UnityEvent<int> { }

    /// <summary>A UnityEvent carrying the apex flying boss that just spawned.</summary>
    [System.Serializable]
    public sealed class WaveBossEvent : UnityEvent<DragonBoss> { }

    /// <summary>
    /// Drives the village wave loop: countdown, spawn, breach detection, ATB
    /// hand-off. A self-contained sub-system MonoBehaviour for the Village scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveManager : MonoBehaviour
    {
        // ── Inspector wiring ──────────────────────────────────────────────────

        [Header("Scene refs (wire in the inspector, or leave blank to auto-find)")]
        [Tooltip("The Heart — enemies march toward it; the breach ring is centred on it. " +
                 "Left blank: WaveManager finds the HeartController in the scene.")]
        [SerializeField] private HeartController _heart;

        [Tooltip("The wave spawn markers beyond each gate. Left empty: WaveManager " +
                 "finds every WaveSpawnPoint in the scene at Start.")]
        [SerializeField] private List<WaveSpawnPoint> _spawnPoints = new List<WaveSpawnPoint>();

        [Tooltip("Parent transform spawned enemies are parented under (keeps the hierarchy tidy). " +
                 "Left blank: enemies parent under this WaveManager's transform.")]
        [SerializeField] private Transform _enemyRoot;

        [Tooltip("Prefab instantiated per spawned enemy. Must carry an Enemy + NavMeshAgent. " +
                 "Left blank: WaveManager builds a primitive-capsule placeholder so the loop " +
                 "still runs before the KayKit skeleton prefab exists.")]
        [SerializeField] private Enemy _enemyPrefab;

        [Tooltip("Apex flying-boss prefab (Boss_Dragon — carries a DragonBoss). Spawned for a " +
                 "wave whose waves.json entry declares an 'apexBoss'. Left blank: an apex wave " +
                 "logs an error and is treated as cleared (the loop never stalls).")]
        [SerializeField] private DragonBoss _apexBossPrefab;

        [Header("Wave scaling (DEF-59)")]
        [Tooltip("Optional SO that scales enemy HP/speed/damage as wave number increases. " +
                 "Create via Assets → Create → Defenders/Waves/Wave Scaling Curve. " +
                 "Left blank: all enemies spawn at their base stats from enemies.json.")]
        [SerializeField] private WaveScalingCurve _scalingCurve;

        [Header("Group spawner (DEF-21)")]
        [Tooltip("Optional EnemyGroupSpawner component (child of this object). " +
                 "When assigned alongside _waveGroupSequence, each wave also spawns " +
                 "the matching WaveEnemyGroup asset — complementing (not replacing) " +
                 "the JSON batch system. Leave blank to use JSON batches only.")]
        [SerializeField] private EnemyGroupSpawner _groupSpawner;

        [Tooltip("One WaveEnemyGroup asset per wave slot (index 0 = wave 1, index 1 = wave 2, …). " +
                 "Shorter than the wave count is fine — missing slots are skipped. " +
                 "Requires _groupSpawner to be wired.")]
        [SerializeField] private System.Collections.Generic.List<WaveEnemyGroup> _waveGroupSequence
            = new System.Collections.Generic.List<WaveEnemyGroup>();

        [Header("Breach detection")]
        [Tooltip("Radius (world units) of the inner wall ring around the Heart. An enemy that " +
                 "crosses INSIDE this ring counts as a breach. Tune to sit just inside the " +
                 "curtain wall (WallLayout.WallHalfZ ~ 21u; the inner ring sits well within).")]
        [SerializeField] private float _innerRingRadius = 9f;

        [Tooltip("Seconds the manager waits after the wave starts before arming breach " +
                 "detection — lets enemies clear the spawn point first.")]
        [SerializeField] private float _breachArmDelay = 0.5f;

        [Header("Loop control")]
        [Tooltip("Start the wave loop automatically on Start(). Off: call BeginLoop() yourself.")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("Start the loop from this wave id (1 = the first wave). Dev override.")]
        [SerializeField, Min(1)] private int _startWave = 1;

        [Header("Performance budget (DEF-48)")]
        [Tooltip("Hard cap on simultaneously live enemies. 0 = no cap. " +
                 "SpawnBatch stalls until an enemy dies when the cap is hit. " +
                 "Recommended values: 4 (early), 6 (mid), 8 (late), 5 (boss wave).")]
        [SerializeField, Min(0)] private int _maxSimultaneousEnemies = 8;

        // ── Events — HUD / Heart subscribe in OnEnable, unsubscribe in OnDisable ──

        [Header("Events")]
        [Tooltip("Fires every frame during the Prepare Phase with the seconds remaining.")]
        public WaveCountdownEvent OnCountdownTick = new WaveCountdownEvent();

        [Tooltip("Fires when a wave's enemies begin spawning. Arg = the wave id.")]
        public WaveNumberEvent OnWaveStarted = new WaveNumberEvent();

        [Tooltip("Fires when every enemy of a wave is dead / consumed. Arg = the wave id.")]
        public WaveNumberEvent OnWaveCleared = new WaveNumberEvent();

        [Tooltip("Fires when one or more enemies breach the inner ring. Arg = the wave id.")]
        public WaveNumberEvent OnBreach = new WaveNumberEvent();

        [Tooltip("Fires when an apex wave releases its flying boss. Arg = the spawned DragonBoss " +
                 "— bind the boss HP bar / camera framing / Heart threat state to it.")]
        public WaveBossEvent OnApexBossSpawned = new WaveBossEvent();

        // ── Runtime state ─────────────────────────────────────────────────────

        private WaveSchedule _schedule;
        private EnemyCatalog _enemyCatalog;
        private readonly List<Enemy> _liveEnemies = new List<Enemy>();
        private readonly List<Enemy> _breachRoster = new List<Enemy>();

        // Failsafe against a stuck enemy freezing the wave's clear gate (the recurring
        // "wave won't advance" bug — clear requires _liveEnemies.Count == 0). Tracks each
        // enemy's best distance toward the Heart; culls one that makes no progress for
        // StuckTimeout so an off-mesh / boxed-in Hollow One can't hang the wave forever.
        private readonly Dictionary<Enemy, float> _enemyBestSqr   = new Dictionary<Enemy, float>();
        private readonly Dictionary<Enemy, float> _enemyStuckTime = new Dictionary<Enemy, float>();
        private const float StuckTimeout = 12f;

        /// <summary>The apex flying boss for the current wave (null when not an apex wave / dead).</summary>
        private DragonBoss _liveApexBoss;

        private WavePhase _phase = WavePhase.Idle;
        private int _currentWaveId;
        private float _countdownRemaining;
        private float _breachArmTimer;
        private bool _breachArmed;
        private int _spawnInstanceCounter;

        /// <summary>The phase the wave loop is in.</summary>
        public WavePhase Phase => _phase;

        /// <summary>The wave currently counting down / active (1-based; 0 before the loop starts).</summary>
        public int CurrentWaveId => _currentWaveId;

        /// <summary>Seconds remaining in the Prepare-Phase countdown (0 when not counting down).</summary>
        public float CountdownRemaining => _countdownRemaining;

        /// <summary>Live enemies currently on the field.</summary>
        public IReadOnlyList<Enemy> LiveEnemies => _liveEnemies;

        /// <summary>The apex flying boss on the field, or null when no apex wave is live.</summary>
        public DragonBoss LiveApexBoss => _liveApexBoss;

        /// <summary>The Heart the wave loop marches enemies at (resolved at BeginLoop).</summary>
        public HeartController Heart => _heart;

        // ── Patricia Light hooks (WO-47) ──────────────────────────────────────
        // The breach-time "Defend the Tower" shooter (PatriciaLightMode) needs to
        // spawn real Enemy instances using the SAME prefab + roster path the wave
        // loop uses. These thin accessors + the public SpawnEnemyForExternalMode
        // helper let it reuse SpawnOne's instantiate / NavMesh-sample / Configure
        // logic without duplicating it or making fields public. PatriciaLightMode
        // lives in DeNelle.Village too, so no asmdef boundary is crossed.

        /// <summary>
        /// The enemy prefab the wave loop instantiates (null = the loop builds a
        /// primitive-capsule placeholder via <see cref="BuildPlaceholderEnemy"/>).
        /// Exposed so the breach-time Defend-the-Tower mode can reuse the same body.
        /// </summary>
        public Enemy EnemyPrefab => _enemyPrefab;

        /// <summary>
        /// Loads the canonical enemy catalog if it is not already loaded and
        /// returns it (null on load failure). PatriciaLightMode uses this to pull
        /// an <see cref="EnemyDef"/> from the same enemies.json the wave loop uses.
        /// </summary>
        public async UniTask<EnemyCatalog> GetEnemyCatalogAsync()
        {
            if (_enemyCatalog == null)
                _enemyCatalog = await WaveDataLoader.LoadEnemiesAsync();
            return _enemyCatalog;
        }

        /// <summary>
        /// Spawns one Enemy at <paramref name="worldPos"/> for an external mode
        /// (the breach-time Defend-the-Tower shooter), reusing the wave loop's
        /// instantiate / NavMesh-sample / Configure path. The caller owns the
        /// returned enemy's lifecycle (this does NOT add it to the wave loop's
        /// live-enemy roster or breach watch). Returns null if no def is given.
        /// </summary>
        /// <param name="def">The enemy stat block (from <see cref="GetEnemyCatalogAsync"/>).</param>
        /// <param name="worldPos">Where to spawn — snapped to the nearest NavMesh.</param>
        /// <param name="heart">The transform the enemy marches at.</param>
        /// <param name="instanceId">Stable per-instance id for attribution / events.</param>
        public Enemy SpawnEnemyForExternalMode(EnemyDef def, Vector3 worldPos, Transform heart, string instanceId)
        {
            if (def == null) return null;

            Vector3 pos = worldPos;
            if (UnityEngine.AI.NavMesh.SamplePosition(
                    pos, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                pos = hit.position;

            Vector3 toHeart = heart != null ? (heart.position - pos) : Vector3.forward;
            toHeart.y = 0f;
            Quaternion rot = Quaternion.LookRotation(
                toHeart.sqrMagnitude > 0.0001f ? toHeart : Vector3.forward);

            Enemy enemy = _enemyPrefab != null
                ? Instantiate(_enemyPrefab, pos, rot, _enemyRoot)
                : BuildPlaceholderEnemy(pos, rot);

            // The hero/pet target sweeps find enemies via GetComponentInParent
            // <IDamageable>, which resolves to EnemyDamageable. The placeholder
            // capsule (and some prefabs) may not carry it — add it so the
            // Defend-the-Tower hero can actually acquire + damage this enemy.
            if (enemy.GetComponent<EnemyDamageable>() == null)
                enemy.gameObject.AddComponent<EnemyDamageable>();

            enemy.Configure(instanceId, def, heart);
            return enemy;
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Start()
        {
            if (_autoStart) BeginLoop().Forget();
        }

        private void Update()
        {
            switch (_phase)
            {
                case WavePhase.Countdown:
                    TickCountdown();
                    break;
                case WavePhase.Active:
                    TickActiveWave();
                    break;
            }
        }

        // =====================================================================
        //  Loop entry
        // =====================================================================

        /// <summary>
        /// Loads the canonical wave data and starts the loop at <see cref="_startWave"/>.
        /// Safe to call again after an ATB return to resume (re-loads are cheap +
        /// the loop simply re-enters the countdown for the un-cleared wave).
        /// Returns a <see cref="UniTask"/> — never <c>async void</c> (port spec Part 3).
        /// </summary>
        public async UniTask BeginLoop()
        {
            ResolveSceneRefs();

            if (_schedule == null)
                _schedule = await WaveDataLoader.LoadWavesAsync();
            if (_enemyCatalog == null)
                _enemyCatalog = await WaveDataLoader.LoadEnemiesAsync();

            if (_schedule == null || _enemyCatalog == null)
            {
                Debug.LogError("[WaveManager] Wave data failed to load — the wave loop cannot run.");
                _phase = WavePhase.Idle;
                return;
            }

            EnterCountdown(_startWave);
            Debug.Log($"[WaveManager] Loop armed — wave {_startWave}, countdown {_countdownRemaining:F1}s.");
        }

        /// <summary>
        /// Finds the Heart + spawn points in the scene when the inspector left
        /// them blank. The integrator may instead wire them by hand.
        /// </summary>
        private void ResolveSceneRefs()
        {
            if (_heart == null)
                _heart = FindAnyObjectByType<HeartController>();

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                _spawnPoints = new List<WaveSpawnPoint>(
                    FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None));
            }

            if (_enemyRoot == null) _enemyRoot = transform;
        }

        // =====================================================================
        //  Countdown phase
        // =====================================================================

        /// <summary>Enters the Prepare-Phase countdown for wave <paramref name="waveId"/>.</summary>
        private void EnterCountdown(int waveId)
        {
            WaveDef wave = _schedule.Find(waveId);
            if (wave == null)
            {
                // No such wave — the schedule is exhausted.
                _phase = WavePhase.Complete;
                Debug.Log($"[WaveManager] All {_schedule.Waves.Count} waves cleared — schedule complete.");
                return;
            }

            _currentWaveId = waveId;
            _enemyBestSqr.Clear();    // fresh stuck-tracking per wave
            _enemyStuckTime.Clear();
            // The between-wave build window scales with the player's chosen
            // difficulty: the canonical WaveDef.CountdownSeconds (45 s first
            // wave, 300 s later) is multiplied by the DifficultyTuning factor so
            // the owner targets land — Easy ~10 min, Normal ~5 min, Hard ~3 min
            // between waves. The multiplier is derived from the base seconds,
            // never hard-coded (see DifficultyTuning).
            _countdownRemaining = Mathf.Max(0f, ScaledCountdown(wave.CountdownSeconds));
            _phase = WavePhase.Countdown;
            OnCountdownTick.Invoke(_countdownRemaining);
        }

        /// <summary>
        /// Scales a wave's authored <paramref name="baseCountdown"/> by the
        /// difficulty the save records. Reads <see cref="GameState.Difficulty"/>
        /// through <see cref="GameStateService"/>; if Core is not bootstrapped
        /// (no service) it falls back to Normal so the loop is never blocked.
        /// </summary>
        private static float ScaledCountdown(float baseCountdown)
        {
            var svc = GameStateService.Instance;
            Difficulty difficulty = (svc != null && svc.State != null)
                ? svc.State.Difficulty
                : Difficulty.Normal;
            return baseCountdown * DifficultyTuning.CountdownMultiplier(difficulty);
        }

        private void TickCountdown()
        {
            _countdownRemaining -= Time.deltaTime;
            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                OnCountdownTick.Invoke(0f);
                StartWave(_currentWaveId);
            }
            else
            {
                OnCountdownTick.Invoke(_countdownRemaining);
            }
        }

        /// <summary>
        /// Owner-facing: skip the remaining Prepare-Phase and start the
        /// current wave immediately. Used by the HUD's "Trigger Wave" button
        /// and the AdminOverlay's debug shortcut. Idle / Complete phases
        /// route into the next countdown so the call is always meaningful.
        /// </summary>
        public void ForceBeginNextWave()
        {
            switch (_phase)
            {
                case WavePhase.Countdown:
                    _countdownRemaining = 0f;
                    OnCountdownTick.Invoke(0f);
                    StartWave(_currentWaveId);
                    break;
                case WavePhase.Active:
                    Debug.Log("[WaveManager] ForceBeginNextWave called during active wave — ignored (current wave already running).");
                    break;
                case WavePhase.Idle:
                case WavePhase.Complete:
                default:
                    Debug.Log("[WaveManager] ForceBeginNextWave kicking the wave loop from " + _phase);
                    BeginLoop().Forget();
                    break;
            }
        }

        // =====================================================================
        //  Active wave — spawn + breach watch
        // =====================================================================

        /// <summary>Begins spawning wave <paramref name="waveId"/>'s enemies.</summary>
        private void StartWave(int waveId)
        {
            WaveDef wave = _schedule.Find(waveId);
            if (wave == null) { EnterCountdown(waveId + 1); return; }

            _phase = WavePhase.Active;
            _breachArmed = false;
            _breachArmTimer = 0f;
            _breachRoster.Clear();
            _liveApexBoss = null;
            OnWaveStarted.Invoke(waveId);

            // An apex (flying-boss) wave drives the Heart's Boss threat state;
            // a normal wave only raises it to Vigilant.
            if (_heart != null)
                _heart.SetState(wave.IsApexBossWave ? HeartState.Boss : HeartState.Vigilant);

            // Each batch spawns on its own delayed coroutine-equivalent UniTask.
            if (wave.Enemies != null)
            {
                foreach (WaveBatch batch in wave.Enemies)
                {
                    if (batch != null) SpawnBatch(batch).Forget();
                }
            }

            // DEF-21: group spawner — if a WaveEnemyGroup asset is assigned for
            // this wave slot, spawn it alongside the JSON batches. Both systems
            // complement each other; leave waves.json entries empty for group-only waves.
            int groupIdx = waveId - 1;
            if (_groupSpawner != null
                && _waveGroupSequence != null
                && groupIdx >= 0
                && groupIdx < _waveGroupSequence.Count
                && _waveGroupSequence[groupIdx] != null)
            {
                WaveSpawnPoint pt = (_spawnPoints != null && _spawnPoints.Count > 0)
                    ? _spawnPoints[0] : null;
                Vector3 spawnPos = pt != null
                    ? pt.transform.position
                    : transform.position;

                List<Enemy> groupEnemies = _groupSpawner.SpawnGroup(
                    _waveGroupSequence[groupIdx],
                    spawnPos,
                    _heart != null ? _heart.transform : null,
                    _enemyRoot,
                    _currentWaveId,
                    ref _spawnInstanceCounter);

                foreach (Enemy e in groupEnemies)
                {
                    if (e == null) continue;
                    e.Died          += HandleEnemyDied;
                    e.ReachedHeart  += HandleEnemyReachedHeart;
                    _liveEnemies.Add(e);
                }
            }

            // A boss, if the wave names one, releases immediately at the north spawn.
            if (!string.IsNullOrEmpty(wave.Boss))
            {
                SpawnBatch(new WaveBatch
                {
                    Type = wave.Boss, Count = 1, SpawnPoint = "spawn-0", Delay = 0f, Interval = 0f,
                }).Forget();
            }

            // An APEX wave fields the kinematic flying boss (the dragon). Unlike
            // wave.Boss above, this is NOT a NavMesh enemy from enemies.json — it
            // is the Boss_Dragon prefab driven by DragonBoss. Released at once so
            // the dragon is aloft the moment the apex wave begins.
            if (wave.IsApexBossWave)
                SpawnApexBoss(wave.ApexBoss);
        }

        /// <summary>
        /// Spawns the apex flying boss for an apex wave: instantiates the
        /// <see cref="_apexBossPrefab"/> over the Heart and calls
        /// <see cref="DragonBoss.Configure"/> with the Heart anchor + the wave's
        /// HP. The dragon owns its own kinematic flight — no NavMesh, no spawn
        /// point. A missing prefab logs an error; the wave then clears normally
        /// (its enemy batches, if any) so the loop never stalls.
        /// </summary>
        private void SpawnApexBoss(ApexBossDef boss)
        {
            if (boss == null) return;

            if (_apexBossPrefab == null)
            {
                // The live Village.unity predates the builder's _apexBossPrefab wiring
                // (WireApexBossPrefab), so the serialized reference is null and no dragon
                // ever spawns. Corruption-safe fallback (no risky village rebake): load the
                // Boss_Dragon prefab copied into Resources/Enemies.
                _apexBossPrefab = Resources.Load<DragonBoss>("Enemies/Boss_Dragon");
                if (_apexBossPrefab == null)
                {
                    Debug.LogError(
                        "[WaveManager] Apex wave has no _apexBossPrefab AND no " +
                        "Resources/Enemies/Boss_Dragon fallback — no dragon will spawn.");
                    return;
                }
                Debug.Log("[WaveManager] _apexBossPrefab was null (stale scene) — using the " +
                          "Resources/Enemies/Boss_Dragon fallback so the apex dragon flies.");
            }

            // Spawn the dragon at cruise height above the Heart so it begins its
            // orbit immediately; DragonBoss.Configure re-seeds its anchor + HP.
            Transform heartT = _heart != null ? _heart.transform : null;
            Vector3 spawnPos = (heartT != null ? heartT.position : transform.position)
                               + new Vector3(0f, 22f, 0f);

            DragonBoss dragon = Instantiate(_apexBossPrefab, spawnPos, Quaternion.identity, _enemyRoot);

            string bossId = !string.IsNullOrEmpty(boss.Id)
                ? boss.Id
                : $"wave{_currentWaveId}-apex-boss";
            dragon.Configure(bossId, heartT, boss.Hp);

            dragon.Died += HandleApexBossDied;
            _liveApexBoss = dragon;
            OnApexBossSpawned.Invoke(dragon);

            Debug.Log(
                $"[WaveManager] Apex wave {_currentWaveId} — released flying boss '{bossId}' " +
                $"(maxHp {(boss.Hp > 0f ? boss.Hp.ToString() : "prefab default")}).");
        }

        /// <summary>
        /// Spawns one batch — waits the batch delay, then releases
        /// <see cref="WaveBatch.Count"/> enemies at the named spawn point,
        /// <see cref="WaveBatch.Interval"/> seconds apart.
        /// </summary>
        private async UniTask SpawnBatch(WaveBatch batch)
        {
            EnemyDef def = _enemyCatalog.Find(batch.Type);
            if (def == null)
            {
                Debug.LogError($"[WaveManager] Wave batch references unknown enemy '{batch.Type}'.");
                return;
            }

            WaveSpawnPoint point = FindSpawnPoint(batch.SpawnPoint);
            if (point == null)
            {
                Debug.LogError(
                    $"[WaveManager] No WaveSpawnPoint '{batch.SpawnPoint}' in the scene — " +
                    "batch skipped. Place the spawn markers (see docs/port-notes/week4-waves.md).");
                return;
            }

            // DEF-52: telegraph ring on the ground while the batch delay ticks so
            // the player gets a "something's coming" warning at the spawn point.
            // Only shown when there is a meaningful delay to warn about (≥0.5 s).
            PooledVfx telegraph = null;
            if (batch.Delay >= 0.5f)
                telegraph = VfxPool.GetTelegraph(point.transform.position);

            if (batch.Delay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(batch.Delay));

            VfxPool.ReturnTelegraph(telegraph);
            telegraph = null;

            for (int i = 0; i < Mathf.Max(0, batch.Count); i++)
            {
                // The wave may have been breached / cleared while this batch
                // was still draining — stop releasing if so.
                if (_phase != WavePhase.Active) return;

                // DEF-48: simultaneous enemy cap — stall the spawn if we're at capacity.
                // _liveEnemies is pruned in TickActiveWave so the count is current.
                if (_maxSimultaneousEnemies > 0 && _liveEnemies.Count >= _maxSimultaneousEnemies)
                {
                    await UniTask.WaitUntil(
                        () => _liveEnemies.Count < _maxSimultaneousEnemies || _phase != WavePhase.Active);
                    if (_phase != WavePhase.Active) return;
                }

                SpawnOne(def, point);

                if (batch.Interval > 0f && i < batch.Count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(batch.Interval));
            }
        }

        /// <summary>Instantiates + configures one enemy at <paramref name="point"/>.</summary>
        private void SpawnOne(EnemyDef def, WaveSpawnPoint point)
        {
            Vector3 heading = point.HeadingToGate.sqrMagnitude > 0.0001f
                ? point.HeadingToGate.normalized : Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(heading);

            // Spread each enemy around the spawn marker (lateral + a little depth) so a
            // batch advances as a loose MOB toward the gate/tree instead of stacking on
            // one point and marching single-file. Perpendicular = lateral to the heading.
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);
            Vector3 pos = point.transform.position
                        + lateral * UnityEngine.Random.Range(-4.5f, 4.5f)
                        + heading * UnityEngine.Random.Range(-3f, 3f);

            // Snap the spawn position to the nearest NavMesh sample so a
            // slightly-off-mesh spawn point doesn't strand the enemy off-mesh
            // (NavMeshAgent.isOnNavMesh would stay false → enemy never moves).
            // Sample within a generous 8 m radius; bail to the raw position if
            // we somehow have no NavMesh nearby at all.
            if (UnityEngine.AI.NavMesh.SamplePosition(
                    pos, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                pos = hit.position;

            Enemy enemy = _enemyPrefab != null
                ? Instantiate(_enemyPrefab, pos, rot, _enemyRoot)
                : BuildPlaceholderEnemy(pos, rot);

            string instanceId = $"wave{_currentWaveId}-{def.Id}-{_spawnInstanceCounter++}";
            Transform heartT = _heart != null ? _heart.transform : null;
            enemy.Configure(instanceId, def, heartT);

            // DEF-59: apply wave-scaling multipliers if a curve SO is assigned.
            if (_scalingCurve != null)
            {
                enemy.ApplyWaveScaling(
                    _scalingCurve.HpMultiplier(_currentWaveId),
                    _scalingCurve.SpeedMultiplier(_currentWaveId),
                    _scalingCurve.DamageMultiplier(_currentWaveId));
            }

            enemy.Died += HandleEnemyDied;
            enemy.ReachedHeart += HandleEnemyReachedHeart;
            _liveEnemies.Add(enemy);
        }

        /// <summary>
        /// Builds a primitive-capsule stand-in when no KayKit enemy prefab is
        /// wired yet, so the wave loop is testable before the skeleton prefab
        /// exists. The integrator should assign a real prefab built from
        /// Assets/Models/KayKit/enemies/Skeleton_Minion.glb (see week4-waves.md).
        /// </summary>
        private Enemy BuildPlaceholderEnemy(Vector3 pos, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Enemy (placeholder)";
            go.transform.SetParent(_enemyRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            // The capsule's own collider would block its own contact probe — make
            // it a trigger so SphereCast ignores it (QueryTriggerInteraction.Ignore).
            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;
            go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            return go.AddComponent<Enemy>();
        }

        /// <summary>
        /// Runs every frame while a wave is Active: arms the breach window, then
        /// watches each live enemy for an inner-ring crossing and detects clear.
        /// </summary>
        private void TickActiveWave()
        {
            // Arm breach detection a beat after the wave starts.
            if (!_breachArmed)
            {
                _breachArmTimer += Time.deltaTime;
                if (_breachArmTimer >= _breachArmDelay) _breachArmed = true;
            }

            // Prune destroyed enemies (Die() destroys the GameObject -> null ref).
            _liveEnemies.RemoveAll(e => e == null);

            // FAILSAFE: cull a stuck enemy (off-mesh / boxed-in / unpathable) that makes
            // no progress toward the Heart for StuckTimeout, so a single Hollow One can't
            // hang the wave forever and block the next wave's countdown.
            if (_heart != null)
            {
                Vector3 hpos = _heart.transform.position;
                for (int i = _liveEnemies.Count - 1; i >= 0; i--)
                {
                    Enemy e = _liveEnemies[i];
                    if (e == null || e.IsDead) continue;
                    float sqr = Vector3.ProjectOnPlane(e.transform.position - hpos, Vector3.up).sqrMagnitude;
                    if (!_enemyBestSqr.TryGetValue(e, out float best) || sqr < best - 0.25f)
                    {
                        _enemyBestSqr[e]   = sqr;   // got closer → reset the stuck timer
                        _enemyStuckTime[e] = 0f;
                    }
                    else
                    {
                        float t = (_enemyStuckTime.TryGetValue(e, out float tv) ? tv : 0f) + Time.deltaTime;
                        _enemyStuckTime[e] = t;
                        if (t >= StuckTimeout)
                        {
                            Debug.LogWarning($"[WaveManager] Culling stuck enemy '{e.name}' (no progress to the Heart for {StuckTimeout:F0}s) so wave {_currentWaveId} can advance.");
                            _enemyBestSqr.Remove(e);
                            _enemyStuckTime.Remove(e);
                            e.Kill();   // fires Died -> HandleEnemyDied removes it from _liveEnemies
                        }
                    }
                }
                _liveEnemies.RemoveAll(e => e == null);
            }

            if (_breachArmed && _heart != null)
            {
                Vector3 heartPos = _heart.transform.position;
                float ringSqr = _innerRingRadius * _innerRingRadius;
                for (int i = 0; i < _liveEnemies.Count; i++)
                {
                    Enemy e = _liveEnemies[i];
                    if (e == null || e.IsDead || _breachRoster.Contains(e)) continue;

                    float planarSqr = Vector3.ProjectOnPlane(
                        e.transform.position - heartPos, Vector3.up).sqrMagnitude;
                    if (planarSqr <= ringSqr)
                        _breachRoster.Add(e);
                }

                if (_breachRoster.Count > 0)
                {
                    TriggerBreach();
                    return;
                }
            }

            // Wave is cleared when no enemies remain on the field. An apex wave
            // additionally holds open until the flying boss is down — the dragon
            // is not in _liveEnemies (it owns kinematic flight, not a NavMesh
            // agent), so its life is tracked separately via _liveApexBoss.
            bool apexBossStillUp = _liveApexBoss != null && !_liveApexBoss.IsDead;
            if (_liveEnemies.Count == 0 && !apexBossStillUp)
            {
                CompleteWave();
            }
        }

        // =====================================================================
        //  Wave clear
        // =====================================================================

        /// <summary>Wave cleared without a breach — award crystals then advance.</summary>
        private void CompleteWave()
        {
            int cleared = _currentWaveId;
            if (_heart != null) _heart.SetState(HeartState.Serene);

            AwardWaveCrystals(cleared);

            OnWaveCleared.Invoke(cleared);

            // WO2: analytics.
            DeNelle.Core.Analytics.EventTracker.Track("wave_completed", new
            {
                waveId     = cleared,
                liveEnemiesKilled = 0, // filled by future combat telemetry pass
            });

            EnterCountdown(cleared + 1);
        }

        /// <summary>
        /// Evaluates boss-wave drops and active-event bonuses from <see cref="ServerConfig"/>
        /// and credits Aether Crystals to <see cref="CrystalEconomy"/> if applicable.
        /// Drop chance and ranges are all backend-controlled — no rebuild needed to tune.
        /// </summary>
        private void AwardWaveCrystals(int waveId)
        {
            if (CrystalEconomy.Instance == null) return;

            var cfg = GameStateService.Instance != null
                ? GameStateService.Instance.ServerConfig
                : ServerConfig.Default;

            int totalAward = 0;

            // ── Boss-wave drop (every Nth wave, chance-based) ─────────────────
            if (waveId % cfg.BossInterval == 0)
            {
                if (UnityEngine.Random.value <= cfg.DropChance)
                {
                    int drop = UnityEngine.Random.Range(cfg.DropMin, cfg.DropMax + 1);
                    totalAward += drop;
                    Debug.Log($"[WaveManager] Boss-wave crystal drop — wave {waveId} awarded {drop} Aether Crystal(s). " +
                              $"(chance={cfg.DropChance:P0}, range={cfg.DropMin}–{cfg.DropMax})");
                }
                else
                {
                    Debug.Log($"[WaveManager] Boss-wave crystal drop — wave {waveId} missed. " +
                              $"(chance={cfg.DropChance:P0})");
                }
            }

            // ── Special event bonus (every wave while event is active) ────────
            if (cfg.IsEventActive() && cfg.EventBonus > 0)
            {
                totalAward += cfg.EventBonus;
                Debug.Log($"[WaveManager] Event bonus '{cfg.ActiveEventName}' — +{cfg.EventBonus} crystal(s) on wave {waveId}.");
            }

            if (totalAward > 0)
                CrystalEconomy.Instance.AddCrystals(totalAward);
        }

        // =====================================================================
        //  Breach -> ATB hand-off
        // =====================================================================

        /// <summary>
        /// One or more enemies crossed the inner ring. Pauses the loop and offers
        /// the breach CHOICE (WO-47): the player picks the ATB "Last Stand" or the
        /// real-time "Defend the Tower" shooter (PatriciaLightMode). The choice
        /// overlay is built in code; if it cannot be created the breach falls back
        /// to the ATB hand-off so a breach is never a dead end.
        /// </summary>
        private void TriggerBreach()
        {
            _phase = WavePhase.Breached;
            OnBreach.Invoke(_currentWaveId);
            if (_heart != null) _heart.SetState(HeartState.Critical);

            // Snapshot the breaching roster's count + the breaching enemy refs
            // BEFORE either branch consumes them. PatriciaLightMode runs IN the
            // village (no scene change), so it borrows the same Heart + a fresh
            // assault rather than the ATB roster; the ATB branch keeps using the
            // breach roster verbatim (see EnterAtbBattle).
            int breachCount = _breachRoster.Count;

            // WO-47 breach choice: show the code-built Last-Stand / Defend prompt.
            // It returns false if it could not be created (no UIDocument host /
            // missing PanelSettings) — in that case fall through to the ATB path
            // immediately so the breach still resolves.
            bool shown = BreachChoiceOverlay.Show(
                heart: _heart,
                onLastStand: EnterAtbBattle,
                onDefendTower: EnterDefendTower);

            if (!shown)
            {
                Debug.LogWarning(
                    "[WaveManager] Breach choice overlay could not be created — " +
                    "falling back to the ATB Last Stand so the breach is not a dead end.");
                EnterAtbBattle();
                return;
            }

            Debug.Log(
                $"[WaveManager] Wave {_currentWaveId} breached with {breachCount} enemies — " +
                "awaiting the player's Last-Stand / Defend-the-Tower choice.");
        }

        /// <summary>
        /// WO-47 "Defend the Tower" (Phase 2): clears the rest of the wave off the
        /// village field (the breaching enemies + any remaining live enemies + the
        /// apex boss), stashes the breach context, and loads the DEDICATED
        /// <see cref="SceneRouter.PatriciaLight"/> scene — the third-person tower-
        /// defense shooter (driven by <c>PatriciaLightController</c>). On win/lose
        /// that scene returns to the village via <see cref="SceneRouter.GoVillage"/>,
        /// where a fresh WaveManager re-runs the loop — same lifecycle as the ATB
        /// path. (Phase 1's in-place <c>PatriciaLightMode.Begin</c> is superseded.)
        /// </summary>
        private void EnterDefendTower()
        {
            // Clear the wave off the field so nothing lingers when we return from
            // the dedicated scene (the shooter spawns its own assault there).
            // Iterate a SNAPSHOT: Kill() -> Die() removes the enemy from _liveEnemies,
            // which otherwise throws "Collection was modified" mid-enumeration and
            // aborts the breach -> Defend-the-Tower transition (dev-log error at
            // WaveManager.cs:722 / BreachChoiceOverlay.Resolve).
            foreach (Enemy e in _liveEnemies.ToArray())
                if (e != null) e.Kill();
            _liveEnemies.Clear();
            _breachRoster.Clear();

            if (_liveApexBoss != null)
            {
                _liveApexBoss.Died -= HandleApexBossDied;
                Destroy(_liveApexBoss.gameObject);
                _liveApexBoss = null;
            }

            Debug.Log($"[WaveManager] Wave {_currentWaveId} breach — loading Defend the Tower (Patricia Light) scene.");

            // Stash the breach context the way PendingBattle works, then load the
            // dedicated scene. PatriciaLightController reads PendingPatriciaLight on
            // the far side and returns to the village when it resolves.
            var p = new PatriciaLightParams
            {
                Wave = _currentWaveId,
                ReturnScene = SceneRouter.Village,
            };
            SceneRouter.GoPatriciaLight(p).Forget();
        }

        /// <summary>
        /// Consumes the breaching roster and hands off to the ATB "Last Stand"
        /// scene via the real <see cref="SceneRouter.GoBattle"/> API. This is the
        /// unchanged pre-WO-47 breach behaviour, extracted so the breach choice
        /// prompt (and its fallback) can invoke it directly.
        /// </summary>
        private void EnterAtbBattle()
        {
            // BreachedIds: the 3D-layer ids of the breaching enemies. The ATB
            // BattleController maps these onto engine combatant defs (today via
            // its fallback; the per-enemy mapper is its own follow-up).
            // BUG-009: hand the ATB the ENGINE def id per breacher (Enemy.EngineDefId
            // → a valid ENEMY_DEFS key like "skeleton"/"necromancer"), not the
            // per-instance EnemyId — so the battle roster matches who actually
            // breached instead of always using the single fallback enemy.
            var breachedIds = new List<string>(_breachRoster.Count);
            foreach (Enemy e in _breachRoster)
            {
                if (e != null && !string.IsNullOrEmpty(e.EngineDefId))
                    breachedIds.Add(e.EngineDefId);
            }

            var battleParams = new BattleParams
            {
                Wave = _currentWaveId,
                BreachedIds = breachedIds.ToArray(),
                // Pets are wired in the Week-4 pet pass; an empty array is valid.
                ParticipatingPetIds = System.Array.Empty<string>(),
            };

            // Consume the breaching enemies out of the village field — they have
            // "left" the 3D layer and now exist only as ATB combatants. The rest
            // of the wave is abandoned: when the ATB scene returns to the Village
            // a fresh WaveManager.Start() re-runs the loop from the same wave.
            foreach (Enemy e in _breachRoster)
                if (e != null) e.Kill();
            _liveEnemies.Clear();
            _breachRoster.Clear();

            // If an apex wave also fielded the flying boss, it leaves the 3D
            // layer with the rest of the wave — destroy it so it does not orbit
            // an empty village while the ATB scene is up. The dragon is its own
            // encounter; ground enemies breaching abandons the apex wave too.
            if (_liveApexBoss != null)
            {
                _liveApexBoss.Died -= HandleApexBossDied;
                Destroy(_liveApexBoss.gameObject);
                _liveApexBoss = null;
            }

            Debug.Log(
                $"[WaveManager] Wave {_currentWaveId} breached with " +
                $"{battleParams.BreachedIds.Length} enemies — handing off to ATBBattle.");

            // SceneRouter.GoBattle stashes the params on SceneRouter.PendingBattle
            // and fades into ATBBattle. BattleController reads PendingBattle and,
            // on resolve, fades back to the Village scene (BattleController
            // .ReturnAfterResult). Fire-and-forget — never await in a hot path.
            SceneRouter.GoBattle(battleParams).Forget();
        }

        // =====================================================================
        //  Enemy event handlers
        // =====================================================================

        private void HandleEnemyDied(Enemy enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.ReachedHeart -= HandleEnemyReachedHeart;
            }
            _liveEnemies.Remove(enemy);
        }

        /// <summary>
        /// The apex flying boss died. Unsubscribes and drops the reference — the
        /// dragon destroys its own GameObject after the death-fall, and
        /// <see cref="TickActiveWave"/> then sees the wave as clear.
        /// </summary>
        private void HandleApexBossDied(DragonBoss boss)
        {
            if (boss != null) boss.Died -= HandleApexBossDied;
            if (_liveApexBoss == boss) _liveApexBoss = null;
        }

        /// <summary>
        /// An enemy walked all the way to the Heart without crossing the breach
        /// ring earlier (e.g. ring radius smaller than the Heart arrival radius).
        /// Treat it as a breach so the Heart is never silently overrun.
        /// </summary>
        private void HandleEnemyReachedHeart(Enemy enemy)
        {
            if (_phase != WavePhase.Active) return;
            if (enemy != null && !_breachRoster.Contains(enemy))
            {
                _breachRoster.Add(enemy);
                TriggerBreach();
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private WaveSpawnPoint FindSpawnPoint(string spawnId)
        {
            if (string.IsNullOrEmpty(spawnId) || _spawnPoints == null) return null;
            foreach (WaveSpawnPoint p in _spawnPoints)
                if (p != null && p.SpawnId == spawnId) return p;
            return null;
        }

#if UNITY_EDITOR
        // Draws the inner breach ring in the Scene view while authoring.
        private void OnDrawGizmosSelected()
        {
            Vector3 center = _heart != null ? _heart.transform.position : transform.position;
            Gizmos.color = new Color(0.86f, 0.27f, 0.27f, 0.6f);
            const int steps = 48;
            Vector3 prev = center + new Vector3(_innerRingRadius, 0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(a) * _innerRingRadius, 0f, Mathf.Sin(a) * _innerRingRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
