using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Auto-configures Mixamo motion clips dropped into <c>Assets/Action/</c> as
    /// retargetable Humanoid animation.
    ///
    /// Heroes and enemies are Humanoid, so a single clip imported this way
    /// retargets to Knight, Mage, Ranger, and every future CC5 hero with no
    /// per-character re-authoring.
    ///
    /// Horizontal root motion is baked into the pose ("in place") so the mesh
    /// animates without translating — code / NavMesh drives world movement.
    /// This is the fix for the historical "hero slides across the ground" bug:
    /// the slide was unwired animation PLUS root drift fighting the locomotion.
    /// </summary>
    public class ActionClipImporter : AssetPostprocessor
    {
        private const string ActionFolder = "Assets/Action/";

        private bool IsActionAsset =>
            assetPath.Replace('\\', '/').StartsWith(ActionFolder, System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsActionAsset) return;

            var importer = (ModelImporter)assetImporter;

            // Humanoid + self-generated avatar: every Mixamo FBX carries the
            // mixamorig skeleton, so it can build its own avatar with no
            // dependency on X Bot being imported first.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // These are motion clips, not art — keep imports lean.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsActionAsset) return;

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            string lower = assetPath.ToLowerInvariant();
            bool looping = lower.Contains("idle")
                        || lower.Contains("walk")
                        || lower.Contains("run");

            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];

                c.loopTime = looping;

                // Bake horizontal root translation into the pose -> in place.
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = false;

                // Preserve vertical motion (jumps / falling deaths read wrong
                // if flattened) and original facing.
                c.lockRootHeightY = false;
                c.keepOriginalPositionY = true;
                c.lockRootRotation = false;
                c.keepOriginalOrientation = true;

                clips[i] = c;
            }

            importer.clipAnimations = clips;
        }
    }
}
