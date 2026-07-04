// =============================================================================
// WeaponPropReadablePostprocessor — RC3a FIX (2026-07-04, weapon-grip editor≠build).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// THE BUG THIS KILLS:
//   The hero weapon-prop FBXs under Resources/Heroes/Props/Weapons/ imported with
//   Read/Write DISABLED (isReadable:0). EquipmentController.CollectWidthProfile reads
//   sharedMesh.vertices to find the crossguard/hilt for the deterministic seat, and it
//   GUARDS on `!sharedMesh.isReadable` — so in a PLAYER BUILD (where an unreadable mesh
//   truly has no CPU-side vertices) the width profile came back EMPTY and the grip
//   refinement silently degraded, while the Editor behaved differently. That editor-vs-
//   build divergence is the core of the 3-month weapon-grip pain.
//
// THE FIX:
//   Force isReadable=true on EVERY model imported under the weapon-prop folder, so the
//   mesh carries CPU vertices in BOTH the Editor and a build — the seat reads the SAME
//   geometry either way (determinism = the whole point). Cost is a small CPU mesh copy
//   for a handful of tiny props; negligible.
//
//   OnPreprocessModel runs on (re)import. Existing assets keep their committed .meta
//   flag until reimported; the melee prop metas were flipped to isReadable:1 in the same
//   change so the shipped state is correct now, and this postprocessor guarantees any
//   NEW weapon dropped into the folder (or any reimport) is readable too — so a new
//   weapon inherits a working, build-identical grip with zero manual step.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class WeaponPropReadablePostprocessor : AssetPostprocessor
    {
        // Any model imported under this folder is a hero weapon prop the grip seat reads.
        private const string WeaponPropDir = "Assets/Resources/Heroes/Props/Weapons/";

        // Bumping this version forces Unity to REIMPORT models on the next asset refresh, so the
        // ALREADY-imported weapon FBXs (committed isReadable:0) pick up the readable flag below
        // without hand-editing 11 .meta files — the shipped build then carries readable meshes.
        // Bump again only if the readable rule changes.
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (assetImporter == null) return;
            string p = assetPath != null ? assetPath.Replace('\\', '/') : null;
            if (string.IsNullOrEmpty(p) || p.IndexOf(WeaponPropDir, System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            if (assetImporter is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                Debug.Log($"[WeaponPropReadable] forced Read/Write ON for weapon prop '{assetPath}' " +
                          "(RC3a: mesh verts must be readable in a BUILD so the grip seat is editor==build).");
            }
        }
    }
}
