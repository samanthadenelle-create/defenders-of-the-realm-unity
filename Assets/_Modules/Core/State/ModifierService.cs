// =============================================================================
// ModifierService — the single source of the active GameModifiers (WO-430).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// THE read-point every consumer + scene uses. Compiles GameState.BuildingTiers
// through BuildingTierCatalog into one GameModifiers (multiplying each building's
// CURRENT-tier contribution), OR returns an OVERRIDE when one is set (dev menu /
// scene-creation modifier JSON — owner: "all scene creations should allow an
// override accepting a modifier JSON that applies these perks").
//
// Consumers: DefenseTower/ArcaneTower (damage/range), TroopDeployer/TroopController
// (damage/health — this is what carries the perks INTO RAIDS), ResourceBuildingState
// (production/offline). They read ModifierService.Active. Village->Core is allowed,
// so every Village consumer can read this Core service.
//
// No internal cache: Active recomputes on each read (iterating <=5 buildings is
// trivial) so it is never stale after a save load / tier change. Listeners that
// want to react to a change (UI refresh) subscribe to Changed.
// =============================================================================

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.State
{
    public static class ModifierService
    {
        private static GameModifiers _override;

        /// <summary>Fired when the active modifiers may have changed (tier bought, override set/cleared).</summary>
        public static event Action Changed;

        /// <summary>True while a dev/scene override is in force (Active ignores the player's real tiers).</summary>
        public static bool HasOverride => _override != null;

        /// <summary>The active perk contract: the override if set, else compiled from the player's building tiers.</summary>
        public static GameModifiers Active => _override ?? Compute();

        /// <summary>The player's current tier for a building (0 = locked/none).</summary>
        public static int TierOf(string buildingId)
        {
            var tiers = GameStateService.Instance != null && GameStateService.Instance.State != null
                ? GameStateService.Instance.State.BuildingTiers : null;
            if (tiers != null && buildingId != null && tiers.TryGetValue(buildingId, out int t)) return t;
            return 0;
        }

        /// <summary>
        /// The production multiplier for a specific resource building (WO-430): maps the building
        /// to the relevant active mult (lumbermill→wood, windmill→food, forge→efficiency). Returns
        /// 1.0 (no-op) for any building not in the city-upgrade set, so non-WO-430 yield is untouched.
        /// </summary>
        public static float ProductionMultFor(string buildingId)
        {
            var m = Active;
            switch (buildingId)
            {
                case "lumbermill": return m.WoodProductionMult;
                case "windmill":   return m.FoodProductionMult;
                case "forge":      return m.ResourceEfficiencyMult;
                default:           return 1f;
            }
        }

        /// <summary>Force the active modifiers to a fixed contract (dev menu / scene override). Pass null to clear.</summary>
        public static void SetOverride(GameModifiers modifiers)
        {
            _override = modifiers;
            Changed?.Invoke();
        }

        /// <summary>Set the override from a JSON GameModifiers (scene-config / dev paste). Empty/invalid → no-op + cleared.</summary>
        public static void SetOverrideJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) { ClearOverride(); return; }
            try { SetOverride(JsonConvert.DeserializeObject<GameModifiers>(json) ?? GameModifiers.None.Clone()); }
            catch (Exception ex) { Debug.LogWarning($"[ModifierService] bad override JSON, cleared: {ex.Message}"); ClearOverride(); }
        }

        public static void ClearOverride() { _override = null; Changed?.Invoke(); }

        /// <summary>Signal that the underlying tiers changed (after an upgrade / load) so listeners refresh.</summary>
        public static void Recompute() => Changed?.Invoke();

        // ── Compile tiers → modifiers ────────────────────────────────────────
        private static GameModifiers Compute()
        {
            var result = new GameModifiers();
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var tiers = state != null ? state.BuildingTiers : null;

            if (tiers != null)
            {
                foreach (var kv in tiers)
                {
                    int tier = kv.Value;
                    if (tier < 1) continue;
                    var def = BuildingTierCatalog.TierOf(kv.Key, tier);
                    if (def != null && def.Modifiers != null) Apply(result, def.Modifiers);
                }
            }

            // WO-432 — owned research perks fold in ON TOP of the tier modifiers (a perk's effect IS a
            // GameModifiers, compiled identically). Key = "buildingId:perkId". This is what carries the
            // numerical research (damage/armor) + the signature ability flags into towers/troops/raids.
            var owned = state != null ? state.OwnedBuildingPerks : null;
            if (owned != null)
            {
                foreach (var key in owned)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    int sep = key.IndexOf(':');
                    if (sep <= 0 || sep >= key.Length - 1) continue;
                    var perk = BuildingTierCatalog.FindPerk(key.Substring(0, sep), key.Substring(sep + 1));
                    if (perk != null && perk.Modifiers != null) Apply(result, perk.Modifiers);
                }
            }
            return result;
        }

        // Multiply the mults, OR the ability flags (each building sets a near-disjoint slice).
        private static void Apply(GameModifiers r, GameModifiers m)
        {
            r.TowerDamageMult        *= m.TowerDamageMult;
            r.TowerRangeMult         *= m.TowerRangeMult;
            r.TroopDamageMult        *= m.TroopDamageMult;
            r.TroopHealthMult        *= m.TroopHealthMult;
            r.WoodProductionMult     *= m.WoodProductionMult;
            r.FoodProductionMult     *= m.FoodProductionMult;
            r.ResourceEfficiencyMult *= m.ResourceEfficiencyMult;
            r.OfflineBonusMult       *= m.OfflineBonusMult;
            r.ArmyCapBonus  += m.ArmyCapBonus;   // additive: +5 troops per owning perk/tier
            r.AutoCollect   |= m.AutoCollect;    // OR: any owning perk enables auto-gather
            r.ArcaneOverload |= m.ArcaneOverload;
            r.BattleForged   |= m.BattleForged;
            r.Forgefire      |= m.Forgefire;
            r.EternalGrove   |= m.EternalGrove;
            r.WindsOfPlenty  |= m.WindsOfPlenty;
        }
    }
}
