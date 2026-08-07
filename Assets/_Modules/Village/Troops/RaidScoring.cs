// =============================================================================
// RaidScoring — the V1 raid SCORER + live clock (WO-771.6, LOCKED teleport/deploy
// loop). It is the missing "win/stars/loot" half the raid spine flagged OUT
// (RaidDeployController.cs:27, RaidVictoryController.cs:34).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES (V1 = PvE, reuse the real-time combat — NO deterministic sim; that
// is V2 / WO-771.3):
//   * Owns the 180s raid CLOCK (elapsed counts up from raid start).
//   * Reads the live garrison via RaidGarrisonSpawner (TotalGarrison / AliveCount /
//     Cleared) — the SAME clear signals the victory path already fires on — to
//     derive %-destruction with NO new combat code.
//   * Tracks troops deployed (RaidDeployController pushes each drop) + a simple
//     RaidDeployLog for re-watch (order/time/place — not byte-exact).
//   * Computes a 0-3 STAR result from: garrison cleared / boss down / within the
//     clock (design B5) via the PURE static ComputeStars — unit-testable, no scene.
//   * Computes resource LOOT scaled by stars + destruction via the PURE static
//     ComputeLoot (the victory path grants it through the village EconomyService).
//   * Fires OnTimeExpired once when the clock runs out (the HUD / retreat path ends
//     the raid) so a raid can never run forever.
//
// SELF-INSTALL: mirrors RaidDeployController / RaidVictoryController — one instance
// per RaidBase_* scene (idempotent). ASCII-only. Canon: Elarion (never Avalon).
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.World.Camps;   // RaidGarrisonSpawner

