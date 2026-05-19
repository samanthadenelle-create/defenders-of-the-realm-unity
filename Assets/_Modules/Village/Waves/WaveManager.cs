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
            _countdownRemaining = Mathf.Max(0f, wave.CountdownSeconds);
            _phase = WavePhase.Countdown;
            OnCountdownTick.Invoke(_countdownRemaining);
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
                Debug.LogError(
                    "[WaveManager] Wave declares an apex boss but no _apexBossPrefab is wired — " +
                    "the Boss_Dragon prefab must be assigned (see docs/port-notes/dragon-wave-wiring.md).");
                return;
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

            if (batch.Delay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(batch.Delay));

            for (int i = 0; i < Mathf.Max(0, batch.Count); i++)
            {
                // The wave may have been breached / cleared while this batch
                // was still draining — stop releasing if so.
                if (_phase != WavePhase.Active) return;

                SpawnOne(def, point);

                if (batch.Interval > 0f && i < batch.Count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(batch.Interval));
            }
        }

        /// <summary>Instantiates + configures one enemy at <paramref name="point"/>.</summary>
        private void SpawnOne(EnemyDef def, WaveSpawnPoint point)
        {
            Vector3 pos = point.transform.position;
            Quaternion rot = Quaternion.LookRotation(
                point.HeadingToGate.sqrMagnitude > 0.0001f ? point.HeadingToGate : Vector3.forward);

            Enemy enemy = _enemyPrefab != null
                ? Instantiate(_enemyPrefab, pos, rot, _enemyRoot)
                : BuildPlaceholderEnemy(pos, rot);

            string instanceId = $"wave{_currentWaveId}-{def.Id}-{_spawnInstanceCounter++}";
            Transform heartT = _heart != null ? _heart.transform : null;
            enemy.Configure(instanceId, def, heartT);

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

        /// <summary>Wave cleared without a breach — advance to the next countdown.</summary>
        private void CompleteWave()
        {
            int cleared = _currentWaveId;
            if (_heart != null) _heart.SetState(HeartState.Serene);
            OnWaveCleared.Invoke(cleared);
            EnterCountdown(cleared + 1);
        }

        // =====================================================================
        //  Breach -> ATB hand-off
        // =====================================================================

        /// <summary>
        /// One or more enemies crossed the inner ring. Pauses the loop, builds a
        /// <see cref="BattleParams"/> from the breaching roster and hands off to
        /// the ATB scene via the real <see cref="SceneRouter.GoBattle"/> API.
        /// </summary>
        private void TriggerBreach()
        {
            _phase = WavePhase.Breached;
            OnBreach.Invoke(_currentWaveId);
            if (_heart != null) _heart.SetState(HeartState.Critical);

            // BreachedIds: the 3D-layer ids of the breaching enemies. The ATB
            // BattleController maps these onto engine combatant defs (today via
            // its fallback; the per-enemy mapper is its own follow-up).
            var breachedIds = new List<string>(_breachRoster.Count);
            foreach (Enemy e in _breachRoster)
            {
                if (e != null && !string.IsNullOrEmpty(e.EnemyId))
                    breachedIds.Add(e.EnemyId);
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
