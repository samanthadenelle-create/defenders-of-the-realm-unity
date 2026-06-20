// =============================================================================
// BlinkArmorHumanoidFixer — the measured T-pose ROOT FIX (2026-06-20).
// -----------------------------------------------------------------------------
// PROVEN from import metadata: the Blink BASE body (HumanMale/Female_Character) is
// imported HUMANOID (animationType 3) and animates, but ALL ~70 armor body FBX
// (Basic*/Dragonic/Centurion/BeastHunter..._HumanMale/_HumanFemale) are GENERIC
// (animationType 2, no avatar). The game's animation library (Assets/Action — 198
// Mixamo clips, ALL Humanoid) retargets via HUMANOID, so a Humanoid clip has NOTHING
// to map onto a Generic armor rig -> the armor holds the bind/T-pose while the base
// animates. (HeroArmorVisual's runtime avatar-borrow can't rescue a Generic import.)
//
// FIX: set every armor body FBX to HUMANOID and COPY the matching base body's avatar
// (same Blink rig, BLINK_NOTES). Then the one Mixamo library drives every armor set
// identically to the base => no T-pose + the owner's "one rig, one library, dynamic".
// Idempotent (skips ones already Humanoid). Run: -executeMethod
// DeNelle.Editor.BlinkArmorHumanoidFixer.Run
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BlinkArmorHumanoidFixer
    {
        [MenuItem("Defenders/Art/Fix Blink Armor Rig (Generic -> Humanoid)")]
        public static void Run()
        {
            Avatar maleAvatar = FindAvatar("HumanMale_Character");
            Avatar femaleAvatar = FindAvatar("HumanFemale_Character");
            Debug.Log($"[BlinkArmorFixer] base avatars resolved: male={(maleAvatar != null)} female={(femaleAvatar != null)}.");

            int fixedCount = 0, alreadyOk = 0, noAvatar = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Blink" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().EndsWith(".fbx")) continue;
                string name = Path.GetFileNameWithoutExtension(path);

                bool male = name.EndsWith("_HumanMale");
                bool female = name.EndsWith("_HumanFemale");
                if (!male && !female) continue;                          // only armor body sets
                if (name == "HumanMale_Character" || name == "HumanFemale_Character") continue; // skip the base

                if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) continue;
                if (imp.animationType == ModelImporterAnimationType.Human) { alreadyOk++; continue; }

                Avatar src = male ? maleAvatar : femaleAvatar;
                if (src == null) { Debug.LogWarning($"[BlinkArmorFixer] no base avatar for '{name}' — skipped."); noAvatar++; continue; }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.sourceAvatar = src;             // Copy From Other Avatar (the shared Blink rig)
                imp.SaveAndReimport();
                fixedCount++;
            }

            Debug.Log($"[BlinkArmorFixer] DONE: {fixedCount} armor FBX set Humanoid (copy base avatar); " +
                      $"{alreadyOk} already Humanoid; {noAvatar} missing-avatar skipped. BLINK_ARMOR_HUMANOID_OK");
        }

        private static Avatar FindAvatar(string fbxName)
        {
            foreach (var guid in AssetDatabase.FindAssets(fbxName + " t:Model", new[] { "Assets/Blink" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != fbxName) continue;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Avatar av) return av;
            }
            return null;
        }
    }
}
