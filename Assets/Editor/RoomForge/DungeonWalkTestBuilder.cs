// =============================================================================
// DungeonWalkTestBuilder — build ONE dungeon and open it, ready to walk.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only).
// MENU: Defenders > Dungeon > Walk Test > ...
//
// WHY THIS EXISTS:
// ComposeAllBatch rebuilds all five dungeons and leaves nothing open — fine for a
// gate, useless for "let me go stand on the thing and see". This builds ONE, opens
// it in the editor, and prints the handful of facts you need BEFORE pressing Play,
// so a walk-test starts from knowledge instead of from wandering.
//
// It exists specifically to chase the open multi-level defect: connectors resolve
// (connectors=N fallbacks=0), the flights and holes are built, the mate gate passes
// — and the bake still reports PathPartial. Three candidates remain and reasoning
// between them is exactly the guessing CLAUDE.md §12 forbids. So this prints WHERE
// the partial path stops, which collapses three candidates to one.
//
// NOT A SUBSTITUTE FOR THE GATE. This opens a scene and leaves the editor dirty on
// purpose. Do not run it in batchmode as a verification step — use
// GraphDungeonComposer.ComposeAllBatch for that.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor.RoomForge
{
    public static class DungeonWalkTestBuilder
    {
        private const string GraphsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs";
        private const string ScenesFolder = "Assets/Scenes/DungeonCompose";
        private const string Sys = "WalkTest";

        // ── Menu entries ──────────────────────────────────────────────────────
        //  descent_probe first and marked SMALLEST: it is 5 rooms with exactly ONE
        //  stair pair, so it isolates the multi-level defect with the least walking.
        //  Chasing a stair bug through bonecrypt's 21 rooms and 4 pairs is a way to
        //  spend twenty minutes proving what five rooms would have shown.

        /// <summary>
        /// THE ONE TO USE for the stair defect. Owner-designed fixture: one flat room between
        /// every stair pair and NOTHING else — no encounters, traps, chests, keys or locks.
        /// Anything that fails to path here is the stairs, because there is nothing else it
        /// could be. Walk it top to bottom; the first descent you cannot make names the broken
        /// variant. Tests all three shapes (Vertical, then Left, then Right) in one run.
        /// </summary>
        [MenuItem("Defenders/Dungeon/Walk Test/★ STAIR RIG (flat-stair-flat, all 3 variants)", priority = 0)]
        public static void WalkStairRig() => BuildAndOpen("dg_stair_rig");

        [MenuItem("Defenders/Dungeon/Walk Test/Descent Probe (5 rooms, 1 stair pair)", priority = 1)]
        public static void WalkDescentProbe() => BuildAndOpen("dg_descent_probe");

        [MenuItem("Defenders/Dungeon/Walk Test/Starter Loop (11 rooms, single floor)", priority = 2)]
        public static void WalkStarterLoop() => BuildAndOpen("dg_starter_loop");

        [MenuItem("Defenders/Dungeon/Walk Test/Sunken Vault (17 rooms)", priority = 3)]
        public static void WalkSunkenVault() => BuildAndOpen("dg_sunken_vault");

        [MenuItem("Defenders/Dungeon/Walk Test/Bonecrypt (21 rooms)", priority = 4)]
        public static void WalkBonecrypt() => BuildAndOpen("dg_bonecrypt");

        [MenuItem("Defenders/Dungeon/Walk Test/Ember Deep (22 rooms)", priority = 5)]
        public static void WalkEmberDeep() => BuildAndOpen("dg_ember_deep");

        /// <summary>
        /// Compose + bake one graph with play population, open the scene, and report
        /// what a walker needs to know. Editor-only; leaves the scene OPEN and dirty.
        /// </summary>
        private static void BuildAndOpen(string graphId)
        {
            string graphPath = $"{GraphsFolder}/{graphId}.json";
            if (!File.Exists(graphPath))
            {
                Debug.LogError($"[{Sys}] no graph at {graphPath}. Available: " +
                               string.Join(", ", ListGraphs()));
                return;
            }

            // Save whatever is open first. This command opens a different scene, and
            // losing unsaved hand-work to a build command is not an acceptable cost.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"[{Sys}] cancelled - the open scene has unsaved changes.");
                return;
            }

            FlowTrace.Step(Sys, $"=== WALK TEST: {graphId} ===");
            GraphDungeonComposer.ComposeAndBake(graphPath, populateForPlay: true);

            string scenePath = $"{ScenesFolder}/{graphId}.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[{Sys}] bake produced no scene at {scenePath} - read the compose log above.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError($"[{Sys}] failed to open {scenePath}"); return; }

            ApplyInspectionLighting(graphId);

            Report(graphId, scenePath);
        }

        /// <summary>
        /// The DIAGNOSTIC rigs are tools, not content — light them for INSPECTION.
        /// </summary>
        /// <remarks>
        /// Owner ask 2026-08-08, walking the stair defect: "can you add lots of lights or make it
        /// bright for the test one so i can see". She is right, and the reason is not comfort — the
        /// WO-919/WO-1004 relight deliberately made dungeons dark (4 m walls, ceilings, ambient
        /// #0a0a10, linear fog 14->42 m) and that is CORRECT for shipped content, but it means a
        /// geometry defect is being hunted in near-black. The 2026-08-07 capture of the composed
        /// ceiling hole was recorded as "unconfirmed since the overview capture is now dark-on-dark".
        /// You cannot eyeball a stair you cannot see.
        ///
        /// SCOPED TO THE RIGS ON PURPOSE. `dg_stair_rig` and `dg_descent_probe` are fixtures that
        /// never ship — the rig is flat-stair-flat with no encounters, traps, chests or locks, built
        /// so that anything failing to path there IS the stairs. Brightening them costs nothing.
        /// Every real dungeon (starter loop, sunken vault, bonecrypt, ember deep) is left EXACTLY as
        /// baked, so this can never leak atmosphere out of shipped content even if the scene is saved.
        ///
        /// Applied AFTER OpenScene and left DIRTY-but-unsaved by default, matching this command's
        /// existing contract. Nothing here is baked by the composer, so a re-compose reverts it.
        /// </remarks>
        private static void ApplyInspectionLighting(string graphId)
        {
            if (graphId != "dg_stair_rig" && graphId != "dg_descent_probe") return;

            // Flat white ambient at full strength + fog OFF. Fog is the bigger readability thief of
            // the two: at fogEnd 42 m the far end of a 5-room probe is already half-swallowed.
            RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight     = new Color(0.85f, 0.85f, 0.88f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog              = false;

            var root = new GameObject("[INSPECTION LIGHTS - rig only, not shipped]");

            // One key light from above-front so steps read as steps: a pure top-down light flattens
            // a staircase into stripes, which is the one thing we need to be able to SEE here.
            var keyGo = new GameObject("Inspection_Key");
            keyGo.transform.SetParent(root.transform);
            keyGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type      = LightType.Directional;
            key.intensity = 1.6f;
            key.color     = Color.white;
            key.shadows   = LightShadows.Soft;   // shadows are what make a tread read as a tread

            // A dim fill from the opposite side kills the black side-faces without washing out the
            // key's shadowing — an unlit underside reads as a HOLE, which is exactly the thing the
            // owner is trying to tell apart from a real hole.
            var fillGo = new GameObject("Inspection_Fill");
            fillGo.transform.SetParent(root.transform);
            fillGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type      = LightType.Directional;
            fill.intensity = 0.5f;
            fill.color     = new Color(0.8f, 0.85f, 1f);
            fill.shadows   = LightShadows.None;

            FlowTrace.Step(Sys, $"INSPECTION LIGHTING applied to '{graphId}' " +
                "(ambient flat 0.85, fog OFF, key+fill directionals). Rig only - shipped dungeons " +
                "keep their authored dark. Not saved unless you save the scene; a re-compose reverts it.");
        }

        /// <summary>
        /// The pre-Play briefing: where to stand, what to walk to, and — when the path is
        /// partial — the LAST REACHABLE POINT, which is the one fact that turns a hunt into
        /// a diagnosis.
        /// </summary>
        private static void Report(string graphId, string scenePath)
        {
            var log = new StringBuilder();
            log.AppendLine($"--- WALK TEST BRIEFING: {graphId} ---");

            // Hero seat + stair ports, found by component/name rather than assumed.
            Transform hero = null, firstPort = null;
            int ports = 0, connectors = 0;
            var floorYs = new SortedSet<int>();

            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.gameObject.name;
                if (hero == null && (n.Contains("HeroSpawn") || n.Contains("Hero_Spawn") || n == "Hero")) hero = t;
                if (n.IndexOf("PortLink", System.StringComparison.OrdinalIgnoreCase) >= 0)
                { ports++; if (firstPort == null) firstPort = t; }
                if (n.IndexOf("StairConnector", System.StringComparison.OrdinalIgnoreCase) >= 0) connectors++;
                if (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    floorYs.Add(Mathf.RoundToInt(t.position.y));
            }

            log.AppendLine($"  stair connectors placed: {connectors}   teleport ports: {ports}");
            log.AppendLine($"  distinct floor heights : {string.Join(", ", floorYs)}");
            log.AppendLine(hero != null
                ? $"  HERO SEAT: {hero.position}  <- Play starts you here"
                : "  HERO SEAT: not found (scene may not be populated for play)");

            // ── The diagnosis: where does the walk actually stop? ──────────────
            if (hero != null && firstPort != null)
            {
                var path = new NavMeshPath();
                bool ok = NavMesh.CalculatePath(hero.position, firstPort.position, NavMesh.AllAreas, path);
                log.AppendLine($"  path hero -> first stair port: {(ok ? path.status.ToString() : "CalculatePath returned false")}" +
                               $" corners={path.corners.Length}");

                if (path.corners.Length > 0)
                {
                    Vector3 last = path.corners[path.corners.Length - 1];
                    float shortBy = Vector3.Distance(last, firstPort.position);
                    log.AppendLine($"  LAST REACHABLE POINT: {last}   still {shortBy:F2}m short of the port at {firstPort.position}");
                    log.AppendLine( "  ^^ WALK TO THAT POINT. Whatever stops you standing there is the defect - " +
                                    "if you can walk past it on foot, the navmesh is wrong, not the geometry.");
                }
            }
            else if (firstPort == null)
            {
                log.AppendLine("  no stair port found - this dungeon is single-floor, or ports were disabled.");
            }

            log.AppendLine($"  scene OPEN and dirty: {scenePath}");
            log.AppendLine( "  Press Play to walk it. This did NOT run the gate - use ComposeAllBatch for that.");

            Debug.Log(log.ToString() + "WALK_TEST_READY");
        }

        private static IEnumerable<string> ListGraphs()
        {
            var names = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(GraphsFolder, "*.json"))
                    names.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { /* reported by the caller */ }
            return names;
        }
    }
}
