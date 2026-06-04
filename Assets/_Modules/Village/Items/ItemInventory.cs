// =============================================================================
// ItemInventory - the item-drops lane's tiny IN-MEMORY, session inventory facade.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// HARD ISOLATION CONTRACT (mirrors CampSystem / the rest of this lane):
//   * NEW type. Touches NO existing file. It only REFERENCES the existing PUBLIC
//     read/write API of DeNelle.Village.Crafting.VillageInventory (the persisted
//     larder the Workshop already crafts from): Get / Add / TryConsume / Counts.
//   * SHIPS DARK. Every mutating call is a no-op unless ItemDropSystem.Enabled, so
//     with the flag off this holder grants nothing, logs nothing, subscribes to
//     nothing - zero footprint in the build.
//
// WHY A FACADE (not a second store): the persisted VillageInventory is the single
// source of truth for owned materials + consumables (it survives sessions). This
// holder ADDS the session layer the new drop/craft/use systems want:
//   * a SESSION log of what dropped THIS run (counts by id), for HUD/feedback;
//   * a Collected event other systems subscribe to (pickup toasts, etc.);
//   * convenience read helpers (OwnedConsumables) that filter the larder by the
//     consumable catalog without duplicating storage.
// Mutations route THROUGH VillageInventory so crafting + use stay consistent.
//
// ASCII runtime strings only. Canon: the village is Elarion (never Avalon).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Items
{
    /// <summary>
    /// Session-scoped inventory facade for the item-drops lane. Read API is public;
    /// mutate only via <see cref="GrantDrop"/> (drops route into the persisted larder).
    /// Inert whenever <see cref="ItemDropSystem.Enabled"/> is false.
    /// </summary>
    public static class ItemInventory
    {
        // Per-session tally of materials collected THIS run (id -> count). This is
        // additive feedback state only; the authoritative counts live in the larder.
        private static readonly Dictionary<string, int> _sessionDrops =
            new Dictionary<string, int>();

        /// <summary>Raised when a drop is granted: (materialId, amountGranted).
        /// Subscribe-only for HUD/pickup feedback. Never fires when the lane is off.</summary>
        public static event Action<string, int> Collected;

        // ---------------------------------------------------------------------
        // Read API (safe to call any time; returns empty/zero when nothing owned).
        // ---------------------------------------------------------------------

        /// <summary>How many of <paramref name="id"/> the player currently OWNS
        /// (reads the persisted larder, not the session tally).</summary>
        public static int Owned(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            var inv = VillageInventory.Instance;
            return inv != null ? inv.Get(id) : 0;
        }

        /// <summary>How many of <paramref name="id"/> dropped THIS session.</summary>
        public static int CollectedThisSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return _sessionDrops.TryGetValue(id, out var n) ? n : 0;
        }

        /// <summary>A snapshot of the session drop tally (copy; safe to enumerate).</summary>
        public static IReadOnlyDictionary<string, int> SessionDrops =>
            new Dictionary<string, int>(_sessionDrops);

        /// <summary>Owned consumables only (larder keys that resolve in the consumable
        /// catalog), as id -> count. Used by the use-hotbar + craft panel readout.</summary>
        public static Dictionary<string, int> OwnedConsumables()
        {
            var result = new Dictionary<string, int>();
            var inv = VillageInventory.Instance;
            if (inv == null) return result;
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                if (ConsumableCatalog.IsConsumable(kv.Key))
                    result[kv.Key] = kv.Value;
            }
            return result;
        }

        // ---------------------------------------------------------------------
        // Mutate API (dark: no-op unless the lane is enabled).
        // ---------------------------------------------------------------------

        /// <summary>
        /// Grant a dropped material into the persisted larder + record it in the
        /// session tally + raise <see cref="Collected"/>. No-op when the lane is off,
        /// when the id/amount is invalid, or when the larder is not yet bootstrapped.
        /// </summary>
        public static void GrantDrop(string id, int amount)
        {
            if (!ItemDropSystem.Enabled) return;          // SHIPS DARK.
            if (string.IsNullOrEmpty(id) || amount <= 0) return;

            var inv = VillageInventory.Instance;
            if (inv == null) return;                       // larder not ready -> drop lost (rare)

            inv.Add(id, amount);

            _sessionDrops.TryGetValue(id, out var prev);
            _sessionDrops[id] = prev + amount;

            Collected?.Invoke(id, amount);
        }

        /// <summary>Dev/test helper: clears the session drop tally (does NOT touch
        /// the persisted larder). Inert when the lane is off.</summary>
        public static void ClearSession()
        {
            if (!ItemDropSystem.Enabled) return;
            _sessionDrops.Clear();
        }
    }
}
