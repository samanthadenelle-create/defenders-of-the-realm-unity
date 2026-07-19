// =============================================================================
// DungeonLootGrant — the WO-749 dungeon -> larder loot resolver.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// The ONE place the ATB/cottage dungeon banks loot into the PERSISTENT village
// larder (DeNelle.Village.Crafting.VillageInventory). Before WO-749 three seams
// granted NOTHING: treasure chests (DungeonChest.rewardKey was read by no C#),
// the per-run scatter inventory (DungeonInventory was wiped on exit), and the
// ATB encounter victory (only the composed-chain live-Enemy path fed the larder).
//
// REUSES (does NOT reinvent, CLAUDE.md §9):
//   - DeNelle.Village.Items.LootTableCatalog.Roll  (the weighted loot roll)
//   - DeNelle.Village.Crafting.VillageInventory.Add (the persistent larder)
//   - loot-tables.json dungeon-* tables (WO-749 content; Resources+StreamingAssets)
//
// INSTRUMENTED per §12: every grant/deposit branch logs [Flow:DungeonLoot];
// every roll + deposit is wrapped in Guard.Try so one bad line never blanks the
// whole grant. ASCII strings only. Canon: the village is Elarion.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;        // FlowTrace / Guard (TGVRU, §12)
using DeNelle.Village.Crafting;         // VillageInventory (persistent larder)
using DeNelle.Village.Items;            // LootTableCatalog (weighted roll)

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Resolves a dungeon loot source (a treasure-chest <c>rewardKey</c>, an ATB
    /// encounter outcome, or the per-run scatter inventory) into deposits in the
    /// persistent village larder. The single dungeon -> larder bridge (WO-749).
    /// </summary>
    public static class DungeonLootGrant
    {
        private const string Sys = "DungeonLoot";

        /// <summary>
        /// Maps a narrative chest <c>rewardKey</c> (healers-cottage.json chests) to a
        /// loot-tables.json table id. A rewardKey that is ITSELF a table id is used
        /// directly; anything unknown falls back to <c>chest-rare</c>.
        /// </summary>
        private static readonly Dictionary<string, string> RewardKeyToTable =
            new Dictionary<string, string>
            {
                { "lightbearer-cloak",   "dungeon-chest" },
                { "secret-cache-embers", "dungeon-chest" },
                { "rare-crafting-shard", "dungeon-deepboss" },
            };

        /// <summary>
        /// Larder-id aliases — the WO-749 id-canonicalization seam. Canonical larder
        /// keys are the materials.json / loot-tables materialId form. Dungeon scatter
        /// ids that already ARE larder-native (ing_* / PascalCase / kebab like
        /// ember-resin) deposit 1:1, so this map only needs entries where a dungeon
        /// pickup id has a DIFFERENT larder twin. Currently identity (extend as art
        /// lands); keeps the deposit additive with no cross-file id churn.
        /// </summary>
        private static readonly Dictionary<string, string> LarderAlias =
            new Dictionary<string, string>();

        /// <summary>The canonical larder key for a dungeon ingredient/material id.</summary>
        public static string CanonicalLarderId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            return LarderAlias.TryGetValue(id, out var mapped) ? mapped : id;
        }

        /// <summary>
        /// Rolls the loot table <paramref name="tableId"/> and deposits each rolled
        /// material into the persistent larder. <paramref name="includeBossOnly"/>
        /// opens the gem/legendary-gated lines. Returns the total item count granted.
        /// </summary>
        public static int GrantTable(string tableId, bool includeBossOnly)
        {
            if (string.IsNullOrEmpty(tableId)) return 0;
            int granted = 0;
            Guard.Try(Sys, $"grant loot table '{tableId}'", () =>
            {
                var rolled = LootTableCatalog.Roll(tableId, includeBossOnly);
                if (rolled == null || rolled.Count == 0)
                {
                    FlowTrace.Step(Sys, $"table '{tableId}' rolled nothing (bossOnly={includeBossOnly}).");
                    return;
                }
                var inv = VillageInventory.Instance;
                if (inv == null)
                {
                    FlowTrace.Warn(Sys,
                        $"VillageInventory.Instance null — {rolled.Count} line(s) from '{tableId}' LOST.");
                    return;
                }
                foreach (var kv in rolled)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                    string id = CanonicalLarderId(kv.Key);
                    inv.Add(id, kv.Value);
                    granted += kv.Value;
                    FlowTrace.Step(Sys, $"deposited '{id}' x{kv.Value} to larder (table '{tableId}').");
                }
            });
            return granted;
        }

        /// <summary>
        /// Resolves a treasure-chest <paramref name="rewardKey"/> to its table and
        /// grants it to the larder (chests may contain gems/legendaries, so the
        /// boss-only lines are opened).
        /// </summary>
        public static void GrantChest(string rewardKey)
        {
            string tableId = ResolveChestTable(rewardKey);
            FlowTrace.Step(Sys, $"chest rewardKey '{rewardKey}' -> table '{tableId}'.");
            GrantTable(tableId, includeBossOnly: true);
        }

        private static string ResolveChestTable(string rewardKey)
        {
            if (string.IsNullOrEmpty(rewardKey)) return "chest-rare";
            if (RewardKeyToTable.TryGetValue(rewardKey, out var mapped)) return mapped;
            if (LootTableCatalog.Find(rewardKey) != null) return rewardKey;  // rewardKey IS a table id
            return "chest-rare";
        }

        /// <summary>
        /// Grants the per-encounter dungeon loot roll on an ATB victory return
        /// (WO-749 gap 3 — the ATB/cottage path credited no loot). A boss uses the
        /// mini-boss table (gems); an ordinary fight uses the Hollow-family table.
        /// </summary>
        public static void GrantEncounter(bool isBoss)
        {
            string tableId = isBoss ? "dungeon-miniboss" : "dungeon-hollow";
            FlowTrace.Step(Sys, $"encounter loot (isBoss={isBoss}) -> table '{tableId}'.");
            GrantTable(tableId, includeBossOnly: isBoss);
        }

        /// <summary>
        /// Banks the per-run scatter inventory into the persistent larder on dungeon
        /// exit (WO-749 gap 2 — the scatter -> larder bridge). Deposits each held
        /// ingredient 1:1 (aliased to its canonical larder id); the caller Clears the
        /// per-run inventory afterward. Returns the total item count moved.
        /// </summary>
        public static int DepositDungeonInventory(DungeonInventory inv)
        {
            if (inv == null) return 0;
            var village = VillageInventory.Instance;
            if (village == null)
            {
                FlowTrace.Warn(Sys, "DepositDungeonInventory: no VillageInventory — scatter LOST.");
                return 0;
            }
            int moved = 0;
            Guard.Try(Sys, "deposit dungeon scatter to larder", () =>
            {
                // Snapshot the id list first (Add mutates the larder, not this list,
                // but a copy keeps the deposit robust against any future re-entrancy).
                var snapshot = new List<string>(inv.HeldIngredientIds);
                foreach (var rawId in snapshot)
                {
                    if (string.IsNullOrEmpty(rawId)) continue;
                    int count = inv.CountOf(rawId);
                    if (count <= 0) continue;
                    string larderId = CanonicalLarderId(rawId);
                    village.Add(larderId, count);
                    moved += count;
                    FlowTrace.Step(Sys, $"scatter '{rawId}' x{count} -> larder '{larderId}'.");
                }
            });
            FlowTrace.Step(Sys, $"DepositDungeonInventory: moved {moved} scatter item(s) to the larder.");
            return moved;
        }
    }
}
