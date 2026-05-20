// =============================================================================
// WizardFbxInspect — one-shot diagnostic. Logs every sub-asset Unity sees
// inside Assets/Models/Wizard/Wizard.fbx so we can confirm whether the
// Tripo-shipped walk + cast animations actually round-tripped through
// import. Editor-only, callable via -executeMethod.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WizardFbxInspect
    {
        private const string Path = "Assets/Models/Wizard/Wizard.fbx";

        [MenuItem("Defenders/Diagnostics/Inspect Wizard FBX")]
        public static void Inspect()
        {
            // Force importAnimation true + reimport so Unity surfaces any clips.
            var importer = AssetImporter.GetAtPath(Path) as ModelImporter;
            if (importer != null)
            {
                bool changed = false;
                if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
                if (importer.animationType == ModelImporterAnimationType.None)
                { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
                if (changed) importer.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            var assets = AssetDatabase.LoadAllAssetsAtPath(Path);
            Debug.Log($"[WizardFbxInspect] '{Path}' contains {assets.Length} sub-asset(s):");
            int meshCount = 0, animCount = 0, avatarCount = 0;
            foreach (var a in assets)
            {
                if (a == null) continue;
                Debug.Log($"  - {a.GetType().Name}: '{a.name}'");
                if (a is Mesh) meshCount++;
                else if (a is AnimationClip) animCount++;
                else if (a is Avatar) avatarCount++;
            }
            Debug.Log($"[WizardFbxInspect] Counts — Mesh={meshCount}, AnimationClip={animCount}, Avatar={avatarCount}");

            if (importer != null)
            {
                Debug.Log($"[WizardFbxInspect] importAnimation={importer.importAnimation}, " +
                          $"AnimationType={importer.animationType}, " +
                          $"clipCount={importer.clipAnimations.Length}, " +
                          $"defaultClipsCount={importer.defaultClipAnimations.Length}, " +
                          $"importBlendShapes={importer.importBlendShapes}");
                foreach (var c in importer.defaultClipAnimations)
                    Debug.Log($"    default clip: '{c.name}' [{c.firstFrame}..{c.lastFrame}]");
            }
        }
    }
}
