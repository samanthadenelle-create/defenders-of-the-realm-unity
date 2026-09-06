// =============================================================================
// BuildFilter — the six Manage > BUILD filter chips, as a CLOSED vocabulary.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog   (WO-2005)
//
// Owner ruling 5 of OWNER_RULINGS_LOCKED.md, and Manage redesign canon §3:
//     ALL / ECONOMY / DEFENSE / CRAFT / STORAGE / CIVIC
//
// ⛔ WHY THIS ONE IS CLOSED WHILE `StructureRole` IS DELIBERATELY OPEN.
// They answer different questions and the difference is load-bearing:
//   * A ROLE says WHAT A BUILDING IS. New buildings arrive with new roles, so the
//     vocabulary must stay fluid (owner 2026-08-23: "if we add a building we do not
//     want to have to manually code it"). StructureRole is therefore string-open.
//   * A FILTER is a CHIP ON A SCREEN. There are six of them, the owner named all
//     six, and a seventh cannot appear without a UI change and a ruling. A typo in
//     the data ("DEFENCE") must FAIL LOUDLY, not silently create a chip nobody can
//     see and quietly hide a building from the player.
// So this file names every legal token and `IsLegal` refuses everything else.
//
// ⛔ ALL IS NOT A MEMBERSHIP AND IS NEVER AUTHORED ON A ROW. ALL is the unfiltered
// list. Authoring "ALL" on 23 rows would be duplicated state of exactly the shape
// that produced the stale WO-number block (CLAUDE.md §2) and the retired assembly
// table (§5): the day a row is added and someone forgets the token, ALL silently
// stops meaning all. `Membership` rejects it for that reason.
//
// The MEMBERSHIP itself lives in the data — `CatalogEntry.manageFilters` — never in
// this file. There is deliberately NO id -> filter map here, for the same reason
// `StructureRoles` refuses to hold a role -> id map: one fact, one place.
// =============================================================================

using System;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// The Manage &gt; BUILD filter chips. Membership is authored per row in
    /// <see cref="CatalogEntry.manageFilters"/>; this type only names and validates
    /// the tokens.
    /// </summary>
    public static class BuildFilter
    {
        /// <summary>Every live structure, unfiltered. NEVER authored on a row.</summary>
        public const string All = "ALL";

        /// <summary>Resource producers — the faucets.</summary>
        public const string Economy = "ECONOMY";

        /// <summary>Towers, walls, gates, defensive emplacements.</summary>
        public const string Defense = "DEFENSE";

        /// <summary>Crafting / forge / armorer / jeweler style structures.</summary>
        public const string Craft = "CRAFT";

        /// <summary>Resource storage containers.</summary>
        public const string Storage = "STORAGE";

        /// <summary>Town services that fit none of the above — Barracks, Cathedral of
        /// Magic, Echo Hollow, Store, Healing Caravan.</summary>
        public const string Civic = "CIVIC";

        /// <summary>
        /// The five AUTHORABLE tokens, in the owner's chip order. <see cref="All"/> is
        /// excluded on purpose — see the header.
        /// </summary>
        public static readonly string[] Membership =
        {
            Economy, Defense, Craft, Storage, Civic
        };

        /// <summary>
        /// The full chip row as the screen shows it: ALL first, then <see cref="Membership"/>.
        /// The ONE ordering authority — a View binds this array, it does not re-list the chips.
        /// </summary>
        public static readonly string[] Chips =
        {
            All, Economy, Defense, Craft, Storage, Civic
        };

        /// <summary>
        /// True when <paramref name="token"/> is a legal value for
        /// <see cref="CatalogEntry.manageFilters"/>. Ordinal, case-insensitive.
        /// <see cref="All"/> returns FALSE here — it is a chip, not a membership.
        /// </summary>
        public static bool IsLegalMembership(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            for (int i = 0; i < Membership.Length; i++)
                if (string.Equals(Membership[i], token, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>True when <paramref name="token"/> names a chip (includes ALL).</summary>
        public static bool IsChip(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.Equals(All, token, StringComparison.OrdinalIgnoreCase)) return true;
            return IsLegalMembership(token);
        }

        /// <summary>
        /// True when <paramref name="entry"/> belongs to <paramref name="chip"/>.
        /// <see cref="All"/> matches every row that carries ANY membership, which is the
        /// definition of "live structure" for the ALL chip — a row with no membership
        /// (<c>deco_torch</c>, <c>repair_default</c>) is not Manage content and appears in
        /// no chip, including ALL.
        /// </summary>
        public static bool Matches(CatalogEntry entry, string chip)
        {
            var f = entry != null ? entry.manageFilters : null;
            if (f == null || f.Length == 0) return false;
            if (string.Equals(All, chip, StringComparison.OrdinalIgnoreCase)) return true;
            for (int i = 0; i < f.Length; i++)
                if (string.Equals(f[i], chip, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
