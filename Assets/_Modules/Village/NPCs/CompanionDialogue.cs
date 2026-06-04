// =============================================================================
// CompanionDialogue — the per-hero STORY COMPANION line table (WO-227 / DEF-119).
// -----------------------------------------------------------------------------
// Each playable hero is shadowed in the village by a single STORY COMPANION — a
// trusted figure from that hero's story who trails them and speaks an intro line
// on first sight plus a small pool of contextual flavour. This is the data half
// of the StoryCompanion system (StoryCompanion.cs is the MonoBehaviour).
//
// It is the deliberate twin of TownsfolkDialogue: a plain static data class in
// DeNelle.Village, no assembly dependency, lines kept HERE as clearly-marked
// // LOCALIZE: constants (same convention TownsfolkDialogue uses — ambient NPC
// dialogue is kept out of the shared en.json, which other agents own). A future
// localization pass lifts these wholesale.
//
// ── The four companions (per owner, mapped by HeroClass) ─────────────────────
//   • Knight  → Grom    → a VETERAN MENTOR: grizzled, steady, soldier's plain talk.
//   • Ranger  → Sylas   → a SCOUT PARTNER: lean, watchful, reads the wild + the wind.
//   • Mage    → Thrain  → a SCHOLAR: precise, curious, speaks of the Heart's light.
//   • Cleric  → Elara   → an ACOLYTE: gentle, devout, tends faith and the wounded.
// (Hero names per HeroCatalog: Mage=Thrain, Knight=Grom, Ranger=Sylas, Cleric=Elara —
//  the companion is a separate figure who walks beside that named hero.)
//
// Voice. Per the narrative bible §8: short, grounded, hopeful at the core, no
// fake-archaic "thee/thou", the Hollow Ones read as grief not villains. The
// companion is warmer + more personal than an ambient townsperson — they address
// the Keeper as a known comrade, not a stranger.
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The per-hero story-companion line pools. Static, read-only data — picked
    /// from by <see cref="StoryCompanion"/> via <see cref="IntroFor"/> /
    /// <see cref="LineFor"/>. One companion per <see cref="HeroClass"/>.
    /// </summary>
    public static class CompanionDialogue
    {
        // ── Display names per companion ──────────────────────────────────────
        // LOCALIZE: shown as the speech-bubble attribution line.
        // The companion is named + titled so the Keeper reads them as a person.

        /// <summary>Returns the speech-bubble display name for a hero's companion.</summary>
        public static string NameFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return "Grom, Veteran of the Wall";   // mentor
                case HeroClass.Ranger: return "Sylas, Scout of the Reach";   // partner
                case HeroClass.Mage:   return "Thrain, Keeper of the Light"; // scholar
                case HeroClass.Cleric: return "Elara, Acolyte of the Heart"; // acolyte
                default:               return "Companion";
            }
        }

        // ── Knight → Grom, the veteran mentor ────────────────────────────────
        // LOCALIZE: grizzled, steady, soldier's plain talk. Proud of the Keeper.
        private static readonly string[] _knightIntro =
        {
            "Up at last, Keeper. I've held this wall through worse mornings — but glad of the company. Stay close, I'll teach you the rest.",
        };
        private static readonly string[] _knight =
        {
            "Shield up, weight on the back foot. The Hollow Ones hit like grief — hard, and all at once.",
            "I've buried good soldiers. I'd rather train one well than mourn another. So listen when I bark.",
            "The Heart holds the town. We hold the Heart. Simple as that, and just as heavy.",
            "Tired? Good. Tired means you're still standing. Rest when the horn's quiet.",
            "Walk the gates with me sometime. A wall you've touched is a wall you'll fight for.",
        };

        // ── Ranger → Sylas, the scout partner ────────────────────────────────
        // LOCALIZE: lean, watchful, reads the wild and the wind. Easy comradeship.
        private static readonly string[] _rangerIntro =
        {
            "There you are. I scouted the treeline at dawn — quiet, for now. Stick with me, Keeper, and I'll show you what the wild's saying.",
        };
        private static readonly string[] _ranger =
        {
            "Wind's turned from the east. The Hollow Ones move easier in a still dusk — keep an arrow nocked.",
            "Tracks by the south lane, deep and dragging. Not deer. We'll watch that gate tonight.",
            "Breathe before you loose. The shot lands where the breath ends, not where the eye wants.",
            "I learned the forest before I learned the bow. The land tells you everything, if you're quiet enough to hear it.",
            "Two of us see twice as far. Point me where you're going and I'll have eyes on the dark side of it.",
        };

        // ── Mage → Thrain, the scholar ───────────────────────────────────────
        // LOCALIZE: precise, curious, speaks of the Heart's light. Mentoring tone.
        private static readonly string[] _mageIntro =
        {
            "Ah — awake, and the light steadied the moment you stirred. Curious. Walk with me, Keeper; there is a great deal the Heart wishes you to learn.",
        };
        private static readonly string[] _mage =
        {
            "The violet light is not fire, nor faith — it is the Heart remembering it is loved. Feed it crystal and it remembers harder.",
            "Magic is patience given a shape. Rush the shape and it shatters. I have the scars to prove it.",
            "I have read every page in Elarion's stacks. The Heart still teaches me something each dawn.",
            "The Hollow Ones were once like us. That is the lesson the books omit — and the one that matters most.",
            "Watch how the wards drink the crystal. When they pulse amber, we are owed a hard night.",
        };

        // ── Cleric → Elara, the acolyte ──────────────────────────────────────
        // LOCALIZE: gentle, devout, tends faith and the wounded. Quiet courage.
        private static readonly string[] _clericIntro =
        {
            "You're awake — thank the Heart. I've prayed at your side since the wave broke. Lean on me, Keeper; no guardian walks this road alone.",
        };
        private static readonly string[] _cleric =
        {
            "I bound the wounded through the night. They live. That is the Heart's mercy working through tired hands.",
            "Faith isn't the absence of fear, Keeper. It's kneeling at the altar with your hands still shaking — and rising anyway.",
            "The Hollow Ones grieve. I light a candle for them too. Mercy costs nothing, and it might cost them everything.",
            "Come to the altar when the day is dark. The light there is small, but it has never once gone out.",
            "Rest a moment. I'll watch over you. Even a guardian is allowed to be tended.",
        };

        /// <summary>The intro / greeting pool for a hero's companion (usually one line).</summary>
        public static string[] IntroPoolFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return _knightIntro;
                case HeroClass.Ranger: return _rangerIntro;
                case HeroClass.Mage:   return _mageIntro;
                case HeroClass.Cleric: return _clericIntro;
                default:               return _knightIntro;
            }
        }

        /// <summary>The contextual flavour-line pool for a hero's companion.</summary>
        public static string[] PoolFor(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Knight: return _knight;
                case HeroClass.Ranger: return _ranger;
                case HeroClass.Mage:   return _mage;
                case HeroClass.Cleric: return _cleric;
                default:               return _knight;
            }
        }

        /// <summary>
        /// The companion's greeting / intro line — the first thing they say.
        /// Never throws, never returns null.
        /// </summary>
        public static string IntroFor(HeroClass hero)
        {
            string[] pool = IntroPoolFor(hero);
            return (pool != null && pool.Length > 0) ? pool[0] : string.Empty;
        }

        /// <summary>
        /// Picks a contextual line from <paramref name="hero"/>'s companion pool.
        /// <paramref name="index"/> is taken modulo the pool size so a caller can
        /// step deterministically through it. Never throws, never returns null.
        /// </summary>
        public static string LineFor(HeroClass hero, int index)
        {
            string[] pool = PoolFor(hero);
            if (pool == null || pool.Length == 0) return string.Empty;
            int i = index % pool.Length;
            if (i < 0) i += pool.Length;
            return pool[i];
        }
    }
}
