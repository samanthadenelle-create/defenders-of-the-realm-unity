// =============================================================================
// WaveAuthoringLiveRegression [wave-authoring] -- are waves.json's authored
// enemies[] batches CONSUMED by the live spawn path, or silently discarded?
// -----------------------------------------------------------------------------
// THE ROT THIS PREVENTS (2026-07-30). WaveManager.StartWave runs the WO-362 SMART
// path FIRST and only falls through to the family-compose / flat-batch paths when
// it spawned nothing. _smartComposition is serialized 1 in both live hubs and both
// carry spawn points, so the smart path ALWAYS succeeds -- every authored batch
// (type / count / spawnPoint / delay / interval) was DISCARDED, every wave, every
// session. Only countdownSeconds, boss and apexBoss ever took effect.
//
// That supersession was DELIBERATE (WO-362, mid-June: "use new composer instead of
// flat spawning"). The DATA is what went wrong: a 20-wave schedule was authored
// 2026-07-11 -- about four weeks AFTER the batches went inert -- against a port
// that no longer runs. 19 waves / 55 batches / 148 enemies of design work, thrown
// away silently. Nothing in code, log, doc or gate said so.
//
// OWNER RULING 2026-07-30: smart composition is the AUTHORITY. The inert batches
// were stripped from waves.json (preserved as design intent in
// docs/design/WAVE_AUTHORING_REFERENCE_2026-07-30.md) and this oracle is the
// permanent guard: it FAILS the gate if live-looking batches are ever re-added
// while smart composition is on.
//
// IT DECIDES FROM THE TWO REAL INPUTS, never from theory:
//   1. LIVE MODE  = the serialized _smartComposition value on every WaveManager in
//      every scene under Assets/Scenes. Component blocks are matched by the
//      WaveManager SCRIPT GUID, so a class/namespace rename cannot make the scan
//      quietly find nothing, and the field name is REFLECTION-VERIFIED against the
//      real type so a field rename FAILS LOUDLY instead of deciding nothing.
//   2. AUTHORED DATA = waves.json read through the runtime's own CanonicalJson
//      path and parsed by the real WaveSchedule type.
//
// PASSES only when the two AGREE:
//   (A) smart composition ON everywhere AND waves.json declares NO batches
//       -> generation is the sole roster authority, nothing is being thrown away.
//       THIS IS TODAY'S STATE.
//   (B) smart composition OFF everywhere AND every declared batch type resolves in
//       enemies.json -> the authored batches ARE the live roster.
// FAILS when smart is ON while batches are declared (the re-add guard), when the
// two modes are mixed across scenes, and when NO WaveManager scene is found at all
// -- an oracle must never go green by finding nothing.
//
// Marker: WAVE_AUTHORING_OK / WAVE_AUTHORING_FAIL.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DeNelle.Core;       // CanonicalJson -- the runtime loader's Resources-first read
using DeNelle.Village;    // WaveSchedule / WaveDataLoader / WaveManager / EnemyCatalog

namespace DeNelle.Editor
{
    public static class WaveAuthoringLiveRegression
    {
        // The toggle that decides the roster authority.
        // Source of truth: WaveManager._smartComposition. Verified by reflection below.
        private const string SmartFieldName = "_smartComposition";

        // The script whose GUID marks a WaveManager component block inside a .unity file.
        private const string WaveManagerScriptPath = "Assets/_Modules/Village/Waves/WaveManager.cs";

        private const string ScenesRoot = "Assets/Scenes";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- WAVE AUTHORING (are waves.json enemies[] batches consumed by the LIVE path?) ---");

