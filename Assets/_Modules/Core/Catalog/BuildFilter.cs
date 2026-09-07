// =============================================================================
// BuildFilter — the Manage > BUILD filter chips, as a CLOSED vocabulary.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog   (WO-2005)
//
// ⛔ DO NOT WRITE THE CHIP COUNT INTO THIS HEADER — COUNT `Chips` BELOW.
// This block said "the six ... chips" and listed CIVIC as canon while the CIVIC note
// above `Membership` removed it and `Chips` held FIVE. A file contradicting itself in its
// own header is the duplicated-state failure CLAUDE.md §2 (stale WO block), §5
// (retired assembly table) and §7 (MaxVisibleFaces, stale TWICE) each describe — and
// it is the same rot WO-1534 §B1 found in the tickets and the canon. Corrected
// 2026-09-07 (WO-1534 §B5 lane) by DELETING the copy, not by fixing the number.
//
// Owner ruling 5 of OWNER_RULINGS_LOCKED.md, and Manage redesign canon §3, authored
// the vocabulary. Both now carry `STALE:` banners (WO-1534 §B1) because the owner's
// mockup — docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png, screen 2 — superseded
// them. **THE ARRAYS BELOW ARE THE AUTHORITY**, and the CIVIC note that precedes
// `Membership` records what was removed and where each of its rows went.
// (No line numbers into this same file, on purpose - the first draft of this very
// block cited `:57` twice and its own insertion had already pushed that line down.)
//
// ⛔ WHY THIS ONE IS CLOSED WHILE `StructureRole` IS DELIBERATELY OPEN.
// They answer different questions and the difference is load-bearing:
//   * A ROLE says WHAT A BUILDING IS. New buildings arrive with new roles, so the
//     vocabulary must stay fluid (owner 2026-08-23: "if we add a building we do not
//     want to have to manually code it"). StructureRole is therefore string-open.
//   * A FILTER is a CHIP ON A SCREEN. The owner drew every one of them, and another
//     cannot appear without a UI change and a ruling. A typo in the data ("DEFENCE")
//     must FAIL LOUDLY, not silently create a chip nobody can see and quietly hide a
//     building from the player.
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

        // ⛔ THERE IS NO CIVIC CHIP. Do not add one back.
        // Owner mockup docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png, screen 2, draws exactly
        // FIVE chips: ALL / ECONOMY / DEFENSE / CRAFT / STORAGE. The mockup is the spec and it
        // supersedes ruling 5's six (CAPTURE_LOOP_GOAL.md 3.0c item 1). CIVIC was a bucket for
        // "town services that fit none of the above", and a bucket named after not-fitting is a
        // sign the other four were read as narrower than they are. The five rows it held were
        // re-homed by WHAT EACH BUILDING DOES, and every one of them is still reachable:
        //   barracks         -> DEFENSE  (it produces the army; that IS the town's defence)
        //   healing_caravan  -> DEFENSE  (it heals the army; it exists for the same fight)
        //   market   (Store) -> ECONOMY  (it trades - the plainest economy verb there is)
        //   pet-house(Echo Hollow) -> ECONOMY (Echoes harvest resources; the hollow is a
        //                                      production building wearing a friendlier name)
        //   arcane-tower (Cathedral of Magic) -> CRAFT (it makes spells, beside forge/armorer/
        //                                      jeweler/workshop, which are the other makers)
        // The mapping is recorded here rather than only in a WO because this array is where a
        // future seat would look to re-derive it.

        /// <summary>
        /// The AUTHORABLE tokens, in the owner's chip order. <see cref="All"/> is excluded
        /// on purpose — see the header. (No count in this sentence: it said "four" while a
        /// comment one file away said "five" and the header said "six".)
        /// </summary>
        public static readonly string[] Membership =
        {
            Economy, Defense, Craft, Storage
        };

        /// <summary>
        /// The full chip row as the screen shows it: ALL first, then <see cref="Membership"/>.
        /// The ONE ordering authority — a View binds this array, it does not re-list the chips.
        /// </summary>
        public static readonly string[] Chips =
        {
            All, Economy, Defense, Craft, Storage
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
