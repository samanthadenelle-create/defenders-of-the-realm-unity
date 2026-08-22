// =============================================================================
// RaidWallMaterialFixer — WO-838 PHASE B. Makes the raid-base wall art SURVIVE
// this machine's import and the URP player build.
// -----------------------------------------------------------------------------
// THE DEFECT (PROVEN on disk, WO-838 Finding 1):
//   Assets/Resources/Walls/{wood,iron,steel}_wall.fbx import with
//   `materialImportMode: 2` + `externalObjects: {}` — FBX-EMBEDDED materials. The
//   FBXes bind their textures by ABSOLUTE PATH into
//   `...\steel_wall.fbm\fantasystonegateway3dmodel_basecolor...` on the ORIGINAL
//   AUTHOR'S MACHINE. That `.fbm` folder is not in this repo and steel_wall.fbx
//   carries ZERO embedded JPEG payloads, so the importer resolves NO albedo and
//   produces a textureless lit material -> WHITE WALL SLABS, in the editor and in
//   every build, on every machine but the author's.
//
//   The owner's textures DID ship — Assets/Resources/Walls/Textures/<tier>_*.JPEG
//   are git-tracked — but under DIFFERENT filenames than the FBX asks for, with no
//   externalObjects remap, so they are wired to NOTHING.
//
// THE FIX (both halves must run; neither works alone):
//   1. Author one TRACKED URP/Lit .mat per tier under Resources/Walls/Materials/,
//      binding the ALREADY-TRACKED owner textures.
//   2. Remap each FBX's embedded material to that tracked .mat via ModelImporter
//      `externalObjects` (AddRemap) — a SCRIPTED remap, never a hand-edited .meta.
//   After this the scenes' prefab instances serialize references to tracked
//   materials + tracked textures: survivable on every machine and every build.
//
// ⛔ NO CREATIVE SUBSTITUTION (memory `vfx-map-owner-tags-no-creative-pick`).
//   This wires ONLY textures the owner already shipped. Where a map has no URP/Lit
//   slot (see ROUGHNESS below) it is reported and HELD, never guessed at.
//
// ⚠ THIS DOES NOT FINISH THE JOB BY ITSELF. The three raid scenes still hold
//   prefab instances serialized against the OLD embedded materials. After this
//   runs, RE-BAKE (never hand-edit a .unity, CLAUDE.md §3):
//       DeNelle.Editor.RaidBaseGenerator.BuildAllRaidScenes
//       DeNelle.Editor.RaidNavBake.BakeAll
//
// Order of operations (WO-838 makes Phase A mandatory):
//   1. DeNelle.Editor.RaidBaseMatDiag.Run     <- capture the pre-fix proof line
//   2. DeNelle.Editor.RaidWallMaterialFixer.Run
//   3. re-bake (above)
//   4. DeNelle.Editor.RaidBaseMatDiag.Run     <- post-fix: tracked mat + bound albedo
//
// Headless:
//   powershell tools\run-unity-method.ps1 -Method DeNelle.Editor.RaidWallMaterialFixer.Run `
//             -LogName raid-wall-materials.log
// Menu: Defenders/Art/Fix Raid Wall Materials
//
// JUDGE BY THE MARKER ON A FRESH LOG, NEVER THE EXIT CODE (§8):
//   RAID_WALL_MAT_FIX_OK <n>/<n> tiers
// Idempotent: re-running reuses the existing .mat and re-asserts the same remap.
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class RaidWallMaterialFixer
    {
        public const string MaterialsDir = "Assets/Resources/Walls/Materials";
        private const string TexturesDir = "Assets/Resources/Walls/Textures";
        private const string WallsDir    = "Assets/Resources/Walls";

        /// <summary>The three wall tiers, keyed by the token both the FBX and the
        /// textures already use. Matches WallTierData.SegmentPrefabPath ("Walls/&lt;tier&gt;_wall").</summary>
        public static readonly string[] Tiers = { "wood", "iron", "steel" };

        /// <summary>Tracked material path for a tier — the ONE place this convention is written.</summary>
        public static string MaterialPath(string tier) => $"{MaterialsDir}/{tier}_wall.mat";

        /// <summary>Source FBX path for a tier.</summary>
        public static string FbxPath(string tier) => $"{WallsDir}/{tier}_wall.fbx";

        /// <summary>Basecolor texture path for a tier (owner art, git-tracked since 2026-07-14).</summary>
        public static string BaseColorPath(string tier) => $"{TexturesDir}/{tier}_basecolor.JPEG";

        [MenuItem("Defenders/Art/Fix Raid Wall Materials")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Flow:RaidWallMat] ===== WO-838 PHASE B — tracked wall materials + importer remap =====");

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                // Hard stop, loudly. Silently authoring Standard-shader materials here would
                // trade the white-slab defect for the magenta one (WO-838 Finding 3).
                sb.AppendLine("[Flow:RaidWallMat] FAIL 'Universal Render Pipeline/Lit' not found — URP package missing. " +
                              "Refusing to author materials on a fallback shader (that ships magenta). NOTHING WRITTEN.");
                Debug.LogError(sb.ToString());
                return;
            }

            EnsureFolder(MaterialsDir);

            int done = 0;
            var held = new List<string>();

            foreach (var tier in Tiers)
            {
                string fbx = FbxPath(tier);
                string matPath = MaterialPath(tier);

                if (!System.IO.File.Exists(fbx))
                {
                    sb.AppendLine($"[Flow:RaidWallMat] WARN tier '{tier}': FBX ABSENT at {fbx} — skipped " +
                                  "(art pack may not be imported; warn, never error — CLAUDE.md §4).");
                    continue;
                }

                // ── 1. the tracked material ─────────────────────────────────────────
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool created = false;
                if (mat == null)
                {
                    mat = new Material(lit) { name = tier + "_wall" };
                    AssetDatabase.CreateAsset(mat, matPath);
                    created = true;
                }
                else if (mat.shader != lit)
                {
                    // Drift repair: someone re-imported the pack and the .mat fell back.
                    mat.shader = lit;
                }

                var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath(tier));
                if (baseColor == null)
                {
                    sb.AppendLine($"[Flow:RaidWallMat] FAIL tier '{tier}': basecolor ABSENT at {BaseColorPath(tier)} — " +
                                  "the material would ship textureless (the exact white-slab defect). Tier NOT remapped.");
                    continue;
                }
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseColor);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseColor);
                // An albedo-textured surface must not also be tinted by a stale base colour.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

                // Normal map — needs its IMPORTER set to NormalMap or URP renders it as
                // colour data and Unity warns per-material. Importer change, not a
                // creative choice, so it is done here rather than reported.
                var normal = LoadAsNormalMap($"{TexturesDir}/{tier}_normal.JPEG", sb, tier);
                if (normal != null && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }

                // Metallic — URP/Lit reads metallic from _MetallicGlossMap RGB and
                // SMOOTHNESS from its ALPHA. The owner's metallic JPEG has no alpha, so
                // the map alone cannot carry smoothness (see ROUGHNESS below).
                var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{tier}_metallic.JPEG");
                if (metallic != null && mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", metallic);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                // ── ROUGHNESS: HELD, NOT GUESSED ────────────────────────────────────
                // URP/Lit has NO roughness slot — it is a SMOOTHNESS pipeline, and the
                // owner's <tier>_roughness.JPEG is the inverse. Converting it (invert +
                // pack into the metallic map's alpha) is a real authoring decision about
                // how shiny these walls read, which is exactly the kind of call this
                // script must NOT make silently. Reported to the owner instead.
                var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{tier}_roughness.JPEG");
                if (roughness != null)
                    held.Add($"{tier}_roughness.JPEG — URP/Lit has no roughness slot; needs an owner call on " +
                             "invert-and-pack-into-_MetallicGlossMap alpha vs a flat smoothness value. Left unwired.");

                EditorUtility.SetDirty(mat);

                // ── 2. the importer remap ───────────────────────────────────────────
                int remapped = Remap(fbx, mat, sb, tier);

                sb.AppendLine($"[Flow:RaidWallMat] tier '{tier}': mat={(created ? "CREATED" : "reused")} {matPath} " +
                              $"baseMap={baseColor.name} normal={(normal != null)} metallic={(metallic != null)} " +
                              $"embeddedMaterialsRemapped={remapped}");

                if (remapped > 0) done++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var h in held)
                sb.AppendLine("[Flow:RaidWallMat] HELD (owner call, no creative substitution): " + h);

            sb.AppendLine("[Flow:RaidWallMat] NEXT (not done here): re-bake the raid scenes so their prefab " +
                          "instances stop referencing the old embedded materials — " +
                          "DeNelle.Editor.RaidBaseGenerator.BuildAllRaidScenes then DeNelle.Editor.RaidNavBake.BakeAll.");
            sb.AppendLine($"[Flow:RaidWallMat] RAID_WALL_MAT_FIX_OK {done}/{Tiers.Length} tiers");
            Debug.Log(sb.ToString());
        }

        // Points every embedded Material sub-asset of the FBX at the tracked .mat.
        // Remapping BY NAME (SourceAssetIdentifier) is the sanctioned ModelImporter
        // path; the names come from the FBX itself, so nothing is guessed here.
        private static int Remap(string fbxPath, Material tracked, StringBuilder sb, string tier)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                sb.AppendLine($"[Flow:RaidWallMat] FAIL tier '{tier}': no ModelImporter at {fbxPath}.");
                return 0;
            }

            var names = new List<string>();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var m = sub as Material;
                if (m != null && !string.IsNullOrEmpty(m.name)) names.Add(m.name);
            }

            if (names.Count == 0)
            {
                // Nothing to key the remap on. Do NOT invent a name — that silently
                // writes a remap Unity will never match and reads as "fixed".
                sb.AppendLine($"[Flow:RaidWallMat] FAIL tier '{tier}': {fbxPath} exposes ZERO embedded Material " +
                              "sub-assets — cannot key an externalObjects remap. Report, do not invent a name.");
                return 0;
            }

            foreach (var n in names)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), tracked);

            importer.SaveAndReimport();
            return names.Count;
        }

        // Flips a texture's importer to NormalMap when it is not already. Returns the
        // texture (post-reimport) or null when absent.
        private static Texture2D LoadAsNormalMap(string path, StringBuilder sb, string tier)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                sb.AppendLine($"[Flow:RaidWallMat] WARN tier '{tier}': normal map absent at {path} — material ships flat-shaded.");
                return null;
            }

            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return tex;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            string leaf = folder.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