namespace DeNelle.Village
{
    /// <summary>
    /// The V1 raid scorer: owns the raid clock, reads the live garrison for
    /// %-destruction, tracks deployed troops + a re-watch log, and settles a
    /// <see cref="RaidResult"/> (0-3 stars) plus the loot payout. Self-installs into
    /// any <c>RaidBase_*</c> scene; read via <see cref="Instance"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidScoring : MonoBehaviour
    {
        /// <summary>The live scorer for the current raid scene (null outside a raid).</summary>
        public static RaidScoring Instance { get; private set; }

        [Header("Clock")]
        [Tooltip("Raid clock in seconds (design B5 = 180s). A full clear UNDER this earns the 3rd star; " +
                 "when it runs out the raid ends (OnTimeExpired). Owner tunes by feel.")]
        [SerializeField] private float _clockSeconds = 180f;

        [Header("Loot (owner tunes by feel)")]
        [Tooltip("Crystals granted at 100% destruction, before the per-star bonus.")]
        [SerializeField] private int _lootCrystalsBase = 40;
        [Tooltip("Food granted at 100% destruction, before the per-star bonus.")]
        [SerializeField] private int _lootFoodBase = 60;
        [Tooltip("Extra crystals per earned star.")]
        [SerializeField] private int _lootCrystalsPerStar = 15;
        [Tooltip("Extra food per earned star.")]
        [SerializeField] private int _lootFoodPerStar = 20;

        // ── Runtime ───────────────────────────────────────────────────────────
        private float _elapsed;
        private bool _finalized;
        private bool _timeExpiredFired;
        private RaidGarrisonSpawner _spawner;
        private RaidSpire _spire;         // the OBJECTIVE (null in a legacy spire-less raid)
        private int _garrisonTotalPeak;   // stable total once the staggered spawn settles

        // ── The STRUCTURES census (WO-853 sec.7) ──────────────────────────────
        // CAPTURED ONCE at raid start, then only read. Walls and turrets are baked into the
        // scene and exist from load, so unlike the staggered garrison this needs no peak
        // tracking - the denominator is simply how many stood when the raid began.
        //
        // The COMPONENTS are cached, not re-found per frame: DestructionPct is read by the
        // HUD every frame, and a FindObjectsByType sweep per frame in a property getter is
        // how a scorer becomes a profiler entry.
        //
        // A razed structure that Unity later destroys becomes fake-null in these arrays and
        // is skipped, contributing 0 to the surviving-HP sum while the denominator holds -
        // so "destroyed and removed" and "standing at 0 HP" score identically, which is the
        // behaviour we want and the reason the denominator is captured rather than counted live.
        private WallSegment[] _startWalls = System.Array.Empty<WallSegment>();
        private DefenseTower[] _startTowers = System.Array.Empty<DefenseTower>();
        private int _structuresTotalAtStart;
        private int _deployedCount;
        private readonly RaidDeployLog _deployLog = new RaidDeployLog();
        private RaidResult _result;

        /// <summary>Raised ONCE when the raid clock runs out (before finalize).</summary>
        public event Action OnTimeExpired;

        // ── Live readouts (the HUD binds these; all null-safe) ─────────────────

        /// <summary>Seconds elapsed since the raid began.</summary>
        public float ElapsedSeconds => _elapsed;
        /// <summary>The raid clock length (seconds).</summary>
        public float ClockSeconds => _clockSeconds;
        /// <summary>Seconds left on the clock (0 once expired).</summary>
        public float RemainingSeconds => Mathf.Max(0f, _clockSeconds - _elapsed);
        /// <summary>True once the raid has settled (victory / timeout).</summary>
        public bool Finalized => _finalized;

        /// <summary>The garrison's stable total defender count (peak observed during spawn).</summary>
        public int GarrisonTotal => _garrisonTotalPeak;
        /// <summary>Living garrison defenders right now.</summary>
        public int GarrisonAlive => _spawner != null ? _spawner.AliveCount : 0;

        // =====================================================================
        //  THE OBJECTIVE (owner concept 2026-08-02): a raid is won by razing the
        //  central SPIRE, not by counting corpses. The garrison is still SCORED -
        //  it just is not the win condition any more.
        // =====================================================================

        /// <summary>
        /// Share of the destruction readout owned by the SPIRE. The remainder is the
        /// garrison clear. Kept public so the HUD copy, the loot floor and the oracle all
        /// read the same number instead of re-deriving it.
        /// WHY THE GARRISON IS STILL PAID: the objective decides WIN/LOSE, but a player who
        /// rushes the spire past a full, living garrison should not be paid the same as
        /// one who dismantled the base. Splitting it keeps both worth doing.
        /// </summary>
        public const float SpireWeight = 0.50f;

        /// <summary>
        /// Share of the destruction readout owned by the enemy base's STRUCTURES - its walls
        /// and its garrison turrets (<see cref="StructuresRazedPct"/>).
        ///
        /// OWNER RULING 2026-08-07 (WO-853 sec.7): the split is 50% spire / 30% structures /
        /// 20% garrison. It was 60/0/40 - structures counted for NOTHING, which is why a raid
        /// read as a fight and never a demolition. This term is the whole point of WO-853: the
        /// seam that made walls, gates and enemy turrets damageable already shipped, but until
        /// now breaking them changed no number the player could see.
        ///
        /// The spire stays the largest single term, so the objective is still the objective.
        /// </summary>
        public const float StructuresWeight = 0.30f;

        /// <summary>
        /// Share owned by the garrison clear - whatever the spire and structures do not take.
        /// DERIVED, never a fourth literal: three hand-typed weights are three chances to
        /// publish a split that does not sum to 1.
        /// </summary>
        public static float GarrisonWeight => 1f - SpireWeight - StructuresWeight;

        /// <summary>True when this raid actually has a spire objective (false in a legacy raid base).</summary>
        public bool HasObjective => _spire != null;

        /// <summary>True once the spire has been razed - the raid is won.</summary>
        public bool ObjectiveComplete => _spire != null && _spire.IsDestroyed;

        /// <summary>Spire HP remaining as 0..1 (1 = untouched). 0 when there is no spire.</summary>
        public float ObjectiveHpFraction => _spire != null ? _spire.HpFraction : 0f;

        /// <summary>
        /// The condition that ends the raid in a WIN: the spire falls. A legacy raid base
        /// with no spire falls back to the old "garrison wiped" rule, so nothing that
        /// shipped before regresses.
        /// </summary>
        public bool RaidWon => _spire != null
            ? _spire.IsDestroyed
            : (_spawner != null && _spawner.Cleared);

        /// <summary>Living garrison fraction razed, 0..1 (1 once the garrison is wiped).</summary>
        private float GarrisonRazedPct
        {
            get
            {
                if (_spawner != null && _spawner.Cleared) return 1f;
                int total = _garrisonTotalPeak;
                if (total <= 0) return 0f;
                int alive = GarrisonAlive;
                return Mathf.Clamp01((total - alive) / (float)total);
            }
        }

        /// <summary>
        /// True when this raid base actually had structures to raze at start. False in a
        /// legacy base with no walls and no garrison turrets - the structures term then
        /// carries no information and is redistributed rather than scored as untouched.
        /// </summary>
        public bool HasStructures => _structuresTotalAtStart > 0;

        /// <summary>Structures standing at raid start (walls + enemy-owned turrets).</summary>
        public int StructuresTotal => _structuresTotalAtStart;

        /// <summary>
        /// Fraction of the base's STRUCTURES razed, 0..1 (1 = every wall and turret flattened).
        ///
        /// Both terms are read through <c>HpFraction</c>, the shared 0..1 abstraction, NOT
        /// through WallSegment's raw <c>Damage</c>. A wall stores an INVERTED 0-100 damage
        /// track (WallSegment.cs:99-100, 180-189), so "1 - Damage/100" is only equal to
        /// HpFraction while MaxHp happens to be exactly 100. Reading HpFraction means a later
        /// change to that constant cannot silently skew raid scores.
        ///
        /// Turrets are counted only when NOT PlayerOwned - a raid scores the defender's base,
        /// and any tower the player owns is not part of what they came to demolish.
        /// </summary>
        public float StructuresRazedPct
        {
            get
            {
                if (_structuresTotalAtStart <= 0) return 0f;

                float standing = 0f;
                for (int i = 0; i < _startWalls.Length; i++)
                {
                    var w = _startWalls[i];
                    if (w != null) standing += Mathf.Clamp01(w.HpFraction);
                }
                for (int i = 0; i < _startTowers.Length; i++)
                {
                    var t = _startTowers[i];
                    if (t != null) standing += Mathf.Clamp01(t.HpFraction);
                }

                return Mathf.Clamp01(1f - standing / _structuresTotalAtStart);
            }
        }

        /// <summary>
        /// Live destruction fraction 0..1 - the number behind the HUD's "Base Razed" bar.
        ///
        /// FULL FORM (spire + structures present): the owner's 50/30/20 blend of spire damage,
        /// structures razed and garrison razed (WO-853 sec.7, ruled 2026-08-07). Before this
        /// it was 60/0/40 and breaking a wall moved nothing.
        ///
        /// DEGRADATION - why the missing term is REDISTRIBUTED, not scored as zero. A legacy
        /// base with no spire, or one with no walls or turrets, would otherwise be incapable
        /// of ever reaching 100% razed: the absent term would sit permanently at 0 and cap the
        /// bar at 70% or 50%. That silently breaks the star thresholds and the loot scale for
        /// every scene that predates the term. So an absent term's weight is dropped and the
        /// survivors are RENORMALISED, which preserves their ratio to each other and keeps the
        /// ceiling at 1.0. With no spire AND no structures this collapses to the original
        /// pure-garrison count, so nothing that shipped before WO-771.6 regresses.
        /// </summary>
        public float DestructionPct
        {
            get
            {
                float garrison = GarrisonRazedPct;
                bool hasSpire = _spire != null;
                bool hasStructures = _structuresTotalAtStart > 0;

                if (!hasSpire && !hasStructures) return garrison;   // legacy: corpse count only

                float wSpire = hasSpire ? SpireWeight : 0f;
                float wStruct = hasStructures ? StructuresWeight : 0f;
                float wGarrison = GarrisonWeight;

                float total = wSpire + wStruct + wGarrison;
                if (total <= 0f) return garrison;                   // unreachable; never divide by 0

                float blended = 0f;
                if (hasSpire) blended += Mathf.Clamp01(_spire.DamagedFraction) * wSpire;
                if (hasStructures) blended += StructuresRazedPct * wStruct;
                blended += garrison * wGarrison;

                return Mathf.Clamp01(blended / total);
            }
        }

        /// <summary>Troops the player has deployed this raid (running total).</summary>
        public int TroopsDeployed => _deployedCount;

        /// <summary>Deployed troops still alive on the field right now.</summary>
        public int TroopsAlive
        {
            get
            {
                int n = 0;
                var troops = FindObjectsByType<TroopController>(FindObjectsSortMode.None);
                for (int i = 0; i < troops.Length; i++)
                    if (troops[i] != null && troops[i].IsAlive) n++;
                return n;
            }
        }

        /// <summary>
        /// The LIVE projected star tier if the raid settled at this instant. The "cleared"
        /// axis is now the OBJECTIVE (<see cref="RaidWon"/>), not the corpse count - the
        /// star ladder itself (ComputeStars) is untouched, only what feeds it.
        /// </summary>
        public int ProjectedStars =>
            ComputeStars(RaidWon,
                         RaidWon,                                 // the objective IS the boss kill now
                         DestructionPct, _elapsed, _clockSeconds, SurvivalPct);

        /// <summary>The simple re-watch record (order/time/place of every deploy).</summary>
        public RaidDeployLog DeployLog => _deployLog;

        /// <summary>The settled result once <see cref="Finalize"/> ran (null until then).</summary>
        public RaidResult Result => _result;

        // =====================================================================
        //  PURE static scoring math — unit-testable with NO scene / GameObject.
        // =====================================================================

        /// <summary>
        /// Fraction of DEPLOYED troops that must still be standing for a raid to count as
        /// "high survival" on the star ladder. Public so HUD copy and oracles read the same
        /// number instead of re-deriving it. Balance knob - tune here, one place.
        /// </summary>
        public const float HighSurvivalPct = 0.70f;

        /// <summary>
        /// The V1 star ladder (OWNER RULING 2026-07-30 - the "premium" model). Two axes,
        /// both earned, on top of clearing the base:
        /// <list type="bullet">
        /// <item><b>1 star</b> - you just cleared it.</item>
        /// <item><b>2 stars</b> - cleared with high survival <b>OR</b> under the clock.</item>
        /// <item><b>3 stars</b> - cleared with high survival <b>AND</b> under the clock.</item>
        /// </list>
        /// Sub-clear credit is unchanged: >= 50% of the garrison razed still pays 1 star, so a
        /// retreat that did real damage keeps its loot.
        ///
        /// WHY THIS REPLACED THE OLD FORMULA: the old ladder floored a clear at 2 and gave 3 for
        /// any clear inside the clock - and a raid that OVERRAN the clock never reached the
        /// victory path at all (the clock ends it via OnTimeExpired -> retreat). So EVERY
        /// victory scored 3 stars, which made the 3-star gate meaningless and, once victory
        /// started paying veterancy, promoted every survivor of every win.
        ///
        /// NOTE on <paramref name="bossDestroyed"/>: its old floor of 2 is now a floor of 1. In
        /// V1 the boss is part of the garrison, so <c>bossDown == cleared</c> (see Finalize) -
        /// the old floor could only ever fire on a clear, where it short-circuited the ladder
        /// above. Non-clear behaviour is therefore unchanged by the demotion.
        ///
        /// <paramref name="survivalPct"/> is survivors/deployed at settle time. Deploying NO
        /// troops (a scout run) is 1f - "nothing was lost" - so it is never punished for it.
        /// Pure + static: unit-testable with no scene.
        /// </summary>
        public static int ComputeStars(bool garrisonCleared, bool bossDestroyed,
                                       float destructionPct, float elapsedSeconds, float clockSeconds,
                                       float survivalPct)
        {
            int s = 0;
            if (destructionPct >= 0.5f) s = 1;
            if (bossDestroyed) s = Mathf.Max(s, 1);

            if (garrisonCleared)
            {
                s = Mathf.Max(s, 1);                                              // cleared it
                bool underTime    = elapsedSeconds <= Mathf.Max(1f, clockSeconds);
                bool highSurvival = Mathf.Clamp01(survivalPct) >= HighSurvivalPct;
                if (underTime || highSurvival) s = Mathf.Max(s, 2);               // one axis
                if (underTime && highSurvival) s = 3;                             // both axes
            }
            return Mathf.Clamp(s, 0, 3);
        }

        /// <summary>
        /// Survivors / deployed at this instant, clamped 0..1. Deploying nothing reads as 1f
        /// (nothing lost) so a no-troop scout clear is not punished on the survival axis.
        /// </summary>
        public float SurvivalPct
        {
            get
            {
                int deployed = _deployedCount;
                if (deployed <= 0) return 1f;
                return Mathf.Clamp01(TroopsAlive / (float)deployed);
            }
        }

        /// <summary>
        /// Loot payout (crystals + food) scaled by destruction AND the star tier. At
        /// 100% + 3 stars a raid pays the base + 3x the per-star bonus; a light raid
        /// pays proportionally less. Reused by the victory grant + the HUD ticker.
        /// Static + pure so a regression can assert the scaling with no scene.
        /// </summary>
        public static ResourceCost ComputeLoot(int stars, float destructionPct,
            int crystalsBase, int foodBase, int crystalsPerStar, int foodPerStar)
        {
            float frac = Mathf.Clamp01(destructionPct);
            int st = Mathf.Clamp(stars, 0, 3);
            int crystals = Mathf.RoundToInt(Mathf.Max(0, crystalsBase) * frac) + Mathf.Max(0, crystalsPerStar) * st;
            int food     = Mathf.RoundToInt(Mathf.Max(0, foodBase)     * frac) + Mathf.Max(0, foodPerStar)     * st;
            return new ResourceCost(food: food, crystals: crystals);
        }

        // =====================================================================
        //  Self-install — one scorer per RaidBase_* scene
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            TryInstall(scene.name);
        }

