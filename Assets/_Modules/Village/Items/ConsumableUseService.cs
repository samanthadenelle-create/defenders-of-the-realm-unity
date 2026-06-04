// =============================================================================
// ConsumableUseService - the "use a potion / eat food / pitch a tent kit" stub.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// ISOLATION: composes existing PUBLIC APIs read-only/additively:
//   - DeNelle.Village.Crafting.VillageInventoryInstance (consume the item, public)
//   - DeNelle.Village.HeroHealth.Heal(amount)            (apply heal, public)
// It edits NO existing file. It is a static helper any caller (a future hotbar
// button, a debug key, the crafting panel) can call to spend a crafted consumable.
//
// v1 SCOPE (deliberately a stub - content + buff math is the deferred work):
//   * Potion  + Heal  -> VillageInventory.TryConsume(id, 1); HeroHealth.Heal(mag)
//   * Food    + Heal  -> same (in-fight heal); Buff effect is logged TODO
//   * Tent    + Rest  -> heal to full BETWEEN fights only (usableInFight=false);
//                        v1 applies a Heal(mag) and logs that the rest layer is
//                        deferred (no between-fight state machine wired yet).
//   * Mana / Buff / duration-over-time effects -> recognised + logged TODO; the
//     mana pool + timed-buff system are deferred (no hero mana field exists yet).
//
// GRACEFUL: returns false and no-ops if the feature is disabled, the consumable
// is unknown, the larder is short, or no hero is found. ASCII strings only.
// =============================================================================

using UnityEngine;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Items
{
    public static class ConsumableUseService
    {
        /// <summary>
        /// Attempt to use one of <paramref name="consumableId"/> from the village
        /// larder. <paramref name="inFight"/> gates fight-only vs rest-only items.
        /// Returns true if an item was consumed and its effect applied.
        /// </summary>
        public static bool TryUse(string consumableId, bool inFight)
        {
            if (!ItemDropSystem.Enabled) return false;      // dark lane: inert when off
            if (string.IsNullOrEmpty(consumableId)) return false;

            var def = ConsumableCatalog.Find(consumableId);
            if (def == null)
            {
                Debug.LogWarning("[ConsumableUse] unknown consumable: " + consumableId);
                return false;
            }

            // Gate by context: tent kits are rest-only; some food may be fight-only.
            if (inFight && !def.UsableInFight)
            {
                Debug.Log("[ConsumableUse] " + consumableId + " cannot be used mid-fight (rest-only).");
                return false;
            }

            var inv = VillageInventory.Instance;
            if (inv == null) return false;
            if (inv.Get(consumableId) <= 0)
            {
                Debug.Log("[ConsumableUse] none in larder: " + consumableId);
                return false;
            }

            // Consume FIRST (only spend on a real apply path).
            if (!inv.TryConsume(consumableId, 1)) return false;

            ApplyEffect(def);
            return true;
        }

        private static void ApplyEffect(ConsumableDef def)
        {
            switch (def.Effect)
            {
                case ConsumableEffect.Heal:
                    ApplyHeal(def.Magnitude);
                    break;

                case ConsumableEffect.Rest:
                    // Tent kit: v1 applies the heal magnitude; the proper "rest
                    // between fights -> heal party to full + clear debuffs" layer
                    // is DEFERRED (no between-fight state machine yet).
                    ApplyHeal(def.Magnitude);
                    Debug.Log("[ConsumableUse] tent rest applied (between-fight rest layer DEFERRED).");
                    break;

                case ConsumableEffect.Mana:
                    // DEFERRED: no hero mana pool field exists yet.
                    Debug.Log("[ConsumableUse] mana restore DEFERRED (no mana pool wired): " + def.Id);
                    break;

                case ConsumableEffect.Buff:
                    // DEFERRED: timed-buff system not wired in this lane.
                    Debug.Log("[ConsumableUse] timed buff DEFERRED (no buff system wired): " + def.Id);
                    break;

                default:
                    Debug.Log("[ConsumableUse] no effect handler for: " + def.Id);
                    break;
            }
        }

        /// <summary>Heal the active hero. Finds the first HeroHealth in the scene.</summary>
        private static void ApplyHeal(float amount)
        {
            if (amount <= 0f) return;
            var hero = Object.FindFirstObjectByType<HeroHealth>();
            if (hero == null)
            {
                Debug.Log("[ConsumableUse] no hero found to heal.");
                return;
            }
            hero.Heal(amount);
        }
    }
}
