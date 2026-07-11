// =============================================================================
// ResourceBuildingHarvester — the runtime harvest tick that makes the resource-
// building UPGRADE LADDER actually pay out (T-025). WO-230 follow-up.
// -----------------------------------------------------------------------------
// Before this, ResourceBuildingProgression's per-level YieldPerTick was a phantom
// number: the upgrade panel SHOWED it, but nothing in the world ticked it — the
// only in-world income came from the separate HarvestSite / Outpost world-claim
// systems (their own BaseYield/HarvestInterval). So upgrading a Farm/Lumbermill/
// Forge changed a label but not the player's actual income.
//
// This driver closes that loop ENTIRELY WITHIN the upgrade silo: it owns ONE
// per-building cooldown for Farm / Lumbermill / Forge, reads the live level's
// HarvestInterval (the SPEED upgrade axis) + effective yield (YieldPerTick ×
// YieldSizeMultiplier, the SIZE axis), and credits the harvestable through the
// ResourceCollector pending buffer (CoC spine WO-663): ticks accrue into Pending on
// each collector; the player Collect All at Heart banks into the wallet. Upgrading
// a building now visibly ticks FASTER and fills the bubble MORE.
//
// SCOPE / CROSS-SILO: this references only the silo's own data (Progression +
// ResourceLedger, both DeNelle.Village -> DeNelle.Core legal). It does NOT touch
// HarvestSite, Outpost, EconomyService, WaveManager, or any scene builder. It is
// auto-spawned by BuildingUpgradePanelBootstrap (same lifetime as the panel), so
// no scene-file edit is needed. A single global cadence is intentional: the three
// resource buildings are global upgradeable economy nodes (CoC-style), not placed
// world props — matching how the panel already treats them by id.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Drives the per-level auto-harvest tick for the three resource buildings,
    /// consuming the upgrade ladder's HarvestInterval (speed) + effective yield
    /// (size). Self-contained MonoBehaviour; one instance, auto-spawned with the
    /// upgrade panel. Crediting flows through <see cref="ResourceLedger.Credit"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBuildingHarvester : MonoBehaviour
    {
        public static ResourceBuildingHarvester Instance { get; private set; }

        // Per-building elapsed time toward its current interval, parallel to
        // ResourceBuildingProgression.OrderedIds.
        private float[] _elapsed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _elapsed = new float[ResourceBuildingProgression.OrderedIds.Length];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // No service yet (Title / HeroSelect) → nothing to credit; skip cheaply.
            var ids = ResourceBuildingProgression.OrderedIds;
            if (_elapsed == null || _elapsed.Length != ids.Length) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var def = ResourceBuildingProgression.Find(id);
                if (def == null) continue;

                // Owner 2026-06-14: a FRESH game must not auto-grow resources. These global
                // CoC-style nodes pay out ONLY after the player invests an upgrade (level > 1).
                // Level 1 (the un-built default) produces nothing — manual node farming funds
                // the first upgrade, then the node ticks. Keeps the loop earned, not free.
                if (ResourceBuildingState.GetLevel(id) <= 1) continue;

                float interval = ResourceBuildingState.CurrentHarvestInterval(id);
                _elapsed[i] += dt;
                if (_elapsed[i] < interval) continue;

                // Roll over (carry the remainder so faster tiers stay accurate).
                _elapsed[i] -= interval;

                int amount = ResourceBuildingState.CurrentEffectiveYield(id);
                if (amount <= 0) continue;

                // WO-676 STEWARD (Provider's Bond): ONE HeroTalentModifiers read at this
                // existing accrual choke point — `harvestRate` scales the per-tick yield.
                // StatSum is internally null-safe (no service/tree/nodes => 0), so the
                // yield is byte-identical to baseline at sum 0. Read only when a tick
                // actually fires (interval seconds), never per-frame.
                float rateBonus = DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                    DeNelle.Village.HeroTalentClassReader.Slug(), "harvestRate");
                if (rateBonus > 0f)
                {
                    amount = Mathf.Max(amount, Mathf.RoundToInt(amount * (1f + rateBonus)));
                    DeNelle.Core.Diagnostics.FlowTrace.Once("Talent", "building-harvestRate",
                        $"harvestRate x{1f + rateBonus:0.###} applied to resource-building tick (WO-676 Provider's Bond).");
                }

                var collector = ResourceCollectorRegistry.Get(id);
                if (collector != null)
                {
                    collector.Accrue(amount);
                    continue;
                }

                // Pre-bootstrap fallback: direct grant when collector not wired yet.
                var econ = EconomyService.Instance;
                if (econ != null)
                {
                    switch (def.Yields)
                    {
                        case HarvestResource.Wood:     econ.Grant(wood: amount);     break;
                        case HarvestResource.Iron:     econ.Grant(iron: amount);     break;
                        case HarvestResource.Food:     econ.Grant(food: amount);     break;
                        case HarvestResource.Crystals: econ.Grant(crystals: amount); break;
                    }
                }
                else
                {
                    ResourceLedger.Credit(def.Yields, amount);
                }
            }
        }
    }
}
