// =============================================================================
// BagConsumableUseEffect - the Bag's REAL consumable effect wiring (WO-844).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// The one adapter InventoryVM.CreateDefault binds as the Use command's effect seam.
// It routes a Bag "Use" through the SAME effect authority the battle potion slots
// fire (ConsumableUseService.TryUse, see HudKitCommandBridge), instead of the old
// bare store decrement that consumed the item with zero effect (the WO-844 bug).
//
// HONEST PRE-GATES (each refusal returns a player-facing reason and consumes
// NOTHING - TryUse spends the item the moment it runs, so gates sit BEFORE it):
//   * feature dark / unknown id (the Bag's Consumables tab is the owned-item
//     catch-all, so crafting materials land here too) -> "cannot be used",
//   * rest-only items mid-fight (inFight resolved LIVE from BattleLock),
//   * authored use-cooldown still running,
//   * heal/rest drinks with the hero already at full health (owner acceptance:
//     a potion at full HP must NOT burn the item).
//
// SIDE OF THE SEAM: this class may touch UnityEngine + scene finds + statics;
// InventoryVM stays pure and fake-testable (it only sees the Func + result).
// The DECREMENT CONTRACT lives in InventoryUseResult: Applied == true means
// TryUse already consumed one from VillageInventory - the same instance the
// VM's InventoryStore wraps, so the Bag count and the battle-belt count agree
// and the VM must never TryRemove on Use.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;        // BattleLock - live in-fight state
using DeNelle.Village.Items;      // ConsumableCatalog / ConsumableUseService / ItemDropSystem

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Default effect seam for the Bag's Use command: pre-gate honestly, then apply
    /// through <see cref="ConsumableUseService.TryUse"/> (which consumes the item).
    /// </summary>
    public static class BagConsumableUseEffect
    {
        public static InventoryUseResult Use(string id)
        {
            if (string.IsNullOrEmpty(id))
                return InventoryUseResult.Refused("Nothing selected.");

            if (!ItemDropSystem.Enabled)
                return InventoryUseResult.Refused("Nothing happens.");

            var def = ConsumableCatalog.Find(id);
            if (def == null)
            {
                // Owned non-catalog stock (crafting materials, drops) projects into the
                // Consumables tab; it has no use effect - keep it, tell the truth.
                return InventoryUseResult.Refused("That cannot be used.");
            }

            bool inFight = BattleLock.IsInBattle();
            if (inFight && !def.UsableInFight)
                return InventoryUseResult.Refused("Cannot be used during a fight.");

            if (def.UseCooldown > 0f)
            {
                float left = ConsumableUseService.CooldownRemaining(id);
                if (left > 0f)
                    return InventoryUseResult.Refused(
                        "Ready in " + System.Math.Ceiling(left).ToString("0") + "s.");
            }

            // Full-health gate: TryUse consumes FIRST and heals blind (Heal clamps at max),
            // so a full-HP drink would burn the item for nothing. Gate before the spend.
            if (def.Effect == ConsumableEffect.Heal || def.Effect == ConsumableEffect.Rest)
            {
                var hero = Object.FindAnyObjectByType<HeroHealth>();
                if (hero == null)
                    return InventoryUseResult.Refused("No hero to heal.");
                if (hero.Hp >= hero.MaxHp)
                    return InventoryUseResult.Refused("Already at full health.");
            }

            // The real spend + effect (heal/mana/rest + VFX + cooldown start) - the exact
            // path the battle belt uses, so Bag use and belt use can never diverge.
            bool ok = ConsumableUseService.TryUse(id, inFight);
            return ok ? InventoryUseResult.Ok()
                      : InventoryUseResult.Refused("That had no effect.");
        }
    }
}
