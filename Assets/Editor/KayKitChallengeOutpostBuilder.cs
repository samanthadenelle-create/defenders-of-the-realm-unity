// =============================================================================
// KayKitChallengeOutpostBuilder — large script-built enemy outpost / dungeon yard.
// -----------------------------------------------------------------------------
// 100% editor script (no hand-edited scene). KayKit dungeon tiles dress the
// perimeter; walkable collidable cubes + NavMeshSurface bake provide gameplay.
// Challenging layout: triple ring, multiple chokes, 8 aggro groups, loot crates.
//
//   Menu: Defenders/World/Build KayKit Challenge Outpost
//   Batch: DeNelle.Editor.KayKitChallengeOutpostBuilder.Build
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    public static class KayKitChallengeOutpostBuilder
    {
        private const string Sys = "KayKitOutpost";
        private const string ScenePath = "Assets/Scenes/KayKitChallengeOutpost.unity";
        private const string KayFolder = "Assets/Models/KayKit/dungeon";

        private const float Outer = 56f;
        private const float Mid   = 36f;
        private const float Inner = 18f;

        private static readonly Vector3 Entry = new Vector3(0f, 0f, -Outer * 0.5f + 4f);

        private static Material _floor, _wall, _crate, _accent;
        private static List<string> _kayPaths;
        private static readonly HashSet<string> _warned = new HashSet<string>();

        [MenuItem("Defenders/World/Build KayKit Challenge Outpost")]
        public static void Build()
        {
            FlowTrace.Step(Sys, "=== KAYKIT CHALLENGE OUTPOST BUILD START ===");
            EnsureMats();
            EnsureKayPaths();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("KayKitChallengeOutpostRoot").transform;

            var sun = new GameObject("Sun");
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideTileSize = true;
            surface.tileSize = 1024;

            MakeFloor(root, "Floor_Outer", 0f, 0f, Outer, Outer);
            BuildRingWalls(root, "Ring_Outer", Outer, gapSouth: true, gapNorth: false);
            BuildRingWalls(root, "Ring_Mid", Mid, gapSouth: false, gapNorth: true);
            BuildRingWalls(root, "Ring_Inner", Inner, gapSouth: true, gapNorth: false);

            BuildChoke(root, "Choke_SouthMid", new Vector3(0f, 2f, -6f), 10f, 1f, 4f);
            BuildChoke(root, "Choke_NorthMid", new Vector3(0f, 2f, 10f), 8f, 1f, 4f);
            BuildChoke(root, "Choke_EastInner", new Vector3(7f, 2f, 0f), 1f, 6f, 4f, rotate90: true);

            DressCornerTowers(root, Outer);
            DressTorches(root);

            MakeMarker(root, "Outpost_Entry", Entry);

            PlaceEnemyGroups(root, new[]
            {
                new Vector3(-18f, 0f, -18f),
                new Vector3( 18f, 0f, -18f),
                new Vector3(-18f, 0f,  18f),
                new Vector3( 18f, 0f,  18f),
                new Vector3(  0f, 0f,  -8f),
                new Vector3(  0f, 0f,   8f),
                new Vector3( -8f, 0f,   0f),
                new Vector3(  8f, 0f,   0f),
            });

            PlaceBreakables(root, new[]
            {
                (new Vector3(-20f, 0f, -10f), "crate", "crate-common"),
                (new Vector3( 20f, 0f, -10f), "crate", "crate-common"),
                (new Vector3(-20f, 0f,  10f), "barrel", "barrel-common"),
                (new Vector3( 20f, 0f,  10f), "barrel", "barrel-common"),
                (new Vector3(-12f, 0f, -20f), "crate", "crate-common"),
                (new Vector3( 12f, 0f,  20f), "chest", "chest-rare"),
                (new Vector3( -4f, 0f,  14f), "crate", "crate-common"),
                (new Vector3(  4f, 0f, -14f), "crate", "crate-common"),
            });

            NavMesh.RemoveAllNavMeshData();
            surface.BuildNavMesh();

            bool entryOk = NavMesh.SamplePosition(Entry, out _, 3f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            bool toCenter = NavMesh.CalculatePath(Entry, Vector3.zero, NavMesh.AllAreas, path)
                            && path.status == NavMeshPathStatus.PathComplete;
            if (entryOk && toCenter)
                FlowTrace.Step(Sys, $"NAV_OK entry->center corners={path.corners.Length}");
            else
                FlowTrace.Fail(Sys, $"NAV_FAIL entrySampled={entryOk} centerPath={toCenter}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            FlowTrace.Step(Sys, $"SAVED {ScenePath} (KayKit challenge outpost — {Outer}m yard, 8 groups)");
            FlowTrace.Step(Sys, "=== KAYKIT CHALLENGE OUTPOST BUILD COMPLETE ===");
        }

        private static void BuildRingWalls(Transform root, string prefix, float size, bool gapSouth, bool gapNorth)
        {
            float hw = size * 0.5f;
            const float h = 4.5f, t = 1.2f, y = h * 0.5f, gap = 5f;
            SpanWall(root, prefix + "_N", new Vector3(0f, y,  hw), new Vector3(size, h, t), gapNorth ? gap : 0f, alongX: true);
            SpanWall(root, prefix + "_S", new Vector3(0f, y, -hw), new Vector3(size, h, t), gapSouth ? gap : 0f, alongX: true);
            MakeBox(root, prefix + "_W", new Vector3(-hw, y, 0f), new Vector3(t, h, size), _wall, nav: true);
            MakeBox(root, prefix + "_E", new Vector3( hw, y, 0f), new Vector3(t, h, size), _wall, nav: true);
        }

        private static void SpanWall(Transform root, string name, Vector3 center, Vector3 size, float gap, bool alongX)
        {
            if (gap <= 0f) { MakeBox(root, name, center, size, _wall, nav: true); return; }
            float span = alongX ? size.x : size.z;
            float seg = (span - gap) * 0.5f;
            float off = (span * 0.5f - seg * 0.5f);
            if (alongX)
            {
                MakeBox(root, name + "_L", center + new Vector3(-off, 0f, 0f), new Vector3(seg, size.y, size.z), _wall, nav: true);
                MakeBox(root, name + "_R", center + new Vector3( off, 0f, 0f), new Vector3(seg, size.y, size.z), _wall, nav: true);
            }
            else
            {
                MakeBox(root, name + "_L", center + new Vector3(0f, 0f, -off), new Vector3(size.x, size.y, seg), _wall, nav: true);
                MakeBox(root, name + "_R", center + new Vector3(0f, 0f,  off), new Vector3(size.x, size.y, seg), _wall, nav: true);
            }
        }

        private static void BuildChoke(Transform root, string name, Vector3 center, float w, float d, float h, bool rotate90 = false)
        {
            float gap = rotate90 ? d * 0.45f : w * 0.45f;
            if (rotate90)
            {
                MakeBox(root, name + "_L", center + new Vector3(-w * 0.35f, 0f, 0f), new Vector3(w * 0.3f, h, d), _wall, nav: true);
                MakeBox(root, name + "_R", center + new Vector3( w * 0.35f, 0f, 0f), new Vector3(w * 0.3f, h, d), _wall, nav: true);
            }
            else
            {
                MakeBox(root, name + "_L", center + new Vector3(0f, 0f, -gap), new Vector3(w, h, d * 0.35f), _wall, nav: true);
                MakeBox(root, name + "_R", center + new Vector3(0f, 0f,  gap), new Vector3(w, h, d * 0.35f), _wall, nav: true);
            }
        }

        private static void DressCornerTowers(Transform root, float span)
        {
            float h = span * 0.5f - 2f;
            var corners = new[]
            {
                new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
                new Vector3(-h, 0f,  h), new Vector3(h, 0f,  h),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                var holder = new GameObject($"KayTower_{i}");
                holder.transform.SetParent(root, false);
                holder.transform.position = corners[i];
                KayDress(holder.transform, "tower", Vector3.zero, Quaternion.identity);
                KayDress(holder.transform, "wall", new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 45f, 0f));
            }
        }

        private static void DressTorches(Transform root)
        {
            var spots = new[]
            {
                new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f),
                new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, -14f), new Vector3(0f, 0f, 14f),
            };
            foreach (var p in spots)
            {
                var h = new GameObject("TorchDressing");
                h.transform.SetParent(root, false);
                h.transform.position = p + Vector3.up * 2.2f;
                KayDress(h.transform, "torch", Vector3.zero, Quaternion.identity);
            }
        }

        private static void KayDress(Transform parent, string token, Vector3 localPos, Quaternion rot)
        {
            string path = FindKay(token);
            if (!string.IsNullOrEmpty(path))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                    if (inst == null) inst = UnityEngine.Object.Instantiate(model, parent);
                    inst.transform.localPosition = localPos;
                    inst.transform.localRotation = rot;
                    StripColliders(inst);
                    return;
                }
            }
            if (_warned.Add(token))
                Debug.LogWarning($"[{Sys}] KayKit '{token}' not found under {KayFolder} — cosmetic skipped.");
        }

        private static void PlaceEnemyGroups(Transform root, Vector3[] spots)
        {
            var spType = FindType("DeNelle.Village.OutpostEnemyGroupSpawner");
            for (int i = 0; i < spots.Length; i++)
            {
                var go = MakeMarker(root, $"EnemyGroup_{i}", spots[i]);
                if (spType != null) go.AddComponent(spType);
            }
            FlowTrace.Step(Sys, $"ENEMY_GROUPS {spots.Length} markers placed.");
        }

        private static void PlaceBreakables(Transform root, (Vector3 pos, string token, string table)[] spots)
        {
            var bcType = FindType("DeNelle.Village.BreakableContainer");
            int layer = LayerMask.NameToLayer("Enemy");
            for (int i = 0; i < spots.Length; i++)
            {
                var (pos, token, table) = spots[i];
                var go = MakeBox(root, $"Breakable_{i}", pos + Vector3.up * 0.5f, Vector3.one, _crate, nav: true);
                if (layer >= 0) go.layer = layer;
                if (bcType != null)
                {
                    var comp = go.AddComponent(bcType);
                    bcType.GetField("lootTableId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.SetValue(comp, table);
                }
            }
        }

        private static void MakeFloor(Transform root, string name, float cx, float cz, float w, float d)
        {
            MakeBox(root, name, new Vector3(cx, -0.25f, cz), new Vector3(w, 0.5f, d), _floor, nav: true);
        }

        private static GameObject MakeBox(Transform root, string name, Vector3 center, Vector3 size, Material mat, bool nav)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            if (nav)
            {
                var f = GameObjectUtility.GetStaticEditorFlags(go);
                GameObjectUtility.SetStaticEditorFlags(go, f | StaticEditorFlags.NavigationStatic);
            }
            return go;
        }

        private static GameObject MakeMarker(Transform root, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            return go;
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
        }

        private static string FindKay(string token)
        {
            token = token.ToLowerInvariant();
            foreach (var p in _kayPaths)
            {
                if (Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(token))
                    return p;
            }
            return null;
        }

        private static void EnsureKayPaths()
        {
            if (_kayPaths != null) return;
            _kayPaths = new List<string>();
            if (!AssetDatabase.IsValidFolder(KayFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { KayFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".fbx" || ext == ".gltf") _kayPaths.Add(p);
            }
        }

        private static void EnsureMats()
        {
            if (_floor != null) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _floor  = MakeMat(lit, new Color(0.22f, 0.20f, 0.18f));
            _wall   = MakeMat(lit, new Color(0.12f, 0.11f, 0.10f));
            _crate  = MakeMat(lit, new Color(0.50f, 0.36f, 0.22f));
            _accent = MakeMat(lit, new Color(0.30f, 0.16f, 0.50f));
        }

        private static Material MakeMat(Shader lit, Color c)
        {
            var m = new Material(lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        private static void EnsureBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}