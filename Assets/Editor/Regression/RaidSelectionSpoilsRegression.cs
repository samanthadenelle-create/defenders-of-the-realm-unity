// =============================================================================
// RaidSelectionSpoilsRegression - the Raid Selection rows say what a raid PAYS,
// the pips carry data or nothing, and a camp above the army says so in words.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WO-1402 (merged UI review 2026-09-05 row 1). WHAT WAS BROKEN, SEEN ON THE FRAME
// Builds/ui-capture/RaidSelection_2670x1200.png (09-05 07:02):
//   1. Rows read "Wood walls . 9 defenders" + a difficulty word and a "- x1.5 Loot"
//      hint. No row carried a resource word or number: nothing said what a win is
//      WORTH. RaidSelectionVM.cs:133 read rewardMultiplier only - the camp authors a
//      multiplier, not a loot list; the real spoils were computed once, at settle
//      (RaidScoring.LootFor -> ComputeLoot, surfaced by EndStateVM.FromRaidVictory).
//   2. Three identical gold pips (StarRatingRow.Build(.., 3, 3, ..)) sat on every row
//      and varied on none - they carried nothing.
//   3. Nothing compared the camp to the army the player has. The scout report on the
//      deploy screen compares ("Garrison: 9 defenders - you field 3", WO-1389); the
//      selection rows did not.
//
// WHAT THIS PINS (behaviour first, source lint last):
//   A. Every row VM exposes a non-empty spoils line: "Spoils: ~<n> wood, ~<n> iron,
//      ~<n> gold" - starts with the prefix, names wood AND iron, every number is a
//      "~" estimate (owner ruling: a range/estimate, never exact), ASCII only.
//   B. ONE PRODUCER. The line equals FormatSpoils(RaidScoring.EstimateSpoils(id, mult)),
//      and EstimateSpoils equals ComputeLoot at the 3-star rung with the live tunable
//      bases - i.e. the settle payout's own arithmetic, no second table. A x1.5 camp
//      estimates 1.5x the wood of a x1.0 camp (wood and iron ride the multiplier; gold
//      does not - RaidLootTunables header).
//   C. THE ARMY WORD. With 0 fieldable troops every garrisoned camp reads
//      "LOCKED - needs Army <garrison>"; with an army that covers the garrison the word
//      is absent; with the army UNKNOWN (-1, headless) the word is absent - a frame must
//      never print a lock it cannot prove.
//   D. THE PIPS. ShowStarPips is false with no rating producer (today's wiring), false
//      when every known rating is identical, true only when known ratings differ.
//   E. SOURCE LINT (the MVVM seam): RaidSelectionVM calls RaidScoring.EstimateSpoils and
//      types no spoils literal; RaidScoring.LootFor and .EstimateSpoils both route
//      through ProjectLoot; RaidSelectionScreen reads SpoilsLineFor / ArmyLockWordFor /
//      ShowStarPips, gates StarRatingRow.Build behind ShowStarPips, and types neither
//      "Spoils:" nor "LOCKED" itself (the VM owns the words).
//   F. THE BANDS ARE TALL ENOUGH TO RENDER. Every entry of RaidSelectionScreen.CardBands
//      must satisfy HavePx >= NeedsPx. This case exists because on 2026-09-05 the WO-1402
//      spoils line shipped INVISIBLE and no suite noticed: the VM composed it
//      (Builds/cap2:13574), the View built it (cap2:13909), and TMP's Ellipsis overflow
//      then culled the entire line because its band was 22.7 px and a 22 pt line needs
//      ~29. FitSingleLine cannot save it - the kit clamps fontSizeMin UP to the label's
//      own fontSize, so there is no shrink room - and the runtime relax guard does not
//      run in a headless capture. Four of the card's five rows were gone (clock, lock
//      sentence, spoils, canon flavour) and only a pixel scan of the screenshot found it.
//      A band is not "tight" when it fails this: it renders NOTHING, silently.
//
// MUTATIONS THIS SUITE CATCHES (named, so the RED is reproducible):
//   M1. In RaidSelectionVM.ArmyLockWord, delete the `garrison > deployableTroops`
//       compare (return null) -> C fails on every garrisoned camp.
//   M2. In RaidSelectionVM.Rebuild, replace `EstimateSpoils(d)` with a literal basket
//       (`new ResourceCost(wood: 600, iron: 250)`) -> B fails (line != the scorer's
//       estimate) and E fails (no RaidScoring.EstimateSpoils call).
//   M3. In RaidSelectionScreen.CreateRaidCard, drop the `if (showPips)` guard so the
//       pips draw unconditionally -> E fails (Build reached before ShowStarPips read).
//   M4. In RaidSelectionScreen, restore the shipped-and-invisible geometry - set
//       CardHeightPx back to 142f, or SpoilsBandY0/Y1 back to 0.18f/0.34f -> F fails
//       naming the band, its px, and the px its font needs. (At 142f with today's
//       fractions FOUR of the five bands red: title 32.7/38.6, scout 24.9/29.1, lock
//       24.9/29.1, spoils 24.9/29.1 - flavour squeaks through at 24.9/24.4 because 18 pt
//       is the cheapest row. That is why the card is 178: it cannot seat five legible
//       rows at 142, and the four that fail are four rows the player cannot see.)
//
// Contract mirrors the other suites - Run(out string reason): true = pass.
// Orchestrator registration (DataRegression.RunAll), covenant style:
//   if (!RaidSelectionSpoilsRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-selection-spoils] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class RaidSelectionSpoilsRegression
    {
        private const string VmRel     = "_Modules/Village/Hero/RaidSelectionVM.cs";
        private const string ScreenRel = "_Modules/Village/Hero/RaidSelectionScreen.cs";
        private const string ScorerRel = "_Modules/Village/Troops/RaidScoring.cs";

        // The ladder as scene-configs.json authors it on 2026-09-05 (garrison headcounts are
        // the composition sums: 7+2 / 4+2+6+3 / 7+5+7 / 7+5+7). Fakes, so the suite proves the
        // VM's arithmetic and not the catalog's presence; the catalog is pinned elsewhere
        // (RaidEscalationRegression A/B).
        private sealed class Camp
        {
            public string Id; public string Name; public float Mult; public int Unlock; public int[] Garrison;
        }

        private static readonly Camp[] Ladder =
        {
            new Camp { Id = "raider_camp_small",  Name = "The Forsaken Camp",   Mult = 1.0f, Unlock = 0,  Garrison = new[] { 7, 2 } },
            new Camp { Id = "fortified_garrison", Name = "The Broken Garrison", Mult = 1.5f, Unlock = 3,  Garrison = new[] { 4, 2, 6, 3 } },
            new Camp { Id = "mage_enclave",       Name = "The Veiled Enclave",  Mult = 2.2f, Unlock = 10, Garrison = new[] { 7, 5, 7 } },
            new Camp { Id = "iron_bastion",       Name = "The Iron Bastion",    Mult = 2.2f, Unlock = 20, Garrison = new[] { 7, 5, 7 } },
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- RAID SELECTION SPOILS (WO-1402: rows say what a raid pays; pips carry data or nothing; army lock is a word) ---");

            var defs = BuildDefs();

            CheckSpoilsLines(defs, failures, log);          // A
            CheckOneProducer(defs, failures, log);          // B
            CheckArmyLockWord(defs, failures, log);         // C
            CheckPipsGate(defs, failures, log);             // D
            CheckSourceSeams(failures, log);                // E
            CheckCardBands(failures, log);                  // F

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "RAID_SELECTION_SPOILS_OK");
                reason = "RAID SELECTION SPOILS OK - every row carries a '~' spoils line from RaidScoring's own " +
                         "estimate (one producer), the pips hide until ratings vary, and a camp above the army " +
                         "reads 'LOCKED - needs Army N' in words";
                return true;
            }

            reason = "raid-selection-spoils: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "RAID_SELECTION_SPOILS_FAIL: " + reason);
            return false;
        }

        private static List<SceneConfigDef> BuildDefs()
        {
            var defs = new List<SceneConfigDef>();
            foreach (var c in Ladder)
            {
                var comp = new List<GarrisonUnitDef>();
                for (int i = 0; i < c.Garrison.Length; i++)
                    comp.Add(new GarrisonUnitDef { enemyId = "fake-" + i, count = c.Garrison[i] });
                defs.Add(new SceneConfigDef
                {
                    id = c.Id, displayName = c.Name, ownership = "Enemy", sceneName = "RaidBase_" + c.Id,
                    unlockVictories = c.Unlock, rewardMultiplier = c.Mult, wallTier = "Wood",
                    garrison = new GarrisonDef { composition = comp },
                });
            }
            return defs;
        }

        private static int GarrisonOf(Camp c)
        {
            int n = 0;
            foreach (var g in c.Garrison) n += g;
            return n;
        }

        // -- A: every row says what it pays ---------------------------------------------
        private static void CheckSpoilsLines(List<SceneConfigDef> defs, List<string> failures, StringBuilder log)
        {
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, deployableTroops: 0))
            {
                foreach (var c in Ladder)
                {
                    string line = vm.SpoilsLineFor(c.Id);
                    if (string.IsNullOrEmpty(line))
                    {
                        failures.Add($"A: '{c.Id}' has NO spoils line - the row still says only how hard the camp is, " +
                                     "never what a raid pays (the merged-review defect this WO exists for)");
                        continue;
                    }
                    if (!line.StartsWith(RaidSelectionVM.SpoilsPrefix, StringComparison.Ordinal))
                        failures.Add($"A: '{c.Id}' spoils line does not start with '{RaidSelectionVM.SpoilsPrefix}' - found \"{line}\"");
                    if (line.IndexOf(" wood", StringComparison.Ordinal) < 0)
                        failures.Add($"A: '{c.Id}' spoils line names no WOOD - \"{line}\"");
                    if (line.IndexOf(" iron", StringComparison.Ordinal) < 0)
                        failures.Add($"A: '{c.Id}' spoils line names no IRON - \"{line}\"");
                    if (line.IndexOf('~') < 0)
                        failures.Add($"A: '{c.Id}' spoils line carries no '~' - the owner ruled a RANGE/estimate, never exact - \"{line}\"");
                    foreach (char ch in line)
                        if (ch > 127) { failures.Add($"A: '{c.Id}' spoils line carries a non-ASCII character (tofu in the build font) - \"{line}\""); break; }
                    // An exact number would be a lie the settle screen has to keep: every amount
                    // must be rounded to the estimate grid (50 below 1000, 100 at/above).
                    foreach (var token in line.Substring(RaidSelectionVM.SpoilsPrefix.Length).Split(','))
                    {
                        string t = token.Trim();
                        int tilde = t.IndexOf('~');
                        int sp = t.IndexOf(' ');
                        if (tilde < 0 || sp < tilde) continue;
                        if (int.TryParse(t.Substring(tilde + 1, sp - tilde - 1), out int n) && RaidSelectionVM.Approx(n) != n)
                            failures.Add($"A: '{c.Id}' spoils amount {n} is not on the estimate grid (Approx({n}) = {RaidSelectionVM.Approx(n)}) - \"{line}\"");
                    }
                    log.AppendLine($"OK: '{c.Id}' -> \"{line}\"");
                }
            }
        }

        // -- B: one producer - the settle payout's own formula --------------------------
        private static void CheckOneProducer(List<SceneConfigDef> defs, List<string> failures, StringBuilder log)
        {
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true))
            {
                foreach (var def in defs)
                {
                    var est = RaidScoring.EstimateSpoils(def.id, def.rewardMultiplier);
                    string expected = RaidSelectionVM.FormatSpoils(est);
                    string actual = vm.SpoilsLineFor(def.id);
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                        failures.Add($"B: '{def.id}' row line \"{actual}\" != FormatSpoils(RaidScoring.EstimateSpoils) " +
                                     $"\"{expected}\" - the row is fed by something other than the scorer's estimate (a second producer)");

                    // The estimate IS ComputeLoot at the 3-star rung with the live bases - the
                    // exact arithmetic LootFor pays through at settle.
                    var settle = RaidScoring.ComputeLoot(RaidScoring.EstimateStars, 0f,
                        RaidLootTunables.CrystalsBase, 0, RaidLootTunables.CrystalsPerStar, 0,
                        def.rewardMultiplier, RaidLootTunables.WoodBase, RaidLootTunables.IronBase,
                        RaidLootTunables.CoinsBaseFor(def.id));
                    if (settle.Wood != est.Wood || settle.Iron != est.Iron || settle.Coins != est.Coins)
                        failures.Add($"B: '{def.id}' EstimateSpoils = {est.Wood}w/{est.Iron}i/{est.Coins}g but ComputeLoot at the " +
                                     $"same rung = {settle.Wood}w/{settle.Iron}i/{settle.Coins}g - the preview and the payout have " +
                                     "forked; there must be exactly one formula");
                    if (est.Wood <= 0 || est.Iron <= 0)
                        failures.Add($"B: '{def.id}' estimate pays {est.Wood} wood / {est.Iron} iron - the tunable rail answered " +
                                     "zero, so the line would be empty on a real row (RaidLootTunables.WoodBase/IronBase)");
                }

                // Wood and iron ride the camp multiplier (RaidLootTunables header); gold does not.
                var flat = RaidScoring.EstimateSpoils("raider_camp_small", 1.0f);
                var hard = RaidScoring.EstimateSpoils("raider_camp_small", 1.5f);
                if (hard.Wood != Mathf.RoundToInt(flat.Wood * 1.5f))
                    failures.Add($"B: a x1.5 camp estimates {hard.Wood} wood against {flat.Wood} at x1.0 - the estimate does not " +
                                 "ride rewardMultiplier the way the settle payout does");
                if (hard.Coins != flat.Coins)
                    failures.Add($"B: gold estimate changed with the multiplier ({flat.Coins} -> {hard.Coins}) - gold escalates through " +
                                 "its per-camp base and must NOT ride rewardMultiplier (RaidLootTunables header)");
                log.AppendLine($"OK: estimate == ComputeLoot at the {RaidScoring.EstimateStars}-star rung; x1.0 -> {flat.Wood}w, x1.5 -> {hard.Wood}w, gold flat {flat.Coins}");
            }
        }

        // -- C: the army word ---------------------------------------------------------------
        private static void CheckArmyLockWord(List<SceneConfigDef> defs, List<string> failures, StringBuilder log)
        {
            // Army 0: every garrisoned camp is above it.
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, deployableTroops: 0))
            {
                foreach (var c in Ladder)
                {
                    string expected = RaidSelectionVM.ArmyLockPrefix + GarrisonOf(c);
                    string word = vm.ArmyLockWordFor(c.Id);
                    if (!string.Equals(word, expected, StringComparison.Ordinal))
                        failures.Add($"C: with 0 fieldable troops '{c.Id}' (garrison {GarrisonOf(c)}) reads " +
                                     $"\"{word ?? "(null)"}\" - expected \"{expected}\"; the row must SAY the camp is above the army");
                }
                log.AppendLine("OK: army 0 -> every garrisoned camp carries 'LOCKED - needs Army <garrison>'");
            }

            // Army 9: the Forsaken Camp (9) is covered, the Broken Garrison (15) is not.
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, deployableTroops: 9))
            {
                if (vm.ArmyLockWordFor("raider_camp_small") != null)
                    failures.Add("C: with 9 fieldable troops 'raider_camp_small' (garrison 9) still reads the lock word - " +
                                 "the compare is garrison > army, and 9 is not above 9");
                if (vm.ArmyLockWordFor("fortified_garrison") != RaidSelectionVM.ArmyLockPrefix + "15")
                    failures.Add("C: with 9 fieldable troops 'fortified_garrison' (garrison 15) does not read 'LOCKED - needs Army 15'");
                log.AppendLine("OK: army 9 -> camp of 9 open, camp of 15 locked in words");
            }

            // Army covers everything: no words.
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, deployableTroops: 99))
            {
                foreach (var c in Ladder)
                    if (vm.ArmyLockWordFor(c.Id) != null)
                        failures.Add($"C: with 99 fieldable troops '{c.Id}' still reads \"{vm.ArmyLockWordFor(c.Id)}\" - a covered camp must carry no lock word");
            }

            // Army UNKNOWN (headless / unwired): no words, because none can be proven.
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true))
            {
                if (vm.DeployableTroops != RaidSelectionVM.Unknown)
                    failures.Add($"C: an unwired VM reports DeployableTroops={vm.DeployableTroops}, expected Unknown ({RaidSelectionVM.Unknown})");
                foreach (var c in Ladder)
                    if (vm.ArmyLockWordFor(c.Id) != null)
                        failures.Add($"C: with the army UNKNOWN '{c.Id}' reads \"{vm.ArmyLockWordFor(c.Id)}\" - a headless frame " +
                                     "must never print a lock it cannot prove");
                log.AppendLine("OK: army unknown / army 99 -> no lock word on any row");
            }

            // The word is ASCII and the prefix is the one the WO spells.
            if (RaidSelectionVM.ArmyLockPrefix != "LOCKED - needs Army ")
                failures.Add($"C: ArmyLockPrefix is \"{RaidSelectionVM.ArmyLockPrefix}\" - WO-1402 spells it 'LOCKED - needs Army N'");
        }

        // -- D: the pips carry data or nothing ----------------------------------------------
        private static void CheckPipsGate(List<SceneConfigDef> defs, List<string> failures, StringBuilder log)
        {
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true))
                if (vm.ShowStarPips)
                    failures.Add("D: ShowStarPips is TRUE with no rating producer - three identical pips on every row carry nothing " +
                                 "and must not draw (merged UI review row 1)");

            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, bestStars: _ => 3))
                if (vm.ShowStarPips)
                    failures.Add("D: ShowStarPips is TRUE when every camp is rated 3 - uniform ratings say nothing, hide the pips");

            var varied = new Dictionary<string, int> { { "raider_camp_small", 3 }, { "fortified_garrison", 1 }, { "mage_enclave", 0 }, { "iron_bastion", -1 } };
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, bestStars: id => varied.TryGetValue(id, out var s) ? s : -1))
            {
                if (!vm.ShowStarPips)
                    failures.Add("D: ShowStarPips is FALSE when ratings differ (3/1/0/unknown) - the pips are the one place the " +
                                 "player's own record per camp would show, and they stay hidden");
                if (vm.BestStarsFor("fortified_garrison") != 1)
                    failures.Add($"D: BestStarsFor('fortified_garrison') = {vm.BestStarsFor("fortified_garrison")}, provider said 1");
                if (vm.BestStarsFor("iron_bastion") != RaidSelectionVM.Unknown)
                    failures.Add($"D: BestStarsFor('iron_bastion') = {vm.BestStarsFor("iron_bastion")}, expected Unknown for a -1 rating");
            }

            // A single known rating cannot vary.
            using (var vm = new RaidSelectionVM(defs, null, 20, _ => true, bestStars: id => id == "raider_camp_small" ? 2 : -1))
                if (vm.ShowStarPips)
                    failures.Add("D: ShowStarPips is TRUE with only ONE rated camp - one number cannot vary");

            log.AppendLine("OK: pips hidden with no producer / uniform / one rating; shown when ratings differ");
        }

        // -- F: the card's bands can actually seat their rows ---------------------------------
        //
        // ⛔ THE CASE THAT WOULD HAVE CAUGHT A SHIPPED-INVISIBLE FEATURE. A/B/E all passed on
        // 2026-09-05 while the spoils line was not on the screen at all: they prove the STRING,
        // and a string that is composed, handed to the View and built into a label is still not
        // a string the player can read. TMP culls a whole Ellipsis line whose box cannot seat in
        // its rect, and ElarionUiKit.FitSingleLine has no shrink room to give (it clamps
        // fontSizeMin up to the label's authored fontSize). So the last mile is arithmetic on
        // the live constants - not a source-text lint, which would go stale the moment someone
        // renamed a band.
        private static void CheckCardBands(List<string> failures, StringBuilder log)
        {
            var bands = RaidSelectionScreen.CardBands;
            if (bands == null || bands.Length == 0)
            {
                failures.Add("F: RaidSelectionScreen.CardBands is empty - the card's band table is the ONLY " +
                             "thing standing between an authored fraction and a row that renders nothing. " +
                             "Every text band on the card belongs in it");
                return;
            }
            foreach (var b in bands)
            {
                if (b.Y1 <= b.Y0)
                    failures.Add($"F: band '{b.Name}' is inverted or empty (Y0 {b.Y0:0.###} >= Y1 {b.Y1:0.###})");
                else if (b.HavePx < b.NeedsPx)
                    failures.Add($"F: band '{b.Name}' gives {b.HavePx:0.0}px but its {b.FontPt}pt row needs " +
                                 $"{b.NeedsPx:0.0}px - TMP's Ellipsis overflow will CULL THE WHOLE LINE and the " +
                                 "row will be blank on the card with no error, no exception and no trace (this is " +
                                 "exactly how the WO-1402 spoils line, the clock, the lock sentence and the canon " +
                                 "flavour line all shipped invisible on 2026-09-05). Raise CardHeightPx or widen " +
                                 "the band - never shrink the font below the kit's readable floor");
            }

            // The bands must also not OVERLAP, or two rows paint over each other - a different
            // way for the same words to become unreadable.
            for (int i = 0; i < bands.Length; i++)
                for (int j = i + 1; j < bands.Length; j++)
                    if (bands[i].Y0 < bands[j].Y1 && bands[j].Y0 < bands[i].Y1)
                        failures.Add($"F: bands '{bands[i].Name}' ({bands[i].Y0:0.###}-{bands[i].Y1:0.###}) and " +
                                     $"'{bands[j].Name}' ({bands[j].Y0:0.###}-{bands[j].Y1:0.###}) OVERLAP - two rows " +
                                     "of text on the same pixels");

            var sb = new StringBuilder("OK: card bands seat their rows -");
            foreach (var b in bands) sb.Append($" {b.Name} {b.HavePx:0.0}/{b.NeedsPx:0.0}px");
            log.AppendLine(sb.ToString());
        }

        // -- E: the MVVM seams, at source ----------------------------------------------------
        private static void CheckSourceSeams(List<string> failures, StringBuilder log)
        {
            string vm = ReadStripped(VmRel, failures);
            string screen = ReadStripped(ScreenRel, failures);
            string scorer = ReadStripped(ScorerRel, failures);
            if (vm == null || screen == null || scorer == null) return;

            // VM: calls the scorer's estimate, types no spoils literal.
            if (vm.IndexOf("RaidScoring.EstimateSpoils(", StringComparison.Ordinal) < 0)
                failures.Add("E: RaidSelectionVM.cs no longer calls RaidScoring.EstimateSpoils( - the row's number must come from the " +
                             "settle payout's own formula, never a literal or a second table (mutation M2)");
            if (vm.IndexOf("\"Spoils: ~", StringComparison.Ordinal) >= 0)
                failures.Add("E: RaidSelectionVM.cs types a literal \"Spoils: ~...\" - the line is COMPOSED from the estimate, never typed");

            // Scorer: payout and preview share ProjectLoot.
            string lootFor = Slice(scorer, "public ResourceCost LootFor(", "public string ResolveCampConfigId(");
            // Comment lines are stripped, so the slice ends at the next CODE token after the method.
            string estimate = Slice(scorer, "public static ResourceCost EstimateSpoils(", "[RuntimeInitializeOnLoadMethod");
            if (lootFor == null || lootFor.IndexOf("ProjectLoot(", StringComparison.Ordinal) < 0)
                failures.Add("E: RaidScoring.LootFor no longer routes through ProjectLoot( - the settle payout and the row estimate " +
                             "have forked into two formulas");
            if (estimate == null || estimate.IndexOf("ProjectLoot(", StringComparison.Ordinal) < 0)
                failures.Add("E: RaidScoring.EstimateSpoils no longer routes through ProjectLoot( - the row estimate is its own arithmetic");

            // Screen: renders the VM's strings, owns none of them, gates the pips.
            if (screen.IndexOf("_vm.SpoilsLineFor(", StringComparison.Ordinal) < 0)
                failures.Add("E: RaidSelectionScreen.cs does not read _vm.SpoilsLineFor( - the spoils line is not painted");
            if (screen.IndexOf("_vm.ArmyLockWordFor(", StringComparison.Ordinal) < 0)
                failures.Add("E: RaidSelectionScreen.cs does not read _vm.ArmyLockWordFor( - the army lock word is not painted");
            int pipsGate = screen.IndexOf("_vm.ShowStarPips", StringComparison.Ordinal);
            int pipsBuild = screen.IndexOf("StarRatingRow.Build(", StringComparison.Ordinal);
            if (pipsGate < 0)
                failures.Add("E: RaidSelectionScreen.cs never reads _vm.ShowStarPips - the pips draw ungated (mutation M3)");
            else if (pipsBuild >= 0 && pipsBuild < pipsGate)
                failures.Add("E: RaidSelectionScreen.cs calls StarRatingRow.Build( BEFORE reading _vm.ShowStarPips - the pips are not gated (mutation M3)");
            if (screen.IndexOf("\"Spoils:", StringComparison.Ordinal) >= 0)
                failures.Add("E: RaidSelectionScreen.cs types \"Spoils: - the View owns no words; the VM composes the line");
            if (screen.IndexOf("\"LOCKED", StringComparison.Ordinal) >= 0)
                failures.Add("E: RaidSelectionScreen.cs types \"LOCKED - the View owns no words; RaidSelectionVM.ArmyLockPrefix does");

            log.AppendLine("OK: VM -> RaidScoring.EstimateSpoils; LootFor + EstimateSpoils -> ProjectLoot; screen renders VM strings, pips gated");
        }

        // Source with every //-comment line and every /* */ block removed, so a comment that
        // quotes a forbidden literal (this suite's own header quotes several) cannot trip a lint
        // or satisfy one.
        private static string ReadStripped(string rel, List<string> failures)
        {
            string path = Path.Combine(Application.dataPath, rel);
            if (!File.Exists(path)) { failures.Add("E: " + rel + " missing at " + path); return null; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { failures.Add("E: could not read " + rel + " (" + e.GetType().Name + ")"); return null; }

            var sb = new StringBuilder(text.Length);
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw;
                int slash = line.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0)
                {
                    // Keep a "//" that sits inside a string literal (an odd count of quotes before it).
                    int quotes = 0;
                    for (int i = 0; i < slash; i++) if (line[i] == '"') quotes++;
                    if (quotes % 2 == 0) line = line.Substring(0, slash);
                }
                sb.Append(line).Append('\n');
            }
            string s = sb.ToString();
            int open;
            while ((open = s.IndexOf("/*", StringComparison.Ordinal)) >= 0)
            {
                int close = s.IndexOf("*/", open + 2, StringComparison.Ordinal);
                if (close < 0) break;
                s = s.Remove(open, close + 2 - open);
            }
            return s;
        }

        private static string Slice(string text, string fromMarker, string toMarker)
        {
            int a = text.IndexOf(fromMarker, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = text.IndexOf(toMarker, a + fromMarker.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }
    }
}
