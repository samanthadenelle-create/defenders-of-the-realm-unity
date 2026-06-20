// =============================================================================
// OuterWorldCavePortalBuilder — places the CLICK-TO-ENTER cave/portal at the far
// end of the (enlarged) OuterWorld so the player walks up to it and taps to enter
// the enemy outpost (Village2). WO-468 task #6.
// -----------------------------------------------------------------------------
// Run from:  Defenders > World > Place OuterWorld Cave Portal
// Batchmode: -executeMethod DeNelle.Editor.OuterWorldCavePortalBuilder.PlaceCavePortal
//
// WHAT IT BUILDS (idempotent, clear-then-build):
//   * A `CavePortal` GameObject at world (0, 0, -470) — the corridor terminus —
//     using a large polyperfect rock formation (Rock_Large) as the cave mouth.
//     Skip-safe: if the prefab is missing (pack not imported), a tinted primitive
//     cube stands in (CLAUDE.md §4) so the build still completes.
//   * A `PortalVFXController` on the cave GameObject (DeNelle.Village). It is
//     SELF-SUFFICIENT — on Start it builds its own glow quad + point light +
//     cheap vortex, so AddComponent is all that's required. Attached BY REFLECTION
//     because the Editor asmdef does NOT reference DeNelle.Village.
//   * A child `CavePortal_Trigger` with a trigger BoxCollider + a
//     `DeNelle.Village.SceneTransitionTrigger` (also reflection — same pattern as
//     EnemyStrongholdBuilder.BuildReturnSeam) that loads Village2 (single load) and
//     warps the hero to the Village2 stronghold spawn. NO auto-teleport: travel is
//     confirm-to-cross (the runtime SceneTransitionTrigger only crosses on a tap).
//
// NO .unity hand-edits — this opens, mutates, saves the scene through the editor
// scene API. Batchmode-safe (no EditorUtility dialogs). Canon: village is Elarion.
//
// Instrumented per CLAUDE.md §12 ([Flow:CavePortal] Step/Warn/Fail + Guard.Try
// around the prefab load / reflection). Loud FlowTrace.Fail if the
// SceneTransitionTrigger type cannot be resolved.
// =============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;     // FlowTrace / Guard — CLAUDE.md §12

namespace DeNelle.Editor
{
    public static class OuterWorldCavePortalBuilder
    {
        private const string ScenePath = "Assets/Scenes/OuterWorld.unity";

        private const string PolyPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        // Big rock formation reads as a cave mouth (docs/polyperfect-asset-catalog.md
        // "Rocks & stones" -> Rock_Large; verified on disk under Nature_M/Stones_M).
        private const string CaveMouthPrefab = PolyPrefabRoot + "Nature_M/Stones_M/Rock_Large.prefab";

        private const string CaveName    = "CavePortal";
        private const string TriggerName = "CavePortal_Trigger";

        // Corridor terminus — the far end of the enlarged OuterWorld.
        private static readonly Vector3 CavePos = new Vector3(0f, 0f, -700f);

        // Village2 stronghold hero spawn. EnemyStrongholdBuilder.Build (~line 177)
        //   var entryPos = new Vector3(0f, 0.1f, -(courtyardHalf + 6f));
        // with the default courtyardHalf (StrongholdLayout default Courtyard.Size = 14):
        //   -(14 + 6) = -20  =>  (0, 0.1, -20). We use that exact resolved value.
        private static readonly Vector3 Village2SpawnPos = new Vector3(0f, 0.1f, -20f);

        [MenuItem("Defenders/World/Place OuterWorld Cave Portal")]
        public static void PlaceCavePortal()
        {
            FlowTrace.Step("CavePortal", $"PlaceCavePortal START — opening '{ScenePath}'");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                FlowTrace.Fail("CavePortal", $"could not open scene '{ScenePath}' — ABORT.");
                return;
            }

            // Parent: an OuterWorld/Exterior root if one exists, else scene root (null parent).
            Transform parent = FindRoot(scene);
            FlowTrace.Step("CavePortal",
                $"parent root = {(parent != null ? parent.name : "<scene root>")}");

            // Idempotent: destroy any prior CavePortal so a re-run leaves exactly one.
            DestroyExisting(parent, scene);

            // ── Cave mouth visual ───────────────────────────────────────────────
            var cave = BuildCaveMouth(parent);
            cave.transform.position = CavePos;
            MarkStatic(cave);

            // ── Portal glow (self-sufficient PortalVFXController, reflection add) ─
            AddPortalVfx(cave);

            // ── Click-to-enter trigger ──────────────────────────────────────────
            BuildTrigger(cave.transform);

