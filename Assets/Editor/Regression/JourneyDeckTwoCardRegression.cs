#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// WO-1421 (owner ruling 2026-09-06, verbatim): "under journey, please remove dungeons season in
    /// realm map as they should not be displayed there right now we don't have anything for seasons
    /// at all dungeons are very vague and ambiguous and there is no realm map other than the regular
    /// realm right now so please remove those".
    /// <para>
    /// The Journey deck is TWO cards - Quests and Raids. This suite is the source-scoped oracle for
    /// that shape. It pins FOUR different things, and the fourth is the one that is easy to get
    /// wrong: the three canon-strings KEYS stay DECLARED AND DORMANT. Deleting a key from HudStrings
    /// (the tempting "tidy up") breaks HudLabelFitRegression Case1_CanonParity, which iterates
    /// HudStrings.AllKeys and requires every key in BOTH canon-strings copies. Removing a card is not
    /// removing its string.
    /// </para>
    /// <para>
    /// Marker: JOURNEY_DECK_OK on pass, JOURNEY_DECK_FAIL [case-tag] ... on failure. Every case
    /// carries a one-line REVERT RECIPE so the CLI can drive a RED proof and restore. A missing
    /// fixture (renamed method, renamed enum arm, unreadable file) is a FAIL that names itself -
    /// there is no path through this suite that passes on nothing.
    /// </para>
    /// </summary>
    public static class JourneyDeckTwoCardRegression
    {
        private const string DeckSrc = "Assets/_Modules/HUD/PlayerDeckWorkspace.cs";
        private const string StringsSrc = "Assets/_Modules/Core/UI/HudStrings.cs";

        public static bool Run(out string result)
        {
            try
            {
                string deck = ReadFixture(DeckSrc);
                string strings = ReadFixture(StringsSrc);
                string journeyRaw = SliceJourneyCase(deck);
                string journeyCode = SliceJourneyCase(StripLineComments(deck));

                // ORDERING NOTE, load-bearing: the named-token cases run BEFORE the count. Case 1
                // fires on ANY third construction, so if it ran first every "re-add that card"
                // revert recipe would trip case 1 and cases 2-4 could never be proven RED
                // individually. Specific first, then the count as the catch-all.

                // -- 2 --------------------------------------------------------------------
                // REVERT RECIPE: insert `new Card { Title = "Dungeons", Concept = "dungeon" },` as the second entry of the Journey arm's card list.
                RequireAbsent(journeyRaw, "Title = \"Dungeons\"", "no-dungeons-card",
                    "the Dungeons card opened the realm map with a context argument, never a dungeon; " +
                    "the world portal is the real door and it is untouched");

                // -- 3 --------------------------------------------------------------------
                // REVERT RECIPE: insert `Route("Realm Map", "x", "map", PanelId.RealmMap),` as the second entry of the Journey arm's card list.
                RequireAbsent(journeyRaw, "Route(\"Realm Map\",", "no-realm-map-card",
                    "there is no realm beyond the regular realm yet (owner 2026-09-06)");

                // -- 4 --------------------------------------------------------------------
                // REVERT RECIPE: insert `Route("Season", "x", "season", PanelId.BattlePass),` as the second entry of the Journey arm's card list.
                RequireAbsent(journeyRaw, "Route(\"Season\",", "no-season-card",
                    "there is no season content at all yet (owner 2026-09-06)");

                // -- 1 --------------------------------------------------------------------
                // The catch-all: a THIRD card of any name, or a lost one. Runs after 2-4 so those
                // three keep their own tags.
                // REVERT RECIPE: append `, Route("Quest Log", "x", "quest", PanelId.RumorBoard)` as a THIRD entry of the Journey arm's card list.
                int constructions = CountToken(journeyCode, "Title =") + CountToken(journeyCode, "Route(");
                if (constructions != 2)
                    throw new InvalidOperationException(
                        "[journey-two-cards] the Journey arm builds " + constructions + " card(s) " +
                        "(counted as `Title =` + `Route(` on comment-stripped code), expected exactly 2 - " +
                        "Quests and Raids. Owner ruling 2026-09-06.");

                // -- 5 --------------------------------------------------------------------
                // REVERT RECIPE: restore the SubtitleFor Journey literal to "Quests, raids, dungeons, the realm map, and the season.".
                // Asserted in BOTH directions so it cannot pass on an empty string: the line must be
                // non-empty AND must not name any of the three removed destinations.
                string subtitle = JourneySubtitleLiteral(deck);
                if (subtitle.Trim().Length == 0)
                    throw new InvalidOperationException(
                        "[subtitle-names-two] the Journey deck subtitle is empty - a blank purpose line is " +
                        "not a pass, it is a screen with no sentence on it");
                foreach (string banned in new[] { "dungeon", "season", "realm map" })
                {
                    if (subtitle.IndexOf(banned, StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException(
                            "[subtitle-names-two] the Journey deck subtitle still promises '" + banned +
                            "': \"" + subtitle + "\" - the deck paints two cards, so the line names two");
                }
                RequireAbsent(deck, "Quests, raids, dungeons", "subtitle-names-two",
                    "the retired five-card subtitle literal is still in the file");

                // -- 6 --------------------------------------------------------------------
                // REVERT RECIPE: paste `private static bool AnyDungeonOpen() { return false; }` back into PlayerDeckWorkspace.cs.
                RequireAbsent(deck, "AnyDungeonOpen", "dungeon-helpers-gone",
                    "the helper existed only to decide whether the removed card was available");
                RequireAbsent(deck, "DescribeDungeonDoors", "dungeon-helpers-gone",
                    "the helper existed only to narrate the removed card's Open lambda");

                // -- 7 --------------------------------------------------------------------
                // REVERT RECIPE: in the Raids card, replace `TraceJourneySubtitle("Raids", journey.RaidsSubtitle)` with `journey.RaidsSubtitle`.
                // (Chosen over deleting the whole card so the construction count stays 2 and this
                // case, not case 1, is the one that fires. JourneyDeckSubtitleRegression also reds
                // on this mutation - a different suite, and expected.)
                RequirePresent(journeyRaw, "TraceJourneySubtitle(\"Quests\"", "quests-and-raids-survive",
                    "removing three cards must not take the Quests card with them");
                RequirePresent(journeyRaw, "TraceJourneySubtitle(\"Raids\"", "quests-and-raids-survive",
                    "removing three cards must not take the Raids card with them");

                // -- 8 --------------------------------------------------------------------
                // REVERT RECIPE: delete `KeyJourneySeason,` from the HudStrings.AllKeys initializer (leave the const declared).
                // (The initializer, not the const: deleting the const leaves AllKeys naming a
                // missing symbol, which fails COMPILE_GATE so the suite never runs and no RED is
                // recordable. This mutation is compile-safe and trips the AllKeys half of case 8.)
                // THIS CASE EXISTS BECAUSE THE TEMPTING TIDY-UP IS EXACTLY WHAT BREAKS
                // HudLabelFitRegression Case1_CanonParity. The cards are gone; the keys stay dormant
                // in HudStrings AND in both canon-strings copies, or parity fails.
                foreach (string key in new[] { "KeyJourneyDungeons", "KeyJourneyRealmMap", "KeyJourneySeason" })
                {
                    RequirePresent(strings, "public const string " + key + " =", "canon-keys-dormant-not-deleted",
                        "the key must stay DECLARED even though no card reads it");
                }
                string allKeys = SliceAllKeys(strings);
                foreach (string key in new[] { "KeyJourneyDungeons", "KeyJourneyRealmMap", "KeyJourneySeason" })
                {
                    RequirePresent(allKeys, key, "canon-keys-dormant-not-deleted",
                        "HudStrings.AllKeys must still list the key - HudLabelFitRegression Case1_CanonParity " +
                        "iterates AllKeys and requires each key in BOTH canon-strings copies");
                }

                result = "JOURNEY_DECK_OK Journey deck = 2 cards (Quests + Raids); Dungeons / Realm Map / " +
                         "Season cards, their two dungeon-door helpers and the five-card subtitle are gone; " +
                         "subtitle=\"" + subtitle + "\"; the three canon keys stay declared and in AllKeys.";
                return true;
            }
            catch (Exception ex)
            {
                result = "JOURNEY_DECK_FAIL " + ex.Message;
                return false;
            }
        }

        // ---- fixtures -----------------------------------------------------------------

        private static string ReadFixture(string path)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "[fixture] " + path + " is missing - this suite is measuring nothing");
            string text = File.ReadAllText(path);
            if (text.Length == 0)
                throw new InvalidOperationException("[fixture] " + path + " is empty");
            return text;
        }

        /// <summary>The text of `case PlayerDeckKind.Journey:` inside CardsFor, up to the next case
        /// or default. Anchored on CardsFor FIRST so the SubtitleFor switch - which switches on the
        /// same enum earlier in the file - cannot satisfy it with a block that has no card in it.</summary>
        private static string SliceJourneyCase(string deck)
        {
            int cardsFor = deck.IndexOf("List<Card> CardsFor(", StringComparison.Ordinal);
            if (cardsFor < 0)
                throw new InvalidOperationException(
                    "[fixture] " + DeckSrc + " has no 'List<Card> CardsFor(' - the deck card table was renamed");
            const string label = "case PlayerDeckKind.Journey:";
            int a = deck.IndexOf(label, cardsFor, StringComparison.Ordinal);
            if (a < 0)
                throw new InvalidOperationException(
                    "[fixture] " + DeckSrc + " has no '" + label + "' block inside CardsFor");
            a += label.Length;
            int next = deck.IndexOf("case PlayerDeckKind.", a, StringComparison.Ordinal);
            int def = deck.IndexOf("default:", a, StringComparison.Ordinal);
            int end = next < 0 ? def : def < 0 ? next : Math.Min(next, def);
            return end < 0 ? deck.Substring(a) : deck.Substring(a, end - a);
        }

        /// <summary>The first quoted literal returned by the Journey arm of SubtitleFor. Anchored on
        /// SubtitleFor and required to land BEFORE RenderPage, so if the method is renamed or the arm
        /// moves the case fails by name instead of measuring some other string.</summary>
        private static string JourneySubtitleLiteral(string deck)
        {
            int at = deck.IndexOf("SubtitleFor(", StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    "[subtitle-names-two] " + DeckSrc + " has no 'SubtitleFor(' - the deck subtitle source was renamed");
            int label = deck.IndexOf("case PlayerDeckKind.Journey:", at, StringComparison.Ordinal);
            if (label < 0)
                throw new InvalidOperationException(
                    "[subtitle-names-two] SubtitleFor has no 'case PlayerDeckKind.Journey:' arm - the Journey " +
                    "subtitle is no longer labelled and this case cannot silently pass on the Realm or Hero line");
            int render = deck.IndexOf("RenderPage(", at, StringComparison.Ordinal);
            if (render >= 0 && label > render)
                throw new InvalidOperationException(
                    "[subtitle-names-two] the 'case PlayerDeckKind.Journey:' found after SubtitleFor sits past " +
                    "RenderPage - the subtitle switch moved and this case would be reading the card table");
            int open = deck.IndexOf('"', label);
            if (open < 0)
                throw new InvalidOperationException(
                    "[subtitle-names-two] no quoted literal follows the Journey arm of SubtitleFor");
            int close = deck.IndexOf('"', open + 1);
            if (close < 0)
                throw new InvalidOperationException(
                    "[subtitle-names-two] the Journey subtitle literal is unterminated");
            return deck.Substring(open + 1, close - open - 1);
        }

        /// <summary>The body of the HudStrings.AllKeys initializer.</summary>
        private static string SliceAllKeys(string strings)
        {
            int at = strings.IndexOf("AllKeys", StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    "[canon-keys-dormant-not-deleted] " + StringsSrc + " has no AllKeys array - the canon-parity " +
                    "oracle's own fixture is gone");
            int open = strings.IndexOf('{', at);
            int close = open < 0 ? -1 : strings.IndexOf("};", open, StringComparison.Ordinal);
            if (open < 0 || close < 0)
                throw new InvalidOperationException(
                    "[canon-keys-dormant-not-deleted] HudStrings.AllKeys has no readable initializer body");
            return strings.Substring(open, close - open);
        }

        // ---- assertions ---------------------------------------------------------------

        private static int CountToken(string source, string token)
        {
            int count = 0, at = 0;
            while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0) { count++; at += token.Length; }
            return count;
        }

        private static void RequireAbsent(string source, string token, string tag, string why)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException(
                    "[" + tag + "] '" + token + "' is back - " + why + " (owner ruling 2026-09-06, WO-1421)");
        }

        private static void RequirePresent(string source, string token, string tag, string why)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "[" + tag + "] '" + token + "' is MISSING - " + why + " (WO-1421)");
        }

        /// <summary>Everything after a `//` on each line is dropped, so a note that NAMES a removed
        /// card is documentation while a line that CONSTRUCTS one is the regression.</summary>
        private static string StripLineComments(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (string raw in source.Split('\n'))
            {
                string line = raw;
                int slash = line.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0) line = line.Substring(0, slash);
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }
    }
}
#endif
