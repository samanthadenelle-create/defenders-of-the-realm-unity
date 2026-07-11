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

        /// <summary>Add production into pending (clamped to capacity).</summary>
        public void Accrue(int amount)
        {
            if (!CanAccrue || amount <= 0) return;
            double cap = Capacity;
            double before = _pending;
            int stepsBefore = FilledSteps;
            _pending = System.Math.Min(cap, _pending + amount);
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
            _broken = false;
            _hp = _maxHp;
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

        /// <summary>~2 hours of max production at current level (CoC internal storage).</summary>
        private double ComputeCapacity()
        {
            int yield = ResourceBuildingState.CurrentEffectiveYield(_buildingId);
            float interval = ResourceBuildingState.CurrentHarvestInterval(_buildingId);
            if (yield <= 0 || interval <= 0f) return 50.0;
            double perHour = yield * (3600.0 / interval);
            return System.Math.Max(50.0, perHour * 2.0);
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
    }
}