// =============================================================================
// SiegeLossStakesRegression — [siege-loss-stakes] (WO-1139, ruling 2026-08-22).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Registered ONCE in DataRegression.RunAll.
//
// THE ORACLE FOR "COLLECTOR LOOTING ONLY. NO BANK THEFT."
//   Player-facing rule: what you have COLLECTED is safe; what is still sitting in
//   the building is at risk.
//
// It pins the six things that would each turn this mechanic from "real risk" into a
// support ticket:
//
//   A. ⛔⛔ NOTHING DEBITS THE WALLET FOR A SIEGE. THIS IS THE HEADLINE CASE and the
//      reason the suite was rewritten. `ResourceCollector.OnSiegeDestroyed` has ALREADY
//      removed the resources from its own pending when the collector broke, so a bank
//      debit on top of it charges the player TWICE FOR ONE SIEGE. An earlier pass on
//      this WO shipped exactly that (a flat 15%-of-banked take through
//      EconomyService.TrySpend). The case MEASURES the bank across a real
//      BuildStakes + ApplyStakes, with a live EconomyService installed and ready to
//      spend, and fails on a single point of movement.
//
//   B. ⛔ CRYSTAL COLLECTORS ARE NEVER ROBBED. A crystal collector breaks like any
//      other and keeps every point of its pending. A player cannot tell a HARVESTED
//      crystal from a PURCHASED one -- same wallet -- so a crystal loss reads as losing
//      bought currency, which turns a gameplay loss into a refund request on a live
//      published title. Enforced twice, independently (nothing is taken at the steal;
//      nothing could be recorded at the ledger), and both halves are asserted.
//
//   C. ⭐ THE REPORT FIGURE AND THE COLLECTOR'S LOSS ARE ONE NUMBER. Not "two
//      calculations that agree" -- the ledger is the collector's own LastLootStolen,
//      summed. The suite measures both ends and the per-bucket sum.
//
//   D. NOTHING PERMANENT IS DESTROYED. No structure loses a level, no ever-built id is
//      forgotten, no cleared-camp / best-wave progress moves.
//
//   E. A DEFENCE IN WHICH NOTHING BROKE REPORTS NOTHING -- and a break from an EARLIER
//      siege is not re-reported. A destroyed collector is never repairable (WO-753), so
//      it stands as a shell carrying its loot figure all session; without the break
//      stamp every later report would re-announce the same robbery.
//
//   F. THE SEAL IS IDEMPOTENT. A re-filed or re-opened report cannot re-count.
//
// =============================================================================
// ⚠⚠ WHY THE LOOT CASES USE HAND-COMPUTED LITERALS — READ BEFORE "TIDYING" ⚠⚠
//
//   Every expected number in LootTableCases is an AUTHORED CONSTANT, worked out by
//   hand from the ruling and written down. It is NOT computed from RaidLootFraction.
//
//   That is the entire point. An oracle that says
//       expected = pending * RaidLootFraction
//   is a restatement of the implementation in test clothing: it passes for EVERY value
//   that constant could ever hold, so it can never fail, and it would sail straight past
//   someone "tuning" 0.5 to 0.9. This repo shipped that exact defect twice inside 24
//   hours. If a future tuning pass changes the ruling, these literals are SUPPOSED to go
//   red -- that red is the oracle telling you a player-money rule moved, which is
//   precisely what you want it to say. RaidLootFraction is explicitly OUT OF SCOPE for
//   tuning (owner ruling 2026-08-22): it stays 0.5.
//
//   The wallet case (A) does not restate anything either: it MEASURES a before/after
//   bank on a real GameState with a real EconomyService and asserts NO MOVEMENT.
// =============================================================================

