// =============================================================================
// BuildInventoryModel — the AUTHORITATIVE live BUILD inventory, with its filter
// membership, its CHARGED costs and its availability, computed once from data.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village   (WO-2005)
//
// Owner ruling 20 (OWNER_RULINGS_LOCKED.md): "BUILD inventory count must be
// reconciled from live definitions before numeric tests are finalized."
// Manage redesign canon §3: "Do not lock acceptance tests to a guessed total. The
// model must expose the authoritative live list."
//
// ⛔ THERE IS NO COUNT IN THIS FILE, AND THERE MUST NEVER BE ONE. Not 28, not 23,
// not 17. Every list below is derived from structures-catalog.json + card-
// collections.json + build-categories.json at call time. A hardcoded total is the
// duplicated state that produced the stale WO-number block (CLAUDE.md §2), the
// retired assembly table (§5) and the MaxVisibleFaces line that went stale TWICE
// (§7). The oracle asserts SHAPE (every offered row has a filter), never a number.
//
// ⛔ AND THERE IS NO ID LIST EITHER. The four ids that are advertised-but-
// unbuildable today (gate_stone, jeweler, tower_catapult, tower_siege_tower) are
// NOT enumerated here. Availability is READ from the same predicate the browser
// itself uses — BuildCollectionBrowser.IsCollectionItemVisible — so the day someone
// writes the missing ProgressionUnlocks.Unlock call, this model starts saying
// "offered" with no code change. A hardcoded dead-list would have to be remembered
// and would go stale the moment the game got better.
//
// ⛔ DUMB UI (canon §9). This model owns the labels, the filter membership, the
// charged lane and the availability verdict. A View binds `Rows`/`For(chip)` and
// `BuildFilter.Chips`; it does not parse ids, read a source tab, or infer a
// category from a prefix or an asset name.
//
// THE COST ASYMMETRY THIS MODEL EXISTS TO GET RIGHT (owner ruling 22, 2026-09-06):
//   * PLACED-STRUCTURE levels (repo.cost / repo.upgradeCost) are charged in the
//     lanes the JSON authors, verbatim — BuildModeController.UpgradeCostFor :2746.
//   * BUILDING-TIER ladders (building-tiers.json) are NOT. The amount is
//     Max(costWood, costCrystal) and the RESOURCE comes from the TIER INDEX, so
//     every tier 2 in the game is charged STONE whatever its JSON says, and the
//     Cathedral's authored crystals are never taken. That rule lives in exactly one
//     place, BuildingTierChargeLane, and this model reads it rather than restating
//     it. Reporting the AUTHORED key here would make every tier-2 cost wrong.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;                 // CardCollectionCatalog / CardCollectionDocument live here
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>Why a row is (or is not) buildable right now. Derived, never authored.</summary>
    public enum BuildAvailability
    {
        /// <summary>Not player content at all — the row carries no Manage filter
        /// (deco_torch, repair_default). Appears in no chip, including ALL.</summary>
        NotPlayerContent = 0,

        /// <summary>In a collection AND visible: the player can open BUILD and place it.</summary>
        Offered = 1,

        /// <summary>In a collection but hidden right now, waiting on a ProgressionUnlocks
        /// flag. <see cref="BuildInventoryRow.AvailabilityReason"/> names the step when the
        /// game names it — and is NULL when nothing does, which is the defect itself.</summary>
        HiddenPendingUnlock = 2,

        /// <summary>In a collection and hidden by a CODE constant that precedes every unlock
        /// check (<c>BuildCollectionBrowser.HiddenUntilFinishedArtId</c>, unfinished card art).
        /// Its unlock flag genuinely flips and genuinely does not matter — a dead tile that no
        /// data edit can revive.</summary>
        HiddenByArtGate = 3,

        /// <summary>Authored in the catalog but in no card collection, so the browser can
        /// never offer it however the flags fall.</summary>
        NotInAnyCollection = 4,
    }

    /// <summary>One reconciled BUILD row. Everything a tile needs, supplied by the model.</summary>
    public sealed class BuildInventoryRow
    {
        /// <summary>The opaque catalog id / save key. Never rendered to a player.</summary>
        public string Id;
        /// <summary>The player-facing name, straight off the catalog row.</summary>
        public string DisplayName;
        /// <summary>The one-sentence authored description, or null.</summary>
        public string Description;
        /// <summary>Authored <see cref="BuildFilter"/> memberships. Never empty for a live row.</summary>
        public string[] Filters = Array.Empty<string>();
        /// <summary>The art-sheet tile name for this row, or null when no tile is drawn.</summary>
        public string ArtKey;
        /// <summary>The catalog type (Tower/Wall/Resource/...). Presentation must NOT use it as a filter.</summary>
        public CatalogType Type;
        /// <summary>The role the row claims, or "" — diagnostics only; a role is not a category.</summary>
        public string Role;

        /// <summary>Placed-structure level ceiling (repo.maxLevel). 1 = no level ladder.</summary>
        public int MaxLevel;
        /// <summary>The building-TIER ladder this row drives, via
        /// <see cref="CatalogRegistry.ResolveUpgradeId"/>, or null when it drives none.</summary>
        public string TierLadderId;
        /// <summary>Top tier of <see cref="TierLadderId"/>, or 0.</summary>
        public int TierLadderMaxTier;
        /// <summary>True when another catalog row resolves to the SAME tier ladder — a live
        /// naming collision the reconciliation must surface rather than pick a winner for.</summary>
        public bool TierLadderShared;

        /// <summary>Availability verdict, derived from the browser's own predicate.</summary>
        public BuildAvailability Availability;
        /// <summary>One sentence naming the gate, or null when Offered. Model-owned copy.</summary>
        public string AvailabilityReason;

        /// <summary>True when at most one may exist in the village (repo.singleton).</summary>
        public bool Singleton;
        /// <summary>The resource word this row stores, or null when it is not a container.</summary>
        public string StorageResource;

        /// <summary>The build cost, in the lanes it is actually charged in.</summary>
        public DeNelle.Core.Catalog.ResourceCost BuildCost;

        /// <summary>Per-tier CHARGED cost of the building-tier ladder, if any. Empty otherwise.</summary>
        public List<BuildTierChargeRow> TierCharges = new List<BuildTierChargeRow>();
    }

    /// <summary>One tier rung, priced in the lane the player is actually charged.</summary>
    public sealed class BuildTierChargeRow
    {
        public int Tier;
        /// <summary>The player-facing word for the charged lane (Wood / Stone / Iron).</summary>
        public string ChargedResource;
        /// <summary>The amount taken from that lane.</summary>
        public int ChargedAmount;
        /// <summary>The Gold term, charged alongside.</summary>
        public int Gold;
        /// <summary>The village tier this rung requires (0 = none).</summary>
        public int RequiresVillageTier;
        /// <summary>The key the JSON authors the amount under — "costWood" or "costCrystal".
        /// Kept ONLY so a mismatch is visible; never use it to price anything.</summary>
        public string AuthoredKey;
        /// <summary>True when <see cref="AuthoredKey"/> names a resource that is not
        /// <see cref="ChargedResource"/> — the WO-1391 / ruling-22 lane mismatch.</summary>
        public bool AuthoredLaneDisagrees;
    }

    /// <summary>
    /// The reconciled BUILD inventory. Call <see cref="Rows"/> for everything the catalog
    /// authors, <see cref="For"/> for one filter chip.
    /// </summary>
    public static class BuildInventoryModel
    {
        /// <summary>The chip row, in the owner's order. Bind this; do not re-list the chips.</summary>
        public static IReadOnlyList<string> Chips => BuildFilter.Chips;

        /// <summary>
        /// Every catalog row, reconciled — including rows the browser does not offer, so a
        /// caller can SEE the dead tiles rather than inherit a silently shorter list.
        /// Rebuilt on every call: the catalog, the unlock flags and the collections can all
        /// change mid-session and a memoized inventory would quietly lie.
        /// </summary>
        public static List<BuildInventoryRow> Rows()
        {
            var rows = new List<BuildInventoryRow>();
            var all = CatalogRegistry.All();
            if (all == null)
            {
                FlowTrace.Fail("BuildInventory",
                    "CatalogRegistry.All() returned null - the BUILD inventory is EMPTY. The " +
                    "catalog failed to load; see the [Flow:Catalog] boot lines.");
                return rows;
            }

            var offered = OfferedIds();
            var ladderUsers = LadderUseCounts(all);

            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                var row = Guard.Try<BuildInventoryRow>("BuildInventory", "reconcile '" + e.id + "'",
                    () => Reconcile(e, offered, ladderUsers), fallback: null);
                if (row != null) rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// The rows in one chip. ALL returns every LIVE row — i.e. every row carrying a
        /// filter membership — exactly once, in catalog order.
        /// </summary>
        public static List<BuildInventoryRow> For(string chip)
        {
            var result = new List<BuildInventoryRow>();
            if (!BuildFilter.IsChip(chip))
            {
                FlowTrace.Fail("BuildInventory",
                    "asked for filter '" + chip + "', which is not one of BuildFilter.Chips. " +
                    "Returning EMPTY rather than silently showing the wrong list.");
                return result;
            }
            foreach (var r in Rows())
            {
                if (r.Filters == null || r.Filters.Length == 0) continue;
                if (string.Equals(chip, BuildFilter.All, StringComparison.OrdinalIgnoreCase)) { result.Add(r); continue; }
                for (int i = 0; i < r.Filters.Length; i++)
                    if (string.Equals(r.Filters[i], chip, StringComparison.OrdinalIgnoreCase)) { result.Add(r); break; }
            }
            return result;
        }

        /// <summary>
        /// The rows a BUILD tile grid should actually render for <paramref name="chip"/>: the
        /// <see cref="BuildAvailability.Offered"/> subset of <see cref="For"/>.
        ///
        /// <para>⛔ THIS IS THE ONE THAT SATISFIES "ALL includes every live structure exactly
        /// once" (WO-2005 numeric acceptance criteria). <see cref="For"/> deliberately returns
        /// the WIDER inventory - it includes rows that are authored and categorised but that no
        /// player action can reach (<c>wall_stone</c>, which is reached by upgrading
        /// <c>wall_wood</c>; <c>mill</c> and <c>lumbermill</c>, which sit in no collection;
        /// and the hidden-pending-unlock rows) - because the reconciliation and the oracle must
        /// SEE those, and a model that silently drops them is how they stayed invisible for
        /// months. A grid that binds <see cref="For"/> would ship dead tiles.</para>
        ///
        /// <para>Teasing an <see cref="BuildAvailability.HiddenPendingUnlock"/> row as a locked
        /// tile is a legitimate design choice and an OWNER call - the browser hides them today
        /// (BuildCollectionBrowser.cs:365-377, owner 2026-08-29 "hide cards that are not
        /// unlocked"). If that is revisited, add a second accessor; do not widen this one, and
        /// never let a View decide it by filtering on its own.</para>
        /// </summary>
        public static List<BuildInventoryRow> Tiles(string chip)
        {
            var result = new List<BuildInventoryRow>();
            foreach (var r in For(chip))
                if (r.Availability == BuildAvailability.Offered) result.Add(r);
            return result;
        }

        // ---------------------------------------------------------------------

        private static BuildInventoryRow Reconcile(
            CatalogEntry e, HashSet<string> offered, Dictionary<string, int> ladderUsers)
        {
            var row = new BuildInventoryRow
            {
                Id = e.id,
                DisplayName = e.displayName,
                Description = e.description,
                Filters = e.manageFilters ?? Array.Empty<string>(),
                ArtKey = e.manageArtKey,
                Type = e.type,
                Role = string.IsNullOrEmpty(e.role) ? StructureRole.None : e.role,
                MaxLevel = e.repo != null ? Mathf.Max(1, e.repo.maxLevel) : 1,
                Singleton = e.repo != null && e.repo.singleton,
                StorageResource = e.repo != null ? e.repo.storageResource : null,
                BuildCost = e.repo != null ? e.repo.cost : default,
            };

            // --- the building-TIER ladder this row drives, and whether it shares it ---
            string ladder = CatalogRegistry.ResolveUpgradeId(e.id);
            if (!string.IsNullOrEmpty(ladder) && BuildingTierCatalog.IsUpgradable(ladder))
            {
                row.TierLadderId = ladder;
                row.TierLadderMaxTier = BuildingTierCatalog.MaxTier(ladder);
                row.TierLadderShared = ladderUsers.TryGetValue(ladder, out int n) && n > 1;
                var def = BuildingTierCatalog.Find(ladder);
                if (def?.Tiers != null)
                    foreach (var t in def.Tiers)
                    {
                        if (t == null) continue;
                        var lane = BuildingTierChargeLane.For(t);
                        string authoredKey = t.CostCrystal > t.CostWood ? "costCrystal"
                                           : t.CostWood > 0 ? "costWood" : null;
                        row.TierCharges.Add(new BuildTierChargeRow
                        {
                            Tier = t.Tier,
                            ChargedResource = ResourceBuildingProgression.LabelFor(lane),
                            ChargedAmount = t.PrimaryMaterialCost,
                            Gold = t.CostGold,
                            RequiresVillageTier = t.RequiresVillageTier,
                            AuthoredKey = authoredKey,
                            // "costWood" authored on a tier that is charged Wood is the only
                            // agreement possible; every other pairing is a mismatch.
                            AuthoredLaneDisagrees =
                                authoredKey != null &&
                                !(authoredKey == "costWood" && lane == HarvestResource.Wood),
                        });
                    }
            }

            // --- availability, read from the browser's own predicate ---
            bool inCollection = offered.Contains(e.id);
            bool visible = inCollection && BuildCollectionBrowser.IsCollectionItemVisible(e.id);

            if (row.Filters.Length == 0)
            {
                row.Availability = BuildAvailability.NotPlayerContent;
                row.AvailabilityReason = null;
            }
            else if (visible)
            {
                row.Availability = BuildAvailability.Offered;
                row.AvailabilityReason = null;
            }
            else if (!inCollection)
            {
                row.Availability = BuildAvailability.NotInAnyCollection;
                row.AvailabilityReason = "Not offered anywhere in the build browser.";
            }
            else if (string.Equals(e.id, BuildCollectionBrowser.HiddenUntilFinishedArtId,
                                   StringComparison.OrdinalIgnoreCase))
            {
                // The hard hide runs BEFORE any flag is read (BuildCollectionBrowser.cs:357), so
                // this row stays invisible even though its unlock genuinely flips
                // (RewardedProgression.TryUnlockStoneGate, fired from wall_wood reaching L2).
                row.Availability = BuildAvailability.HiddenByArtGate;
                row.AvailabilityReason =
                    "Hidden until its card art is finished - the unlock is earned and has no effect.";
            }
            else
            {
                // In a collection but hidden, waiting on a ProgressionUnlocks flag. The model
                // reports only what is DERIVABLE at runtime: whether anything names the step.
                // ⛔ It does NOT claim "unreachable" - that verdict needs a repo-wide search for
                // an unlock WRITER, which no runtime check can do and which a hardcoded id list
                // here would freeze into a lie the day someone writes the missing call.
                // BuildInventoryFilterRegression carries that finding as a dated, cited,
                // SELF-CLEANING exemption instead.
                row.Availability = BuildAvailability.HiddenPendingUnlock;
                row.AvailabilityReason = RewardedProgression.LockReasonFor(e.id);
                if (string.IsNullOrEmpty(row.AvailabilityReason))
                    FlowTrace.Once("BuildInventory", "unnamed-lock-" + e.id,
                        "'" + e.id + "' (" + e.displayName + ") is advertised in a card collection, " +
                        "is HIDDEN, and NOTHING names the step that would reveal it. Either a " +
                        "player action writes its ProgressionUnlocks flag and the game never says " +
                        "which, or nothing does and the tile is dead. See " +
                        "docs/PREREQUISITE_REGISTRY_2026-09-06.md section 2.1.");
            }

            return row;
        }

        /// <summary>Every id any card collection points at. Data, never a literal list.</summary>
        private static HashSet<string> OfferedIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var doc = Guard.Try<CardCollectionDocument>("BuildInventory", "resolve card collections",
                () => CardCollectionCatalog
                    .CreateDefault(Application.persistentDataPath, Application.version)
                    .Resolve(),
                fallback: (CardCollectionDocument)null);
            if (doc?.Collections == null)
            {
                FlowTrace.Fail("BuildInventory",
                    "card-collections.json did not resolve - EVERY row will report " +
                    "NotInAnyCollection. This is a read failure, not an empty game.");
                return set;
            }
            foreach (var c in doc.Collections)
            {
                if (c?.Items == null) continue;
                foreach (var i in c.Items)
                    if (i != null && !string.IsNullOrEmpty(i.ItemId)) set.Add(i.ItemId);
            }
            return set;
        }

        /// <summary>
        /// How many catalog rows resolve to each tier-ladder id. &gt;1 is a live collision:
        /// two different buildings driving one ladder, which the reconciliation must SAY
        /// rather than resolve by picking a winner (that is an owner call).
        /// </summary>
        private static Dictionary<string, int> LadderUseCounts(IEnumerable<CatalogEntry> all)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                string ladder = CatalogRegistry.ResolveUpgradeId(e.id);
                if (string.IsNullOrEmpty(ladder) || !BuildingTierCatalog.IsUpgradable(ladder)) continue;
                counts.TryGetValue(ladder, out int n);
                counts[ladder] = n + 1;
            }
            return counts;
        }
    }
}
