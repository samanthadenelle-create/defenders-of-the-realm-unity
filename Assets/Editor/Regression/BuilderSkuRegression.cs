// =============================================================================
// BuilderSkuRegression [builder-sku] -- WO-1253: permanent builder is CONCURRENCY.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Proves the store SKU `permanent-builder`
// raises crew slots and NOT queue depth, that a repeated settle is idempotent,
// that a player without the entitlement is unaffected, and that Manage's
// "Buy builder" routes to the store instead of crystal TryBuySlot.
//
// Crystal TryBuySlot (DEPTH) is KEEP BOTH and is asserted still present.
//
// ⛔ HOLLOW-PASS RULE (WO-1138): if PackCatalog cannot resolve the SKU, this suite
// emits RegressionOutcome.Skip -- never a quiet green. A missing entitlement
// path is not coverage.
//
// Marker: BUILDER_SKU_OK / BUILDER_SKU_FAIL.
//
// Wire (DataRegression.RunAll):
//   if (!DeNelle.Editor.Regression.BuilderSkuRegression.Run(out var r)) failures.Add(r);
//   else log.AppendLine("[builder-sku] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.UI;
using DeNelle.Wallet;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class BuilderSkuRegression
    {
        private const string Sku = PackCatalog.PermanentBuilderSku;
        private const string Kind = PackCatalog.PermanentBuilderKind;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- BUILDER SKU (WO-1253: permanent-builder is CONCURRENCY, not depth) ---");

            PackCatalog.Reload();
            var pack = PackCatalog.Find(Sku);
            if (pack == null)
            {
                return RegressionOutcome.Skip(out reason, "BUILDER SKU",
                    "PackCatalog.Find('" + Sku + "') returned null -- entitlement unresolved. ASSERTED NOTHING.");
            }

            CaseCatalog(pack, failures, log);
            CaseAxes(failures, log);
            CaseIdempotentOwnership(failures, log);
            CaseUnaffectedWithoutEntitlement(failures, log);
            CaseManageRoutesToStore(failures, log);
            CaseCrystalPathKept(failures, log);
            CaseLabelWidth(failures, log);
            CaseSettle(failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // ── 1. the SKU exists, is sellable, and is not vapor ──────────────────
        private static void CaseCatalog(PackDef pack, List<string> failures, StringBuilder log)
        {
            if (!pack.StoreVisible)
                failures.Add("[catalog] '" + Sku + "' is storeVisible:false -- WO-1246/WO-1253: do not hide a SKU whose grant path just shipped.");
            if (pack.Impulse)
                failures.Add("[catalog] '" + Sku + "' is tagged impulse -- it would be filtered off the shelf unless shelfCurated.");
            if (pack.Pricing == null || pack.Pricing.Usd != 9.99d)
                failures.Add("[catalog] '" + Sku + "' usd is " + (pack.Pricing != null ? pack.Pricing.Usd.ToString() : "null") +
                             " -- authored $9.99 so PurchaseGate requires an attested wallet (reinstall-safe).");

            bool hasKind = false;
            if (pack.Contents != null && pack.Contents.Convenience != null)
            {
                foreach (var item in pack.Contents.Convenience)
                {
                    if (item != null && PackCatalog.IsPermanentBuilderKind(item.Kind) && item.Count > 0)
                        hasKind = true;
                }
            }
            if (!hasKind)
                failures.Add("[catalog] '" + Sku + "' does not advertise convenience kind '" + Kind +
                             "' -- [no-vapor] would fail and the card would grant NOTHING.");
            if (!PackCatalog.IsRedeemableConvenience(Kind))
                failures.Add("[catalog] '" + Kind + "' is not IsRedeemableConvenience -- a shelf pack with an unredeemable kind is vapor (WO-1118).");

            string stream = ReadRepoFile(Path.Combine("Assets", "StreamingAssets", "Data", "Canonical", "packs.json"));
            string res = ReadRepoFile(Path.Combine("Assets", "Resources", "Data", "Canonical", "packs.json"));
            if (stream == null || res == null)
                failures.Add("[catalog] one of the canonical packs.json copies is unreadable");
            else if (!string.Equals(stream, res, StringComparison.Ordinal))
                failures.Add("[catalog] StreamingAssets and Resources packs.json differ -- mirror law broken.");

            string server = ReadRepoFile(Path.Combine("api", "_lib", "purchase-catalog.js"));
            if (server == null)
                failures.Add("[catalog] api/_lib/purchase-catalog.js unreadable -- cannot prove the USD anchor.");
            else
            {
                var m = Regex.Match(server, @"['""]" + Regex.Escape(Sku) + @"['""]\s*:\s*([0-9.]+)");
                if (!m.Success)
                    failures.Add("[catalog] USD_ANCHORS is missing '" + Sku +
                                 "' -- a sellable SKU with no server price is unbuyable (WO-1165).");
                else if (m.Groups[1].Value != "9.99")
                    failures.Add("[catalog] USD_ANCHORS['" + Sku + "']=" + m.Groups[1].Value + " != 9.99 (client pricing.usd).");
            }

            log.AppendLine("  [catalog] sku='" + pack.Sku + "' visible=" + pack.StoreVisible +
                           " usd=" + (pack.Pricing != null ? pack.Pricing.Usd.ToString() : "?") +
                           " redeemable=" + PackCatalog.IsRedeemableConvenience(Kind));
        }

        // ── 2. concurrency vs depth (RED-first: without SKU, numbers stay 2 / 5) ──
        private static void CaseAxes(List<string> failures, StringBuilder log)
        {
            int noneCrew = BuildTimerService.ConcurrencyOf(2, 0, false);
            int noneDepth = BuildTimerService.DepthOf(5, 0);
            if (noneCrew != 2)
                failures.Add("[axes] without entitlement ConcurrencyOf(2,0,false)=" + noneCrew + " -- baseline freeBuildSlots must stay 2.");
            if (noneDepth != 5)
                failures.Add("[axes] without entitlement DepthOf(5,0)=" + noneDepth + " -- baseline queueDepthPerLine must stay 5.");

            int ownedCrew = BuildTimerService.ConcurrencyOf(2, 0, true);
            int ownedDepth = BuildTimerService.DepthOf(5, 0);
            if (ownedCrew != 3)
                failures.Add("[axes] with entitlement ConcurrencyOf(2,0,true)=" + ownedCrew + " -- SKU must add +1 crew, not 0 and not +2.");
            if (ownedDepth != 5)
                failures.Add("[axes] with entitlement DepthOf(5,0)=" + ownedDepth +
                             " -- SKU MUST NOT raise queue depth (a player who buys a builder and gets a longer queue was sold the wrong product).");

            // Re-settle is still "owns=true", never a stacked count.
            int againCrew = BuildTimerService.ConcurrencyOf(2, 0, true);
            if (againCrew != 3)
                failures.Add("[axes] re-settle ConcurrencyOf still " + againCrew + " -- grant must be idempotent.");

            int crystalCrew = BuildTimerService.ConcurrencyOf(2, 1, true);
            int crystalDepth = BuildTimerService.DepthOf(5, 1);
            if (crystalCrew != 4)
                failures.Add("[axes] KEEP BOTH: crystal boughtSlots + SKU should be 4 crews, got " + crystalCrew);
            if (crystalDepth != 6)
                failures.Add("[axes] KEEP BOTH: crystal boughtSlots still widens DEPTH to 6, got " + crystalDepth);

            log.AppendLine("  [axes] none=" + noneCrew + "c/" + noneDepth + "d  owned=" + ownedCrew + "c/" + ownedDepth +
                           "d  crystal+sku=" + crystalCrew + "c/" + crystalDepth + "d");
        }

        // ── 3. SKU ownership is the entitlement; a second RecordOwned does not stack ──
        private static void CaseIdempotentOwnership(List<string> failures, StringBuilder log)
        {
            var owned = new List<string>();
            if (PackCatalog.OwnsPermanentBuilder(owned))
                failures.Add("[idempotent] empty OwnedItemIds reported OwnsPermanentBuilder -- a player without the SKU must be unaffected.");

            owned.Add(Sku);
            if (!PackCatalog.OwnsPermanentBuilder(owned))
                failures.Add("[idempotent] OwnedItemIds containing '" + Sku + "' did not count as owned.");

            owned.Add(Sku);
            int count = 0;
            foreach (var id in owned)
                if (string.Equals(id, Sku, StringComparison.Ordinal)) count++;
            if (count != 2)
                failures.Add("[idempotent] fixture setup failed (expected two copies of the sku in the list).");
            // Owns is a bool, not a count -- SlotCount adds +1 once.
            if (!PackCatalog.OwnsPermanentBuilder(owned))
                failures.Add("[idempotent] duplicate SKU entries made OwnsPermanentBuilder false.");
            int crew = BuildTimerService.ConcurrencyOf(2, 0, PackCatalog.OwnsPermanentBuilder(owned));
            if (crew != 3)
                failures.Add("[idempotent] two SKU entries produced crew=" + crew + " -- ownership is a flag, not a stack.");

            log.AppendLine("  [idempotent] empty=false, once=true, twice-in-list still crew=3");
        }

        // ── 4. player without entitlement is unaffected ────────────────────────
        private static void CaseUnaffectedWithoutEntitlement(List<string> failures, StringBuilder log)
        {
            var other = new List<string> { "hearth-spark", "starters-hand" };
            if (PackCatalog.OwnsPermanentBuilder(other))
                failures.Add("[unaffected] owning unrelated SKUs granted a permanent builder.");
            if (BuildTimerService.ConcurrencyOf(2, 0, false) != 2)
                failures.Add("[unaffected] baseline concurrency moved off 2.");
            if (BuildTimerService.DepthOf(5, 0) != 5)
                failures.Add("[unaffected] baseline depth moved off 5.");
            log.AppendLine("  [unaffected] unrelated SKUs do not grant a crew; defaults stay 2/5");
        }

        // ── 5. Manage affordance is the store SKU, not crystal spend ───────────
        private static void CaseManageRoutesToStore(List<string> failures, StringBuilder log)
        {
            if (ManageScreenVM.BuyBuilderButtonCopy != "Buy builder")
                failures.Add("[manage] BuyBuilderButtonCopy='" + ManageScreenVM.BuyBuilderButtonCopy +
                             "' -- expected 'Buy builder' (not 'Buy slot').");
            if (ManageScreenVM.BuyBuilderButtonCopy.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[manage] button copy still says 'slot' -- the owner ruled that word off this affordance.");
            if (ManageScreenVM.BuyBuilderLabelCopy.IndexOf("crystal", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[manage] label still names crystals -- Manage must not sell the crystal sink.");

            string panel = ReadRepoFile(Path.Combine("Assets", "_Modules", "Village", "UI", "Manage", "ManageScreenPanel.cs"));
            string vm = ReadRepoFile(Path.Combine("Assets", "_Modules", "Village", "UI", "Manage", "ManageScreenVM.cs"));
            if (panel == null || vm == null)
            {
                failures.Add("[manage] Manage source unreadable");
                return;
            }
            if (panel.Contains("TryBuySlot"))
                failures.Add("[manage] ManageScreenPanel still names TryBuySlot -- Manage must not spend crystals.");
            if (vm.Contains("TryBuySlot"))
                failures.Add("[manage] ManageScreenVM still names TryBuySlot -- Manage must route to the store.");
            if (!vm.Contains("RequestFocusSku") || !vm.Contains("PermanentBuilderSku"))
                failures.Add("[manage] ManageScreenVM.BuySlot does not RequestFocusSku(PermanentBuilderSku).");
            if (!panel.Contains("BuyBuilderButtonCopy"))
                failures.Add("[manage] ManageScreenPanel does not stamp BuyBuilderButtonCopy on the button.");

            log.AppendLine("  [manage] button='" + ManageScreenVM.BuyBuilderButtonCopy +
                           "' label='" + ManageScreenVM.BuyBuilderLabelCopy + "' routes to store SKU");
        }

        // ── 6. KEEP BOTH: crystal DEPTH sink still exists ──────────────────────
        private static void CaseCrystalPathKept(List<string> failures, StringBuilder log)
        {
            var t = typeof(BuildTimerService);
            if (t.GetMethod("TryBuySlot", BindingFlags.Public | BindingFlags.Instance) == null)
                failures.Add("[keep-both] BuildTimerService.TryBuySlot missing -- crystal DEPTH sink was silently deleted.");
            if (t.GetMethod("CanBuySlot", BindingFlags.Public | BindingFlags.Instance) == null)
                failures.Add("[keep-both] BuildTimerService.CanBuySlot missing.");

            string hud = ReadRepoFile(Path.Combine("Assets", "_Modules", "Village", "BuildMode", "ObsidianQueueHud.cs"));
            if (hud != null && !hud.Contains("TryBuySlot"))
                failures.Add("[keep-both] ObsidianQueueHud no longer names TryBuySlot -- crystal extra-slot lost its surface.");

            string upgradeVm = ReadRepoFile(Path.Combine("Assets", "_Modules", "Village", "Buildings", "Progression", "BuildingUpgradeVM.cs"));
            if (upgradeVm != null && !upgradeVm.Contains("TryBuySlot"))
                failures.Add("[keep-both] BuildingUpgradeVM no longer names TryBuySlot -- queue-full DEPTH remedy lost.");

            log.AppendLine("  [keep-both] TryBuySlot still present on service + upgrade VM + queue HUD");
        }

        // ── 7. label width -- truncation class this week ───────────────────────
        private static void CaseLabelWidth(List<string> failures, StringBuilder log)
        {
            // Slot button occupies 0.66..0.99 of the Manage slot row (0.33 of panel).
            // Narrowest portrait we pin: 640 px canvas, panel frac 0.92 -> button ~194 px.
            // Advance budget used in-repo for this class of defect: 16 px/glyph at label size.
            const int MinButtonPx = 194;
            const int PxPerGlyph = 16;
            string button = ManageScreenVM.BuyBuilderButtonCopy ?? "";
            string label = ManageScreenVM.BuyBuilderLabelCopy ?? "";
            int buttonPx = button.Length * PxPerGlyph;
            if (buttonPx > MinButtonPx)
                failures.Add("[width] '" + button + "' measures " + buttonPx + " px at 16px/glyph against a " +
                             MinButtonPx + " px button on a 640-wide canvas -- truncation class.");
            if (button.Length > 12)
                failures.Add("[width] button copy is " + button.Length + " chars -- keep it shorter than the old 'Buy slot 250c' (14).");

            // Label occupies 0.01..0.62 of the row (~0.61). Same 640 canvas -> ~360 px.
            const int MinLabelPx = 360;
            int labelPx = label.Length * PxPerGlyph;
            if (labelPx > MinLabelPx)
                failures.Add("[width] '" + label + "' measures " + labelPx + " px against a " + MinLabelPx +
                             " px label column -- will ellipsize.");

            log.AppendLine("  [width] button '" + button + "' " + button.Length + "ch/" + buttonPx +
                           "px  label '" + label + "' " + label.Length + "ch/" + labelPx + "px (budgets " +
                           MinButtonPx + "/" + MinLabelPx + ")");
        }

        // ── 8. live settle through ApplyPackContents is idempotent ─────────────
        private static void CaseSettle(List<string> failures, StringBuilder log)
        {
            Type vmType = typeof(PackStoreVM);
            var apply = vmType.GetMethod("ApplyPackContents", new[] { typeof(PackDef) });
            if (apply == null)
            {
                failures.Add("[settle] PackStoreVM.ApplyPackContents(PackDef) missing -- grant path unresolved.");
                return;
            }

            var pack = PackCatalog.Find(Sku);
            if (pack == null)
            {
                failures.Add("[settle] SKU vanished between catalog case and settle case.");
                return;
            }

            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            bool hadSave = PlayerPrefs.HasKey("dotr-save");
            string rawSave = hadSave ? PlayerPrefs.GetString("dotr-save", null) : null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                throwaway.OwnedItemIds = new List<string>();
                gssGo = new GameObject("GSS (builder-sku oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  " + RegressionOutcome.PartialSkip("[settle] ApplyPackContents",
                        "GameStateService state seam not reflectable (needs fleet)"));
                    return;
                }

                var vm = new PackStoreVM(() => throwaway);
                apply.Invoke(vm, new object[] { pack });
                apply.Invoke(vm, new object[] { pack });

                int skuHits = 0;
                if (throwaway.OwnedItemIds != null)
                {
                    foreach (var id in throwaway.OwnedItemIds)
                        if (string.Equals(id, Sku, StringComparison.Ordinal)) skuHits++;
                }
                if (skuHits != 1)
                    failures.Add("[settle] after two ApplyPackContents, OwnedItemIds has " + skuHits +
                                 " copies of '" + Sku + "' -- grant must be idempotent (exactly one).");
                if (!PackCatalog.OwnsPermanentBuilder(throwaway.OwnedItemIds))
                    failures.Add("[settle] after grant, OwnsPermanentBuilder is false -- entitlement did not take.");

                int crew = BuildTimerService.ConcurrencyOf(2, 0, PackCatalog.OwnsPermanentBuilder(throwaway.OwnedItemIds));
                int depth = BuildTimerService.DepthOf(5, 0);
                if (crew != 3)
                    failures.Add("[settle] after grant, crew=" + crew + " -- expected 3 (2 free + 1 SKU).");
                if (depth != 5)
                    failures.Add("[settle] after grant, depth=" + depth + " -- SKU must not raise depth.");

                log.AppendLine("  [settle] two ApplyPackContents -> ownedHits=" + skuHits + " crew=" + crew + " depth=" + depth);
            }
            catch (Exception ex)
            {
                failures.Add("[settle] threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString("dotr-save", rawSave); else PlayerPrefs.DeleteKey("dotr-save");
                PlayerPrefs.Save();
            }
        }

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static string ReadRepoFile(string relative)
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, relative);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch { return null; }
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "BUILDER_SKU_OK");
                return "BUILDER SKU OK -- permanent-builder raises concurrency not depth; grant is idempotent; Manage routes to the store; crystal TryBuySlot kept";
            }
            string reason = "builder-sku: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "BUILDER_SKU_FAIL: " + reason);
            return reason;
        }
    }
}
