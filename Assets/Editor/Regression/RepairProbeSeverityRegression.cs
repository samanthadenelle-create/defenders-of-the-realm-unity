// =============================================================================
// RepairProbeSeverityRegression [repair-probe-severity] — WO-1580
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Registered in DataRegression.RunAll.
//
// THE DEFECT (owner Seeker session 2026-09-07, build 2026.09.07.359076, F8 seq
// 4698, scene RaidBase_fortified_garrison, kind=error):
//
//   [Flow:RepairProbe] SURFACES scene='RaidBase_fortified_garrison'
//   WallRepairController=ABSENT HubRepairAffordance=ABSENT
//   WaveManager=none(pure hub) -> NO repair surface exists in this scene at all
//   while a structure burns. The player has no way to repair anything here.
//
// RepairAvailabilityProbe.ReportSurfaces published the both-absent line through
// FlowTrace.Fail, which is ERROR level, which is precisely what the F8 harness
// records (FlowTrace.cs:184 — the break-log listener records only Error /
// Exception / Assert). Inside an enemy raid base the absence of a repair surface
// is the AUTHORED state — the player is there to destroy structures — so every
// raid with a burning structure minted an error capture the owner then had to
// triage.
//
// WHAT THIS ORACLE PINS. The severity decision now lives in the public static
// seam RepairAvailabilityProbe.EmitSurfaceLine(sceneName, noSurfaceAtAll, line),
// so it is decidable from a scene NAME with no scene loaded and no play mode.
// This suite installs a CAPTURING ITraceSink (FlowTrace.Sink is swappable by
// design, FlowTrace.cs:60-66), drives that seam once per case, and reads the
// severity the sink actually received — not the source text, and not what the
// call site was believed to do.
//
//   CASE A  raid base, both surfaces absent   -> 0 Error, 0 Warn, >=1 Info that
//           still carries the SURFACES line and says the absence is expected.
//           This is the acceptance criterion of the ticket.
//   CASE B  hub scene, both surfaces absent   -> >=1 Error. The probe must keep
//           shouting where the absence IS a defect; a fix that silenced both
//           scene classes would pass CASE A alone, which is why B exists.
//   CASE C  SCOPE PIN — Garrison_* / Outpost* / Dungeon_* / Village2, both
//           absent -> still Error. HubScenes.cs:143-152 records that the enemy
//           outposts are deliberately NOT IsRaid and that whether they are
//           committed assaults is an owner question nobody has asked. WO-1580
//           proves exactly ONE scene class. This case fails a future widening to
//           IsEnemyOutpost / IsDungeon so the widening arrives as an owner
//           decision rather than as a quiet silence.
//   CASE D  raid base with a surface PRESENT  -> Info, no Error. Branch-order
//           sanity: the raid branch must sit under the both-absent test, not
//           over it.
//   CASE E  null / empty scene name, both absent -> Error. An unknown scene is
//           not a raid, and the honest default for an undecidable name is the
//           loud one.
//
// FIXTURE HONESTY. FlowTrace.Allowed() also consults the Only/Mute category
// filters, which are write-only (no getter to save and restore). This suite does
// NOT clear them — mutating global trace state to make a fixture pass is the
// hollow-pass shape. Instead it emits one canary line first and FAILS if the sink
// never sees it, so a muted "RepairProbe" category reads as UNDECIDABLE rather
// than as five silent green cases. FlowTrace.Enabled is saved, forced true and
// restored in a finally, as is the sink.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.RepairProbeSeverityRegression.RunStandalone
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class RepairProbeSeverityRegression
    {
        /// <summary>Batchmode entry point (run-unity-method.ps1).</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log(reason);
            else Debug.LogError(reason);
        }

        // ---------------------------------------------------------------------
        //  Capturing sink — the measurement instrument.
        // ---------------------------------------------------------------------
        private sealed class CaptureSink : ITraceSink
        {
            public readonly List<string> Info = new List<string>();
            public readonly List<string> Warn = new List<string>();
            public readonly List<string> Error = new List<string>();

            void ITraceSink.Info(string line) { Info.Add(line ?? string.Empty); }
            void ITraceSink.Warn(string line) { Warn.Add(line ?? string.Empty); }
            void ITraceSink.Error(string line) { Error.Add(line ?? string.Empty); }

            public void Clear() { Info.Clear(); Warn.Clear(); Error.Clear(); }
            public string Counts() { return $"info={Info.Count} warn={Warn.Count} error={Error.Count}"; }
        }

        /// <summary>The shape of the real surfaces line, so the fixture is not a bare string.</summary>
        private static string SurfacesLine(string sceneName, bool noSurfaceAtAll)
        {
            return $"SURFACES scene='{sceneName}' " +
                   (noSurfaceAtAll
                        ? "WallRepairController=ABSENT HubRepairAffordance=ABSENT "
                        : "WallRepairController=present+ENABLED HubRepairAffordance=present:idle ") +
                   "WaveManager=none(pure hub)";
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== RepairProbeSeverityRegression (WO-1580): scene name in, FlowTrace severity out ===");

            var sink = new CaptureSink();
            ITraceSink priorSink = FlowTrace.Sink;
            bool priorEnabled = FlowTrace.Enabled;

            try
            {
                FlowTrace.Enabled = true;
                FlowTrace.Sink = sink;

                // -- CANARY: prove the instrument can hear this category at all. --
                FlowTrace.Step("RepairProbe", "severity-oracle canary");
                if (sink.Info.Count == 0)
                {
                    failures.Add("fixture UNDECIDABLE: a FlowTrace.Step on the 'RepairProbe' category reached the " +
                                 "capture sink 0 times, so this suite cannot observe severity at all (a category " +
                                 "Only/Mute filter is set, or the sink swap did not take). No case below is trusted.");
                    return Verdict(failures, log, out reason);
                }
                sink.Clear();

                // -- CASE A: the captured scene. Both absent, raid base -> no error. --
                foreach (string raid in new[]
                         {
                             SceneRouter.RaidBaseFortifiedGarrison,   // the scene in F8 seq 4698
                             SceneRouter.RaidBaseRaiderCampSmall,
                             SceneRouter.RaidBaseMageEnclave,
                         })
                {
                    sink.Clear();
                    RepairAvailabilityProbe.EmitSurfaceLine(raid, true, SurfacesLine(raid, true));
                    log.AppendLine($"  CASE A '{raid}' both-absent -> {sink.Counts()}");

                    if (sink.Error.Count != 0)
                        failures.Add($"CASE A '{raid}': a raid base with NO repair surface emitted {sink.Error.Count} " +
                                     "ERROR line(s). Error level is what the F8 harness captures (FlowTrace.cs:184), " +
                                     "so every raid with a burning structure mints a capture again — this is WO-1580 " +
                                     "recurring.");
                    if (sink.Warn.Count != 0)
                        failures.Add($"CASE A '{raid}': emitted {sink.Warn.Count} WARN line(s); the designed absence " +
                                     "must report at Step (info) level.");
                    if (sink.Info.Count == 0)
                        failures.Add($"CASE A '{raid}': emitted NOTHING. The information must survive the downgrade — " +
                                     "the probe was silenced instead of quietened, which strips instrumentation " +
                                     "(CLAUDE.md section 12: never strip FlowTrace).");
                    else
                    {
                        string joined = string.Join(" | ", sink.Info.ToArray());
                        if (joined.IndexOf("SURFACES scene='" + raid + "'", System.StringComparison.Ordinal) < 0)
                            failures.Add($"CASE A '{raid}': the Step line no longer carries the SURFACES payload " +
                                         "— the capture would name no scene and no surface state: " + joined);
                        if (joined.IndexOf("expected", System.StringComparison.OrdinalIgnoreCase) < 0)
                            failures.Add($"CASE A '{raid}': the Step line does not say the absence is EXPECTED, so a " +
                                         "reader cannot tell a designed absence from a downgraded defect: " + joined);
                    }
                }

                // -- CASE B: the hub. Both absent -> still a defect, still ERROR. --
                foreach (string hub in SceneRouter.CastleCandidates)
                {
                    sink.Clear();
                    RepairAvailabilityProbe.EmitSurfaceLine(hub, true, SurfacesLine(hub, true));
                    log.AppendLine($"  CASE B '{hub}' both-absent -> {sink.Counts()}");

                    if (sink.Error.Count == 0)
                        failures.Add($"CASE B '{hub}': a HUB with no repair surface at all emitted no ERROR line. " +
                                     "That absence is the defect this probe exists to catch (F8 seq 2153) and the " +
                                     "WO-1580 downgrade was scoped to raid bases only — the whole probe has gone quiet.");
                    else if (string.Join(" | ", sink.Error.ToArray())
                                   .IndexOf("NO repair surface", System.StringComparison.Ordinal) < 0)
                        failures.Add($"CASE B '{hub}': the error line no longer names the missing surface: " +
                                     string.Join(" | ", sink.Error.ToArray()));
                }

                // -- CASE C: SCOPE PIN. Everything that is NOT IsRaid keeps the error. --
                foreach (string other in new[]
                         {
                             "Garrison_hollow_watch",          // HubScenes.IsEnemyOutpost, deliberately NOT IsRaid
                             "Outpost1",                       // the hand-named outpost family
                             SceneRouter.DungeonFrostStair,    // a dungeon
                             SceneRouter.Village,              // Village2, the raid TARGET town (a hub by name)
                         })
                {
                    sink.Clear();
                    RepairAvailabilityProbe.EmitSurfaceLine(other, true, SurfacesLine(other, true));
                    log.AppendLine($"  CASE C '{other}' both-absent -> {sink.Counts()} (IsRaid={HubScenes.IsRaid(other)})");

                    if (HubScenes.IsRaid(other))
                    {
                        failures.Add($"CASE C '{other}': HubScenes.IsRaid now returns TRUE for this scene, so the " +
                                     "scope pin is vacuous. IsRaid was widened beyond RaidBase_* — read " +
                                     "HubScenes.cs:143-152 and get an owner ruling before that ships.");
                        continue;
                    }
                    if (sink.Error.Count == 0)
                        failures.Add($"CASE C '{other}': a non-raid scene with no repair surface stopped reporting at " +
                                     "ERROR level. WO-1580 scoped the downgrade to HubScenes.IsRaid ONLY; whether a " +
                                     "garrison/outpost/dungeon is a committed assault is an owner question that has " +
                                     "not been asked (HubScenes.cs:143-152). Widening the branch silences a defect " +
                                     "nothing has ruled on.");
                }

                // -- CASE D: branch order. A raid WITH a surface still reports, and not as an error. --
                {
                    string raid = SceneRouter.RaidBaseFortifiedGarrison;
                    sink.Clear();
                    RepairAvailabilityProbe.EmitSurfaceLine(raid, false, SurfacesLine(raid, false));
                    log.AppendLine($"  CASE D '{raid}' surface-present -> {sink.Counts()}");

                    if (sink.Error.Count != 0)
                        failures.Add($"CASE D '{raid}': a raid base that DOES carry a repair surface emitted an ERROR " +
                                     "line — the both-absent test and the scene-class test are in the wrong order.");
                    if (sink.Info.Count == 0)
                        failures.Add($"CASE D '{raid}': a present surface reported nothing at all; the surfaces line " +
                                     "must still be traced on change.");
                }

                // -- CASE E: an unknown scene name is not a raid. Loud by default. --
                foreach (string unknown in new[] { null, string.Empty })
                {
                    sink.Clear();
                    RepairAvailabilityProbe.EmitSurfaceLine(unknown, true, SurfacesLine(unknown ?? "<null>", true));
                    log.AppendLine($"  CASE E scene={(unknown == null ? "<null>" : "<empty>")} both-absent -> {sink.Counts()}");

                    if (sink.Error.Count == 0)
                        failures.Add($"CASE E scene={(unknown == null ? "<null>" : "<empty>")}: an unresolvable scene " +
                                     "name took the raid branch. An unknown scene is not a proven raid base, and the " +
                                     "honest default for an undecidable name is the loud one.");
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"RepairProbeSeverityRegression threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                FlowTrace.Sink = priorSink;
                FlowTrace.Enabled = priorEnabled;
            }

            return Verdict(failures, log, out reason);
        }

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "REPAIR PROBE SEVERITY OK - a raid base with no repair surface reports the SAME line at Step " +
                         "level and emits no error (so no F8 capture), while every other scene class - hub, garrison, " +
                         "outpost, dungeon, Village2, and an unresolvable name - still fails loudly";
                return true;
            }
            reason = $"REPAIR PROBE SEVERITY: {failures.Count} failure(s): " + string.Join(" | ", failures) +
                     "\n" + log;
            return false;
        }
    }
}
