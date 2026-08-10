// =============================================================================
// EchoService -- the Echo Workforce V1 faucet (ECHO_WORKFORCE_SPEC).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE FARM PILLAR'S PLAYER-FACING LOOP (owner-resolved 2026-06-26):
//   - Start with 1 Echo; beating 5 waves unlocks the next (cap 6). Unlocks feel
//     EARNED via the defense/wave pillar (the wave hook below).
//   - Each Echo auto-harvests at BaseRatePerHour; rate = echoCount x BaseRatePerHour.
//   - A pooled SILO buffers the haul (shared, V1). Capacity in HOURS (4h -> 6h/8h
//     via upgrades). Fills while ONLINE (per-frame tick) + OFFLINE (the existing
//     OfflineHarvestService clock), CAPPED. Idle waste past the cap is fair, not
//     punishing.
//   - "Dump" (DumpSilos) is the come-back-claim-reset loop: one tap transfers the
//     silo into the spendable wallet (EconomyService.GrantSpendable -> persists +
//     reaches the building-upgrade ledger), resets the silo, advances the clock.
//
// INTEGRATION TO THE REAL CODE (no placeholder APIs):
//   - OFFLINE accrual reuses OfflineHarvestService's persisted clock,
//     GameState.LastHarvestClaimMs (Unix-ms, advanced atomically on every OHS
//     claim). On a deferred Start we integrate echoCount x ratePerSec over
//     (TimeSource.NowUnixMs() - LastHarvestClaimMs), CLAMPED to the silo HOUR cap,
//     into the silo. NO Time.time (that resets per session = wrong for offline).
//     The Echo silo is a SEPARATE faucet from OHS's worker/settlement/pet nodes
//     (which bank to the wallet) -- they share only the CLOCK, never a node, so
//     there is no double-grant: OHS banks node haul to the wallet; Echo only fills
//     its OWN silo, and the silo reaches the wallet ONLY via Dump.
//   - DUMP banks through EconomyService.GrantSpendable (the persisting path -- the
//     Wood/Iron routing fix), so claimed resources stick + reach upgrades.
//   - PERSISTED via GameState (EchoCount / SiloResources / WavesCompleted, schema
//     v25) -- NOT a side-band PlayerPrefs key.
//   - WorkerManager's harvest ROLE is retired for V1 (UseOfflineCatchUp already off;
//     ClickToDispatch disabled by EchoWorkforceBootstrap so the two systems never
//     bank the same nodes). The Echo model is the V1 workforce abstraction.
//
// Self-bootstrapping DDOL (see EchoWorkforceBootstrap) -- no scene authoring,
// mirroring OfflineHarvestService.
// =============================================================================
using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Owns the Echo workforce: echo count (1..6), the pooled silo fill (online +
    /// offline, capped in hours), the Dump-to-wallet transfer, and the wave-driven
    /// Echo unlocks. Persisted via <see cref="GameState"/> (schema v25).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoService : MonoBehaviour
    {
        public static EchoService Instance { get; private set; }

        // -- Tunables (owner-tunable in playtest) ------------------------------
        [Header("Workforce")]
        [Tooltip("Hard cap on owned Echoes. 6 = the full canonical Echo roster (EchoRosterCatalog: " +
                 "the awakened souls Aldwin/Elowen/Corvin/Bran/Doran/Maren the Heart of Elarion guards). " +
                 "The wave hook earns them one per WavesPerEcho clears; the roster/unlock dialogue reads this cap.")]
        [Min(1)] public int MaxEchoes = 6;

        [Tooltip("Waves cleared per Echo unlock (owner model: every 5 = 4 normal + 1 boss).")]
        [Min(1)] public int WavesPerEcho = 5;

        [Header("Harvest")]
        [Tooltip("Base resources/hour PER Echo. Total rate = echoCount x this. Owner-tunable.")]
        [Min(0f)] public float BaseRatePerHour = 120f;

        [Header("Silo")]
        [Tooltip("Silo capacity in HOURS of accrual (base 4h; upgrades 6h/8h). The buffer/" +
                 "engagement cap -- fills online+offline then idles until Dumped.")]
        [Min(0.1f)] public float SiloCapHours = 4f;

        /// <summary>Raised after the silo balance or echo count changes (HUD listens).</summary>
        public event Action Changed;

        /// <summary>Raised when a new Echo is unlocked (count, total) -- the "New Echo joined!" toast.</summary>
        public event Action<int> EchoUnlocked;

        /// <summary>Persisted one-shot key (GameState.SeenTutorials) that gates the FOUNDING-echo
        /// teaching card so it fires exactly once per save. Set ONLY after the card actually
        /// renders (see <see cref="AnnounceFoundingEcho"/>) so an app-quit mid-build never
        /// consumes the teaching. Shared with EchoUnlockFeedback (the caller).</summary>
        public const string FoundingTaughtKey = "echo_founding_taught";

        // Guard so the one-time offline catch-up runs once per session (not on every
        // re-enable). The clock advance is owned by OfflineHarvestService; we only READ.
        private bool _offlineClaimedThisSession;

        // -- Convenience accessors over the persisted state --------------------
        private static GameState State => GameStateService.Instance != null ? GameStateService.Instance.State : null;

        /// <summary>Owned Echo count (>=1). Reads the persisted state; 1 when state is absent.</summary>
        public int EchoCount
        {
            get { var s = State; return s != null ? Mathf.Max(1, s.EchoCount) : 1; }
        }

        /// <summary>Pooled silo balance (resources accrued, pre-Dump). 0 when state is absent.</summary>
        public double Silo
        {
            get { var s = State; return s != null ? s.SiloResources : 0.0; }
        }

        /// <summary>
        /// WO-709 (owner design 2026-07-13, curve RULED quadratic-total — "each new Echo amps
        /// up the entire harvesting operation"): every echo works at xEchoCount speed, so this
        /// is the ONE modifier every harvest tick consumes (echo silo online+offline here;
        /// ResourceBuildingHarvester reads it for collector yield). 1 echo = x1, 2 = x2 each
        /// (x4 total), 4 = x4 each (x16 total vs one echo's base). Tune BaseRatePerHour down
        /// to compensate. Displayed on the workforce HUD as the "xN ALL HARVEST" medallion.
        /// </summary>
        // WO-738: this stays the count-quadratic SPINE (the public value the HUD/UI reads for
        // the "xN ALL HARVEST" medallion). The value ACTUALLY APPLIED to income is
        // EchoBonusCalculator.AggregateHarvestMultiplier(), which folds THIS spine in ONCE and
        // layers per-echo specialization on top -- so RatePerSecond multiplies by the aggregate
        // INSTEAD OF this property (never both). Do not add a second global multiplier here.
        public double GlobalHarvestMultiplier => EchoCount;

        /// <summary>Total resources/sec the workforce produces right now (echoCount x base
        /// x the WO-738 AggregateHarvestMultiplier = the count-quadratic spine folded with
        /// per-echo Harvest-lane specialization, scaled by the STEWARD `harvestRate` talent
        /// sum — WO-676 Provider's Bond; x1 at sum 0). The aggregate REPLACES the bare
        /// GlobalHarvestMultiplier factor (it already contains the spine — no double-apply).</summary>
        public double RatePerSecond => EchoCount * (BaseRatePerHour / 3600.0) * EchoBonusCalculator.AggregateHarvestMultiplier() * (1.0 + HarvestRateBonus());

        // WO-676 §2b: ONE registry read at the existing rate calc (this property feeds the
        // online Update tick AND the offline ClaimOffline integral). StatSum is internally
        // null-safe (no service / no tree / no nodes => 0), so behavior is byte-identical
        // to baseline until a harvestRate node is learned. Silo CAPACITY deliberately stays
        // base-rated (capacity is `collectorCap`'s seam, not this one).
        private static float HarvestRateBonus()
        {
            float bonus = Talents.HeroTalentModifiers.StatSum(HeroTalentClassReader.Slug(), "harvestRate");
            if (bonus <= 0f) return 0f;
            FlowTrace.Once("Talent", "echo-harvestRate",
                $"harvestRate x{1f + bonus:0.###} applied to echo tick (WO-676 Provider's Bond).");
            return bonus;
        }

        /// <summary>The silo's absolute capacity in resources = capHours x ratePerHour x echoCount
        /// x AggregateHarvestMultiplier (WO-830 cadence reconcile -- WO-830 Sec.2 caveat 1). Rate
        /// folds the FULL specialization aggregate while capacity only carried the count spine, so
        /// with bonuses active the silo filled well before the intended SiloCapHours. Scaling
        /// capacity by the SAME multiplier basis as rate keeps fill-time ~= SiloCapHours as the
        /// roster/specialization grows. The STEWARD talent factor is deliberately excluded
        /// (capacity is `collectorCap`'s seam, not `harvestRate`'s -- WO-676 note above).</summary>
        public double SiloCapacity => SiloCapHours * BaseRatePerHour * EchoCount * EchoBonusCalculator.AggregateHarvestMultiplier();

        /// <summary>Silo fill fraction 0..1 (silo / capacity). 0 when capacity is 0.</summary>
        public float FillFraction
        {
            get { double cap = SiloCapacity; return cap > 0.0 ? Mathf.Clamp01((float)(Silo / cap)) : 0f; }
        }

        /// <summary>Waves cleared so far (the Echo-unlock counter). 0 when state is absent.</summary>
        public int WavesCompleted
        {
            get { var s = State; return s != null ? s.WavesCompleted : 0; }
        }

        /// <summary>
        /// Waves remaining until the NEXT Echo unlock (roster/pet-box readout). Computed from
        /// the REAL cadence: WavesPerEcho - (WavesCompleted % WavesPerEcho). Returns 0 once every
        /// roster spirit is owned (EchoCount &gt;= MaxEchoes) so the UI can show "roster complete".
        /// </summary>
        public int WavesUntilNextEcho
        {
            get
            {
                if (EchoCount >= MaxEchoes) return 0;
                int per = Mathf.Max(1, WavesPerEcho);
                int into = WavesCompleted % per;
                return per - into;   // 1..per (never 0 while more spirits remain)
            }
        }

        /// <summary>Progress 0..1 toward the next Echo unlock (for a pet-box progress bar).
        /// 1 when the roster is already full.</summary>
        public float NextEchoProgress
        {
            get
            {
                if (EchoCount >= MaxEchoes) return 1f;
                int per = Mathf.Max(1, WavesPerEcho);
                return Mathf.Clamp01((WavesCompleted % per) / (float)per);
            }
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Destroy(this) -- NOT Destroy(gameObject): may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // WO-738: keep the Core EchoLaneBonuses contract current whenever a lane assignment
            // changes (the picker writes through EchoAssignments). Count changes recompute in
            // OnWaveCleared/GrantEcho; the first compute happens once in Start.
            EchoAssignments.Changed += OnAssignmentsChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                EchoAssignments.Changed -= OnAssignmentsChanged;
                Instance = null;
            }
        }

        /// <summary>Assignment-change handler (WO-738): recompute the passive lane bonuses off the
        /// event, NOT per frame, then notify the HUD so the harvest medallion reflects the new mix.</summary>
        private void OnAssignmentsChanged()
        {
            EchoBonusCalculator.Recompute();
            Changed?.Invoke();
        }

        private void Start()
        {
            // WO-738: populate EchoLaneBonuses once at boot from the persisted assignment so hosts
            // (and the harvest faucet) see current multipliers before any change event fires.
            EchoBonusCalculator.Recompute();

            // Deferred one frame so GameStateService (loads the save in its Awake) and
            // OfflineHarvestService are up before we read LastHarvestClaimMs.
            StartCoroutine(OfflineCatchUpNextFrame());
        }

        private System.Collections.IEnumerator OfflineCatchUpNextFrame()
        {
            yield return null;
            ClaimOffline();
        }

        // =====================================================================
        //  Offline fill -- reuse OfflineHarvestService's persisted clock
        // =====================================================================

        /// <summary>
        /// One-time-per-session offline accrual: integrate echoCount x ratePerSec over
        /// (now - GameState.LastHarvestClaimMs), CLAMPED to the silo HOUR cap, and add it
        /// to the silo. Reuses the SAME persisted Unix-ms clock OfflineHarvestService owns
        /// (it advances the clock atomically on its own claim) -- we only READ the delta, so
        /// there is no double-grant: OHS banks NODE haul to the wallet; the Echo silo is a
        /// separate faucet that reaches the wallet only via Dump. Fresh save (clock 0) ->
        /// nothing accrues (OHS seeds the clock to now on its first claim).
        /// </summary>
        public void ClaimOffline()
        {
            if (_offlineClaimedThisSession) return;
            var s = State;
            if (s == null) return;
            _offlineClaimedThisSession = true;

            double nowMs = TimeSource.NowUnixMs();
            double lastMs = s.LastHarvestClaimMs;
            if (lastMs <= 0)
            {
                // Fresh save: OHS seeds the clock to now this launch; nothing to back-fill.
                FlowTrace.Step("Echo", "ClaimOffline: fresh clock (LastHarvestClaimMs<=0) -- no offline fill this launch.");
                return;
            }

            double elapsedSec = Math.Max(0.0, (nowMs - lastMs) / 1000.0);   // monotonic guard (clock-back -> 0)
            double capSec = Math.Max(0.0, SiloCapHours) * 3600.0;
            double cappedSec = Math.Min(elapsedSec, capSec);

            double gained = RatePerSecond * cappedSec;
            if (gained <= 0.0)
            {
                FlowTrace.Step("Echo", $"ClaimOffline: away {elapsedSec:F0}s, gained 0 (rate {RatePerSecond:F3}/s, echoes {EchoCount}).");
                return;
            }

            AddToSilo(gained);
            FlowTrace.Step("Echo",
                $"ClaimOffline: +{gained:F0} to silo over {cappedSec:F0}s away" +
                (elapsedSec > capSec ? " (capped)" : "") +
                $" -> silo {Silo:F0}/{SiloCapacity:F0} (echoes {EchoCount}).");
        }

        // =====================================================================
        //  Online fill -- per-frame accrual up to the cap
        // =====================================================================

        private void Update()
        {
            // Online ticking does NOT advance LastHarvestClaimMs (OfflineHarvestService
            // owns that clock) -- it only tops the silo up in real-time while playing,
            // clamped to the HOUR cap. The offline path covers any away-gap.
            double add = RatePerSecond * Time.deltaTime;
            if (add > 0.0) AddToSilo(add);
        }

        /// <summary>Add <paramref name="amount"/> to the silo, clamped to the HOUR cap, and persist+notify.</summary>
        private void AddToSilo(double amount)
        {
            var s = State;
            if (s == null || amount <= 0.0) return;
            double cap = SiloCapacity;
            double before = s.SiloResources;
            double next = Math.Min(cap, before + amount);
            if (next <= before) return;            // already at cap -> idle waste (fair), no churn
            s.SiloResources = next;
            // Online ticks persist coarsely (the dump + offline path are the durable claims);
            // we avoid a PlayerPrefs write every frame -- the silo is re-derivable offline and
            // is saved on Dump / unlock / quit. Notify the HUD every tick for a live fill bar.
            Changed?.Invoke();
        }

        // =====================================================================
        //  Dump -- the come-back-claim-reset hook
        // =====================================================================

        /// <summary>
        /// Transfer the pooled silo into the spendable wallet (split across resource
        /// types), reset the silo, advance the clock + Save. Banks through
        /// <see cref="EconomyService.GrantSpendable"/> so Wood/Iron persist into
        /// GameState + reach the building-upgrade ledger (NOT plain Grant). Returns the
        /// integer total banked (0 when the silo was empty).
        /// </summary>
        public int DumpSilos()
        {
            using var _t = FlowTrace.Enter("Echo", "DumpSilos");
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null) { FlowTrace.Warn("Echo", "DumpSilos: no GameState -- no-op."); return 0; }

            int pool = (int)Math.Floor(s.SiloResources);
            if (pool <= 0)
            {
                FlowTrace.Step("Echo", "DumpSilos: silo empty -- nothing to bank.");
                return 0;
            }

            // WO-830 split: the pooled silo divides across the FIVE harvest targets
            // (Wood / Iron / Food / Gold / Crystals) by the Harvest-assigned echoes'
            // player-picked assignment WEIGHTS (EchoBonusCalculator.HarvestTargetWeights --
            // rate x level per echo, routed to its ASSIGNED resource). Gold credits Coins
            // (EconomyService.AddCoins); Crystals credits the Aether wallet via the
            // crystals param of GrantSpendable (NEVER the old 3-param form -- WO-830 Sec.7);
            // both only flow when an echo is explicitly assigned there. Uses LARGEST-
            // REMAINDER apportionment so the integer split sums to the EXACT pool (no unit
            // created or lost); leftover units go to the largest fractional shares.
            var weights = EchoBonusCalculator.HarvestTargetWeights();
            double wW = weights.TryGetValue(HarvestTarget.Wood, out var vw) ? vw : 0.0;
            double wI = weights.TryGetValue(HarvestTarget.Iron, out var vi) ? vi : 0.0;
            double wF = weights.TryGetValue(HarvestTarget.Food, out var vf) ? vf : 0.0;
            double wG = weights.TryGetValue(HarvestTarget.Gold, out var vg) ? vg : 0.0;
            double wC = weights.TryGetValue(HarvestTarget.Crystals, out var vc) ? vc : 0.0;
            double totalW = wW + wI + wF + wG + wC;
            if (totalW <= 0.0) { wW = 1.0; wI = 1.0; wF = 1.0; wG = 0.0; wC = 0.0; totalW = 3.0; }   // defensive classic split

            const int n = 5;
            double[] exact =
            {
                pool * (wW / totalW), pool * (wI / totalW), pool * (wF / totalW),
                pool * (wG / totalW), pool * (wC / totalW),
            };
            int[] alloc = new int[n];
            double[] fracs = new double[n];
            int used = 0;
            for (int k = 0; k < n; k++)
            {
                alloc[k] = (int)Math.Floor(exact[k]);
                fracs[k] = exact[k] - alloc[k];
                used += alloc[k];
            }
            int remainder = pool - used;   // in {0..n-1}: sum of fractional parts < n
            for (int r = 0; r < remainder; r++)
            {
                int best = -1; double bestFrac = -1.0;
                for (int k = 0; k < n; k++) { if (fracs[k] > bestFrac) { bestFrac = fracs[k]; best = k; } }
                if (best < 0) break;
                alloc[best] += 1;
                fracs[best] = -1.0;   // consume so each leftover unit lands on a distinct top share
            }
            int wood = alloc[0];
            int iron = alloc[1];
            int food = alloc[2];
            int gold = alloc[3];
            int crystals = alloc[4];
            FlowTrace.Step("Echo",
                $"DumpSilos split (pool {pool}) by harvest weights [W {wW:0.##}/I {wI:0.##}/F {wF:0.##}/G {wG:0.##}/C {wC:0.##}] -> " +
                $"wood {wood}, iron {iron}, food {food}, gold {gold}, crystals {crystals} (sum {wood + iron + food + gold + crystals}).");

            var eco = EconomyService.Instance;
            if (eco != null)
            {
                // GrantSpendable persists Wood/Iron into GameState (upgrade ledger), fills
                // the in-session pool, routes Food + Crystals through GameState (the crystals
                // param -- the single wallet after WO-842). AddCoins is the GOLD mover
                // (GameState.Resources.Coins). The single banking path -- no double-grant
                // (the silo is the ONLY source here).
                eco.GrantSpendable(wood: wood, food: food, iron: iron, crystals: crystals);
                if (gold > 0) eco.AddCoins(gold);
            }
            else
            {
                // Defensive fallback (EconomyService is always installed in practice): keep
                // the classic direct Wood/Iron write; route Crystals through the public
                // GameStateService mover; a Gold share cannot be banked without the economy
                // service -- log it LOUDLY (never silent) rather than poke the wallet struct.
                FlowTrace.Warn("Echo", "DumpSilos: EconomyService absent -- writing Wood/Iron directly to GameState as fallback"
                    + (gold > 0 ? $"; {gold} gold NOT banked (no coin mover without EconomyService)" : "") + ".");
                s.Wood = Mathf.Max(0, s.Wood + wood);
                s.Iron = Mathf.Max(0, s.Iron + iron);
                if (crystals > 0 && gs != null) gs.AddCrystals(crystals);
            }

            // Reset the silo (keep the sub-1 fractional remainder so slow rates aren't lost).
            s.SiloResources -= pool;
            if (s.SiloResources < 0) s.SiloResources = 0;

            // Advance the silo clock to now so the next offline window starts fresh (reusing
            // the OfflineHarvestService clock = the come-back-RESET) and persist atomically.
            s.LastHarvestClaimMs = TimeSource.NowUnixMs();
            if (gs != null) gs.Save();

            FlowTrace.Step("Echo", $"DumpSilos: banked +{wood} wood, +{iron} iron, +{food} food, +{gold} gold, +{crystals} crystals (pool {pool}); silo reset, clock advanced.");

            // WO-953: the felt moment — "+N <resource>" pops for every banked share,
            // through the ONE pooled damage-number spawner (owner ruling: "we can use
            // the same item that spawns the damage points"). Word carries the meaning;
            // the tint is the shared income palette (redundant channel, colorblind law).
            SpawnDumpPops(wood, iron, food, gold, crystals);

            Changed?.Invoke();
            return pool;
        }

        /// <summary>
        /// WO-953 pop hook for the silo dump: one "+N &lt;resource&gt;" pooled pop per
        /// nonzero banked share, stacked above the hero (the tap that banked them).
        /// Purely presentational — banking already happened and is FlowTrace'd above;
        /// a missing hero/camera skips the visual, never the grant.
        /// </summary>
        private static void SpawnDumpPops(int wood, int iron, int food, int gold, int crystals)
        {
            var hero = GameObject.FindWithTag("Player");   // canon hero tag (WO-450)
            if (hero == null)
            {
                FlowTrace.Once("Echo", "dump-pop-nohero",
                    "DumpSilos: no 'Player'-tagged hero in scene -- +N pops skipped (the grant itself is logged above).");
                return;
            }

            Vector3 basePos = hero.transform.position + Vector3.up * 2.0f;
            int slot = 0;
            void Pop(int amount, string label, Color tint)
            {
                if (amount <= 0) return;
                // Stack each resource's pop a step higher so a 5-way dump reads as a
                // column, not an overdraw pile (per-resource labels never merge with
                // each other -- the merge in the pool is per-resource by design).
                DamageNumberSpawner.SpawnResourceGain(amount, label,
                    basePos + Vector3.up * (0.55f * slot), tint);
                slot++;
            }

            // The shared income palette (MineNode.ResourceTint / ResourceCollector.PopupTint
            // values) so wood reads as wood whichever faucet paid it. Gold has no prior
            // world-pop precedent; warm coin gold, named in TEXT like every other.
            Pop(wood,     "Wood",     new Color(0.55f, 0.38f, 0.22f));
            Pop(iron,     "Iron",     new Color(0.62f, 0.64f, 0.70f));
            Pop(food,     "Food",     new Color(0.72f, 0.62f, 0.28f));
            Pop(gold,     "Gold",     new Color(1.00f, 0.85f, 0.35f));
            Pop(crystals, "Crystals", new Color(0.35f, 0.72f, 0.95f));
        }

        // =====================================================================
        //  Wave unlock -- mirror WaveXpBridge's OnWaveCleared hook
        // =====================================================================

        /// <summary>
        /// Record a wave clear (the Echo-unlock counter) and, on every multiple of
        /// <see cref="WavesPerEcho"/>, unlock the next Echo (up to <see cref="MaxEchoes"/>).
        /// Called by EchoWaveUnlockBridge listening to WaveManager.OnWaveCleared.
        /// </summary>
        public void OnWaveCleared(int waveNumber)
        {
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null) return;

            s.WavesCompleted += 1;
            bool unlockTick = (s.WavesCompleted % Mathf.Max(1, WavesPerEcho)) == 0;
            FlowTrace.Step("Echo",
                $"OnWaveCleared(wave {waveNumber}): wavesCompleted={s.WavesCompleted}, " +
                $"echoes={s.EchoCount}/{MaxEchoes}, unlockTick={unlockTick}.");

            if (unlockTick && s.EchoCount < MaxEchoes)
            {
                s.EchoCount += 1;
                if (gs != null) gs.Save();
                EchoBonusCalculator.Recompute();   // WO-738: count changed -> refresh passive lane bonuses.
                FlowTrace.Step("Echo", $"New Echo joined! count now {s.EchoCount}/{MaxEchoes} (at {s.WavesCompleted} waves).");
                EchoUnlocked?.Invoke(s.EchoCount);
                Changed?.Invoke();
            }
            else
            {
                if (gs != null) gs.Save();   // persist the wave-count progress either way
            }
        }

        /// <summary>
        /// Grant ONE new Echo (the "unlock the next Echo" path), up to <see cref="MaxEchoes"/>.
        /// Mirrors the wave-unlock increment but is driven by an external coordinator
        /// (PopulationService milestones, WO-587) instead of the wave counter. No-op at the
        /// cap or before GameState exists. Persists + fires EchoUnlocked + Changed on success.
        /// </summary>
        public void GrantEcho(string reason)
        {
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null) { FlowTrace.Warn("Echo", $"GrantEcho('{reason}') before GameState -- ignored."); return; }

            if (s.EchoCount >= MaxEchoes)
            {
                FlowTrace.Step("Echo", $"GrantEcho('{reason}'): already at cap {MaxEchoes} -- no-op.");
                return;
            }

            s.EchoCount += 1;
            if (gs != null) gs.Save();
            EchoBonusCalculator.Recompute();   // WO-738: count changed -> refresh passive lane bonuses.
            FlowTrace.Step("Echo", $"GrantEcho('{reason}'): New Echo joined! count now {s.EchoCount}/{MaxEchoes}.");
            EchoUnlocked?.Invoke(s.EchoCount);
            Changed?.Invoke();
        }

        // =====================================================================
        //  Founding-echo teaching -- fire the SAME portrait card for echo #1
        // =====================================================================

        /// <summary>
        /// Announce the FOUNDING echo (the starter, granted via the pet path -- EchoCount==1),
        /// firing the SAME "spirit awakens and speaks" portrait card that echoes #2-6 get. The
        /// wave path only raises <see cref="EchoUnlocked"/> at count&gt;=2, so the founding spirit
        /// never got the card; this closes that gap, DECOUPLED from the fragile FTUE tutorial line.
        ///
        /// Fires EXACTLY ONCE per save: guarded by the persisted <see cref="FoundingTaughtKey"/>
        /// flag (read here as the gate). Raising EchoUnlocked(1) synchronously renders the card via
        /// EchoUnlockFeedback.OnEchoUnlocked -> EchoUnlockDialogue.Show(ByCount(1)=Aldwin, the Ice Echo). We
        /// persist the flag ONLY AFTER the card is confirmed on screen
        /// (<see cref="EchoUnlockDialogue.IsShowing"/>) -- if the render failed we leave the flag
        /// unset so a later attempt (e.g. after the builder closes) still teaches it.
        ///
        /// Raising EchoUnlocked(1) is side-effect-safe: the tutorial signal adapter only reacts at
        /// count&gt;=2 (echo.born:2); every other subscriber merely re-snapshots UI for count 1
        /// (already the state). Does NOT change EchoCount -- it only announces the existing founding echo.
        /// </summary>
        public void AnnounceFoundingEcho()
        {
            var gs = GameStateService.Instance;
            var s = gs != null ? gs.State : null;
            if (s == null) { FlowTrace.Warn("Echo", "AnnounceFoundingEcho before GameState -- ignored."); return; }

            // Gate: already taught this save -> no-op (idempotent).
            if (s.SeenTutorials.TryGetValue(FoundingTaughtKey, out bool taught) && taught)
            {
                FlowTrace.Step("Echo", "AnnounceFoundingEcho: already taught (flag set) -- no-op.");
                return;
            }

            // The founding echo must actually exist (EchoCount is always >=1 via the property,
            // but keep the guard explicit -- this is the "founding echo exists" contract).
            if (EchoCount < 1)
            {
                FlowTrace.Step("Echo", "AnnounceFoundingEcho: no founding echo yet (EchoCount<1) -- skipped.");
                return;
            }

            FlowTrace.Step("Echo", "AnnounceFoundingEcho: firing founding-echo teaching card (EchoUnlocked(1)).");
            EchoUnlocked?.Invoke(1);   // synchronous -> renders the same portrait card as echoes #2-6

            // Persist the teaching ONLY IF the card is confirmed on screen -- so an app-quit
            // mid-build (where we never reached here) or a failed render never burns the one-shot.
            if (EchoUnlockDialogue.IsShowing)
            {
                gs.MarkTutorialSeen(FoundingTaughtKey);
                FlowTrace.Step("Echo", "AnnounceFoundingEcho: card rendered -> founding teaching marked seen (persisted).");
            }
            else
            {
                FlowTrace.Warn("Echo", "AnnounceFoundingEcho: card did NOT render -- flag left unset so a later attempt can teach it.");
            }
        }

        // =====================================================================
        //  Silo upgrades (V1 hook -- 4h -> 6h -> 8h)
        // =====================================================================

        /// <summary>Set the silo capacity in hours (owner model: 4 -> 6 -> 8 via upgrades). Notifies the HUD.</summary>
        public void SetSiloCapHours(float hours)
        {
            SiloCapHours = Mathf.Max(0.1f, hours);
            // Re-clamp the silo to the new cap (a downgrade trims; an upgrade just lifts the ceiling).
            var s = State;
            if (s != null && s.SiloResources > SiloCapacity) s.SiloResources = SiloCapacity;
            FlowTrace.Step("Echo", $"SetSiloCapHours: {SiloCapHours}h -> capacity {SiloCapacity:F0}.");
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// WO-676 STEWARD-reader helper: resolves the active hero's class slug for
    /// <see cref="Talents.HeroTalentModifiers.StatSum"/> lookups from systems that do NOT
    /// sit on the hero rig (economy/defense choke points have no HeroAbilities component
    /// to ask). Mirrors the HeroAbilities.Awake GameState backstop mapping exactly
    /// (HeroClassOpt -> slug; Cleric reuses the Mage loadout, WO-226). Returns "knight"
    /// when unchosen/absent — the V1 solo-Knight north star, and the Shared Universal
    /// pool applies to any class string, so this is always a safe identity default.
    /// Stateless + null-safe: no GameState => "knight" => StatSum still returns 0
    /// without a WisdomCurrencyService, so consumers stay at baseline.
    /// </summary>
    internal static class HeroTalentClassReader
    {
        public static string Slug()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return "knight";
            var opt = svc.State.HeroClass.ToNullable();
            if (!opt.HasValue) return "knight";
            switch (opt.Value)
            {
                case DeNelle.Core.State.HeroClass.Ranger: return "ranger";
                case DeNelle.Core.State.HeroClass.Mage:   return "mage";
                case DeNelle.Core.State.HeroClass.Cleric: return "mage";   // caster reuses the Mage loadout (WO-226)
                default:                                  return "knight";
            }
        }
    }
}
