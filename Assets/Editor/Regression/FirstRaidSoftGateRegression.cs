// =============================================================================
// FirstRaidSoftGateRegression - WO-823 Phase E: the FIRST-RAID soft gate.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Headless, no scene load. Modelled on StrategicPlacementRegression (the closest
// precedent for an additive bool: migrator-seeds-the-right-default, round-trip,
// absent-on-old-save) plus SourceLint for the single-source gates.
//
// WHAT THIS PINS, and why each gate exists rather than being obvious:
//
//   1. SCHEMA TRIPLE - SaveSchema.CurrentVersion, the SaveMigrator top step and
//      the new wire field agree. The WO-823 E2 spec was written against v38 and
//      shipped saying "bump to 39"; by implementation time CurrentVersion was 40.
//      A doc cannot be the source for this number, so the oracle reads it off the
//      constant and only checks the three agree.
//
//   2. MIGRATION DERIVATION - a pre-v41 save must be DERIVED, not defaulted. The
//      field is the input to a gate that softens the FIRST raid; defaulting an
//      existing player to false would re-open a door they had already earned past.
//      Both derivation clauses read evidence ONLY a finished raid can write:
//      veterancyRank >= 1 (GrantVeterancy, 3-star clears only) and any
//      raidCooldowns record (RaidCooldownService, on a clear).
//
//   3. FAIL-OPEN, PROVEN - a genuinely fresh pre-v41 save derives FALSE. That is
//      the DOCUMENTED gap (a veteran with no veterancy and no live cooldown gets
//      one extra softened raid) and it is pinned deliberately so that nobody
//      later "fixes" it into a lock. A wrong FALSE costs a live player nothing;
//      a wrong TRUE would gate a new player behind the full army cap, which is
//      the unrecoverable direction.
//
//   4. WIRE ROUND-TRIP - true survives serialize->deserialize->Validate through
//      the REAL SaveSchema.JsonSettings, and an old payload with no key reads
//      back NULL (default-on-read intact, so an old save is loadable).
//
//   5. THE GATE MATH - the softened bar is 3 SLOTS, not a headcount (owner ruling
//      2026-08-24: "THE NUMBER IS 3 OF 10", and "3 of 10" means slots). Pinned
//      both directions: 3 deployable slots opens the door on a save that has
//      never raided, and the SAME 3 slots does NOT open it once the flag is set.
//
//   6. HEADLESS NEVER-FALSE-BLOCK - a null GameState still returns Ready with
//      FirstRaidSoftGate false (the WO-813/WO-820 contract Phase A established).
//
//   7. SINGLE SOURCE - FirstRaidMinDeployableSlots is defined ONCE and read ONCE,
//      both inside ArmyReadiness.cs. Phase E exists because readiness had grown a
//      SECOND opinion inside the raid screen; a copy of the constant elsewhere
//      would be a third. Source-linted over comment-stripped code, so a mention
//      in a comment neither satisfies nor trips this gate.
//
//   8. THE TWO BYPASSES ARE GONE - RaidDeployScreen.BuildDeployBar and OnDeploy
//      no longer gate on _vm.DeployableCount (a raw HEADCOUNT) while
//      ArmyReadiness is slot-weighted. That disagreement was the
//      grey-button-versus-open-gate defect in its original form, live before
//      Phase E added anything. Copy/label reads of DeployableCount elsewhere in
//      that file are legitimate and are NOT linted - only these two bodies are.
//
//   9. NO SECOND WRITER - EverCompletedRaid is written in exactly ONE runtime
//      file (RaidDeployController) and appears in NO raid screen, panel or VM.
//      A second stamp would fork the one-owner seam the field depends on.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "first-raid soft gate suite", () => { if (!DeNelle.Editor.Regression.FirstRaidSoftGateRegression.Run(out var firstRaidReason)) failures.Add(firstRaidReason); else log.AppendLine("[first-raid-soft-gate] " + firstRaidReason); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-823 Phase E oracle - the first-raid soft gate, end to end.</summary>
    public static class FirstRaidSoftGateRegression
    {
        private const string ArmyReadinessPath = "_Modules/Village/Troops/ArmyReadiness.cs";
        private const string DeployScreenPath = "_Modules/Village/Hero/RaidDeployScreen.cs";
        private const string DeployControllerPath = "_Modules/Village/Troops/RaidDeployController.cs";
        private const string ConstName = "FirstRaidMinDeployableSlots";
        private const string FlagName = "EverCompletedRaid";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- FIRST-RAID SOFT GATE (WO-823 Phase E) ---");

            var created = new List<UnityEngine.Object>();
            try
            {
                GateOne_SchemaTriple(failures, log);
                GateTwo_MigrationDerivation(failures, log);
                GateThree_WireRoundTrip(failures, log);
                GateFour_GateMath(created, failures, log);
                GateFive_SingleSource(failures, log);
                GateSix_BypassesRemoved(failures, log);
                GateSeven_OneWriter(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("first-raid-soft-gate oracle threw: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (var o in created)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("first-raid soft gate FAILED (").Append(failures.Count).Append("):");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                sb.Append('\n').Append(log);
                reason = sb.ToString();
                return false;
            }

            reason = "7 gates OK (schema triple, migration derivation, wire round-trip, gate math, " +
                     "single source, bypasses removed, one writer). Softened bar = " +
                     ArmyReadiness.FirstRaidMinDeployableSlots + " slots; schema v" +
                     SaveSchema.CurrentVersion + ".";
            return true;
        }

        // =====================================================================
        //  GATE 1 - the schema triple agrees with itself (never with a doc)
        // =====================================================================
        private static void GateOne_SchemaTriple(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 1] schema triple");

            int top = -1;
            try
            {
                var f = typeof(SaveMigrator).GetField("Steps",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null)
                {
                    failures.Add("SaveMigrator.Steps seam moved - the version triple cannot be checked");
                }
                else
                {
                    var steps = f.GetValue(null) as System.Collections.Generic.SortedDictionary<int,
                        Func<SaveSchema.PersistedState, SaveSchema.PersistedState>>;
                    if (steps == null || steps.Count == 0)
                        failures.Add("SaveMigrator.Steps is empty or a different type - version triple unverifiable");
                    else
                        foreach (var kv in steps) top = kv.Key;   // SortedDictionary -> last is highest
                }
            }
            catch (Exception ex)
            {
                failures.Add("reading SaveMigrator.Steps threw: " + ex.Message);
            }

            if (top >= 0 && top != SaveSchema.CurrentVersion)
                failures.Add("SaveMigrator top step is " + top + " but SaveSchema.CurrentVersion is " +
                             SaveSchema.CurrentVersion + " - the version triple is broken (CoreSaveContract " +
                             "pins this too; Phase E must ship a step for its own bump)");
            else if (top >= 0)
                log.AppendLine("  top step == CurrentVersion == " + SaveSchema.CurrentVersion + " ok");

            // The wire field must actually exist on PersistedState, and be NULLABLE
            // (default-on-read) - a non-nullable bool would make an old save
            // indistinguishable from one that answered false.
            var wire = typeof(SaveSchema.PersistedState).GetField(FlagName);
            if (wire == null)
                failures.Add("SaveSchema.PersistedState." + FlagName + " does not exist - Phase E's wire field is missing");
            else if (wire.FieldType != typeof(bool?))
                failures.Add("SaveSchema.PersistedState." + FlagName + " is " + wire.FieldType.Name +
                             " - it MUST be bool? so an absent key is distinguishable from an authored false");
            else log.AppendLine("  wire field present and nullable ok");

            var live = typeof(GameState).GetField(FlagName);
            if (live == null || live.FieldType != typeof(bool))
                failures.Add("GameState." + FlagName + " missing or not a bool - the live field is the gate's input");
        }

        // =====================================================================
        //  GATE 2 - a pre-v41 save is DERIVED, not defaulted
        // =====================================================================
        private static void GateTwo_MigrationDerivation(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 2] migration derivation");

            int from = SaveSchema.CurrentVersion - 1;

            // (a) a genuinely fresh pre-v41 save -> FALSE. Documented and deliberate:
            //     fail-open. A wrong FALSE is a softer first raid; a wrong TRUE would
            //     lock a new player behind the full cap.
            var fresh = SaveMigrator.Migrate(new SaveSchema.PersistedState(), from);
            if (fresh == null || !fresh.EverCompletedRaid.HasValue)
                failures.Add("migrate v" + from + "->current did not SET " + FlagName +
                             " - the Phase E migrator step is missing or did not run");
            else if (fresh.EverCompletedRaid.Value)
                failures.Add("a fresh pre-v" + SaveSchema.CurrentVersion + " save derived " + FlagName +
                             " = TRUE. The derivation must read raid EVIDENCE only; deriving true from " +
                             "nothing locks a brand-new player behind the full army cap");
            else log.AppendLine("  fresh save derives FALSE (fail-open, documented gap) ok");

            // (b) veterancy is raid evidence: AddVeterancy has exactly one caller
            //     (GrantVeterancy, from ReconcileRaidEnd at a 3-star clear).
            var vet = new SaveSchema.PersistedState { Army = new ArmyStorage() };
            vet.Army.Owned.Add(new PlayerTroop { Id = "troop-1", TroopDefId = "troop-footman", VeterancyRank = 1 });
            var vetOut = SaveMigrator.Migrate(vet, from);
            if (vetOut == null || !vetOut.EverCompletedRaid.HasValue || !vetOut.EverCompletedRaid.Value)
                failures.Add("a save carrying a troop at veterancyRank >= 1 did NOT derive " + FlagName +
                             " = true. Veterancy is only ever granted by ReconcileRaidEnd at a 3-star " +
                             "clear, so it is proof this player has finished a raid");
            else log.AppendLine("  veterancyRank >= 1 derives TRUE ok");

            // (c) a live camp cooldown is raid evidence: RaidCooldownService is the
            //     only writer and only stamps one on a clear.
            var cd = new SaveSchema.PersistedState
            {
                RaidCooldowns = new List<RaidCooldownRecord>
                {
                    new RaidCooldownRecord { ConfigId = "raider_camp_small", StartedUnixMs = 1000, DurationSeconds = 60 }
                }
            };
            var cdOut = SaveMigrator.Migrate(cd, from);
            if (cdOut == null || !cdOut.EverCompletedRaid.HasValue || !cdOut.EverCompletedRaid.Value)
                failures.Add("a save carrying a non-empty raidCooldowns list did NOT derive " + FlagName +
                             " = true. A cooldown record is only ever written when a camp is CLEARED");
            else log.AppendLine("  non-empty raidCooldowns derives TRUE ok");

            // (d) a rank-0 roster is NOT evidence - a player can train an army and
            //     never raid. This is the clause that keeps (b) honest.
            var trained = new SaveSchema.PersistedState { Army = new ArmyStorage() };
            trained.Army.Owned.Add(new PlayerTroop { Id = "troop-1", TroopDefId = "troop-footman", VeterancyRank = 0 });
            var trainedOut = SaveMigrator.Migrate(trained, from);
            if (trainedOut != null && trainedOut.EverCompletedRaid.HasValue && trainedOut.EverCompletedRaid.Value)
                failures.Add("a save with a trained but rank-0 roster derived " + FlagName + " = true - " +
                             "owning troops is not evidence of having RAIDED");
            else log.AppendLine("  rank-0 roster is not evidence ok");

            // (e) idempotent - an answered save is never overwritten.
            var answered = new SaveSchema.PersistedState { EverCompletedRaid = true };
            var answeredOut = SaveMigrator.Migrate(answered, from);
            if (answeredOut == null || !answeredOut.EverCompletedRaid.HasValue || !answeredOut.EverCompletedRaid.Value)
                failures.Add("the migrator OVERWROTE an already-answered " + FlagName +
                             " - it must only ever fill in a missing answer");
            else log.AppendLine("  an already-answered save is left alone ok");
        }

        // =====================================================================
        //  GATE 3 - the wire round-trips, and an old payload stays loadable
        // =====================================================================
        private static void GateThree_WireRoundTrip(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 3] wire round-trip");

            var outState = new SaveSchema.PersistedState { EverCompletedRaid = true };
            string json = JsonConvert.SerializeObject(outState, SaveSchema.JsonSettings);
            var back = JsonConvert.DeserializeObject<SaveSchema.PersistedState>(json, SaveSchema.JsonSettings);
            if (back == null) { failures.Add("v" + SaveSchema.CurrentVersion + " round-trip deserialized to null"); return; }

            var vr = SaveSchema.Validate(back);
            if (!vr.Ok)
                failures.Add("a save carrying " + FlagName + " FAILED validation: field '" + vr.FieldPath +
                             "' (" + vr.Reason + ")");
            if (!back.EverCompletedRaid.HasValue || !back.EverCompletedRaid.Value)
                failures.Add(FlagName + " did not survive the save round-trip (wrote true, read back " +
                             (back.EverCompletedRaid.HasValue ? "false" : "null") + ")");
            else log.AppendLine("  true survives serialize->deserialize->validate ok");

            // Default-on-read: a pre-v41 payload has no key at all and must load clean.
            var old = JsonConvert.DeserializeObject<SaveSchema.PersistedState>("{}", SaveSchema.JsonSettings);
            if (old == null)
                failures.Add("an old key-less payload deserialized to null - Phase E broke old-save loading");
            else if (old.EverCompletedRaid.HasValue)
                failures.Add("an old key-less payload read back " + FlagName + " = " + old.EverCompletedRaid.Value +
                             " instead of null - default-on-read is broken, and the migrator can no longer " +
                             "tell an unanswered save from an answered one");
            else log.AppendLine("  an old key-less payload reads back null ok");
        }

        // =====================================================================
        //  GATE 4 - the gate math: 3 SLOTS, first raid only, both directions
        // =====================================================================
        private static void GateFour_GateMath(List<UnityEngine.Object> created, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 4] gate math");

            int bar = ArmyReadiness.FirstRaidMinDeployableSlots;
            if (bar != 3)
                failures.Add("FirstRaidMinDeployableSlots is " + bar + ", not 3. The owner ruling is literal " +
                             "(\"soften the first raid. THE NUMBER IS 3 OF 10\") and the number is not the " +
                             "lead's to retune");

            var state = ScriptableObject.CreateInstance<GameState>();
            created.Add(state);
            state.Army = new ArmyStorage();
            int cap = state.Army.MaxArmySize;
            if (cap <= bar)
            {
                log.AppendLine("  army cap " + cap + " <= bar " + bar + " - soft gate is inert by design; skipping");
                return;
            }

            // Never raided: the softened bar opens the door at exactly `bar` slots.
            var atBar = ArmyReadiness.Compute(state.Army, bar, 0, everCompletedRaid: false);
            if (atBar.RequiredSlots != bar)
                failures.Add("a never-raided snapshot reported RequiredSlots = " + atBar.RequiredSlots +
                             ", expected the softened " + bar);
            if (!atBar.FirstRaidSoftGate)
                failures.Add("a never-raided snapshot did not set FirstRaidSoftGate - the copy/meter layer " +
                             "has no way to say WHY the bar is low");
            if (!atBar.Ready)
                failures.Add(bar + " deployable slots did not open the FIRST raid. This is the whole ruling: " +
                             "a new player must not have to fill " + cap + " slots before ever seeing a raid");
            if (atBar.CapSlots != cap)
                failures.Add("the softened snapshot moved CapSlots to " + atBar.CapSlots +
                             " - the cap is unchanged; only the REQUIREMENT softens");

            var belowBar = ArmyReadiness.Compute(state.Army, bar - 1, 0, everCompletedRaid: false);
            if (belowBar.Ready)
                failures.Add((bar - 1) + " deployable slots opened the first raid - the soft gate is a FLOOR, " +
                             "not an open door");

            // Already raided: the SAME slot count must no longer open it.
            var veteranAtBar = ArmyReadiness.Compute(state.Army, bar, 0, everCompletedRaid: true);
            if (veteranAtBar.RequiredSlots != cap)
                failures.Add("a veteran snapshot reported RequiredSlots = " + veteranAtBar.RequiredSlots +
                             ", expected the full cap " + cap);
            if (veteranAtBar.FirstRaidSoftGate)
                failures.Add("a veteran snapshot still claims FirstRaidSoftGate - the softening is FIRST RAID " +
                             "ONLY and must never come back");
            if (veteranAtBar.Ready)
                failures.Add(bar + " deployable slots opened a raid on a save that has ALREADY raided - the " +
                             "full-army gate must return permanently after the first raid");

            var veteranFull = ArmyReadiness.Compute(state.Army, cap, 0, everCompletedRaid: true);
            if (!veteranFull.Ready)
                failures.Add("a full deployable roster is not Ready on a veteran save - Phase E broke the " +
                             "WO-820 full-army rule it was only supposed to soften for raid one");

            // The default on the seam overload stays the STRICT reading, so an existing
            // caller (and every pre-Phase-E EditMode test) keeps the full-cap behaviour.
            var defaulted = ArmyReadiness.Compute(state.Army, bar, 0);
            if (defaulted.Ready)
                failures.Add("the seam overload's DEFAULT softened the gate. The default must be " +
                             "everCompletedRaid: true (strict), so adding the parameter cannot silently " +
                             "weaken an existing caller");

            // GameState drives it end to end - not just the seam.
            state.EverCompletedRaid = false;
            var live = ArmyReadiness.Compute(state);
            if (live.RequiredSlots != bar || !live.FirstRaidSoftGate)
                failures.Add("Compute(GameState) did not read GameState." + FlagName +
                             " - the persisted field is not actually wired to the gate");
            state.EverCompletedRaid = true;
            var liveVet = ArmyReadiness.Compute(state);
            if (liveVet.RequiredSlots != cap || liveVet.FirstRaidSoftGate)
                failures.Add("Compute(GameState) ignored " + FlagName + " = true - the gate never re-hardens");

            // Headless never-false-block (Phase A contract).
            var headless = ArmyReadiness.Compute((GameState)null);
            if (!headless.Ready)
                failures.Add("a null GameState is no longer Ready - Phase E broke the WO-813/WO-820 " +
                             "never-false-block rule and headless/AutoPilot will be gated out of raids");
            if (headless.FirstRaidSoftGate)
                failures.Add("a null GameState reported FirstRaidSoftGate = true - headless never meets the " +
                             "soft gate, so nothing may word its copy as a first raid");
            log.AppendLine("  3-slot floor, veteran re-hardening, strict default and headless bypass ok");
        }

        // =====================================================================
        //  GATE 5 - one definition, one read, both in ArmyReadiness.cs
        // =====================================================================
        private static void GateFive_SingleSource(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 5] single source");

            // Comment-stripped CODE only: a mention of the constant in a comment must
            // neither satisfy this gate nor trip it (SourceLint.ReadCode strips both
            // comments and literal contents).
            string readiness = SourceLint.ReadCode(ArmyReadinessPath, failures);
            int here = Occurrences(readiness, ConstName);
            if (here != 2)
                failures.Add(ConstName + " appears " + here + " time(s) in ArmyReadiness.cs code; expected " +
                             "exactly 2 (one definition, one read). Phase E's contract is one definition and " +
                             "one read, both in this file");
            else log.AppendLine("  1 definition + 1 read inside ArmyReadiness.cs ok");

            var elsewhere = new List<string>();
            foreach (var rel in RuntimeSources(failures))
            {
                if (rel.Replace('\\', '/').EndsWith(ArmyReadinessPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (Occurrences(SourceLint.ReadCode(rel, null), ConstName) > 0) elsewhere.Add(rel);
            }
            if (elsewhere.Count > 0)
                failures.Add(ConstName + " is referenced outside ArmyReadiness.cs (" +
                             string.Join(", ", elsewhere.ToArray()) + "). Phase E REMOVED a second readiness " +
                             "opinion; a copy of this constant anywhere else is a third");
            else log.AppendLine("  no runtime file outside ArmyReadiness.cs references it ok");
        }

        // =====================================================================
        //  GATE 6 - the two headcount bypasses are gone from the raid screen
        // =====================================================================
        private static void GateSix_BypassesRemoved(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 6] raid-screen bypasses removed");

            string screen = SourceLint.ReadCode(DeployScreenPath, failures);
            if (string.IsNullOrEmpty(screen)) return;

            string bar = SourceLint.Body(screen, @"private\s+void\s+BuildDeployBar\s*\([^)]*\)");
            string deploy = SourceLint.Body(screen, @"private\s+void\s+OnDeploy\s*\(\s*\)");

            if (string.IsNullOrEmpty(bar))
                failures.Add("RaidDeployScreen.BuildDeployBar not found - the deploy-button gate moved and " +
                             "this oracle can no longer see it");
            else if (Occurrences(bar, "DeployableCount") > 0)
                failures.Add("RaidDeployScreen.BuildDeployBar still gates the DEPLOY button on " +
                             "_vm.DeployableCount, a raw HEADCOUNT, while ArmyReadiness is SLOT-weighted. " +
                             "That disagreement IS the grey-button-versus-open-gate defect");
            else log.AppendLine("  BuildDeployBar no longer reads DeployableCount ok");

            if (string.IsNullOrEmpty(deploy))
                failures.Add("RaidDeployScreen.OnDeploy not found - the deploy handler moved");
            else if (Occurrences(deploy, "DeployableCount") > 0)
                failures.Add("RaidDeployScreen.OnDeploy still gates on _vm.DeployableCount - the SECOND copy " +
                             "of the same headcount bypass");
            else log.AppendLine("  OnDeploy no longer reads DeployableCount ok");

            // ...and the screen routes through the ONE formula instead.
            if (Occurrences(screen, "ArmyReadiness.Compute") == 0)
                failures.Add("RaidDeployScreen no longer calls ArmyReadiness.Compute - removing the bypasses " +
                             "without routing through the snapshot leaves the screen with NO readiness input");
        }

        // =====================================================================
        //  GATE 7 - exactly one runtime writer, and none of them is a screen
        // =====================================================================
        private static void GateSeven_OneWriter(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 7] one writer");

            string controller = SourceLint.ReadCode(DeployControllerPath, failures);
            if (Occurrences(controller, FlagName + " = true") == 0)
                failures.Add("RaidDeployController does not stamp " + FlagName + " = true. It is the LATCHED " +
                             "seam every raid exit funnels through (victory, retreat, hero death), so the " +
                             "stamp belongs there and only there");
            else log.AppendLine("  RaidDeployController carries the one stamp ok");

            var writers = new List<string>();
            foreach (var rel in RuntimeSources(failures))
            {
                string norm = rel.Replace('\\', '/');
                if (norm.EndsWith(DeployControllerPath, StringComparison.OrdinalIgnoreCase)) continue;
                // The state layer legitimately assigns it (capture / restore / new game).
                if (norm.IndexOf("/Core/State/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (Occurrences(SourceLint.ReadCode(rel, null), FlagName + " =") > 0) writers.Add(rel);
            }
            if (writers.Count > 0)
                failures.Add(FlagName + " is ASSIGNED outside RaidDeployController and the save layer (" +
                             string.Join(", ", writers.ToArray()) + "). A second writer forks the one-owner " +
                             "seam - which is exactly the defect Phase E removed from the raid screen");
            else log.AppendLine("  no second writer ok");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static int Occurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        /// <summary>
        /// Every runtime .cs under Assets/_Modules, as a path relative to Assets/ (the shape
        /// SourceLint.ReadCode takes). Editor + test code is deliberately OUT of scope: this
        /// oracle itself names both symbols, and an oracle that fails on its own text is a
        /// trap rather than a gate.
        /// </summary>
        private static List<string> RuntimeSources(List<string> failures)
        {
            var list = new List<string>();
            try
            {
                string root = Path.Combine(Application.dataPath, "_Modules");
                if (!Directory.Exists(root))
                {
                    if (failures != null) failures.Add("Assets/_Modules not found - the runtime sweep cannot run");
                    return list;
                }
                foreach (var abs in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                    list.Add(abs.Substring(Application.dataPath.Length + 1));
            }
            catch (IOException ex)
            {
                if (failures != null) failures.Add("runtime source sweep threw: " + ex.Message);
            }
            return list;
        }
    }
}
