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

        // OUTPOST PORTAL ENTRANCE (owner 2026-06-30): the outpost is entered through the
        // enemy_outpost structure (NOT a cave rock). Lives under Resources/Dungeons; loaded by the
        // editor path at bake. Alignment comes from the Offset Forge offset id "enemy_outpost", read
        // DATA-DRIVEN from offsets.json — the first real-world placement test of the Offset Forge tool.
        private const string OutpostModelPath = "Assets/Resources/Dungeons/enemy_outpost.fbx";
        private const string OutpostOffsetId  = "enemy_outpost";

        private const string CaveName    = "CavePortal";
        private const string TriggerName = "CavePortal_Trigger";

        // Outpost portal placement. OWNER 2026-06-30: position is arbitrary, but it should sit FAR
        // from town so the player must walk + EXPLORE OuterWorld to find it (not quick-access at the
        // seam). The 1000m terrain spans z ~[-500,+500] (TerrainCenterZ=0); the south seam lands the
        // player ~z-66, so z-420 is a ~350m southward walk to a deep-south entrance.
        private static readonly Vector3 CavePos = new Vector3(0f, 0f, -420f);

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
                $"target Outpost1 @ (0,0,-12) [Outpost1_Entry], scene-saved={saved}.");
        }

        // Parent the portal under a root that SURVIVES a terrain rebuild. CRITICAL (2026-06-20):
        // ExteriorTerrainBuilder.BuildExterior NUKES its "ExteriorRoot" — if the cave is parented
        // there, a later BuildExterior DESTROYS it (owner: "the portal didn't spawn, we were at the
        // end"). So we (a) NEVER use ExteriorRoot/ExteriorTerrainRoot, and (b) loop candidates OUTER
        // so OuterWorldRoot (which BuildExterior preserves) wins regardless of scene root order.
        private static Transform FindRoot(Scene scene)
        {
            string[] candidates = { "OuterWorldRoot", "OuterWorld" };   // BuildExterior-safe roots only
            foreach (var name in candidates)
                foreach (var go in scene.GetRootGameObjects())
                    if (go != null && go.name == name) return go.transform;
            return null; // scene root (also survives BuildExterior; only ExteriorRoot is nuked)
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

            // ALSO sweep every stale CavePortal_Trigger: since the trigger is now parented to
            // the root (not the cave) so its world pos is unscaled, destroying the cave above no
            // longer takes the trigger with it. A prior run's trigger would otherwise linger on
            // the path (e.g. the -454 duplicate the fleet caught) and warp the player early. Sweep
            // the whole scene by name and destroy all of them so a re-run leaves exactly one.
            int sweptTriggers = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == null) continue;
                if (go.name == TriggerName) { Object.DestroyImmediate(go); sweptTriggers++; continue; }
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.gameObject != null && t.gameObject.name == TriggerName)
                    { Object.DestroyImmediate(t.gameObject); sweptTriggers++; }
                }
            }
            if (sweptTriggers > 0)
                FlowTrace.Step("CavePortal", $"swept {sweptTriggers} stale '{TriggerName}' object(s) (idempotent — no orphan triggers on the path).");
        }

        // Instantiate the enemy_outpost structure as the outpost-portal entrance, aligned by the
        // Offset Forge offset (data-driven). Skip-safe: a missing model logs a warning and falls back
        // to a tinted primitive cube. The GameObject name stays "CavePortal" so the trigger/warp
        // wiring + SeamTrace are unchanged — only the visual model is swapped.
        private static GameObject BuildCaveMouth(Transform parent)
        {
            GameObject prefab = Guard.Try("CavePortal", "LoadOutpostModel",
                () => AssetDatabase.LoadAssetAtPath<GameObject>(OutpostModelPath), null);

            GameObject cave;
            if (prefab != null)
            {
                cave = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                ApplyForgeOffset(cave.transform, OutpostOffsetId);
                FlowTrace.Step("CavePortal", $"outpost entrance = '{OutpostModelPath}' (OffsetForge id '{OutpostOffsetId}').");
            }
            else
            {
                Debug.LogWarning("[CavePortal] outpost model '" + OutpostModelPath +
                    "' not found — using a primitive placeholder cube.");
                FlowTrace.Warn("CavePortal", "outpost model missing — primitive placeholder cube stand-in.");
                cave = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (parent != null) cave.transform.SetParent(parent, false);
                cave.transform.localScale = new Vector3(6f, 5f, 4f);
                TintMesh(cave, new Color(0.20f, 0.19f, 0.18f));
            }

            cave.name = CaveName;
            return cave;
        }

        // Apply the Offset Forge offset authored in the tool (euler degrees). DATA-DRIVEN (CLAUDE.md
        // §12) — same convention as MineNodeVisual: the model's LOCAL rotation = Quaternion.Euler(rot),
        // scale = the authored uniform scale. Position is owned by CavePos (the offset's pos is the
        // in-tool nudge; for a world structure the placement coord wins, so pos is not applied here).
        private static void ApplyForgeOffset(Transform t, string id)
        {
            var e = LoadForgeOffset(id);
            if (e == null)
            {
                FlowTrace.Warn("CavePortal", $"OffsetForge id '{id}' not in offsets.json — model left at identity (verify alignment).");
                return;
            }
            t.localRotation = Quaternion.Euler(e.rot.x, e.rot.y, e.rot.z);
            if (e.scale > 0f) t.localScale = Vector3.one * e.scale;
            FlowTrace.Step("CavePortal", $"OffsetForge '{id}': rot=({e.rot.x},{e.rot.y},{e.rot.z}) scale={e.scale} (applied).");
        }

        // Local JsonUtility mirror of OffsetForge.OffsetTable — the DeNelle.Editor asmdef does not
        // reference OffsetForge.Runtime, and the schema is tiny + stable. Reads the authoring file the
        // Offset Forge tool writes (Assets/OffsetForge/offsets.json).
        [System.Serializable] private struct ForgeV3 { public float x, y, z; }
        [System.Serializable] private class ForgeOffset { public string id; public ForgeV3 rot; public ForgeV3 pos; public float scale; }
        [System.Serializable] private class ForgeTable { public ForgeOffset[] offsets; }

        private static ForgeOffset LoadForgeOffset(string id)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "OffsetForge/offsets.json");
                if (!System.IO.File.Exists(path)) return null;
                var table = JsonUtility.FromJson<ForgeTable>(System.IO.File.ReadAllText(path));
                if (table == null || table.offsets == null) return null;
                foreach (var e in table.offsets)
                    if (e != null && e.id == id) return e;
            }
            catch (System.Exception ex)
            {
                FlowTrace.Warn("CavePortal", $"OffsetForge read failed: {ex.Message}");
            }
            return null;
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
                // CONNECT THE CHAIN (owner 2026-06-30): the outpost entrance now routes into the
                // dungeon chain (OuterWorld -> Outpost1 -> Dungeon -> Outpost2), NOT the retired
                // Village2 raid target (the stale seam both RCA agents flagged). Single-load Outpost1
                // and seat the hero at its Outpost1_Entry marker — DungeonChainBuilder.EntryPos =
                // (0,0,-12); scene-links.json link 'outerworld_to_outpost1' targets the same.
                SetField(transType, comp, "targetSceneName", "Outpost1");
                SetField(transType, comp, "loadAdditive", false);
                SetField(transType, comp, "targetPosition", new Vector3(0f, 0f, -12f));
                SetField(transType, comp, "ProximityRadius", 16f);
                // NARRATIVE LABEL (WO-468): SceneTransitionTrigger now carries a `promptOverride`
                // field (added by the orchestrator) that REPLACES the default "Travel to <dest>"
                // text. Set the story line the owner asked for on this enemy-outpost portal.
                SetField(transType, comp, "promptOverride", "Enter the enemy stronghold");
                return true;
            }, false);

            FlowTrace.Step("CavePortal",
                $"click-to-enter wired: SceneTransitionTrigger -> Outpost1 (single load), " +
                $"target (0,0,-12) [Outpost1_Entry], proximity 16m. Trigger world pos = {trig.transform.position}.");
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
