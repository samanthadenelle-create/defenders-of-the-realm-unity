// =============================================================================
// WaveManager — the Elarion village wave loop (Week-4 slice).
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

using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;   // FlowTrace — wave-start flow instrumentation (§12)
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;   // singleton "active-scene wins" rule (TriggerWave RCA)
using DeNelle.Data;       // WaveData SO — WO-86

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
        /// <summary>The Heart fell (HP 0) — the run is lost. Terminal; the loop halts here.</summary>
        Defeated = 5,
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

        [Tooltip("WO-316: COMPOSE each wave's batches into runtime role-mix FAMILY squads " +
                 "(a tank + healer + a few DPS, advancing + charging together via the " +
                 "EnemyGroupSpawner / EnemyGroupCoordinator) instead of releasing the flat " +
                 "single-type batch stream. ON = composed groups (auto-builds a spawner if " +
                 "none is wired); OFF = legacy flat WaveBatch spawning (back-compat).")]
        [SerializeField] private bool _composeFamilyGroups = true;

        [Tooltip("WO-316: the formation runtime-composed family squads spawn in.")]
        [SerializeField] private SpawnFormation _composedFormation = SpawnFormation.Wedge;

        [Tooltip("WO-362: SMART per-wave composition + tactical positioning. ON = ignore the " +
                 "flat waves.json batches and instead GENERATE each wave's roster from the wave " +
                 "number (tiered weak/medium/strong mix, an elite every 5th wave, no two " +
                 "consecutive waves identical), then place each enemy by tactical role (tanks " +
                 "front-centre, archers backline, weak trailing) at a gate that ROTATES N→E→S→W. " +
                 "OFF = the legacy compose-family / flat-batch paths (full back-compat). Takes " +
                 "priority over _composeFamilyGroups when both are on.")]
        [SerializeField] private bool _smartComposition = true;

        [Header("Wave SO Authoring (WO-86)")]
        [Tooltip("Optional list of WaveData ScriptableObjects for SO-driven authoring. The existing JSON-driven loop runs independently and is unaffected.")]
        [SerializeField] private List<WaveData> _soWaves
            = new List<WaveData>();

        [Header("Breach detection")]
        [Tooltip("Radius (world units) of the inner wall ring around the Heart. An enemy that " +
                 "crosses INSIDE this ring counts as a breach. Tune to sit just inside the " +
                 "curtain wall (WallLayout.WallHalfZ ~ 21u; the inner ring sits well within).")]
        [SerializeField] private float _innerRingRadius = 9f;

        [Tooltip("Seconds the manager waits after the wave starts before arming breach " +
                 "detection — lets enemies clear the spawn point first.")]
        [SerializeField] private float _breachArmDelay = 0.5f;

        [Header("Loop control")]
        [Tooltip("Start the wave loop automatically on Start(). OFF by default: the wave " +
                 "must NOT pre-spawn / pre-countdown at scene load — the player gets a calm " +
                 "build/prep phase first, then presses the HUD DEFEND button, which kicks " +
                 "the loop via ForceBeginNextWave() -> BeginLoop(). On: legacy auto-countdown " +
                 "at load (dev/standalone-wave-scene use only).")]
        [SerializeField] private bool _autoStart = false;

        [Tooltip("Start the loop from this wave id (1 = the first wave). Dev override.")]
        [SerializeField, Min(1)] private int _startWave = 1;

        [Header("Wave-clear resource reward (WO-330 — defend → earn → build)")]
        [Tooltip("Award build resources (Wood/Iron) when a wave is fully cleared. This is the " +
                 "primary economy income: defend the city → defeat the wave → earn the resources " +
                 "you spend on building/upgrading defenses. OFF = no wave-clear resource grant " +
                 "(the legacy crystal drop still runs).")]
        [SerializeField] private bool _awardResourcesOnWaveClear = true;

        [Tooltip("Base Wood granted on a wood-payout wave (before scaling). WO-361: wood pays out " +
                 "every Nth wave (WoodInterval), in the range [Base .. Base+Spread], scaled by wave.")]
        [SerializeField, Min(0)] private int _woodRewardBase = 20;

        [Tooltip("Random spread added on top of the wood base (0 = flat). Final = Random[Base..Base+Spread] * scale.")]
        [SerializeField, Min(0)] private int _woodRewardSpread = 10;

        [Tooltip("WO-330 wiring contract: extra Wood added to the wood base per wave number (linear ramp). " +
                 "Folded into the effective wood base BEFORE the random spread + WO-361 scaling, so a " +
                 "later wood-payout wave starts from a higher floor. 0 = no per-wave wood ramp.")]
        [SerializeField, Min(0)] private int _woodRewardPerWave = 0;

        [Tooltip("Wood pays out every Nth wave (WO-361 default: every 3rd). 0/1 = every wave.")]
        [SerializeField, Min(0)] private int _woodRewardInterval = 3;

        [Tooltip("Base Iron granted on an iron-payout wave (before scaling). WO-361: iron pays out " +
                 "every Nth wave (IronInterval), in the range [Base .. Base+Spread], scaled by wave.")]
        [SerializeField, Min(0)] private int _ironRewardBase = 15;

        [Tooltip("Random spread added on top of the iron base (0 = flat). Final = Random[Base..Base+Spread] * scale.")]
        [SerializeField, Min(0)] private int _ironRewardSpread = 10;

        [Tooltip("WO-330 wiring contract: extra Iron added to the iron base per wave number (linear ramp). " +
                 "Folded into the effective iron base BEFORE the random spread + WO-361 scaling, so a " +
                 "later iron-payout wave starts from a higher floor. 0 = no per-wave iron ramp.")]
        [SerializeField, Min(0)] private int _ironRewardPerWave = 0;

        [Tooltip("Iron pays out every Nth wave (WO-361 default: every 4th). 0/1 = every wave.")]
        [SerializeField, Min(0)] private int _ironRewardInterval = 4;

        [Tooltip("Base Food granted on a food-payout wave (before scaling). WO-361: food pays out " +
                 "every Nth wave (FoodInterval), in the range [Base .. Base+Spread], scaled by wave.")]
        [SerializeField, Min(0)] private int _foodRewardBase = 30;

        [Tooltip("Random spread added on top of the food base (0 = flat). Final = Random[Base..Base+Spread] * scale.")]
        [SerializeField, Min(0)] private int _foodRewardSpread = 20;

        [Tooltip("Food pays out every Nth wave (WO-361 default: every 2nd). 0/1 = every wave.")]
        [SerializeField, Min(0)] private int _foodRewardInterval = 2;

        [Tooltip("Reward scaling: amounts grow by this fraction every ScalePer waves (WO-361: +20% per 5 waves). " +
                 "e.g. 0.2 with ScalePer 5 → wave 6 pays 1.2×, wave 11 pays 1.4×, …")]
        [SerializeField, Min(0f)] private float _rewardScalePerStep = 0.2f;

        [Tooltip("Number of waves per scaling step (WO-361: every 5 waves). Min 1.")]
        [SerializeField, Min(1)] private int _rewardScaleWaveStep = 5;

        [Tooltip("Clamps the scaling step count so reward growth never runs away on very " +
                 "late waves. 0 = uncapped. e.g. 6 caps scaling after 6 steps.")]
        [SerializeField, Min(0)] private int _rewardScalingStepCap = 0;

        [Header("Per-kill resource trickle (WO-330 — optional, secondary)")]
        [Tooltip("Grant a small Wood/Iron trickle per enemy killed during the wave (the wave-clear " +
                 "bonus is the primary reward). OFF = only the wave-clear bonus pays out.")]
        [SerializeField] private bool _awardResourcesPerKill = true;

        [Tooltip("Wood granted per enemy killed.")]
        [SerializeField, Min(0)] private int _woodPerKill = 1;

        [Tooltip("Iron granted per enemy killed.")]
        [SerializeField, Min(0)] private int _ironPerKill = 0;

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

        [Tooltip("Fires once when the Heart of Elarion falls (HP 0) — the village LOSE condition " +
                 "(WO-125 Bug 3). The loop halts (phase Defeated); bind a defeat screen here.")]
        public UnityEvent OnDefeat = new UnityEvent();

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

        // WO-430: per-enemy spawn scatter around the marker (metres, applied ±). Lateral was
        // ±4.5 — too wide, it pushed the pre-NavMesh-sample XZ off the baked mesh boundary and
        // the silent SamplePosition miss then stranded the enemy at the raw spawn Y (sky/underground).
        // Tightened to ±3 to lower the miss probability; depth kept at ±3.
        private const float SpawnLateralSpread = 3f;
        private const float SpawnDepthSpread   = 3f;

        /// <summary>The apex flying boss for the current wave (null when not an apex wave / dead).</summary>
        private DragonBoss _liveApexBoss;

        /// <summary>WO-362: lazily-built tactical spawner (no inspector wiring required).</summary>
        private SmartEnemySpawner _smartSpawner;

        /// <summary>True once we've subscribed to the Heart's OnHeartDestroyed — fire-once subscribe guard.</summary>
        private bool _heartDeathHooked;

        private WavePhase _phase = WavePhase.Idle;
        private int _currentWaveId;
        private float _countdownRemaining;
        private bool  _forceSpawnNow;   // dev/bot "jump to wave": zero the countdown on the next BeginLoop

        // ENDLESS MODE (owner ruling 2026-07-11: "after 20 rounds continue to allow the user to
        // start waves manually and increase difficulty and mobs every level up"). Past the last
        // authored wave the loop does NOT auto-run the prepare countdown — it parks in phase
        // Countdown with _countdownRemaining held at 0 and this flag set, WAITING for the player's
        // DEFEND button (ForceBeginNextWave / ForceSpawnNextWaveNow). The HUD needs no change:
        // StartWaveHudBridge already shows the DEFEND button in phase Countdown, and the
        // HudKit wave-timer label only renders while CountdownRemaining > 0 (so it stays blank).
        private bool _awaitingPlayerStart;

        // Endless cycling: waves beyond the schedule replay the authored waves from this id
        // upward IN ORDER (4..20 by default — the escalating family squads), so every full
        // cycle ends on the authored apex wave (the dragon returns as the cycle capstone at
        // true waves 37, 54, …). Clamped into the schedule range at runtime.
        private const int EndlessCycleStartWaveId = 4;

        /// <summary>
        /// True while an endless wave (beyond the authored schedule) is armed and the loop is
        /// waiting for the player to start it via the HUD DEFEND button. Read-only seam for
        /// HUD/bot producers; before the schedule is exhausted this is always false.
        /// </summary>
        public bool IsAwaitingPlayerStart => _awaitingPlayerStart;
        private float _breachArmTimer;
        private bool _breachArmed;
        private int _spawnInstanceCounter;

        // WO-579 (#5 "resets to wave 1") — cross-reload wave RESUME. The WaveManager is rebuilt on
        // every hub (re)load (it is NOT DontDestroyOnLoad), so without a resume point BeginLoop always
        // restarts at _startWave (=1). This static survives a scene reload WITHIN a play session, and is
        // seeded once from the save (GameState.BestWave + 1) for cross-session continuation.
        // CompleteWave advances it; ResetResumeStatic clears it at each play start so a new game / save
        // reset re-seeds from the (possibly reset) BestWave instead of carrying a stale wave number.
        private static int s_resumeWaveId = 0;   // 0 = unseeded

        // WO-139 #12 pattern: with domain reload disabled, statics persist across Play sessions. Reset
        // the resume seed at each play start so it re-derives from the save (handles new game / reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetResumeStatic() => s_resumeWaveId = 0;

        /// <summary>The phase the wave loop is in.</summary>
        public WavePhase Phase => _phase;

        /// <summary>The wave currently counting down / active (1-based; 0 before the loop starts).</summary>
        public int CurrentWaveId => _currentWaveId;

        /// <summary>Seconds remaining in the Prepare-Phase countdown (0 when not counting down).</summary>
        public float CountdownRemaining => _countdownRemaining;

        /// <summary>True while the loop is in the Prepare-Phase countdown (the wave timer is live).</summary>
        public bool IsCountingDown => _phase == WavePhase.Countdown;

        /// <summary>
        /// T-022 (HUD wave-timer bind): seconds until the next wave begins. Returns the
        /// live countdown value ONLY while actually counting down, and 0 otherwise (Idle /
        /// Active / Complete), so a HUD timer label binding this reads a clean, unambiguous
        /// value — no need to also test <see cref="Phase"/>. Additive read-only accessor;
        /// existing <see cref="CountdownRemaining"/> / <see cref="OnCountdownTick"/> are
        /// unchanged, so existing bindings keep working.
        /// </summary>
        public float SecondsUntilNextWave => _phase == WavePhase.Countdown ? _countdownRemaining : 0f;

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

            // POOLED (same path as SpawnOne): reuse a dead body instead of churning a
            // fresh GameObject. The external caller owns this enemy's lifecycle; on its
            // death Enemy.Die returns it to the pool like any other.
            //
            // T-011: prefer the model-keyed FACTORY body (varied per def) over the single
            // serialized _enemyPrefab so this external-mode spawn matches the variety of
            // the wave loop. _enemyPrefab is only a fallback when no model resolves.
            string model = EnemyFactory.ModelForEnemy(def);
            bool useFactory = !string.IsNullOrEmpty(model);
            if (!useFactory && _enemyPrefab == null)
            {
                Debug.LogError($"[WaveManager] No model resolves for enemy data '{def.Id}' and no _enemyPrefab fallback — using primitive placeholder.");
            }
            string poolKey = useFactory ? "model:" + model : "prefab:" + _enemyPrefab.name;
            Enemy enemy = EnemyPool.Get(poolKey, useFactory ? null : _enemyPrefab, def, pos, rot, _enemyRoot);
            if (enemy == null) return null;

            // The hero/pet target sweeps find enemies via GetComponentInParent
            // <IDamageable>, which resolves to EnemyDamageable. The placeholder
            // capsule (and some prefabs) may not carry it — add it so the
            // Defend-the-Tower hero can actually acquire + damage this enemy.
            if (enemy.GetComponent<EnemyDamageable>() == null)
                enemy.gameObject.AddComponent<EnemyDamageable>();

            enemy.Configure(instanceId, def, heart);
            return enemy;
        }

        // ── Singleton (TriggerWave-timeout RCA) ───────────────────────────────
        //
        // WaveManager is NOT a DontDestroyOnLoad global — there is a WaveManager
        // baked into BOTH MainCastle_Hall (the home hub / start scene) and
        // Village2 (the raid target). With those scenes loaded additively,
        // FindAnyObjectByType<WaveManager>() enumerates in a NON-deterministic order,
        // so a consumer (BattleMusic / TowerSwap / CameraMode / the AutoPilot
        // TriggerWave probe) could resolve a DIFFERENT instance than the one being
        // triggered/watched — the "works ~5/12, fails ~9/12" race that surfaced as
        // the intermittent "TriggerWave timeout".
        //
        // CANONICAL RULE — the WaveManager in the ACTIVE scene wins. On Awake/OnEnable:
        //   • if Instance is null → claim it (first one home).
        //   • else if THIS object lives in the active scene → claim it (active wins,
        //     even if a hub instance claimed first while it was the active scene).
        // We never destroy the loser (both managers still run their own scene's loop);
        // we only steer Find-based consumers at the canonically-correct one. Cleared
        // in OnDestroy ONLY if we are still the current Instance (never clobber a
        // newer claimant). Consumers prefer WaveManager.Instance, falling back to a
        // Find when Instance is null (pre-Awake / between scene loads) for safety.
        public static WaveManager Instance { get; private set; }

        /// <summary>
        /// Claims <see cref="Instance"/> per the "active-scene wins" rule above.
        /// Idempotent + safe to call from both Awake and OnEnable (a scene becoming
        /// active after load re-asserts the claim on the next enable).
        /// </summary>
        private void ClaimInstanceIfCanonical()
        {
            bool inActiveScene = gameObject.scene.IsValid()
                                 && gameObject.scene == SceneManager.GetActiveScene();
            if (Instance == null || inActiveScene)
            {
                if (Instance != this)
                    FlowTrace.Step("Wave", $"WaveManager.Instance claimed by '{name}' in scene '{gameObject.scene.name}' (active={inActiveScene}).");
                Instance = this;
            }
        }

        private void Awake()  => ClaimInstanceIfCanonical();
        private void OnEnable() => ClaimInstanceIfCanonical();

        private void OnDestroy()
        {
            // Only relinquish if we still hold it — never clobber a newer claimant.
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Start()
        {
            // DEFEND-GATED START: the wave loop must NOT pre-spawn or pre-countdown at
            // scene load. At load there are ZERO enemies and the manager sits Idle; the
            // player gets the calm build/prep phase, then presses the HUD DEFEND button
            // (VillageHudController.StartWaveRequested -> StartWaveHudBridge ->
            // ForceBeginNextWave() -> BeginLoop()), which is the SOLE kickoff. _autoStart
            // therefore defaults OFF and stays off on the live village scene.
            //
            // WO-133 (FTUE): even with _autoStart left ON (dev / standalone wave scene),
            // a FIRST run (GameState.Onboarded == false) still must NOT auto-start — the
            // tutorial's BeginWaveRequested is the kickoff there. Core-not-bootstrapped
            // (no service) is treated as a returning player so a missing Core never
            // strands a dev auto-start.
            // WO-579 (#2/#3 — owner felt-test 2026-06-28 "start wave should AUTO attack; Start Wave is
            // an OVERRIDE"): AUTO-ARM the prepare-phase countdown in the player's HOME HUB so the
            // top-left "next wave in MM:SS" clock ticks (VillageHudController.PollWaveTimer surfaces
            // CountdownRemaining) and the wave AUTO-starts at zero (towers + hero auto-defend in-hub).
            // The baked MainCastle_Hall WaveManager has _autoStart serialized OFF, so we also auto-arm
            // when ff.waveautostart is on AND this is the home hub (a hub scene that is NOT enemy-owned;
            // excludes the Village2 enemy stronghold, which runs its own garrison loop). IsFirstRun
            // (FTUE) still blocks it — the tutorial owns the first kickoff. The HUD "Start Wave" button
            // (ForceBeginNextWave) remains the manual EARLY override that skips the remaining countdown.
            bool autoArm = _autoStart || (FeatureFlags.WaveAutoStart && IsHomeHubScene());
            if (autoArm && !IsFirstRun()) GuardedKickoff("Start/auto-arm");
        }

        /// <summary>
        /// WO-579: true when this WaveManager lives in the player-owned HOME HUB — a hub scene that is
        /// NOT enemy-owned (MainCastle_Hall / CastleHub / CastleHub_MainKeep). The Village2 enemy
        /// stronghold is a hub name too but is enemy-owned, so it is excluded from the home-defense
        /// auto-countdown (it drives its own garrison roster).
        /// </summary>
        private bool IsHomeHubScene()
        {
            string scene = gameObject.scene.IsValid()
                ? gameObject.scene.name
                : SceneManager.GetActiveScene().name;
            return HubScenes.IsHub(scene) && !HubScenes.IsEnemyOwnedScene(scene);
        }

        /// <summary>
        /// True when this is a brand-new save still in onboarding
        /// (<see cref="GameState.Onboarded"/> == false). Mirrors
        /// <c>OnboardingFlow.ShouldRun</c> so the wave loop and the FTUE agree on
        /// who is a first-run player. No Core service yet ⇒ NOT first-run, so the
        /// loop is never blocked when Core has not bootstrapped.
        /// </summary>
        private static bool IsFirstRun()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false;
            return !svc.State.Onboarded;
        }

        // Audit 2026-05-30 (WO-139 #4): unsubscribe from all live enemy/boss
        // events so stale callbacks don't fire into a torn-down manager across
        // the breach->reload loop. Mirrors the subs in SpawnEnemy/SpawnApexBoss.
        private void OnDisable()
        {
            foreach (Enemy e in _liveEnemies)
            {
                if (e == null) continue;
                e.Died -= HandleEnemyDied;
                e.ReachedHeart -= HandleEnemyReachedHeart;
            }
            _liveEnemies.Clear();

            if (_liveApexBoss != null)
            {
                _liveApexBoss.Died -= HandleApexBossDied;
                _liveApexBoss = null;
            }

            // WO-125 Bug 3: drop the Heart lose-condition subscription so a stale
            // delegate can't fire into a torn-down manager across the breach/reload loop.
            if (_heart != null && _heartDeathHooked)
                _heart.OnHeartDestroyed -= HandleHeartDestroyed;
            _heartDeathHooked = false;

            // Drop the stall watchdog handle — Unity stops coroutines on disable, but
            // clearing the field keeps a stale handle from being StopCoroutine'd later.
            _stallWatchdogRoutine = null;

            // ENDLESS: drop the awaiting-player latch — a re-enabled/reloaded manager
            // re-derives it from the resume seed via BeginLoop -> EnterCountdown.
            _awaitingPlayerStart = false;
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
            // §12 wave-start instrumentation (TriggerWave TIMEOUT RCA). ADDITIVE only —
            // no logic/timing/async change. Traces every step + each await result so a
            // headless fleet run shows EXACTLY where the start flow stalls.
            FlowTrace.Step("Wave", $"BeginLoop ENTRY phase={_phase} forceSpawn={_forceSpawnNow} startWave={_startWave} scheduleCached={_schedule != null} catalogCached={_enemyCatalog != null}");

            // FTUE GUARD (F8 2026-07-08 "died in tutorial — nothing should spawn"): while the
            // first-time tutorial is active the ambient wave loop must NOT arm — a countdown/wave
            // could otherwise kill the player mid-tutorial. This mirrors (and back-stops) the
            // Start/auto-arm !IsFirstRun gate for ANY BeginLoop caller (manual DEFEND button, dev
            // seams, resume). The tutorial's OWN scripted teaching wave uses TutorialWaveSpawner ->
            // SpawnEnemyForExternalMode (not this loop), so it is unaffected. The intended
            // post-tutorial kick from TutorialFlow.FinishFlow runs AFTER FinishOnboarding sets
            // Onboarded=true, so this never blocks the legitimate handoff.
            if (TutorialFlow.HostilesSuppressedForTutorial)
            {
                FlowTrace.Step("Wave", "BeginLoop suppressed — tutorial (FTUE) active; ambient wave loop stays closed until onboarding completes.");
                return;
            }

            ResolveSceneRefs();

            using (FlowTrace.Measure("Wave", "wave data load", warnAboveMs: 2000f))
            {
                // LOAD-GUARD (layer 3): a faulted/null load must NOT escape this async
                // (it would never reach EnterCountdown and the loop would silently stay
                // at Idle). We catch, FlowTrace.Fail, and LEAVE the cache field null so
                // the null-check below forces _phase=Idle — which RetryTillActive then
                // re-attempts (re-running these loads), so a transient fault self-heals
                // instead of permanently stalling. A successful load caches and is never
                // re-fetched.
                if (_schedule == null)
                {
                    FlowTrace.Step("Wave", "BeginLoop awaiting LoadWavesAsync…");
                    try
                    {
                        _schedule = await WaveDataLoader.LoadWavesAsync();
                        FlowTrace.Step("Wave", $"BeginLoop LoadWavesAsync returned — waves loaded: {_schedule?.Waves?.Count ?? -1}");
                    }
                    catch (Exception e)
                    {
                        _schedule = null;
                        FlowTrace.Fail("Wave", $"LoadWavesAsync threw: {e.Message} — leaving schedule null for retry");
                        Debug.LogError($"[WaveManager] LoadWavesAsync threw: {e}");
                    }
                }
                if (_enemyCatalog == null)
                {
                    FlowTrace.Step("Wave", "BeginLoop awaiting LoadEnemiesAsync…");
                    try
                    {
                        _enemyCatalog = await WaveDataLoader.LoadEnemiesAsync();
                        FlowTrace.Step("Wave", $"BeginLoop LoadEnemiesAsync returned — enemies loaded: {_enemyCatalog?.Enemies?.Count ?? -1}");
                    }
                    catch (Exception e)
                    {
                        _enemyCatalog = null;
                        FlowTrace.Fail("Wave", $"LoadEnemiesAsync threw: {e.Message} — leaving catalog null for retry");
                        Debug.LogError($"[WaveManager] LoadEnemiesAsync threw: {e}");
                    }
                }
            }

            if (_schedule == null || _enemyCatalog == null)
            {
                FlowTrace.Fail("Wave", $"wave data null — loop cannot run (schedule={_schedule != null}, catalog={_enemyCatalog != null}) — phase forced to Idle — {StallStateDump(-1)}");
                Debug.LogError("[WaveManager] Wave data failed to load — the wave loop cannot run.");
                _phase = WavePhase.Idle;
                return;
            }

            // WO-579 (#5): resume from the persisted run progress so a hub reload does not reset to 1.
            int startAt = ResolveStartWave();
            FlowTrace.Step("Wave", $"BeginLoop data OK — calling EnterCountdown(startWave={startAt}) [resume={s_resumeWaveId}, dev_startWave={_startWave}], forceSpawn={_forceSpawnNow}");
            EnterCountdown(startAt);
            FlowTrace.Step("Wave", $"BeginLoop EXIT — phase={_phase} countdownRemaining={_countdownRemaining:F2}s");
            Debug.Log($"[WaveManager] Loop armed — wave {startAt}, countdown {_countdownRemaining:F1}s.");
        }

        /// <summary>
        /// WO-579: the wave the loop should (re)start at. Resumes from the in-session/saved run
        /// progress so returning to the hub does not reset to wave 1. Seeds <see cref="s_resumeWaveId"/>
        /// once from <c>GameState.BestWave + 1</c> (cross-session) the first time it is needed; the dev
        /// <see cref="_startWave"/> is the floor (a dev override above the resume still wins).
        /// </summary>
        private int ResolveStartWave()
        {
            if (s_resumeWaveId <= 0)
            {
                var svc = GameStateService.Instance;
                int best = (svc != null && svc.State != null) ? svc.State.BestWave : 0;
                s_resumeWaveId = Mathf.Max(1, best + 1);
            }
            return Mathf.Max(_startWave, s_resumeWaveId);
        }

        // ── Robust start (TriggerWave-timeout RCA, layers 2 + 3) ──────────────
        //
        // A wave kickoff goes through the async BeginLoop(): it awaits the
        // WaveDataLoader loads, then EnterCountdown moves the phase off Idle. Two
        // things could leave the loop stranded at Idle and surface as a "wave never
        // started" timeout: (a) BeginLoop throws mid-flight (a load faults), or
        // (b) a transient null load forces _phase back to Idle. Both are now caught
        // and RETRIED a bounded number of times.
        //
        // GuardedKickoff wraps the start in try/catch (no silent failure — §12) and
        // arms RetryTillActive, a capped coroutine that re-fires the start only if
        // the phase has NOT left Idle within a short window. The NO-DOUBLE-START
        // guard: it re-fires ONLY from Idle (Countdown/Active/Breached/Complete/
        // Defeated all mean a wave already took, so it stops) — a real player's wave
        // that started normally is never re-kicked or stacked.

        private const int   StartRetryCap      = 3;     // bounded — never infinite
        private const float StartRetryWindow   = 1.5f;  // seconds to wait for Idle→off before a retry
        private Coroutine   _retryRoutine;

        // ── Stall watchdog (§12 "instrument + guard critical logic") ──────────
        //
        // The retry path above re-FIRES a stuck start, but a *silent stall* — the
        // phase never leaving Idle even after the retries, OR an async BeginLoop that
        // never resolves — is NOT a thrown exception, so Guard.Try / the GuardedKickoff
        // catch can never see it. The break-log only captures errors/exceptions, and the
        // headless Player.log is clobbered/shared, so a lost FlowTrace.Step at Idle leaves
        // us BLIND to where the start died.
        //
        // This watchdog DETECTS "didn't advance": armed alongside every kickoff, it waits
        // a generous window (well under any real countdown, generous for the async loads)
        // and, if the phase is STILL Idle (never reached Countdown/Active), emits ONE
        // captured FlowTrace.Fail (→ LogError → break-log.jsonl + WebTrace-able) with the
        // full state dump — which pinpoints the dead step: Idle+schedule==-1 ⇒ load never
        // completed; Idle+schedule>0 ⇒ loaded but countdown never entered; OTHER instance
        // ⇒ we armed the wrong (un-triggered) manager; etc. It fires AT MOST once per
        // kickoff and cancels the instant the phase advances (no false Fail on a slow-OK
        // start). Additive-only: it never touches wave balance/timing/spawn/retry logic.
        private const float StallWatchdogWindow = 9f;   // s before a still-Idle kickoff is declared STALLED
        private Coroutine   _stallWatchdogRoutine;

        /// <summary>
        /// Full wave-start state dump shared by every captured Fail (stall, throw,
        /// null-load, retry-exhaustion) so each surfaces the EXACT same diagnostic
        /// snapshot. Null-safe on every field. <paramref name="retries"/> = -1 when
        /// the caller has no retry count to report.
        /// </summary>
        private string StallStateDump(int retries)
        {
            string scene = "?";
            try { scene = gameObject.scene.name; } catch { /* torn-down */ }
            string which = (Instance == this) ? "self"
                         : (Instance == null ? "NULL" : "OTHER");
            return $"phase={_phase} "
                 + $"schedule={(_schedule?.Waves?.Count ?? -1)} "
                 + $"enemyCat={(_enemyCatalog?.Enemies?.Count ?? -1)} "
                 + $"retries={retries} forceSpawn={_forceSpawnNow} "
                 + $"instance={which} scene={scene}";
        }

        /// <summary>
        /// Watchdog coroutine: if the phase is STILL <see cref="WavePhase.Idle"/> after
        /// <see cref="StallWatchdogWindow"/>, emit ONE captured <see cref="FlowTrace.Fail"/>
        /// with the full <see cref="StallStateDump"/> — the silent stall announces itself
        /// instead of being a lost Step. Cancelled the instant the phase advances (yields
        /// each frame, checks Idle), so a normal slow-but-OK start never false-Fails.
        /// Fires at most once per armed kickoff.
        /// </summary>
        private IEnumerator StallWatchdog(string source)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < StallWatchdogWindow)
            {
                // Phase advanced past Idle → a wave took. No stall — clear + done.
                if (_phase != WavePhase.Idle) { _stallWatchdogRoutine = null; yield break; }
                yield return null;
            }

            // Window elapsed and STILL Idle → a silent stall. Announce it LOUDLY, once.
            if (_phase == WavePhase.Idle)
            {
                string dump = StallStateDump(-1);
                FlowTrace.Fail("Wave",
                    $"STALLED: kickoff '{source}' never left Idle after {StallWatchdogWindow:F0}s — {dump}");
                Debug.LogError($"[WaveManager] STALL WATCHDOG ({source}): {dump}");
            }
            _stallWatchdogRoutine = null;
        }

        /// <summary>Arms (re-arms) the stall watchdog for a kickoff. Null-safe; one at a time.</summary>
        private void ArmStallWatchdog(string source)
        {
            if (!isActiveAndEnabled) return;   // can't StartCoroutine on a disabled manager
            if (_stallWatchdogRoutine != null) StopCoroutine(_stallWatchdogRoutine);
            _stallWatchdogRoutine = StartCoroutine(StallWatchdog(source));
        }

        /// <summary>
        /// Fires BeginLoop() guarded by try/catch and arms the retry watchdog.
        /// Used by the player "Defend!" path and the bot/jump path so a faulted or
        /// stalled start self-heals instead of stranding the loop at Idle.
        /// </summary>
        private void GuardedKickoff(string source)
        {
            // FTUE GUARD (regression fix 2026-07-08): while the first-time tutorial is active the
            // ambient wave loop stays closed — BeginLoop() returns early on
            // TutorialFlow.HostilesSuppressedForTutorial. Firing the force-start path here would
            // arm RetryTillActive, which re-fires BeginLoop StartRetryCap times (each returns
            // suppressed → phase never leaves Idle) and then emits a FlowTrace.Fail to the
            // break-log for an EXPECTED state (the exact regression from the spawn-suppression fix).
            // Exit CLEANLY here — a Step, NOT a retry loop and NOT a Fail — so ANY force-start caller
            // (DEFEND button, dev seams, bot jump) is safe during the FTUE. The tutorial's OWN scripted
            // teaching wave uses TutorialWaveSpawner -> SpawnEnemyForExternalMode, NOT this loop, so it
            // is unaffected. This keys off !Onboarded exactly like the BeginLoop guard, so it LIFTS the
            // instant onboarding completes and post-tutorial DEFEND / force-start works normally.
            if (TutorialFlow.HostilesSuppressedForTutorial)
            {
                FlowTrace.Step("Wave", $"GuardedKickoff ({source}) — force-start suppressed: tutorial (FTUE) active; ambient wave loop stays closed until onboarding completes. No retry, no fail.");
                return;
            }

            try
            {
                FlowTrace.Step("Wave", $"GuardedKickoff ({source}) — BeginLoop().Forget() phase={_phase}");
                BeginLoop().Forget();
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Wave", $"wave start threw ({source}): {e.Message} — {StallStateDump(-1)}");
                Debug.LogError($"[WaveManager] Wave start threw ({source}): {e}");
            }

            // Arm the watchdog (only one at a time). isActiveAndEnabled guards a
            // StartCoroutine on a disabled manager (e.g. mid scene-teardown).
            if (isActiveAndEnabled)
            {
                if (_retryRoutine != null) StopCoroutine(_retryRoutine);
                _retryRoutine = StartCoroutine(RetryTillActive(source));
            }

            // Arm the STALL watchdog: detect "phase never left Idle" (a silent stall
            // that no try/catch can see) and announce it as ONE captured Fail with state.
            ArmStallWatchdog(source);
        }

        /// <summary>
        /// Watchdog: if the phase is still Idle after <see cref="StartRetryWindow"/>,
        /// re-fire BeginLoop — up to <see cref="StartRetryCap"/> times, yielding
        /// between attempts (NEVER a busy/infinite loop). Re-fires ONLY from Idle so a
        /// wave that already started (any non-Idle phase) is never double-started; a
        /// transient null load that bounced the phase back to Idle gets another go,
        /// which also re-attempts the WaveDataLoader loads (load-guard, layer 3).
        /// </summary>
        private IEnumerator RetryTillActive(string source)
        {
            for (int attempt = 1; attempt <= StartRetryCap; attempt++)
            {
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < StartRetryWindow)
                {
                    // Left Idle → a wave took. Done — do NOT re-fire (no double-start).
                    if (_phase != WavePhase.Idle) { _retryRoutine = null; yield break; }
                    yield return null;
                }

                // Still Idle after the window — only Idle is re-firable.
                if (_phase != WavePhase.Idle) { _retryRoutine = null; yield break; }

                FlowTrace.Warn("Wave", $"RetryTillActive ({source}) — phase still Idle after {StartRetryWindow:F1}s, retry {attempt}/{StartRetryCap}");
                Debug.LogWarning($"[WaveManager] Wave start did not leave Idle — retry {attempt}/{StartRetryCap} ({source}).");
                try { BeginLoop().Forget(); }
                catch (Exception e)
                {
                    FlowTrace.Fail("Wave", $"wave start retry {attempt} threw ({source}): {e.Message} — {StallStateDump(attempt)}");
                    Debug.LogError($"[WaveManager] Wave start retry {attempt} threw ({source}): {e}");
                }
            }

            if (_phase == WavePhase.Idle)
                FlowTrace.Fail("Wave", $"RetryTillActive ({source}) — phase STILL Idle after {StartRetryCap} retries; giving up — {StallStateDump(StartRetryCap)}");
            _retryRoutine = null;
        }

        /// <summary>
        /// Finds the Heart + spawn points in the scene when the inspector left
        /// them blank. The integrator may instead wire them by hand.
        /// </summary>
        private void ResolveSceneRefs()
        {
            if (_heart == null)
                _heart = FindAnyObjectByType<HeartController>();

            // WO-125 Bug 3: hook the Heart's lose condition once the Heart is known.
            // Idempotent — BeginLoop can re-run (e.g. after a Defend-the-Tower return),
            // and the guard keeps us from stacking duplicate handlers on the same Heart.
            if (_heart != null && !_heartDeathHooked)
            {
                _heart.OnHeartDestroyed += HandleHeartDestroyed;
                _heartDeathHooked = true;
            }

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                _spawnPoints = new List<WaveSpawnPoint>(
                    FindObjectsByType<WaveSpawnPoint>());
            }

            // A wave with no spawn markers can spawn ZERO enemies and then "clear"
            // itself instantly (each spawn path no-ops on an empty list), so the
            // countdown/loop appears to run but nothing ever attacks. Warn LOUDLY
            // once at loop-start so a missing-markers scene (e.g. MainCastle_Hall
            // before the spawn points are placed) is obvious instead of silent.
            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                Debug.LogWarning(
                    "[WaveManager] No WaveSpawnPoint markers found in the scene — waves " +
                    "will spawn NO enemies and each wave will self-clear instantly. Place " +
                    "WaveSpawnPoint markers (tag 'SpawnPoint', ~12 m outside each gate) so " +
                    "the wave loop has somewhere to spawn. See docs/port-notes/week4-waves.md.");
            }

            if (_enemyRoot == null) _enemyRoot = transform;
        }

        // =====================================================================
        //  Countdown phase
        // =====================================================================

        /// <summary>Enters the Prepare-Phase countdown for wave <paramref name="waveId"/>.</summary>
        private void EnterCountdown(int waveId)
        {
            FlowTrace.Step("Wave", $"EnterCountdown(waveId={waveId}) phaseBefore={_phase} forceSpawn={_forceSpawnNow}");

            WaveDef wave = _schedule.Find(waveId);
            if (wave == null)
            {
                // ENDLESS MODE (owner ruling 2026-07-11): past the authored schedule the loop
                // does NOT end — arm the next endless wave (cycled def, manual player start).
                if (TryArmEndlessWave(waveId)) return;

                // No such wave and endless couldn't resolve (empty/degenerate schedule, or a
                // gap INSIDE the authored range) — the schedule is exhausted.
                _phase = WavePhase.Complete;
                FlowTrace.Step("Wave", $"EnterCountdown: no WaveDef for waveId={waveId} — schedule exhausted, phase->Complete");
                Debug.Log($"[WaveManager] All {_schedule.Waves.Count} waves cleared — schedule complete.");
                return;
            }

            _currentWaveId = waveId;
            _enemyBestSqr.Clear();    // fresh stuck-tracking per wave
            _enemyStuckTime.Clear();

            // The lookout spots the incoming raid — blow the horn. This is the very
            // warning the FTUE teaches ("when you hear the horn, get to the gate").
            GameSfx.PlayLookoutHorn();

            // The between-wave build window scales with the player's chosen
            // difficulty: the canonical WaveDef.CountdownSeconds (45 s first
            // wave, 300 s later) is multiplied by the DifficultyTuning factor so
            // the owner targets land — Easy ~10 min, Normal ~5 min, Hard ~3 min
            // between waves. The multiplier is derived from the base seconds,
            // never hard-coded (see DifficultyTuning).
            _countdownRemaining = Mathf.Max(0f, ScaledCountdown(wave.CountdownSeconds));
            // DEV/bot immediate-spawn: ForceSpawnNextWaveNow set this from Idle — zero the
            // countdown so TickCountdown() spawns the wave on the next tick (no race with the
            // async BeginLoop setting the countdown above).
            bool zeroedByForce = _forceSpawnNow;
            if (_forceSpawnNow) { _forceSpawnNow = false; _countdownRemaining = 0f; }
            _phase = WavePhase.Countdown;
            FlowTrace.Step("Wave", $"EnterCountdown -> phase=Countdown wave={_currentWaveId} countdown={_countdownRemaining:F2}s zeroedByForceSpawn={zeroedByForce}");
            OnCountdownTick.Invoke(_countdownRemaining);
            WaveCountdownUI.Instance?.StartCountdown(_countdownRemaining);
        }

        // =====================================================================
        //  Endless mode (owner ruling 2026-07-11 — past the authored schedule)
        // =====================================================================

        /// <summary>
        /// Arms endless wave <paramref name="waveId"/> (a wave BEYOND the authored schedule):
        /// resolves its cycled authored def, then parks the loop in phase Countdown with the
        /// countdown held at 0 and <see cref="_awaitingPlayerStart"/> set — the wave will not
        /// spawn until the player presses the HUD DEFEND button (ForceBeginNextWave) or a
        /// dev/bot force-start fires. A pending <see cref="_forceSpawnNow"/> (bot jump) skips
        /// the wait and spawns on the next tick, exactly like the authored-wave force path.
        /// Returns false when no endless def resolves (empty schedule / waveId inside the
        /// authored range) so the caller falls through to the legacy Complete handling.
        /// </summary>
        private bool TryArmEndlessWave(int waveId)
        {
            WaveDef src = ResolveEndlessWaveDef(waveId, out int sourceWaveId);
            if (src == null) return false;

            _currentWaveId = waveId;
            _enemyBestSqr.Clear();    // fresh stuck-tracking per wave (parity with EnterCountdown)
            _enemyStuckTime.Clear();

            float countScale = EndlessCountScale(waveId);

            bool zeroedByForce = _forceSpawnNow;
            if (_forceSpawnNow) { _forceSpawnNow = false; _awaitingPlayerStart = false; }
            else _awaitingPlayerStart = true;

            // Phase Countdown + remaining 0 + waiting flag: TickCountdown holds (never
            // decrements past the flag), the HUD DEFEND button shows (StartWaveHudBridge
            // treats Countdown as available), and the HudKit "Next wave in Ns" label stays
            // blank (it only renders while CountdownRemaining > 0). No horn yet — the calm
            // build phase is open-ended; the lookout horn blows when the wave actually starts.
            _countdownRemaining = 0f;
            _phase = WavePhase.Countdown;
            FlowTrace.Step("Wave",
                $"endless wave {waveId}: def={sourceWaveId} countScale=x{countScale:F2} " +
                (zeroedByForce ? "force-spawning now (bot/jump)" : "awaiting player start"));
            OnCountdownTick.Invoke(0f);
            Debug.Log($"[WaveManager] Endless mode — wave {waveId} armed (cycled authored wave {sourceWaveId}, " +
                      $"mob count x{countScale:F2}). " +
                      (zeroedByForce ? "Force-spawning now." : "Waiting for the player's DEFEND."));
            return true;
        }

        /// <summary>
        /// Resolves the authored WaveDef an endless wave replays. Only waves STRICTLY beyond
        /// the last authored wave resolve (a gap inside the authored range stays a real gap).
        /// Cycling rule: waves <see cref="EndlessCycleStartWaveId"/>..max replay IN ORDER, so
        /// with the 20-wave schedule true wave 21 → def 4, 22 → def 5, …, 37 → def 20 (the
        /// apex dragon returns as every cycle's capstone), 38 → def 4 again. The TRUE wave
        /// number keeps counting so WaveScalingCurve keeps scaling HP/speed/damage past 20.
        /// </summary>
        private WaveDef ResolveEndlessWaveDef(int waveId, out int sourceWaveId)
        {
            sourceWaveId = waveId;
            if (_schedule == null) return null;
            int max = _schedule.MaxWaveId;
            if (max <= 0 || waveId <= max) return null;

            int cycleStart = Mathf.Clamp(EndlessCycleStartWaveId, 1, max);
            int cycleLen = max - cycleStart + 1;
            sourceWaveId = cycleStart + (waveId - max - 1) % cycleLen;
            return _schedule.Find(sourceWaveId);
        }

        /// <summary>
        /// The endless mob-count multiplier for <paramref name="waveId"/>: 1 within the
        /// authored schedule, then 1 + growth × (waveId − lastAuthored), capped. Growth/cap
        /// are DATA-DRIVEN from the waves.json "endless" block (defaults +5%/wave, cap 3×)
        /// so the owner tunes balance without a code edit.
        /// </summary>
        private float EndlessCountScale(int waveId)
        {
            if (_schedule == null) return 1f;
            int max = _schedule.MaxWaveId;
            if (max <= 0 || waveId <= max) return 1f;

            float growth = _schedule.Endless != null ? _schedule.Endless.CountGrowthPerWave : 0.05f;
            float cap    = _schedule.Endless != null ? _schedule.Endless.CountCap : 3f;
            if (growth <= 0f) return 1f;
            float scale = 1f + growth * (waveId - max);
            return cap > 0f ? Mathf.Min(scale, cap) : scale;
        }

        /// <summary>
        /// Clones an authored WaveDef for an endless wave with every batch count multiplied by
        /// <paramref name="countScale"/> (rounded UP, never below the authored count). A CLONE —
        /// never mutates the cached schedule, since cycled defs are replayed every cycle.
        /// Boss / apexBoss declarations carry over unchanged (stat growth is WaveScalingCurve's job).
        /// </summary>
        private static WaveDef CloneWaveWithScaledCounts(WaveDef src, int waveId, float countScale)
        {
            var clone = new WaveDef
            {
                WaveId           = waveId,
                Name             = src.Name,
                CountdownSeconds = src.CountdownSeconds,
                Boss             = src.Boss,
                ApexBoss         = src.ApexBoss,
                Enemies          = new List<WaveBatch>(src.Enemies != null ? src.Enemies.Count : 0),
            };
            if (src.Enemies != null)
            {
                foreach (WaveBatch b in src.Enemies)
                {
                    if (b == null) continue;
                    clone.Enemies.Add(new WaveBatch
                    {
                        Type       = b.Type,
                        Count      = Mathf.Max(b.Count, Mathf.CeilToInt(b.Count * countScale)),
                        SpawnPoint = b.SpawnPoint,
                        Delay      = b.Delay,
                        Interval   = b.Interval,
                    });
                }
            }
            return clone;
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
            // ENDLESS manual start: the countdown is parked (held at 0, never ticking) until
            // the player fires the DEFEND button / a force-start seam. StartWave clears the flag.
            if (_awaitingPlayerStart) return;

            _countdownRemaining -= Time.deltaTime;
            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                OnCountdownTick.Invoke(0f);
                FlowTrace.Step("Wave", $"TickCountdown: countdown hit 0 for wave={_currentWaveId} — calling StartWave (phase Countdown->Active)");
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
            FlowTrace.Step("Wave", $"ForceBeginNextWave (PLAYER 'Defend!' path) phase={_phase}");
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
                    GuardedKickoff("ForceBeginNextWave");
                    break;
            }
        }

        /// <summary>
        /// DEV / bot / "Jump to wave" button: spawn the next wave IMMEDIATELY — skip the
        /// prepare-phase countdown. Distinct from <see cref="ForceBeginNextWave"/> (the normal
        /// kickoff, which keeps the calm countdown). Fixes the AutoPilot 'TriggerWave timeout'
        /// (the bot triggered from Idle → got a 45s countdown → timed out at 20s) and makes the
        /// owner's "Trigger next wave" button actually jump to the wave instead of starting a timer.
        /// </summary>
        public void ForceSpawnNextWaveNow()
        {
            FlowTrace.Step("Wave", $"ForceSpawnNextWaveNow (BOT/jump path) phase={_phase} forceSpawn-set");
            switch (_phase)
            {
                case WavePhase.Countdown:
                    FlowTrace.Step("Wave", $"ForceSpawnNextWaveNow: from Countdown — zero countdown + StartWave({_currentWaveId})");
                    _countdownRemaining = 0f;
                    OnCountdownTick.Invoke(0f);
                    StartWave(_currentWaveId);
                    break;
                case WavePhase.Active:
                    FlowTrace.Step("Wave", "ForceSpawnNextWaveNow during active wave — ignored.");
                    Debug.Log("[WaveManager] ForceSpawnNextWaveNow during active wave — ignored.");
                    break;
                default: // Idle / Complete — kick the loop but zero the countdown so it spawns now.
                    FlowTrace.Step("Wave", $"ForceSpawnNextWaveNow: from {_phase} — setting _forceSpawnNow=true + GuardedKickoff (try/catch + retry-till-active)");
                    _forceSpawnNow = true;
                    GuardedKickoff("ForceSpawnNextWaveNow");
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
            if (wave == null)
            {
                // ENDLESS MODE: replay the cycled authored def with the endless mob-count
                // multiplier baked into a CLONE (the cached schedule is never mutated).
                // The TRUE waveId is what _currentWaveId carries, so WaveScalingCurve keeps
                // scaling HP/speed/damage by the real wave number in every spawn path.
                WaveDef src = ResolveEndlessWaveDef(waveId, out int sourceWaveId);
                if (src != null)
                {
                    float countScale = EndlessCountScale(waveId);
                    wave = CloneWaveWithScaledCounts(src, waveId, countScale);
                    _awaitingPlayerStart = false;
                    // The lookout horn was held back while the endless build phase waited on
                    // the player — blow it now the raid is actually released.
                    GameSfx.PlayLookoutHorn();
                    FlowTrace.Step("Wave",
                        $"endless wave {waveId}: START — def={sourceWaveId} countScale=x{countScale:F2} (player/force released)");
                }
            }
            if (wave == null) { FlowTrace.Step("Wave", $"StartWave: no WaveDef for {waveId} — rolling to next countdown"); EnterCountdown(waveId + 1); return; }

            _phase = WavePhase.Active;
            FlowTrace.Step("Wave", $"StartWave({waveId}) -> phase=Active (spawning begins)");
            _breachArmed = false;
            _breachArmTimer = 0f;
            _breachRoster.Clear();
            _liveApexBoss = null;
            OnWaveStarted.Invoke(waveId);

            // An apex (flying-boss) wave drives the Heart's Boss threat state;
            // a normal wave only raises it to Vigilant.
            if (_heart != null)
                _heart.SetState(wave.IsApexBossWave ? HeartState.Boss : HeartState.Vigilant);

            // WO-362: SMART composition + tactical positioning takes priority. When on,
            // GENERATE the wave's ground roster from the wave number (tiered mix + elite
            // cadence + anti-repeat) and place each enemy by role at a ROTATING gate,
            // instead of releasing the flat waves.json batches. Falls through to the
            // legacy paths if it spawns nothing (e.g. no spawn points / catalog).
            bool composed = false;
            if (_smartComposition)
                composed = SpawnSmartComposedWave(waveId);

            // WO-316: compose the wave's batches into runtime role-mix FAMILY squads
            // (tank + healer + a few DPS that hold then charge together) routed through
            // the EnemyGroupSpawner / EnemyGroupCoordinator. Falls back to the flat
            // per-batch SpawnBatch stream when composition is off or yields nothing
            // (so the legacy path is always available for back-compat).
            if (!composed && _composeFamilyGroups && wave.Enemies != null && wave.Enemies.Count > 0)
                composed = SpawnComposedFamilyGroups(wave);

            if (!composed && wave.Enemies != null)
            {
                // Legacy flat path: each batch spawns on its own delayed UniTask.
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

            // ROBUSTNESS (castle / missing-markers): if every ground-spawn path produced
            // ZERO enemies and this is not a boss/apex wave, the wave would otherwise
            // self-clear on the next TickActiveWave (LiveEnemies == 0) and silently
            // advance — the timer/loop "runs" but no enemy ever appears. Surface it with
            // one clear warning so the cause (no spawn points / empty roster) is obvious.
            bool willHaveBoss = !string.IsNullOrEmpty(wave.Boss) || wave.IsApexBossWave;
            if (_liveEnemies.Count == 0 && !willHaveBoss)
            {
                Debug.LogWarning(
                    $"[WaveManager] Wave {waveId} started but spawned ZERO enemies — it will " +
                    "self-clear instantly. Likely cause: no WaveSpawnPoint markers in the " +
                    "scene (place them, tag 'SpawnPoint', ~12 m outside each gate) or an empty " +
                    "enemy roster/catalog.");
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
        /// WO-316: composes the wave's flat batches into runtime role-mix FAMILY
        /// squads and releases each as ONE coordinated group via the existing
        /// <see cref="EnemyGroupSpawner.SpawnComposedGroup"/> (formation spread,
        /// NavMesh snap, per-member <see cref="EnemyRole"/>, and the
        /// <see cref="EnemyGroupCoordinator"/> "hold then charge together" release).
        ///
        /// Each batch's <c>type</c> resolves to an <see cref="EnemyDef"/>; the def's
        /// <see cref="EnemyDef.Family"/> buckets the batch, its <see cref="EnemyDef.RoleKind"/>
        /// assigns the tactical role, and its <c>count</c> sets how many. So a wave
        /// of "3 hollow-walker + 1 hollow-warrior + 2 hollow-rogue" becomes ONE
        /// Hollow squad of 3 DPS + 1 Tank + 2 Ranged that advance and charge as a
        /// unit, instead of three single-type conga lines.
        ///
        /// Returns true if at least one group was spawned (caller skips the flat
        /// path); false when nothing resolved (caller falls back to flat batches).
        /// </summary>
        private bool SpawnComposedFamilyGroups(WaveDef wave)
        {
            if (wave?.Enemies == null || _enemyCatalog == null) return false;

            // Lazily build a spawner so composition works even when the inspector
            // left _groupSpawner blank (the common case on the live scene).
            if (_groupSpawner == null)
            {
                _groupSpawner = GetComponentInChildren<EnemyGroupSpawner>();
                if (_groupSpawner == null)
                {
                    var go = new GameObject("[EnemyGroupSpawner]");
                    go.transform.SetParent(transform, false);
                    _groupSpawner = go.AddComponent<EnemyGroupSpawner>();
                }
            }

            // Bucket the batches by family, preserving the first spawn point seen
            // for each family so the squad materialises at the gate its batch named.
            var byFamily   = new Dictionary<string, List<ComposedGroupMember>>();
            var familySpawn = new Dictionary<string, string>();

            foreach (WaveBatch batch in wave.Enemies)
            {
                if (batch == null) continue;
                EnemyDef def = _enemyCatalog.Find(batch.Type);
                if (def == null)
                {
                    Debug.LogWarning($"[WaveManager] WO-316 compose: unknown enemy '{batch.Type}' in wave {wave.WaveId} — skipped.");
                    continue;
                }

                string family = string.IsNullOrEmpty(def.Family) ? "hollow" : def.Family;
                if (!byFamily.TryGetValue(family, out var members))
                {
                    members = new List<ComposedGroupMember>();
                    byFamily[family] = members;
                    familySpawn[family] = batch.SpawnPoint;
                }
                members.Add(new ComposedGroupMember(def, def.RoleKind, Mathf.Max(1, batch.Count)));
            }

            if (byFamily.Count == 0) return false;

            Transform heartT = _heart != null ? _heart.transform : null;
            bool spawnedAny = false;

            foreach (var kv in byFamily)
            {
                WaveSpawnPoint pt = FindSpawnPoint(familySpawn[kv.Key]);
                Vector3 spawnPos = pt != null ? pt.transform.position
                                 : (_spawnPoints != null && _spawnPoints.Count > 0 && _spawnPoints[0] != null
                                        ? _spawnPoints[0].transform.position
                                        : transform.position);

                List<Enemy> squad = _groupSpawner.SpawnComposedGroup(
                    kv.Value,
                    spawnPos,
                    heartT,
                    _enemyRoot,
                    _composedFormation,
                    $"{kv.Key}-squad",
                    _currentWaveId,
                    ref _spawnInstanceCounter);

                foreach (Enemy e in squad)
                {
                    if (e == null) continue;

                    // DEF-59: apply wave-scaling parity with the flat SpawnOne path.
                    if (_scalingCurve != null)
                        e.ApplyWaveScaling(
                            _scalingCurve.HpMultiplier(_currentWaveId),
                            _scalingCurve.SpeedMultiplier(_currentWaveId),
                            _scalingCurve.DamageMultiplier(_currentWaveId));

                    e.Died         += HandleEnemyDied;
                    e.ReachedHeart += HandleEnemyReachedHeart;
                    _liveEnemies.Add(e);
                    spawnedAny = true;
                }
            }

            return spawnedAny;
        }

        /// <summary>
        /// WO-362: generates this wave's ground roster via
        /// <see cref="WaveCompositionBuilder.Build"/> (tiered weak/medium/strong mix,
        /// an elite every 5th wave, no two consecutive waves identical, count + difficulty
        /// scaling with the wave number) and releases it through the
        /// <see cref="SmartEnemySpawner"/>, which positions each enemy by tactical role
        /// (tanks front-centre, archers backline, weak trailing) at a gate that ROTATES
        /// N→E→S→W across waves. Subscribes Died / ReachedHeart and applies wave-scaling
        /// with full parity to the legacy <see cref="SpawnOne"/> / compose paths.
        ///
        /// Returns true if at least one enemy spawned (caller skips the legacy paths);
        /// false when nothing resolved (caller falls back to compose / flat batches).
        /// </summary>
        private bool SpawnSmartComposedWave(int waveId)
        {
            if (_enemyCatalog == null) return false;

            // Lazily build the spawner (no inspector wiring needed on the live scene).
            if (_smartSpawner == null)
            {
                _smartSpawner = GetComponentInChildren<SmartEnemySpawner>();
                if (_smartSpawner == null)
                {
                    var go = new GameObject("[SmartEnemySpawner]");
                    go.transform.SetParent(transform, false);
                    _smartSpawner = go.AddComponent<SmartEnemySpawner>();
                }
            }

            EnemyWaveComposition composition =
                WaveCompositionBuilder.Build(waveId, _enemyCatalog);
            if (composition == null || composition.Entries.Count == 0) return false;

            // ENDLESS MODE: the smart path generates its roster from the TRUE wave number
            // (so its own count/difficulty ramp continues past the schedule), but the
            // owner-tunable endless mob-count multiplier from waves.json applies ON TOP —
            // each slot's count is scaled (rounded up, never below the generated count),
            // exactly like the batch paths. No-op (x1) within the authored schedule.
            float endlessScale = EndlessCountScale(waveId);
            if (endlessScale > 1f)
            {
                for (int i = 0; i < composition.Entries.Count; i++)
                {
                    WaveCompositionEntry entry = composition.Entries[i];
                    entry.Count = Mathf.Max(entry.Count, Mathf.CeilToInt(entry.Count * endlessScale));
                    composition.Entries[i] = entry;   // struct — write back
                }
                FlowTrace.Step("Wave",
                    $"endless wave {waveId}: smart composition countScale=x{endlessScale:F2} total={composition.TotalCount}");
            }

            Transform heartT = _heart != null ? _heart.transform : null;
            List<Enemy> squad = _smartSpawner.SpawnWave(
                composition,
                _enemyCatalog,
                _spawnPoints,
                heartT,
                _enemyRoot,
                waveId,
                ref _spawnInstanceCounter);

            bool spawnedAny = false;
            foreach (Enemy e in squad)
            {
                if (e == null) continue;

                // DEF-59: wave-scaling parity with the flat SpawnOne / compose paths.
                if (_scalingCurve != null)
                    e.ApplyWaveScaling(
                        _scalingCurve.HpMultiplier(_currentWaveId),
                        _scalingCurve.SpeedMultiplier(_currentWaveId),
                        _scalingCurve.DamageMultiplier(_currentWaveId));

                e.Died         += HandleEnemyDied;
                e.ReachedHeart += HandleEnemyReachedHeart;
                _liveEnemies.Add(e);
                spawnedAny = true;
            }

            return spawnedAny;
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
                    // U(pgrade Debug->FlowTrace.Fail): the apex wave has no boss to spawn — the
                    // wave's headline threat silently never appears. Fail-loud so a capture knows
                    // the dragon was asked for and the prefab couldn't be resolved.
                    FlowTrace.Fail("Waves",
                        "SpawnApexBoss: Apex wave has no _apexBossPrefab AND no " +
                        "Resources/Enemies/Boss_Dragon fallback — no dragon will spawn.");
                    return;
                }
                FlowTrace.Warn("Waves",
                    "SpawnApexBoss: _apexBossPrefab was null (stale scene) — using the " +
                    "Resources/Enemies/Boss_Dragon fallback so the apex dragon flies.");
            }

            // Spawn the dragon at cruise height above the Heart so it begins its
            // orbit immediately; DragonBoss.Configure re-seeds its anchor + HP.
            Transform heartT = _heart != null ? _heart.transform : null;
            // #66: lower the entry drop from +22 to +10 to match the lowered _orbitHeight (was 22 -> 10)
            // so the smaller (scale 0.3) dragon reads in-frame instead of starting far overhead.
            Vector3 spawnPos = (heartT != null ? heartT.position : transform.position)
                               + new Vector3(0f, 10f, 0f);

            // G(uard the Instantiate): the prefab instantiation can throw on a corrupt/missing
            // asset; an unguarded throw here aborts the whole wave-start coroutine (every later
            // batch is lost). Build under Guard.Try, then NULL-CHECK the result before any deref.
            DragonBoss dragon = null;
            Guard.Try("Waves", "instantiate apex dragon",
                () => dragon = Instantiate(_apexBossPrefab, spawnPos, Quaternion.identity, _enemyRoot));
            if (dragon == null)
            {
                // R(eturn-fallback never silent): no boss body — the wave continues (its batches
                // still clear) but the apex threat is missing. Fail-loud so it self-reports.
                FlowTrace.Fail("Waves",
                    $"SpawnApexBoss: Instantiate returned null for the apex dragon (wave {_currentWaveId}) — " +
                    "no boss this wave (batches still run so the loop never stalls).");
                return;
            }

            string bossId = !string.IsNullOrEmpty(boss.Id)
                ? boss.Id
                : $"wave{_currentWaveId}-apex-boss";
            dragon.Configure(bossId, heartT, boss.Hp);

            dragon.Died += HandleApexBossDied;
            _liveApexBoss = dragon;
            OnApexBossSpawned.Invoke(dragon);

            FlowTrace.Step("Waves",
                $"SpawnApexBoss: Apex wave {_currentWaveId} — released flying boss '{bossId}' " +
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
                // U + R: an unknown enemy type means this WHOLE batch never spawns — silent under
                // a bare LogError. Fail-loud so the missing batch self-reports in a capture.
                FlowTrace.Fail("Waves",
                    $"SpawnBatch: wave batch references unknown enemy '{batch.Type}' — batch skipped (0 spawned).");
                return;
            }

            WaveSpawnPoint point = FindSpawnPoint(batch.SpawnPoint);
            if (point == null)
            {
                FlowTrace.Fail("Waves",
                    $"SpawnBatch: no WaveSpawnPoint '{batch.SpawnPoint}' in the scene — " +
                    "batch skipped (0 spawned). Place the spawn markers (see docs/port-notes/week4-waves.md).");
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
            // WO-430: lateral spread tightened 4.5→3 m — the wider spread pushed the
            // pre-sample XZ OUTSIDE the baked NavMesh boundary more often, and a miss there
            // used to strand the enemy at the raw spawn-point Y (floating/underground).
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);
            Vector3 rawPoint = point.transform.position;
            Vector3 pos = rawPoint
                        + lateral * UnityEngine.Random.Range(-SpawnLateralSpread, SpawnLateralSpread)
                        + heading * UnityEngine.Random.Range(-SpawnDepthSpread, SpawnDepthSpread);

            // Snap the spawn position to the nearest NavMesh sample so a
            // slightly-off-mesh spawn point doesn't strand the enemy off-mesh
            // (NavMeshAgent.isOnNavMesh would stay false → enemy never moves).
            // Sample within a generous 8 m radius; on a miss, DON'T silently keep the
            // spread XZ at the raw spawn-point Y (WO-430: that was the sky/underground
            // spawn — the miss was silent). Warn (§12: never silent), then ground-snap
            // via a downward raycast so the enemy at least lands on the visible surface.
            if (UnityEngine.AI.NavMesh.SamplePosition(
                    pos, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pos = hit.position;
            }
            else
            {
                FlowTrace.Warn("Waves",
                    $"SpawnOne: NavMesh.SamplePosition MISS (no mesh within 8 m) for def " +
                    $"'{def?.Id ?? "<null>"}' — spawnPoint={rawPoint}, attemptedPos={pos}. " +
                    $"Falling back to ground-snap raycast instead of the raw spawn Y (would float/sink).");

                // Ground/terrain/default layers only (mirrors Enemy.cs SnapBodyToGround) — this
                // naturally excludes the enemy's own collider (on the "Enemy" layer). Cast DOWN
                // from well above the attempted XZ onto the visible surface and spawn at that Y.
                int groundMask = LayerMask.GetMask("Default", "Terrain", "Ground");
                if (groundMask == 0) groundMask = Physics.DefaultRaycastLayers;
                Vector3 rayOrigin = new Vector3(pos.x, pos.y + 50f, pos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out var groundHit, 200f,
                        groundMask, QueryTriggerInteraction.Ignore))
                {
                    pos.y = groundHit.point.y;
                }
                else
                {
                    // Last resort: keep the raw spawn-point Y (never worse than pre-WO-430).
                    FlowTrace.Warn("Waves",
                        $"SpawnOne: ground-snap raycast ALSO missed at XZ=({pos.x:F1},{pos.z:F1}) for def " +
                        $"'{def?.Id ?? "<null>"}' — using spawn-point Y {rawPoint.y:F1} as last resort.");
                    pos.y = rawPoint.y;
                }
            }

            // POOLED: route through EnemyPool so a dead enemy's body is reused instead
            // of Instantiate-per-spawn / Destroy-on-death (the per-spawn GameObject churn
            // that was the main GC / stray-accumulation source). The pool keys on the
            // prefab name (prefab path) or the EnemyDef model id (factory path) so a
            // reused body matches the kind asked for; it builds fresh on a drain. The
            // CALLER still Configures + scales + hooks events below, exactly as before.
            //
            // T-011 ("all enemies one type / friendly enemies"): the single serialized
            // _enemyPrefab used to OVERRIDE every def here — keying the pool on the one
            // prefab name and handing the SAME body out for hollow-walker / -warrior /
            // -rogue / necromancer alike, so a mixed wave read as clones. The varied
            // family bodies come from EnemyFactory.Build (model-keyed per ModelForEnemy);
            // route any def that resolves to a real model through the FACTORY path so the
            // flat batch stream is as varied as the smart/family paths already are. The
            // _enemyPrefab is kept ONLY as a genuine fallback for a def with no usable
            // model (it never overrides a valid def again).
            string model = EnemyFactory.ModelForEnemy(def);
            bool useFactory = !string.IsNullOrEmpty(model);
            string poolKey = useFactory ? "model:" + model : "prefab:" + _enemyPrefab.name;
            Enemy enemy = EnemyPool.Get(poolKey, useFactory ? null : _enemyPrefab, def, pos, rot, _enemyRoot);
            if (enemy == null)
            {
                // R(eturn-fallback never silent): the pool couldn't lease/build a body for this
                // def — the wave is now SHORT one enemy and the loop's clear-count can never be
                // met (a silent stall). Fail-loud naming the def + pool key so a capture pinpoints
                // which spawn died instead of a blank "enemy never appeared".
                FlowTrace.Fail("Waves",
                    $"SpawnOne: EnemyPool.Get returned null for def '{def?.Id ?? "<null>"}' (poolKey='{poolKey}', " +
                    $"useFactory={useFactory}, model='{model}') — enemy NOT spawned; wave is short one body.");
                return;
            }

            // V(erify the spawned enemy actually RENDERS + is ON the NavMesh): a leased body with
            // no enabled renderer is an invisible enemy; an off-mesh NavMeshAgent never moves
            // (isOnNavMesh==false) and so never reaches the Heart — the wave then stalls with a
            // live-but-frozen enemy. Both are Warn (skip-not-abort: the enemy still counts toward
            // the wave + can still be configured) so a capture splits "invisible" vs "stranded".
            VerifySpawnedEnemy(enemy, def, pos);

            // The hero/pet target sweeps find enemies via GetComponentInParent<IDamageable>,
            // which resolves to EnemyDamageable. The placeholder capsule (and some prefabs)
            // don't carry it — add it so the hero/pets can actually ACQUIRE + DAMAGE this
            // wave enemy. Without it the enemy marches + attacks but is INVULNERABLE to you.
            // (EnemyPool also guarantees this on a fresh build; kept for prefab parity.)
            if (enemy.GetComponent<EnemyDamageable>() == null)
                enemy.gameObject.AddComponent<EnemyDamageable>();

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
        /// V(erify) a just-spawned wave enemy can actually be SEEN and can MOVE: it must carry
        /// at least one enabled Renderer (else it's invisible) and, if it drives a NavMeshAgent,
        /// that agent must be on the NavMesh (else it's stranded and never reaches the Heart —
        /// the wave silently stalls). Anomalies are Warn'd (skip-not-abort) so a capture pinpoints
        /// the broken spawn; the enemy is left live (it still counts toward the wave).
        /// </summary>
        private void VerifySpawnedEnemy(Enemy enemy, EnemyDef def, Vector3 pos)
        {
            if (enemy == null) return;

            int total = 0, enabledR = 0;
            var renderers = enemy.GetComponentsInChildren<Renderer>(true);
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    total++;
                    if (r.enabled) enabledR++;
                }
            }
            if (enabledR == 0)
                FlowTrace.Warn("Waves",
                    $"VerifySpawnedEnemy: enemy '{def?.Id ?? "<null>"}' on '{enemy.gameObject.name}' has " +
                    $"NO enabled Renderer ({total} found) — it will spawn INVISIBLE.");

            var agent = enemy.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && !agent.isOnNavMesh)
                FlowTrace.Warn("Waves",
                    $"VerifySpawnedEnemy: enemy '{def?.Id ?? "<null>"}' on '{enemy.gameObject.name}' is OFF the NavMesh " +
                    $"at {pos} (agent.isOnNavMesh==false) — it will NOT move toward the Heart; wave may stall.");
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

            // WO-579 (#4 — owner felt-test 2026-06-28): the village wave resolves IN-HUB (towers + hero
            // auto-defend; enemies contact-attack the Heart : IDamageableStructure; Heart at 0 = defeat).
            // The legacy breach-ring → ATBBattle hand-off (the "wave ~3 auto-launches ATB" symptom — an
            // escalating brute reaches the tree ring) is gated OFF by default. With it off, no scene swap
            // ever happens, so placed towers + the wave counter never reset across the (removed) detour.
            if (FeatureFlags.WaveBreachToAtb && _breachArmed && _heart != null)
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

            // WO-579 (#5): advance + PERSIST the run progress so a hub reload / scene return resumes at
            // the next wave instead of restarting at 1. The static carries it within the play session;
            // RecordRun writes BestWave to the save (the cross-session resume seed) and saves.
            s_resumeWaveId = cleared + 1;
            GameStateService.Instance?.RecordRun(cleared);

            // ENDLESS: an endless clear persists exactly like an authored clear (BestWave has
            // no upper clamp — SaveSchema only floors it at 0), and the resume seed lands the
            // returning player back in the endless awaiting-player state, never Complete.
            if (_schedule != null && cleared > _schedule.MaxWaveId)
                FlowTrace.Step("Wave", $"endless wave {cleared}: CLEARED — resume seed -> {cleared + 1}");

            AwardWaveResources(cleared);   // WO-330: build resources (Wood/Iron) — the core economy income
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
        /// WO-330 — the city-defense fundamental loop's payout: clearing a wave grants
        /// BUILD RESOURCES (Wood/Iron, optional Food) into the player's wallet via
        /// <see cref="EconomyService.Grant(ResourceCost)"/> — the same pool the BuildMenu /
        /// upgrade paths spend from. This is the primary economy income: defend the city →
        /// defeat the wave → earn the resources you build/upgrade defenses with → harder
        /// waves → repeat.
        ///
        /// WO-361 — the payout is STAGGERED by interval rather than every wave:
        ///   • Food  every <see cref="_foodRewardInterval"/>th wave (default 2nd), 30–50
        ///   • Wood  every <see cref="_woodRewardInterval"/>th wave (default 3rd), 20–30
        ///   • Iron  every <see cref="_ironRewardInterval"/>th wave (default 4th), 15–25
        /// Each amount is randomized in [Base .. Base+Spread] and SCALES with the wave
        /// number — +<see cref="_rewardScalePerStep"/> (default +20%) every
        /// <see cref="_rewardScaleWaveStep"/> waves (default 5), optionally clamped by
        /// <see cref="_rewardScalingStepCap"/>. All amounts/intervals are inspector-tunable
        /// so balance changes need no code edit. Crystals stay on the separate
        /// <see cref="AwardWaveCrystals"/> path.
        ///
        /// No-ops cleanly when <see cref="_awardResourcesOnWaveClear"/> is off, when nothing
        /// is due this wave, or when no EconomyService exists yet (early boot).
        /// </summary>
        private void AwardWaveResources(int waveId)
        {
            if (!_awardResourcesOnWaveClear) return;

            var economy = EconomyService.Instance;
            if (economy == null) return;

            // Scaling multiplier: +scalePerStep every scaleWaveStep waves, capped.
            int step = waveId / Mathf.Max(1, _rewardScaleWaveStep);
            if (_rewardScalingStepCap > 0) step = Mathf.Min(step, _rewardScalingStepCap);
            float scale = 1f + _rewardScalePerStep * step;

            // WO-676 STEWARD capstone (Bountiful Banners): ONE HeroTalentModifiers read at
            // this existing reward grant — `waveReward` folds into the same scale multiplier
            // the wood/iron/food rolls already apply, so every wave-clear payout grows
            // together. StatSum is internally null-safe (0 with no service/tree/nodes), so
            // rewards are byte-identical to baseline at sum 0.
            float rewardBonus = DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                HeroTalentClassReader.Slug(), "waveReward");
            if (rewardBonus > 0f)
            {
                scale *= 1f + rewardBonus;
                FlowTrace.Once("Talent", "waveReward",
                    $"waveReward x{1f + rewardBonus:0.###} applied to wave-clear rewards (WO-676 Bountiful Banners).");
            }

            // WO-330 wiring contract: the per-wave ramp fields raise the effective base
            // floor by (perWave * waveId) before the random spread + scaling are applied,
            // so they stay LIVE alongside the WO-361 staggered/scaled payout.
            int woodBase = _woodRewardBase + _woodRewardPerWave * waveId;
            int ironBase = _ironRewardBase + _ironRewardPerWave * waveId;

            int wood = DueThisWave(waveId, _woodRewardInterval)
                ? ScaledRoll(woodBase, _woodRewardSpread, scale) : 0;
            int iron = DueThisWave(waveId, _ironRewardInterval)
                ? ScaledRoll(ironBase, _ironRewardSpread, scale) : 0;
            int food = DueThisWave(waveId, _foodRewardInterval)
                ? ScaledRoll(_foodRewardBase, _foodRewardSpread, scale) : 0;

            if (wood <= 0 && iron <= 0 && food <= 0) return;

            economy.Grant(new ResourceCost(wood: wood, food: food, iron: iron));

            // Brief on-victory loot toast (reuses the existing combat feedback if present).
            ShowRewardToast(wood, iron, food);

            Debug.Log(
                $"[WaveManager] Wave {waveId} cleared — granted build resources (×{scale:0.0} scale): " +
                string.Join(", ", BuildRewardParts(wood, iron, food)) +
                " (defend → earn → build).");
        }

        /// <summary>True when a payout with the given interval is due on this wave.
        /// Interval 0/1 = every wave; otherwise every Nth wave (waveId % N == 0).</summary>
        private static bool DueThisWave(int waveId, int interval)
        {
            if (interval <= 1) return true;
            return waveId % interval == 0;
        }

        /// <summary>Randomized, wave-scaled amount: Round(Random[base..base+spread] * scale).</summary>
        private static int ScaledRoll(int baseAmount, int spread, float scale)
        {
            if (baseAmount <= 0 && spread <= 0) return 0;
            int rolled = baseAmount + (spread > 0 ? UnityEngine.Random.Range(0, spread + 1) : 0);
            return Mathf.Max(0, Mathf.RoundToInt(rolled * scale));
        }

        private static System.Collections.Generic.List<string> BuildRewardParts(int wood, int iron, int food)
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if (wood > 0) parts.Add($"{wood} Wood");
            if (iron > 0) parts.Add($"{iron} Iron");
            if (food > 0) parts.Add($"{food} Food");
            if (parts.Count == 0) parts.Add("nothing");
            return parts;
        }

        /// <summary>
        /// Best-effort floating loot/notification on wave victory. Resolves the HUD's
        /// banner setter by cached reflection (HUD → Core only; Village cannot reference
        /// DeNelle.HUD), exactly like the town-HUD bridges. Fully guarded — a missing HUD
        /// or method simply skips the toast (never throws; WebGL halts on an uncaught throw).
        /// </summary>
        private void ShowRewardToast(int wood, int iron, int food)
        {
            try
            {
                var hud = DeNelle.Core.CoreServices.Hud as object;
                if (hud == null) return;
                var parts = BuildRewardParts(wood, iron, food);
                string msg = "Wave cleared! +" + string.Join("  +", parts);

                // Prefer a dedicated banner/toast setter if the HUD exposes one.
                var t = hud.GetType();
                var m = t.GetMethod("ShowBanner",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                            null, new[] { typeof(string) }, null)
                     ?? t.GetMethod("ShowToast",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                            null, new[] { typeof(string) }, null);
                m?.Invoke(hud, new object[] { msg });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WaveManager] reward toast skipped: " + e.Message);
            }
        }

        /// <summary>
        /// Evaluates boss-wave drops and active-event bonuses from <see cref="ServerConfig"/>
        /// and credits the won crystals to the BUILD-SPEND pool
        /// (<see cref="GameState.Resources"/>.Crystals) via
        /// <see cref="GameStateService.AddCrystals"/> — the exact balance the
        /// BuildMenu spends from and the village HUD top-bar displays (WO-131
        /// follow-up, owner decision (a): "win waves → build towers" on one
        /// currency the player sees). This deliberately does NOT touch the
        /// separate AetherCrystals empower pool (CrystalEconomy); every other
        /// AetherCrystals source (CrystalMine, MineNode, tower-empower, TalentTree,
        /// BattlePass, Referral, Promo) stays on AetherCrystals.
        /// Drop chance and ranges are all backend-controlled — no rebuild needed to tune.
        /// </summary>
        private void AwardWaveCrystals(int waveId)
        {
            var cfg = GameStateService.Instance != null
                ? GameStateService.Instance.ServerConfig
                : ServerConfig.Default;

            int totalAward = 0;

            // ── Boss-wave drop (every Nth wave, chance-based) ─────────────────
            // Guard the interval: a backend ServerConfig can deliver BossWaveInterval = 0
            // (non-null, so the ?? fallback to 5 does NOT apply), which would make
            // `waveId % 0` throw DivideByZeroException and abort the wave-clear reward
            // path right after OnWaveCleared fired. Clamp to ≥1 so the modulo is safe.
            int bossInterval = Mathf.Max(1, cfg.BossInterval);
            if (waveId % bossInterval == 0)
            {
                if (UnityEngine.Random.value <= cfg.DropChance)
                {
                    int drop = UnityEngine.Random.Range(cfg.DropMin, cfg.DropMax + 1);
                    totalAward += drop;
                    Debug.Log($"[WaveManager] Boss-wave crystal drop — wave {waveId} awarded {drop} Crystal(s) to the build-spend pool. " +
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
                GameStateService.Instance?.AddCrystals(totalAward);
        }

        // =====================================================================
        //  Defeat — the Heart fell (village lose condition, WO-125 Bug 3)
        // =====================================================================

        /// <summary>
        /// WO-125 Bug 3 — the Heart of Elarion fell (HP 0). This is the village's
        /// real LOSE condition: the dragon (or any source) drained the Heart, and
        /// previously nothing reacted. Halt the wave loop into the terminal
        /// <see cref="WavePhase.Defeated"/> state (no further countdown/spawn ticks),
        /// stop the live boss so it can't keep striking a dead Heart, and raise
        /// <see cref="OnDefeat"/> so a bound defeat screen can present "Elarion has
        /// fallen." Fires at most once (HeartController guards the source event).
        /// </summary>
        private void HandleHeartDestroyed()
        {
            if (_phase == WavePhase.Defeated) return;   // already lost — idempotent
            _phase = WavePhase.Defeated;

            // Stop the apex boss mid-encounter — a dead Heart should not keep taking
            // swoop/breath hits, and the boss's death-fall would otherwise read oddly.
            if (_liveApexBoss != null)
            {
                _liveApexBoss.Died -= HandleApexBossDied;
                _liveApexBoss.Kill();
                _liveApexBoss = null;
            }

            // Show the Heart at its terminal critical state for the defeat beat.
            _heart?.SetState(HeartState.Critical);

            Debug.Log("[WaveManager] The Heart of Elarion has fallen — village defeat. Wave loop halted.");
            OnDefeat?.Invoke();
        }

        // =====================================================================
        //  Breach -> ATB hand-off
        // =====================================================================

        /// <summary>
        /// One or more enemies crossed the inner ring. Pauses the loop and hands
        /// off to the ATB "Last Stand" battle. (The real-time "Defend the Tower"
        /// shooter / PatriciaLight branch has been retired — the breach now routes
        /// straight to the ATB path so a breach is never a dead end.)
        /// </summary>
        private void TriggerBreach()
        {
            _phase = WavePhase.Breached;
            OnBreach.Invoke(_currentWaveId);
            if (_heart != null) _heart.SetState(HeartState.Critical);

            // Snapshot the breaching roster's count BEFORE EnterAtbBattle consumes
            // the roster (it keeps using the breach roster verbatim).
            int breachCount = _breachRoster.Count;

            Debug.Log(
                $"[WaveManager] Wave {_currentWaveId} breached with {breachCount} enemies — " +
                "handing off to the ATB Last Stand.");

            EnterAtbBattle();
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
                _enemyBestSqr.Remove(enemy);     // bug-triage P1: prune stuck-tracking on normal death (was leaking dead keys)
                _enemyStuckTime.Remove(enemy);
            }
            _liveEnemies.Remove(enemy);

            // WO-330: optional small per-kill resource trickle (the wave-clear bonus is
            // the primary reward). Only paid while the wave is still ACTIVE so the forced
            // removals on a breach hand-off (phase Breached) / defeat don't pay out, and
            // only for a real death (the enemy actually reached zero HP, not a stuck-cull
            // Kill()). Routes through the same EconomyService wallet as the wave-clear bonus.
            if (_awardResourcesPerKill
                && _phase == WavePhase.Active
                && enemy != null && enemy.Hp <= 0f
                && (_woodPerKill > 0 || _ironPerKill > 0))
            {
                EconomyService.Instance?.Grant(wood: _woodPerKill, iron: _ironPerKill);
            }
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
            // WO-579 (#4): in-hub resolution (default) — an enemy that reaches the Heart simply
            // contact-attacks it (Enemy.TickContactAttack strikes HeartController : IDamageableStructure),
            // draining its HP toward the defeat condition. The wave does NOT pause/load the deprecated
            // ATBBattle scene. Only the legacy breach→ATB route (ff.wavebreachtoatb) still hands off.
            if (!FeatureFlags.WaveBreachToAtb) return;
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
            if (_spawnPoints == null) return null;
            if (!string.IsNullOrEmpty(spawnId))
                foreach (WaveSpawnPoint p in _spawnPoints)
                    if (p != null && p.SpawnId == spawnId) return p;
            // Bug-fix (audit 2026-05-30): a missing named id (e.g. boss "spawn-0") used to skip the
            // whole batch, so the boss/apex never spawned. Fall back to the first valid spawn point.
            foreach (WaveSpawnPoint p in _spawnPoints)
                if (p != null)
                {
                    if (!string.IsNullOrEmpty(spawnId))
                        Debug.LogWarning($"[WaveManager] spawn '{spawnId}' not found — using first spawn point.");
                    return p;
                }
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
