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

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Village.Crafting;
using DeNelle.Core.Diagnostics; // WO3: FlowTrace self-reporting for the percent/over-time potions

namespace DeNelle.Village.Items
{
    public static class ConsumableUseService
    {
        // Owner directive (2026-07-24): ENFORCED, visually-told use-cooldown. Mirrors the
        // Q/W/E/R ability model - state lives HERE in the service (never the View), the Try
        // gate refuses while cooling, and HudModelProducers reads these to drive the belt
        // tile's radial sweep. RUNTIME-ONLY: absolute Time.time end-stamps keyed by
        // consumableId; never persisted, no save-schema bump (matches ability cooldowns).
        private static readonly Dictionary<string, float> _nextReadyAt = new Dictionary<string, float>();

        /// <summary>Seconds of use-cooldown left on <paramref name="id"/> (0 = ready). Read by the HUD.</summary>
        public static float CooldownRemaining(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0f;
            if (!_nextReadyAt.TryGetValue(id, out var readyAt)) return 0f;
            float left = readyAt - Time.time;
            return left > 0f ? left : 0f;
        }

        /// <summary>The full authored use-cooldown of <paramref name="id"/> (from consumables.json). 0 = spammable.</summary>
        public static float CooldownTotal(string id)
        {
            var def = ConsumableCatalog.Find(id);
            return def != null ? def.UseCooldown : 0f;
        }

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

            // Cooldown gate: refuse while this consumable is still cooling (mirrors
            // HeroAbilities.TryCast's ExtraCooldownRemaining>0 refusal). Only gates authored
            // consumables (useCooldown > 0); spammable ones fall straight through.
            if (def.UseCooldown > 0f && CooldownRemaining(consumableId) > 0f)
            {
                Debug.Log("[ConsumableUse] " + consumableId + " still cooling ("
                    + CooldownRemaining(consumableId).ToString("0.0") + "s left).");
                return false;
            }

            // Consume FIRST (only spend on a real apply path).
            if (!inv.TryConsume(consumableId, 1)) return false;

            ApplyEffect(def);

            // Start the cooldown ONLY on a successful use (runtime-only; not persisted).
            if (def.UseCooldown > 0f)
                _nextReadyAt[consumableId] = Time.time + def.UseCooldown;

            return true;
        }

        private static void ApplyEffect(ConsumableDef def)
        {
            switch (def.Effect)
            {
                case ConsumableEffect.Heal:
                    ApplyHeal(def);
                    break;

                case ConsumableEffect.Rest:
                    // Tent kit: v1 applies the heal magnitude; the proper "rest
                    // between fights -> heal party to full + clear debuffs" layer
                    // is DEFERRED (no between-fight state machine yet).
                    ApplyHeal(def);
                    Debug.Log("[ConsumableUse] tent rest applied (between-fight rest layer DEFERRED).");
                    break;

                case ConsumableEffect.Mana:
                    ApplyMana(def);
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

        /// <summary>
        /// Heal the active hero. WO3: when <c>magnitudePct &gt; 0</c> the heal is a PERCENT of
        /// the hero's effective max HP (gear+talent), so "30%" scales with the build; otherwise
        /// the flat <c>magnitude</c> path is preserved. Finds the first HeroHealth in the scene.
        /// </summary>
        private static void ApplyHeal(ConsumableDef def)
        {
            var hero = Object.FindAnyObjectByType<HeroHealth>();
            if (hero == null)
            {
                Debug.Log("[ConsumableUse] no hero found to heal.");
                return;
            }

            float amount;
            if (def.MagnitudePct > 0f)
            {
                amount = def.MagnitudePct / 100f * hero.MaxHp;
                FlowTrace.Step("ConsumableUse", $"heal {def.Id}: {def.MagnitudePct}% of maxHp ({hero.MaxHp:0.0}) = {amount:0.0}.");
            }
            else
            {
                amount = def.Magnitude;
                FlowTrace.Step("ConsumableUse", $"heal {def.Id}: flat {amount:0.0}.");
            }

            if (amount <= 0f) return;
            hero?.Heal(amount);

            // Owner 2026-07-24: drinking a heal potion now plays the SAME full-prefab Hovl heal read
            // the Knight's Warden's Grace uses, so a heal is visibly a heal either way. Heal_Cast is a
            // self-lifetiming radiant burst (a oneshot prefab — no leak from this static service), fired
            // at the hero's chest through the ONE VFXManager pool (PlayKey). A missing key throttled-
            // no-ops (no throw), so this is ship-safe even before the catalog row exists.
            if (hero != null)
                VFXManager.PlayKey("Heal_Cast", hero.transform.position + Vector3.up * 1.2f,
                    Quaternion.identity, hero.transform);
        }

        /// <summary>
        /// WO3 (Mana Draught): restore mana GRADUALLY via HeroAbilities — <c>magnitudePct</c>
        /// percent of max mana spread over <c>duration</c> seconds (owner spec: +3%/s till 30%).
        /// Data-driven; the code only interprets. No-ops null-safely if no mana pool is present.
        /// </summary>
        private static void ApplyMana(ConsumableDef def)
        {
            if (def.MagnitudePct <= 0f)
            {
                Debug.Log("[ConsumableUse] mana potion has no magnitudePct; nothing to restore: " + def.Id);
                return;
            }

            var hero = Object.FindAnyObjectByType<HeroAbilities>();
            if (hero == null)
            {
                Debug.Log("[ConsumableUse] no hero mana pool found (HeroAbilities) for: " + def.Id);
                return;
            }

            float seconds = def.Duration > 0f ? def.Duration : 10f;
            hero?.RestoreManaOverTime(def.MagnitudePct, seconds);
            FlowTrace.Step("ConsumableUse", $"mana {def.Id}: +{def.MagnitudePct}% over {seconds}s (over-time drip).");
        }
    }
}
