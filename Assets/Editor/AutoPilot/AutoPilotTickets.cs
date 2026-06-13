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
            public PhaseResult[] phases;
        }

        private sealed class Ticket
        {
            public string Category;      // bug / hang / warning / owner-note
            public string Kind;          // the raw break kind
            public string Sample;        // first raw message seen
            public string NormalizedKey; // dedupe key
            public int Count;
            public string Scene;
            public string Stack;
        }

        public static void Emit()
        {
            var log = new StringBuilder();
            log.AppendLine("=== AutoPilotTickets: break-log + summary -> triaged tickets ===");

            string dir = Application.persistentDataPath;
            string breakLog = Path.Combine(dir, "break-log.jsonl");
            string summaryPath = Path.Combine(dir, "autopilot-summary.json");

            // ── load summary (best-effort) ───────────────────────────────────
            RunSummary summary = default;
            bool haveSummary = false;
            try
            {
                if (File.Exists(summaryPath))
                {
                    summary = JsonUtility.FromJson<RunSummary>(File.ReadAllText(summaryPath));
                    haveSummary = true;
                    log.AppendLine($"summary: {summary.totalSeconds:0.0}s, aborted={summary.aborted}, " +
                                   $"{(summary.phases != null ? summary.phases.Length : 0)} phase(s)");
                }
                else log.AppendLine($"summary: NOT FOUND ({summaryPath})");
            }
            catch (Exception ex) { log.AppendLine("summary parse error: " + ex.Message); }

            // ── load + group breaks ──────────────────────────────────────────
            var tickets = new Dictionary<string, Ticket>();
            int parsedLines = 0, badLines = 0;
            if (File.Exists(breakLog))
            {
                foreach (var line in File.ReadAllLines(breakLog))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    BreakRecord rec;
                    try { rec = JsonUtility.FromJson<BreakRecord>(line); }
                    catch { badLines++; continue; }
                    if (string.IsNullOrEmpty(rec.kind)) { badLines++; continue; }
                    parsedLines++;

                    string category = Classify(rec);
                    if (category == null) continue;   // not ticket-worthy (session_start, scene_loaded)

                    string norm = Normalize(rec.message);
                    string key = rec.kind + "|" + category + "|" + norm;
                    if (!tickets.TryGetValue(key, out var t))
                    {
                        t = new Ticket
                        {
                            Category = category,
                            Kind = rec.kind,
                            Sample = rec.message ?? "",
                            NormalizedKey = norm,
                            Count = 0,
                            Scene = rec.scene ?? "?",
                            Stack = rec.stack ?? "",
                        };
                        tickets[key] = t;
                    }
                    t.Count++;
                }
                log.AppendLine($"break-log: {parsedLines} parsed, {badLines} unparseable, {tickets.Count} unique ticket(s)");
            }
            else log.AppendLine($"break-log: NOT FOUND ({breakLog})");

            // ── phase failures from the summary become tickets too ───────────
            if (haveSummary && summary.phases != null)
            {
                foreach (var p in summary.phases)
                {
                    if (p.status == "ok") continue;
                    string category = p.status == "timeout" ? "hang" : "bug";
                    string key = "phase|" + category + "|" + p.phase;
                    if (!tickets.TryGetValue(key, out var t))
                    {
                        t = new Ticket
                        {
                            Category = category,
                            Kind = "phase_" + p.status,
                            Sample = $"AutoPilot phase '{p.phase}' {p.status} after {p.seconds:0.0}s" +
                                     (string.IsNullOrEmpty(p.detail) ? "" : $" ({p.detail})"),
                            NormalizedKey = p.phase,
                            Count = 0,
                            Scene = "?",
                            Stack = "",
                        };
                        tickets[key] = t;
                    }
                    t.Count++;
                }
            }

            // ── write outputs ────────────────────────────────────────────────
            var sorted = new List<Ticket>(tickets.Values);
            sorted.Sort((a, b) => Severity(a.Category).CompareTo(Severity(b.Category)));

            try { WriteMarkdown(sorted, summary, haveSummary); WriteJson(sorted); }
            catch (Exception ex) { log.AppendLine("write error: " + ex.Message); }

            int bugs = 0, hangs = 0, warns = 0, notes = 0;
            foreach (var t in sorted)
            {
                switch (t.Category)
                {
                    case "bug": bugs++; break;
                    case "hang": hangs++; break;
                    case "warning": warns++; break;
                    case "owner-note": notes++; break;
                }
            }
            log.AppendLine($"tickets: {bugs} bug(s), {hangs} hang(s), {warns} warning(s), {notes} note(s)");

            // ── verdict ──────────────────────────────────────────────────────
            int total = sorted.Count;
            log.AppendLine("=== verdict ===");
            // OK is "the emitter ran and produced the report" — bugs found are still a
            // successful EMIT (the run did its job). FAIL is reserved for the emitter
            // being unable to produce tickets at all (no inputs).
            if (!File.Exists(breakLog) && !haveSummary)
            {
                log.AppendLine("AUTOPILOT_TICKETS_FAIL");
                Debug.LogError(log.ToString());
            }
            else
            {
                log.AppendLine($"AUTOPILOT_TICKETS_OK: {total}");
                Debug.Log(log.ToString());
            }
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

        private static void WriteMarkdown(List<Ticket> tickets, RunSummary summary, bool haveSummary)
        {
            Directory.CreateDirectory("Builds");
            var sb = new StringBuilder();
            sb.AppendLine("# AutoPilot Tickets");
            sb.AppendLine();
            sb.AppendLine($"_Generated {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}_");
            sb.AppendLine();

            if (haveSummary)
            {
                sb.AppendLine("## Run summary");
                sb.AppendLine();
                sb.AppendLine($"- Total: {summary.totalSeconds:0.0}s  |  aborted: {summary.aborted}");
                if (summary.phases != null)
                    foreach (var p in summary.phases)
                        sb.AppendLine($"- **{p.phase}** — {p.status} ({p.seconds:0.0}s)" +
                                      (string.IsNullOrEmpty(p.detail) ? "" : $" — {p.detail}"));
                sb.AppendLine();
            }

            sb.AppendLine("## Tickets");
            sb.AppendLine();
            if (tickets.Count == 0)
            {
                sb.AppendLine("_No breaks recorded — clean run._");
            }
            else
            {
                string lastCat = null;
                foreach (var t in tickets)
                {
                    if (t.Category != lastCat)
                    {
                        lastCat = t.Category;
                        sb.AppendLine();
                        sb.AppendLine($"### {t.Category.ToUpperInvariant()}");
                        sb.AppendLine();
                    }
                    sb.AppendLine($"- **[{t.Kind}] x{t.Count}** (scene: {t.Scene})");
                    sb.AppendLine($"  - {t.Sample}");
                    if (!string.IsNullOrEmpty(t.Stack))
                    {
                        string firstLine = t.Stack.Split('\n')[0];
                        sb.AppendLine($"  - _stack:_ `{firstLine}`");
                    }
                }
            }

            File.WriteAllText(Path.Combine("Builds", "autopilot-tickets.md"), sb.ToString());
        }

        private static void WriteJson(List<Ticket> tickets)
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
                sb.Append("\"scene\":").Append(JsonStr(t.Scene)).Append(",");
                sb.Append("\"message\":").Append(JsonStr(t.Sample));
                sb.Append("}");
            }
            sb.Append("]");
            File.WriteAllText(Path.Combine("Builds", "autopilot-tickets.json"), sb.ToString());
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