            // -- (0) the toggle must still exist on the real type ---------------------
            FieldInfo smartField = typeof(WaveManager).GetField(
                SmartFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (smartField == null)
            {
                reason = $"wave-authoring: WaveManager has no '{SmartFieldName}' field (renamed or removed) -- " +
                         "the scene scan would decide NOTHING; re-point this oracle";
                Debug.LogError(log.ToString() + "WAVE_AUTHORING_FAIL: " + reason);
                return false;
            }

            // -- (1) LIVE MODE: the serialized flag on every WaveManager in the scenes --
            string wmGuid = AssetDatabase.AssetPathToGUID(WaveManagerScriptPath);
            if (string.IsNullOrEmpty(wmGuid))
            {
                reason = $"wave-authoring: could not resolve the WaveManager script GUID from " +
                         $"'{WaveManagerScriptPath}' (file moved?) -- cannot identify its scene blocks";
                Debug.LogError(log.ToString() + "WAVE_AUTHORING_FAIL: " + reason);
                return false;
            }

            string[] scenes;
            try { scenes = Directory.GetFiles(ScenesRoot, "*.unity", SearchOption.AllDirectories); }
            catch (Exception e)
            {
                reason = $"wave-authoring: could not enumerate scenes under '{ScenesRoot}': " +
                         $"{e.GetType().Name}: {e.Message}";
                Debug.LogError(log.ToString() + "WAVE_AUTHORING_FAIL: " + reason);
                return false;
            }

            var smartOn = new List<string>();
            var smartOff = new List<string>();
            int wmBlocks = 0;

            foreach (string scenePath in scenes)
            {
                string rel = scenePath.Replace('\\', '/');
                foreach (int flag in ReadSmartFlags(scenePath, wmGuid))
                {
                    wmBlocks++;
                    if (flag == 1) { smartOn.Add(rel); log.AppendLine($"  {rel}: {SmartFieldName}=1 (GENERATED roster)"); }
                    else if (flag == 0) { smartOff.Add(rel); log.AppendLine($"  {rel}: {SmartFieldName}=0 (AUTHORED batches)"); }
                    else failures.Add($"{rel} carries a WaveManager but does not serialize '{SmartFieldName}' -- " +
                                      "the live roster authority cannot be decided from data (resave the scene)");
                }
            }

            if (wmBlocks == 0)
                failures.Add($"no scene under '{ScenesRoot}' carries a WaveManager -- this oracle would pass by " +
                             "finding NOTHING; re-point it at the live hub scenes");

            // -- (2) AUTHORED DATA: waves.json through the runtime read path ----------
            WaveSchedule schedule = null;
            string wavesJson = CanonicalJson.Read(WaveDataLoader.WavesRelativePath);
            if (string.IsNullOrEmpty(wavesJson))
                failures.Add("waves.json unreadable via CanonicalJson (the runtime loader's first read path)");
            else
            {
                try { schedule = JsonConvert.DeserializeObject<WaveSchedule>(wavesJson); }
                catch (Exception e)
                { failures.Add($"waves.json failed to deserialise into WaveSchedule: {e.GetType().Name}: {e.Message}"); }
            }

            EnemyCatalog catalog = null;
            string enemiesJson = CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            if (!string.IsNullOrEmpty(enemiesJson))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(enemiesJson); }
                catch (Exception e)
                { failures.Add($"enemies.json failed to deserialise into EnemyCatalog: {e.GetType().Name}: {e.Message}"); }
            }

            if (schedule == null || schedule.Waves == null || schedule.Waves.Count == 0)
            {
                failures.Add("waves.json produced 0 waves -- nothing to reconcile (see [waves-schema])");
                reason = "wave-authoring: " + string.Join("; ", failures);
                Debug.LogError(log.ToString() + "WAVE_AUTHORING_FAIL: " + reason);
                return false;
            }

            int wavesWithBatches = 0, batchEntries = 0, authoredEnemies = 0;
            foreach (WaveDef w in schedule.Waves)
            {
                if (w == null) continue;
                int n = (w.Enemies != null) ? w.Enemies.Count : 0;
                if (n <= 0) continue;
                wavesWithBatches++;
                batchEntries += n;
                authoredEnemies += w.TotalEnemyCount;
            }
            log.AppendLine($"  waves.json: {schedule.Waves.Count} wave(s); {wavesWithBatches} declare batches " +
                           $"({batchEntries} entries, {authoredEnemies} enemies)");

            // -- (3) always-live invariant: a declared boss id must resolve -----------
            // WaveManager releases wave.Boss through SpawnBatch, which hard-fails on an
            // unknown id. This path is live in BOTH modes, so guard it unconditionally.
            if (catalog != null)
            {
                foreach (WaveDef w in schedule.Waves)
                {
                    if (w == null || string.IsNullOrEmpty(w.Boss)) continue;
                    if (catalog.Find(w.Boss) == null)
                        failures.Add($"wave {w.WaveId} declares boss '{w.Boss}' which is NOT in enemies.json -- " +
                                     "SpawnBatch would fail-loud and the boss would never appear");
                }
            }
            else failures.Add("enemies.json produced no EnemyCatalog -- boss/batch ids cannot be resolved");

