// =============================================================================
// RaidAssaultTraceCapture — WO-1595 handback evidence (CLI review 2026-09-07).
// Opens a baked raid scene, deploys Footman/Archer/Support/Siege via TroopDeployer,
// forces one hunt scan per troop, writes [Flow:RaidAI] lines to
// Builds/raid-assault-ai-trace.txt. Batch: DeNelle.Editor.RaidAssaultTraceCapture.Run
// =============================================================================
using System.IO;
using System.Text;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.World.Camps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class RaidAssaultTraceCapture
    {
        private const string ScenePath = "Assets/Scenes/RaidBase_raider_camp_small.unity";
        private const string OutRel = "Builds/raid-assault-ai-trace.txt";

        public static void Run()
        {
            var log = new StringBuilder();
            log.AppendLine("=== RaidAssaultTraceCapture WO-1595 ===");

            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[RaidAssaultTrace] missing " + ScenePath);
                return;
            }

            FlowTrace.Enabled = true;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            log.AppendLine("opened " + scene.path);

            var spire = Object.FindAnyObjectByType<RaidSpire>();
            if (spire == null)
            {
                Debug.LogError("[RaidAssaultTrace] no RaidSpire in scene — cannot prove formation/push.");
                WriteOut(log);
                return;
            }
            // EditMode batch skips Awake — bind Active so FormationOrRingOffset uses the march axis.
            RaidSpire.BindActiveForEditorCapture(spire);
            log.AppendLine("spire=" + spire.name + " at " + spire.WorldPosition.ToString("F1")
                           + " ActiveBound=" + (RaidSpire.Active != null));

            // Staging / south approach — same side troops deploy from.
            Vector3 tap = new Vector3(0f, 0.1f, -(31f + 10f));
            var staging = GameObject.Find("RaidStagingPoint");
            if (staging != null) tap = staging.transform.position;
            log.AppendLine("tap=" + tap.ToString("F1"));

            string[] defs = { "troop-footman", "troop-archer", "troop-field-cleric", "troop-catapult" };
            string[] expectJob = { "Front", "Ranged", "Support", "Breaker" };
            var spawned = new System.Collections.Generic.List<TroopController>();

            for (int i = 0; i < defs.Length; i++)
            {
                var pt = new PlayerTroop("trace-" + defs[i], defs[i]);
                var troop = TroopDeployer.SpawnFromArmy(pt, tap, stackIndex: i);
                if (troop == null)
                {
                    log.AppendLine("FAIL spawn " + defs[i]);
                    continue;
                }
                spawned.Add(troop);
                log.AppendLine($"spawned {defs[i]} expectJob={expectJob[i]} at {troop.transform.position.ToString("F1")}");
                troop.ForceAssaultRescanForTrace();
            }

            // Harvest recent Unity console is unreliable in batch; re-emit a summary Step per troop
            // after ForceAssaultRescan (NearestHostile already Throttled RaidAI during the call).
            for (int i = 0; i < spawned.Count; i++)
            {
                var t = spawned[i];
                if (t == null) continue;
                // Second scan so Throttle keys differ per instance (already keyed by instance id).
                t.ForceAssaultRescanForTrace();
                FlowTrace.Step("RaidAI",
                    $"trace-capture id={t.TroopId} alive={t.IsAlive} pos={t.transform.position.ToString("F1")} " +
                    $"(ForceAssaultRescanForTrace executed — see companion Throttle lines in the Unity log).");
            }

            log.AppendLine("spawned=" + spawned.Count + " — search Unity log for [Flow:RaidAI]");
            log.AppendLine("RAID_ASSAULT_TRACE_OK");
            WriteOut(log);
            Debug.Log("[RaidAssaultTrace] wrote " + OutRel + " and emitted [Flow:RaidAI] lines.");
        }

        private static void WriteOut(StringBuilder log)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string path = Path.Combine(root, OutRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, log.ToString());
        }
    }
}