using System.Collections.Generic;
using System.Reflection;
using DeNelle.Core.Defense;
using DeNelle.Core.Economy;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Buildings.Progression;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Oracle for the WO-1139 loss stakes: collector looting only, no bank theft.</summary>
    public static class SiegeLossStakesRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            try
            {
                LootTableCases(failures);
                CrystalExemptionShapeCases(failures);
                LedgerWriterCases(failures);
                NoBankArithmeticCases(failures);

                LiveCollectorCases(failures, out bool skipped, out string skipWhy);
                if (skipped)
                {
                    // The GameStateService / EconomyService install seam moved -- genuinely
                    // unrunnable headless. NAMED SKIP, never a false pass (harness-integrity rule).
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "SIEGE LOSS STAKES", "needs fleet -- " + skipWhy);
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "SIEGE LOSS STAKES OK -- collector looting only: a broken collector's loot " +
                         "matches an AUTHORED table (not re-derived from RaidLootFraction); CRYSTAL " +
                         "COLLECTORS ARE NEVER ROBBED and no crystal bucket can be written; the report " +
                         "figure IS the collector's own LastLootStolen, summed; ⛔ THE BANK DOES NOT MOVE " +
                         "(no double-charge -- the collector already paid); a break from an earlier siege " +
                         "is never re-reported; the seal is idempotent; and no structure level, ever-built " +
                         "id, camp cooldown or best-wave moved";
                return true;
            }
            reason = $"SIEGE LOSS STAKES FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  THE AUTHORED LOOT TABLE — hand-computed, never re-derived
        // =====================================================================

        /// <summary>One hand-worked row of the ruling. <see cref="Expected"/> is a LITERAL.</summary>
        private struct Fixture
        {
            public string Note;
            public HarvestResource Resource;
            public double Pending;
            public int Expected;
        }

        private static void LootTableCases(List<string> f)
        {
            // Worked BY HAND from the ruling: half of what is still UNCOLLECTED, rounded down;
            // a crystal collector loses nothing. The brief's worked example is row 1.
            var table = new[]
            {
                new Fixture { Note = "the worked example: a wood collector holding 800",
                              Resource = HarvestResource.Wood, Pending = 800, Expected = 400 },

                new Fixture { Note = "iron, odd pending -- rounds DOWN, in the player's favour",
                              Resource = HarvestResource.Iron, Pending = 801, Expected = 400 },

                new Fixture { Note = "food, a big haul",
                              Resource = HarvestResource.Food, Pending = 5000, Expected = 2500 },

                new Fixture { Note = "a single unit is not worth half a unit -- rounds to nothing",
                              Resource = HarvestResource.Wood, Pending = 1, Expected = 0 },

                new Fixture { Note = "three units",
                              Resource = HarvestResource.Wood, Pending = 3, Expected = 1 },

                new Fixture { Note = "an empty collector",
                              Resource = HarvestResource.Food, Pending = 0, Expected = 0 },

                new Fixture { Note = "a negative pending cannot produce a loot",
                              Resource = HarvestResource.Iron, Pending = -500, Expected = 0 },

                // ⛔ THE EXEMPTION ROWS. A crystal collector BREAKS -- it is simply never robbed.
                new Fixture { Note = "CRYSTALS: a full crystal collector loses NOTHING",
                              Resource = HarvestResource.Crystals, Pending = 800, Expected = 0 },

                new Fixture { Note = "CRYSTALS: however much it holds",
                              Resource = HarvestResource.Crystals, Pending = 999999, Expected = 0 },
            };

            for (int i = 0; i < table.Length; i++)
            {
                var t = table[i];
                int actual = ResourceCollector.LootTakenFrom(t.Resource, t.Pending);
                if (actual != t.Expected)
                    f.Add($"loot[{i}] ({t.Note}): {t.Resource} pending={t.Pending} lost {actual}, the ruling " +
                          $"says {t.Expected}. THIS NUMBER IS PLAYER MONEY -- RaidLootFraction is out of " +
                          "scope for tuning (owner ruling 2026-08-22); if the ruling really moved, update " +
                          "this AUTHORED table with a new hand-worked value and never replace it with an " +
                          "expression over the production constant.");

                if (t.Pending > 0 && actual > t.Pending)
                    f.Add($"loot[{i}]: carried off {actual} from a collector holding {t.Pending} -- more than exists");
            }
        }

        // =====================================================================
        //  ⛔ THE CRYSTAL EXEMPTION — asserted on BOTH independent halves
        // =====================================================================

        private static void CrystalExemptionShapeCases(List<string> f)
        {
            // (1) THE STEAL SIDE. Every harvest type, so adding one cannot slip through untested.
            foreach (HarvestResource r in System.Enum.GetValues(typeof(HarvestResource)))
            {
                bool lootable = ResourceCollector.IsResourceLootable(r);
                bool shouldBe = r != HarvestResource.Crystals;
                if (lootable != shouldBe)
                    f.Add($"[HARD-RULE]exemption: ResourceCollector.IsResourceLootable({r}) is {lootable}. " +
                          "Crystals are indistinguishable from PURCHASED crystals -- the same wallet -- so " +
                          "looting one reads as taking bought currency and turns a gameplay loss into a " +
                          "refund request on a live published title. Hard exemption, not a balance knob.");
            }

            // (2) THE LEDGER SIDE, INDEPENDENTLY. Even if the steal side were wrong, no crystal
            //     (or coin) bucket can be written into a report the player reads.
            foreach (BankResource r in System.Enum.GetValues(typeof(BankResource)))
            {
                bool lootable = StakeRules.IsLootable(r);
                bool shouldBe = r == BankResource.Wood || r == BankResource.Iron || r == BankResource.Food;
                if (lootable != shouldBe)
                    f.Add($"[HARD-RULE]exemption: StakeRules.IsLootable({r}) is {lootable}. Only earned " +
                          "wood/iron/food may ever appear on a loss report.");
            }

            // (3) The paranoid half: ASK for a crystal loss and prove the ledger refuses it.
            var poisoned = StakeRules.Empty();
            if (StakeRules.Add(poisoned, BankResource.Crystals, 5000))
                f.Add("[HARD-RULE]StakeRules.Add accepted a CRYSTAL bucket -- the exemption must hold " +
                      "against a caller that gets it wrong, or it is not a rule");
            if (StakeRules.Add(poisoned, BankResource.Coins, 5000))
                f.Add("[HARD-RULE]StakeRules.Add accepted a COINS bucket -- coins are not a harvest");
            if (poisoned.Crystals != 0 || poisoned.Magic != 0 || !poisoned.IsEmpty)
                f.Add($"[HARD-RULE]a refused bucket still landed on the ledger (c{poisoned.Crystals} " +
                      $"m{poisoned.Magic} w{poisoned.Wood} i{poisoned.Iron} f{poisoned.Food})");
        }

        // =====================================================================
        //  THE ONE WRITER — StakeRules.Add routes and accumulates correctly
        // =====================================================================

        private static void LedgerWriterCases(List<string> f)
        {
            var l = StakeRules.Empty();

            if (l.StakesRuleId != StakeRules.RuleId)
                f.Add($"StakeRules.Empty() is not self-describing: rule id '{l.StakesRuleId}'");
            if (StakesLedger.InterimRuleId == StakeRules.RuleId)
                f.Add("the interim rule id was collapsed into the live one -- old reports would start " +
                      "claiming they were written under a ruling that did not exist yet");
            if (l.Applied)
                f.Add("StakeRules.Empty() came back APPLIED -- an unsealed ledger must not look sealed");
            if (!l.IsEmpty)
                f.Add("StakeRules.Empty() is not empty");

            // Accumulation: two wood collectors on one report add up rather than overwrite.
            StakeRules.Add(l, BankResource.Wood, 400);
            StakeRules.Add(l, BankResource.Wood, 150);
            StakeRules.Add(l, BankResource.Iron, 25);
            if (l.Wood != 550) f.Add($"two broken wood collectors summed to {l.Wood}, hand-worked answer 550 " +
                                     "-- a second collector must ADD, not overwrite");
            if (l.Iron != 25) f.Add($"iron bucket is {l.Iron}, expected 25");
            if (l.Food != 0) f.Add($"food bucket is {l.Food} with no food collector broken -- expected 0");

            // Zero / negative are not stakes.
            if (StakeRules.Add(l, BankResource.Wood, 0) || StakeRules.Add(l, BankResource.Wood, -300))
                f.Add("StakeRules.Add accepted a zero/negative amount -- a 'loss' that hands resources " +
                      "back is a bug, and it must never reach a ledger the player reads");
            if (l.Wood != 550)
                f.Add($"a zero/negative Add moved the wood bucket to {l.Wood}");
        }

        // =====================================================================
        //  ⛔ THE RIVAL SYSTEM MUST NOT COME BACK — structural
        // =====================================================================

        /// <summary>
        /// The superseded flat 15%-of-banked take is DELETED. This asserts it structurally, so a
        /// re-add fails the gate the moment it is written rather than the day a player is charged
        /// twice for one siege.
        /// </summary>
        private static void NoBankArithmeticCases(List<string> f)
        {
            foreach (string gone in new[] { "Build", "TakeFrom", "ProtectedFloor" })
                if (typeof(StakeRules).GetMethod(gone, BindingFlags.Public | BindingFlags.Static) != null)
                    f.Add($"[HARD-RULE]StakeRules.{gone} is back. That is the RETIRED bank-theft " +
                          "arithmetic (owner ruling 2026-08-22: collector looting only, NO bank theft). " +
                          "The collector already removed the resources when it broke -- a second, " +
                          "bank-side take charges the player twice for one siege.");

            foreach (string gone in new[] { "StealFraction", "ProtectedFloorFraction" })
                if (typeof(StakeRules).GetField(gone, BindingFlags.Public | BindingFlags.Static) != null)
                    f.Add($"[HARD-RULE]StakeRules.{gone} is back -- see above; there must be no bank-take " +
                          "constant for a future edit to hang arithmetic off.");

            // No surviving method may even ACCEPT a bank/capacity input.
            foreach (var m in typeof(StakeRules).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                foreach (var p in m.GetParameters())
                {
                    string n = p.Name != null ? p.Name.ToLowerInvariant() : string.Empty;
                    if (n.Contains("bank") || n.Contains("capacity") || n.Contains("wallet"))
                        f.Add($"[HARD-RULE]StakeRules.{m.Name} gained a '{p.Name}' parameter. This file must " +
                              "not know what the town bank holds: knowing is the first half of taking.");
                }
            }
        }

        // =====================================================================
        //  ⭐ THE LIVE CASE — a real collector, a real bank, a real report
        // =====================================================================

        private static void LiveCollectorCases(List<string> f, out bool skipped, out string skipWhy)
        {
            skipped = false; skipWhy = null;

            var priorState = GameStateService.Instance;
            var priorEconomy = EconomyService.Instance;
            string rawSave = HeadlessState.SnapshotSave(out bool hadSave);

            GameObject gssGo = null;
            GameObject ecoGo = null;
            GameObject collectorGo = null;
            GameState throwaway = null;
            bool installed = false;

            // The registry is a process-wide static. Park whatever is in it so a real scene's
            // collectors cannot leak into these cases (or be lost by them).
            var parked = new List<ResourceCollector>(ResourceCollectorRegistry.All);
            foreach (var c in parked) ResourceCollectorRegistry.Unregister(c);

            // The collector persists pending/hp/stamp in PlayerPrefs keyed by building id, so the
            // fixture would otherwise dirty a real save's lumbermill. Snapshot and restore.
            const string FixtureId = ResourceBuildingProgression.LumbermillId;   // yields Wood
            string[] prefKeys =
            {
                "dotr.collector.pending." + FixtureId,
                "dotr.collector.hp." + FixtureId,
                "dotr.collector.lastaccrual." + FixtureId,
            };
            var prefWas = new string[prefKeys.Length];
            var prefHad = new bool[prefKeys.Length];
            for (int i = 0; i < prefKeys.Length; i++)
            {
                prefHad[i] = PlayerPrefs.HasKey(prefKeys[i]);
                prefWas[i] = prefHad[i] ? PlayerPrefs.GetString(prefKeys[i], string.Empty) : string.Empty;
            }

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (loss-stakes-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!HeadlessState.TryInstall(gss, throwaway, out skipWhy)) { skipped = true; return; }
                installed = true;
                var s = throwaway;

                // ⛔ A LIVE ECONOMY SERVICE IS INSTALLED ON PURPOSE. The point of case A is not
                //    "nothing could be debited"; it is "everything needed to debit was present and
                //    NOTHING WAS".
                ecoGo = new GameObject("EconomyService (loss-stakes-oracle)");
                var eco = ecoGo.AddComponent<EconomyService>();
                if (!TrySetEconomyInstance(eco, out string ecoWhy))
                { skipped = true; skipWhy = ecoWhy; return; }

                // Permanent progress to prove untouched.
                s.BaseLayout.Clear();
                s.BaseLayout.Add(Placed("tower_ground_archer", 3, 4, 5));
                s.BaseLayout.Add(Placed("wall_stone", -1, 2, 6));
                s.BestWave = 17;
                s.MarkEverBuilt("tower_ground_archer");

                // A bank with plenty in it -- if anything were ever going to be taken from the
                // wallet, a full bank is where it would show.
                s.Wood = 12000;
                s.Iron = 9000;
                var r = s.Resources; r.Food = 7000; r.Crystals = 4242; s.Resources = r;

                // Clear the fixture's persisted pending/hp/stamp BEFORE Configure loads them: a
                // real save's lumbermill could be sitting at hp 0, which would hand the oracle an
                // ALREADY-BROKEN collector and quietly invalidate the not-broken case.
                for (int i = 0; i < prefKeys.Length; i++) PlayerPrefs.DeleteKey(prefKeys[i]);

                collectorGo = new GameObject("ResourceCollector (loss-stakes-oracle)");
                var collector = collectorGo.AddComponent<ResourceCollector>();
                // Awake/OnEnable do NOT run for a component added outside play mode, so Configure
                // (which loads state, seeds hp and registers) is what brings it online here.
                collector.Configure(FixtureId, maxHp: 100f);
                ResourceCollectorRegistry.Register(collector);
                if (ResourceCollectorRegistry.Get(FixtureId) != collector)
                { skipped = true; skipWhy = "the collector registry would not take the fixture"; return; }

                if (!SetPrivate(collector, "_pending", 800.0, out string pendWhy))
                { skipped = true; skipWhy = pendWhy; return; }

                NotBrokenReportsNothingCase(f, s, collector);
                BrokenCollectorCase(f, s, collector);
                StaleBreakCase(f, s, collector);
            }
            finally
            {
                if (installed)
                {
                    HeadlessState.TrySetInstance(priorState);
                    TrySetEconomyInstance(priorEconomy, out _);
                }
                HeadlessState.RestoreSave(hadSave, rawSave);

                if (collectorGo != null) Object.DestroyImmediate(collectorGo);
                if (ecoGo != null) Object.DestroyImmediate(ecoGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);

                foreach (var c in new List<ResourceCollector>(ResourceCollectorRegistry.All))
                    ResourceCollectorRegistry.Unregister(c);
                foreach (var c in parked) ResourceCollectorRegistry.Register(c);

                for (int i = 0; i < prefKeys.Length; i++)
                {
                    if (prefHad[i]) PlayerPrefs.SetString(prefKeys[i], prefWas[i]);
                    else PlayerPrefs.DeleteKey(prefKeys[i]);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>E, first half. A collector that never broke costs nothing, however full it is.</summary>
        private static void NotBrokenReportsNothingCase(List<string> f, GameState s, ResourceCollector collector)
        {
            if (collector.IsBroken)
            { f.Add("not-broken case: the fixture was already broken before the case ran"); return; }

            var bank = Bank.Of(s);
            var record = Overrun(TimeSource.NowUnixMs() - 1000.0);
            record.ResourcesLost = DefenseReportBuilder.BuildStakes(record);
            bool sealed_ = DefenseReportBuilder.ApplyStakes(record);

            if (sealed_ || record.ResourcesLost == null || !record.ResourcesLost.IsEmpty)
                f.Add("[HARD-RULE]not-broken: a report claimed a loot from a collector that never broke. " +
                      "The stake is the break, not the defeat -- a report that invents a loss is worse " +
                      "than no report.");
            Bank.AssertUnchanged(f, "not-broken", bank, s);
        }

        /// <summary>
        /// ⭐⭐ THE HEADLINE CASE. Break a wood collector holding 800:
        /// it loses exactly 400, the report says exactly 400, and ⛔ THE BANK DOES NOT MOVE.
        /// </summary>
        private static void BrokenCollectorCase(List<string> f, GameState s, ResourceCollector collector)
        {
            double pendingBefore = collector.PendingAmount;
            if (Mathf.RoundToInt((float)pendingBefore) != 800)
            { f.Add($"broken case: fixture pending is {pendingBefore}, expected the authored 800"); return; }

            var bank = Bank.Of(s);
            int campCooldowns = s.RaidCooldowns != null ? s.RaidCooldowns.Count : 0;
            int levelsBefore = LevelsOf(s);
            int bestWaveBefore = s.BestWave;
            int everBuiltBefore = s.EverBuiltStructureIds != null ? s.EverBuiltStructureIds.Count : 0;

            double siegeStart = TimeSource.NowUnixMs();

            // BREAK IT through the real damage surface -- no reflection on the theft itself.
            collector.ApplyContactDamage(10000f);

            if (!collector.IsBroken)
            { f.Add("broken case: the collector survived 10,000 damage -- the fixture never broke"); return; }

            // ── The collector's own account of what it lost. AUTHORED literal: 800 -> 400.
            int lost = Mathf.RoundToInt(collector.LastLootStolen);
            if (lost != 400)
                f.Add($"[HARD-RULE]broken case: a wood collector holding 800 lost {lost}; the ruling says 400 " +
                      "(half of what was still uncollected). RaidLootFraction stays 0.5 -- this literal is " +
                      "hand-worked on purpose so re-tuning it goes RED.");
            int pendingDrop = Mathf.RoundToInt((float)(pendingBefore - collector.PendingAmount));
            if (pendingDrop != 400)
                f.Add($"[HARD-RULE]broken case: the collector's pending fell by {pendingDrop}, not 400 -- the " +
                      "loot figure and the pending it came out of must be the same event");

            // ── The report.
            var record = Overrun(siegeStart);
            record.ResourcesLost = DefenseReportBuilder.BuildStakes(record);
            bool sealed_ = DefenseReportBuilder.ApplyStakes(record);
            var l = record.ResourcesLost;
            if (l == null) { f.Add("broken case: ApplyStakes left a null ledger"); return; }

            if (!sealed_ || l.IsEmpty)
                f.Add("broken case: a broken collector full of wood produced an EMPTY report -- the " +
                      "consequence loop has no consequence, which is the whole reason WO-1139 exists");
            if (!l.Applied)
                f.Add("broken case: the ledger is non-empty but not sealed -- a re-file could re-count it");
            if (l.StakesRuleId != StakeRules.RuleId)
                f.Add($"broken case: StakesRuleId is '{l.StakesRuleId}', expected '{StakeRules.RuleId}' -- " +
                      "every report must stay self-describing about which ruling produced it");

            // ⭐ C. ONE NUMBER. The report figure IS the collector's figure.
            if (l.Wood != lost)
                f.Add($"[ONE-NUMBER]broken case: the REPORT says {l.Wood} wood, the COLLECTOR lost {lost}. " +
                      "These are supposed to be the same value read from the same field, not two " +
                      "computations that agree.");
            if (l.Wood != 400)
                f.Add($"[HARD-RULE]broken case: the report says {l.Wood} wood, hand-worked answer 400");
            if (l.Iron != 0 || l.Food != 0)
                f.Add($"broken case: a wood collector's break filled other buckets (i{l.Iron} f{l.Food}) -- " +
                      "the loot must be bucketed by the COLLECTOR'S OWN harvest resource");

            // The per-bucket sum must equal what the collectors say they lost. Mismatch = drift.
            int collectorSum = 0;
            foreach (var c in ResourceCollectorRegistry.All)
                if (c != null && c.IsBroken && c.IsLootable && c.LastLootStolenAtUnixMs >= siegeStart)
                    collectorSum += Mathf.RoundToInt(c.LastLootStolen);
            if (l.Wood + l.Iron + l.Food != collectorSum)
                f.Add($"[ONE-NUMBER]broken case: the ledger sums to {l.Wood + l.Iron + l.Food} but the broken " +
                      $"collectors lost {collectorSum} between them");

            // ⛔ B. Crystals, both halves.
            if (l.Crystals != 0 || l.Magic != 0)
                f.Add($"[HARD-RULE]broken case: the report CLAIMS c{l.Crystals} m{l.Magic} were taken");

            // ⛔⛔ A. THE POINT OF THE WHOLE REWRITE. Nothing debited the bank.
            Bank.AssertUnchanged(f, "broken case [DOUBLE-CHARGE]", bank, s);

            // ⛔ D. Nothing permanent moved.
            if (LevelsOf(s) != levelsBefore)
                f.Add($"[HARD-RULE]broken case: a structure DOWNGRADED across the loss ({levelsBefore} -> " +
                      $"{LevelsOf(s)}). The ruling: no building downgrade, ever.");
            if (s.BaseLayout.Count != 2)
                f.Add($"[HARD-RULE]broken case: the base layout lost a structure ({s.BaseLayout.Count} of 2 " +
                      "left) -- no permanent progress is ever destroyed");
            if (s.BestWave != bestWaveBefore)
                f.Add($"[HARD-RULE]broken case: BestWave moved {bestWaveBefore} -> {s.BestWave}");
            int everBuiltAfter = s.EverBuiltStructureIds != null ? s.EverBuiltStructureIds.Count : 0;
            if (everBuiltAfter != everBuiltBefore)
                f.Add("[HARD-RULE]broken case: the ever-built set changed across a loss");
            int campsAfter = s.RaidCooldowns != null ? s.RaidCooldowns.Count : 0;
            if (campsAfter != campCooldowns)
                f.Add("[HARD-RULE]broken case: camp cooldown state changed -- a cleared camp stays cleared");

            // ⛔ F. Idempotence.
            int ledgerWood = l.Wood;
            var bankAfterFirst = Bank.Of(s);
            if (DefenseReportBuilder.ApplyStakes(record))
                f.Add("[HARD-RULE]idempotence: ApplyStakes sealed the SAME report a second time");
            if (l.Wood != ledgerWood)
                f.Add("[HARD-RULE]idempotence: the second call rewrote the ledger the player already read");
            Bank.AssertUnchanged(f, "idempotence", bankAfterFirst, s);
        }

        /// <summary>
        /// E, second half. The SAME still-broken collector must not be re-reported by the NEXT
        /// siege. A destroyed collector is never repairable (WO-753), so it stands as a shell
        /// carrying its loot figure all session -- without the break stamp every later report
        /// would re-announce the same robbery and the player would think they were robbed twice.
        /// </summary>
        private static void StaleBreakCase(List<string> f, GameState s, ResourceCollector collector)
        {
            if (!collector.IsBroken)
            { f.Add("stale-break case: the fixture is not broken, so there is nothing stale to skip"); return; }
            if (collector.LastLootStolen <= 0f)
            { f.Add("stale-break case: the fixture carries no loot figure to re-report"); return; }

            var bank = Bank.Of(s);

            // A LATER siege: it started after the break stamp.
            var later = Overrun(collector.LastLootStolenAtUnixMs + 1.0);
            later.ResourcesLost = DefenseReportBuilder.BuildStakes(later);
            bool sealed_ = DefenseReportBuilder.ApplyStakes(later);

            if (sealed_ || later.ResourcesLost == null || !later.ResourcesLost.IsEmpty)
                f.Add($"[HARD-RULE]stale-break: the NEXT siege re-reported an EARLIER siege's loot " +
                      $"(w{later.ResourcesLost?.Wood}). A broken collector is never repairable, so it " +
                      "keeps its loot figure all session -- only breaks at or after the record's " +
                      "StartedAtUnixMs may count, or the player is told they were robbed again.");
            Bank.AssertUnchanged(f, "stale-break", bank, s);

            // And a HELD defence over the same shell reports nothing either.
            var held = DefenseOutcomeRecord.NewEmpty();
            held.Outcome = DefenseOutcome.Held;
            held.StartedAtUnixMs = collector.LastLootStolenAtUnixMs + 1.0;
            held.ResourcesLost = DefenseReportBuilder.BuildStakes(held);
            DefenseReportBuilder.ApplyStakes(held);
            if (held.ResourcesLost == null || !held.ResourcesLost.IsEmpty)
                f.Add("[HARD-RULE]held: a cleared defence in which nothing broke produced a non-empty ledger");
            Bank.AssertUnchanged(f, "held", bank, s);
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Every wallet number that a siege could conceivably move, snapshotted.</summary>
        private struct Bank
        {
            public int Wood, Iron, Food, Crystals;

            public static Bank Of(GameState s) => new Bank
            {
                Wood = s.Wood,
                Iron = s.Iron,
                Food = s.Resources.Food,
                Crystals = s.Resources.Crystals,
            };

            /// <summary>
            /// ⛔⛔ THE DOUBLE-CHARGE GUARD. The collector already removed the resources from its
            /// own pending when it broke; a bank debit on top of that charges the player TWICE for
            /// one siege. Nothing in the siege path may move a single point of the wallet.
            /// </summary>
            public static void AssertUnchanged(List<string> f, string tag, Bank before, GameState s)
            {
                var now = Of(s);
                if (now.Wood == before.Wood && now.Iron == before.Iron &&
                    now.Food == before.Food && now.Crystals == before.Crystals) return;

                f.Add($"[HARD-RULE]{tag}: THE BANK MOVED across a siege report " +
                      $"(w{before.Wood}->{now.Wood} i{before.Iron}->{now.Iron} " +
                      $"f{before.Food}->{now.Food} c{before.Crystals}->{now.Crystals}). " +
                      "Owner ruling 2026-08-22: COLLECTOR LOOTING ONLY, NO BANK THEFT. The collector " +
                      "ALREADY subtracted the loot from its own pending -- a wallet debit here charges " +
                      "the player twice for one siege. Nothing in the siege path may debit the wallet.");
            }
        }

        private static DefenseOutcomeRecord Overrun(double startedAtUnixMs)
        {
            var r = DefenseOutcomeRecord.NewEmpty();
            r.Outcome = DefenseOutcome.Overrun;
            r.WaveId = 9;
            r.StartedAtUnixMs = startedAtUnixMs;
            return r;
        }

        private static PlacedStructureData Placed(string id, int x, int z, int level)
        {
            var p = new PlacedStructureData();
            p.itemId = id; p.cellX = x; p.cellZ = z; p.yawSteps = 0; p.level = level;
            return p;
        }

        /// <summary>Sum of every placed structure's level — the cheapest proof that nothing downgraded.</summary>
        private static int LevelsOf(GameState s)
        {
            int total = 0;
            if (s == null || s.BaseLayout == null) return 0;
            for (int i = 0; i < s.BaseLayout.Count; i++) total += s.BaseLayout[i].level;
            return total;
        }

        /// <summary>
        /// Seeds a private field on the fixture collector. Reflection is confined to SEEDING the
        /// fixture — never to the behaviour under test: the break goes through the real
        /// <c>ApplyContactDamage</c> surface and the loot is read off the real public property.
        /// </summary>
        private static bool SetPrivate(object target, string field, object value, out string err)
        {
            err = null;
            var fld = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fld == null)
            {
                err = $"ResourceCollector.{field} not found by reflection (the fixture seam changed)";
                return false;
            }
            fld.SetValue(target, value);
            return true;
        }

        /// <summary>Sets the private static backing field behind <c>EconomyService.Instance</c>.
        /// Mirrors HeadlessState.TrySetInstance; false (named) only if the seam is gone.</summary>
        private static bool TrySetEconomyInstance(EconomyService svc, out string err)
        {
            err = null;
            var fld = typeof(EconomyService).GetField("<Instance>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (fld == null)
            {
                err = "EconomyService.Instance backing field not found by reflection (singleton seam changed)";
                return false;
            }
            fld.SetValue(null, svc);
            return true;
        }
    }
}
