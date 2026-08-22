// =============================================================================
// RaidWallMaterialRegression [raid-wall-material]  --  markers RAID_WALL_MATERIAL_OK / _FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Asset-lint (edit mode, no scene, no PlayMode).
// Registered ONCE in DataRegression.RunAll.  NEVER throws.
//
// THE DRIFT ORACLE FOR THE WHITE-SLAB CLASS (WO-838 Phase E).
//
// What it locks: the raid-base wall art must be reachable from TRACKED assets, not
// from an FBX-embedded material. WO-838 proved on disk that all three wall FBXes
// import with `externalObjects: {}` and bind their textures by ABSOLUTE PATH into a
// `.fbm` folder on the ORIGINAL AUTHOR'S MACHINE - a folder this repo does not
// contain. On every other machine the importer resolves NO albedo and produces a
// textureless lit material: 86 WHITE WALL SLABS in RaidBase_mage_enclave, in the
// editor and in every player build, WITH NO ERROR ANYWHERE.
//
// ⚠ WHY A GATE IS THE ONLY DETECTOR. MagentaGuard cannot see this and that is BY
// DESIGN, not a bug in it: a textureless-but-valid URP/Lit material passes
// MagentaGuard.IsBrokenShader (only null / unsupported / Standard / Legacy /
// InternalError count as broken), and its colorless repaint branch only applies to
// GROUND-LIKE renderers - a 1.5x3x1.5 wall segment is neither. White-but-valid is
// structurally invisible to the runtime guard. Nothing on screen says "broken".
// The owner's eyes were the detector, which is exactly what CLAUDE.md sec.14 exists
// to never rely on. So the detector has to live here, at asset-lint time.
//
// ⛔ THIS IS NOT A SECOND BROKEN-SHADER PREDICATE. MagentaGuard.IsBrokenShader stays
// the ONE authority for the MAGENTA class and VisualFactory.Skin stays its one choke
// point. This oracle tests a DIFFERENT property - "is the art wired to tracked
// assets" - at a different time (import, not runtime), and no runtime path calls it.
//
// The four pins, per wall tier (wood / iron / steel):
//   PIN 1  A tracked material exists at Resources/Walls/Materials/<tier>_wall.mat.
//   PIN 2  It is on URP/Lit - never Standard/Legacy (that trades the white-slab
//          defect for the magenta one, WO-838 Finding 3).
//   PIN 3  Its albedo is BOUND, to a texture that lives under the tracked
//          Resources/Walls/Textures folder. A null base map IS the white slab.
//   PIN 4  The tier's FBX importer carries an externalObjects Material remap onto
//          that .mat - i.e. the scene no longer depends on the embedded material.
//   PIN 5  WallTierData's SegmentPrefabPath for the tier still resolves through the
//          REAL Resources.Load call RaidBaseGenerator.PlaceSegment uses, so a
//          renamed/moved FBX is caught here rather than as a missing wall in a raid.
//
// RED-THEN-GREEN (WO-838 acceptance sec.5): on HEAD as of 2026-08-21 pins 1-4 are RED
// by construction - Resources/Walls/Materials does not exist and all three
// `.fbx.meta` still read `externalObjects: {}`. The remedy is ONE command:
//     powershell tools\run-unity-method.ps1 `
//       -Method DeNelle.Editor.RaidWallMaterialFixer.Run -LogName raid-wall-materials.log
//   then re-bake:  RaidBaseGenerator.BuildAllRaidScenes  ->  RaidNavBake.BakeAll
// The failure text below repeats that command, so a red run is self-remedying and
// never needs a human to remember a second step (CLAUDE.md sec.16's lesson).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RaidWallMaterialRegression
    {
        private const string MaterialsDir = "Assets/Resources/Walls/Materials";
        private const string TexturesDir  = "Assets/Resources/Walls/Textures";
        private const string WallsDir     = "Assets/Resources/Walls";

        // The tier tokens. Deliberately a literal here and not a reference to
        // RaidWallMaterialFixer: DeNelle.EditorRegression does not (and should not)
        // reference DeNelle.Editor, and an oracle that imports its subject's own
        // constants cannot catch that subject changing them.
        private static readonly string[] Tiers = { "wood", "iron", "steel" };

        private const string Remedy =
            "REMEDY (one command): powershell tools\\run-unity-method.ps1 -Method " +
            "DeNelle.Editor.RaidWallMaterialFixer.Run -LogName raid-wall-materials.log ; then re-bake " +
            "DeNelle.Editor.RaidBaseGenerator.BuildAllRaidScenes -> DeNelle.Editor.RaidNavBake.BakeAll.";

        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "raid-wall-material: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>Standalone batch entry.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("RAID_WALL_MATERIAL_OK - " + reason);
            else Debug.LogError("RAID_WALL_MATERIAL_FAIL - " + reason);
        }

        private static bool RunCore(out string reason)
        {
            var fails = new List<string>();
            var notes = new StringBuilder();
            int tiersChecked = 0;

            foreach (var tier in Tiers)
            {
                string fbxPath = $"{WallsDir}/{tier}_wall.fbx";
                string matPath = $"{MaterialsDir}/{tier}_wall.mat";

                if (!System.IO.File.Exists(fbxPath))
                {
                    // Warn, never fail, on a missing art file - CLAUDE.md sec.4 (the packs
                    // are gitignored and may not be imported on this seat).
                    notes.Append($"{tier}=FBX-ABSENT ");
                    continue;
                }
                tiersChecked++;

                // ── PIN 1 — the tracked material exists ─────────────────────────────
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    fails.Add($"wall tier '{tier}': NO tracked material at {matPath}. The scene's wall art can " +
                              $"therefore only come from {tier}_wall.fbx's EMBEDDED material, whose textures bind " +
                              "to a .fbm folder on the original author's machine that is not in this repo -> " +
                              "untextured WHITE SLABS in every build, with no error on screen. " + Remedy);
                    continue;
                }

                // ── PIN 2 — URP/Lit, never Standard/Legacy ──────────────────────────
                string shader = mat.shader != null ? mat.shader.name : "<null>";
                if (shader != "Universal Render Pipeline/Lit")
                {
                    fails.Add($"wall tier '{tier}': {matPath} is on shader '{shader}', not " +
                              "'Universal Render Pipeline/Lit'. Standard/Legacy under URP renders MAGENTA " +
                              "(WO-838 Finding 3) - that trades one visible defect for another. " + Remedy);
                }

                // ── PIN 3 — the albedo is bound, to TRACKED owner art ───────────────
                Texture albedo = null;
                if (mat.HasProperty("_BaseMap")) albedo = mat.GetTexture("_BaseMap");
                if (albedo == null && mat.HasProperty("_MainTex")) albedo = mat.GetTexture("_MainTex");

                if (albedo == null)
                {
                    fails.Add($"wall tier '{tier}': {matPath} has a NULL base map. A textureless opaque URP/Lit " +
                              "material is the white-slab defect itself, and it passes MagentaGuard.IsBrokenShader " +
                              "(valid shader) AND misses its ground-like repaint branch (a wall is not ground) - " +
                              "so nothing at runtime will ever report it. " + Remedy);
                }
                else
                {
                    string texPath = AssetDatabase.GetAssetPath(albedo);
                    if (string.IsNullOrEmpty(texPath) || !texPath.StartsWith(TexturesDir, StringComparison.OrdinalIgnoreCase))
                        fails.Add($"wall tier '{tier}': {matPath} base map '{albedo.name}' resolves to " +
                                  $"'{(string.IsNullOrEmpty(texPath) ? "<not an asset>" : texPath)}', which is OUTSIDE " +
                                  $"the tracked {TexturesDir}. Only owner art that git actually carries survives a " +
                                  "fresh clone (the ExteriorTerrainMaterial class, KEY_FACTS.md). " + Remedy);
                }

                // ── PIN 4 — the FBX importer remap exists and points at that .mat ───
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    fails.Add($"wall tier '{tier}': no ModelImporter at {fbxPath} (asset not imported as a model).");
                }
                else
                {
                    bool remapped = false;
                    var map = importer.GetExternalObjectMap();
                    if (map != null)
                    {
                        foreach (var kv in map)
                        {
                            if (kv.Key.type != typeof(Material)) continue;
                            if (kv.Value == mat) { remapped = true; break; }
                        }
                    }
                    if (!remapped)
                        fails.Add($"wall tier '{tier}': {fbxPath} has NO externalObjects Material remap onto " +
                                  $"{matPath}. Without the remap the importer keeps generating the embedded, " +
                                  "textureless material and every baked prefab instance serializes THAT - the " +
                                  "tracked .mat existing changes nothing on its own. " + Remedy);
                }

                // ── PIN 5 — the runtime load path still resolves ────────────────────
                // The REAL call RaidBaseGenerator.PlaceSegment makes, via the REAL
                // WallTierData path - not a re-derivation of the string.
                string resPath = DeNelle.Village.Walls.WallTierData.Get(TierIndex(tier)).SegmentPrefabPath;
                var loaded = Resources.Load<GameObject>(resPath);
                if (loaded == null)
                    fails.Add($"wall tier '{tier}': Resources.Load<GameObject>(\"{resPath}\") returned NULL - the " +
                              "path WallTierData hands RaidBaseGenerator.PlaceSegment does not resolve, so the " +
                              "raid base bakes with NO wall segments at all for this tier.");

                notes.Append($"{tier}=ok ");
            }

            if (tiersChecked == 0)
            {
                // Every wall FBX is absent. That is a seat/import condition, not a
                // product defect - stand down rather than report a false red.
                return RegressionOutcome.Skip(out reason, "raid-wall-material",
                    "no wall FBX present under " + WallsDir + " (art not imported on this seat)");
            }

            if (fails.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"raid-wall-material: {fails.Count} defect(s) across {tiersChecked} tier(s) - ");
                foreach (var f in fails) sb.Append("\n  - ").Append(f);
                reason = sb.ToString();
                return false;
            }

            reason = $"raid-wall-material: {tiersChecked}/{Tiers.Length} wall tiers on TRACKED URP/Lit materials " +
                     $"with bound owner albedo + an FBX externalObjects remap; every SegmentPrefabPath resolves. " +
                     $"[{notes.ToString().Trim()}]";
            return true;
        }

        // WallTierData is indexed 1..3 = Wood / Iron / ReinforcedSteel (index 0 unused).
        private static int TierIndex(string tier)
        {
            if (string.Equals(tier, "wood", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(tier, "iron", StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }
    }
}
