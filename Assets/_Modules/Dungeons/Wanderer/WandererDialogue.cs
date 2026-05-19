// =============================================================================
// WandererDialogue — Bryn the Wanderer's line table + line picker (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/wanderer/ -> _Modules/Dungeons/Wanderer/Bryn.cs + WandererDialogue.cs
//
// C# port of src/modules/dungeons/atmosphere/wandererCopy.ts — the single
// source of truth for every line Bryn speaks. Bryn is a CANON character (port
// spec Part 2 canon-names list); her voice is narrative-bible compliant —
// short sentences with weight, no exclamation marks, no all-caps, no jargon.
//
// ── Canon prose ──
// Every line below is verbatim from wandererCopy.ts (which itself sources
// docs/dungeon-tension-spec.md §4.6 and docs/narrative-bible.md). The Healer's
// Cottage entrance line is HEALERS_COTTAGE_LINE — the exact authored Beat-1
// line from docs/dungeon-3d-healers-cottage-design.md §4. NEVER paraphrase
// these; v2 foundation ships them verbatim. (In a later week the strings move
// behind data/lore-fragments.json#bryn-cottage-entry per the port-table row;
// they are inlined here as the canon source until that data file is extracted,
// matching how wandererCopy.ts is the React source of truth.)
//
// Pure C# — no MonoBehaviour. Easy to unit-test (mirrors wandererCopy.test.ts).
// =============================================================================

using System;

namespace DeNelle.Dungeons
{
    /// <summary>Bryn's recommended-level warning tiers — the wandererCopy bucket set.</summary>
    public enum WandererTier
    {
        /// <summary>recommendedLevel 1-4 — gentle.</summary>
        Tier1 = 1,
        /// <summary>recommendedLevel 5-10 — a real warning.</summary>
        Tier2 = 2,
        /// <summary>recommendedLevel 11+ — urgent.</summary>
        Tier3 = 3,
    }

    /// <summary>
    /// Bryn the Wanderer's line table + line picker. Static — the canon prose
    /// source of truth, ported verbatim from <c>wandererCopy.ts</c>.
    /// </summary>
    public static class WandererDialogue
    {
        // ── The line table (wandererCopy.ts WANDERER_COPY — verbatim) ────────

        /// <summary>Pre-meet lines — fire once, the first time Bryn's bubble appears.</summary>
        public static readonly string[] FirstMeet =
        {
            "Keeper. I walk the roads. They are colder this year.",
            "Bryn, of nowhere in particular. I've been in there. Most of them.",
        };

        /// <summary>Ambient idle lines — when Bryn is not addressing the Keeper.</summary>
        public static readonly string[] Idle =
        {
            "I'll wait. The roads will too.",
            "Pick your door, Keeper. They all open the same.",
            "A long walk, then a longer one.",
        };

        /// <summary>Tier 1 (recommendedLevel 1-4) — gentle.</summary>
        public static readonly string[] Tier1Lines =
        {
            "The path opens easy, Keeper. But mind the rocks — they remember you.",
            "A small door. Walk slow inside. The dark there isn't deep, but it is old.",
            "Bring light. Even a candle. You'll be glad of it.",
        };

        /// <summary>Tier 2 (recommendedLevel 5-10) — a real warning.</summary>
        public static readonly string[] Tier2Lines =
        {
            "I would not walk in there empty-handed. Bring fire of some sort.",
            "Strong things move in this one, Keeper. Not many — but the ones there bite.",
            "I lost a day in here once. The dark gets long.",
        };

        /// <summary>Tier 3 (recommendedLevel 11+) — urgent.</summary>
        public static readonly string[] Tier3Lines =
        {
            "Don't go light. What's in there has teeth I haven't named.",
            "Keeper — please. Bring the lantern. Bring the cloak. Bring everything.",
            "I came back from this one without a friend. Be the one who comes back.",
        };

        /// <summary>Died-before lines — override the tier pick when deaths &gt; 0.</summary>
        public static readonly string[] DiedBefore =
        {
            "You came back. Good. The dark doesn't keep what isn't given.",
            "I knew you'd return. The road always teaches twice.",
            "Slow this time. The way back is the same length as the way in.",
        };

