// UI-001: independent source oracle for the landscape Night Market and its persistent HUD door.
// Presentation only. It deliberately does not inspect or alter PurchaseGate/payment transport.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class NightMarketUiRegression
    {
        private const string StoreRel = "/_Modules/Wallet/PackStore.cs";
        private const string HudRel = "/_Modules/HUD/Kit/HudKitController.cs";

        [MenuItem("Tools/Regression/UI/Night Market Landscape")]
        public static void RunMenu()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("--- NIGHT MARKET UI-001 ---\n");
            string store = Read(Application.dataPath + StoreRel, failures);
            string hud = Read(Application.dataPath + HudRel, failures);

            // Independent thresholds: these are acceptance bounds, not values imported from the
            // production constants. Shrinking the implementation therefore makes this go red.
            var panelMin = Vector(store, "PanelMin", failures);
            var panelMax = Vector(store, "PanelMax", failures);
            if (panelMax.x - panelMin.x < 0.80f)
                failures.Add($"Night Market panel uses only {(panelMax.x - panelMin.x):P0} of landscape width (minimum 80%).");

            var spotMin = Vector(store, "SpotlightMin", failures);
            var spotMax = Vector(store, "SpotlightMax", failures);
            var shelfMin = Vector(store, "ShelfMin", failures);
            var shelfMax = Vector(store, "ShelfMax", failures);
            if (spotMax.x >= shelfMin.x) failures.Add("spotlight overlaps the merchandise shelf.");
            if (shelfMax.x - shelfMin.x < 0.50f) failures.Add("shelf owns less than half the body width.");
            if (Math.Min(spotMax.y - spotMin.y, shelfMax.y - shelfMin.y) < 0.65f)
                failures.Add("store body leaves too much vertical space unused.");

            float cardHeight = Scalar(store, "CardHeightPx", failures);
            float cardsPerRow = Scalar(store, "CardsPerRow", failures);
            if (Math.Abs(cardsPerRow - 2f) > 0.01f) failures.Add("priced shelf is not two-up.");
            if (cardHeight < 240f) failures.Add($"card height {cardHeight:0}px is below the 240px readability floor.");

            string freeBand = Slice(store, "private void BuildFreeBand", "private void BuildFreeDoor", failures);
            if (Regex.Matches(freeBand, @"BuildCardRow\(\)").Count != 2 ||
                Regex.Matches(freeBand, @"BuildFreeDoor\(").Count != 2 ||
                !freeBand.Contains("Spacer(secondStrip)"))
                failures.Add("FREE TONIGHT is not composed as two complete two-up rows (redeem + two doors + spacer).");

            var redeem = AnchorsAfter(freeBand, "entryLabel,", failures);
            var freeDoor = AnchorsAfter(store, "slot.transform, \"OPEN\"", failures);
            float paddedCardHeight = cardHeight - 6f; // BuildCardRow top+bottom padding.
            if ((redeem.max.y - redeem.min.y) * paddedCardHeight < 112f)
                failures.Add("Redeem control is authored below the 112px touch floor and will inflate.");
            if ((freeDoor.max.y - freeDoor.min.y) * paddedCardHeight < 112f)
                failures.Add("Free-door control is authored below the 112px touch floor and will inflate.");

            var nameRect = AnchorsAfter(store, "pack.Name, 24", failures);
            var stateRect = AnchorsAfter(store, "MakeText(card, flag, 15", failures);
            if (nameRect.max.y > stateRect.min.y)
                failures.Add("pack name and state/badge lanes overlap vertically.");

            var ctaMin = Vector(store, "ctaMin", failures);
            var ctaMax = Vector(store, "ctaMax", failures);
            const float referenceHeight = 1080f;
            const float conservativeBodyShare = 0.80f; // modal chrome/status reserve
            float ctaPx = referenceHeight * (panelMax.y - panelMin.y) * conservativeBodyShare *
                          (spotMax.y - spotMin.y) * (ctaMax.y - ctaMin.y);
            if (ctaPx < 112f)
                failures.Add($"spotlight CTA derives to only {ctaPx:0}px at 2340x1080 after chrome reserve.");
            Require(store, "pack.Name, 24", "pack names fell below the device readability floor", failures);
            Require(store, "DescribeContents(pack), 18", "pack contents fell below the device readability floor", failures);
            Require(store, "pack.AmountLabel(_defaultCurrency), 26", "pack price fell below the device readability floor", failures);
            if (Regex.Matches(store, "KeyWordmark").Count != 1) // modal title only; no body duplicate
                failures.Add("wordmark occurrence count drifted; confirm the visible title is rendered exactly once.");
            Require(store, "FitSingleLine(redeemLabel, 20f, 28f)", "Redeem action has no explicit readable single-line guard", failures);
            Require(store, "StoreLegalFooter", "shared legal/footer component is absent", failures);

            Require(hud, "RealmStoreHudButton", "dedicated HUD Store face is absent", failures);
            Require(hud, "sizeDelta = new Vector2(dockTabPx, dockTabPx)", "HUD Store face is not pinned to the touch floor", failures);
            Require(hud, "SafeAreaInset.EdgeMarginPx", "HUD Store face is not seated inside the common safe-area margin", failures);
            Require(hud, "PanelRouter.Open(PanelId.RealmStore)", "HUD Store does not route to the existing Realm Store door", failures);
            if (Regex.Matches(hud, @"PanelRouter\.Open\(PanelId\.RealmStore\)").Count != 1)
                failures.Add("HUD declares more than one Realm Store routing authority.");
            if (hud.Contains("AddDockTab(_slideDock.panel, 5, \"Realm Store\""))
                failures.Add("Realm Store still occupies a drawer row in addition to the persistent face.");
            Require(hud, "Register(\"chatDock\"", "Store/Menu column is not posture-owned with the dock", failures);

            if (failures.Count > 0)
            {
                reason = "NIGHT_MARKET_UI_FAIL\n - " + string.Join("\n - ", failures);
                return false;
            }

            log.Append("NIGHT_MARKET_UI_OK — landscape body, one visible title, readable cards, persistent single-authority HUD door");
            reason = log.ToString();
            return true;
        }

        private static string Read(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing source: " + path);
            return string.Empty;
        }

        private static Vector2 Vector(string source, string name, List<string> failures)
        {
            var m = Regex.Match(source, name + @"\s*=\s*new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y)) return new Vector2(x, y);
            failures.Add("could not parse independent layout value " + name);
            return Vector2.zero;
        }

        private static float Scalar(string source, string name, List<string> failures)
        {
            var m = Regex.Match(source, name + @"\s*=\s*([-0-9.]+)f?");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value)) return value;
            failures.Add("could not parse independent layout scalar " + name);
            return 0f;
        }

        private static string Slice(string source, string start, string end, List<string> failures)
        {
            int a = source.IndexOf(start, StringComparison.Ordinal);
            int b = a >= 0 ? source.IndexOf(end, a + start.Length, StringComparison.Ordinal) : -1;
            if (a >= 0 && b > a) return source.Substring(a, b - a);
            failures.Add("could not isolate source block " + start);
            return string.Empty;
        }

        private static (Vector2 min, Vector2 max) AnchorsAfter(string source, string marker, List<string> failures)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                var matches = Regex.Matches(source.Substring(start, Math.Min(800, source.Length - start)),
                    @"new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)");
                if (matches.Count >= 2)
                {
                    float Parse(Group g) => float.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture);
                    return (new Vector2(Parse(matches[0].Groups[1]), Parse(matches[0].Groups[2])),
                            new Vector2(Parse(matches[1].Groups[1]), Parse(matches[1].Groups[2])));
                }
            }
            failures.Add("could not derive anchored rect after " + marker);
            return (Vector2.zero, Vector2.zero);
        }

        private static void Require(string source, string marker, string failure, List<string> failures)
        {
            if (source.IndexOf(marker, StringComparison.Ordinal) < 0) failures.Add(failure);
        }
    }
}
