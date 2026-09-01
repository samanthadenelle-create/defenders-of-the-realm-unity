#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Owner ruling: Realm Map and Season Pass have no public navigation entry points.</summary>
    public static class PublicNavigationRetirementRegression
    {
        public static bool Run(out string result)
        {
            try
            {
                AssertAbsent("Assets/_Modules/HUD/PlayerDeckWorkspace.cs",
                    "Realm Map", "PanelId.BattlePass", "Season Track");
                AssertAbsent("Assets/_Modules/Wallet/PackStore.cs",
                    "PanelId.BattlePass", "Season Track");
                AssertAbsent("Assets/_Modules/Village/Hero/InventoryUIBuilder.cs",
                    "BuildRailEntry(content.transform, RailMap");
                AssertAbsent("Assets/_Modules/Village/Hero/InventoryPaperDoll.cs",
                    "BuildHeaderChip(headerRow.transform, \"Map\"");
                AssertRealmCard("realm-store", "PanelId.RealmStore");
                AssertRealmCard("defense-report", "PanelId.DefenseReport");
                AssertRealmCard("monthly-ledger", "PanelId.MonthlyLedger");
                AssertRealmCard("game-guide", "PanelId.GameGuide");
                AssertRealmCardPackagingIsMasked();
                result = "Realm Map and Season Pass remain absent; four illustrated Realm destinations retain live routes and mask delivered packaging margins.";
                return true;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                return false;
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

        private static void AssertRealmCard(string artKey, string routeToken)
        {
            string asset = "Assets/Resources/UI/ElarionMedieval/cards/" + artKey + ".png";
            if (!File.Exists(asset))
                throw new InvalidOperationException("Realm destination art is missing: " + asset);
            string source = File.ReadAllText("Assets/_Modules/HUD/PlayerDeckWorkspace.cs");
            if (source.IndexOf("\"" + artKey + "\"", StringComparison.Ordinal) < 0 ||
                source.IndexOf(routeToken, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Realm destination lost art/route mapping: " + artKey + " -> " + routeToken);
        }

        private static void AssertRealmCardPackagingIsMasked()
        {
            string source = File.ReadAllText("Assets/_Modules/HUD/PlayerDeckWorkspace.cs");
            if (source.IndexOf("RectMask2D", StringComparison.Ordinal) < 0 ||
                source.IndexOf("IllustratedCardSurface", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "Illustrated Realm cards no longer mask their baked packaging margins.");
        }
    }
}
#endif