        /// <summary>Cleared lines — override the tier pick when clears &gt; 0 (and no death).</summary>
        public static readonly string[] Cleared =
        {
            "You know this one. Walk it kind, Keeper.",
            "An old friend, this dungeon. Treat her as such.",
            "Good. The ones we've walked, we walk lighter.",
        };

        /// <summary>
        /// Bryn's EXACT authored first-encounter line for the Healer's Cottage
        /// entrance — docs/dungeon-3d-healers-cottage-design.md §4 Beat 1. A
        /// scripted, dungeon-specific line, NOT a tier-bucketed pick. Verbatim
        /// from <c>wandererCopy.ts HEALERS_COTTAGE_BRYN_LINE</c>.
        /// </summary>
        public const string HealersCottageLine =
            "The path opens easy, Keeper. But mind the rocks — they remember you. " +
            "And don't walk it dark. The cottage keeps her shadows close.";

        // ── Picker (wandererCopy.ts pick* functions) ─────────────────────────

        /// <summary>Buckets a dungeon's recommended level into a warning tier.</summary>
        public static WandererTier TierFor(int? recommendedLevel)
        {
            if (recommendedLevel == null) return WandererTier.Tier1; // unset reads as Tier 1
            if (recommendedLevel >= 11) return WandererTier.Tier3;
            if (recommendedLevel >= 5) return WandererTier.Tier2;
            return WandererTier.Tier1;
        }

        /// <summary>Stable-random pick from a list — <paramref name="seed"/> keeps it steady.</summary>
        private static string PickFrom(string[] list, int seed)
        {
            if (list == null || list.Length == 0) return string.Empty;
            int idx = Math.Abs(seed) % list.Length;
            return list[idx];
        }

        /// <summary>
        /// Chooses Bryn's line for a dungeon. Priority: died-before &gt; cleared
        /// &gt; tier-based. <paramref name="seed"/> (e.g. a per-show counter)
        /// gives a stable-random pick so repeat visits cycle rather than scripting.
        /// Port of <c>pickWandererLine</c>.
        /// </summary>
        public static string PickLine(int? recommendedLevel, int deaths, int clears, int seed)
        {
            if (deaths > 0) return PickFrom(DiedBefore, seed);
            if (clears > 0) return PickFrom(Cleared, seed);
            return TierFor(recommendedLevel) switch
            {
                WandererTier.Tier3 => PickFrom(Tier3Lines, seed),
                WandererTier.Tier2 => PickFrom(Tier2Lines, seed),
                _ => PickFrom(Tier1Lines, seed),
            };
        }

        /// <summary>Picks the first-meet introduction line. Port of <c>pickFirstMeetLine</c>.</summary>
        public static string PickFirstMeetLine(int seed) => PickFrom(FirstMeet, seed);

        /// <summary>Picks an ambient idle line. Port of <c>pickIdleLine</c>.</summary>
        public static string PickIdleLine(int seed) => PickFrom(Idle, seed);

        // ── Data-file resolution (Week-6 port-table row) ─────────────────────

        /// <summary>
        /// The canonical id of Bryn's Healer's Cottage entry dialogue fragment in
        /// <c>lore-fragments.json</c>. Mirrors <c>DungeonBrynDef.loreFragmentId</c>
        /// authored in the layout JSON.
        /// </summary>
        public const string CottageEntryFragmentId = "bryn-cottage-entry";

        /// <summary>
        /// Resolves Bryn's entrance line from a loaded lore-fragment set, keyed by
        /// <paramref name="fragmentId"/> (e.g. <c>bryn-cottage-entry</c>). Falls
        /// back to the inlined canon <see cref="HealersCottageLine"/> when the set
        /// is null or the fragment is missing — the canon prose is the same
        /// string in both places, so a missing data file never drops Bryn's voice.
        /// Port-table Week-6: "dialogue from data/lore-fragments.json#bryn-cottage-entry".
        /// </summary>
        public static string ResolveCottageEntryLine(LoreFragmentSet fragmentSet, string fragmentId)
        {
            string id = string.IsNullOrEmpty(fragmentId) ? CottageEntryFragmentId : fragmentId;
            LoreFragment fragment = fragmentSet?.Find(id);
            if (fragment != null && fragment.Body != null && fragment.Body.Length > 0)
                return fragment.OneLine;
            return HealersCottageLine;
        }
    }
}
