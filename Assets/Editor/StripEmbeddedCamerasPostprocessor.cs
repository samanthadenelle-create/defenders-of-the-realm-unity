// =============================================================================
// StripEmbeddedCamerasPostprocessor — removes CAMERA + AUDIOLISTENER nodes that
// Tripo / Blender FBX exports bake into model files.
// -----------------------------------------------------------------------------
// Root cause of the long-running "camera follows the pet" bug (2026-05-25): the
// pet/hero FBXs ship with an embedded camera node. Instantiated at runtime that
// camera renders to the SCREEN from the model, hijacking the display from the
// hero's VillageCamera (runtime showed 12 live cameras for a scene that bakes
// only 1). Runtime strippers in PetDeployer / HeroBodySwapper + VillageCamera's
// sole-camera enforcement fix it defensively; THIS import hook is the permanent,
// project-wide cure — no game model has any business carrying a camera, so any
// imported model gets these stripped once, at import, for every current and
// future asset.
//
// Re-import existing models (Assets → Reimport, or right-click a folder →
// Reimport) to clean assets that were imported before this hook existed.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class StripEmbeddedCamerasPostprocessor : AssetPostprocessor
    {
        // Runs on the instantiated model root after Unity finishes importing an
        // FBX/model. Editing the hierarchy here bakes the change into the asset.
        private void OnPostprocessModel(GameObject root)
        {
            int cams = 0, listeners = 0;

            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam == null) continue;
                Object.DestroyImmediate(cam, true);   // remove just the Camera component
                cams++;
            }
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
            {
                if (al == null) continue;
                Object.DestroyImmediate(al, true);
                listeners++;
            }

            if (cams > 0 || listeners > 0)
                Debug.Log($"[StripEmbeddedCameras] '{assetPath}': removed {cams} camera(s) " +
                          $"+ {listeners} audio listener(s) embedded by the export tool.");
        }
    }
}
