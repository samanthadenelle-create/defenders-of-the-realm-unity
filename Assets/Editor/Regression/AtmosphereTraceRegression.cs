// =============================================================================
// AtmosphereTraceRegression - WO-1602 (in the first minutes of a new game the town
// ground reads as blue-green water, then the scene sits under heavy pale haze).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll between the oracle fences.
//
// HONEST SCOPE, STATED FIRST (WO-1494: six suites claimed to MEASURE and were lint).
// THIS SUITE IS A SOURCE LINT AND EVERY REASON STRING SAYS SO. It opens named files,
// slices named method BODIES by brace matching, and asserts the instrumentation is
// present and of the right SHAPE. It cannot render a frame, cannot read a fog density
// at runtime, and therefore CANNOT prove the town looks right. That proof is a fleet
// run plus the owner's eyes (WO-1602 acceptance), and this file never claims it.
//
// WHAT IT PINS, AND WHY EACH ONE EARNS ITS PLACE
//   1. [writers]  every method in the tree that ASSIGNS a RenderSettings atmosphere
//                 property carries a FlowTrace call tagged "Atmos" inside its OWN body.
//                 File-scope presence is not enough: WO-1483's lesson was that a Measure
//                 on a load path passed a file-scope grep while the frame path stayed
//                 unmeasured. The whole point of WO-1602 is that the owner's transient
//                 had NO writer attribution at all - the device break-log for that
//                 session (11421 lines) contained zero fog lines - so an unsigned writer
//                 is the exact defect, not a style nit.
//   2. [throttle] the writers that run PER FRAME use Throttle (or the 4-arg Measure),
//                 never a bare Step. A per-frame atmosphere line evicts the boot window
//                 out of the 256 KiB Android logcat ring and destroys the evidence it was
//                 added to collect (CLAUDE.md sec.12; memory
//                 logcat-ring-buffer-destroys-evidence). Instrumentation that erases the
//                 log is worse than none, so the shape is pinned, not just the presence.
//   3. [probe]    AtmosphereProbe still exists, still samples out to at least 300s (the
//                 window the ticket names - both owner frames land inside it), and its
//                 ladder is BOUNDED. An unbounded probe is a per-frame log by another name.
//   4. [readonly] AtmosphereProbe ASSIGNS no RenderSettings property. A witness that
//                 writes is a writer, and it would then be competing with the very systems
//                 it exists to attribute - the failure mode this ticket is about.
//   5. [terrain]  MagentaGuard still emits the [Flow:Terrain] BIND verdict naming the
//                 layer count and the missing-base-colour count. That line is what answers
//                 the ticket's "did the terrain layers arrive late" question; without it
//                 the only terrain evidence is a long FloorDiag line a human must parse.
//
// CLAUDE.md sec.12 forbids stripping instrumentation once a system is stabilised. These
// five rules are how that survives the next edit to any of six unrelated files.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DeNelle.Editor.Regression
{
    public static class AtmosphereTraceRegression
    {
        private const string ProbePath =
            "Assets/_Modules/Village/World/AtmosphereProbe.cs";
        private const string MagentaGuardPath =
            "Assets/_Modules/Core/MagentaGuard.cs";

        /// <summary>
        /// The sampler must reach at least this far. Both of the owner's bad frames sit
        /// inside the first ~4 minutes and the ticket names "the first 5 minutes"; a probe
        /// that stops at 60s would have missed the dense-haze frame entirely.
        /// </summary>
        private const float RequiredProbeHorizonSec = 300f;

        /// <summary>
        /// An unbounded ladder is a per-frame log wearing a coroutine. Keeping the sample
        /// count small is what makes the probe free after its window closes.
        /// </summary>
        private const int MaxProbeSamples = 24;

        // Declared as a PAIR so this file's own brace tally stays balanced - CLAUDE.md sec.1
        // runs a naive open-vs-close count over every .cs and a lone open-brace char literal
        // reads to that gate as a missing close.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>
        /// A method that writes the global atmosphere. <c>PerFrame</c> means the body runs on
        /// an Update/tick path, so its trace must be rate-limited.
        /// </summary>
        private struct WriterSite
        {
            public string Path;
            public string Signature;
            public bool PerFrame;
            /// <summary>
            /// True for the methods that assign a RenderSettings property. FALSE for the URP
            /// Volume grade, which changes the LOOK of the frame just as decisively while
            /// touching no RenderSettings field at all. Asserting a RenderSettings write there
            /// was this suite's own first failure on its first dry run: it would have failed a
            /// correctly-instrumented site and pushed the next seat to "fix" working code.
            /// </summary>
            public bool WritesRenderSettings;
            public string Why;
        }

        private static readonly WriterSite[] Writers =
        {
            new WriterSite {
                Path = "Assets/_Modules/Village/World/WorldFeelInjector.cs",
                Signature = "private void ApplySkySunAmbientFog()", PerFrame = false, WritesRenderSettings = true,
                Why = "the town's baseline fog/ambient/sun/skybox writer - it re-runs on EVERY " +
                      "sceneLoaded and activeSceneChanged, so it is the highest-frequency fog " +
                      "writer in town and the one a heavy reading must be checked against" },
            new WriterSite {
                Path = "Assets/_Modules/Village/World/WorldFeelInjector.cs",
                Signature = "private void ApplyPostVolume()", PerFrame = false, WritesRenderSettings = false,
                Why = "the DDOL global grade: +0.75EV post-exposure and Bloom 4.5. A pale, " +
                      "washed-out frame is what an over-applied exposure looks like and a " +
                      "screenshot cannot separate it from dense fog - so the grade state must " +
                      "sit on the same timeline as the fog numbers" },
            new WriterSite {
                Path = "Assets/_Modules/Village/Arena/BattleArena.cs",
                Signature = "private void ApplyCavernMood()", PerFrame = false, WritesRenderSettings = true,
                Why = "saves the open world's atmosphere and overwrites it with a dense cavern " +
                      "fog; nothing checks _moodSaved before the save, so a second apply could " +
                      "capture the cavern values AS the town's" },
            new WriterSite {
                Path = "Assets/_Modules/Village/Arena/BattleArena.cs",
                Signature = "private void RestoreCavernMood()", PerFrame = false, WritesRenderSettings = true,
                Why = "the other half of that pair - the write that would hand a leaked cavern " +
                      "density back to the open world" },
            new WriterSite {
                Path = "Assets/_Modules/Dungeons/Lantern.cs",
                Signature = "private void ApplyDarknessVisibility()", PerFrame = true, WritesRenderSettings = true,
                Why = "flips fogMode to Linear with a 0.45m..3.2m wall, every frame. " +
                      "RenderSettings is global to the active scene, so a Lantern alive while " +
                      "the town is active would produce exactly the owner's haze frame" },
            new WriterSite {
                Path = "Assets/_Modules/Dungeons/Lantern.cs",
                Signature = "private void RestoreDungeonFog()", PerFrame = false, WritesRenderSettings = true,
                Why = "restores fogMode/start/end from values captured in this Lantern's own " +
                      "Awake, which is only correct if Awake ran in the scene the restore lands in" },
            new WriterSite {
                Path = "Assets/_Modules/Village/Waves/SkyProgressionController.cs",
                Signature = "private void Update()", PerFrame = true, WritesRenderSettings = true,
                Why = "an unconditional per-frame fogDensity + ambientLight write. Measured " +
                      "2026-09-07 this component is attached to NO scene and NO prefab, so it is " +
                      "dormant - but if it is ever attached it OWNS both channels and drags every " +
                      "other writer's value back toward its own target within ~2s" },
            new WriterSite {
                Path = "Assets/_Modules/Village/Waves/SkyProgressionController.cs",
                Signature = "private void OnDisable()", PerFrame = false, WritesRenderSettings = true,
                Why = "writes back the baseline captured in Awake - which, if Awake ran before " +
                      "WorldFeelInjector applied, is a PRE-INJECTOR sky being written over the town's" },
            new WriterSite {
                Path = "Assets/_Modules/Environment/NightTorchLightSystem.cs",
                Signature = "private void ApplyAmbientFloor(float nightT)", PerFrame = true, WritesRenderSettings = true,
                Why = "a per-frame ambientLight write called from Update. It installs only in " +
                      "Village2, so its appearance on an overworld timeline would itself be the finding" },
        };

        /// <summary>Any assignment to a RenderSettings atmosphere property (assignment, not comparison).</summary>
        private static readonly Regex AtmosphereWrite = new Regex(
            @"RenderSettings\.(fog|fogMode|fogColor|fogDensity|fogStartDistance|fogEndDistance|" +
            @"ambientLight|ambientIntensity|ambientMode|ambientSkyColor|ambientEquatorColor|" +
            @"ambientGroundColor|skybox|sun)\s*=(?!=)",
            RegexOptions.Compiled);

        /// <summary>A FlowTrace call carrying the shared "Atmos" system tag.</summary>
        private static readonly Regex AtmosTrace = new Regex(
            @"FlowTrace\.(Step|Warn|Fail|Throttle|Once)\s*\(\s*""Atmos""",
            RegexOptions.Compiled);

        /// <summary>The rate-limited forms. Measure's 4-arg overload accumulates instead of logging.</summary>
        private static readonly Regex RateLimitedTrace = new Regex(
            @"FlowTrace\.(Throttle|Once)\s*\(\s*""Atmos""",
            RegexOptions.Compiled);

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int writersChecked = 0;

            try
            {
                string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;

                // ---- 1 + 2: every atmosphere writer signs its own body, in the right shape ----
                foreach (var site in Writers)
                {
                    string full = Path.Combine(root, site.Path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full))
                    {
                        failures.Add("[writers] " + site.Path + " is MISSING - the WO-1602 atmosphere " +
                                     "trace cannot be pinned on a file that no longer exists. If it moved, " +
                                     "update this suite's Writers table in the same change.");
                        continue;
                    }

                    string src = File.ReadAllText(full);
                    string body = ExtractMethodBody(src, site.Signature);
                    if (string.IsNullOrEmpty(body))
                    {
                        failures.Add("[writers] " + site.Path + " no longer declares '" + site.Signature +
                                     "' (or its body could not be brace-matched). That method is " +
                                     site.Why + ". A renamed writer must carry its trace across the rename.");
                        continue;
                    }

                    writersChecked++;

                    if (site.WritesRenderSettings && !AtmosphereWrite.IsMatch(body))
                    {
                        // Not a failure of instrumentation - a failure of this suite's own map.
                        // Saying so is the difference between a stale pin and a silent one.
                        failures.Add("[writers] '" + site.Signature + "' in " + site.Path +
                                     " no longer ASSIGNS any RenderSettings atmosphere property. Either the " +
                                     "write moved (follow it and re-point this row) or the site is dead and the " +
                                     "row should be deleted - leaving it here pins nothing.");
                        continue;
                    }

                    if (!AtmosTrace.IsMatch(body))
                    {
                        failures.Add("[writers] '" + site.Signature + "' in " + site.Path + " writes the global " +
                                     "atmosphere with NO FlowTrace(\"Atmos\", ...) inside its own body. It is " +
                                     site.Why + ". WO-1602 exists because the owner's transient had zero writer " +
                                     "attribution: the device break-log for that session carried 11421 lines and " +
                                     "not one fog line. An unsigned writer is the defect (CLAUDE.md sec.12 - " +
                                     "NEVER STRIP FLOWTRACE).");
                        continue;
                    }

                    if (site.PerFrame && !RateLimitedTrace.IsMatch(body))
                    {
                        failures.Add("[throttle] '" + site.Signature + "' in " + site.Path + " runs PER FRAME but " +
                                     "its \"Atmos\" trace is a bare Step. At 60fps that is a line every frame; it " +
                                     "evicts the boot window out of the 256 KiB Android logcat ring and destroys " +
                                     "the evidence the trace was added to collect (memory: " +
                                     "logcat-ring-buffer-destroys-evidence). Use FlowTrace.Throttle or .Once.");
                    }
                }

                // ---- 3 + 4: the sampler is bounded, reaches the ticket's window, and never writes ----
                string probeFull = Path.Combine(root, ProbePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(probeFull))
                {
                    failures.Add("[probe] " + ProbePath + " is MISSING. It is the ONLY thing in the tree that " +
                                 "samples the atmosphere on a timeline; every other trace is a point event at " +
                                 "scene load. Without it a transient that resolves itself - which is exactly what " +
                                 "WO-1602 describes - is structurally invisible again.");
                }
                else
                {
                    string probe = File.ReadAllText(probeFull);

                    var samples = new List<float>();
                    // Brace characters are written as \x7B / \x7D on purpose: CLAUDE.md sec.1's
                    // gate is a naive open-vs-close tally over the whole file, so a literal brace
                    // inside a regex reads to it as an unbalanced block. Escapes keep both the
                    // regex and the gate correct without a second, weaker check.
                    var ladder = Regex.Match(probe, @"SampleTimesSec\s*=\s*\x7B([^\x7D]*)\x7D");
                    if (!ladder.Success)
                    {
                        failures.Add("[probe] AtmosphereProbe no longer declares a SampleTimesSec ladder. The " +
                                     "bounded ladder IS the design: it is what lets the probe answer 'what did " +
                                     "the sky look like two minutes in' without logging every frame.");
                    }
                    else
                    {
                        foreach (Match m in Regex.Matches(ladder.Groups[1].Value, @"(\d+(?:\.\d+)?)f?"))
                        {
                            float v;
                            if (float.TryParse(m.Groups[1].Value,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out v))
                                samples.Add(v);
                        }

                        float horizon = 0f;
                        for (int i = 0; i < samples.Count; i++) if (samples[i] > horizon) horizon = samples[i];

                        if (horizon < RequiredProbeHorizonSec)
                            failures.Add("[probe] the sample ladder stops at T+" + horizon.ToString("0") + "s, short of " +
                                         "the required T+" + RequiredProbeHorizonSec.ToString("0") + "s. The owner's " +
                                         "dense-haze frame was captured ~3.5 minutes into a new game; a ladder that " +
                                         "ends sooner cannot see the state it was built to explain.");

                        if (samples.Count == 0)
                            failures.Add("[probe] the sample ladder parsed to ZERO entries - the probe would arm and " +
                                         "never sample, which reads in a log exactly like a probe that is working.");
                        else if (samples.Count > MaxProbeSamples)
                            failures.Add("[probe] the ladder holds " + samples.Count + " samples (cap " + MaxProbeSamples +
                                         "). Past that it stops being a bounded timeline and becomes the periodic " +
                                         "spam CLAUDE.md sec.12 forbids on a device.");
                    }

                    if (AtmosphereWrite.IsMatch(probe))
                        failures.Add("[readonly] AtmosphereProbe ASSIGNS a RenderSettings atmosphere property. It is a " +
                                     "witness, not a writer: the moment it writes it competes with the systems it " +
                                     "exists to attribute, and the attribution it prints becomes its own. Read only.");
                }

                // ---- 5: the terrain verdict the ticket actually asked for ----
                string mgFull = Path.Combine(root, MagentaGuardPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(mgFull))
                {
                    failures.Add("[terrain] " + MagentaGuardPath + " is MISSING - it owns the only runtime " +
                                 "terrain-binding trace in the game (the terrain is otherwise bound by an " +
                                 "EDITOR-only builder, so there is no other seam to instrument).");
                }
                else
                {
                    string mg = File.ReadAllText(mgFull);
                    if (!Regex.IsMatch(mg, @"FlowTrace\.Step\s*\(\s*""Terrain""") ||
                        mg.IndexOf("layersMissingBaseColor", StringComparison.Ordinal) < 0)
                    {
                        failures.Add("[terrain] MagentaGuard no longer emits the [Flow:Terrain] BIND verdict with " +
                                     "layersMissingBaseColor. That one greppable line is what answers WO-1602's " +
                                     "'did the terrain layers arrive late, or is the ground showing a base colour' " +
                                     "as a yes/no. The long FloorDiag line beside it carries the same data but has " +
                                     "to be parsed by a human, which is why it was not enough on its own.");
                    }
                }
            }
            catch (Exception ex)
            {
                reason = "AtmosphereTraceRegression THREW " + ex.GetType().Name + ": " + ex.Message +
                         " - the suite could not run, which is NOT a pass.";
                return false;
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("SOURCE LINT FAIL (").Append(failures.Count).Append(" issue(s)) - WO-1602 atmosphere/terrain " +
                          "instrumentation. This suite reads source text only; it cannot prove the town looks right.");
                for (int i = 0; i < failures.Count; i++) sb.Append("\n    - ").Append(failures[i]);
                reason = sb.ToString();
                return false;
            }

            reason = "SOURCE LINT PASS - " + writersChecked + " atmosphere writer(s) each sign their own body with a " +
                     "[Flow:Atmos] trace (per-frame ones rate-limited); AtmosphereProbe samples a bounded ladder to " +
                     "T+" + RequiredProbeHorizonSec.ToString("0") + "s and writes nothing; MagentaGuard still emits the " +
                     "[Flow:Terrain] BIND verdict. THIS IS TEXT, NOT A FRAME: it proves the traces exist, never that " +
                     "the sky is right - that needs the fleet run plus the owner's eyes (WO-1602 acceptance).";
            return true;
        }

        /// <summary>
        /// The body of the method whose signature line matches, by brace matching from the
        /// first open brace after it. Anywhere-in-the-file is deliberately not good enough:
        /// WO-1483 proved a file-scope grep passes on a trace that sits on a different path
        /// from the one that matters.
        /// </summary>
        private static string ExtractMethodBody(string src, string signature)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            int sig = src.IndexOf(signature, StringComparison.Ordinal);
            if (sig < 0) return string.Empty;
            int open = src.IndexOf(OpenBrace, sig);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == OpenBrace) depth++;
                else if (src[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return string.Empty;
        }
    }
}
