// =============================================================================
// DungeonDresser -- seats REAL cosmetic props (torches, barrels, crates, decor)
// into a composed (RoomForge) room so a bare composed dungeon reads as a dressed
// place. Wired into DungeonBaker's bake flow (one call per composed room).
// -----------------------------------------------------------------------------
// REUSES (does NOT reinvent, CLAUDE.md sec.9) the DungeonChainBuilder KayKit
// prop-resolution idiom: GUID-scan Assets/Models/KayKit/dungeon for .gltf/.fbx
// GameObjects, filename-substring match a token -> LoadAssetAtPath ->
// PrefabUtility.InstantiatePrefab -> StripColliders (dressing must NEVER fragment
// or trap the NavMesh). A missing prop logs a warning + falls back to a tinted
// primitive (never throws) -- so a room ALWAYS gains real prop children even if
// the KayKit pack is not imported.
//
// NAV NOTE: every seated prop has its colliders STRIPPED, and DungeonBaker bakes
// its NavMesh with NavMeshCollectGeometry.PhysicsColliders -- so these props do
// NOT block or carve the mesh regardless of bake order. They are placed AGAINST
// WALLS (corners + mid-wall offsets) with doorway/socket clearance so they read
// right and never sit on the walkable spine.
//
// Placement is DETERMINISTIC: seeded by the room index so repeated bakes of the
// same layout produce the same dressing (editor code -- no Date.now/Random ban,
// but a seeded System.Random keeps bakes reproducible).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public static class DungeonDresser
    {
        private const string Sys = "Dungeon";
        private const string KayDungeonFolder = "Assets/Models/KayKit/dungeon";

        private static List<string> _kayPaths;
        private static readonly HashSet<string> _warnedTokens = new HashSet<string>();
        private static Material _fallbackMat;

        // Wall-mounted torch first (best against a wall), then lit/plain torch fallbacks.
        private static readonly string[] TorchTokens = { "torch_mounted", "torch_lit", "torch" };
        // Floor clutter seated against the walls (barrels / crates / boxes / a chest).
        private static readonly string[] FloorTokens =
            { "barrel_large", "barrel_small", "crates_stacked", "box_small", "box_large", "chest" };

        /// <summary>
        /// Seat cosmetic props into one composed room (parented under it, so each counts as a
        /// room child). Torches at the interior corners (against walls) + a few floor props at
        /// mid-wall offsets, all with doorway/socket clearance. Deterministic per
        /// <paramref name="seedIndex"/>. Returns the number of props seated (&gt;0 on success).
        /// </summary>
        public static int DressRoom(GameObject room, int seedIndex)
        {
            if (room == null)
            {
                FlowTrace.Warn(Sys, "DRESS skipped: null room");
                return 0;
            }

            var rng = new System.Random(unchecked(seedIndex * 486187739 + 17));

            // Room floor extent (world footprint on XZ). Default to the 6u kit cell.
            Vector2 fp = new Vector2(6f, 6f);
            var meta = room.GetComponent<RoomPrefabMeta>();
            if (meta != null) fp = meta.FootprintWorld;
            float halfW = Mathf.Max(1.5f, fp.x * 0.5f);
            float halfD = Mathf.Max(1.5f, fp.y * 0.5f);

            // Doorway/socket local positions to keep clear (do not block connections).
            var socketLocal = new List<Vector3>();
            foreach (var s in room.GetComponentsInChildren<RoomSocket>(true))
                if (s != null) socketLocal.Add(room.transform.InverseTransformPoint(s.WorldPosition));

            int count = 0;

            // 1) Torches at the four interior corners (against walls, non-blocking).
            const float tInset = 0.5f;
            Vector3[] corners =
            {
                new Vector3(-(halfW - tInset), 0f,  (halfD - tInset)),
                new Vector3( (halfW - tInset), 0f,  (halfD - tInset)),
                new Vector3(-(halfW - tInset), 0f, -(halfD - tInset)),
                new Vector3( (halfW - tInset), 0f, -(halfD - tInset)),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                if (NearSocket(corners[i], socketLocal, 1.2f)) continue;
                if (SeatProp(room.transform, PickToken(TorchTokens, rng), corners[i],
                             rng.Next(4) * 90f, torch: true, idx: count))
                    count++;
            }

            // 2) Floor props at mid-wall offsets (away from corners + doorways, on the perimeter).
            const float fInset = 0.7f;
            float alongW = halfW * 0.45f;
            float alongD = halfD * 0.45f;
            var anchors = new List<Vector3>
            {
                new Vector3( alongW, 0f,  (halfD - fInset)),   // N wall
                new Vector3(-alongW, 0f,  (halfD - fInset)),
                new Vector3( alongW, 0f, -(halfD - fInset)),   // S wall
                new Vector3(-alongW, 0f, -(halfD - fInset)),
                new Vector3( (halfW - fInset), 0f,  alongD),   // E wall
                new Vector3( (halfW - fInset), 0f, -alongD),
                new Vector3(-(halfW - fInset), 0f,  alongD),   // W wall
                new Vector3(-(halfW - fInset), 0f, -alongD),
            };
            Shuffle(anchors, rng);

            const int floorTarget = 4;
            int seated = 0;
            foreach (var a in anchors)
            {
                if (seated >= floorTarget) break;
                if (NearSocket(a, socketLocal, 1.4f)) continue;
                if (SeatProp(room.transform, PickToken(FloorTokens, rng), a,
                             rng.Next(8) * 45f, torch: false, idx: count))
                {
                    count++;
                    seated++;
                }
            }

            FlowTrace.Step(Sys, $"DRESS room='{room.name}' props={count} (torches+floor, seed={seedIndex})");
            return count;
        }

        // ---- prop seating ----------------------------------------------------

        // Wrap each prop in a "Dressing_*" holder that is a DIRECT child of the room (so it
        // counts as a room child + the oracle can find it). Guard.Try-wrapped so one bad prop
        // logs + is skipped, never throwing out of the bake.
        private static bool SeatProp(Transform room, string token, Vector3 local, float yaw, bool torch, int idx)
        {
            bool ok = Guard.Try(Sys, $"seat prop '{token}'", () =>
            {
                var holder = new GameObject($"Dressing_{token}_{idx}");
                holder.transform.SetParent(room, false);
                holder.transform.localPosition = local + new Vector3(0f, torch ? 2.2f : 0f, 0f);
                holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                ResolveAndInstantiate(token, holder.transform);

                if (torch)
                {
                    // A warm point light regardless of whether the model resolved.
                    var lt = holder.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.color = new Color(1f, 0.62f, 0.28f);
                    lt.intensity = 2.0f;
                    lt.range = 10f;
                }
            });
            return ok;
        }

        // Resolve a KayKit dungeon model by filename-substring token, instantiate + strip
        // colliders (dressing never fragments nav). Falls back to a tinted primitive on miss.
        private static GameObject ResolveAndInstantiate(string token, Transform parent)
        {
            string path = FindKayPath(token);
            if (!string.IsNullOrEmpty(path))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                    if (inst == null) inst = (GameObject)UnityEngine.Object.Instantiate(model, parent);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    StripColliders(inst);
                    return inst;
                }
            }
            if (_warnedTokens.Add(token))
                Debug.LogWarning($"[{Sys}] KayKit dungeon prop containing '{token}' not found under " +
                                 $"{KayDungeonFolder} -- using primitive fallback.");
            var fb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fb.name = $"Fallback_{token}";
            fb.transform.SetParent(parent, false);
            fb.transform.localPosition = Vector3.zero;
            fb.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var mr = fb.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = FallbackMat();
            StripColliders(fb);
            return fb;
        }

        private static string FindKayPath(string token)
        {
            EnsureKayPaths();
            token = token.ToLowerInvariant();
            foreach (var p in _kayPaths)
            {
                string file = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (file.Contains(token)) return p;
            }
            return null;
        }

        private static void EnsureKayPaths()
        {
            if (_kayPaths != null) return;
            _kayPaths = new List<string>();
            if (!AssetDatabase.IsValidFolder(KayDungeonFolder))
            {
                Debug.LogWarning($"[{Sys}] KayKit dungeon folder missing: {KayDungeonFolder} " +
                                 "(dressing will use primitive fallbacks).");
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { KayDungeonFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".gltf" || ext == ".fbx") _kayPaths.Add(p);
            }
        }

        private static void StripColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
        }

        private static Material FallbackMat()
        {
            if (_fallbackMat != null) return _fallbackMat;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _fallbackMat = new Material(lit) { name = "DresserFallback" };
            var c = new Color(0.45f, 0.33f, 0.20f);
            if (_fallbackMat.HasProperty("_BaseColor")) _fallbackMat.SetColor("_BaseColor", c);
            if (_fallbackMat.HasProperty("_Color")) _fallbackMat.SetColor("_Color", c);
            return _fallbackMat;
        }

        // ---- placement helpers ----------------------------------------------

        private static bool NearSocket(Vector3 local, List<Vector3> sockets, float clearance)
        {
            float c2 = clearance * clearance;
            foreach (var s in sockets)
            {
                float dx = s.x - local.x;
                float dz = s.z - local.z;
                if (dx * dx + dz * dz < c2) return true;
            }
            return false;
        }

        private static string PickToken(string[] tokens, System.Random rng)
            => tokens[rng.Next(tokens.Length)];

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
