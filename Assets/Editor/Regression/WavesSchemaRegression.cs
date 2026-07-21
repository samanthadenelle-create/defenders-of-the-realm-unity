// =============================================================================
// WavesSchemaRegression [waves-schema] -- closes audit P1 EW-3.
// -----------------------------------------------------------------------------
// THE GAP (EW-3): waves.json had ONLY a parse check. If someone renames the
// top-level "waves" key (or it goes missing), JsonConvert maps the file to a
// WaveSchedule whose Waves list is EMPTY -- the WaveManager then runs a SILENT
// 0-wave loop and every gate stays GREEN. A renamed key ships a broken game.
//
// This oracle is the hard schema check that catches that:
//   (a) Loads the REAL waves.json via the SAME path the runtime loader uses
//       (CanonicalJson / Resources first, then StreamingAssets), asserts the
//       top-level "waves" key EXISTS and the parsed WaveSchedule has >= 1 wave.
//   (b) PROVES the guard: takes the real json, RENAMES the top-level wave key
//       in-memory, runs it through the SAME parse (JsonConvert -> WaveSchedule),
//       asserts it yields 0 waves AND that the loader's own usability predicate
//       DETECTS it (a renamed key cannot silently pass).
//   (c) waves.json is DUAL-COPY (Resources + StreamingAssets) -- asserts BOTH
//       copies carry the key + >= 1 wave.
//
// The key name is NEVER re-derived here from a guessed literal: every parse goes
// through the real DeNelle.Village.WaveSchedule type, whose [JsonProperty("waves")]
// (WaveData.cs:363) is the single source of truth. The only literal "waves" is the
// JObject rename in the guard proof (b), cited to that same line so it stays in sync.
// The loader-usability predicate mirrors WaveDataLoader.LoadWavesAsync (WaveData.cs:427).
//
// Marker: WAVES_SCHEMA_OK / WAVES_SCHEMA_FAIL. Expected: GREEN on the current real
// waves.json (both copies carry "waves" with 20 authored waves).
//
// Wire (DataRegression.RunAll -- orchestrator's job, NOT edited here):
//   if (!WavesSchemaRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[waves-schema] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core;       // CanonicalJson -- the runtime loader's Resources-first read
using DeNelle.Village;    // WaveSchedule / WaveDataLoader -- the real parse + path constant

namespace DeNelle.Editor
{
    public static class WavesSchemaRegression
    {
        // The exact top-level key the runtime reads. Source of truth: WaveSchedule.Waves
        // carries [JsonProperty("waves")] (Assets/_Modules/Village/Waves/WaveData.cs:363).
        // Used only for the RAW-key existence check + the malformed-variant rename below;
        // every actual parse goes through the WaveSchedule type so the key never drifts.
        private const string WavesKey = "waves";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- WAVES SCHEMA (real waves.json top-level \"waves\" key + >=1 wave; renamed-key guard) ---");

            // -- (a)+(c): the REAL dual-copy files must be healthy (key present, >=1 wave) --
            //
            // Resources copy -- read through the SAME CanonicalJson.Read the runtime loader's
            // ReadTextAsync hits FIRST (WaveData.cs:481), keyed by the loader's own relative-path
            // constant so the test and runtime can never point at different files.
            string resourcesJson = null;
            try { resourcesJson = CanonicalJson.Read(WaveDataLoader.WavesRelativePath); }
            catch (Exception e) { failures.Add($"Resources read of waves.json threw: {e.GetType().Name}: {e.Message}"); }
            CheckCopy("Resources", resourcesJson, failures, log);

            // StreamingAssets copy -- the loader's desktop fallback (WaveData.cs:485), same path constant.
            string streamingPath = Path.Combine(Application.streamingAssetsPath, WaveDataLoader.WavesRelativePath);
            string streamingJson = null;
            try
            {
                if (File.Exists(streamingPath)) streamingJson = File.ReadAllText(streamingPath);
                else failures.Add($"StreamingAssets copy of waves.json missing at '{streamingPath}'");
            }
            catch (Exception e) { failures.Add($"StreamingAssets read of waves.json threw: {e.GetType().Name}: {e.Message}"); }
            if (streamingJson != null) CheckCopy("StreamingAssets", streamingJson, failures, log);

