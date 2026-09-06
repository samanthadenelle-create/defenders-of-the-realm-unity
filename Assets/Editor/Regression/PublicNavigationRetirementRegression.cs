#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// RE-POINTED 2026-09-06 (WO-1421) - STRICTER, NEVER DELETED.
    /// <para>
    /// CURRENT RULING (owner 2026-09-06, verbatim): "under journey, please remove dungeons season in
    /// realm map as they should not be displayed there right now we don't have anything for seasons
    /// at all dungeons are very vague and ambiguous and there is no realm map other than the regular
    /// realm right now so please remove those". The Journey deck is TWO cards - Quests and Raids -
    /// and the Realm Map / Season / Dungeons doors are DELETED from it. The panels stay compiled and
    /// registered, deliberately orphaned, so a later re-add is one line. So the shape is now
    /// ABSENT FROM THE DECK, AND STILL ABSENT EVERYWHERE ELSE:
    /// </para>
    /// <list type="bullet">
    /// <item>PlayerDeckWorkspace.cs names PanelId.RealmMap and PanelId.BattlePass ZERO times in code
    /// and carries no `Route("Realm Map", ...)`, no `Route("Season", ...)` and no Dungeons card; the
    /// `case PlayerDeckKind.Journey:` block names none of those tokens and does not read
    /// DungeonStatusCatalog.</item>
    /// <item>PackStore.cs still never names PanelId.BattlePass / "Season Track" (the FREE band stays
    /// two tabs - NightMarketUiRegression).</item>
    /// <item>The Bag has NO Realm Map door: InventoryUIBuilder.cs never builds a RailMap entry and
    /// never opens PanelId.RealmMap; HeroInventoryController.cs never reads FeatureFlags.MapTab or
    /// calls OpenRealmMap; FeatureFlags.cs no longer declares MapTab; InventoryPaperDoll.cs has no
    /// Map header chip.</item>
    /// <item>The HUD has NO face or dock row for either: HudKitController.cs never names
    /// PanelId.RealmMap or PanelId.BattlePass in code (the bar's Map face is separately pinned dormant
    /// by HudActionBarRegression).</item>
    /// </list>
    /// <para>
    /// HISTORY, kept because it is why this suite has flipped twice and must never be deleted. Until
    /// 2026-09-04 the ruling was "Realm Map and Season Pass have no public navigation entry points"
    /// and this suite asserted their ABSENCE. PROGRAM_RAID_ECONOMY_2026-09-04 section 8 re-ruled
    /// Journey as five cards and section 12 item 4 said of this oracle: "re-points that oracle; it
    /// does not delete it", so on 2026-09-05 it flipped to PRESENT EXACTLY ONCE. WO-1421 flips it
    /// back to absence WITH the ruling, in the same commit as the code (CLAUDE.md section 15). The
    /// blocks below the deck section have never moved and are untouched by either flip.
    /// </para>
    /// RED-FIRST: on the pre-WO-1421 tree the Journey block still carries all three doors, so every
    /// deck assertion here fails. ONE-LINE MUTATIONS that red it on the fixed tree: re-add a
    /// `Route("Season", ...)` or a `Route("Realm Map", ...)` entry to the Journey arm; re-add a
    /// `Title = "Dungeons"` card; name PanelId.BattlePass anywhere in PlayerDeckWorkspace.cs; restore
    /// `public static bool MapTab` to FeatureFlags.cs.
    /// </summary>
    public static class PublicNavigationRetirementRegression
    {
        private const string DeckSrc = "Assets/_Modules/HUD/PlayerDeckWorkspace.cs";

        public static bool Run(out string result)
        {
            try
            {
                // -- The doors exist, once each, on the Journey deck ----------------------
                string deck = File.ReadAllText(DeckSrc);
                string journey = SliceJourneyCase(deck, out _);
                // WO-1421: counted on CODE (comment lines stripped) so a note that NAMES a removed
                // door stays legal documentation while a LINE that calls it is the regression. The
                // deck is the whole file here, not just the Journey block: the doors are gone from
                // the product, so there is no arm they are allowed to reappear in.
                string deckCode = StripLineComments(deck);
                string journeyCode = SliceJourneyCase(deckCode, out int journeyStart);
                string outsideJourney = deckCode.Remove(journeyStart, journeyCode.Length);
                AssertCountExactly(DeckSrc + " (code)", deckCode, "PanelId.BattlePass", 0);
                AssertCountExactly(DeckSrc + " (code)", deckCode, "Route(\"Realm Map\",", 0);
                AssertCountExactly(DeckSrc + " (code)", deckCode, "Route(\"Season\",", 0);
                AssertCountExactly(DeckSrc + " (code outside the Journey block)", outsideJourney, "PanelId.RealmMap", 0);
                AssertCountExactly(DeckSrc + " (code outside the Journey block)", outsideJourney, "PanelId.BattlePass", 0);
                // The three removed cards, asserted absent from the RAW Journey slice - comments
                // included. A surviving comment that still constructs the shape is drift, and the
                // WO-1421 note written in that arm deliberately names none of these tokens.
                AssertAbsentFrom(DeckSrc + " (Journey block)", journey,
                    "PanelId.RealmMap", "PanelId.BattlePass",
                    "Route(\"Realm Map\",", "Route(\"Season\",",
                    "Title = \"Dungeons\"", "DungeonStatusCatalog");
                // The three canon-strings purpose lines go with their cards. The KEYS stay declared
                // in HudStrings and present in both canon-strings copies, dormant - deleting one
                // breaks HudLabelFitRegression Case1_CanonParity. Only the READS are retired here.
                AssertAbsentFrom(DeckSrc + " (Journey block)", journey,
                    "HudStrings.Get(HudStrings.KeyJourneySeason)",
                    "HudStrings.Get(HudStrings.KeyJourneyRealmMap)",
                    "HudStrings.Get(HudStrings.KeyJourneyDungeons)");
                AssertAbsent(DeckSrc, "Season Track");

                // -- Nowhere else ---------------------------------------------------------
                AssertAbsent("Assets/_Modules/Wallet/PackStore.cs",
                    "PanelId.BattlePass", "Season Track");
                AssertAbsent("Assets/_Modules/Village/Hero/InventoryUIBuilder.cs",
                    "BuildRailEntry(content.transform, RailMap", "PanelId.RealmMap", "FeatureFlags.MapTab");
                AssertAbsent("Assets/_Modules/Village/Hero/HeroInventoryController.cs",
                    "FeatureFlags.MapTab", "OpenRealmMap(");
                AssertAbsent("Assets/_Modules/Core/FeatureFlags.cs",
                    "public static bool MapTab", "Get(\"maptab\"");
                AssertAbsent("Assets/_Modules/Village/Hero/InventoryPaperDoll.cs",
                    "BuildHeaderChip(headerRow.transform, \"Map\"");
                AssertAbsentInCode("Assets/_Modules/HUD/Kit/HudKitController.cs",
                    "PanelId.RealmMap", "PanelId.BattlePass");

                // -- The Realm deck's four illustrated destinations are untouched ---------
                AssertRealmCard("realm-store", "PanelId.RealmStore");
                AssertRealmCard("defense-report", "PanelId.DefenseReport");
                AssertRealmCard("monthly-ledger", "PanelId.MonthlyLedger");
                AssertRealmCard("game-guide", "PanelId.GameGuide");
                AssertRealmCardPackagingIsMasked();
                result = "Realm Map, Season and Dungeons have NO public door: the Journey deck is Quests + Raids " +
                         "(WO-1421, owner 2026-09-06); no Bag route, no MapTab flag, no HUD face; the panels stay " +
                         "registered and orphaned; four illustrated Realm destinations retain live routes and mask " +
                         "delivered packaging margins.";
                return true;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                return false;
            }
        }

        /// <summary>The text of `case PlayerDeckKind.Journey:` inside CardsFor, up to the next case
        /// or default. Anchored on CardsFor first so the SubtitleFor switch cannot satisfy it.
        /// <paramref name="start"/> is the slice's offset in <paramref name="deck"/>.</summary>
        private static string SliceJourneyCase(string deck, out int start)
        {
            int cardsFor = deck.IndexOf("List<Card> CardsFor(", StringComparison.Ordinal);
            if (cardsFor < 0)
                throw new InvalidOperationException(DeckSrc + " has no 'List<Card> CardsFor(' - the deck table was renamed");
            const string label = "case PlayerDeckKind.Journey:";
            int a = deck.IndexOf(label, cardsFor, StringComparison.Ordinal);
            if (a < 0)
                throw new InvalidOperationException(DeckSrc + " has no '" + label + "' block inside CardsFor");
            a += label.Length;
            int next = deck.IndexOf("case PlayerDeckKind.", a, StringComparison.Ordinal);
            int def = deck.IndexOf("default:", a, StringComparison.Ordinal);
            int end = next < 0 ? def : def < 0 ? next : Math.Min(next, def);
            start = a;
            return end < 0 ? deck.Substring(a) : deck.Substring(a, end - a);
        }

        private static void AssertCountExactly(string path, string source, string token, int expected)
        {
            int count = 0, at = 0;
            while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0) { count++; at += token.Length; }
            if (count != expected)
                throw new InvalidOperationException(
                    path + " names " + token + " " + count + " time(s), expected exactly " + expected +
                    " - Realm Map / Season / Dungeons have NO player-facing door (owner ruling 2026-09-06, WO-1421)");
        }

        /// <summary>AssertAbsent against an already-read slice rather than a file on disk, so the
        /// Journey block can be judged without re-reading the deck.</summary>
        private static void AssertAbsentFrom(string what, string source, params string[] forbidden)
        {
            foreach (string token in forbidden)
            {
                if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        "Retired public navigation returned in " + what + ": " + token +
                        " - the Journey deck is Quests + Raids (owner ruling 2026-09-06, WO-1421)");
            }
        }

        private static void AssertAbsent(string path, params string[] forbidden)
        {
            string source = File.ReadAllText(path);
            foreach (string token in forbidden)
            {
                if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        "Retired public navigation returned in " + path + ": " + token);
            }
        }

        /// <summary>Everything after a `//` on each line is dropped. Good enough for these files:
        /// none of the asserted tokens sits inside a string literal that also carries `//`.</summary>
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

        /// <summary>Like AssertAbsent, but comment lines are stripped first - a note that NAMES a
        /// retired route is documentation, a line that calls it is the regression.</summary>
        private static void AssertAbsentInCode(string path, params string[] forbidden)
        {
            string code = StripLineComments(File.ReadAllText(path));
            foreach (string token in forbidden)
            {
                if (code.IndexOf(token, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        "Retired public navigation returned in " + path + " (code, not comment): " + token);
            }
        }

        private static void AssertRealmCard(string artKey, string routeToken)
        {
            string asset = "Assets/Resources/UI/ElarionMedieval/cards/" + artKey + ".png";
            if (!File.Exists(asset))
                throw new InvalidOperationException("Realm destination art is missing: " + asset);
            string source = File.ReadAllText(DeckSrc);
            if (source.IndexOf("\"" + artKey + "\"", StringComparison.Ordinal) < 0 ||
                source.IndexOf(routeToken, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Realm destination lost art/route mapping: " + artKey + " -> " + routeToken);
        }

        private static void AssertRealmCardPackagingIsMasked()
        {
            string source = File.ReadAllText(DeckSrc);
            if (source.IndexOf("RectMask2D", StringComparison.Ordinal) < 0 ||
                source.IndexOf("IllustratedCardSurface", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Illustrated Realm cards no longer mask their baked packaging margins.");
        }
    }
}
#endif
