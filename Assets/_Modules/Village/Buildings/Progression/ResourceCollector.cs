// =============================================================================
// ResourceCollector — CoC-style typed town collector (WO-663 / WO-664).
// Accrues into Pending; Collect() banks to wallet; siege raids steal uncollected.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;
using DeNelle.Village;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// One resource-type collector (Farm / Lumbermill / Forge). Pending fills locally;
    /// <see cref="Collect"/> pipes value home through <see cref="EconomyService.GrantSpendable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceCollector : MonoBehaviour, IDamageableStructure, ISiegeLootTarget, IHarvestSource
    {
        private const string PendingPrefsPrefix = "dotr.collector.pending.";
        private const string HpPrefsPrefix = "dotr.collector.hp.";

        /// <summary>
        /// WO-859 - per-collector LAST-ACCRUAL stamp (unix ms), the whole basis of away/offline
        /// accrual. Deliberately a PlayerPrefs key beside the two this collector already owns:
        /// pending and HP persist this way, so there is NO GameState field, NO SaveSchema change
        /// and NO version bump. Stored as a STRING, not a float: unix-ms is ~1.7e12 and a float
        /// carries only ~7 significant digits, which would quantise the stamp to ~100-second
        /// buckets and make every catch-up wrong by up to two minutes.
        /// </summary>
        private const string LastAccrualPrefsPrefix = "dotr.collector.lastaccrual.";

        /// <summary>
        /// WO-859 overflow guard (NOT the design cap - capacity is the cap). Purely so a
        /// tampered/rolled-forward system clock cannot overflow the int handed to
        /// <see cref="Accrue"/>. Anything past this is clamped; the pool clamps again anyway.
        /// </summary>
        private const double MaxAwaySeconds = 30.0 * 24.0 * 3600.0;

        private const float DefaultMaxHp = 120f;

        [SerializeField] private string _buildingId = ResourceBuildingProgression.FarmId;
        [SerializeField] private float _maxHp = DefaultMaxHp;

        private float _hp;
        private double _pending;
        private bool _broken;

        // WO-859 - unix ms of the last time production was ACCOUNTED FOR on this collector.
        // 0 = never stamped (a fresh collector: seed to now, back-fill nothing).
        private double _lastAccrualMs;

        // True once Start() has run. Configure() lands BETWEEN OnEnable and Start (AddComponent
        // fires Awake+OnEnable synchronously, the factory configures on the next line), so the
        // away catch-up runs in Start where the building id is finally correct - see CatchUpAway.
        private bool _started;
        // AddComponent invokes OnEnable before Configure. Preserve an existing owner of the
        // serialized-default key so a lumbermill/forge collector cannot temporarily replace the
        // farm fallback and then orphan it while re-keying.
        private ResourceCollector _displacedOnEnable;
        private bool _suppressNextDisableSave;

        public string BuildingId => _buildingId;
        public string SourceId => _buildingId;
        public HarvestResource Resource => ResolveResource();

        /// <summary>
        /// A standing, unbroken collector is PRODUCING. The retired
        /// <c>GetLevel(_buildingId) &gt; 1</c> clause was removed 2026-08-04: it predates
        /// (this file has carried it since creation) the owner's 2026-07-13 evening ruling
        /// already recorded in <see cref="ResourceBuildingHarvester"/> - "LEVEL 1 PRODUCES:
        /// CoC-style, a placed collector earns from the moment it stands". Levels start at 1
        /// (<see cref="ResourceBuildingState.GetLevel"/> defaults to 1), so the clause made
        /// EVERY freshly placed collector accrue ZERO until its first paid upgrade, while an
        /// UNPLACED building silently paid the wallet through the harvester's direct-grant
        /// fallback - placing a collector strictly REDUCED income. With that phantom fallback
        /// removed, this is now the only accrual gate, so the retired rule would have zeroed
        /// all collector income and dead-locked the zero-seed founding bootstrap.
        /// </summary>
        public bool IsActive => IsAlive && !_broken;
        public bool CanAccrue => IsActive;
        public double PendingAmount => _pending;
        public bool IsBroken => _broken;

        /// <summary>Health 0..1 — the accrual scale (F8-45: damage reduces economy) and
        /// the wave damage-report fraction. Derived from this collector's own HP fields
        /// (data-driven — no per-type hardcode).</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>
        /// RETIRED, AND PINNED AT ZERO -- COLLECTOR LOOTING IS REMOVED (owner ruling 2026-08-27).
        ///
        /// <para>The property survives because <c>WaveDamageReport</c> reads it to build the row's
        /// "looted N" line; it is now always 0, so that line never appears. It is NOT deleted
        /// outright because a report row that silently loses a field is harder to reason about
        /// than one whose field is provably zero.</para>
        ///
        /// <para>THE RULING: "BANK THEFT REPLACES COLLECTOR LOOTING. A siege bills ONCE per
        /// attack." A siege now takes a bounded percentage of the UNPROTECTED BANK
        /// (<c>DeNelle.Core.Defense.StakeRules</c>) and nothing at all from a collector's pending.
        /// If a collector ever starts taking again while bank theft is live, the player is charged
        /// TWICE for one siege -- which is precisely the defect the superseded WO-1139 ruling
        /// existed to prevent, and the reason its oracle was re-pointed rather than deleted.</para>
        /// </summary>
        public float LastLootStolen { get; private set; }

        /// <summary>
        /// House-clock stamp (unix ms) of the last break; 0 = this collector has not broken this
        /// session. Session-scoped, not persisted, cleared by <see cref="Repair"/>.
        ///
        /// <para>It was the scope key that stopped a still-broken shell being re-reported by every
        /// later siege. With collector looting removed there is no loot to re-report, but the stamp
        /// is kept: it is the only record that a given collector broke during a given siege, and
        /// deleting it would discard evidence rather than a mechanic (CLAUDE.md section 12).</para>
        /// </summary>
        public double LastLootStolenAtUnixMs { get; private set; }

        public double Capacity => ComputeCapacity();

        // ── WO-665a: diegetic collector-fill stack seams (model-only; the view renders) ──
        /// <summary>Number of discrete fill steps a full collector shows (each = 5% of capacity).</summary>
        public const int StepCount = 20;

        /// <summary>How many of the <see cref="StepCount"/> stack items should be shown (0..StepCount).</summary>
        public int FilledSteps => Mathf.Clamp(Mathf.FloorToInt(FillFraction * StepCount), 0, StepCount);

        /// <summary>True when pending is at (or effectively at) capacity — the FULL tell fires.</summary>
        public bool IsFull => FillFraction >= 0.999f;

        /// <summary>
        /// Raised ONLY when <see cref="FilledSteps"/> actually changes (event-driven off the
        /// accrue/collect/siege ticks — never per-frame). A separate view subscribes and re-poses
        /// its pooled props; the model never builds UI (presentation-separate law).
        /// </summary>
        public event System.Action<ResourceCollector> StepChanged;

        // Fire StepChanged if the discrete step count moved since the caller captured it.
        private void RaiseStepChangedIfMoved(int oldSteps)
        {
            int now = FilledSteps;
            if (now != oldSteps) StepChanged?.Invoke(this);
        }

        // IDamageableStructure
        public bool IsAlive => _hp > 0f && !_broken;

        // ISiegeLootTarget
        public Transform LootTransform => transform;
        public bool IsLootTargetAlive => IsAlive;
        public float PendingLoot => (float)_pending;
        public float FillFraction
        {
            get
            {
                double cap = Capacity;
                return cap > 0.0 ? Mathf.Clamp01((float)(_pending / cap)) : 0f;
            }
        }
        public float SiegeRoleValue => 0.85f * (1f + FillFraction * 0.75f);

        private void Awake()
        {
            LoadState();
            if (_hp <= 0f) _hp = _maxHp;
        }

        private void OnEnable()
        {
            var prior = ResourceCollectorRegistry.Get(_buildingId);
            _displacedOnEnable = prior != null && prior != this ? prior : null;
            ResourceCollectorRegistry.Register(this);
            HarvestSourceRegistry.Register(this);

            // WO-VFX-POI — opt in to the near-field harvest CALLOUT aura (colorblind-safe:
            // motion/shape/luminance, not hue). Spent while the collector is not producing.
            PoiBeacon.Attach(gameObject, PoiBeacon.PoiTier.Node,
                calloutRadius: 28f, handoffRadius: 3.5f,
                tint: new Color(1f, 0.94f, 0.72f, 1f),
                isSpent: () => !IsActive);
        }

        /// <summary>
        /// WO-859 - the away/offline catch-up seam. Deliberately <c>Start</c>, not <c>OnEnable</c>:
        /// <c>AddComponent</c> fires Awake+OnEnable synchronously, and both call sites call
        /// <see cref="Configure"/> on the NEXT LINE, so an OnEnable catch-up would integrate the
        /// wrong building's away window (the serialized default "farm") into a collector that is
        /// about to become the lumbermill - the same ordering trap that produced the measured
        /// register-as-farm defect documented on <see cref="Configure"/>. Start runs on the first
        /// frame AFTER Configure, so the id is settled. A scene-unload/reload, a hub->dungeon->hub
        /// round trip and an app relaunch all DESTROY and re-create the component, so Start is the
        /// once-per-lifetime hook every away case passes through.
        /// </summary>
        private void Start()
        {
            _started = true;
            CatchUpAway();
        }

        private void OnDisable()
        {
            // A parked DDOL fallback is not the registry owner once a real placed collector has
            // taken over. It must not overwrite that owner's newer pending/HP/accrual PlayerPrefs.
            bool ownedRegistrySlot = ResourceCollectorRegistry.Get(_buildingId) == this;
            HarvestSourceRegistry.Unregister(this);
            ResourceCollectorRegistry.Unregister(this);
            if (ownedRegistrySlot && !_suppressNextDisableSave) SaveState();
            _suppressNextDisableSave = false;
        }

        /// <summary>Park a DDOL fallback during ownership handoff or state replacement without
        /// allowing its stale snapshot to overwrite the real/new state's PlayerPrefs.</summary>
        internal void ParkWithoutPersisting()
        {
            _suppressNextDisableSave = true;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// WO-859 - pay this collector for the wall-clock it was not being ticked (app closed,
        /// dungeon, raid: sec.3 rules them ONE case, and keying off the collector's own stamp is what
        /// collapses them, with no dependence on service ordering or on which scene the harvester
        /// happens to live in). Pays through the EXISTING <see cref="Accrue"/>, so the capacity
        /// clamp, the <see cref="CanAccrue"/> gate and the manual Collect tap all still apply -
        /// no new route to the wallet. Never touches GameState.LastHarvestClaimMs (WO-859 sec.2 R2:
        /// that clock already has an ordering race between OfflineHarvestService and EchoService,
        /// and this must not become a third consumer).
        /// </summary>
        private void CatchUpAway()
        {
            double nowMs = TimeSource.NowUnixMs();

            // Fresh collector: seed the clock to now and back-fill NOTHING this launch
            // (mirrors OfflineHarvestService's fresh-save arm, :141-148).
            if (_lastAccrualMs <= 0.0)
            {
                _lastAccrualMs = nowMs;
                SaveState();
                FlowTrace.Step("Harvest",
                    $"away catch-up '{_buildingId}': fresh stamp (<=0) - seeded to now, nothing back-filled.");
                return;
            }

            // Anti-tamper monotonic guard: a clock set BACKWARDS yields 0, never a negative and
            // never a re-claim (mirrors OfflineHarvestService.cs:152-154).
            double awaySec = (nowMs - _lastAccrualMs) / 1000.0;
            if (awaySec < 0.0)
            {
                FlowTrace.Warn("Harvest",
                    $"away catch-up '{_buildingId}': clock ran BACKWARDS (now={nowMs:0} < stamp={_lastAccrualMs:0}) - " +
                    "clamped to 0, no accrual, no re-claim.");
                awaySec = 0.0;
            }
            if (awaySec > MaxAwaySeconds)
            {
                FlowTrace.Warn("Harvest",
                    $"away catch-up '{_buildingId}': away {awaySec:0}s exceeds the {MaxAwaySeconds:0}s int-overflow " +
                    "guard - clamped (this is NOT the design cap; capacity is).");
                awaySec = MaxAwaySeconds;
            }

            int amount = AwayAmount(_buildingId, awaySec);
            double before = _pending;

            // ALWAYS stamp, even when the window earns nothing - Accrue's unconditional stamp
            // write covers the paying case; this covers amount<=0 and the CanAccrue-false case
            // (a broken collector must not bank a frozen backlog if it is ever revived).
            _lastAccrualMs = nowMs;

            if (amount > 0) Accrue(amount);
            else SaveState();

            FlowTrace.Step("Harvest",
                $"away catch-up '{_buildingId}': away={awaySec:0}s owed={amount} " +
                $"pending {before:F0} -> {_pending:F0} / cap {Capacity:F0}" +
                (_pending >= Capacity - 0.001 ? " (AT CAP - the cap is what bounds the away window)" : ""));
        }

        /// <summary>
        /// Units owed for <paramref name="awaySec"/> of unattended production. The rate comes from
        /// the SHARED <see cref="ResourceBuildingHarvester.EffectiveYieldPerTick"/> - the very
        /// function the online tick uses - so the offline path can never drift from the online one
        /// by re-implementing the multiplier stack. Clamped to int range before the cast.
        /// </summary>
        private static int AwayAmount(string buildingId, double awaySec)
        {
            if (awaySec <= 0.0) return 0;
            float interval = ResourceBuildingState.CurrentHarvestInterval(buildingId);
            if (interval <= 0f) return 0;
            int perTick = ResourceBuildingHarvester.EffectiveYieldPerTick(buildingId);
            if (perTick <= 0) return 0;
            double owed = perTick * (awaySec / interval);
            if (owed <= 0.0) return 0;
            return owed >= int.MaxValue ? int.MaxValue : (int)owed;
        }

        /// <summary>
        /// Wire from the bootstrap / <see cref="StructureFactory"/> after AddComponent.
        /// <para>
        /// ! MEASURED DEFECT (2026-08-04 headless capture, WO-859 Phase 0): registration happens in
        /// <see cref="OnEnable"/>, and <c>AddComponent</c> on an ACTIVE GameObject runs Awake+OnEnable
        /// SYNCHRONOUSLY - i.e. BEFORE this method has been called. At that moment
        /// <see cref="_buildingId"/> is still its serialized default (<c>FarmId</c>), so EVERY
        /// collector registered itself under the key "farm". The capture is unambiguous - three
        /// consecutive lines, one per fallback collector:
        ///   <c>[Flow:Harvest] register id=farm pending=1088/2000</c> (x3)
        /// followed by <c>existence gate CLOSED for 'lumbermill' (liveCollector=no)</c> and the same
        /// for 'forge', while the FARM tick was paid into a collector whose id had since become
        /// 'forge' (<c>accrue-pending building=forge</c> rising in steps of ~12 = the FARM's yield 13
        /// x HpFraction, not the forge's 6). Consequences: lumbermill/forge income was silently
        /// WITHHELD by the no-live-collector branch, and farm income landed in the wrong pool and
        /// banked as the wrong RESOURCE.
        /// </para>
        /// The fix belongs HERE, at the one seam where the id changes, so both call sites
        /// (ResourceCollectorBootstrap and StructureFactory) are corrected by construction: drop the
        /// stale key BEFORE the id moves, then re-register under the new one.
        /// </summary>
        public void Configure(string buildingId, float maxHp = DefaultMaxHp)
        {
            string previousId = _buildingId;
            bool live = isActiveAndEnabled;
            var displaced = _displacedOnEnable;
            _displacedOnEnable = null;

            // Unregister while BuildingId still returns the OLD key (the registry removes by
            // current id), otherwise the stale "farm" entry is orphaned forever.
            if (live) ResourceCollectorRegistry.Unregister(this);

            // OnEnable registered this new component under its serialized default before the
            // factory supplied its real id. If that displaced a different live collector, restore
            // it before registering this component under the final id.
            if (displaced != null && displaced.isActiveAndEnabled &&
                !string.Equals(previousId, buildingId, System.StringComparison.Ordinal))
                ResourceCollectorRegistry.Register(displaced);

            _buildingId = buildingId;
            _maxHp = Mathf.Max(1f, maxHp);
            LoadState();
            if (_hp <= 0f) _hp = _maxHp;

            if (live)
            {
                ResourceCollectorRegistry.Register(this);
                if (!string.Equals(previousId, buildingId, System.StringComparison.Ordinal))
                    FlowTrace.Step("Harvest",
                        $"collector re-keyed '{previousId}' -> '{buildingId}' on Configure " +
                        "(AddComponent registers under the serialized default before Configure runs).");
                ResourceCollectorBootstrap.NotifyCollectorConfigured(this);
            }

            // A collector configured AFTER Start (re-purposed host) still owes its away catch-up
            // for the id it now carries; before Start, Start() will do it with the correct id.
            if (_started) CatchUpAway();
        }

        // Last accrual scale that was FlowTraced — edge-logged (per state change), never per tick.
        private float _lastLoggedAccrualScale = 1f;

        /// <summary>
        /// Add production into pending (clamped to capacity). F8-45 (owner 2026-07-11:
        /// "if they did damage to collectors then those need to reduce economy"):
        /// effective accrual = amount × HpFraction — a 40%-damaged collector produces
        /// 40% less. Broken (<see cref="IsBroken"/>) already yields ZERO accrual via
        /// the <see cref="CanAccrue"/> gate (IsActive requires IsAlive &amp;&amp; !_broken).
        /// </summary>
        public void Accrue(int amount)
        {
            if (!CanAccrue || amount <= 0) return;
            float health = HpFraction;
            if (health <= 0f) return;   // defensive — CanAccrue should already gate this
            if (Mathf.Abs(health - _lastLoggedAccrualScale) > 0.005f)
            {
                // Edge-only trace: fires when the scale CHANGES (post-hit / post-repair),
                // not on every accrual tick.
                _lastLoggedAccrualScale = health;
                FlowTrace.Step("Harvest",
                    $"collector '{_buildingId}' accrual scaled x{health:0.##} (hp {_hp:F0}/{_maxHp:F0})");
            }
            double cap = Capacity;
            double before = _pending;
            int stepsBefore = FilledSteps;
            _pending = System.Math.Min(cap, _pending + amount * (double)health);

            // ! WO-859 sec.4, THE HIGHEST-RISK LINE IN THE WHOLE CHANGE - and it is deliberately
            // OUTSIDE the `_pending > before` block below. The stamp records "production up to
            // HERE has been accounted for", which is true even when the pool was already FULL and
            // this call added nothing. If the stamp only advanced when the pool grew, it would
            // FREEZE the moment a collector caps; the player would then tap Collect and the very
            // next catch-up would re-pay the entire frozen backlog instantly, so the capacity cap
            // would bound nothing at all and the away window would be unlimited. Mirrors
            // OfflineHarvestService.cs:176-181 ("ALWAYS advance the clock - even on a zero haul").
            // Pinned by CollectorIncomeRegression case 8 [stamp-advances-at-cap]; moving this line
            // inside the block below FAILS that oracle.
            _lastAccrualMs = TimeSource.NowUnixMs();

            if (_pending > before)
            {
                FlowTrace.Throttle("Harvest", $"accrue-{_buildingId}", 2f,
                    $"accrue-pending building={_buildingId} pending={_pending:F0}/{cap:F0}");
                SaveState();
                RaiseStepChangedIfMoved(stepsBefore);
            }
            else
            {
                // At cap: nothing banked, but the advanced stamp above MUST be persisted, or a
                // relaunch would read the pre-cap stamp off disk and refill from the backlog.
                SaveState();
                FlowTrace.Throttle("Harvest", $"atcap-{_buildingId}", 30f,
                    $"collector '{_buildingId}' is AT CAP ({_pending:F0}/{cap:F0}) - production is being " +
                    "DISCARDED and the last-accrual stamp still advances (no frozen backlog).");
            }
        }

        /// <summary>CoC collect — pending → spendable wallet at home.</summary>
        public int Collect()
        {
            if (_pending <= 0.0) return 0;
            int amount = (int)System.Math.Floor(_pending);
            if (amount <= 0) return 0;
            int stepsBefore = FilledSteps;

            var eco = EconomyService.Instance;
            var res = ResolveResource();
            if (eco != null)
            {
                switch (res)
                {
                    case HarvestResource.Wood:     eco.GrantSpendable(wood: amount);     break;
                    case HarvestResource.Iron:     eco.GrantSpendable(iron: amount);     break;
                    case HarvestResource.Food:     eco.GrantSpendable(food: amount);     break;
                    case HarvestResource.Crystals: eco.GrantSpendable(crystals: amount); break;
                }
            }
            else
            {
                ResourceLedger.Credit(res, amount);
            }

            _pending -= amount;
            if (_pending < 0) _pending = 0;
            SaveState();
            FlowTrace.Step("Harvest", $"collect building={_buildingId} +{amount} {res} wallet");

            // WO-890 - THE COLLECT TAP HAD NO FEEDBACK AT ALL, and it is the most repeated
            // action in the town loop. Verified at source before this line existed: Collect
            // moved a wallet number and printed a trace, and that was the entire response -
            // no popup, no sound, no effect. Every OTHER banking path in the game already
            // emits this exact popup (MineNode.SpawnGainPopup on tap / worker extract,
            // HarvestSite via EconomyService.AddResource), so a collector was the one income
            // source that paid you silently.
            //
            // This is the SHARED income language, not a new one, and deliberately not a new
            // VFX pick: ResourceGainPopup is the code-built world text those paths already
            // use, so "+N Wood" means the same thing wherever the wood came from. It is also
            // free of the loop budget - it is a self-destroying GameObject, not a pooled
            // loop, so the most-repeated action in the game cannot starve the 20 aura slots.
            // The COLLECTOR's own state tells (the Collector_Ready beacon going out, the
            // pile emptying, the "N/20" readout) are driven by the StepChanged raised below.
            //
            // The model spawning this mirrors MineNode.Extract, the established precedent in
            // this same domain: the popup needs the AMOUNT, which only lives here for the
            // duration of this call, and ResourceGainPopup is a shared static spawner rather
            // than UI this class builds or owns.
            DeNelle.Village.World.ResourceGainPopup.Spawn(
                transform.position + Vector3.up * 1.6f,
                $"+{amount} {ResourceBuildingProgression.LabelFor(res)}",
                PopupTint(res));

            RaiseStepChangedIfMoved(stepsBefore);
            return amount;
        }

        /// <summary>
        /// Popup tint per resource. Deliberately the SAME palette MineNode.ResourceTint
        /// uses so wood reads as wood whether it came from a node or a lumbermill. The
        /// colour is a redundant channel only - the popup always names the resource in
        /// words, which is what carries the meaning for a red/green-colourblind reader.
        /// </summary>
        private static Color PopupTint(HarvestResource res)
        {
            switch (res)
            {
                case HarvestResource.Wood:     return new Color(0.55f, 0.38f, 0.22f);
                case HarvestResource.Iron:     return new Color(0.62f, 0.64f, 0.70f);
                case HarvestResource.Food:     return new Color(0.72f, 0.62f, 0.28f);
                case HarvestResource.Crystals: return new Color(0.35f, 0.72f, 0.95f);
                default:                       return Color.white;
            }
        }

        public void ApplyContactDamage(float amount)
        {
            if (!IsAlive) return;
            amount = Mathf.Max(0f, amount);
            _hp = Mathf.Max(0f, _hp - amount);
            FlowTrace.Step("Harvest", $"collector-hit building={_buildingId} hp={_hp:F0}/{_maxHp:F0} pending={_pending:F0}");
            if (_hp <= 0f) OnSiegeDestroyed();
            else SaveState();
        }

        /// <summary>Restore collector after siege break (simple repair).</summary>
        public void Repair()
        {
            // WO-753 ruling (owner 2026-07-19, SUPERSEDES WO-672's repair-back-online): a DESTROYED
            // collector is LOST - it returns ONLY via a full-cost build-mode placement, never an
            // in-place repair. Mirrors the guard Building.Repair already carries.
            if (_broken) return;
            _hp = _maxHp;
            LastLootStolen = 0f;   // F8-45: the loot report is per-break; a repair clears it
            LastLootStolenAtUnixMs = 0.0;   // ...and so does its siege stamp, or the pair could disagree
            SaveState();
            FlowTrace.Step("Harvest", $"collector-repair building={_buildingId}");
            // Leave the broken/scatter state — re-render the stack from live pending.
            StepChanged?.Invoke(this);
        }

        /// <summary>
        /// The collector BREAKS -- and, since the owner ruling of 2026-08-27, IT LOSES NOTHING.
        ///
        /// <para>! COLLECTOR LOOTING IS REMOVED. BANK THEFT REPLACES IT, and a siege bills ONCE
        /// per attack (StakeRules / DefenseReportBuilder.ApplyStakes take a bounded percentage of
        /// the UNPROTECTED BANK). The take that used to live here -- half of the uncollected
        /// pending -- is DELETED, along with RaidLootFraction, LootTakenFrom and the crystal
        /// carve-out that only existed to bound it.</para>
        ///
        /// <para>! DO NOT RE-ADD A TAKE HERE while bank theft is live. The two together charge the
        /// player twice for one siege, which is exactly the defect the superseded WO-1139 ruling
        /// was written to prevent; its oracle (SiegeLossStakesRegression) now fails the gate if a
        /// break moves a collector's pending at all.</para>
        ///
        /// <para>The pending stays in the broken shell. A destroyed collector is not repairable
        /// (WO-753) so it is not recoverable either -- but that is the STRUCTURE being lost, which
        /// is stake (1) of the ruling, not a second theft.</para>
        /// </summary>
        private void OnSiegeDestroyed()
        {
            int stepsBefore = FilledSteps;
            _broken = true;

            LastLootStolen = 0f;                            // removed by ruling -- never non-zero again
            LastLootStolenAtUnixMs = TimeSource.NowUnixMs();  // the break stamp survives as evidence
            SaveState();
            FlowTrace.Warn("Harvest",
                $"collector-destroyed building={_buildingId} resource={Resource} " +
                $"pending-kept={_pending:F0} (COLLECTOR LOOTING IS REMOVED, owner ruling 2026-08-27 -- " +
                "the siege bills the BANK once, through StakeRules; nothing is taken from this collector).");
            // Fire even if the raw step count is unchanged: IsBroken flipped, and the view
            // must switch to its scatter/hidden state. StepChanged is the collector's single
            // "re-render your visual" signal, so raise it on the break edge too.
            StepChanged?.Invoke(this);
        }

        private HarvestResource ResolveResource()
        {
            var def = ResourceBuildingProgression.Find(_buildingId);
            return def != null ? def.Yields : HarvestResource.Wood;
        }

        /// <summary>
        /// Collector reserve capacity, expressed in HOURS OF PRODUCTION (WO-859 sec.5).
        /// <para>
        /// PRIMARY path: the DATA-authored base reserve from the structures-catalog `capacity`
        /// field is the collector's L1 pool, and it is scaled by <see cref="ThroughputScale"/> -
        /// the ratio of what this collector produces NOW to what it produced at level 1 with one
        /// echo. That makes HOURS-TO-FULL CONSTANT across level and echo count, so `capacity`
        /// reads as "hours the town keeps working unattended" and stays correct through any future
        /// rate re-scale.
        /// </para>
        /// <para>
        /// WHAT THIS REPLACED, and why: the old scale was a flat <c>1 + 0.5x(level-1)</c>, i.e.
        /// capacity grew x3 from L1->L5 while throughput grew x5.6 - so UPGRADING A COLLECTOR
        /// SHORTENED how long it could run unattended (the curve ran BACKWARDS), and the echo
        /// multiplier was not in the capacity basis at all, so a 6-echo L5 farm filled in under
        /// six minutes. Capacity now grows MORE on upgrade than it used to (x5.6 at L5, not x3),
        /// so "upgrade to hold more" is strengthened, not weakened.
        /// </para>
        /// The STEWARD `collectorCap` talent sum (WO-676 Deep Reserves; x1 at sum 0) still
        /// multiplies ON TOP. FALLBACK (field absent/0): the legacy ~2-hours-of-production
        /// formula, unchanged.
        /// </summary>
        private double ComputeCapacity()
        {
            double baseCap;
            double catalogCap = CatalogCapacity();
            if (catalogCap > 0.0)
            {
                // DATA base reserve for level 1 / one echo, scaled by live throughput so the
                // FILL TIME - not the unit count - is the authored quantity.
                baseCap = catalogCap * ThroughputScale();
            }
            else
            {
                // Legacy fallback: ~2 hours of max production at current level.
                int yield = ResourceBuildingState.CurrentEffectiveYield(_buildingId);
                float interval = ResourceBuildingState.CurrentHarvestInterval(_buildingId);
                baseCap = (yield <= 0 || interval <= 0f)
                    ? 50.0
                    : System.Math.Max(50.0, yield * (3600.0 / interval) * 2.0);
            }

            // WO-676 STEWARD (Deep Reserves): ONE HeroTalentModifiers read at this existing
            // capacity calc — `collectorCap` grows how much pending the collector holds
            // before it is full. StatSum is internally null-safe (0 with no service/tree/
            // nodes), so capacity is unchanged at sum 0.
            float capBonus = DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                HeroTalentClassReader.Slug(), "collectorCap");
            if (capBonus > 0f)
            {
                baseCap *= 1.0 + capBonus;
                FlowTrace.Once("Talent", "collectorCap",
                    $"collectorCap x{1f + capBonus:0.###} applied to collector capacity (WO-676 Deep Reserves).");
            }
            return baseCap;
        }

        /// <summary>
        /// WO-859 sec.5 - how much this collector produces per hour RIGHT NOW, divided by what it
        /// produced per hour at level 1 with a single echo. Multiplying the authored `repo.capacity`
        /// by this keeps hours-to-full constant, which is the whole point.
        /// <para>
        /// INCLUDED: the level's yield + interval (both upgrade axes) and the echo
        /// <c>GlobalHarvestMultiplier</c>. EXCLUDED, deliberately: the STEWARD `harvestRate` talent.
        /// That is not an oversight - it mirrors the identical, already-shipped ruling on the Echo
        /// silo (<c>EchoService.SiloCapacity</c>, "capacity is `collectorCap`'s seam, not
        /// `harvestRate`'s"), so the game's two capacity systems obey ONE rule instead of two.
        /// </para>
        /// The denominator is the AUTHORED level-1 table row, never a live read: it is the fixed
        /// reference `repo.capacity` was authored against, so perks/echoes move the numerator only.
        /// Floored at 1.0 so capacity can never shrink below its authored base.
        /// </summary>
        private double ThroughputScale()
        {
            var def = ResourceBuildingProgression.Find(_buildingId);
            var l1 = def?.LevelDef(1);
            if (l1 == null) return 1.0;

            double baseInterval = Mathf.Max(0.5f, l1.HarvestInterval);
            double basePerHour = l1.YieldPerTick * Mathf.Max(0f, l1.YieldSizeMultiplier) * (3600.0 / baseInterval);
            if (basePerHour <= 0.0) return 1.0;

            int yieldNow = ResourceBuildingState.CurrentEffectiveYield(_buildingId);
            float intervalNow = ResourceBuildingState.CurrentHarvestInterval(_buildingId);
            if (yieldNow <= 0 || intervalNow <= 0f) return 1.0;

            double nowPerHour = yieldNow * (3600.0 / intervalNow)
                              * ResourceBuildingHarvester.EchoHarvestMultiplier();
            return System.Math.Max(1.0, nowPerHour / basePerHour);
        }

        /// <summary>
        /// The DATA-authored base collector reserve (structures-catalog `repo.capacity`) for
        /// this collector's building, or 0 when none is authored. A collector is registered
        /// under its catalog id (e.g. "collector_farm") but keyed on the bare
        /// <see cref="_buildingId"/> ("farm"), so we match on <c>repo.collectorBuildingId</c>
        /// (falling back to the entry id) across the Collector catalog type — mirroring
        /// StructureFactory's collector resolution. Null-safe: 0 if the registry is empty.
        /// </summary>
        private double CatalogCapacity()
        {
            foreach (var e in DeNelle.Core.Catalog.CatalogRegistry.OfType(DeNelle.Core.Catalog.CatalogType.Collector))
            {
                if (e == null || e.repo == null) continue;
                string bid = !string.IsNullOrEmpty(e.repo.collectorBuildingId) ? e.repo.collectorBuildingId : e.id;
                if (bid == _buildingId) return e.repo.capacity;
            }
            return 0.0;
        }

        private void LoadState()
        {
            _pending = PlayerPrefs.GetFloat(PendingPrefsPrefix + _buildingId, 0f);
            _hp = PlayerPrefs.GetFloat(HpPrefsPrefix + _buildingId, _maxHp);
            _broken = _hp <= 0f;

            // WO-859: the away stamp. Stored as a string (see LastAccrualPrefsPrefix) - an
            // unparsable/absent value reads as 0 = "never stamped", which seeds to now and
            // back-fills nothing rather than paying a bogus window.
            string stamp = PlayerPrefs.GetString(LastAccrualPrefsPrefix + _buildingId, string.Empty);
            if (string.IsNullOrEmpty(stamp) ||
                !double.TryParse(stamp, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out _lastAccrualMs))
                _lastAccrualMs = 0.0;
            if (_lastAccrualMs < 0.0) _lastAccrualMs = 0.0;
        }

        private void SaveState()
        {
            PlayerPrefs.SetFloat(PendingPrefsPrefix + _buildingId, (float)_pending);
            PlayerPrefs.SetFloat(HpPrefsPrefix + _buildingId, _hp);
            PlayerPrefs.SetString(LastAccrualPrefsPrefix + _buildingId,
                _lastAccrualMs.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// F8 2026-07-13 "lumbermill came in damaged": collector HP persists in
        /// PlayerPrefs keyed ONLY by buildingId, so a freshly PLACED structure
        /// inherited the previous building's wave damage (owner's Lumbermill spawned
        /// at hp=0.41 — proving line `[Flow:DamageVis] bar attached: 'lumbermill'
        /// (collector) hp=0.41`). A new placement is a NEW building (owner ruling:
        /// destroyed = pay to rebuild, fresh) — call this from the fresh-placement
        /// path to restore full HP + clear the stale persisted key. Reload replay of
        /// a STANDING building keeps its damage (the repair loop's domain).
        /// </summary>
        public void ResetToFullHp()
        {
            _hp = _maxHp;
            _broken = false;
            // WO-859: a fresh placement is a NEW building, so it also gets a FRESH away clock.
            // Without this it would inherit the destroyed building's stale stamp and instantly
            // back-fill a backlog it never earned (the PlayerPrefs keys are keyed by buildingId,
            // which is exactly the stale-state trap this method already exists to close for HP).
            _lastAccrualMs = TimeSource.NowUnixMs();
            SaveState();
            StepChanged?.Invoke(this);   // health/broken state moved — let the fill/damage views re-read
            FlowTrace.Step("Harvest", $"collector '{_buildingId}' HP reset to full on fresh placement (stale persisted damage cleared)");
        }
    }
}
