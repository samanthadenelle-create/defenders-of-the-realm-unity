// =============================================================================
// AutoPilotTickets — headless emitter that turns an AutoPilot playtest run into a
// triaged ticket list.
// -----------------------------------------------------------------------------
// Reads the artifacts the runtime AutoPilot run left behind:
//   * <persistentDataPath>/break-log.jsonl   — one BreakRecord per line (written
//        by BreakCaptureHarness; FlowTrace "Auto" Warn/Fail land here too).
//   * <persistentDataPath>/autopilot-summary.json — per-phase status/duration.
//
// It groups + dedupes breaks by (kind + normalized message) — stripping volatile
// numbers (coords, timings, ids) so "TIMEOUT >25s" and "TIMEOUT >24s" collapse to
// one ticket — classifies each group, and writes:
//   * Builds/autopilot-tickets.md   — human-readable punch list.
//   * Builds/autopilot-tickets.json — machine-readable for the WO pipeline.
// Prints a single authoritative marker (mirrors DataRegression.cs):
//   AUTOPILOT_TICKETS_OK: <n>   /   AUTOPILOT_TICKETS_FAIL
//
// Run headless (Unity closed) via:
//   run-unity-method.ps1 -Method DeNelle.Editor.AutoPilotTickets.Emit -LogName autopilot-tickets.log
//
// Editor-only: uses Debug.Log / Debug.LogError (NOT FlowTrace — that's the
// runtime tag). Lives in DeNelle.AutoPilot.Editor (Editor platform only).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class AutoPilotTickets
    {
        // Mirrors BreakCaptureHarness.BreakRecord (private there) so JsonUtility
        // can deserialize each break-log.jsonl line. Field names MUST match.
        [Serializable]
        private struct BreakRecord
        {
            public string kind;
            public string message;
            public string stack;
            public string scene;
            public float t;
            public string utc;
        }

        // Mirrors AutoPilotDriver.PhaseResult / RunSummary so the summary parses.
        [Serializable]
        private struct PhaseResult
        {
            public string phase;
            public string status;
            public float seconds;
            public string detail;
        }

        [Serializable]
        private struct RunSummary
        {
            public string utc;
            public float totalSeconds;
            public bool aborted;
            public int seed;        // WO-452 tranche E — the run's seed (for replay)
            public string runId;    // WO-452 tranche E — the run id
            public PhaseResult[] phases;
        }

        private sealed class Ticket
        {
            public string Category;      // bug / hang / warning / owner-note
            public string Kind;          // the raw break kind
            public string Sample;        // first raw message seen
            public string NormalizedKey; // dedupe key
            public int Count;            // total raw occurrences across all runs
            public string Scene;
            public string Stack;
            // DISTINCT runs that reproduced this break — the hit-rank signal. A break
            // seen by many runs is high-priority; a 1-of-N fluke is low.
            public readonly HashSet<string> Runs = new HashSet<string>();
        }

        // One scanned source: the root run, or a fleet run dir (autopilot-runs/<id>).
        private struct RunSource
        {
            public string Id;
            public string BreakLog;
            public string SummaryPath;
        }

        // Find-or-create a ticket for a dedupe key, seeding the immutable fields once.
        private static Ticket GetOrAdd(Dictionary<string, Ticket> tickets, string key,
            string category, string kind, string sample, string norm, string scene, string stack)
        {
            if (!tickets.TryGetValue(key, out var t))
            {
                t = new Ticket
                {
                    Category = category,
                    Kind = kind,
                    Sample = sample,
                    NormalizedKey = norm,
                    Count = 0,
                    Scene = scene,
                    Stack = stack,
                };
                tickets[key] = t;
            }
            return t;
        }

        // Minimum DISTINCT runs a break must reproduce in to be "confirmed". Below
        // this, the ticket is DEMOTED (not dropped) into a labeled trailing section —
        // a one-off may be a fluke, but a deterministic 1/N bug can be real. Override
        // with the AUTOPILOT_MIN_RUNS env var; defaults to 2 when unset/invalid.
        private const int DefaultMinRuns = 2;

        private static int ResolveMinRuns()
        {
            string raw = Environment.GetEnvironmentVariable("AUTOPILOT_MIN_RUNS");
            if (!string.IsNullOrEmpty(raw)
                && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                && v >= 1)
            {
                return v;
            }
            return DefaultMinRuns;
        }

        public static void Emit()
        {
            var log = new StringBuilder();
            log.AppendLine("=== AutoPilotTickets: break-log + summary -> triaged tickets ===");

            int minRuns = ResolveMinRuns();
            log.AppendLine($"reproduction threshold: >={minRuns} distinct run(s) to be CONFIRMED " +
                           "(set AUTOPILOT_MIN_RUNS to change)");

            string dir = Application.persistentDataPath;

            // ── enumerate every FLEET run source ─────────────────────────────
            // FLEET-ONLY: each headless instance launched with --run=<id> wrote into
            // persistentDataPath/autopilot-runs/<id>/. We scan ONLY those — the root
            // break-log.jsonl is deliberately DROPPED because it accumulates stale,
            // pre-fix errors across every build of the day and pollutes the ranking.
            // Each fleet dir = one "run" for hit-ranking.
            var runs = new List<RunSource>();
            try
            {
                string runsRoot = Path.Combine(dir, "autopilot-runs");
                if (Directory.Exists(runsRoot))
                {
                    foreach (var runDir in Directory.GetDirectories(runsRoot))
                    {
                        runs.Add(new RunSource
                        {
                            Id = Path.GetFileName(runDir),
                            BreakLog = Path.Combine(runDir, "break-log.jsonl"),
                            SummaryPath = Path.Combine(runDir, "autopilot-summary.json"),
                        });
                    }
                }
            }
            catch (Exception ex) { log.AppendLine("run-dir scan error: " + ex.Message); }

            // No fleet runs at all → emit a clean, empty (OK, 0 tickets) report. We do
            // NOT fall back to the root break-log (that's the stale-error trap).
            if (runs.Count == 0)
            {
                log.AppendLine("no fleet runs found — " +
                    $"'{Path.Combine(dir, "autopilot-runs")}' has zero run folders. " +
                    "Nothing to triage (root break-log.jsonl is intentionally NOT scanned).");
                try { WriteMarkdown(new List<Ticket>(), default, false, 0, 0, 0, 0, minRuns, new Dictionary<string, int>(), new Dictionary<string, string>()); WriteJson(new List<Ticket>(), 0, new Dictionary<string, int>(), new Dictionary<string, string>()); }
                catch (Exception ex) { log.AppendLine("write error: " + ex.Message); }
                log.AppendLine("=== verdict ===");
                log.AppendLine("AUTOPILOT_TICKETS_OK: 0 (0 confirmed, 0 below-threshold)");
                Debug.Log(log.ToString());
                return;
            }

            var tickets = new Dictionary<string, Ticket>();
            // WO-452 tranche E (reproducibility): per-run seed + ordered action trace, keyed by
            // run id, so every ticket can print "replay: --seed=<n> --run=<id>" + the action path
            // that led to the failure. A break already carries its triggering Flow line (Sample).
            var runSeeds = new Dictionary<string, int>();
            var runTraces = new Dictionary<string, string>();
            int parsedLines = 0, badLines = 0, filteredArtifacts = 0;
            int runsWithSummary = 0, runsWithBreakLog = 0;
            // The "representative" summary shown in the report header: the first fleet
            // run that produced one. (Per-run summaries differ.)
            RunSummary summary = default;
            bool haveSummary = false;

            foreach (var run in runs)
            {
                // summary (best-effort, per run)
                RunSummary runSummary = default;
                bool runHasSummary = false;
                try
                {
                    if (File.Exists(run.SummaryPath))
                    {
                        runSummary = JsonUtility.FromJson<RunSummary>(File.ReadAllText(run.SummaryPath));
                        runHasSummary = true;
                        runsWithSummary++;
                        if (!haveSummary) { summary = runSummary; haveSummary = true; }

                        // WO-452 tranche E: record this run's seed + ordered action trace so every
                        // ticket can be replayed (--seed=<n> --run=<id>) with its action path shown.
                        runSeeds[run.Id] = runSummary.seed;
                        if (runSummary.phases != null && runSummary.phases.Length > 0)
                        {
                            var parts = new List<string>(runSummary.phases.Length);
                            foreach (var ph in runSummary.phases)
                                parts.Add(ph.phase + "(" + ph.status + ")");
                            runTraces[run.Id] = string.Join(" > ", parts);
                        }
                    }
                }
                catch (Exception ex) { log.AppendLine($"summary parse error [{run.Id}]: " + ex.Message); }

                // break-log (per run)
                if (File.Exists(run.BreakLog))
                {
                    runsWithBreakLog++;
                    foreach (var line in File.ReadAllLines(run.BreakLog))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        BreakRecord rec;
                        try { rec = JsonUtility.FromJson<BreakRecord>(line); }
                        catch { badLines++; continue; }
                        if (string.IsNullOrEmpty(rec.kind)) { badLines++; continue; }
                        parsedLines++;

                        // -nographics render/video-subsystem noise is NOT a bug — it
                        // only appears because the headless run has no renderer. Drop it.
                        if (IsRenderArtifact(rec.message)) { filteredArtifacts++; continue; }

                        string category = Classify(rec);
                        if (category == null) continue;   // breadcrumb (session_start/scene_loaded)

                        string norm = Normalize(rec.message);
                        string key = rec.kind + "|" + category + "|" + norm;
                        var t = GetOrAdd(tickets, key, category, rec.kind, rec.message ?? "",
                                         norm, rec.scene ?? "?", rec.stack ?? "");
                        t.Count++;
                        t.Runs.Add(run.Id);
                    }
                }

                // phase failures from this run's summary become tickets too
                if (runHasSummary && runSummary.phases != null)
                {
                    foreach (var p in runSummary.phases)
                    {
                        if (p.status == "ok") continue;
                        string category = p.status == "timeout" ? "hang" : "bug";
                        string key = "phase|" + category + "|" + p.phase;
                        var t = GetOrAdd(tickets, key, category, "phase_" + p.status,
                            $"AutoPilot phase '{p.phase}' {p.status} after {p.seconds:0.0}s" +
                            (string.IsNullOrEmpty(p.detail) ? "" : $" ({p.detail})"),
                            p.phase, "?", "");
                        t.Count++;
                        t.Runs.Add(run.Id);
                    }
                }
            }

            int totalRuns = runsWithBreakLog > 0 ? runsWithBreakLog : runs.Count;
            log.AppendLine($"runs scanned: {runs.Count} source(s) " +
                           $"({runsWithBreakLog} with break-log, {runsWithSummary} with summary)");
            log.AppendLine($"break-log: {parsedLines} parsed, {badLines} unparseable, " +
                           $"{filteredArtifacts} render-artifact record(s) filtered (-nographics), " +
                           $"{tickets.Count} unique ticket(s)");

            // ── write outputs ────────────────────────────────────────────────
            // Rank: by DISTINCT runs reproducing (desc) — a break hit by many runs is
            // higher priority — then by severity, then raw count.
            var sorted = new List<Ticket>(tickets.Values);
            sorted.Sort((a, b) =>
            {
                int sev = Severity(a.Category).CompareTo(Severity(b.Category));
                if (sev != 0) return sev;
                int byRuns = b.Runs.Count.CompareTo(a.Runs.Count);
                if (byRuns != 0) return byRuns;
                return b.Count.CompareTo(a.Count);
            });

            try { WriteMarkdown(sorted, summary, haveSummary, totalRuns, runsWithSummary, runs.Count, filteredArtifacts, minRuns, runSeeds, runTraces); WriteJson(sorted, totalRuns, runSeeds, runTraces); }
            catch (Exception ex) { log.AppendLine("write error: " + ex.Message); }

            int bugs = 0, hangs = 0, warns = 0, notes = 0;
            int confirmed = 0, belowThreshold = 0;
            foreach (var t in sorted)
            {
                switch (t.Category)
                {
                    case "bug": bugs++; break;
                    case "hang": hangs++; break;
                    case "warning": warns++; break;
                    case "owner-note": notes++; break;
                }
                if (t.Runs.Count >= minRuns) confirmed++; else belowThreshold++;
            }
            log.AppendLine($"tickets: {bugs} bug(s), {hangs} hang(s), {warns} warning(s), {notes} note(s)");
            log.AppendLine($"reproduction: {confirmed} confirmed (>={minRuns} runs), " +
                           $"{belowThreshold} below-threshold (single/low-repro — may be flukes)");

            // ── verdict ──────────────────────────────────────────────────────
            int total = sorted.Count;
            log.AppendLine("=== verdict ===");
            // OK is "the emitter ran and produced the report" — bugs found are still a
            // successful EMIT (the run did its job). FAIL is reserved for the emitter
            // being unable to produce tickets at all (no inputs). Only CONFIRMED tickets
            // (>=AUTOPILOT_MIN_RUNS distinct runs) count toward the headline number; the
            // below-threshold count is stated explicitly so nothing is silently hidden.
            if (runsWithBreakLog == 0 && !haveSummary)
            {
                log.AppendLine("AUTOPILOT_TICKETS_FAIL");
                Debug.LogError(log.ToString());
            }
            else
            {
                log.AppendLine($"AUTOPILOT_TICKETS_OK: {confirmed} ({confirmed} confirmed, {belowThreshold} below-threshold, {total} total)");
                Debug.Log(log.ToString());
            }
        }

        // Known headless render/video-subsystem noise. These messages only appear
        // because -nographics has no renderer/GfxDevice — they are NOT gameplay or
        // script bugs, so they must never become tickets. Conservative substring
        // match: only clear graphics/video-subsystem strings (case-insensitive).
        private static readonly string[] RenderArtifactNeedles =
        {
            "video decode shader pass",
            "could not find material hidden/video",
            "videodecode",
            "videocomposite",
            "custom render path shader needs to have at least 1 passes",
            "could not find video",
            "d3d11",
            "direct3d",
            "no graphics device",
            "gfxdevice",
            // -nographics MSAA/render-target artifacts (Camera:Render with no real
            // GfxDevice) — ~26k lines/fleet collapsed into false top-ranked tickets
            // until these needles landed (fleet 9000/9200 audit, 2026-07-06).
            "samples but",
            "endrenderpass",
            "not inside a renderpass",
            "rendertexture.create failed",
            "drawopaqueobjects",
            "drawtransparentobjects",
            "attachment 0 was created",
        };

        private static bool IsRenderArtifact(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            string m = message.ToLowerInvariant();
            foreach (var needle in RenderArtifactNeedles)
                if (m.Contains(needle)) return true;
            return false;
        }

        // session_start / scene_loaded are breadcrumbs, not tickets.
        private static string Classify(BreakRecord rec)
        {
            string kind = rec.kind ?? "";
            string msg = rec.message ?? "";

            if (kind == "session_start" || kind == "scene_loaded") return null;
            if (kind == "exception" || kind == "error") return "bug";
            if (kind == "possible_softlock") return "hang";
            if (kind == "flagged") return "owner-note";

            // FlowTrace lines land as warnings/errors via the engine logger; the
            // BreakCaptureHarness only records Error/Exception/Assert + its own kinds,
            // so a [Flow:*] message here arrived as an error -> a bug. A literal
            // "Fail"/"TIMEOUT"/"THREW" in the text is a bug; "Warn"/"fallback" a warning.
            if (msg.Contains("TIMEOUT") || msg.Contains("THREW") || msg.Contains("Fail")) return "bug";
            if (msg.Contains("[Flow:") && (msg.Contains("Warn") || msg.Contains("fallback"))) return "warning";

            // Anything else that got recorded is at least a warning worth a look.
            return "warning";
        }

        private static int Severity(string category)
        {
            switch (category)
            {
                case "bug": return 0;
                case "hang": return 1;
                case "warning": return 2;
                case "owner-note": return 3;
                default: return 4;
            }
        }

        // Strip volatile substrings so near-identical breaks dedupe to one ticket:
        // numbers (coords, timings, percentages), guids, and bracketed scene tags.
        private static readonly Regex RxNumber = new Regex(@"-?\d+(\.\d+)?", RegexOptions.Compiled);
        private static readonly Regex RxWs = new Regex(@"\s+", RegexOptions.Compiled);

        private static string Normalize(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            string s = RxNumber.Replace(message, "#");
            s = RxWs.Replace(s, " ").Trim();
            return s.ToLowerInvariant();
        }

        private static void WriteMarkdown(List<Ticket> tickets, RunSummary summary, bool haveSummary,
                                          int totalRuns, int runsWithSummary, int sourceCount, int filteredArtifacts,
                                          int minRuns,
                                          Dictionary<string, int> runSeeds, Dictionary<string, string> runTraces)
        {
            Directory.CreateDirectory("Builds");
            var sb = new StringBuilder();
            sb.AppendLine("# AutoPilot Tickets");
            sb.AppendLine();
            sb.AppendLine($"_Generated {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}_");
            sb.AppendLine();
            sb.AppendLine($"**Fleet coverage:** {sourceCount} run source(s) scanned, " +
                          $"{totalRuns} with a break-log, {runsWithSummary} produced a summary. " +
                          "Each ticket is ranked by how many DISTINCT runs reproduced it.");
            sb.AppendLine();
            sb.AppendLine($"_Reproduction threshold: >={minRuns} runs to be 'confirmed' (set AUTOPILOT_MIN_RUNS to change). " +
                          "Tickets below the threshold are DEMOTED into a trailing section, not dropped._");
            sb.AppendLine();
            sb.AppendLine($"_{filteredArtifacts} render-artifact records filtered (-nographics)_");
            sb.AppendLine();

            if (haveSummary)
            {
                sb.AppendLine("## Sample run summary (first run found)");
                sb.AppendLine();
                sb.AppendLine($"- Total: {summary.totalSeconds:0.0}s  |  aborted: {summary.aborted}");
                if (summary.phases != null)
                    foreach (var p in summary.phases)
                        sb.AppendLine($"- **{p.phase}** — {p.status} ({p.seconds:0.0}s)" +
                                      (string.IsNullOrEmpty(p.detail) ? "" : $" — {p.detail}"));
                sb.AppendLine();
            }

            // Partition into CONFIRMED (>=minRuns distinct runs) vs BELOW-THRESHOLD,
            // preserving the incoming severity-then-run-count order in each bucket.
            var confirmed = new List<Ticket>();
            var below = new List<Ticket>();
            foreach (var t in tickets)
            {
                if (t.Runs.Count >= minRuns) confirmed.Add(t);
                else below.Add(t);
            }

            sb.AppendLine("## Tickets");
            sb.AppendLine();
            if (tickets.Count == 0)
            {
                sb.AppendLine("_No breaks recorded — clean run._");
            }
            else if (confirmed.Count == 0)
            {
                sb.AppendLine($"_No confirmed tickets (none reproduced in >={minRuns} runs). " +
                              "See the below-threshold section._");
            }
            else
            {
                string lastCat = null;
                foreach (var t in confirmed)
                {
                    if (t.Category != lastCat)
                    {
                        lastCat = t.Category;
                        sb.AppendLine();
                        sb.AppendLine($"### {t.Category.ToUpperInvariant()}");
                        sb.AppendLine();
                    }
                    AppendTicket(sb, t, totalRuns, runSeeds, runTraces);
                }
            }

            if (below.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Below threshold (single/low-repro — verify, may be flukes)");
                sb.AppendLine();
                sb.AppendLine($"_Reproduced in fewer than {minRuns} distinct run(s). Demoted, not dropped — " +
                              "a deterministic 1/N bug can still be real._");
                sb.AppendLine();
                foreach (var t in below)
                    AppendTicket(sb, t, totalRuns, runSeeds, runTraces);
            }

            File.WriteAllText(Path.Combine("Builds", "autopilot-tickets.md"), sb.ToString());
        }

        // Renders one ticket bullet (shared by the confirmed + below-threshold sections).
        // WO-452 tranche E: each ticket carries its seed(s) + run id(s) + the ordered action
        // trace + the triggering Flow line (Sample) so a failure is replayable end-to-end.
        private static void AppendTicket(StringBuilder sb, Ticket t, int totalRuns,
                                         Dictionary<string, int> runSeeds, Dictionary<string, string> runTraces)
        {
            int k = t.Runs.Count;
            sb.AppendLine($"- **[{t.Kind}] x{t.Count}** — reproduced in {k}/{totalRuns} runs (scene: {t.Scene})");
            sb.AppendLine($"  - _flow line:_ {t.Sample}");
            if (!string.IsNullOrEmpty(t.Stack))
            {
                string firstLine = t.Stack.Split('\n')[0];
                sb.AppendLine($"  - _stack:_ `{firstLine}`");
            }

            // Reproducibility: seed(s) + run id(s) + replay hint.
            var seeds = SeedsFor(t, runSeeds);
            string seedStr = seeds.Count > 0 ? string.Join(",", seeds) : "?";
            sb.AppendLine($"  - _repro:_ seed(s)=[{seedStr}] run(s)=[{string.Join(",", t.Runs)}] — replay: `--seed=<n> --run=<id>`");

            // Ordered action trace (the first reproducing run that recorded one).
            string trace = TraceFor(t, runTraces);
            if (!string.IsNullOrEmpty(trace))
                sb.AppendLine($"  - _action trace:_ {trace}");
        }

        // The distinct seeds of the runs that reproduced this ticket (sorted, deduped).
        private static List<int> SeedsFor(Ticket t, Dictionary<string, int> runSeeds)
        {
            var set = new SortedSet<int>();
            foreach (var r in t.Runs)
                if (runSeeds != null && runSeeds.TryGetValue(r, out int sd)) set.Add(sd);
            return new List<int>(set);
        }

        // The ordered action trace of the first reproducing run that recorded one.
        private static string TraceFor(Ticket t, Dictionary<string, string> runTraces)
        {
            if (runTraces == null) return null;
            foreach (var r in t.Runs)
                if (runTraces.TryGetValue(r, out var tr) && !string.IsNullOrEmpty(tr)) return tr;
            return null;
        }

        private static void WriteJson(List<Ticket> tickets, int totalRuns,
                                      Dictionary<string, int> runSeeds, Dictionary<string, string> runTraces)
        {
            Directory.CreateDirectory("Builds");
            // Hand-roll the JSON array (JsonUtility can't serialize a bare List<T>
            // with string escaping reliably across nested quotes).
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < tickets.Count; i++)
            {
                var t = tickets[i];
                if (i > 0) sb.Append(",");
                sb.Append("{");
                sb.Append("\"category\":").Append(JsonStr(t.Category)).Append(",");
                sb.Append("\"kind\":").Append(JsonStr(t.Kind)).Append(",");
                sb.Append("\"count\":").Append(t.Count).Append(",");
                sb.Append("\"reproducedInRuns\":").Append(t.Runs.Count).Append(",");
                sb.Append("\"totalRuns\":").Append(totalRuns).Append(",");
                sb.Append("\"scene\":").Append(JsonStr(t.Scene)).Append(",");
                sb.Append("\"message\":").Append(JsonStr(t.Sample)).Append(",");
                // WO-452 tranche E — reproducibility fields (replay handle + action path).
                sb.Append("\"flowLine\":").Append(JsonStr(t.Sample)).Append(",");
                sb.Append("\"seeds\":").Append(JsonIntArray(SeedsFor(t, runSeeds))).Append(",");
                sb.Append("\"runIds\":").Append(JsonStrArray(t.Runs)).Append(",");
                sb.Append("\"actionTrace\":").Append(JsonStr(TraceFor(t, runTraces) ?? ""));
                sb.Append("}");
            }
            sb.Append("]");
            File.WriteAllText(Path.Combine("Builds", "autopilot-tickets.json"), sb.ToString());
        }

        // Renders a JSON array of ints, e.g. [12345,7].
        private static string JsonIntArray(List<int> values)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(values[i]);
            }
            sb.Append("]");
            return sb.ToString();
        }

        // Renders a JSON array of (escaped) strings.
        private static string JsonStrArray(IEnumerable<string> values)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var v in values)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append(JsonStr(v));
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }
    }
}
