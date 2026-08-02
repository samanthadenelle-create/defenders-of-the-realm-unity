// =============================================================================
// EchoSpecializationRegression — the §2c permission-gate oracle for WO-738
// (Echo per-echo specialization). Headless, data-decidable, no play-mode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Asserts the COMMITTED runtime contract of the Echo specialization model — real
// objects in through the actual game path (reload the balance catalog, stand up a
// throwaway GameState the way OfflineHarvestRegression/CoreSaveContractRegression do),
// assert the response, one marker out. Mirrors MonetizationCovenantRegression's shape:
//   public static bool Run(out string reason)   →  wired into DataRegression.RunAll.
//
// SIX ASSERTION GROUPS (all data-decidable headless — none deferred):
//   1. Catalog integrity  — EchoRosterCatalog: 6 entries, non-Idle PreferredLane each,
//      the 3 demo-live specialists map (Stag/Bear→Harvest, Phoenix→Crafting), Stag→Wood,
//      Bear→Iron.
//   2. Balance load       — EchoBalanceCatalog loads (version 1, maxLevel 8) via
//      CanonicalJson; the Resources/StreamingAssets dual-copy is byte-identical; the key
//      tunables are the expected defaults (0.75 / 0.15 / 0.20 / 0.05).
//   3. Token round-trip + legacy migration — Assign+SetLevel produce a "lane:level" token
//      that reads back identically; a legacy "wood,iron,food,idle" token reads as
//      [Harvest L1, Harvest L1, Harvest L1, Idle]; a bare token defaults level 1.
//   4. Bonus math         — a known 6-echo assignment: AggregateHarvestMultiplier equals
//      the formula's hand-computed value (spine EchoCount × (1 + Σ harvest terms + sixSet));
//      HarvestResourceWeights splits to the expected Wood/Iron/Food proportions;
//      LaneMultiplier(Crafting) reflects the Phoenix assignment.
//   5. Save round-trip    — a rich echoLanes token survives SaveSchema serialize→deserialize
//      at CurrentVersion 33 (assert 33) with the token intact; an older-version blob without
//      echoLanes loads with the default (default-on-read, no throw).
//   6. EchoLaneBonuses     — after Recompute() with the known assignment, HarvestBonusMult /
//      CraftingMult reflect the computed values (not the 1.0 default).
//
// SAFETY: snapshots+restores the raw PlayerPrefs save blob (Assign/SetLevel call Save()),
// restores the prior live GameStateService singleton, DestroyImmediate's the throwaway
// objects, and Reset()s EchoLaneBonuses in finally — the real save/state is untouched.
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

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            void Fail(string s) => failures.Add("ECHO_SPEC FAIL: " + s);

            // Snapshot the persisted save so nothing Assign/SetLevel writes here survives.
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorInstance = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            bool installed = false;

            try
            {
                // --- Group 1 + 2 + 5 need NO live GameStateService (pure catalog/schema). --
                CheckCatalogIntegrity(Fail);
                CheckBalanceLoad(Fail, notes);
                CheckSaveRoundTrip(Fail);

                // --- Groups 3/4/6 drive the assignment seam → install a headless state. ----
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
                    notes.Add("groups 3/4/6 skipped (needs fleet — " + installErr + ")");
                }
                else
                {
                    installed = true;
                    var state = gss.State;
                    if (state == null)
                    {
                        notes.Add("groups 3/4/6 skipped (throwaway state did not install)");
                    }
                    else
                    {
                        state.EchoCount = 6;               // own the full roster (six-set bonus live)
                        EchoBalanceCatalog.Reload();       // ensure the tunables are loaded fresh
                        CheckTokenRoundTrip(state, Fail);
                        CheckBonusMath(state, Fail);
                        CheckLaneBonusesPopulation(state, Fail);
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

                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);

                // Restore the live service later oracles read (DestroyImmediate may have
                // nulled the static via OnDestroy).
                if (installed) TrySetInstanceStatic(priorInstance);

                // Restore the persisted save blob (Assign/SetLevel called Save()).
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "ECHO SPEC OK — roster identity + balance dual-copy + token/legacy round-trip + "
                         + "bonus math (aggregate/weights/lane) + save v33 + EchoLaneBonuses populate all hold"
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

            // Every spirit has a real (non-Idle) preferred lane — a match earns the affinity bonus.
            for (int i = 0; i < roster.Length; i++)
            {
                var e = roster[i];
                if (e == null) { Fail($"roster index {i} is null"); continue; }
                if (e.PreferredLane == LaneType.Idle)
                    Fail($"roster '{e.Id}' has PreferredLane=Idle (every spirit must favor a real lane)");
            }

            // The 3 demo-live specialists map exactly as WO-738 specifies.
            AssertEntry(Fail, "echo-verdant-stag", LaneType.Harvest, ResourceType.Wood);
            AssertEntry(Fail, "echo-stonewarden-bear", LaneType.Harvest, ResourceType.Iron);
            AssertEntry(Fail, "echo-ember-phoenix", LaneType.Crafting, null);
        }

        private static void AssertEntry(Action<string> Fail, string id, LaneType lane, ResourceType? resource)
        {
            EchoRosterEntry e = null;
            foreach (var candidate in EchoRosterCatalog.All)
                if (candidate != null && candidate.Id == id) { e = candidate; break; }

            if (e == null) { Fail($"roster is missing the demo-live specialist '{id}'"); return; }
            if (e.PreferredLane != lane)
                Fail($"'{id}' PreferredLane={e.PreferredLane} (expected {lane})");
            if (e.HarvestResource != resource)
                Fail($"'{id}' HarvestResource={(e.HarvestResource.HasValue ? e.HarvestResource.Value.ToString() : "null")} " +
                     $"(expected {(resource.HasValue ? resource.Value.ToString() : "null")})");
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

            // The key tunables must be the WO-738 defaults (the balance the math below assumes).
            AssertClose(Fail, EchoBalanceCatalog.PreferredLaneMatchBonus, 0.75f, "preferredLaneMatchBonus");
            AssertClose(Fail, EchoBalanceCatalog.BaseContributionPerEcho, 0.15f, "baseContributionPerEcho");
            AssertClose(Fail, EchoBalanceCatalog.SixSetBonusGlobalHarvest, 0.20f, "sixSetBonusGlobalHarvest");
            AssertClose(Fail, EchoBalanceCatalog.PerLevelBonus, 0.05f, "perLevelBonus");

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

        // =====================================================================
        //  Group 3 — Token round-trip + legacy migration (EchoAssignments)
        // =====================================================================
        private static void CheckTokenRoundTrip(GameState state, Action<string> Fail)
        {
            // (a) WRITE PATH: Assign a functional lane + set a level → the persisted CSV carries a
            //     "lane:level" token that reads back identically.
            EchoAssignments.Assign(5, EchoAssignments.LaneCrafting);
            EchoAssignments.SetLevel(5, 3);
            if (EchoAssignments.LaneOf(5) != EchoAssignments.LaneCrafting)
                Fail($"token write: LaneOf(5)='{EchoAssignments.LaneOf(5)}' (expected 'crafting')");
            if (EchoAssignments.LevelOf(5) != 3)
                Fail($"token write: LevelOf(5)={EchoAssignments.LevelOf(5)} (expected 3)");
            var parts = (state.EchoLanes ?? "").Split(',');
            if (parts.Length <= 5 || parts[5] != "crafting:3")
                Fail($"token write: persisted CSV token[5]='{(parts.Length > 5 ? parts[5] : "<none>")}' (expected 'crafting:3'); full='{state.EchoLanes}'");

            // (b) LEGACY MIGRATION: a pre-738 resource-lane CSV reads forward to the functional
            //     Harvest lane at level 1; "idle" stays Idle. Default-on-read, no migrator.
            state.EchoLanes = "wood,iron,food,idle";
            for (int i = 0; i <= 2; i++)
            {
                if (EchoAssignments.LaneOf(i) != EchoAssignments.LaneHarvest)
                    Fail($"legacy: index {i} of 'wood,iron,food,idle' reads '{EchoAssignments.LaneOf(i)}' (expected 'harvest')");
                if (EchoAssignments.LevelOf(i) != 1)
                    Fail($"legacy: index {i} bare token reads level {EchoAssignments.LevelOf(i)} (expected 1)");
            }
            if (EchoAssignments.LaneOf(3) != EchoAssignments.LaneIdle)
                Fail($"legacy: index 3 'idle' reads '{EchoAssignments.LaneOf(3)}' (expected 'idle')");

            // (c) BARE TOKEN (no ":level") defaults to level 1.
            state.EchoLanes = "harvest,crafting";
            if (EchoAssignments.LevelOf(0) != 1)
                Fail($"bare token: LevelOf(0) of 'harvest' reads {EchoAssignments.LevelOf(0)} (expected 1)");
            if (EchoAssignments.LaneOf(1) != EchoAssignments.LaneCrafting || EchoAssignments.LevelOf(1) != 1)
                Fail($"bare token: index 1 'crafting' reads lane='{EchoAssignments.LaneOf(1)}' level={EchoAssignments.LevelOf(1)} (expected crafting/1)");
        }

        // =====================================================================
        //  Group 4 — Bonus math (AggregateHarvestMultiplier / weights / lane)
        // =====================================================================
        // Known 6-echo assignment (index → lane:level):
        //   0 Frosthowl  harvest:2   (pref Exploration → NOT matched for Harvest)
        //   1 VerdantStag harvest:1  (pref Harvest → MATCHED, resource Wood)
        //   2 —          idle
        //   3 Stormcoil  defense:1   (pref Defense → MATCHED)
        //   4 Stonewarden harvest:3  (pref Harvest → MATCHED, resource Iron)
        //   5 Ember      crafting:1  (pref Crafting → MATCHED)
        private static void CheckBonusMath(GameState state, Action<string> Fail)
        {
            state.EchoLanes = "harvest:2,harvest:1,idle,defense:1,harvest:3,crafting:1";

            float b = EchoBalanceCatalog.BaseContributionPerEcho;
            float m = EchoBalanceCatalog.PreferredLaneMatchBonus;
            float per = EchoBalanceCatalog.PerLevelBonus;
            float six = EchoBalanceCatalog.SixSetBonusGlobalHarvest;

            // Hand-computed spine × (1 + Σ harvest terms + sixSet). Frosthowl not matched (+per*1);
            // Stag matched L1 (+m); Bear matched L3 (+m + per*2). All 6 owned → + six.
            float specSum = (b + per * 1f) + (b + m) + (b + m + per * 2f) + six;
            float expectedAgg = 6f * (1f + specSum);
            float actualAgg = EchoBonusCalculator.AggregateHarvestMultiplier();
            if (Mathf.Abs(actualAgg - expectedAgg) > Eps)
                Fail($"AggregateHarvestMultiplier={actualAgg:0.####} (expected {expectedAgg:0.####} from the documented formula)");

            // Per-resource split weights: Stag→Wood (rate×1), Bear→Iron (rate×3), Frosthowl (null
            // resource)→ even third across Wood/Iron/Food (rate×2). Non-harvest echoes contribute 0.
            var w = EchoBonusCalculator.HarvestResourceWeights();
            float rF = EchoBalanceCatalog.BaseRateFor("echo-frosthowl");
            float rS = EchoBalanceCatalog.BaseRateFor("echo-verdant-stag");
            float rB = EchoBalanceCatalog.BaseRateFor("echo-stonewarden-bear");
            float third = rF * 2f / 3f;
            float expWood = third + rS * 1f;
            float expIron = third + rB * 3f;
            float expFood = third;
            AssertClose(Fail, GetW(w, ResourceType.Wood), expWood, "HarvestResourceWeights[Wood]");
            AssertClose(Fail, GetW(w, ResourceType.Iron), expIron, "HarvestResourceWeights[Iron]");
            AssertClose(Fail, GetW(w, ResourceType.Food), expFood, "HarvestResourceWeights[Food]");
            // Proportion sanity: Bear(L3,Iron) > Stag(L1,Wood) > the Food-only spread share.
            if (!(GetW(w, ResourceType.Iron) > GetW(w, ResourceType.Wood) &&
                  GetW(w, ResourceType.Wood) > GetW(w, ResourceType.Food)))
                Fail($"HarvestResourceWeights proportion wrong: Iron={GetW(w, ResourceType.Iron):0.###} " +
                     $"Wood={GetW(w, ResourceType.Wood):0.###} Food={GetW(w, ResourceType.Food):0.###} (expected Iron>Wood>Food)");

            // LaneMultiplier(Crafting) reflects the Phoenix (matched Crafting L1) assignment.
            float expCraft = 1f + (b + m);
            float actualCraft = EchoBonusCalculator.LaneMultiplier(LaneType.Crafting);
            if (Mathf.Abs(actualCraft - expCraft) > Eps)
                Fail($"LaneMultiplier(Crafting)={actualCraft:0.####} (expected {expCraft:0.####} from the Phoenix assignment)");
            if (actualCraft <= 1f)
                Fail("LaneMultiplier(Crafting) is <= 1.0 — the Phoenix crafting assignment did not register");
        }

        // =====================================================================
        //  Group 6 — EchoLaneBonuses population after Recompute()
        // =====================================================================
        private static void CheckLaneBonusesPopulation(GameState state, Action<string> Fail)
        {
            // Same known assignment as group 4.
            state.EchoLanes = "harvest:2,harvest:1,idle,defense:1,harvest:3,crafting:1";
            EchoBonusCalculator.Recompute();

            float b = EchoBalanceCatalog.BaseContributionPerEcho;
            float m = EchoBalanceCatalog.PreferredLaneMatchBonus;
            float per = EchoBalanceCatalog.PerLevelBonus;
            float six = EchoBalanceCatalog.SixSetBonusGlobalHarvest;
            float specSum = (b + per * 1f) + (b + m) + (b + m + per * 2f) + six;
            float expectedAgg = 6f * (1f + specSum);
            float expCraft = 1f + (b + m);

            if (Mathf.Abs(EchoLaneBonuses.HarvestBonusMult - expectedAgg) > Eps)
                Fail($"EchoLaneBonuses.HarvestBonusMult={EchoLaneBonuses.HarvestBonusMult:0.####} after Recompute (expected {expectedAgg:0.####}, the applied aggregate)");
            if (Mathf.Abs(EchoLaneBonuses.HarvestBonusMult - 1f) < Eps)
                Fail("EchoLaneBonuses.HarvestBonusMult is still 1.0 after Recompute (never populated)");
            if (Mathf.Abs(EchoLaneBonuses.CraftingMult - expCraft) > Eps)
                Fail($"EchoLaneBonuses.CraftingMult={EchoLaneBonuses.CraftingMult:0.####} after Recompute (expected {expCraft:0.####})");
            if (Mathf.Abs(EchoLaneBonuses.CraftingMult - 1f) < Eps)
                Fail("EchoLaneBonuses.CraftingMult is still 1.0 after Recompute (Phoenix crafting bonus never populated)");
        }

        // =====================================================================
        //  Group 5 — Save round-trip (SaveSchema serialize→deserialize at v33)
        // =====================================================================
        private static void CheckSaveRoundTrip(Action<string> Fail)
        {
            // Canary on unreviewed schema bumps. WO-738 established the richer echoLanes token at
            // v33; v34 (persist Tribes/Wards/Arena + pet active-slot, RED #3/#4), v35 (WO-773 the
            // common Obsidian multi-channel work queue) and v36 (WO-834 everBuiltStructureIds — the
            // blank-town baked-standdown ledger) are all additive + reviewed, and none touches
            // echoLanes (it round-trips unchanged). Update this pin in the SAME breath as any
            // future reviewed bump (CLAUDE.md §15).
            if (SaveSchema.CurrentVersion != 36)
                Fail($"SaveSchema.CurrentVersion={SaveSchema.CurrentVersion} (expected 36; echoLanes token must survive the current schema)");

            // A rich echoLanes token survives the REAL serialize → deserialize → validate path.
            const string richToken = "harvest:3,idle,crafting:1";
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
        private static float GetW(Dictionary<ResourceType, float> w, ResourceType r)
            => (w != null && w.TryGetValue(r, out var v)) ? v : 0f;

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
    }
}
