// =============================================================================
// KayKitMaterials — URP material repair for already-imported KayKit models
// -----------------------------------------------------------------------------
// THE PROBLEM. The KayKit packs (Medieval Hexagon Pack, Adventurers, Dungeon,
// etc.) ship FBX/GLB/OBJ models but NO Unity .mat files. When Unity imports an
// FBX it auto-creates a material from whatever shader the FBX names — in a URP
// project that comes out as either:
//   • white  — the embedded material has no _BaseMap texture wired up, or
//   • magenta — the embedded material references a Built-in-pipeline shader
//               (Standard / legacy) that URP cannot render.
//
// THE FIX. Every KayKit model folder ships its own shared palette atlas
// (hexagons_medieval.png for the Medieval Hexagon Pack; *_texture.png for the
// character / weapon packs; dungeon_texture.png for the Dungeon pack). This
// utility, per model folder:
//   1. Locates the shared palette texture sitting beside the FBX files.
//   2. Creates ONCE a `Universal Render Pipeline/Lit` material that wires that
//      texture into _BaseMap, with a flat low-poly look (smoothness 0,
//      metallic 0). The .mat is written next to the texture and cached so a
//      folder full of 27 buildings shares ONE material.
//   3. Remaps every FBX importer's material references to that .mat via the
//      ModelImporter external-material remap (AddRemap) and reimports.
//
// IDEMPOTENT. Re-running is safe: an existing .mat is reused (and re-pointed at
// the right shader/texture), and a remap that already points at the right
// material is left alone — so no needless reimport churn.
//
// HOW TO RUN.
//   • Editor menu:  Tools ▸ DeNelle ▸ Fix KayKit Materials
//   • Batch mode :  -executeMethod DeNelle.Editor.KayKitMaterials.FixAllMaterials
//
// The companion AssetImportPostprocessor.OnAssignMaterialModel hook makes FUTURE
// imports come out correct automatically; this utility repairs the models that
// were ALREADY imported before the hook existed.
//
// ASSEMBLY NOTE. Lives in the fixed DeNelle.Editor.asmdef (Editor-only). Uses
// only UnityEditor + UnityEngine APIs the asmdef already exposes.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Repairs the auto-imported materials of every KayKit model under
    /// <c>Assets/Models/KayKit/</c> so they render with a proper URP/Lit
    /// material driven by the pack's shared palette atlas.
    /// </summary>
    public static class KayKitMaterials
    {
        // ── Scope ────────────────────────────────────────────────────────────
        private const string KayKitRoot = "Assets/Models/KayKit";

        // ── URP shader ───────────────────────────────────────────────────────
        // The Universal Render Pipeline lit shader. This is the one that renders
        // correctly in this URP 17.x project; the Built-in "Standard" shader the
        // FBX auto-import picks is what produces the magenta error material.
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        // ── Material look ────────────────────────────────────────────────────
        // KayKit art is flat-shaded low-poly: a palette texture, no specular
        // highlight, no metalness.
        private const float Smoothness = 0f;
        private const float Metallic   = 0f;

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>
        /// Menu entry — fixes every already-imported KayKit model's materials.
        /// </summary>
        [MenuItem("Tools/DeNelle/Fix KayKit Materials")]
        public static void FixAllMaterialsMenu()
        {
            FixAllMaterials();
        }

        /// <summary>
        /// Batch-runnable entry point
        /// (<c>-executeMethod DeNelle.Editor.KayKitMaterials.FixAllMaterials</c>).
        /// Walks every FBX under <c>Assets/Models/KayKit/</c>, builds (once per
        /// folder) a URP/Lit material from the folder's shared palette atlas,
        /// remaps the FBX importers' material references to it, and reimports.
        /// Safe to run repeatedly.
        /// </summary>
        public static void FixAllMaterials()
        {
            Shader urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                Debug.LogError(
                    $"[KayKitMaterials] Shader '{UrpLitShaderName}' not found — is the " +
                    "Universal RP package installed? Aborting.");
                return;
            }

            // Every FBX under the KayKit root. GUID search keeps it to assets
            // actually in the AssetDatabase (no stray files in Library, etc.).
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { KayKitRoot });
            if (fbxGuids.Length == 0)
            {
                Debug.LogWarning(
                    $"[KayKitMaterials] No models found under '{KayKitRoot}/'. Nothing to fix.");
                return;
            }

            // One material per (texture) — cached so a folder of 27 buildings
            // that all share hexagons_medieval.png reuses a single .mat.
            var materialByTexturePath = new Dictionary<string, Material>(
                StringComparer.OrdinalIgnoreCase);

            int modelsRemapped = 0;
            int modelsSkipped  = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < fbxGuids.Length; i++)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                    if (string.IsNullOrEmpty(fbxPath)) continue;

                    EditorUtility.DisplayProgressBar(
                        "Fix KayKit Materials",
                        fbxPath,
                        (float)i / fbxGuids.Length);

                    string texturePath = FindPaletteTexture(fbxPath);
                    if (string.IsNullOrEmpty(texturePath))
                    {
                        // No shared atlas beside this model — leave it alone
                        // rather than guess. (Animation-only rig FBX files and
                        // a few odd packs have no co-located texture.)
                        modelsSkipped++;
                        continue;
                    }

                    if (!materialByTexturePath.TryGetValue(texturePath, out Material mat))
                    {
                        mat = GetOrCreateUrpMaterial(texturePath, urpLit);
                        materialByTexturePath[texturePath] = mat;
                    }
                    if (mat == null)
                    {
                        modelsSkipped++;
                        continue;
                    }

                    if (RemapModelMaterials(fbxPath, mat))
                        modelsRemapped++;
                    else
                        modelsSkipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[KayKitMaterials] Done. Models remapped/reimported: {modelsRemapped}; " +
                $"skipped (no co-located atlas): {modelsSkipped}; " +
                $"shared materials: {materialByTexturePath.Count}.");
        }

        // =====================================================================
        //  Material creation
        // =====================================================================

        /// <summary>
        /// Returns the cached <c>.mat</c> for <paramref name="texturePath"/>,
        /// creating a new <c>URP/Lit</c> material beside the texture if none
        /// exists. An existing <c>.mat</c> is re-pointed at the URP shader and
        /// the palette texture (so a stale Built-in material is healed too).
        /// </summary>
        private static Material GetOrCreateUrpMaterial(string texturePath, Shader urpLit)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogWarning(
                    $"[KayKitMaterials] Could not load texture '{texturePath}'.");
                return null;
            }

            // .mat sits beside the texture, named "<texture>_URP.mat" so it does
            // not collide with anything KayKit might ship later.
            string dir     = Path.GetDirectoryName(texturePath)?.Replace('\\', '/');
            string texName = Path.GetFileNameWithoutExtension(texturePath);
            string matPath = $"{dir}/{texName}_URP.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != urpLit)
            {
                // Heal a material that somehow ended up on the wrong shader.
                mat.shader = urpLit;
            }

            ConfigureUrpLitMaterial(mat, texture);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Wires <paramref name="texture"/> into the URP/Lit base map and dials
        /// in the flat low-poly KayKit look (smoothness 0, metallic 0, opaque).
        /// </summary>
        internal static void ConfigureUrpLitMaterial(Material mat, Texture2D texture)
        {
            // URP/Lit base colour map + tint. Set both the URP names and the
            // legacy aliases so the texture shows regardless of which property
            // block the shader variant exposes.
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);

            // Flat low-poly surface — no gloss, no metal.
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Smoothness);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", Metallic);

            // Opaque workflow — the hexagon palette has no transparency.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f); // 0 = Opaque

            mat.mainTexture = texture;
        }

        // =====================================================================
        //  Importer remap
        // =====================================================================

        /// <summary>
        /// Points every material slot of the FBX at <paramref name="mat"/> via
        /// the importer's external-object remap, and reimports if anything
        /// changed. Returns true when a reimport was issued.
        /// </summary>
        private static bool RemapModelMaterials(string fbxPath, Material mat)
        {
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
                return false;

            // Make the importer USE external materials and search for them — so
            // our remap actually takes effect instead of an embedded material.
            bool importerChanged = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importerChanged = true;
            }
            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importerChanged = true;
            }

            // The remap is keyed by the sub-asset identifier of each Material
            // the FBX defines. Enumerate the FBX's own objects to find them.
            var sourceMaterials = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(fbxPath)
                .OfType<Material>()
                .ToArray();

            // Fallback: a freshly-discovered FBX may report no sub-materials
            // until first import. The existing remap keys (if any) still let us
            // re-point them; collect those too.
            var identifiers = new List<AssetImporter.SourceAssetIdentifier>();
            foreach (var srcMat in sourceMaterials)
            {
                identifiers.Add(new AssetImporter.SourceAssetIdentifier(srcMat));
            }
            foreach (var kvp in importer.GetExternalObjectMap())
            {
                if (kvp.Key.type == typeof(Material) &&
                    !identifiers.Any(id => id.name == kvp.Key.name && id.type == kvp.Key.type))
                {
                    identifiers.Add(kvp.Key);
                }
            }

            bool remapChanged = false;
            foreach (var id in identifiers)
            {
                if (id.type != typeof(Material)) continue;

                // Already pointing at the right material? leave it.
                var current = importer.GetExternalObjectMap();
                if (current.TryGetValue(id, out UnityEngine.Object existing) && existing == mat)
                    continue;

                importer.AddRemap(id, mat);
                remapChanged = true;
            }

            if (!importerChanged && !remapChanged)
                return false; // already correct — idempotent no-op

            importer.SaveAndReimport();
            return true;
        }

        // =====================================================================
        //  Palette-texture discovery
        // =====================================================================

        /// <summary>
        /// Finds the shared palette / atlas texture that belongs with the FBX
        /// at <paramref name="fbxPath"/>. Search order:
        ///   1. <c>hexagons_medieval.png</c> in the FBX's own folder (the
        ///      Medieval Hexagon Pack — explicitly the plain palette, never the
        ///      _Fall / _Summer / _Winter seasonal variants).
        ///   2. Any single shared atlas in the FBX's own folder
        ///      (<c>*_texture.png</c>, <c>dungeon_texture.png</c>, a lone
        ///      <c>*palette*.png</c>, etc.).
        ///   3. The same, walking up to a sibling <c>Textures</c> folder.
        /// Returns an empty string when no co-located atlas can be identified.
        /// </summary>
        internal static string FindPaletteTexture(string fbxPath)
        {
            string folder = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return string.Empty;

            // 1 + 2 — search the model's own folder first.
            string found = PickAtlasInFolder(folder, fbxPath);
            if (!string.IsNullOrEmpty(found)) return found;

            // 3 — climb a couple of levels looking for a Textures folder that
            // sits beside the model tree (covers packs whose FBX live in an
            // Assets/ tree and textures in a sibling Textures/ folder).
            string probe = folder;
            for (int up = 0; up < 4 && !string.IsNullOrEmpty(probe); up++)
            {
                string[] subdirs = AssetDatabase.GetSubFolders(probe);
                foreach (string sub in subdirs)
                {
                    string leaf = Path.GetFileName(sub);
                    if (leaf.Equals("Textures", StringComparison.OrdinalIgnoreCase) ||
                        leaf.Equals("texture", StringComparison.OrdinalIgnoreCase))
                    {
                        found = PickAtlasInFolder(sub, fbxPath);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                }
                probe = ParentFolder(probe);
            }

            return string.Empty;
        }

        /// <summary>
        /// Picks the best shared-atlas texture directly inside
        /// <paramref name="folder"/> (non-recursive). Prefers an exact
        /// <c>hexagons_medieval.png</c>, then any obvious atlas; never returns a
        /// seasonal <c>_Fall/_Summer/_Winter</c> variant.
        /// </summary>
        private static string PickAtlasInFolder(string folder, string fbxPath)
        {
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            if (texGuids.Length == 0) return string.Empty;

            var candidates = new List<string>();
            foreach (string g in texGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                // Non-recursive: only textures DIRECTLY in this folder.
                if (!string.Equals(Path.GetDirectoryName(p)?.Replace('\\', '/'),
                                   folder, StringComparison.OrdinalIgnoreCase))
                    continue;
                candidates.Add(p);
            }
            if (candidates.Count == 0) return string.Empty;

            // 1 — exact plain hexagons_medieval (NOT the seasonal variants).
            foreach (string p in candidates)
            {
                string name = Path.GetFileNameWithoutExtension(p);
                if (name.Equals("hexagons_medieval", StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            // 2 — a single shared atlas: a *_texture, *palette*, *colormap*,
            // or *atlas* PNG that is NOT a seasonal variant.
            var atlases = candidates
                .Where(p => IsAtlasName(Path.GetFileNameWithoutExtension(p)))
                .Where(p => !IsSeasonalVariant(Path.GetFileNameWithoutExtension(p)))
                .ToList();

            // Prefer an atlas whose name matches the FBX's character/colour stem
            // (e.g. knight.fbx -> knight_texture.png) when several exist.
            if (atlases.Count > 1)
            {
                string fbxStem = Path.GetFileNameWithoutExtension(fbxPath)
                    .ToLowerInvariant();
                string best = atlases.FirstOrDefault(p =>
                    fbxStem.StartsWith(Path.GetFileNameWithoutExtension(p)
                            .ToLowerInvariant()
                            .Replace("_texture", string.Empty)));
                if (!string.IsNullOrEmpty(best)) return best;
            }

            if (atlases.Count >= 1) return atlases[0];

            // 3 — exactly one texture in the folder: treat it as the atlas.
            var nonSeasonal = candidates
                .Where(p => !IsSeasonalVariant(Path.GetFileNameWithoutExtension(p)))
                .ToList();
            if (nonSeasonal.Count == 1) return nonSeasonal[0];

            return string.Empty;
        }

        /// <summary>True for the usual KayKit shared-atlas naming.</summary>
        private static bool IsAtlasName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("hexagons_medieval")
                || n.Contains("_texture")
                || n.EndsWith("texture")
                || n.Contains("palette")
                || n.Contains("colormap")
                || n.Contains("atlas")
                || n.Contains("_albedo")
                || n.Contains("_basecolor");
        }

        /// <summary>
        /// True for the seasonal palette variants
        /// (<c>hexagons_medieval_Fall/_Summer/_Winter</c>) — these must never be
        /// chosen for tiles; the plain <c>hexagons_medieval.png</c> is the one.
        /// </summary>
        private static bool IsSeasonalVariant(string name)
        {
            string n = name.ToLowerInvariant();
            return n.EndsWith("_fall")
                || n.EndsWith("_summer")
                || n.EndsWith("_winter")
                || n.EndsWith("_spring");
        }

        /// <summary>Returns the parent folder asset path, or empty at the root.</summary>
        private static string ParentFolder(string folder)
        {
            int slash = folder.LastIndexOf('/');
            return slash <= 0 ? string.Empty : folder.Substring(0, slash);
        }
    }
}
