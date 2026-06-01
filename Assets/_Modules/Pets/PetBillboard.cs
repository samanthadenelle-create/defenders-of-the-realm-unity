// =============================================================================
// PetBillboard — keeps a lite-pet sprite quad facing the camera (WO-211 Phase 2).
// -----------------------------------------------------------------------------
// The "lite pet visuals" path renders each pet as a single camera-facing sprite
// quad instead of its 208 MB of Tripo 3D meshes (the dominant WebGL build
// bloat). This tiny billboard keeps that quad turned toward the active camera on
// the Y axis only, so the sprite stays upright (no pitch/roll) as the hero +
// follow camera move around the village.
//
// Mirrors the inline PetNameTagBillboard in PetHeroLeash.cs, but constrains the
// rotation to yaw so the flat sprite never tilts toward an overhead camera.
// =============================================================================

using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// Rotates this transform each LateUpdate so its forward faces the active
    /// camera, but only about the world Y axis — an upright billboard. Used by
    /// the lite-pet sprite quad (see <c>PetDeployer.SpawnPet</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Flatten the camera direction to the XZ plane so the quad stays
            // upright regardless of the camera's pitch (e.g. an over-shoulder
            // or overhead view).
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // Quad meshes face -Z toward the viewer when the quad's +Z points
            // AWAY from the camera, so look along the away-from-camera vector.
            transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }
    }
}
