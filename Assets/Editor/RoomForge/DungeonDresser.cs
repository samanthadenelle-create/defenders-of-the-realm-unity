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
        //
        // WO-921 A2, VERIFIED AT SOURCE 2026-08-07 (not assumed). The WO asks to "prefer
        // torch_mounted over torch_lit if torch_lit carries a huge particle plume". Reading the
        // POSITION accessor min/max out of the KayKit glTFs settles both halves:
        //
        //   torch_mounted  y -0.381 .. 0.682   z  0.000 .. 0.616   <- back plate at z=0, arm
        //                                                             projects FORWARD: a WALL BRACKET
        //   torch_lit      y -0.395 .. 0.731   z -0.275 .. 0.275   <- radially symmetric: a
        //                                                             FLOOR-STANDING torch
        //   torch          y -0.395 .. 0.647   z -0.275 .. 0.275   <- same, and UNLIT (the missing
        //                                                             0.084 of height IS the flame mesh)
        //
        // 1) There is NO PLUME to scale down. These are .gltf/.fbx MESH assets — one node, one
        //    mesh, one "texture" material, zero extensions; neither format can even carry a
        //    ParticleSystem. 100% of the "encased in fire" light was the point light below.
        // 2) The real defect the bounds expose: this array was consumed by a RANDOM PickToken, so
        //    two thirds of every room's torches were FLOOR-STANDING models seated at the WALL
        //    height of +2.2 m — floating in mid-air — and one third of those were the UNLIT stick.
        //    Only torch_mounted is a wall bracket, so torches now resolve IN ORDER (first token
        //    that actually exists), which is what the comment above always claimed.
        private static readonly string[] TorchTokens = { "torch_mounted", "torch_lit", "torch" };

        // ---- WO-921 Phase A cosmetic-torch dial ------------------------------
        // A cosmetic torch light NEVER damages; hazard fire is a separate recipe
        // (ComposedTrapHazard / WO-921 Phase C) and the two must not blur.
        //
        // INTENSITY was 2.0. The WO's band is 0.6-0.9; 0.85 is the top of it because this lands
        // in the same wave as the WO-1004 relight, which drops scene ambient 0.08 -> 0.05 and the
        // directional 0.35 -> 0.18. Dimmer room => the accent can sit at the bright end of the
        // band and still read as an accent.
        private const float TorchIntensity = 0.85f;

        // RANGE was a flat 10 m. The WO says "~4-5 m" — that number was written when
        // RoomForgeCanon.Cell was 6, and Cell is now 10 (WO-922), so it does NOT survive
        // transplanting. What survives is the RATIO it encoded: at Cell=6 a corner torch sat
        // 2.5 u from centre on each axis => 3.54 m corner-to-centre, and 4-5 m of range is
        // 1.13x-1.41x that distance. At Cell=10 the same corner is 6.36 m from centre, so the
        // WO's own intent scales to ~7.2-9.0 m; a literal 4-5 m would leave the middle of every
        // 10 m room — where the hero and the fight are — completely unlit.
        //
        // So the range is DERIVED from the room's real footprint instead of being re-typed, for
        // the same reason RoomForgeCanon exists: a 2x2 boss room is 20 m across and any fixed
        // literal is wrong for one of the two sizes, and wrong again the next time Cell moves.
        private const float TorchRangeFactor = 1.2f;   // x corner-to-centre distance
        private const float TorchRangeMin = 4.5f;
        private const float TorchRangeMax = 12f;       // caps the pool on 2x2+ rooms
        // Floor clutter seated against the walls (barrels / crates / boxes / a chest).
        private static readonly string[] FloorTokens =
            { "barrel_large", "barrel_small", "crates_stacked", "box_small", "box_large", "chest" };

        /// <summary>
        /// Seat cosmetic props into one composed room (parented under it, so each counts as a
        /// room child). Torches at the interior corners (against walls) + a few floor props at
        /// mid-wall offsets, all with doorway/socket clearance. Deterministic per
        /// <paramref name="seedIndex"/>. Returns the number of props seated (&gt;0 on success).
        /// </summary>
        public static int DressRoom(GameObject room, int seedIndex) => DressRoom(room, seedIndex, false);

        /// <summary>
        /// As <see cref="DressRoom(GameObject,int)"/>, plus WO-921 Phase A option A3: when
        /// <paramref name="isEntryRoom"/> the torch MESHES still seat but their point lights do
        /// NOT, so the hero cannot spawn standing inside the glow.
        /// </summary>
        /// <remarks>
        /// The 2-arg overload above is kept as a REAL overload, not an optional parameter.
        /// <c>DungeonDressingRegression</c> (Assets/Editor/Regression/DungeonDressingRegression.cs
        /// L51-52) resolves this entry point with
        /// <c>GetMethod("DressRoom", ..., new[]{ typeof(GameObject), typeof(int) }, null)</c>,
        /// and an exact-types GetMethod does not match a 3-parameter method however many of its
        /// parameters are optional — collapsing these into one signature silently fails that
        /// oracle with "dressing entrypoint missing".
        /// </remarks>
        public static int DressRoom(GameObject room, int seedIndex, bool isEntryRoom)
        {
            if (room == null)
            {
                FlowTrace.Warn(Sys, "DRESS skipped: null room");
                return 0;
            }

            var rng = new System.Random(unchecked(seedIndex * 486187739 + 17));

            // Room floor extent (world footprint on XZ). Default to ONE canon kit cell (WO-922
            // moved this off a hardcoded 6; a meta-less room would otherwise have been dressed
            // to a 6x6 footprint inside a 10x10 shell, stranding every prop 2m off its wall).
            // With meta present this scales with the room automatically - nothing else to do.
            Vector2 fp = new Vector2(RoomForgeCanon.Cell, RoomForgeCanon.Cell);
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
            // Range derived from THIS room's own corner-to-centre distance (see TorchRangeFactor).
            float cornerToCentre = Mathf.Sqrt((halfW - tInset) * (halfW - tInset) +
                                              (halfD - tInset) * (halfD - tInset));
            float torchRange = Mathf.Clamp(cornerToCentre * TorchRangeFactor, TorchRangeMin, TorchRangeMax);
            string torchToken = PickTorchToken();
            int torchLights = 0;

            for (int i = 0; i < corners.Length; i++)
            {
                if (NearSocket(corners[i], socketLocal, 1.2f)) continue;
                // Yaw FACES THE ROOM, replacing rng.Next(4)*90f. torch_mounted's back plate sits
                // at local z=0 and its arm projects to +z (bounds measured on the glTF, see
                // TorchTokens), so a random cardinal yaw pointed the bracket INTO the wall three
                // times out of four. From a corner, "into the room" is the diagonal.
                if (SeatProp(room.transform, torchToken, corners[i], YawTowardCentre(corners[i]),
                             torch: true, idx: count, torchRange: torchRange, torchLight: !isEntryRoom))
                {
                    count++;
                    if (!isEntryRoom) torchLights++;
                }
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
                             rng.Next(8) * 45f, torch: false, idx: count,
                             torchRange: torchRange, torchLight: false))
                {
                    count++;
                    seated++;
                }
            }

            FlowTrace.Step(Sys, $"DRESS room='{room.name}' props={count} (torches+floor, seed={seedIndex}) " +
                                $"torch='{torchToken}' lights={torchLights} " +
                                $"intensity={(torchLights > 0 ? TorchIntensity : 0f):F2} range={torchRange:F1}m" +
                                (isEntryRoom ? " ENTRY: torch lights suppressed (WO-921 A3)" : ""));
            return count;
        }

        /// <summary>
        /// First torch token that actually resolves to a KayKit asset, IN PREFERENCE ORDER — the
        /// wall bracket wins whenever it exists. Replaces the random <see cref="PickToken"/> draw
        /// that put floor-standing torch models at wall height (see <see cref="TorchTokens"/>).
        /// </summary>
        private static string PickTorchToken()
        {
            foreach (var t in TorchTokens)
                if (!string.IsNullOrEmpty(FindKayPath(t))) return t;
            return TorchTokens[0];   // nothing imported -> primitive fallback path, same as before
        }

        /// <summary>Yaw that points a prop's local +Z at the room centre (0,0) from a local seat.</summary>
        private static float YawTowardCentre(Vector3 local)
        {
            Vector3 inward = new Vector3(-local.x, 0f, -local.z);
            if (inward.sqrMagnitude < 0.0001f) return 0f;
            return Quaternion.LookRotation(inward.normalized, Vector3.up).eulerAngles.y;
        }

        // ---- prop seating ----------------------------------------------------

        // Wrap each prop in a "Dressing_*" holder that is a DIRECT child of the room (so it
        // counts as a room child + the oracle can find it). Guard.Try-wrapped so one bad prop
        // logs + is skipped, never throwing out of the bake.
        private static bool SeatProp(Transform room, string token, Vector3 local, float yaw, bool torch,
                                     int idx, float torchRange, bool torchLight)
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
                    SeatCandleAnchor(holder.transform, token);

                    // WO-921 Phase A: a warm point light regardless of whether the model resolved.
                    // COSMETIC ONLY — no collider, no trigger, no damage path of any kind touches
                    // this object. Hazard fire is ComposedTrapHazard and stays a separate recipe.
                    // Suppressed entirely in the entry/spawn room (A3) so the hero never spawns
                    // inside the glow; the mesh above still seats, so the room still reads dressed.
                    if (!torchLight) return;
                    var lt = holder.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.color = new Color(1f, 0.62f, 0.28f);
                    lt.intensity = TorchIntensity;   // was 2.0
                    lt.range = torchRange;           // was a flat 10 (= the whole 10 m cell)
                    // Four shadow-casting point lights per room x N rooms is a per-frame shadow
                    // pass the mobile target cannot pay for, and an accent light casts nothing
                    // worth seeing. DungeonSceneBuilder.LitFixture does the same.
                    lt.shadows = LightShadows.None;
                }
            });
            return ok;
        }

        /// <summary>
        /// WO-1004 §1.3 seat for the <c>Env_Candle</c> wick flame: an empty marker at the torch's
        /// FLAME TIP, in the holder's local space.
        ///
        /// This is the ANCHOR ONLY — it deliberately does not instantiate any VFX. The candle is a
        /// LOOPING, POOLED runtime effect (VFXCatalogGenerator L287: Env_Candle isLoop:true,
        /// poolSize:6) owned by <c>VFXManager</c> in DeNelle.Village, which DeNelle.Editor cannot
        /// reference and which enforces a global loop-slot cap. Baking ~40 loop instances into the
        /// scene would bypass the pool and the cap outright — the exact failure HarvestAura was
        /// written to prevent. So the bake contributes the one thing only the bake knows (WHERE the
        /// flame is) and the runtime consumer is reported as the remaining seam.
        ///
        /// Offsets are the measured glTF POSITION bounds (see <see cref="TorchTokens"/>): the wall
        /// bracket's flame sits at the top of an arm that projects to +z, the floor torches' at the
        /// top of a radially symmetric stick.
        /// </summary>
        private static void SeatCandleAnchor(Transform holder, string token)
        {
            var anchor = new GameObject("CandleAnchor");
            anchor.transform.SetParent(holder, false);
            anchor.transform.localPosition = token == "torch_mounted"
                ? new Vector3(0f, 0.70f, 0.30f)   // torch_mounted: y max 0.682, arm z 0 -> 0.616
                : new Vector3(0f, 0.75f, 0f);     // torch_lit / torch: y max 0.731 / 0.647
            anchor.transform.localRotation = Quaternion.identity;
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
