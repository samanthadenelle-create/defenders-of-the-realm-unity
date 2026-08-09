// =============================================================================
// EchoSpecializationRegression — the §2c permission-gate oracle for WO-738/830
// (Echo specialization + affinity/synergy). Headless, data-decidable, no play-mode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Asserts the COMMITTED runtime contract of the Echo affinity/synergy model — real
// objects in through the actual game path (reload the balance catalog, stand up a
// throwaway GameState the way OfflineHarvestRegression/CoreSaveContractRegression do),
// assert the response, one marker out. Mirrors MonetizationCovenantRegression's shape:
//   public static bool Run(out string reason)   →  wired into DataRegression.RunAll.
//
// SEVEN ASSERTION GROUPS (all data-decidable headless — none deferred):
//   1. Catalog integrity  — EchoRosterCatalog: 6 entries, ALL PreferredLane=Harvest
//      (WO-830), the full affinity table (Aldwin→Food, Elowen→Wood, Corvin→Gold,
//      Bran→Crystals, Doran→Iron, Maren→Crystals — crystals the ONE doubled affinity),
//      HarvestResource kept for the 3 classic resources, EmergeLine present (WO-831).
//   2. Balance load       — EchoBalanceCatalog loads via CanonicalJson; the
//      Resources/StreamingAssets dual-copy is byte-identical; the WO-830 tunables
//      (0.03 / 0.02 / 0.20 / 0.01 / hiddenTri 0.25 -- owner "+5% not 55%" ruling
//      2026-08-02, was 0.40 / 0.15 / 0.20 / 0.05), the 3 crossBonuses pairs, and
//      the crystals-slowest rate law (Bran+Maren combined < every other single rate).
//   3. Token grammar      — AssignHarvest+SetLevel produce a "resource:level" token that
//      round-trips; legacy "wood,iron,food,idle" reads Harvest with the RESOURCE
//      preserved at L1; a v33 generic "harvest:N" defaults to the AFFINITY resource;
//      a stored non-pickable "crafting:1" still reads back (read-compat).
//   4. Bonus math         — all-matched assignment: AggregateHarvestMultiplier equals the
//      hand-computed formula INCLUDING pair bonuses + six-set + the HIDDEN tri term;
//      the tri term is APPLIED but NOT in any ReadoutFor().BonusPct (applied ≠ displayed);
//      breaking one pair drops that pair AND the tri; HarvestTargetWeights routes by
//      ACTUAL assignment with crystals the smallest share.
//   5. Save round-trip    — a rich echoLanes resource token survives SaveSchema
//      serialize→deserialize at the current version; an older blob without echoLanes
//      loads with the default (default-on-read, no throw).
//   6. EchoLaneBonuses    — after Recompute(), HarvestBonusMult mirrors the applied
//      aggregate (hidden tri included — write-only mirror, no UI reader).
//   7. Dump credit        — a real EchoService.DumpSilos through a real EconomyService:
//      Wood/Iron/Food AND Gold (Coins) AND Crystals wallets all move; the crystal share
//      is the smallest; crystals also move with only ONE crystal harvester assigned.
//
// SAFETY: snapshots+restores the raw PlayerPrefs save blob (Assign/SetLevel/Dump call
// Save()), restores the prior live GameStateService singleton, DestroyImmediate's the
// throwaway objects, and Reset()s EchoLaneBonuses in finally — the real save/state is
// untouched.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EchoSpecializationRegression
    {
        private const string SaveKey = "dotr-save";
        private const float Eps = 0.001f;

        // The all-matched, all-L1 assignment (index order == roster order):
        //   0 Aldwin food, 1 Elowen wood, 2 Corvin gold, 3 Bran crystals,
        //   4 Doran iron, 5 Maren crystals.
        private const string AllMatchedL1 = "food:1,wood:1,gold:1,crystals:1,iron:1,crystals:1";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            void Fail(string s) => failures.Add("ECHO_SPEC FAIL: " + s);

            // Snapshot the persisted save so nothing Assign/SetLevel/Dump writes here survives.
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorInstance = GameStateService.Instance;
            GameObject gssGo = null;
            GameObject svcGo = null;
            GameState throwaway = null;
            bool installed = false;

            try
            {
                // --- Groups 1 + 2 + 5 need NO live GameStateService (pure catalog/schema). --
                CheckCatalogIntegrity(Fail);
                CheckBalanceLoad(Fail, notes);
                CheckSaveRoundTrip(Fail);

                // --- Groups 3/4/6/7 drive the assignment seam → install a headless state. --
                // Editmode batchmode NEVER runs GameStateService.Awake, so a bare
                // AddComponent leaves Instance/State null. Install a throwaway GameState by
                // reflection (the same seam Awake sets), exactly as OfflineHarvestRegression.
                throwaway = ScriptableObject.CreateInstance<GameState>();   // fresh defaults; collections init'd → Save()-safe
                gssGo = new GameObject("GameStateService (echo-spec-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    // The singleton/state seam moved — the assignment-driven groups can't run
                    // headless. NAMED SKIP (not a false FAIL); groups 1/2/5 above still stand.
                    notes.Add("groups 3/4/6/7 skipped (needs fleet — " + installErr + ")");
                }
                else
                {
                    installed = true;
                    var state = gss.State;
                    if (state == null)
                    {
                        notes.Add("groups 3/4/6/7 skipped (throwaway state did not install)");
                    }
                    else
                    {
                        state.EchoCount = 6;               // own the full roster (six-set + tri live)
                        EchoBalanceCatalog.Reload();       // ensure the tunables are loaded fresh
                        CheckTokenGrammar(state, Fail);
                        CheckBonusMath(state, Fail);
                        CheckLaneBonusesPopulation(state, Fail);
                        svcGo = CheckDumpCredit(state, Fail, notes);
                    }
                }
            }
            catch (Exception ex)
            {
                Fail($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                EchoLaneBonuses.Reset();   // don't leak computed mults into later oracles

                if (svcGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(svcGo);
                    // Editmode Awake/OnDestroy never ran — clear the reflected-in singletons
                    // so later oracles never see a destroyed instance behind the statics.
                    TrySetStaticProperty(typeof(EchoService), "Instance", null);
                    TrySetStaticProperty(typeof(EconomyService), "Instance", null);
                }
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);

                // Restore the live service later oracles read (DestroyImmediate may have
                // nulled the static via OnDestroy).
                if (installed) TrySetInstanceStatic(priorInstance);

                // Restore the persisted save blob (Assign/SetLevel/Dump called Save()).
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "ECHO SPEC OK — WO-830 affinity table + balance dual-copy + resource-token grammar + "
                         + "pair/tri math (tri applied-not-displayed) + save round-trip + EchoLaneBonuses + dump credit all hold"
                         + (notes.Count > 0 ? " [" + string.Join("; ", notes) + "]" : "");
                return true;
            }
            reason = $"echo-spec: {failures.Count} failure(s): " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Group 1 — Catalog integrity (EchoRosterCatalog fixed identity table)
        // =====================================================================
        private static void CheckCatalogIntegrity(Action<string> Fail)
        {
            var roster = EchoRosterCatalog.All;
            if (roster == null || EchoRosterCatalog.Count != 6)
            {
                Fail($"EchoRosterCatalog should have 6 entries, has {(roster == null ? 0 : EchoRosterCatalog.Count)}");
                return;
            }

            // WO-830: EVERY spirit prefers Harvest (all affinities reachable) and carries
            // a non-empty EmergeLine (WO-831 -- ASCII, the emergence intro).
            for (int i = 0; i < roster.Length; i++)
            {
                var e = roster[i];
                if (e == null) { Fail($"roster index {i} is null"); continue; }
                if (e.PreferredLane != LaneType.Harvest)
                    Fail($"roster '{e.Id}' PreferredLane={e.PreferredLane} (WO-830: all six must prefer Harvest)");
                if (string.IsNullOrEmpty(e.EmergeLine))
                    Fail($"roster '{e.Id}' has no EmergeLine (WO-831 emergence intro)");
                else if (!IsAscii(e.EmergeLine))
                    Fail($"roster '{e.Id}' EmergeLine contains non-ASCII characters");
            }

            // The full WO-830 affinity table (owner-approved, amended 2026-08-02).
            AssertEntry(Fail, "echo-frosthowl", HarvestTarget.Food, ResourceType.Food);
            AssertEntry(Fail, "echo-verdant-stag", HarvestTarget.Wood, ResourceType.Wood);
            AssertEntry(Fail, "echo-voidwing-raven", HarvestTarget.Gold, null);
            AssertEntry(Fail, "echo-stormcoil-serpent", HarvestTarget.Crystals, null);
            AssertEntry(Fail, "echo-stonewarden-bear", HarvestTarget.Iron, ResourceType.Iron);
            AssertEntry(Fail, "echo-ember-phoenix", HarvestTarget.Crystals, null);

            // Crystals is the ONE deliberately doubled affinity (exactly two crystal harvesters).
            int crystalCount = 0;
            foreach (var e in roster)
                if (e != null && e.Affinity == HarvestTarget.Crystals) crystalCount++;
            if (crystalCount != 2)
                Fail($"crystals affinity count = {crystalCount} (expected exactly 2 — Bran + Maren, the doubled affinity)");
        }

        private static void AssertEntry(Action<string> Fail, string id, HarvestTarget affinity, ResourceType? resource)
        {
            EchoRosterEntry e = null;
            foreach (var candidate in EchoRosterCatalog.All)
                if (candidate != null && candidate.Id == id) { e = candidate; break; }

            if (e == null) { Fail($"roster is missing '{id}'"); return; }
            if (e.Affinity != affinity)
                Fail($"'{id}' Affinity={e.Affinity} (expected {affinity})");
            if (e.HarvestResource != resource)
                Fail($"'{id}' HarvestResource={(e.HarvestResource.HasValue ? e.HarvestResource.Value.ToString() : "null")} " +
                     $"(expected {(resource.HasValue ? resource.Value.ToString() : "null")})");
        }

        private static bool IsAscii(string s)
        {
            foreach (char c in s) if (c > 127) return false;
            return true;
        }

        // =====================================================================
        //  Group 2 — Balance load (echoes-balance.json via CanonicalJson) + dual-copy
        // =====================================================================
        private static void CheckBalanceLoad(Action<string> Fail, List<string> notes)
        {
            EchoBalanceCatalog.Reload();
            var data = EchoBalanceCatalog.Data;
            if (data == null) { Fail("EchoBalanceCatalog.Data is null (file missing/invalid, no fallback)"); return; }

            if (data.Version != 1) Fail($"echoes-balance.json version {data.Version} (expected 1)");
            if (EchoBalanceCatalog.MaxLevel != 8) Fail($"echoes-balance MaxLevel {EchoBalanceCatalog.MaxLevel} (expected 8)");

            // The key tunables must be the CURRENT owner-ruled balance (the math below assumes it).
            // RE-PINNED 2026-08-02 after the owner's F8 on the Echo card ("should be +5% not 55%"):
            // the card prints base+match, which was 0.15+0.40 = +55%. Now 0.02+0.03 = +5% matched,
            // +2% unmatched (matching still pays 2.5x). perLevelBonus fell 0.05 -> 0.01 in the same
            // breath because at the old value ONE level-up outweighed the entire matched bonus,
            // which would have made the affinity pick cosmetic. Lv8 matched now reads +12%.
            // This canary FIRED on the change (REGRESSION_FAIL x3) and was reviewed, not
            // reflexively repinned - the set/tri knobs below are deliberately UNCHANGED, which is
            // why team composition now outweighs individual assignment (flagged to the owner,
            // see docs/design/ECONOMY_PROGRESSION_THESIS_2026-08-02.md).
            AssertClose(Fail, EchoBalanceCatalog.PreferredLaneMatchBonus, 0.03f, "preferredLaneMatchBonus (owner +5% ruling)");
            AssertClose(Fail, EchoBalanceCatalog.BaseContributionPerEcho, 0.02f, "baseContributionPerEcho (owner +5% ruling)");
            AssertClose(Fail, EchoBalanceCatalog.SixSetBonusGlobalHarvest, 0.20f, "sixSetBonusGlobalHarvest");
            AssertClose(Fail, EchoBalanceCatalog.PerLevelBonus, 0.01f, "perLevelBonus (owner +5% ruling)");
            AssertClose(Fail, EchoBalanceCatalog.HiddenTriSynergyBonus, 0.25f, "hiddenTriSynergyBonus (WO-830 Sec.3d)");

            // The 3 disclosed pair synergies (Provisions / Forge / Fortune) with positive bonuses.
            var pairs = EchoBalanceCatalog.CrossBonuses;
            if (pairs == null || pairs.Count != 3)
            {
                Fail($"crossBonuses count = {(pairs == null ? 0 : pairs.Count)} (expected the 3 WO-830 pairs)");
            }
            else
            {
                AssertPair(Fail, pairs, "Provisions", "echo-verdant-stag", "echo-frosthowl");
                AssertPair(Fail, pairs, "Forge", "echo-stonewarden-bear", "echo-ember-phoenix");
                AssertPair(Fail, pairs, "Fortune", "echo-voidwing-raven", "echo-stormcoil-serpent");
            }

            // Crystals-slowest law (WO-830 Sec.3b): the COMBINED Bran+Maren rate stays below
            // every other single affinity rate, so the double-crystal trickle is the slowest
            // faucet of the six affinity assignments.
            float crystalsCombined = EchoBalanceCatalog.BaseRateFor("echo-stormcoil-serpent")
                                   + EchoBalanceCatalog.BaseRateFor("echo-ember-phoenix");
            float[] others =
            {
                EchoBalanceCatalog.BaseRateFor("echo-frosthowl"),
                EchoBalanceCatalog.BaseRateFor("echo-verdant-stag"),
                EchoBalanceCatalog.BaseRateFor("echo-voidwing-raven"),
                EchoBalanceCatalog.BaseRateFor("echo-stonewarden-bear"),
            };
            foreach (var r in others)
                if (crystalsCombined >= r)
                {
                    Fail($"crystals combined rate {crystalsCombined:0.###} is not the slowest (another affinity rate is {r:0.###}) — monetization guard broken");
                    break;
                }

            // Dual-copy must be byte-identical (owner mandate: keep Resources + StreamingAssets in sync).
            string resPath = Path.Combine(Application.dataPath, "Resources/Data/Canonical/echoes-balance.json");
            string streamPath = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/echoes-balance.json");
            bool resExists = File.Exists(resPath), streamExists = File.Exists(streamPath);
            if (!resExists && !streamExists)
            {
                notes.Add("echoes-balance.json not found on disk (loaded via built-in fallback); dual-copy check skipped");
            }
            else if (resExists && streamExists)
            {
                string a = File.ReadAllText(resPath);
                string b = File.ReadAllText(streamPath);
                if (a != b)
                    Fail("echoes-balance.json Resources/StreamingAssets dual-copy is NOT byte-identical (owner: keep the two in sync)");
            }
            else
            {
                notes.Add($"echoes-balance.json present in only one location (res={resExists}, stream={streamExists}); dual-copy check skipped");
            }
        }

        private static void AssertPair(Action<string> Fail, List<EchoCrossBonusDef> pairs, string name, string a, string b)
        {
            foreach (var p in pairs)
            {
                if (p == null || p.Name != name) continue;
                bool members = (p.A == a && p.B == b) || (p.A == b && p.B == a);
                if (!members) Fail($"crossBonuses '{name}' members [{p.A},{p.B}] (expected [{a},{b}])");
                if (p.Bonus <= 0f) Fail($"crossBonuses '{name}' bonus {p.Bonus:0.###} (expected > 0 — the disclosed synergy)");
                return;
            }
            Fail($"crossBonuses is missing the '{name}' pair");
        }

        // =====================================================================
        //  Group 3 — Token grammar (WO-830 resource:level + legacy + affinity default)
        // =====================================================================
        private static void CheckTokenGrammar(GameState state, Action<string> Fail)
        {
            // (a) WRITE PATH: the resource picker writes an explicit "resource:level" token
            //     that round-trips through lane + resource + level reads.
            EchoAssignments.AssignHarvest(5, EchoAssignments.ResCrystals);
            EchoAssignments.SetLevel(5, 3);
            if (EchoAssignments.LaneOf(5) != EchoAssignments.LaneHarvest)
                Fail($"resource token: LaneOf(5)='{EchoAssignments.LaneOf(5)}' (expected 'harvest')");
            if (EchoAssignments.ResourceTokenOf(5) != EchoAssignments.ResCrystals)
                Fail($"resource token: ResourceTokenOf(5)='{EchoAssignments.ResourceTokenOf(5)}' (expected 'crystals')");
            if (EchoAssignments.LevelOf(5) != 3)
                Fail($"resource token: LevelOf(5)={EchoAssignments.LevelOf(5)} (expected 3)");
            var parts = (state.EchoLanes ?? "").Split(',');
            if (parts.Length <= 5 || parts[5] != "crystals:3")
                Fail($"resource token: persisted CSV token[5]='{(parts.Length > 5 ? parts[5] : "<none>")}' (expected 'crystals:3'); full='{state.EchoLanes}'");

            // (b) AssignHarvest rejects a non-resource token (logged no-op, returns false).
            if (EchoAssignments.AssignHarvest(5, "crafting"))
                Fail("AssignHarvest accepted 'crafting' (must reject non-resource tokens)");
            if (EchoAssignments.ResourceTokenOf(5) != EchoAssignments.ResCrystals)
                Fail("AssignHarvest('crafting') mutated the stored assignment (must be a no-op)");

            // (c) LEGACY (pre-v33) resource CSV reads Harvest with the RESOURCE PRESERVED at L1.
            state.EchoLanes = "wood,iron,food,idle";
            string[] expectedRes = { EchoAssignments.ResWood, EchoAssignments.ResIron, EchoAssignments.ResFood };
            for (int i = 0; i <= 2; i++)
            {
                if (EchoAssignments.LaneOf(i) != EchoAssignments.LaneHarvest)
                    Fail($"legacy: index {i} of 'wood,iron,food,idle' reads lane '{EchoAssignments.LaneOf(i)}' (expected 'harvest')");
                if (EchoAssignments.ResourceTokenOf(i) != expectedRes[i])
                    Fail($"legacy: index {i} resource '{EchoAssignments.ResourceTokenOf(i)}' (expected '{expectedRes[i]}' — resource preserved, WO-830)");
                if (EchoAssignments.LevelOf(i) != 1)
                    Fail($"legacy: index {i} bare token reads level {EchoAssignments.LevelOf(i)} (expected 1)");
            }
            if (EchoAssignments.LaneOf(3) != EchoAssignments.LaneIdle)
                Fail($"legacy: index 3 'idle' reads '{EchoAssignments.LaneOf(3)}' (expected 'idle')");
            if (EchoAssignments.ResourceTokenOf(3) != "")
                Fail($"legacy: index 3 'idle' resource '{EchoAssignments.ResourceTokenOf(3)}' (expected '')");

            // (d) v33 GENERIC "harvest:N" defaults on read to the echo's AFFINITY resource.
            state.EchoLanes = "harvest:2,harvest:1";
            if (EchoAssignments.ResourceTokenOf(0) != EchoAssignments.ResFood)
                Fail($"generic harvest: index 0 (Aldwin) resource '{EchoAssignments.ResourceTokenOf(0)}' (expected 'food' — affinity default-on-read)");
            if (EchoAssignments.ResourceTokenOf(1) != EchoAssignments.ResWood)
                Fail($"generic harvest: index 1 (Elowen) resource '{EchoAssignments.ResourceTokenOf(1)}' (expected 'wood' — affinity default-on-read)");
            if (EchoAssignments.LevelOf(0) != 2)
                Fail($"generic harvest: LevelOf(0)={EchoAssignments.LevelOf(0)} (expected 2)");

            // (e) A stored non-pickable lane token still reads back (read-compat; no offer).
            state.EchoLanes = "harvest:1,crafting:1";
            if (EchoAssignments.LaneOf(1) != EchoAssignments.LaneCrafting || EchoAssignments.LevelOf(1) != 1)
                Fail($"stored crafting token reads lane='{EchoAssignments.LaneOf(1)}' level={EchoAssignments.LevelOf(1)} (expected crafting/1 read-compat)");
            if (EchoAssignments.PickableLanes.Length != 1 || EchoAssignments.PickableLanes[0] != EchoAssignments.LaneHarvest)
                Fail("PickableLanes must be Harvest-only (WO-830 Sec.3e — the dead Crafting chip is removed)");
            if (EchoAssignments.PickableResources.Length != 5)
                Fail($"PickableResources length {EchoAssignments.PickableResources.Length} (expected the 5 harvest resources)");
        }

        // =====================================================================
        //  Group 4 — Bonus math (aggregate incl. pairs + hidden tri; weights)
        // =====================================================================
        private static void CheckBonusMath(GameState state, Action<string> Fail)
        {
            float b = EchoBalanceCatalog.BaseContributionPerEcho;
            float m = EchoBalanceCatalog.PreferredLaneMatchBonus;
            float per = EchoBalanceCatalog.PerLevelBonus;
            float six = EchoBalanceCatalog.SixSetBonusGlobalHarvest;
            float tri = EchoBalanceCatalog.HiddenTriSynergyBonus;
            float pairSum = 0f;
            foreach (var p in EchoBalanceCatalog.CrossBonuses) if (p != null) pairSum += Mathf.Max(0f, p.Bonus);

            // (a) ALL-MATCHED, mixed levels: every echo on its affinity resource.
            //     0 food:1, 1 wood:2, 2 gold:1, 3 crystals:1, 4 iron:3, 5 crystals:1
            state.EchoLanes = "food:1,wood:2,gold:1,crystals:1,iron:3,crystals:1";
            float perEchoSum = (b + m) + (b + m + per * 1f) + (b + m) + (b + m) + (b + m + per * 2f) + (b + m);
            float disclosed = perEchoSum + pairSum + six;
            float applied = disclosed + tri;             // ALL pairs run -> hidden tri fires
            float expectedAgg = 6f * (1f + applied);
            float actualAgg = EchoBonusCalculator.AggregateHarvestMultiplier();
            if (Mathf.Abs(actualAgg - expectedAgg) > Eps)
                Fail($"AggregateHarvestMultiplier={actualAgg:0.####} (expected {expectedAgg:0.####} incl. pairs {pairSum:0.###} + hidden tri {tri:0.###})");

            // (b) THE SECRET STAYS SECRET: no ReadoutFor().BonusPct contains the pair or tri
            //     terms — the displayed per-echo % is exactly base+match+level.
            float[] expectedPct =
            {
                (b + m) * 100f, (b + m + per) * 100f, (b + m) * 100f,
                (b + m) * 100f, (b + m + per * 2f) * 100f, (b + m) * 100f,
            };
            float displayedSum = 0f;
            for (int i = 0; i < 6; i++)
            {
                var ro = EchoBonusCalculator.ReadoutFor(i);
                displayedSum += ro.BonusPct;
                if (Mathf.Abs(ro.BonusPct - expectedPct[i]) > Eps * 100f)
                    Fail($"ReadoutFor({i}).BonusPct={ro.BonusPct:0.##} (expected {expectedPct[i]:0.##} — pair/tri must NOT leak into the displayed %)");
                if (!ro.PreferredMatch)
                    Fail($"ReadoutFor({i}).PreferredMatch=false (all-matched assignment — affinity match must register)");
            }
            // applied ≠ displayed: the applied spec sum exceeds the displayed per-echo sum by
            // EXACTLY pairs+six+tri — i.e. the hidden tri is applied but never displayed.
            float appliedSpecSum = actualAgg / 6f - 1f;
            float hiddenDelta = appliedSpecSum - displayedSum / 100f;
            if (Mathf.Abs(hiddenDelta - (pairSum + six + tri)) > Eps)
                Fail($"applied-vs-displayed delta {hiddenDelta:0.####} (expected pairs+six+tri = {pairSum + six + tri:0.####})");
            if (hiddenDelta < tri - Eps)
                Fail("the hidden tri-synergy does not appear to be applied (applied-displayed delta too small)");

            // (c) BREAK one pair (Corvin off gold -> wood): Fortune + the tri both drop.
            state.EchoLanes = "food:1,wood:2,wood:1,crystals:1,iron:3,crystals:1";
            float fortuneBonus = 0f;
            foreach (var p in EchoBalanceCatalog.CrossBonuses)
                if (p != null && p.Name == "Fortune") fortuneBonus = Mathf.Max(0f, p.Bonus);
            float perEchoSum2 = (b + m) + (b + m + per * 1f) + (b) + (b + m) + (b + m + per * 2f) + (b + m);
            float expectedAgg2 = 6f * (1f + perEchoSum2 + (pairSum - fortuneBonus) + six);   // no tri
            float actualAgg2 = EchoBonusCalculator.AggregateHarvestMultiplier();
            if (Mathf.Abs(actualAgg2 - expectedAgg2) > Eps)
                Fail($"broken-pair aggregate={actualAgg2:0.####} (expected {expectedAgg2:0.####} — Fortune AND the hidden tri must both drop)");
            var roCorvin = EchoBonusCalculator.ReadoutFor(2);
            if (roCorvin.PreferredMatch)
                Fail("Corvin assigned wood still reads PreferredMatch=true (affinity is gold — match must key off the ACTUAL assignment)");
            var syCorvin = EchoBonusCalculator.SynergyFor(2);
            if (!syCorvin.HasPair || syCorvin.Active)
                Fail($"SynergyFor(Corvin) HasPair={syCorvin.HasPair} Active={syCorvin.Active} (expected a defined but INACTIVE pair)");

            // (d) WEIGHTS: 5-way split by ACTUAL assignment; crystals the smallest share.
            state.EchoLanes = AllMatchedL1;
            var w = EchoBonusCalculator.HarvestTargetWeights();
            float rAld = EchoBalanceCatalog.BaseRateFor("echo-frosthowl");
            float rElo = EchoBalanceCatalog.BaseRateFor("echo-verdant-stag");
            float rCor = EchoBalanceCatalog.BaseRateFor("echo-voidwing-raven");
            float rBra = EchoBalanceCatalog.BaseRateFor("echo-stormcoil-serpent");
            float rDor = EchoBalanceCatalog.BaseRateFor("echo-stonewarden-bear");
            float rMar = EchoBalanceCatalog.BaseRateFor("echo-ember-phoenix");
            AssertClose(Fail, GetW(w, HarvestTarget.Food), rAld, "HarvestTargetWeights[Food]");
            AssertClose(Fail, GetW(w, HarvestTarget.Wood), rElo, "HarvestTargetWeights[Wood]");
            AssertClose(Fail, GetW(w, HarvestTarget.Gold), rCor, "HarvestTargetWeights[Gold]");
            AssertClose(Fail, GetW(w, HarvestTarget.Iron), rDor, "HarvestTargetWeights[Iron]");
            AssertClose(Fail, GetW(w, HarvestTarget.Crystals), rBra + rMar, "HarvestTargetWeights[Crystals]");
            float crystalsW = GetW(w, HarvestTarget.Crystals);
            foreach (var t in new[] { HarvestTarget.Wood, HarvestTarget.Iron, HarvestTarget.Food, HarvestTarget.Gold })
                if (crystalsW >= GetW(w, t))
                {
                    Fail($"crystals weight {crystalsW:0.###} not the smallest (>{GetW(w, t):0.###} for {t}) — combined double-crystal trickle must stay slowest");
                    break;
                }
        }

        // =====================================================================
        //  Group 6 — EchoLaneBonuses population after Recompute()
        // =====================================================================
        private static void CheckLaneBonusesPopulation(GameState state, Action<string> Fail)
        {
            state.EchoLanes = AllMatchedL1;
            EchoBonusCalculator.Recompute();

            float expectedAgg = EchoBonusCalculator.AggregateHarvestMultiplier();
            if (Mathf.Abs(EchoLaneBonuses.HarvestBonusMult - expectedAgg) > Eps)
                Fail($"EchoLaneBonuses.HarvestBonusMult={EchoLaneBonuses.HarvestBonusMult:0.####} after Recompute (expected {expectedAgg:0.####}, the applied aggregate)");
            if (Mathf.Abs(EchoLaneBonuses.HarvestBonusMult - 1f) < Eps)
                Fail("EchoLaneBonuses.HarvestBonusMult is still 1.0 after Recompute (never populated)");
        }

        // =====================================================================
        //  Group 7 — Dump credit: all five wallets move through the REAL path
        // =====================================================================
        private static GameObject CheckDumpCredit(GameState state, Action<string> Fail, List<string> notes)
        {
            // Stand up the REAL services headless. Editmode NEVER runs Awake (same law the
            // GameStateService seam documents above), so the Instance singletons are installed
            // by reflection on the auto-property backing setters — the method bodies under test
            // (DumpSilos / GrantSpendable / AddCoins) are still the REAL production code.
            var go = new GameObject("EchoDumpOracle");
            EchoService echo = null;
            EconomyService eco = null;
            try
            {
                echo = go.AddComponent<EchoService>();
                eco = go.AddComponent<EconomyService>();
            }
            catch (Exception ex)
            {
                notes.Add("group 7 skipped (service AddComponent failed headless — " + ex.Message + ")");
                return go;
            }
            if (EchoService.Instance == null && !TrySetStaticProperty(typeof(EchoService), "Instance", echo))
            {
                notes.Add("group 7 skipped (EchoService.Instance seam not installable headless)");
                return go;
            }
            if (EconomyService.Instance == null && !TrySetStaticProperty(typeof(EconomyService), "Instance", eco))
            {
                notes.Add("group 7 skipped (EconomyService.Instance seam not installable headless)");
                return go;
            }
            if (EchoService.Instance == null || EconomyService.Instance == null)
            {
                notes.Add("group 7 skipped (service singletons did not install headless)");
                return go;
            }

            // (a) All six harvest their affinities: every wallet moves; crystals the smallest.
            state.EchoLanes = AllMatchedL1;
            state.SiloResources = 1000.0;
            int woodBefore = state.Wood, ironBefore = state.Iron;
            int foodBefore = state.Resources.Food, coinsBefore = state.Resources.Coins, crysBefore = state.Resources.Crystals;
            int banked = echo.DumpSilos();
            if (banked != 1000)
                Fail($"DumpSilos banked {banked} (expected the full 1000 pool)");
            int dWood = state.Wood - woodBefore;
            int dIron = state.Iron - ironBefore;
            int dFood = state.Resources.Food - foodBefore;
            int dGold = state.Resources.Coins - coinsBefore;
            int dCrys = state.Resources.Crystals - crysBefore;
            if (dWood <= 0) Fail($"Dump: Wood wallet did not move (+{dWood})");
            if (dIron <= 0) Fail($"Dump: Iron wallet did not move (+{dIron})");
            if (dFood <= 0) Fail($"Dump: Food wallet did not move (+{dFood})");
            if (dGold <= 0) Fail($"Dump: Gold/Coins wallet did not move (+{dGold}) — Corvin's affinity must credit AddCoins");
            if (dCrys <= 0) Fail($"Dump: Crystals wallet did not move (+{dCrys}) — Bran+Maren must credit the Aether wallet");
            if (dWood + dIron + dFood + dGold + dCrys != 1000)
                Fail($"Dump: split sum {dWood + dIron + dFood + dGold + dCrys} != pool 1000 (largest-remainder invariant broken)");
            foreach (int other in new[] { dWood, dIron, dFood, dGold })
                if (dCrys >= other)
                {
                    Fail($"Dump: crystal share {dCrys} not the smallest (vs {other}) — the double-crystal trickle must stay slowest");
                    break;
                }

            // (b) Crystals move with only ONE crystal harvester assigned (either suffices).
            state.EchoLanes = "food:1,wood:1,gold:1,idle,iron:1,crystals:1";   // Maren only
            state.SiloResources = 1000.0;
            crysBefore = state.Resources.Crystals;
            echo.DumpSilos();
            if (state.Resources.Crystals - crysBefore <= 0)
                Fail("Dump: crystals did not move with only Maren assigned (either crystal harvester must credit)");

            return go;
        }

        // =====================================================================
        //  Group 5 — Save round-trip (SaveSchema serialize→deserialize)
        // =====================================================================
        private static void CheckSaveRoundTrip(Action<string> Fail)
        {
            // Canary on unreviewed schema bumps. WO-738 established the richer echoLanes token at
            // v33; v34-v36 are additive + reviewed; WO-830 extended the TOKEN GRAMMAR ONLY
            // (resource:level — same wire shape, NO bump). Update this pin in the SAME breath as
            // any future reviewed bump (CLAUDE.md §15).
            //
            // v37 REVIEWED 2026-08-07 (WO-911): paid basket on BuildJobData — additive only.
            // v38 REVIEWED 2026-08-09 (WO-934): army loadouts nested under Army — no echoLanes change.
            // Update this pin in the SAME breath as any future reviewed bump (CLAUDE.md §15).
            if (SaveSchema.CurrentVersion != 38)
                Fail($"SaveSchema.CurrentVersion={SaveSchema.CurrentVersion} (expected 38; echoLanes token must survive the current schema)");

            // A rich WO-830 resource token survives the REAL serialize → deserialize → validate path.
            const string richToken = "crystals:3,idle,wood:1,gold:2";
            var ps = new SaveSchema.PersistedState { EchoLanes = richToken, EchoCount = 6 };
            try
            {
                string json = JsonConvert.SerializeObject(ps, SaveSchema.JsonSettings);
                var back = JsonConvert.DeserializeObject<SaveSchema.PersistedState>(json, SaveSchema.JsonSettings);
                if (back == null) { Fail("save round-trip deserialized to null"); }
                else
                {
                    var vr = SaveSchema.Validate(back);
                    if (!vr.Ok) Fail($"round-tripped save FAILED validation: field '{vr.FieldPath}' ({vr.Reason})");
                    if (back.EchoLanes != richToken)
                        Fail($"echoLanes did not survive the save round-trip: wrote '{richToken}', read back '{back.EchoLanes}'");
                }
            }
            catch (Exception ex) { Fail($"save round-trip THREW: {ex.GetType().Name}: {ex.Message}"); }

            // An older-version blob with NO echoLanes loads with the default (default-on-read,
            // no throw). SaveMigrator's v31 step seeds the "wood" starter when absent.
            try
            {
                var migrated = SaveMigrator.Migrate(new SaveSchema.PersistedState(), 25);
                if (migrated == null) Fail("SaveMigrator.Migrate(v25, no echoLanes) returned null");
                else if (migrated.EchoLanes == null)
                    Fail("migrate v25→current left echoLanes null (default-on-read did not seed the starter lane)");
            }
            catch (Exception ex) { Fail($"legacy-blob migrate THREW: {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static float GetW(Dictionary<HarvestTarget, float> w, HarvestTarget t)
            => (w != null && w.TryGetValue(t, out var v)) ? v : 0f;

        private static void AssertClose(Action<string> Fail, float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > Eps)
                Fail($"{label}={actual:0.#####} (expected {expected:0.#####})");
        }

        // --- Headless state-install (editmode has no Awake) — mirrors OfflineHarvestRegression --

        private static bool TryInstallHeadlessState(GameStateService svc, GameState state, out string err)
        {
            err = null;
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null)
            { err = "GameStateService._state field not found by reflection (state seam renamed/removed)"; return false; }
            stateField.SetValue(svc, state);
            if (!TrySetInstanceStatic(svc))
            { err = "GameStateService._instance static not found by reflection (singleton seam renamed/removed)"; return false; }
            return true;
        }

        private static bool TrySetInstanceStatic(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        /// <summary>Set a static auto-property with a private setter (the `Instance` singleton
        /// seam) by reflection. Returns false when the seam moved (caller emits a named skip).</summary>
        private static bool TrySetStaticProperty(Type type, string name, object value)
        {
            try
            {
                var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null && p.CanWrite) { p.SetValue(null, value, null); return true; }
                var f = type.GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null) return false;
                f.SetValue(null, value);
                return true;
            }
            catch { return false; }   // seam moved — named-skip path, never a throw out of finally
        }
    }
}
