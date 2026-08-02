// =============================================================================
// PlayableHeroes - the ONE registry of "which hero classes can the player play".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State   (WO-861 Phase 0)
//
// WHY THIS EXISTS. Before Phase 0 the answer to "who is playable" was hardcoded
// in THREE unrelated places, each with its own copy of the rule:
//   * GameStateService.ChooseHero      - "if (FeatureFlags.KnightOnly) cls = Knight"
//   * HeroSelectController.IsPlayable  - "private const HeroClass PlayableHero = Knight"
//   * VendorStockResolver.RosterClasses - "FeatureFlags.KnightOnly ? {knight} : {knight,mage,ranger,cleric}"
// Three truths means unlocking a hero is a three-site edit with no compiler help,
// and the store shelf could disagree with the select screen about who exists.
// This type is the single truth all three now read.
//
// TODAY'S BEHAVIOUR IS UNCHANGED. ff.knightonly defaults ON, so All == { Knight }
// exactly as before; Phase 0 removes the HARDCODING, it does not flip content on.
// Flipping PlayerPrefs "ff.knightonly" = 0 widens every consumer at once with no
// further code change - which is precisely what WO-861 Phase 1/2 need.
//
// ROSTER NOTE (deliberate, WO-861): the flag-OFF set is Knight / Ranger / Mage -
// the three heroes WO-861 makes playable (Grom / Sylas / Thrain). The CLERIC is
// NOT in it: no kit, no tree, no body work is authored for her, and HeroAbilities
// already aliases Cleric to the mage loadout. This narrows the pre-Phase-0
// VendorStockResolver.FullRoster (which listed "cleric"), so cleric-ONLY weapons
// would stop appearing on shelves once the flag is turned off. That is correct -
// the shelf follows the roster - and is inert today (flag ON = knight only).
// Add HeroClass.Cleric here the day her kit is authored; nothing else changes.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.State
{
    /// <summary>
    /// The classes the current build's player may actually play. Read by
    /// <see cref="GameStateService.ChooseHero"/> (coercion), the hero-select screen
    /// (lock state) and the vendor stock resolver (shelf roster filter).
    /// </summary>
    public static class PlayableHeroes
    {
        /// <summary>
        /// The fallback hero. Used when a selection is not playable (coercion target),
        /// when a save carries no class, and as the hero-select screen's opening slot.
        /// </summary>
        public const HeroClass Default = HeroClass.Knight;

        // V1 pivot (owner 2026-06-22): one polished hero. ff.knightonly ON => this set.
        private static readonly HeroClass[] SoloKnight = { HeroClass.Knight };

        // WO-861: the full playable roster once the flag is off - Grom / Sylas / Thrain.
        // Cleric is intentionally absent (see the header note).
        private static readonly HeroClass[] Roster =
            { HeroClass.Knight, HeroClass.Ranger, HeroClass.Mage };

        /// <summary>Every currently-playable class, in display order. Never null/empty.</summary>
        public static IReadOnlyList<HeroClass> All =>
            FeatureFlags.KnightOnly ? SoloKnight : Roster;

        /// <summary>True when the player may actually pick + play <paramref name="cls"/>.</summary>
        public static bool IsPlayable(HeroClass cls)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] == cls) return true;
            return false;
        }

        /// <summary>
        /// The lowercase catalog/persistence job key for a class ("knight" / "ranger" /
        /// "mage" / "cleric") - the SAME key weapons.json `job`, armor weight-class lookup
        /// and the per-class PlayerPrefs slots use.
        /// </summary>
        public static string JobKey(HeroClass cls)
        {
            switch (cls)
            {
                case HeroClass.Knight: return "knight";
                case HeroClass.Ranger: return "ranger";
                case HeroClass.Mage:   return "mage";
                case HeroClass.Cleric: return "cleric";
                default:               return "knight";
            }
        }

        /// <summary>
        /// The playable set as lowercase job keys - what the vendor shelf filters on.
        /// Freshly allocated per call (the set is tiny and this is not a hot path); the
        /// caller may cache it for the duration of one resolve.
        /// </summary>
        public static IReadOnlyList<string> JobKeys()
        {
            var all = All;
            var keys = new List<string>(all.Count);
            for (int i = 0; i < all.Count; i++) keys.Add(JobKey(all[i]));
            return keys;
        }

        /// <summary>EVERY class key the game has ever persisted gear under - the enum, lowercased.
        /// Used by the New Game reset to clear per-class PlayerPrefs for classes that are not
        /// currently playable but may still hold a stale equip from an earlier build/session
        /// (or from a COMPANION loadout, which binds its class the same way).</summary>
        public static IReadOnlyList<string> AllKnownJobKeys() =>
            new[] { "knight", "ranger", "mage", "cleric" };

        /// <summary>Human-readable set, for FlowTrace lines.</summary>
        public static string Describe() => string.Join(",", JobKeys());
    }
}