            // ── Save ────────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            FlowTrace.Step("CavePortal",
                $"DONE — cave @ {cave.transform.position}, trigger child '{TriggerName}', " +
                $"target Village2 @ {Village2SpawnPos}, scene-saved={saved}.");
        }

        // Prefer an explicit OuterWorld/Exterior root; otherwise place at scene root.
        private static Transform FindRoot(Scene scene)
        {
            string[] candidates = { "OuterWorldRoot", "ExteriorRoot", "OuterWorld", "ExteriorTerrainRoot" };
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var name in candidates)
                {
                    if (go.name == name) return go.transform;
                }
            }
            return null; // scene root
        }

        private static void DestroyExisting(Transform parent, Scene scene)
        {
            // Search both the chosen parent's children and the whole scene (in case a
            // prior run placed it under a different root).
            GameObject existing = null;
            if (parent != null)
            {
                var t = parent.Find(CaveName);
                if (t != null) existing = t.gameObject;
            }
            if (existing == null)
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name == CaveName) { existing = go; break; }
                    var t = go.transform.Find(CaveName);
                    if (t != null) { existing = t.gameObject; break; }
                }
            }
            if (existing != null)
            {
                FlowTrace.Step("CavePortal", "existing CavePortal found — DestroyImmediate (idempotent re-run).");
                Object.DestroyImmediate(existing);
            }
        }

        // Instantiate the polyperfect rock as the cave mouth. Skip-safe: a missing
        // prefab logs a warning and falls back to a tinted primitive cube.
        private static GameObject BuildCaveMouth(Transform parent)
        {
            GameObject prefab = Guard.Try("CavePortal", "LoadCaveMouthPrefab",
                () => AssetDatabase.LoadAssetAtPath<GameObject>(CaveMouthPrefab), null);

            GameObject cave;
            if (prefab != null)
            {
                cave = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                FlowTrace.Step("CavePortal", $"cave mouth = polyperfect '{CaveMouthPrefab}'.");
                // Scale up so the boulder reads as a sizeable cave entrance (a few metres).
                cave.transform.localScale = Vector3.one * 4f;
            }
            else
            {
                Debug.LogWarning("[CavePortal] cave-mouth prefab '" + CaveMouthPrefab +
                    "' not found (polyperfect pack may not be imported) — using a primitive placeholder cube.");
                FlowTrace.Warn("CavePortal", "cave-mouth prefab missing — primitive placeholder cube stand-in.");
                cave = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (parent != null) cave.transform.SetParent(parent, false);
                cave.transform.localScale = new Vector3(6f, 5f, 4f); // sizeable cave-mouth read
                TintMesh(cave, new Color(0.20f, 0.19f, 0.18f));       // dark stone
            }

            cave.name = CaveName;
            return cave;
        }

        // PortalVFXController self-bootstraps its glow/light/vortex on Start, so a
        // bare AddComponent is enough. Reflection because the Editor asmdef cannot
        // reference DeNelle.Village.
        private static void AddPortalVfx(GameObject cave)
        {
            var vfxType = FindType("DeNelle.Village.PortalVFXController");
            if (vfxType == null)
            {
                FlowTrace.Warn("CavePortal",
                    "DeNelle.Village.PortalVFXController not found — cave placed WITHOUT glow. Re-run after compile.");
                return;
            }
            Guard.Try("CavePortal", "AddPortalVFXController", () =>
            {
                if (cave.GetComponent(vfxType) == null) cave.AddComponent(vfxType);
                return true;
            }, false);
            FlowTrace.Step("CavePortal", "PortalVFXController attached (self-builds glow/light/vortex at runtime).");
        }

        // Child trigger: trigger BoxCollider a few metres in front of the cave mouth
        // + a SceneTransitionTrigger (reflection) that loads Village2 single-load and
        // warps the hero to the stronghold spawn. Mirrors EnemyStrongholdBuilder.BuildReturnSeam.
        private static void BuildTrigger(Transform cave)
        {
            var trig = new GameObject(TriggerName);
            // Parent under the cave's PARENT (the UNSCALED root), NOT the x4-scaled cave —
            // parenting under the scaled cave multiplied a (0,1.5,4) local offset to a world
            // (0,6,-454), floating the trigger 6m off the navmesh (fleet 2026-06-19:
            // SEAM-OFF-MESH) and inflating the BoxCollider to 48x24x32m. Seat it at GROUND
            // level on the approach path, ~16m north of the cave mouth (the player walks south
            // in from z=-12, so +Z of the cave is the approach side), in unscaled world space.
            trig.transform.SetParent(cave.parent, false);
            trig.transform.position = new Vector3(CavePos.x, 1f, CavePos.z + 16f);

            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(12f, 6f, 8f);   // unscaled now: a 12x6x8m volume on the path

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null)
            {
                FlowTrace.Fail("CavePortal",
                    "DeNelle.Village.SceneTransitionTrigger NOT FOUND — trigger collider added WITHOUT the " +
                    "behaviour (no click-to-enter). Re-run after compile.");
                return;
            }

            Guard.Try("CavePortal", "WireSceneTransitionTrigger", () =>
            {
                var comp = trig.AddComponent(transType);
                SetField(transType, comp, "targetSceneName", "Village2");
                SetField(transType, comp, "loadAdditive", false);
                SetField(transType, comp, "targetPosition", Village2SpawnPos);
                SetField(transType, comp, "ProximityRadius", 16f);
                // NARRATIVE LABEL (WO-468): SceneTransitionTrigger now carries a `promptOverride`
                // field (added by the orchestrator) that REPLACES the default "Travel to <dest>"
                // text. Set the story line the owner asked for on this enemy-outpost portal.
                SetField(transType, comp, "promptOverride", "Enter the enemy stronghold");
                return true;
            }, false);

            FlowTrace.Step("CavePortal",
                $"click-to-enter wired: SceneTransitionTrigger -> Village2 (single load), " +
                $"target {Village2SpawnPos}, proximity 16m. Trigger world pos = {trig.transform.position}.");
        }

        // ── shared helpers (mirror EnemyStrongholdBuilder) ──────────────────────
        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void TintMesh(GameObject go, Color c)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            mr.sharedMaterial = mat;
        }

        private static void SetField(System.Type type, Object comp, string fieldName, object value)
        {
            var f = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(comp, value);
            else FlowTrace.Warn("CavePortal", "field '" + fieldName + "' not found on " + type.Name + " — skipped.");
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
