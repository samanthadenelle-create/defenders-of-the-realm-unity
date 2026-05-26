// =============================================================================
// VillageStrayCleanup — removes stray DungeonController objects from the village
// scene. A DungeonController belongs ONLY in a dungeon scene; left in the village
// it re-places the hero (ResolveSpawnPosition) + installs a dungeon camera, which
// fought the village hero/camera. One survived the owner's manual portal cleanup
// on an empty-named GameObject. Run headless:
//   run-unity-method.ps1 -Method DeNelle.Editor.VillageStrayCleanup.RemoveStrayDungeonControllers -LogName cleanup.log
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class VillageStrayCleanup
    {
        private const string VillagePath = "Assets/Scenes/Village.unity";

        [MenuItem("Defenders/Cleanup/Remove Stray DungeonControllers from Village")]
        public static void RemoveStrayDungeonControllers()
        {
            var scene = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

            // Editor asmdef can't reference DeNelle.Dungeons — resolve by reflection.
            Type dcType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                dcType = asm.GetType("DeNelle.Dungeons.DungeonController", false);
                if (dcType != null) break;
            }
            if (dcType == null)
            {
                Debug.LogError("[VillageStrayCleanup] DungeonController type not found — aborting (scene NOT saved).");
                EditorApplication.Exit(2);
                return;
            }

            var found = UnityEngine.Object.FindObjectsByType(dcType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;
            foreach (var o in found)
            {
                var comp = o as Component;
                if (comp == null) continue;
                var go = comp.gameObject;
                Debug.Log($"[VillageStrayCleanup] Removing stray '{go.name}' (DungeonController) " +
                          $"at {go.transform.position}, children={go.transform.childCount}.");
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }

            Debug.Log($"[VillageStrayCleanup] Removed {removed} stray DungeonController object(s).");
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene);
                Debug.Log($"[VillageStrayCleanup] Village.unity saved: {ok}.");
            }
            else
            {
                Debug.Log("[VillageStrayCleanup] No stray DungeonControllers found — scene left untouched.");
            }
        }

        // Replaces the Tree-of-Life's oversized (canopy) collider with a small
        // trunk capsule so the hero can walk the plaza. Enemies hit the Heart by
        // proximity (not a collider), so a slim trunk blocker is all that's needed.
        // Sizes the capsule to ~2 m radius / 8 m height in WORLD space (divided by
        // the object's lossyScale) so it's correct whatever scale the tree has.
        [MenuItem("Defenders/Cleanup/Fix Tree-of-Life Collider")]
        public static void FixTreeOfLifeCollider()
        {
            var scene = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

            Type heartType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                heartType = asm.GetType("DeNelle.Village.HeartController", false);
                if (heartType != null) break;
            }
            if (heartType == null)
            {
                Debug.LogError("[TreeCollider] HeartController type not found — aborting (scene NOT saved).");
                EditorApplication.Exit(2);
                return;
            }
            var heart = UnityEngine.Object.FindAnyObjectByType(heartType) as Component;
            if (heart == null)
            {
                Debug.LogError("[TreeCollider] No HeartController in the village — aborting.");
                EditorApplication.Exit(2);
                return;
            }
            var root = heart.gameObject;

            int removed = 0;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                Debug.Log($"[TreeCollider] Removing {c.GetType().Name} on '{c.gameObject.name}'.");
                UnityEngine.Object.DestroyImmediate(c);
                removed++;
            }

            float s = Mathf.Max(0.01f, root.transform.lossyScale.x);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.direction = 1;                       // Y-axis (upright trunk)
            cap.radius = 2.0f / s;
            cap.height = 8.0f / s;
            cap.center = new Vector3(0f, (8.0f / s) * 0.5f, 0f);  // feet at root origin
            Debug.Log($"[TreeCollider] root='{root.name}' lossyScale={root.transform.lossyScale} — removed " +
                      $"{removed} collider(s); added trunk CapsuleCollider (world ~2 m x 8 m; local r={cap.radius:F2} h={cap.height:F2}).");

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TreeCollider] Village.unity saved: {ok}.");
        }

        // Read-only dump of the hand-built village's portals + buildings +
        // townsfolk so targeted edits (WO-28/30) work against the ACTUAL scene
        // rather than the builder's assumed coords. Does NOT modify or save.
        [MenuItem("Defenders/Cleanup/Diagnose Village (portals + buildings)")]
        public static void DiagnoseVillage()
        {
            EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("[VDiag] ===== PORTALS =====");
            foreach (var t in all)
            {
                if (t == null || !t.name.StartsWith("DungeonPortal")) continue;
                sb.AppendLine($"[VDiag] PORTAL '{t.name}' worldPos={t.position} localScale={t.localScale} lossyScale={t.lossyScale} rotY={t.eulerAngles.y:F0} children={t.childCount}");
                foreach (Transform c in t)
                    sb.AppendLine($"[VDiag]     child '{c.name}' localPos={c.localPosition} localScale={c.localScale}");
                var bc = t.GetComponent<BoxCollider>();
                if (bc != null) sb.AppendLine($"[VDiag]     BoxCollider center={bc.center} size={bc.size} trigger={bc.isTrigger}");
            }

            sb.AppendLine("[VDiag] ===== BUILDINGS (name starts 'Building-') =====");
            foreach (var t in all)
            {
                if (t == null || !t.name.StartsWith("Building-")) continue;   // skip the 'Buildings' container
                var rends = t.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"[VDiag] BUILDING '{t.name}' worldPos={t.position} localScale={t.localScale} activeInHierarchy={t.gameObject.activeInHierarchy} renderers={rends.Length}");
                int i = 0;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    if (i++ >= 4) { sb.AppendLine("[VDiag]     ...(more renderers)"); break; }
                    var mf = r.GetComponent<MeshFilter>();
                    var mesh = mf != null ? mf.sharedMesh : null;
                    string meshInfo = mesh != null ? $"mesh='{mesh.name}' verts={mesh.vertexCount}" : "mesh=NULL(!)";
                    var b = r.bounds;   // world-space render bounds
                    sb.AppendLine($"[VDiag]     rend '{r.gameObject.name}' on={r.enabled} localScale={r.transform.localScale} lossyScale={r.transform.lossyScale} {meshInfo} worldBoundsSize={b.size} worldBoundsCenter={b.center}");
                }
            }

            // Hunt for the owner's VISIBLE portal arch — any sizeable mesh near the
            // two portal trigger positions (the DungeonPortal_* roots carry only a
            // trigger + sign; the arch the owner sees may be a separate object).
            sb.AppendLine("[VDiag] ===== OBJECTS NEAR PORTAL POSITIONS (±18,_,6) =====");
            Vector3[] portalPts = { new Vector3(-18f, 0f, 6f), new Vector3(18f, 0f, 6f) };
            foreach (var t in all)
            {
                if (t == null) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;   // only objects that actually draw
                foreach (var p in portalPts)
                {
                    Vector3 d = t.position - p; d.y = 0f;
                    if (d.magnitude <= 8f)
                    {
                        sb.AppendLine($"[VDiag] NEAR {p}: '{t.name}' pos={t.position} worldBoundsSize={r.bounds.size} parent='{(t.parent != null ? t.parent.name : "<root>")}'");
                        break;
                    }
                }
            }

            sb.AppendLine("[VDiag] ===== TOWNSFOLK =====");
            foreach (var t in all)
            {
                if (t == null || (!t.name.StartsWith("Townsperson") && t.name != "Townsfolk")) continue;
                var rends = t.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"[VDiag] NPC '{t.name}' pos={t.position} renderers={rends.Length} children={t.childCount}");
                foreach (Transform c in t)
                    sb.AppendLine($"[VDiag]     child '{c.name}'");
            }

            Debug.Log(sb.ToString());
            Debug.Log("[VDiag] DONE (read-only; scene NOT modified).");
        }

        // WO-28: the 5 building meshes have valid materials but render tens of
        // metres from their building root — the tripo mesh child is scaled
        // 100-450x and its off-centre pivot, multiplied by that scale, flings the
        // visible mesh away from the collider/prompt. Result: the [F] prompt
        // floats in empty space. Fix: translate each building's visual subtree so
        // its world bounds re-centre on the building root (feet at the root's Y).
        // Targeted, no re-bake; the collider + prompt (the correct gameplay anchor)
        // are not moved.
        [MenuItem("Defenders/Cleanup/Fix Building Mesh Placement")]
        public static void FixBuildingMeshPlacement()
        {
            var scene = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fixedCount = 0;

            foreach (var t in all)
            {
                if (t == null || !t.name.StartsWith("Building-")) continue;

                // Mesh renderers only — never a Sign/Prompt/Bubble label.
                var rends = t.GetComponentsInChildren<Renderer>(true);
                Bounds? combined = null;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    string n = r.gameObject.name;
                    if (n.Contains("Sign") || n.Contains("Prompt") || n.Contains("Bubble") || n.Contains("Text")) continue;
                    if (combined == null) combined = r.bounds;
                    else { var b = combined.Value; b.Encapsulate(r.bounds); combined = b; }
                }
                if (combined == null) { Debug.Log($"[BFix] '{t.name}' has no mesh renderer — skipped."); continue; }

                Bounds bb = combined.Value;
                Vector3 root = t.position;
                Vector3 delta = new Vector3(root.x - bb.center.x, root.y - bb.min.y, root.z - bb.center.z);
                if (delta.magnitude < 0.10f)
                {
                    Debug.Log($"[BFix] '{t.name}' already centred (delta={delta.magnitude:F2}m) — skipped.");
                    continue;
                }

                // Move only the visual children (those holding a non-label renderer);
                // the BoxCollider + interact prompt on the root stay put.
                foreach (Transform child in t)
                {
                    var cr = child.GetComponentInChildren<Renderer>(true);
                    if (cr == null) continue;
                    string cn = cr.gameObject.name;
                    if (cn.Contains("Sign") || cn.Contains("Prompt") || cn.Contains("Bubble") || cn.Contains("Text")) continue;
                    child.position += delta;
                }
                Debug.Log($"[BFix] '{t.name}': moved visual by {delta} (meshCenter {bb.center} -> root {root}; feet to y={root.y:F2}).");
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene);
                Debug.Log($"[BFix] Re-centred {fixedCount} building(s). Village.unity saved: {ok}.");
            }
            else
            {
                Debug.Log("[BFix] No buildings needed re-centring — scene left untouched.");
            }
        }
    }
}
