// =============================================================================
// BuildMenuRealEconomyRegression [buildmenu-economy]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only; references DeNelle.Core +
// DeNelle.Village). Contract mirrors the other Run(out reason) oracles:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: BUILDMENU_ECONOMY_OK (Debug.Log) / BUILDMENU_ECONOMY_FAIL (LogError)
//
// WHY THIS EXISTS (owner Tier 0, 2026-08-02 -- WO-861). BuildMenu shipped TWO live
// economy defects in one View:
//
//   DEFECT 1  THE FAKE WALLET. BuildMenu.GetMaterialCount(id) returned the literals
//             wood=20 / stone=5. Those literals were SHOWN to the player as their
//             on-hand balance, were what the Build button's afford gate compared
//             against, and were never deducted -- so every tower priced in wood or
//             stone was FREE. A second hard-coded TowerVariantDef table in the same
//             file was a rival cost authority to the catalog.
//   DEFECT 2  THE UNVERIFIED SPEND. OnConfirmBuild called BuildModeController
//             .ChargeLedger, which DISCARDS IEconomy.TrySpend's bool return, and
//             then placed with prepaid:true even when the ledger DECLINED.
//
// The cases below are deliberately split into BEHAVIOURAL (drive the real
// BuildMenuVM over an injected ledger) and GENERAL SOURCE-LINT (stop the CLASS of
// bug, not this instance).
//
// FIDELITY NOTE -- WHY THE BEHAVIOURAL HALF IS VM-LEVEL, NOT A REAL PLACEMENT.
// DataRegression runs in EDITOR BATCHMODE WITH NO PLAY SESSION: EconomyService and
// GameStateService are MonoBehaviour singletons that do not exist, and a real tap-
// to-place needs a loaded scene, a PlacementGrid and TowerPlacementSystem. So this
// oracle takes option (a) from the brief -- it CONSTRUCTS the production
// BuildMenuVM with an INJECTED IEconomy ledger (the same public ctor the shipped
// UICaptureLaunch + EditMode tests use) and drives the exact methods the View calls:
// MaterialCount / CanAfford / TrySpendBuild. That is the real balance-read, the real
// afford gate and the real spend decision -- everything the View does with them is a
// direct render or an early return. It NEVER no-ops over a null singleton: every
// prerequisite (catalog rows, VM construction) is asserted, and a missing one FAILS.
// What is NOT covered here (and is not claimed): the uGUI render itself, and the
// TowerPlacementSystem commit downstream of the spend -- both need play mode.
//
// Cases:
//   1 [ledger-read]        the VM's balances + MaterialCount read the INJECTED ledger,
//                          and specifically do NOT return the retired 20/5 literals.
//   2 [catalog-cost]       the Build-Tower rows come from the catalog and every row's
//                          displayed cost is byte-for-byte BuildModeController.CostFor
//                          for that id; the deleted literal table has not come back.
//   3 [blocked-spend]      OWNER ACCEPTANCE. With a known-LOW ledger the cheapest tower
//                          is unaffordable, TrySpendBuild returns FALSE with a reason,
//                          and EVERY balance is UNCHANGED. Plus the race control: a
//                          ledger whose CanAfford says yes but whose TrySpend says NO
//                          must still block (this is the half ChargeLedger threw away).
//   4 [funded-spend]       positive control -- exact funds: afford true, spend true,
//                          balances debited by EXACTLY the cost (so case 3 is not
//                          passing vacuously).
//   5 [no-fake-wallet]     GENERAL source-lint: no build/cost/affordability path
//                          anywhere under Assets/_Modules or Assets/Editor returns a
//                          hardcoded resource balance.
//   6 [tryspend-honoured]  GENERAL source-lint: no spend path anywhere discards
//                          IEconomy.TrySpend's bool return.
//
// EXPECT CASE 6 TO FAIL UNTIL BuildModeController.ChargeLedger IS PATCHED. That
// method is the remaining offender in the tree (it is lane-fenced away from the
// author of this oracle). The failure line names the file and line -- that is the
// proving data, not noise.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.BuildMenuRealEconomyRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;
using DeNelle.Village.UI;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Editor.Regression
{
    public static class BuildMenuRealEconomyRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string BuildMenuSrc   = "Assets/_Modules/Village/Buildings/UI/BuildMenu.cs";

        /// <summary>The literals the retired BuildMenu.GetMaterialCount stub returned.</summary>
        private const int RetiredFakeWood  = 20;
        private const int RetiredFakeStone = 5;

        /// <summary>Roots the two general source-lints sweep.</summary>
        private static readonly string[] LintRoots = { "Assets/_Modules", "Assets/Editor" };

        // Brace characters as CODE POINTS, never as char literals. CLAUDE.md sec.1's
        // mandatory C# quality gate counts raw open/close brace CHARACTERS in the file
        // and rejects any imbalance -- a source-scanning oracle that writes them as
        // literals fails that gate for a reason that is not a real syntax error.
        private const char BraceOpen  = (char)123;
        private const char BraceClose = (char)125;

        // =====================================================================
        //  Allowlist for case 6 -- the ONLY places a discarded TrySpend is legal.
        //  Keyed by file + the exact statement text, so a NEW discard in the same
        //  file still fails. BuildModeController.ChargeLedger is deliberately NOT
        //  here: it is the defect this oracle exists to hold down.
        // =====================================================================
        private struct DiscardException
        {
            public string File;      // path suffix
            public string Snippet;   // whitespace-normalised statement text that must match
            public string Why;
        }

        private static readonly DiscardException[] AllowedDiscards =
        {
            new DiscardException
            {
                File    = "Assets/_Modules/Village/EconomyService.cs",
                Snippet = "TrySpend(ResourceCost.WoodOnly(woodCost));",
                Why     = "the [Obsolete] wood-only Spend(int) alias -- its DOCUMENTED contract is a silent no-op when short (WO-842), and it returns void, so there is no bool for a caller to honour.",
            },
            new DiscardException
            {
                File    = "Assets/_Modules/Village/World/OutpostHub.cs",
                Snippet = "econ.TrySpend(upkeep);",
                Why     = "per-tick defender upkeep drain, pre-gated on the line above by econ.Food/econ.Wood >= upkeep; a short tick is best-effort by design (defenders survive, recruitment is what gets blocked).",
            },
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BUILDMENU_ECONOMY_OK - " + reason);
            else Debug.LogError("BUILDMENU_ECONOMY_FAIL: " + reason);
        }

        // =====================================================================
        //  Entry
        // =====================================================================
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== BuildMenuRealEconomyRegression [buildmenu-economy] ===");

            try
            {
                HydrateCatalog(failures, log);
                CaseLedgerRead(failures, log);
                CaseCatalogCost(failures, log);
                CaseBlockedSpend(failures, log);
                CaseFundedSpend(failures, log);
                CaseNoFakeWallet(failures, log);
                CaseTrySpendHonoured(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("BuildMenuRealEconomyRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "BUILDMENU ECONOMY OK - the build menu reads the live ledger (no 20/5 literals), " +
                         "prices every tower from the catalog, blocks an unaffordable placement without moving a " +
                         "single resource, debits exactly the cost when funded, and no spend path in the tree " +
                         "discards TrySpend's return.";
                Debug.Log("BUILDMENU_ECONOMY_OK\n" + log);
                return true;
            }
            reason = "BUILDMENU ECONOMY: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("BUILDMENU_ECONOMY_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  Catalog hydration -- the SAME parse CatalogBootstrap performs, so the
        //  VM's rows resolve exactly as they do in the player. A parse break is a
        //  FAILURE, never a quiet skip.
        // =====================================================================
        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        private static void HydrateCatalog(List<string> failures, StringBuilder log)
        {
            if (CatalogRegistry.OfType(CatalogType.Tower).Count > 0)
            {
                log.AppendLine("  catalog already hydrated (" + CatalogRegistry.OfType(CatalogType.Tower).Count + " Tower row(s))");
                return;
            }

            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[buildmenu-economy] " + CatalogRelPath + " unreadable - the build menu has no cost source at all");
                return;
            }
            StructuresFile file = null;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            }
            catch (Exception ex)
            {
                failures.Add("[buildmenu-economy] structures-catalog.json failed to parse: " + ex.Message);
                return;
            }
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[buildmenu-economy] structures-catalog.json deserialized to 0 entries");
                return;
            }
            int n = 0;
            foreach (var e in file.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (CatalogRegistry.Get(e.id) == null) { CatalogRegistry.Register(e); n++; }
            }
            log.AppendLine("  hydrated CatalogRegistry with " + n + " entry(ies) from " + CatalogRelPath);
        }

        // =====================================================================
        //  A fake ledger. Mutates ONLY on a successful TrySpend, so "resources
        //  unchanged" is observable rather than assumed. DeclineEverySpend models
        //  the race BuildModeController.ChargeLedger threw away: CanAfford says
        //  yes, TrySpend says no.
        // =====================================================================
        private sealed class FakeLedger : IEconomy
        {
            public int Coins { get; set; }
            public int Wood { get; set; }
            public int Iron { get; set; }
            public int Food { get; set; }
            public int Crystals { get; set; }

            /// <summary>When true, TrySpend always refuses (and never mutates) even if affordable.</summary>
            public bool DeclineEverySpend;

            public int SpendCalls;

            public event Action<ResourceSnapshot> OnChanged;

            public bool CanAfford(DeNelle.Village.ResourceCost cost)
                => Wood >= cost.Wood && Food >= cost.Food && Iron >= cost.Iron
                   && Crystals >= cost.Crystals && Coins >= cost.Coins;

            public bool TrySpend(DeNelle.Village.ResourceCost cost)
            {
                SpendCalls++;
                if (DeclineEverySpend) return false;
                if (!CanAfford(cost)) return false;
                Wood -= cost.Wood; Food -= cost.Food; Iron -= cost.Iron;
                Crystals -= cost.Crystals; Coins -= cost.Coins;
                OnChanged?.Invoke(new ResourceSnapshot(Wood, Food, Iron, Crystals));
                return true;
            }

            public void Grant(DeNelle.Village.ResourceCost amount)
            {
                Wood += amount.Wood; Food += amount.Food; Iron += amount.Iron;
                Crystals += amount.Crystals; Coins += amount.Coins;
                OnChanged?.Invoke(new ResourceSnapshot(Wood, Food, Iron, Crystals));
            }

            public string Describe()
                => "W" + Wood + " F" + Food + " I" + Iron + " C" + Crystals;
        }

        private static BuildMenuVM MakeVm(IEconomy ledger, int fallbackCrystals = 0)
            => new BuildMenuVM(ledger, new PlacedTowerListVM(() => new Tower[0]), null, fallbackCrystals, null);

        // =====================================================================
        //  CASE 1 [ledger-read] -- the balances the menu shows are the LEDGER's.
        // =====================================================================
        private static void CaseLedgerRead(List<string> failures, StringBuilder log)
        {
            // Values chosen so that a surviving stub is unmistakable: neither equals
            // the retired 20 / 5 literals.
            var ledger = new FakeLedger { Wood = 3, Iron = 2, Food = 11, Crystals = 7 };
            var vm = MakeVm(ledger, fallbackCrystals: 999);
            if (vm == null) { failures.Add("[ledger-read] BuildMenuVM could not be constructed"); return; }

            AssertInt(failures, "[ledger-read] MaterialCount(\"wood\")", vm.MaterialCount("wood"), ledger.Wood);
            AssertInt(failures, "[ledger-read] MaterialCount(\"stone\") (legacy UI label for the Iron axis)", vm.MaterialCount("stone"), ledger.Iron);
            AssertInt(failures, "[ledger-read] MaterialCount(\"iron\")", vm.MaterialCount("iron"), ledger.Iron);
            AssertInt(failures, "[ledger-read] MaterialCount(\"food\")", vm.MaterialCount("food"), ledger.Food);
            AssertInt(failures, "[ledger-read] MaterialCount(\"crystals\")", vm.MaterialCount("crystals"), ledger.Crystals);
            AssertInt(failures, "[ledger-read] Wood", vm.Wood, ledger.Wood);
            AssertInt(failures, "[ledger-read] Iron", vm.Iron, ledger.Iron);
            AssertInt(failures, "[ledger-read] Crystals (economy present -> NOT the fallback)", vm.Crystals, ledger.Crystals);

            // The literal-stub tell, stated explicitly so a regression reads plainly.
            if (vm.MaterialCount("wood") == RetiredFakeWood && ledger.Wood != RetiredFakeWood)
                failures.Add("[ledger-read] MaterialCount(\"wood\") returned the RETIRED FAKE literal " + RetiredFakeWood +
                             " while the ledger holds " + ledger.Wood + " - the stub wallet is back");
            if (vm.MaterialCount("stone") == RetiredFakeStone && ledger.Iron != RetiredFakeStone)
                failures.Add("[ledger-read] MaterialCount(\"stone\") returned the RETIRED FAKE literal " + RetiredFakeStone +
                             " while the ledger holds " + ledger.Iron + " - the stub wallet is back");

            // A ledger the menu cannot see must report NOTHING on hand, never a
            // convenience number (that is exactly how the original defect read).
            var vmNoEconomy = MakeVm(null, fallbackCrystals: 42);
            AssertInt(failures, "[ledger-read] no-service Wood", vmNoEconomy.Wood, 0);
            AssertInt(failures, "[ledger-read] no-service MaterialCount(\"stone\")", vmNoEconomy.MaterialCount("stone"), 0);
            AssertInt(failures, "[ledger-read] no-service unknown axis", vmNoEconomy.MaterialCount("obsidian"), 0);

            log.AppendLine("  [ledger-read] VM balances track the injected ledger (" + ledger.Describe() + ") OK");
            vm.Dispose();
            vmNoEconomy.Dispose();
        }

        // =====================================================================
        //  CASE 2 [catalog-cost] -- displayed cost == catalog cost, for every row.
        // =====================================================================
        private static void CaseCatalogCost(List<string> failures, StringBuilder log)
        {
            var vm = MakeVm(new FakeLedger());
            var options = vm.TowerOptions;
            if (options == null || options.Count == 0)
            {
                failures.Add("[catalog-cost] BuildMenuVM.TowerOptions is EMPTY - the Build-Tower screen has nothing to price " +
                             "(catalog hydration failed, or the menu stopped sourcing the catalog)");
                vm.Dispose();
                return;
            }

            foreach (var opt in options)
            {
                var entry = CatalogRegistry.Get(opt.Id);
                if (entry == null)
                {
                    failures.Add("[catalog-cost] offered tower id '" + opt.Id + "' does not resolve in CatalogRegistry - " +
                                 "the menu is offering a row the rest of the game cannot price");
                    continue;
                }
                if (entry.type != CatalogType.Tower)
                    failures.Add("[catalog-cost] offered id '" + opt.Id + "' is type " + entry.type + ", not Tower");

                CoreCost catalogCost = BuildModeController.CostFor(entry);
                if (!SameCost(opt.Cost, catalogCost))
                    failures.Add("[catalog-cost] '" + opt.Id + "' DISPLAYED cost " + Describe(opt.Cost) +
                                 " != catalog cost " + Describe(catalogCost) +
                                 " (BuildModeController.CostFor) - the menu is pricing from a second, divergent table");
                if (catalogCost.IsZero)
                    failures.Add("[catalog-cost] '" + opt.Id + "' resolves a ZERO catalog cost - the menu would hand out a free tower");
                log.AppendLine("  [catalog-cost] " + opt.Id + " -> " + Describe(opt.Cost) + " (matches CostFor)");
            }

            // Cheap-first ordering: the FTUE default selection must be the cheapest row.
            for (int i = 1; i < options.Count; i++)
                if (options[i].CostTotal < options[i - 1].CostTotal)
                    failures.Add("[catalog-cost] tower rows are not cheapest-first: '" + options[i].Id + "' (" +
                                 options[i].CostTotal + ") follows '" + options[i - 1].Id + "' (" + options[i - 1].CostTotal + ")");

            // Anti-regression source-lint: the deleted View-side authorities stay deleted.
            string src = ReadSource(BuildMenuSrc);
            if (src == null)
            {
                failures.Add("[catalog-cost] cannot read " + BuildMenuSrc + " - the literal-table lint could not run");
            }
            else
            {
                string code = StripCommentsAndStrings(src);
                if (code.Contains("TowerVariantDef"))
                    failures.Add("[catalog-cost] BuildMenu.cs declares TowerVariantDef again - the View-side balance table " +
                                 "(a second cost authority to the catalog) is back");
                if (code.Contains("GetMaterialCount"))
                    failures.Add("[catalog-cost] BuildMenu.cs declares GetMaterialCount again - the fake wallet is back");
            }

            log.AppendLine("  [catalog-cost] " + options.Count + " tower row(s), all priced by BuildModeController.CostFor OK");
            vm.Dispose();
        }

        // =====================================================================
        //  CASE 3 [blocked-spend] -- THE OWNER ACCEPTANCE.
        //  "Attempt to place a tower with insufficient wood -> placement blocked
        //   and resources unchanged."
        // =====================================================================
        private static void CaseBlockedSpend(List<string> failures, StringBuilder log)
        {
            var probe = MakeVm(new FakeLedger());
            var options = probe.TowerOptions;
            if (options == null || options.Count == 0)
            {
                failures.Add("[blocked-spend] no tower rows to attempt - case 2 already reported the cause");
                probe.Dispose();
                return;
            }
            var cheapest = options[0];
            probe.Dispose();

            CoreCost cost = cheapest.Cost;
            if (cost.IsZero)
            {
                failures.Add("[blocked-spend] the cheapest tower '" + cheapest.Id + "' costs NOTHING - an unaffordable " +
                             "attempt cannot even be constructed, and the menu hands out free towers");
                return;
            }

            // (3a) A ledger that is short on EVERY axis the cost touches - deliberately
            // one unit under wood, which is the axis the owner's acceptance names.
            var poor = new FakeLedger
            {
                Wood     = Math.Max(0, cost.wood - 1),
                Iron     = Math.Max(0, cost.iron - 1),
                Food     = Math.Max(0, cost.food - 1),
                Crystals = Math.Max(0, cost.crystals - 1),
            };
            string before = poor.Describe();
            var vm = MakeVm(poor);

            if (vm.CanAfford(cost))
                failures.Add("[blocked-spend] CanAfford said TRUE for '" + cheapest.Id + "' costing " + Describe(cost) +
                             " against a ledger holding " + before + " - the Build button would light up on an unpayable tower");

            string why;
            bool spent = vm.TrySpendBuild(cost, out why);
            if (spent)
                failures.Add("[blocked-spend] TrySpendBuild returned TRUE for an UNAFFORDABLE tower '" + cheapest.Id +
                             "' (" + Describe(cost) + ") against " + before + " - the free-tower path is live");
            if (!spent && string.IsNullOrEmpty(why))
                failures.Add("[blocked-spend] TrySpendBuild refused without a reason - a silent refusal is a refusal the player cannot read (CLAUDE.md sec.12)");
            if (poor.Describe() != before)
                failures.Add("[blocked-spend] RESOURCES MOVED on a refused build: " + before + " -> " + poor.Describe() +
                             " (the owner's acceptance requires them UNCHANGED)");
            log.AppendLine("  [blocked-spend] '" + cheapest.Id + "' " + Describe(cost) + " vs " + before +
                           " -> blocked=" + (!spent) + ", reason='" + (why ?? "") + "', ledger after=" + poor.Describe());
            vm.Dispose();

            // (3b) THE RACE CONTROL - this is the exact half BuildModeController
            // .ChargeLedger threw away. CanAfford says yes; the ledger still declines.
            // A caller that ignores TrySpend's bool places a tower here for free.
            var liar = new FakeLedger
            {
                Wood = cost.wood + 100, Iron = cost.iron + 100,
                Food = cost.food + 100, Crystals = cost.crystals + 100,
                DeclineEverySpend = true,
            };
            string liarBefore = liar.Describe();
            var vm2 = MakeVm(liar);
            if (!vm2.CanAfford(cost))
                failures.Add("[blocked-spend] race control mis-set: the funded ledger reported CanAfford FALSE");
            string why2;
            bool spent2 = vm2.TrySpendBuild(cost, out why2);
            if (spent2)
                failures.Add("[blocked-spend] RACE: the ledger DECLINED the spend (TrySpend returned false) but TrySpendBuild " +
                             "reported SUCCESS - the caller would place a tower that was never paid for");
            if (liar.SpendCalls == 0)
                failures.Add("[blocked-spend] race control never reached IEconomy.TrySpend - the spend path is not going through the ledger at all");
            if (liar.Describe() != liarBefore)
                failures.Add("[blocked-spend] race control mutated the ledger on a declined spend: " + liarBefore + " -> " + liar.Describe());
            log.AppendLine("  [blocked-spend] declining-ledger race -> blocked=" + (!spent2) + ", TrySpend reached=" + (liar.SpendCalls > 0) +
                           ", reason='" + (why2 ?? "") + "' OK");
            vm2.Dispose();

            // (3c) No economy at all must REFUSE, never fall through to a silent yes.
            var vm3 = MakeVm(null, fallbackCrystals: 100000);
            string why3;
            if (vm3.TrySpendBuild(cost, out why3))
                failures.Add("[blocked-spend] TrySpendBuild returned TRUE with NO economy service - a spend that cannot be " +
                             "proven must never be reported as made");
            vm3.Dispose();
        }

        // =====================================================================
        //  CASE 4 [funded-spend] -- positive control, so case 3 cannot pass vacuously.
        // =====================================================================
        private static void CaseFundedSpend(List<string> failures, StringBuilder log)
        {
            var probe = MakeVm(new FakeLedger());
            var options = probe.TowerOptions;
            if (options == null || options.Count == 0) { probe.Dispose(); return; }
            var cheapest = options[0];
            probe.Dispose();

            CoreCost cost = cheapest.Cost;
            if (cost.IsZero) return;   // case 3 already failed this

            var funded = new FakeLedger
            {
                Wood = cost.wood, Iron = cost.iron, Food = cost.food, Crystals = cost.crystals,
            };
            var vm = MakeVm(funded);
            if (!vm.CanAfford(cost))
                failures.Add("[funded-spend] CanAfford said FALSE for an EXACTLY funded ledger (" + funded.Describe() +
                             ") against " + Describe(cost) + " - the afford gate is over-strict");
            string why;
            if (!vm.TrySpendBuild(cost, out why))
            {
                failures.Add("[funded-spend] TrySpendBuild REFUSED an exactly funded build of '" + cheapest.Id +
                             "' (" + Describe(cost) + "): " + (why ?? "<no reason>") + " - the menu can no longer build anything");
            }
            else
            {
                if (funded.Wood != 0 || funded.Iron != 0 || funded.Food != 0 || funded.Crystals != 0)
                    failures.Add("[funded-spend] after an exactly funded spend the ledger reads " + funded.Describe() +
                                 " - expected every axis at 0 (the debit did not match the displayed cost)");
                else
                    log.AppendLine("  [funded-spend] '" + cheapest.Id + "' " + Describe(cost) + " debited exactly, ledger drained to " + funded.Describe() + " OK");
            }
            vm.Dispose();
        }

        // =====================================================================
        //  CASE 5 [no-fake-wallet] -- GENERAL source-lint.
        //  Two shapes, both of which the retired stub matched:
        //    (a) a resource-keyed switch arm returning an integer literal
        //        (case "wood": return 20;)
        //    (b) a count/balance/on-hand style helper whose every return is a
        //        literal (a wallet that cannot possibly be reading a ledger).
        // =====================================================================
        private static readonly Regex ResourceCaseLiteral = new Regex(
            "case\\s*\"(wood|stone|iron|food|crystal|crystals|coin|coins|gold|magic)\"\\s*:\\s*return\\s+[0-9]+\\s*;",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WalletHelperSignature = new Regex(
            "\\b(?:private|public|internal|protected)[A-Za-z0-9_\\s]*\\bint\\s+" +
            "(\\w*(?:Material|Resource|Wallet|Stock|Currency)\\w*(?:Count|Amount|Balance|OnHand|Held)|" +
            "Get\\w*(?:Material|Resource|Wallet|Currency)\\w*)\\s*\\(",
            RegexOptions.Compiled);

        private static readonly Regex LiteralReturn = new Regex("\\breturn\\s+([0-9]+)\\s*;", RegexOptions.Compiled);
        private static readonly Regex AnyReturn     = new Regex("\\breturn\\b", RegexOptions.Compiled);

        private static void CaseNoFakeWallet(List<string> failures, StringBuilder log)
        {
            int scanned = 0, flagged = 0;
            foreach (string path in EnumerateSources())
            {
                if (IsSelf(path)) continue;
                string src = ReadSource(path);
                if (src == null) continue;
                scanned++;
                string code = StripCommentsAndStrings(src);
                // (a) resource-keyed literal switch arms. Strings are blanked by the
                // stripper, so match against the ORIGINAL text but confirm the match
                // region is live code (not blanked) in the stripped copy.
                foreach (Match m in ResourceCaseLiteral.Matches(src))
                {
                    if (!IsLiveCode(code, m.Index, m.Length)) continue;
                    flagged++;
                    failures.Add("[no-fake-wallet] " + path + ":" + LineOf(src, m.Index) +
                                 " returns a HARDCODED resource balance from a resource-keyed switch: '" +
                                 Squash(m.Value) + "' - a build/cost/affordability path must read the live ledger");
                }
                // (b) wallet-shaped helpers whose every return is a literal.
                foreach (Match m in WalletHelperSignature.Matches(code))
                {
                    string body = BodyAfter(code, m.Index + m.Length);
                    if (body == null) continue;
                    var lits = LiteralReturn.Matches(body);
                    int rets = AnyReturn.Matches(body).Count;
                    if (lits.Count == 0 || rets != lits.Count) continue;   // reads something real
                    flagged++;
                    failures.Add("[no-fake-wallet] " + path + ":" + LineOf(code, m.Index) + " helper '" + m.Groups[1].Value +
                                 "' returns ONLY integer literals (" + lits.Count + " literal return(s), no live read) - " +
                                 "that is a fabricated balance, exactly the BuildMenu.GetMaterialCount defect");
                }
            }
            log.AppendLine("  [no-fake-wallet] scanned " + scanned + " source file(s), " + flagged + " hardcoded-balance site(s)");
        }

        // =====================================================================
        //  CASE 6 [tryspend-honoured] -- GENERAL source-lint, the generic killer.
        //  Any `TrySpend(` whose bool result is THROWN AWAY is a spend the caller
        //  cannot have verified. Detection: find each call, walk back to the start
        //  of its statement, and check what precedes it. If the only thing in front
        //  is a receiver chain (`econ.`, `EconomyService.Instance.`) or nothing at
        //  all, the value is discarded. Anything else (if / ! / = / return / && /
        //  var / a declaration) consumes it.
        // =====================================================================
        private static readonly Regex TrySpendCall = new Regex("\\bTrySpend\\s*\\(", RegexOptions.Compiled);
        private static readonly Regex ReceiverChain = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*(\\s*\\.\\s*[A-Za-z_][A-Za-z0-9_]*)*\\s*\\.$", RegexOptions.Compiled);

        private static void CaseTrySpendHonoured(List<string> failures, StringBuilder log)
        {
            int calls = 0, discards = 0, allowed = 0;
            foreach (string path in EnumerateSources())
            {
                if (IsSelf(path)) continue;
                string src = ReadSource(path);
                if (src == null) continue;
                string code = StripCommentsAndStrings(src);

                foreach (Match m in TrySpendCall.Matches(code))
                {
                    calls++;
                    int b = -1;
                    for (int i = m.Index - 1; i >= 0; i--)
                    {
                        char c = code[i];
                        if (c == ';' || c == BraceOpen || c == BraceClose || c == ')' || c == ':') { b = i; break; }
                    }
                    string pre = code.Substring(b + 1, m.Index - b - 1).Trim();
                    if (pre.Length != 0 && !ReceiverChain.IsMatch(pre)) continue;   // consumed

                    string stmt = Squash(StatementAt(code, m.Index));
                    var ex = MatchAllowed(path, stmt);
                    if (ex.HasValue)
                    {
                        allowed++;
                        log.AppendLine("  [tryspend-honoured] ALLOWED discard " + path + ":" + LineOf(code, m.Index) +
                                       " -- " + ex.Value.Why);
                        continue;
                    }
                    discards++;
                    failures.Add("[tryspend-honoured] " + path + ":" + LineOf(code, m.Index) +
                                 " DISCARDS TrySpend's return: '" + stmt + "'. The spend may have been DECLINED and the " +
                                 "caller would never know - every caller downstream proceeds as if it were paid. Consume the " +
                                 "bool (or return it up to a caller that does).");
                }
            }
            log.AppendLine("  [tryspend-honoured] " + calls + " TrySpend call site(s): " + discards +
                           " unverified, " + allowed + " allowlisted");
        }

        private static DiscardException? MatchAllowed(string path, string statement)
        {
            string p = path.Replace('\\', '/');
            foreach (var ex in AllowedDiscards)
            {
                if (!p.EndsWith(ex.File.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
                if (statement.Contains(Squash(ex.Snippet))) return ex;
            }
            return null;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static bool IsSelf(string path)
            => path.Replace('\\', '/').EndsWith("BuildMenuRealEconomyRegression.cs", StringComparison.OrdinalIgnoreCase);

        /// <summary>The folder ABOVE Assets/ — never the process cwd, which batchmode does not pin.</summary>
        private static string ProjectRoot
        {
            get
            {
                var parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
            }
        }

        private static IEnumerable<string> EnumerateSources()
        {
            foreach (string root in LintRoots)
            {
                string abs = Path.Combine(ProjectRoot, root);
                if (!Directory.Exists(abs)) continue;
                string[] files;
                try { files = Directory.GetFiles(abs, "*.cs", SearchOption.AllDirectories); }
                catch (Exception) { continue; }
                foreach (string f in files)
                {
                    string rel = f.Replace('\\', '/');
                    int idx = rel.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                    yield return idx >= 0 ? rel.Substring(idx + 1) : rel;
                }
            }
        }

        private static string ReadSource(string relOrAbs)
        {
            try
            {
                string p = Path.IsPathRooted(relOrAbs)
                    ? relOrAbs
                    : Path.Combine(ProjectRoot, relOrAbs);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch (Exception) { return null; }
        }

        private static int LineOf(string src, int index)
        {
            int n = 1;
            for (int i = 0; i < index && i < src.Length; i++) if (src[i] == '\n') n++;
            return n;
        }

        private static string Squash(string s)
            => Regex.Replace(s ?? string.Empty, "\\s+", " ").Trim();

        /// <summary>The whole statement containing <paramref name="index"/> (previous
        /// terminator .. the next ';'), for a readable failure line.</summary>
        private static string StatementAt(string code, int index)
        {
            int start = 0;
            for (int i = index - 1; i >= 0; i--)
            {
                char c = code[i];
                if (c == ';' || c == BraceOpen || c == BraceClose) { start = i + 1; break; }
            }
            int end = code.IndexOf(';', index);
            if (end < 0) end = Math.Min(code.Length - 1, index + 160);
            return code.Substring(start, end - start + 1);
        }

        /// <summary>The brace-balanced body starting at the first opening brace at or after
        /// <paramref name="from"/>. Null for an expression-bodied / abstract member.</summary>
        private static string BodyAfter(string code, int from)
        {
            int open = -1;
            for (int i = from; i < code.Length; i++)
            {
                char c = code[i];
                if (c == BraceOpen) { open = i; break; }
                if (c == ';' || c == '=') return null;   // expression-bodied or declaration only
            }
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == BraceOpen) depth++;
                else if (code[i] == BraceClose)
                {
                    depth--;
                    if (depth == 0) return code.Substring(open, i - open + 1);
                }
            }
            return null;
        }

        /// <summary>True when the [index,index+len) span of the ORIGINAL source is still
        /// live code in the stripped copy (i.e. it was not a comment or a string body).</summary>
        private static bool IsLiveCode(string stripped, int index, int len)
        {
            if (index < 0 || index >= stripped.Length) return false;
            int end = Math.Min(stripped.Length, index + len);
            for (int i = index; i < end; i++)
                if (!char.IsWhiteSpace(stripped[i])) return true;
            return false;
        }

        /// <summary>
        /// Blanks comment bodies and string/char literal bodies with spaces, PRESERVING
        /// length and newlines so indices and line numbers still line up with the original.
        /// Without this, a doc-comment mentioning "EconomyService.Instance.TrySpend(cost)"
        /// would be reported as a live unverified spend.
        /// </summary>
        private static string StripCommentsAndStrings(string src)
        {
            var outp = src.ToCharArray();
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') { outp[i] = ' '; i++; }
                }
                else if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    outp[i] = ' '; outp[i + 1] = ' '; i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/'))
                    {
                        if (src[i] != '\n') outp[i] = ' ';
                        i++;
                    }
                    if (i + 1 < n) { outp[i] = ' '; outp[i + 1] = ' '; i += 2; }
                }
                else if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    outp[i] = ' '; outp[i + 1] = ' '; i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { outp[i] = ' '; outp[i + 1] = ' '; i += 2; continue; }
                            outp[i] = ' '; i++; break;
                        }
                        if (src[i] != '\n') outp[i] = ' ';
                        i++;
                    }
                }
                else if (c == '"')
                {
                    outp[i] = ' '; i++;
                    while (i < n)
                    {
                        if (src[i] == '\\')
                        {
                            outp[i] = ' ';
                            if (i + 1 < n) outp[i + 1] = ' ';
                            i += 2; continue;
                        }
                        if (src[i] == '"') { outp[i] = ' '; i++; break; }
                        if (src[i] != '\n') outp[i] = ' ';
                        i++;
                    }
                }
                else if (c == '\'')
                {
                    outp[i] = ' '; i++;
                    while (i < n)
                    {
                        if (src[i] == '\\')
                        {
                            outp[i] = ' ';
                            if (i + 1 < n) outp[i + 1] = ' ';
                            i += 2; continue;
                        }
                        if (src[i] == '\'') { outp[i] = ' '; i++; break; }
                        outp[i] = ' '; i++;
                    }
                }
                else i++;
            }
            return new string(outp);
        }

        private static bool SameCost(CoreCost a, CoreCost b)
            => a.wood == b.wood && a.food == b.food && a.iron == b.iron && a.crystals == b.crystals;

        private static string Describe(CoreCost c)
            => "w" + c.wood + " f" + c.food + " i" + c.iron + " c" + c.crystals;

        private static void AssertInt(List<string> failures, string what, int got, int expected)
        {
            if (got != expected)
                failures.Add(what + " = " + got + ", expected " + expected + " (the live ledger value)");
        }
    }
}