            // -- (4) the reconciliation verdict --------------------------------------
            if (smartOn.Count > 0 && smartOff.Count > 0)
                failures.Add($"MIXED roster authority: {smartOn.Count} scene(s) run {SmartFieldName}=1 and " +
                             $"{smartOff.Count} run =0 -- waves behave differently per scene. Pick one " +
                             $"(on: {string.Join(", ", smartOn)} | off: {string.Join(", ", smartOff)})");

            if (smartOn.Count > 0 && wavesWithBatches > 0)
            {
                failures.Add($"waves.json declares {batchEntries} enemy batch(es) across {wavesWithBatches} wave(s) " +
                             $"({authoredEnemies} enemies) that the LIVE path CANNOT consume: {smartOn.Count} scene(s) " +
                             $"run {SmartFieldName}=1, so WaveManager GENERATES the roster and the authored " +
                             "type/count/spawnPoint/delay/interval are DISCARDED. Only countdownSeconds, boss and " +
                             "apexBoss survive. Owner ruling 2026-07-30: smart composition is the AUTHORITY -- do not " +
                             "re-add batches here. Design intent lives in " +
                             "docs/design/WAVE_AUTHORING_REFERENCE_2026-07-30.md. " +
                             $"(scenes: {string.Join(", ", smartOn)})");
            }
            else if (smartOff.Count > 0 && catalog != null)
            {
                // Authored batches ARE the live roster -- then every type must resolve.
                foreach (WaveDef w in schedule.Waves)
                {
                    if (w == null || w.Enemies == null) continue;
                    foreach (WaveBatch b in w.Enemies)
                    {
                        if (b == null) continue;
                        if (catalog.Find(b.Type) == null)
                            failures.Add($"wave {w.WaveId} batch type '{b.Type}' is NOT in enemies.json -- " +
                                         "SpawnBatch would skip the whole batch (0 spawned)");
                    }
                }
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "WAVE_AUTHORING_OK");
                reason = smartOn.Count > 0
                    ? $"WAVE AUTHORING OK -- {smartOn.Count} scene(s) run {SmartFieldName}=1 (generated roster) and " +
                      "waves.json declares NO enemies[] batches, so no authored roster data is being discarded"
                    : $"WAVE AUTHORING OK -- {smartOff.Count} scene(s) run {SmartFieldName}=0, so waves.json's " +
                      $"{batchEntries} authored batch(es) ARE the live roster and every type resolves";
                return true;
            }

            reason = "wave-authoring: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "WAVE_AUTHORING_FAIL: " + reason);
            return false;
        }

        /// <summary>
        /// Every WaveManager component block in a .unity file, as its serialized
        /// _smartComposition value (1 / 0, or -1 when the field is not serialized at all).
        /// Blocks are identified by the WaveManager SCRIPT GUID so a class or namespace
        /// rename cannot make this scan quietly find nothing.
        /// </summary>
        private static List<int> ReadSmartFlags(string scenePath, string wmGuid)
        {
            var found = new List<int>();
            string[] lines;
            try { lines = File.ReadAllLines(scenePath); }
            catch { return found; }

            string guidToken = "guid: " + wmGuid;
            string fieldToken = SmartFieldName + ":";
            bool inBlock = false;
            int flag = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    if (inBlock) found.Add(flag);
                    inBlock = false;
                    flag = -1;
                    continue;
                }

                if (!inBlock && line.IndexOf(guidToken, StringComparison.Ordinal) >= 0)
                {
                    inBlock = true;
                    continue;
                }

                if (inBlock && line.TrimStart().StartsWith(fieldToken, StringComparison.Ordinal))
                {
                    int colon = line.IndexOf(':');
                    if (colon >= 0 && int.TryParse(line.Substring(colon + 1).Trim(), out int parsed))
                        flag = parsed;
                }
            }

            if (inBlock) found.Add(flag);
            return found;
        }
    }
}
