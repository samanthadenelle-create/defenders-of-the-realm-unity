// =============================================================================
// CastleHomeBuilder — Editor tool to create a new "Castle Home" village area
// adjacent to the main world (e.g. at offset from current Village/OuterWorld).
// Beautiful low-poly castle with 2-level interior (home access) + room for exactly
// 7 structures around it.
//
// Run via menu: Defenders > Art > Build Castle Home Village (adjacent to world)
//
// Uses existing project prefabs + notes for upgrading to beautiful pieces from
// our full catalog (Quaternius modular for castle beauty, KayKit Dungeon for
// gorgeous 2-level home interior, polyperfect/KayKit for the 7 structures +
// landscaping).
//
// Placed "adjacent": offset in world space so it feels connected (add path/bridge
// manually or via future builder pass). No hand-edit of .unity — this is the builder.
//
// Catalog note: See full asset list below in comments + the .md catalogs in docs/.
// =============================================================================

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DeNelle.Editor
{
    public static class CastleHomeBuilder
    {
        private const string MenuPath = "Defenders/Art/Build Castle Home Village (adjacent to world)";

        // World offset for "new village adjacent to this world".
        // Current village ~ (0,0,0), OuterWorld terrain ~ +/-150-300. This places it east-adjacent.
        private static readonly Vector3 CastleCenter = new Vector3(450f, 0f, 0f);

        [MenuItem(MenuPath)]
        public static void BuildCastleHomeArea()
        {
            if (!EditorUtility.DisplayDialog("Build Castle Home?",
                "This will create a new CastleHome area at " + CastleCenter + " (adjacent to current world).\n\n" +
                "Uses current prefabs for the 7 structures + basic castle proxy.\n" +
                "For final 'beautifully designed' castle: replace pieces with Quaternius walls/roofs/towers (symmetrical keep + 4 corner towers + gate) + KayKit Dungeon modular for gorgeous 2-level interior home (stairs, rooms).\n\n" +
                "The 7 structures have space around the castle. Interior has room to 'create as a home' (add furniture from catalog).\n\n" +
                "Run this in a test scene or current scene. Do NOT run on live Village.unity without backup.",
                "Build it", "Cancel"))
            {
                return;
            }

            var root = new GameObject("CastleHomeVillage");
            root.transform.position = CastleCenter;

            // --- Central Beautiful Castle (proxy for now; upgrade with catalog pieces) ---
            // Design: Symmetrical keep with 4 "towers" (using KitTower), central body, gate to south.
            // Two levels access: ground "Level1" + elevated "Level2" with stairs.
            // Room for home: labeled areas with space for customization.
            var castleRoot = new GameObject("CastleKeep_BeautifulDesign");
            castleRoot.transform.SetParent(root.transform, false);
            castleRoot.transform.localPosition = Vector3.zero;

            // Corner towers (beautiful low-poly towers from our assets; replace/duplicate with Quaternius tower pieces for more grandeur)
            var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/KitTower.prefab");
            if (towerPrefab != null)
            {
                Vector3[] towerOffsets = {
                    new Vector3(-15, 0, -15), new Vector3(15, 0, -15),
                    new Vector3(-15, 0, 15), new Vector3(15, 0, 15)
                };
                for (int i = 0; i < towerOffsets.Length; i++)
                {
                    var t = PrefabUtility.InstantiatePrefab(towerPrefab) as GameObject;
                    t.transform.SetParent(castleRoot.transform, false);
                    t.transform.localPosition = towerOffsets[i];
                    t.name = "CornerTower_" + (i + 1);
                }
            }

            // Central keep body (use existing houses as proxy for main hall; for beauty replace with Quaternius/KayKit castle keep + walls)
            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/HouseC.prefab");
            if (housePrefab != null)
            {
                var keep = PrefabUtility.InstantiatePrefab(housePrefab) as GameObject;
                keep.transform.SetParent(castleRoot.transform, false);
                keep.transform.localPosition = new Vector3(0, 0, 0);
                keep.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                keep.name = "CentralKeep_BeautifulProxy";
            }

            // Gatehouse (south access; use CastleGate model or another tower)
            if (towerPrefab != null)
            {
                var gate = PrefabUtility.InstantiatePrefab(towerPrefab) as GameObject;
                gate.transform.SetParent(castleRoot.transform, false);
                gate.transform.localPosition = new Vector3(0, 0, -20);
                gate.name = "Gatehouse_South";
            }

            // --- Two Levels Interior (home access — beautifully designed with catalog) ---
            // Level 1 (ground): Great Hall + entry (space for home customization).
            // Level 2 (upper): Private quarters (bedroom/study — "create as a home").
            // Access: Stairs between levels (use dungeon stairs from KayKit for beauty; proxy here).
            var interiorRoot = new GameObject("CastleInterior_TwoLevelsHome");
            interiorRoot.transform.SetParent(castleRoot.transform, false);
            interiorRoot.transform.localPosition = Vector3.zero;

            // Level 1 floor/platform (beautiful — replace floor with KayKit Dungeon floor pieces + props)
            var level1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            level1.transform.SetParent(interiorRoot.transform, false);
            level1.transform.localPosition = new Vector3(0, 0.1f, 0);
            level1.transform.localScale = new Vector3(20, 0.2f, 20);
            level1.name = "Level1_GreatHall_Entry (add KayKit Dungeon floors + furniture for home)";
            Object.DestroyImmediate(level1.GetComponent<Collider>()); // keep visual only for now

            // Level 2 floor (upper home level)
            var level2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            level2.transform.SetParent(interiorRoot.transform, false);
            level2.transform.localPosition = new Vector3(0, 5.1f, 0);
            level2.transform.localScale = new Vector3(18, 0.2f, 18);
            level2.name = "Level2_PrivateQuarters (bedroom/study — create as home with catalog furniture)";
            Object.DestroyImmediate(level2.GetComponent<Collider>());

            // Stairs access between levels (beautiful: replace with KayKit Dungeon stairs + railings)
            var stairs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stairs.transform.SetParent(interiorRoot.transform, false);
            stairs.transform.localPosition = new Vector3(8, 2.5f, 0);
            stairs.transform.localScale = new Vector3(3, 5, 2);
            stairs.transform.localRotation = Quaternion.Euler(0, 0, 30);
            stairs.name = "Stairs_Level1_to_Level2 (use KayKit Dungeon stairs for beauty; add door at top)";
            Object.DestroyImmediate(stairs.GetComponent<Collider>());

            // Basic "home" props in levels (space to create/customize; add KayKit Furniture Bits / RPG Tools here)
            // Level1 example
            var table1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table1.transform.SetParent(level1.transform, false);
            table1.transform.localPosition = new Vector3(-5, 1, 0);
            table1.transform.localScale = new Vector3(3, 0.5f, 1.5f);
            table1.name = "Level1_Table (home customization spot)";

            // Level2 example (bedroom feel)
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.transform.SetParent(level2.transform, false);
            bed.transform.localPosition = new Vector3(0, 1, 5);
            bed.transform.localScale = new Vector3(4, 1, 2);
            bed.name = "Level2_Bed (create as home — add KayKit beds/lamps)";

            // --- 7 Structures (room around castle for a small village feel) ---
            // Beautiful layout: ring around the castle with space between for paths/gardens.
            // Using existing project Generated prefabs for consistency (swap with Quaternius/KayKit for final beauty).
            var structuresRoot = new GameObject("VillageStructures_7Room");
            structuresRoot.transform.SetParent(root.transform, false);

            string[] structurePaths = {
                "Assets/Prefabs/Village/Generated/Building_arcane-tower.prefab", // 1. Tower/lookout
                "Assets/Prefabs/Village/Generated/Building_farm.prefab",         // 2. Farm/guest
                "Assets/Prefabs/Village/Generated/Building_forge.prefab",        // 3. Forge
                "Assets/Prefabs/Village/Generated/Building_lumbermill.prefab",   // 4. Workshop
                "Assets/Prefabs/Village/Generated/Building_market.prefab",       // 5. Market
                "Assets/Prefabs/Village/Generated/Building_pet-house.prefab",    // 6. Stable/home annex
                "Assets/Prefabs/Village/Generated/Building_workshop.prefab"      // 7. Armory/storage
            };

            Vector3[] structureOffsets = {
                new Vector3(35, 0, 0), new Vector3(25, 0, 30), new Vector3(0, 0, 40),
                new Vector3(-25, 0, 30), new Vector3(-35, 0, 0), new Vector3(-25, 0, -30),
                new Vector3(0, 0, -40)
            };

            for (int i = 0; i < 7; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<GameObject>(structurePaths[i]);
                if (s != null)
                {
                    var inst = PrefabUtility.InstantiatePrefab(s) as GameObject;
                    inst.transform.SetParent(structuresRoot.transform, false);
                    inst.transform.localPosition = structureOffsets[i];
                    inst.name = "Structure_" + (i + 1) + "_" + System.IO.Path.GetFileNameWithoutExtension(structurePaths[i]);
                }
            }

            // --- Landscaping & Connection (adjacent village feel, beautiful dressing) ---
            var landscaping = new GameObject("Landscaping_AdjacentVillage");
            landscaping.transform.SetParent(root.transform, false);

            // Simple path from main world (west) to castle gate (use props or note to add Quaternius/KayKit path pieces + trees).
            for (int i = 0; i < 8; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.transform.SetParent(landscaping.transform, false);
                p.transform.localPosition = new Vector3(-30 - i * 8, 0.05f, 0);
                p.transform.localScale = new Vector3(6, 0.1f, 2);
                p.name = "PathSegment_" + i;
                Object.DestroyImmediate(p.GetComponent<Collider>());
            }

            // Trees/props for beauty (from catalog: polyperfect Nature or KayKit Forest; add more manually).
            // Placeholder trees around for now.
            for (int i = 0; i < 12; i++)
            {
                var angle = i * 30 * Mathf.Deg2Rad;
                float r = 55 + (i % 3) * 5;
                var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                t.transform.SetParent(landscaping.transform, false);
                t.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 2, Mathf.Sin(angle) * r);
                t.transform.localScale = new Vector3(1.5f, 4, 1.5f);
                t.name = "TreeDressing_" + i + " (replace with beautiful Nature_M or KayKit trees)";
                Object.DestroyImmediate(t.GetComponent<Collider>());
            }

            // Note for final beauty (from full catalog):
            // - Castle exterior: Replace proxies with Quaternius Wall/Roof/Window-Door + polyperfect Medieval_M castle/wall/tower pieces arranged symmetrically.
            // - 2 levels home: Full KayKit Dungeon Remastered (floors, stairs, walls, doors) + Furniture Bits / RPG Tools for "create as a home" (bed, desk, lamps, storage — leave modular space).
            // - 7 structures: Swap with KayKit Hexagon homes/towers or polyperfect Buildings_M for varied beautiful low-poly village look.
            // - Landscaping: Add KayKit Forest Nature (trees, rocks) + polyperfect Nature/Animals + props for a lush adjacent village feel.
            // - Connect to main world: Extend path or add bridge using existing props.
            // Full catalog in docs/kaykit-asset-catalog.md, docs/polyperfect-asset-catalog.md, docs/INSTALLED_PACKS_INDEX.md, and Quaternius/KayKit folders.

            Selection.activeGameObject = root;
            Debug.Log("[CastleHomeBuilder] Created new village area adjacent to world at " + CastleCenter + 
                      ". Castle with 2-level home access + 7 structures placed. Upgrade pieces from catalog for final beauty. Run again to refresh.");
        }
    }
}