            // -- (b): PROVE the guard -- a renamed top-level key cannot silently pass --
            string baseJson = !string.IsNullOrEmpty(resourcesJson) ? resourcesJson : streamingJson;
            if (string.IsNullOrEmpty(baseJson))
            {
                failures.Add("no readable waves.json to build the malformed-variant guard proof from");
            }
            else
            {
                try
                {
                    JObject jo = JObject.Parse(baseJson);
                    if (jo[WavesKey] == null)
                    {
                        failures.Add($"base waves.json has no top-level '{WavesKey}' token to rename -- " +
                                     "guard proof cannot run (key drift vs WaveSchedule [JsonProperty(\"waves\")], WaveData.cs:363?)");
                    }
                    else
                    {
                        // Rename the top-level wave key -> exactly the break EW-3 warns about.
                        jo["waves_RENAMED_BY_TEST"] = jo[WavesKey];
                        jo.Remove(WavesKey);

                        // Run the RENAMED json through the SAME parse the loader uses (JsonConvert -> WaveSchedule).
                        WaveSchedule broken = ParseSchedule(jo.ToString());
                        int brokenCount = broken?.Waves?.Count ?? -1;

                        if (brokenCount > 0)
                        {
                            failures.Add($"GUARD HOLE: renaming the top-level '{WavesKey}' key STILL parsed " +
                                         $"{brokenCount} waves -- a renamed key would ship silently");
                        }
                        else if (IsUsableSchedule(broken))
                        {
                            failures.Add("GUARD HOLE: the loader-usability predicate PASSED a 0-wave schedule -- " +
                                         "a renamed key would ship silently");
                        }
                        else
                        {
                            log.AppendLine($"  guard OK: renaming top-level '{WavesKey}' -> {(brokenCount < 0 ? "null" : brokenCount.ToString())} " +
                                           "waves AND detected by the loader predicate (not silently usable)");
                        }
                    }
                }
                catch (Exception e)
                {
                    failures.Add($"malformed-variant guard proof threw: {e.GetType().Name}: {e.Message}");
                }
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "WAVES_SCHEMA_OK");
                reason = "WAVES SCHEMA OK -- both waves.json copies carry the top-level \"waves\" key with >=1 wave, " +
                         "and a renamed key is proven to yield 0 waves AND be detected (no silent 0-wave loop)";
                return true;
            }

            reason = "waves-schema: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "WAVES_SCHEMA_FAIL: " + reason);
            return false;
        }

        /// <summary>
        /// Asserts one copy of waves.json: valid JSON, top-level "waves" key present in the
        /// RAW text, and -- deserialised through the real WaveSchedule type -- >= 1 wave.
        /// </summary>
        private static void CheckCopy(string label, string json, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(json))
            {
                failures.Add($"{label} copy of waves.json is empty/unreadable");
                return;
            }

            JObject jo;
            try { jo = JObject.Parse(json); }
            catch (Exception e)
            {
                failures.Add($"{label} copy of waves.json is not valid JSON: {e.Message}");
                return;
            }

            if (jo[WavesKey] == null)
            {
                failures.Add($"{label} copy is MISSING the top-level '{WavesKey}' key (renamed/removed?) -- " +
                             "the runtime would map to a 0-wave loop");
                return;
            }

            WaveSchedule schedule;
            try { schedule = ParseSchedule(json); }
            catch (Exception e)
            {
                failures.Add($"{label} copy failed to deserialise into WaveSchedule: {e.Message}");
                return;
            }

            int count = schedule?.Waves?.Count ?? -1;
            if (!IsUsableSchedule(schedule))
            {
                failures.Add($"{label} copy parsed to {count} waves (need >=1) -- the '{WavesKey}' key resolved empty");
                return;
            }

            log.AppendLine($"  {label} copy OK: top-level '{WavesKey}' present, {count} waves parsed");
        }

        /// <summary>The real parse the runtime loader uses (WaveDataLoader.LoadWavesAsync, WaveData.cs:426).</summary>
        private static WaveSchedule ParseSchedule(string json) =>
            JsonConvert.DeserializeObject<WaveSchedule>(json);

        /// <summary>
        /// Mirrors the loader's own usability predicate (WaveDataLoader.LoadWavesAsync, WaveData.cs:427):
        /// a schedule is usable only when non-null with a non-empty Waves list. A renamed/removed
        /// top-level key trips this (Waves stays at its empty initialiser) -> the guard detects it.
        /// </summary>
        private static bool IsUsableSchedule(WaveSchedule schedule) =>
            schedule != null && schedule.Waves != null && schedule.Waves.Count > 0;
    }
}
