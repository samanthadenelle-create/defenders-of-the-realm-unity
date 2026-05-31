// =============================================================================
// TownsfolkDialogue — the ambient-villager flavour-line table (Workstream D).
// -----------------------------------------------------------------------------
// The dialogue content for the Avalon village's ambient townsfolk. When the
// Keeper draws near a villager a word bubble shows ONE of these lines; it hides
// again when the Keeper walks away (see AmbientNPC / TownsfolkController).
//
// LOCALIZE: every spoken string in this file is user-facing flavour text. The
// task brief keeps townsfolk dialogue OUT of the shared en.json (other agents
// own that file) — these are kept here as clearly-marked // LOCALIZE: constants
// so a future localization pass can lift them wholesale into the string table.
//
// Voice. Avalon is a magical + medieval crossover town that defends the Heart
// of Elarion against the Hollow Ones. The lines lean cozy, lived-in, and a
// little anxious about the waves — they should make the town read as ALIVE in
// a grant demo without long-winded lore dumps. Each villager "type" gets its
// own small pool so a market trader sounds different from an off-duty guard.
//
// No assembly dependency: this is a plain data class in DeNelle.Village,
// referenced only by the NPC MonoBehaviours in the same module.
// =============================================================================

using System;

namespace DeNelle.Village
{
    /// <summary>
    /// The flavour-line pools the ambient townsfolk speak. Static, read-only
    /// data — picked from by <see cref="AmbientNPC"/> through
    /// <see cref="LineFor"/>.
    /// </summary>
    public static class TownsfolkDialogue
    {
        /// <summary>
        /// The ambient-villager archetypes. Each maps to its own line pool and a
        /// display name, so a wandering trader reads differently from an
        /// idle-by-the-well gossip or an off-duty wall guard.
        /// </summary>
        public enum Archetype
        {
            /// <summary>A market trader / craftsperson — busy, transactional, warm.</summary>
            Trader = 0,
            /// <summary>An everyday villager going about their day — cozy, grounded.</summary>
            Villager = 1,
            /// <summary>An off-duty guard / wall-watcher — wary, eyes on the gates.</summary>
            Guard = 2,
            /// <summary>A young child of the village — playful, awed by the Heart.</summary>
            Child = 3,
            /// <summary>An elder who remembers older wars — calm, encouraging.</summary>
            Elder = 4,
        }

        // ── Display names per archetype ──────────────────────────────────────
        // LOCALIZE: shown as the speech-bubble attribution line.
        private static readonly string[] _names =
        {
            "Elarion Trader",  // Trader
            "Villager",        // Villager
            "Off-duty Guard",  // Guard
            "Village Child",   // Child
            "Village Elder",   // Elder
        };

        // ── Trader lines ─────────────────────────────────────────────────────
        // LOCALIZE: ambient market-trader flavour.
        private static readonly string[] _trader =
        {
            "Fresh moon-pears, Keeper! Picked before the dew burned off.",
            "Crystal dust's gone dear since the last wave — everyone wants warding charms.",
            "Trade you a warm loaf for a kind word. The Heart keeps us, but bread keeps the cold out.",
            "Mind the east stall — old Harrow stacks his barrels where folk trip.",
            "Every coin I take goes to mending the walls. We all pay the wall its due.",
        };

        // ── Villager lines ───────────────────────────────────────────────────
        // LOCALIZE: ambient everyday-villager flavour.
        private static readonly string[] _villager =
        {
            "Morning, Keeper. The Heart glowed bright last night — slept like a babe, I did.",
            "They say the Hollow Ones fear the violet light. I hope they're right.",
            "My garden's blooming again. Funny how flowers don't care about the waves.",
            "If you're headed to the gates, give them a knock for luck from me.",
            "Heard the Healer took in a wanderer at the cottage. Strange days.",
            "Quiet today. Almost too quiet — but I'll take quiet over the drums.",
        };

        // ── Guard lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient off-duty-guard flavour.
        private static readonly string[] _guard =
        {
            "Walls held through the night. They always do — but I still count every stone.",
            "Force-fields are humming steady on all four gates. Sleep easy, Keeper.",
            "Saw movement past the south lane at dusk. Could be deer. Could be worse.",
            "Off-duty, but a guard's never truly off. Shout if the horn sounds.",
            "Keep a tower manned and the Hollow Ones break against it like sea on rock.",
        };

        // ── Child lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient village-child flavour.
        private static readonly string[] _child =
        {
            "Are you the Keeper? My da says you're why the lights stay on!",
            "I'm not scared of the Hollow Ones. Well — not in the daytime.",
            "When I grow up I'll guard the Heart too. You'll teach me, won't you?",
            "Watch this! ...okay, I can't really do magic yet. Soon though!",
        };

        // ── Elder lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient village-elder flavour.
        private static readonly string[] _elder =
        {
            "I've seen three Keepers stand where you stand. The realm endures, child.",
            "The Heart of Elarion was old when my grandmother was young. Tend it well.",
            "Waves come and waves break. What matters is the town behind the wall.",
            "Rest when you can, Keeper. Even a guardian must close their eyes.",
        };

        /// <summary>Returns the speech-bubble display name for an archetype.</summary>
        public static string NameFor(Archetype archetype)
        {
            int i = (int)archetype;
            return (i >= 0 && i < _names.Length) ? _names[i] : "Villager";
        }

        /// <summary>The full line pool for an archetype.</summary>
        public static string[] PoolFor(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.Trader:   return _trader;
                case Archetype.Villager: return _villager;
                case Archetype.Guard:    return _guard;
                case Archetype.Child:    return _child;
                case Archetype.Elder:    return _elder;
                default:                 return _villager;
            }
        }

        /// <summary>
        /// Picks a line from <paramref name="archetype"/>'s pool. <paramref name="index"/>
        /// is taken modulo the pool size, so a caller can step deterministically
        /// through the pool (each fresh approach shows the next line) or pass a
        /// random value for a random pick. Never throws, never returns null.
        /// </summary>
        public static string LineFor(Archetype archetype, int index)
        {
            string[] pool = PoolFor(archetype);
            if (pool == null || pool.Length == 0) return string.Empty;
            int i = index % pool.Length;
            if (i < 0) i += pool.Length;
            return pool[i];
        }

        /// <summary>The number of distinct archetypes — handy for round-robin assignment.</summary>
        public static int ArchetypeCount => Enum.GetValues(typeof(Archetype)).Length;
    }
}
