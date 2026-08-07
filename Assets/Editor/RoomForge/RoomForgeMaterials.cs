// =============================================================================
// RoomForgeMaterials — ONE shared wall mat + ONE shared floor mat for all rooms.
// -----------------------------------------------------------------------------
// SOLID STONE URP/Lit — deliberately NO texture. The procedural room shells are
// Unity PRIMITIVE CUBES; a cube maps the full 0→1 UV across every face, so sampling
// the KayKit colormap atlas (dungeon_texture.png = grid of solid-color swatches)
// repeats the WHOLE palette across each face = a rainbow patchwork. Flat stone
// _BaseColor sidesteps that. Props from KayKit keep their own pack materials (they
// are real FBX with authored UVs — Fix KayKit Materials — do NOT touch those).
//
// WO-1004 — WHY THE STRIP HAD TO BECOME STRUCTURAL (the rainbow-floor defect).
// The old strip cleared exactly three slots (_BaseMap, _MainTex, mainTexture) and
// nothing else. That is a NAMED-SLOT strip, and a named-slot strip cannot prove
// the absence of a texture. Two things survived it, both readable on disk:
//   * the TILING. Every one of the three shipped mats still carried the atlas-era
//     scale — RoomFloor_KayKit.mat `_BaseMap m_Scale {2.5, 2.5}`, RoomWall_ and
//     RoomAccent_ `{1.5, 1.5}`. A 2.5x-tiled swatch grid on a cube face is not a
//     patchwork, it is a REPEATING BAND — i.e. the "multicoloured stripes on the
//     floor" symptom exactly, and the floor's 2.5 (against the walls' 1.5) is why
//     the FLOOR was the surface that read as stripes. Clearing the pointer while
//     leaving the multiplier leaves the machine armed for the next re-assignment.
//   * the OTHER ~10 texture slots URP/Lit exposes (_DetailAlbedoMap, _EmissionMap,
//     _SpecGlossMap, …). Any of them holding the atlas rainbows the same surface,
//     and none of them were looked at.
// The strip is now EXHAUSTIVE BY CONSTRUCTION: it walks the shader's own property
// table, nulls every texture property, resets every scale/offset, and drops the
// sampler keywords — so a slot nobody thought of cannot be the next hole.
//
// The same atlas is also the whole of the "stray purple/green squares" symptom:
// dungeon_texture.png carries a purple swatch (row 1 col 3, #662C8E) and green
// swatches (row 1 col 6 / row 2 col 1). A cube face mapping 0→1 over that grid
// shows those cells as small solid squares — a big face reads "rainbow stripes",
// a small face reads "a purple square". One defect, two apparent symptoms.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor.RoomForge
{
    public static class RoomForgeMaterials
    {
        private const string OutFolder = "Assets/Dungeon/Materials";
        private const string WallMatPath = OutFolder + "/RoomWall_KayKit.mat";
        private const string FloorMatPath = OutFolder + "/RoomFloor_KayKit.mat";
        private const string AccentMatPath = OutFolder + "/RoomAccent_KayKit.mat";
        private const string UrpLit = "Universal Render Pipeline/Lit";
        private const string Sys = "RoomForge";

        // Sampler keywords. A stale keyword can re-arm a sampler on a material whose
        // texture slot we just nulled, so they come off with the textures.
        private static readonly string[] SamplerKeywords =
        {
            "_EMISSION", "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_SPECGLOSSMAP",
            "_OCCLUSIONMAP", "_PARALLAXMAP", "_DETAIL_MULX2", "_DETAIL_SCALED",
        };

        // Preferred atlas paths (first hit wins).
        private static readonly string[] AtlasCandidates =
        {
            "Assets/Models/KayKit/dungeon/dungeon_texture.png",
            "Assets/Models/KayKit/dungeon/fbx(unity)/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/textures/dungeon_texture.png",
            "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/dungeon_texture.png",
        };

        // Solid stone tones (no texture — see header for why atlas-on-cube = rainbow).
        private static readonly Color WallStone = new Color(0.42f, 0.42f, 0.45f, 1f);
        private static readonly Color FloorStone = new Color(0.30f, 0.30f, 0.33f, 1f);
        private static readonly Color AccentStone = new Color(0.46f, 0.41f, 0.35f, 1f); // warm stone

        /// <summary>Shared wall material — solid mid-grey stone.</summary>
        public static Material Wall => GetOrCreate(WallMatPath, WallStone);

        /// <summary>Shared floor material — solid darker stone.</summary>
        public static Material Floor => GetOrCreate(FloorMatPath, FloorStone);

        /// <summary>Optional accent (boss/reward) — solid warm stone.</summary>
        public static Material Accent => GetOrCreate(AccentMatPath, AccentStone);

        [MenuItem("Defenders/Dungeon/Ensure Room Forge Materials (KayKit atlas)")]
        public static void EnsureMenu()
        {
            var w = Wall;
            var f = Floor;
            var a = Accent;
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomForgeMaterials] wall={(w != null ? WallMatPath : "NULL")} " +
                      $"floor={(f != null ? FloorMatPath : "NULL")} accent={(a != null ? AccentMatPath : "NULL")} " +
                      $"atlas={FindAtlasPath() ?? "MISSING"}");
        }

        /// <summary>
        /// Force the shared stone mats onto EVERY mesh surface under <paramref name="root"/>.
        ///
        /// WO-1004 — two structural widenings over the original by-name pass:
        ///  * EVERY MATERIAL SLOT, not just slot 0. The old pass wrote <c>sharedMaterial</c>,
        ///    which is slot 0 only; a renderer with more than one submesh kept whatever its
        ///    remaining slots were born with, and an atlas material sitting in slot 1 is
        ///    invisible to every by-name audit.
        ///  * SkinnedMeshRenderer as well as MeshRenderer, so a rigged shell piece cannot
        ///    slip past by not being the one renderer type we happened to enumerate.
        /// The name heuristic below now only picks WHICH stone (floor vs wall) — it is a
        /// choice, no longer a guard. Anything the names do not recognise still gets stone,
        /// and <see cref="VerifyRoomSurfaces"/> proves it afterwards.
        /// </summary>
        public static void ApplyToRoomRoot(GameObject root, bool useAccentFloor = false)
        {
            if (root == null) return;
            var wall = Wall;
            var floor = useAccentFloor ? Accent : Floor;
            if (wall == null && floor == null) return;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsShellRenderer(r)) continue;
                var chosen = PickStone(r.gameObject.name, wall, floor);
                if (chosen == null) continue;
                AssignAllSlots(r, chosen);
            }
        }

        /// <summary>
        /// Prove — do not assume — that no surface under <paramref name="root"/> can render a
        /// texture. Walks every shell renderer and every material slot; a slot is an OFFENDER
        /// when it is null, is not one of the three shared stone mats, or resolves to a
        /// material that still carries ANY texture (the atlas is only the one we caught, but
        /// every room shell is an un-UV'd primitive, so ANY texture on one is wrong by
        /// construction). Offenders are named in the [Flow:RoomForge] band rather than silently
        /// passed, and — when <paramref name="autoFix"/> — re-slotted to stone. Pass
        /// autoFix:false to audit an on-disk prefab ASSET, which must be reported on, never
        /// silently mutated. Returns the offender count (0 = clean).
        /// </summary>
        public static int VerifyRoomSurfaces(GameObject root, string label,
                                             bool useAccentFloor = false, bool autoFix = true)
        {
            if (root == null) return 0;
            // Resolve the three shared mats ONCE. Each property getter re-runs GetOrCreate
            // (asset load + the full shader-property strip), which is fine once and wasteful
            // per material slot.
            var wall = Wall;
            var accent = Accent;
            var plainFloor = Floor;
            var floor = useAccentFloor ? accent : plainFloor;
            int offenders = 0;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsShellRenderer(r)) continue;
                var chosen = PickStone(r.gameObject.name, wall, floor);
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    offenders++;
                    FlowTrace.Warn(Sys, $"surface audit '{label}': piece='{r.gameObject.name}' had NO material slot" +
                                        $"{(autoFix ? " - forced stone" : "")}");
                    if (autoFix && chosen != null) AssignAllSlots(r, chosen);
                    continue;
                }

                for (int i = 0; i < mats.Length; i++)
                {
                    string why = SlotOffence(mats[i], wall, plainFloor, accent);
                    if (why == null) continue;
                    offenders++;
                    FlowTrace.Warn(Sys, $"surface audit '{label}': piece='{r.gameObject.name}' slot={i} " +
                                        $"mat='{(mats[i] != null ? mats[i].name : "NULL")}' {why}" +
                                        $"{(autoFix ? " - forced stone" : "")}");
                    if (autoFix && chosen != null) AssignAllSlots(r, chosen);
                    break; // whole renderer is re-slotted; no need to re-report the siblings
                }
            }

            if (offenders == 0)
                FlowTrace.Step(Sys, $"surface audit '{label}': clean - every shell slot is a textureless stone mat");
            return offenders;
        }

        /// <summary>
        /// Standing headless proof for the WO-1004 acceptance line ("no rainbow surfaces").
        /// Loads every shipped room prefab and audits its surfaces WITHOUT a bake. Batch:
        /// <c>-executeMethod DeNelle.Editor.RoomForge.RoomForgeMaterials.AuditRoomPrefabs</c>
        /// </summary>
        [MenuItem("Defenders/Dungeon/Audit Room Prefabs (no atlas surfaces)")]
        public static void AuditRoomPrefabs()
        {
            // Re-run the exhaustive strip first: an audit against a mat that is itself dirty
            // would report the prefabs clean while the shared asset carries the rainbow.
            EnsureMenu();

            int prefabs = 0, offenders = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Dungeon/Rooms" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                prefabs++;
                var meta = go.GetComponent<DeNelle.Dungeons.RoomForge.RoomPrefabMeta>();
                bool accent = meta != null && (meta.archetype == "reward" || meta.archetype == "boss");
                offenders += VerifyRoomSurfaces(go, Path.GetFileNameWithoutExtension(path), accent, autoFix: false);
            }

            string marker = offenders == 0 ? "ROOM_SURFACES_OK" : "ROOM_SURFACES_FAIL";
            Debug.Log($"[RoomForgeMaterials] {marker} prefabs={prefabs} offendingSlots={offenders} " +
                      $"atlas={FindAtlasPath() ?? "MISSING"}");
        }

        // ---- internals -------------------------------------------------------

        /// <summary>Mesh surfaces only. Particle/line/trail renderers are presentation, never shell.</summary>
        private static bool IsShellRenderer(Renderer r)
            => r != null && (r is MeshRenderer || r is SkinnedMeshRenderer);

        /// <summary>Which stone a piece wants. Names only choose; they never gate.</summary>
        private static Material PickStone(string n, Material wall, Material floor)
        {
            n ??= string.Empty;
            bool isFloor = n.StartsWith("Floor") || n.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isFloor && floor != null) return floor;
            return wall != null ? wall : floor; // everything else (walls, chokes, seals, ceilings, unknowns) → wall stone
        }

        private static void AssignAllSlots(Renderer r, Material mat)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterials = new[] { mat };
                return;
            }
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == mat) continue;
                mats[i] = mat;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }

        /// <summary>Null when the slot is acceptable; otherwise a short reason string.</summary>
        private static string SlotOffence(Material m, Material wall, Material floor, Material accent)
        {
            if (m == null) return "slot is NULL (renders as the pipeline default, not stone)";
            if (m != wall && m != floor && m != accent) return "is not a shared RoomForge stone mat";
            string tex = FirstTextureProperty(m);
            return tex == null ? null : $"still carries a texture on '{tex}'";
        }

        /// <summary>First texture property on <paramref name="m"/> that is not null, else null.</summary>
        private static string FirstTextureProperty(Material m)
        {
            var sh = m != null ? m.shader : null;
            if (sh == null) return m != null && m.mainTexture != null ? "mainTexture" : null;
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                if (sh.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                string prop = sh.GetPropertyName(i);
                if (m.HasProperty(prop) && m.GetTexture(prop) != null) return prop;
            }
            return m.mainTexture != null ? "mainTexture" : null;
        }

        private static Material GetOrCreate(string matPath, Color stoneColor)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                // Regeneration: force solid stone. Strip EXHAUSTIVELY (see header) — older
                // versions of this asset assigned dungeon_texture.png and left a 1.5x/2.5x
                // tiling behind, which is the rainbow-stripe fingerprint.
                bool dirty = false;
                StripAllTextures(existing, ref dirty);
                ApplyStone(existing, stoneColor, ref dirty);
                if (dirty) EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find(UrpLit);
            if (shader == null)
            {
                Debug.LogError("[RoomForgeMaterials] URP Lit shader missing.");
                return null;
            }

            EnsureFolder(OutFolder);
            // Fresh URP/Lit with NO texture — solid stone. Cube shells must not sample the atlas.
            Material mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);

            bool _ = false;
            StripAllTextures(mat, ref _);
            ApplyStone(mat, stoneColor, ref _);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        /// <summary>
        /// Null EVERY texture property the shader declares, reset its scale/offset, and drop
        /// the sampler keywords. Walking the shader's own property table (rather than a
        /// hand-written list of slot names) is the whole point: this cannot be out of date
        /// with respect to the shader, so there is no "slot nobody thought of" left to leak
        /// the atlas through.
        /// </summary>
        public static void StripAllTextures(Material mat, ref bool dirty)
        {
            if (mat == null) return;

            var sh = mat.shader;
            if (sh != null)
            {
                int n = sh.GetPropertyCount();
                for (int i = 0; i < n; i++)
                {
                    if (sh.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                    string prop = sh.GetPropertyName(i);
                    if (!mat.HasProperty(prop)) continue;

                    if (mat.GetTexture(prop) != null) { mat.SetTexture(prop, null); dirty = true; }

                    // The TILING is half the defect — a stale 2.5x multiplier turns any future
                    // re-assignment straight back into repeating bands. Reset it with the map.
                    if (mat.GetTextureScale(prop) != Vector2.one) { mat.SetTextureScale(prop, Vector2.one); dirty = true; }
                    if (mat.GetTextureOffset(prop) != Vector2.zero) { mat.SetTextureOffset(prop, Vector2.zero); dirty = true; }
                }
            }

            if (mat.mainTexture != null) { mat.mainTexture = null; dirty = true; }
            if (mat.mainTextureScale != Vector2.one) { mat.mainTextureScale = Vector2.one; dirty = true; }
            if (mat.mainTextureOffset != Vector2.zero) { mat.mainTextureOffset = Vector2.zero; dirty = true; }

            foreach (var kw in SamplerKeywords)
            {
                if (!mat.IsKeywordEnabled(kw)) continue;
                mat.DisableKeyword(kw);
                dirty = true;
            }
        }

        private static void ApplyStone(Material mat, Color stoneColor, ref bool dirty)
        {
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            if (mat.HasProperty("_BaseColor"))
            {
                if (mat.GetColor("_BaseColor") != stoneColor) { mat.SetColor("_BaseColor", stoneColor); dirty = true; }
            }
            else if (mat.color != stoneColor) { mat.color = stoneColor; dirty = true; }

            if (mat.HasProperty("_Color") && mat.GetColor("_Color") != stoneColor)
            {
                mat.SetColor("_Color", stoneColor);
                dirty = true;
            }
        }

        public static string FindAtlasPath()
        {
            foreach (var p in AtlasCandidates)
            {
                if (File.Exists(p.Replace("Assets/", Application.dataPath + "/"))
                    || AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null)
                {
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null)
                        return p;
                }
            }
            // Fallback: GUID search
            string[] guids = AssetDatabase.FindAssets("dungeon_texture t:Texture2D", new[] { "Assets/Models/KayKit" });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith("dungeon_texture.png")) return path;
            }
            return null;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
