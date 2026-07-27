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
        private const float DefaultMaxHp = 120f;
        private const float RaidLootFraction = 0.5f;

        [SerializeField] private string _buildingId = ResourceBuildingProgression.FarmId;
        [SerializeField] private float _maxHp = DefaultMaxHp;

        private float _hp;
        private double _pending;
        private bool _broken;

        public string BuildingId => _buildingId;
        public string SourceId => _buildingId;
        public HarvestResource Resource => ResolveResource();
        public bool IsActive => IsAlive && !_broken && ResourceBuildingState.GetLevel(_buildingId) > 1;
        public bool CanAccrue => IsActive;
        public double PendingAmount => _pending;
        public bool IsBroken => _broken;

        /// <summary>Health 0..1 — the accrual scale (F8-45: damage reduces economy) and
        /// the wave damage-report fraction. Derived from this collector's own HP fields
        /// (data-driven — no per-type hardcode).</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>Pending resources stolen when the collector last broke under siege
        /// (session-scoped; cleared on <see cref="Repair"/>). The wave damage report
        /// reads it to show the "looted" line.</summary>
        public float LastLootStolen { get; private set; }

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
            ResourceCollectorRegistry.Register(this);
            HarvestSourceRegistry.Register(this);

            // WO-VFX-POI — opt in to the near-field harvest CALLOUT aura (colorblind-safe:
            // motion/shape/luminance, not hue). Spent while the collector is not producing.
            PoiBeacon.Attach(gameObject, PoiBeacon.PoiTier.Node,
                calloutRadius: 28f, handoffRadius: 3.5f,
                tint: new Color(1f, 0.94f, 0.72f, 1f),
                isSpent: () => !IsActive);
        }

        private void OnDisable()
        {
            HarvestSourceRegistry.Unregister(this);
            ResourceCollectorRegistry.Unregister(this);
            SaveState();
        }

        /// <summary>Wire from bootstrap when attaching to a hub storefront.</summary>
        public void Configure(string buildingId, float maxHp = DefaultMaxHp)
        {
            _buildingId = buildingId;
            _maxHp = Mathf.Max(1f, maxHp);
            LoadState();
            if (_hp <= 0f) _hp = _maxHp;
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
            if (_pending > before)
            {
                FlowTrace.Throttle("Harvest", $"accrue-{_buildingId}", 2f,
                    $"accrue-pending building={_buildingId} pending={_pending:F0}/{cap:F0}");
                SaveState();
                RaiseStepChangedIfMoved(stepsBefore);
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
            RaiseStepChangedIfMoved(stepsBefore);
            return amount;
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
            SaveState();
            FlowTrace.Step("Harvest", $"collector-repair building={_buildingId}");
            // Leave the broken/scatter state — re-render the stack from live pending.
            StepChanged?.Invoke(this);
        }

        private void OnSiegeDestroyed()
        {
            int stepsBefore = FilledSteps;
            _broken = true;
            float stolen = Mathf.FloorToInt((float)_pending * RaidLootFraction);
            _pending = System.Math.Max(0, _pending - stolen);
            LastLootStolen = stolen;   // F8-45: surfaced by the wave damage report ("looted N")
            SaveState();
            FlowTrace.Warn("Harvest",
                $"collector-destroyed building={_buildingId} loot-stolen={stolen} pending-left={_pending:F0}");
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
        /// Collector reserve capacity. PRIMARY path (owner creative 2026-07-24, TIGHT
        /// collect-loop): the DATA-authored base reserve from the structures-catalog
        /// `capacity` field (designer-tunable), deepened +50% per LEVEL above 1 so
        /// upgrading a collector holds more. FALLBACK (field absent/0): the legacy
        /// ~2 hours of max production formula (which over-sized the buffer so collectors
        /// never filled). The STEWARD `collectorCap` talent sum (WO-676 Deep Reserves;
        /// x1 at sum 0) multiplies ON TOP of whichever base is used.
        /// </summary>
        private double ComputeCapacity()
        {
            double baseCap;
            double catalogCap = CatalogCapacity();
            if (catalogCap > 0.0)
            {
                // DATA base reserve; +50% per level above 1 (upgrading deepens the reserve).
                int level = ResourceBuildingState.GetLevel(_buildingId);
                double levelScale = 1.0 + 0.5 * System.Math.Max(0, level - 1);
                baseCap = catalogCap * levelScale;
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
        }

        private void SaveState()
        {
            PlayerPrefs.SetFloat(PendingPrefsPrefix + _buildingId, (float)_pending);
            PlayerPrefs.SetFloat(HpPrefsPrefix + _buildingId, _hp);
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
            SaveState();
            StepChanged?.Invoke(this);   // health/broken state moved — let the fill/damage views re-read
            FlowTrace.Step("Harvest", $"collector '{_buildingId}' HP reset to full on fresh placement (stale persisted damage cleared)");
        }
    }
}