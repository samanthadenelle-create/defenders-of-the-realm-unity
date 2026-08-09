// =============================================================================
// SupercyanResourceWire — Resources mirrors for troop bodies + gear (WO-453 +
// troop-gear resolution).
// -----------------------------------------------------------------------------
// VisualFactory.Skin loads "Resources/Heroes/<model>"; Supercyan art lives under
// Assets/Supercyan/.... This tool writes lightweight prefab VARIANTS into
// Resources so TroopFactory + TroopGearApplier can load them at runtime.
//
// BODIES  (Resources/Heroes/SC_*)
//   SC_Footman    <- Knight base
//   SC_Archer     <- Archer base
//   SC_Barbarian  <- Barbarian base  (outrider)
//   SC_Mage       <- Mage base       (battlemage)
//
// GEAR    (Resources/TroopGear/*)
//   Sword, Spear, Bow, Shield, Staff, AxeRight  <- Base/High Quality item prefabs
//
// Base bodies are UNARMED; gear is attached at spawn by TroopGearApplier (not the
// WithItemAnimators pack scripts — those need runtime item logic we do not drive).
//
// Batchmode: DeNelle.Editor.SupercyanResourceWire.Run
// Menu:      Defenders/Troops/Wire Supercyan Bodies And Gear
// =============================================================================
using System.IO;
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class SupercyanResourceWire
    {
        private const string ScBodyBase =
            "Assets/Supercyan/Prefabs/Fantasy/Base/High Quality/";
        private const string HeroesDir = "Assets/Resources/Heroes";
        private const string GearDir = "Assets/Resources/TroopGear";

        private static readonly (string src, string dest)[] BodyMap =
        {
            ("Knight", "SC_Footman"),
            ("Archer", "SC_Archer"),
            ("Barbarian", "SC_Barbarian"),
            ("Mage", "SC_Mage"),
        };

        private static readonly (string src, string dest)[] GearMap =
        {
            ("Sword", "Sword"),
            ("Spear", "Spear"),
            ("Bow", "Bow"),
            ("Shield", "Shield"),
            ("StaffHeroes", "Staff"),
            ("AxeRight", "AxeRight"),
            ("Mace", "Mace"),
        };

        [MenuItem("Defenders/Troops/Wire Supercyan Bodies And Gear")]
        public static void Run()
        {
            EnsureFolder(HeroesDir);
            EnsureFolder(GearDir);

            int bodies = 0, gear = 0;
            foreach (var (srcName, destName) in BodyMap)
            {
                if (WriteVariant(ScBodyBase + srcName + ".prefab", $"{HeroesDir}/{destName}.prefab", srcName, destName))
                    bodies++;
            }

            foreach (var (srcName, destName) in GearMap)
            {
                if (WriteVariant(ScBodyBase + srcName + ".prefab", $"{GearDir}/{destName}.prefab", srcName, destName))
                    gear++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SupercyanResourceWire] bodies {bodies}/{BodyMap.Length} -> {HeroesDir}; " +
                      $"gear {gear}/{GearMap.Length} -> {GearDir}.");
        }

        /// <summary>Batchmode entry.</summary>
        public static void RunBatch() => Run();

        private static bool WriteVariant(string srcPath, string destPath, string srcName, string destName)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null)
            {
                Debug.LogWarning($"[SupercyanResourceWire] source missing: {srcPath} " +
                                 "(Supercyan pack not imported?) — skipped {destName}.");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (instance == null)
            {
                Debug.LogWarning($"[SupercyanResourceWire] could not instantiate {srcName} — skipped.");
                return false;
            }

            instance.name = destName;
            // Strip rigidbodies / item scripts that fight gameplay (toggle item keys etc.).
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);
            // Strip colliders on gear variants so they never block nav/hits when held.
            if (destPath.Contains("/TroopGear/"))
            {
                foreach (var c in instance.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
            }

            var variant = PrefabUtility.SaveAsPrefabAsset(instance, destPath);
            Object.DestroyImmediate(instance);
            if (variant == null)
            {
                Debug.LogWarning($"[SupercyanResourceWire] SaveAsPrefabAsset failed: {destPath}");
                return false;
            }
            Debug.Log($"[SupercyanResourceWire] {srcName} -> {destPath}");
            return true;
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