        private static void TryInstall(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!sceneName.StartsWith("RaidBase", StringComparison.OrdinalIgnoreCase)) return;
            if (FindAnyObjectByType<RaidScoring>() != null) return;

            var go = new GameObject("RaidScoring");
            go.AddComponent<RaidScoring>();
            FlowTrace.Step("Raid", $"RaidScoring self-installed in raid scene '{sceneName}'.");
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            StartCoroutine(BindRoutine());
        }

        // The spawner arms its garrison a frame after its own Start; poll a few
        // frames for it (mirrors RaidVictoryController.BindRoutine).
        private IEnumerator BindRoutine()
        {
            for (int i = 0; i < 10 && _spawner == null; i++)
            {
                _spawner = FindAnyObjectByType<RaidGarrisonSpawner>();
                if (_spawner != null) break;
                yield return null;
            }
            if (_spawner == null)
                FlowTrace.Warn("Raid", "RaidScoring: no RaidGarrisonSpawner found — destruction% will read 0 (scoring degrades to clock/deploy only).");
            else
                FlowTrace.Step("Raid", "RaidScoring bound to the garrison spawner (clock + destruction% live).");

            // The OBJECTIVE. Baked into the scene, so it is present from load - but bind the
            // same tolerant way in case a scene predates the spire (legacy raid bases keep the
            // old garrison-wipe rule rather than becoming unwinnable).
            _spire = RaidSpire.Active != null ? RaidSpire.Active : FindAnyObjectByType<RaidSpire>();
            if (_spire == null)
                FlowTrace.Warn("Raid", "RaidScoring: this raid scene has NO RaidSpire objective — " +
                                       "falling back to the legacy garrison-wipe win condition. " +
                                       "Re-bake it with RaidBaseGenerator.BuildAllRaidScenes to get the spire.");
            else
                FlowTrace.Step("Raid", $"RaidScoring bound to the OBJECTIVE: spire '{_spire.name}' " +
                                       $"({_spire.MaxHp:0} HP). Razing it wins the raid.");

            CaptureStructureCensus();

            FlowTrace.Step("Raid", $"destruction% split = {SpireWeight:P0} spire / " +
                                   $"{StructuresWeight:P0} structures / {GarrisonWeight:P0} garrison " +
                                   $"(spire={(_spire != null ? "yes" : "NO")}, " +
                                   $"structures={_structuresTotalAtStart}). " +
                                   "Absent terms are renormalised away, not scored as 0.");
        }

        /// <summary>
        /// Snapshot the structures that stood when the raid began - the denominator for
        /// <see cref="StructuresRazedPct"/>. Run ONCE, after the spawner/spire bind, because
        /// walls and turrets are baked into the scene and present from load.
        /// </summary>
        private void CaptureStructureCensus()
        {
            _startWalls = FindObjectsByType<WallSegment>(FindObjectsSortMode.None);

            // Only the DEFENDER's turrets count. A raid scores what the player came to
            // demolish, and a PlayerOwned tower is not part of that - it is also the exact
            // ownership axis DefenseTower keeps its two IsAlive answers apart on (see
            // DefenseTower.cs:192-238), so scoring must respect it rather than count every
            // tower in the scene.
            var allTowers = FindObjectsByType<DefenseTower>(FindObjectsSortMode.None);
            var enemyTowers = new System.Collections.Generic.List<DefenseTower>(allTowers.Length);
            for (int i = 0; i < allTowers.Length; i++)
            {
                var t = allTowers[i];
                if (t != null && t.Allegiance != TowerAllegiance.PlayerOwned) enemyTowers.Add(t);
            }
            _startTowers = enemyTowers.ToArray();

            _structuresTotalAtStart = _startWalls.Length + _startTowers.Length;

            if (_structuresTotalAtStart == 0)
                FlowTrace.Warn("Raid", "RaidScoring: this raid base has NO walls and NO enemy turrets - " +
                                       "the structures term carries no information and its 30% is " +
                                       "renormalised into spire/garrison. Re-bake with " +
                                       "RaidBaseGenerator.BuildAllRaidScenes if that is unexpected.");
            else
                FlowTrace.Step("Raid", $"structures census at raid start: {_startWalls.Length} wall segment(s) + " +
                                       $"{_startTowers.Length} enemy turret(s) = {_structuresTotalAtStart} " +
                                       "(denominator fixed for the whole raid).");
        }

        private void Update()
        {
            if (_finalized) return;

            _elapsed += Time.deltaTime;

            // Track the peak garrison total (it grows as the staggered spawn lands,
            // then holds stable — the denominator for destruction%).
            if (_spawner != null)
            {
                int t = _spawner.TotalGarrison;
                if (t > _garrisonTotalPeak) _garrisonTotalPeak = t;
            }

            if (!_timeExpiredFired && _elapsed >= _clockSeconds)
            {
                _timeExpiredFired = true;
                FlowTrace.Step("Raid", $"raid clock expired at {_elapsed:0.0}s (destruction {DestructionPct * 100f:0}%). Ending the raid.");
                OnTimeExpired?.Invoke();
            }
        }

        // =====================================================================
        //  Deploy tracking — RaidDeployController pushes each drop here.
        // =====================================================================

        /// <summary>
        /// Record one troop deploy (RaidDeployController calls this on each successful
        /// drop): bumps the deployed count and appends to the re-watch log at the
        /// current clock time. Null-safe.
        /// </summary>
        public void RecordDeploy(string troopId, Vector3 worldPos)
        {
            _deployedCount++;
            _deployLog.Record(troopId ?? "", _elapsed, worldPos);
        }

        // =====================================================================
        //  Finalize — settle the result + compute the loot payout.
        // =====================================================================

        /// <summary>
        /// Settle the raid into a <see cref="RaidResult"/> (idempotent — a second call
        /// returns the first result). On a full clear destruction is 100% and the boss
        /// is down; otherwise destruction is read live off the garrison. Called by the
        /// victory path (cleared:true) and the timeout/retreat path (cleared:false).
        /// </summary>
        public RaidResult Finalize(bool cleared)
        {
            if (_finalized && _result != null) return _result;
            _finalized = true;

            // DESTRUCTION ON SETTLE. With a spire the win is the OBJECTIVE, so a victory is
            // floored at the spire's share (SpireWeight) and rises with whatever else the
            // player razed - a spire rush past a living garrison no longer pays the same as
            // a full dismantle. Without a spire the legacy "cleared => 100%" holds exactly.
            float destruction = _spire != null
                ? (cleared ? Mathf.Max(SpireWeight, DestructionPct) : DestructionPct)
                : (cleared ? 1f : DestructionPct);
            bool bossDown = cleared;   // the objective IS the boss kill (spire) / full clear (legacy)
            // Survival is sampled BEFORE any teardown - the surviving bodies are still on the
            // field at Finalize (nothing on the victory path destroys troops; the scene only
            // unloads at ReturnHome), so this is the real number, not an estimate.
            float survival = SurvivalPct;
            int stars = ComputeStars(cleared, bossDown, destruction, _elapsed, _clockSeconds, survival);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                $"stars settled: {stars} (cleared={cleared} destruction={destruction:P0} " +
                $"elapsed={_elapsed:F0}s/{_clockSeconds:F0}s underTime={_elapsed <= Mathf.Max(1f, _clockSeconds)} " +
                $"survival={survival:P0} high={survival >= HighSurvivalPct} @{HighSurvivalPct:P0}).");

            _result = new RaidResult
            {
                Stars = stars,
                DestructionPct = destruction,
                ElapsedSeconds = _elapsed,
                ClockSeconds = _clockSeconds,
                Cleared = cleared,
            };

            FlowTrace.Step("Raid", $"raid scored: {stars} star(s), {_result.DestructionPercent}% razed, " +
                                   $"{_elapsed:0.0}s, cleared={cleared}, deployed={_deployedCount}.");
            return _result;
        }

        /// <summary>
        /// The loot payout for a settled result, using THIS scorer's tunables.
        /// (Thin instance wrapper over the pure <see cref="ComputeLoot"/>.)
        /// </summary>
        public ResourceCost LootFor(RaidResult result)
        {
            if (result == null) return default(ResourceCost);
            return ComputeLoot(result.Stars, result.DestructionPct,
                _lootCrystalsBase, _lootFoodBase, _lootCrystalsPerStar, _lootFoodPerStar);
        }
    }
}
