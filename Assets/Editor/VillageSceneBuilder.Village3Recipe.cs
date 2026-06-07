// =============================================================================
// VillageSceneBuilder.Village3Recipe — PHASE 1 of the "Village3 = authored shell
// + recipe-driven buildings" migration.
// -----------------------------------------------------------------------------
// Village2's gameplay buildings are HARD-CODED in the Buildings[] array
// (VillageSceneBuilder.Content.cs) and dropped into the scene by PlaceBuilding.
// This file makes that placement DATA-DRIVEN, purely additively:
//
//   1. ExportBuildingsToRecipe  — read Buildings[] -> write a JSON recipe
//      (id/label/type/x/z/yawDeg + visual source prefabM/customFbx/fbx + the
//      remaining visual-identity fields). Pure read->JSON; breaks nothing.
//
//   2. InjectBuildingsFromRecipe(recipePath) — read the recipe -> for EACH entry
//      rebuild a BuildingPlacement struct and call the SAME PlaceBuilding code the
//      hard-coded path uses. The hard-coded path AND the recipe path therefore
//      share ONE placement implementation (Configure + Interactable + Register +
//      OrientationFix identical). Then WireBuildingInteractables() (same as inject).
//
// Village2, Village2.unity, and InjectGameplayBuildingsIntoVillage2 are NEVER
// touched — this is additive. The BuildingPlacement struct (Content.cs) is already
// the shared shape PlaceBuilding consumes, so a recipe entry is simply that struct
// populated from JSON — no fork of PlaceBuilding's logic.
//
//   Defenders > Village3 > Export Buildings -> Recipe
//   (batchmode: DeNelle.Editor.VillageSceneBuilder.ExportBuildingsToRecipe)
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static partial class VillageSceneBuilder
    {
        // Recipe lives alongside the Village2 placement recipe precedent (_Village2).
        private const string Village3RecipePath = "Assets/_Village2/Village3BuildingRecipe.json";

        // -------------------------------------------------------------------------
        // Serializable recipe shape. Mirrors the visual-identity-bearing fields of
        // BuildingPlacement so the recipe path reproduces the hard-coded look exactly
        // (PrefabM wins over CustomFbx wins over Fbx — same precedence PlaceBuilding
        // already implements). placeholderColor is captured so the placeholder path
        // (missing model) matches too. JsonUtility serializes Color natively.
        // -------------------------------------------------------------------------
        [System.Serializable]
        private class Village3BuildingEntry
        {
            public string id;
            public string label;
            public int type;
            public float x;
            public float z;
            public float yawDeg;
            public string prefabM;       // primary polyperfect _M prefab path (wins)
            public string prefabM2;      // optional secondary polyperfect prefab path
            public string customFbx;     // Tripo FBX path (-90X OrientationFix path)
            public string fbx;           // KayKit base name (legacy native path)
            public string baseColorTex;  // single-texture Tripo basecolor override
            public string fenceKind;     // "wood" | "stone"
            public Color placeholderColor;
        }

        [System.Serializable]
        private class Village3BuildingRecipe
        {
            public List<Village3BuildingEntry> buildings = new List<Village3BuildingEntry>();
        }

        // =========================================================================
        // 1. EXPORT — Buildings[] -> recipe JSON. Pure read; never opens/edits a scene.
        // =========================================================================
        [MenuItem("Defenders/Village3/Export Buildings -> Recipe")]
        public static void ExportBuildingsToRecipe()
        {
            var recipe = new Village3BuildingRecipe();
            foreach (var b in Buildings)
            {
                recipe.buildings.Add(new Village3BuildingEntry
                {
                    id               = b.Id,
                    label            = b.Label,
                    type             = b.Type,
                    x                = b.X,
                    z                = b.Z,
                    yawDeg           = b.YawDeg,
                    prefabM          = b.PrefabM,
                    prefabM2         = b.PrefabM2,
                    customFbx        = b.CustomFbx,
                    fbx              = b.Fbx,
                    baseColorTex     = b.BaseColorTex,
                    fenceKind        = b.FenceKind,
                    placeholderColor = b.PlaceholderColor,
                });
            }

            string dir = Path.GetDirectoryName(Village3RecipePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Village3RecipePath, JsonUtility.ToJson(recipe, true));
            AssetDatabase.Refresh();

            Debug.Log($"[Village3Recipe] EXPORT DONE — wrote {recipe.buildings.Count} building(s) " +
                      $"-> {Village3RecipePath}. VILLAGE3_EXPORT_OK");
        }

        // =========================================================================
        // 2. RECIPE-FED INJECT — recipe JSON -> the SAME PlaceBuilding code.
        // sceneOrRecipe: a recipe .json path (preferred). If null/empty/not a .json,
        // the default Village3RecipePath is used (lets a batchmode caller omit args).
        // Operates on the CURRENTLY OPEN/ACTIVE scene (the Village3 builder opens it);
        // it does NOT open a scene itself, so it composes cleanly into a build chain.
        // =========================================================================
        public static int InjectBuildingsFromRecipe(string sceneOrRecipe = null)
        {
            string recipePath = (!string.IsNullOrEmpty(sceneOrRecipe) &&
                                 sceneOrRecipe.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                ? sceneOrRecipe
                : Village3RecipePath;

            if (!File.Exists(recipePath))
            {
                Debug.LogError($"[Village3Recipe] INJECT — recipe not found at '{recipePath}'. " +
                               "Run Defenders/Village3/Export Buildings -> Recipe first. Aborting.");
                return 0;
            }

            Village3BuildingRecipe recipe;
            try { recipe = JsonUtility.FromJson<Village3BuildingRecipe>(File.ReadAllText(recipePath)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Village3Recipe] INJECT — recipe parse failed ({e.Message}). Aborting.");
                return 0;
            }
            if (recipe == null || recipe.buildings == null || recipe.buildings.Count == 0)
            {
                Debug.LogWarning($"[Village3Recipe] INJECT — recipe '{recipePath}' is empty. Nothing placed.");
                return 0;
            }

            // VillageController for RegisterBuilding (null-tolerant — placement still works).
            Component controller = null;
            var ctrlType = FindType(TypeVillageController);
            if (ctrlType != null)
                controller = UnityEngine.Object.FindAnyObjectByType(ctrlType, FindObjectsInactive.Include) as Component;

            var parentGo = GameObject.Find("GameplayBuildings");
            if (parentGo == null) parentGo = new GameObject("GameplayBuildings");

            int placed = 0;
            foreach (var e in recipe.buildings)
            {
                // Rebuild the SAME struct PlaceBuilding consumes — the recipe entry IS
                // a BuildingPlacement populated from JSON. ONE shared placement path.
                var b = new BuildingPlacement
                {
                    Type             = e.type,
                    Id               = e.id,
                    Label            = e.label,
                    X                = e.x,
                    Z                = e.z,
                    YawDeg           = e.yawDeg,
                    Fbx              = e.fbx,
                    PlaceholderColor = e.placeholderColor,
                    FenceKind        = e.fenceKind,
                    CustomFbx        = e.customFbx,
                    BaseColorTex     = e.baseColorTex,
                    PrefabM          = e.prefabM,
                    PrefabM2         = e.prefabM2,
                };
                PlaceBuilding(parentGo.transform, b, controller, seatToGround: false);
                placed++;
            }

            // Identical to the hard-coded inject: give every building its F-prompt.
            WireBuildingInteractables();

            Debug.Log($"[Village3Recipe] INJECT DONE — placed {placed} building(s) from recipe " +
                      $"'{recipePath}' (same PlaceBuilding path as Buildings[]). VILLAGE3_INJECT_OK");
            return placed;
        }
    }
}
