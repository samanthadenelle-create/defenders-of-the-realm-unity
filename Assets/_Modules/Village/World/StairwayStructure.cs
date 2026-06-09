// =============================================================================
// StairwayStructure — drag-and-drop wrapper around StairwayBuilder.
// -----------------------------------------------------------------------------
// Drop this on an empty GameObject, assign Start/End anchors (or a rise), set
// the form/width/step height in the Inspector, then right-click the component
// and choose "Generate Stairs". It calls the shared StairwayBuilder so ANY
// structure (this castle, another castle, the base-build catalog, an enemy
// camp) reuses the exact same fit-to-bounds stair geometry — composition, not
// copy-paste (the project's StructureFactory thesis).
//
// Generation is EDITOR-TIME via the ContextMenu (no runtime bake). The host
// scene's persisted NavMeshSurface bakes the generated step colliders; this
// component does not bake a surface at runtime.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Inspector-driven stair generator. Configure the fields, then right-click →
    /// "Generate Stairs" (or "Clear Generated Stairs"). Wraps <see cref="StairwayBuilder"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairwayStructure : MonoBehaviour
    {
        private const string GeneratedRootName = "GeneratedStairs";

        [Header("Form")]
        [Tooltip("Straight run or wide curved sweep (curved = the castle grand-stair feel).")]
        public StairwayForm form = StairwayForm.Curved;

        [Header("Anchors (world or relative)")]
        [Tooltip("Bottom anchor (courtyard). If null, this transform's position is the Start.")]
        public Transform startAnchor;
        [Tooltip("Top anchor (battlement edge). The top tread lands flush here.")]
        public Transform endAnchor;
        [Tooltip("Used only when endAnchor is null: vertical rise above Start, and horizontal run along +Z.")]
        public float rise = 11.5f;
        [Tooltip("Used only when endAnchor is null: horizontal run length along this transform's +Z.")]
        public float run = 14f;

        [Header("Geometry")]
        [Tooltip("Walkable tread width (≥~8 m for a multi-agent climb).")]
        public float width = 9f;
        [Tooltip("Target rise per step (~0.3–0.5). Step count is derived from the measured rise.")]
        public float stepHeight = 0.4f;
        [Tooltip("Tread depth (front-to-back) per piece.")]
        public float treadDepth = 0.55f;

        [Header("Curved sweep")]
        [Tooltip("Total sweep angle in degrees (90 = quarter turn, ~110 generous, 200 ≈ the draft).")]
        public float sweepDegrees = 110f;
        [Tooltip("Centreline radius of the sweep. Kept ≥ width so the inner band never pinches.")]
        public float radius = 12f;

        [Header("Extras")]
        [Tooltip("Thin cosmetic railing posts along both edges (colliders off).")]
        public bool railings = true;
        [Tooltip("Add ONE NavMeshLink across the chord (start→end) as the reliability backup.")]
        public bool chordNavLink = true;
        [Tooltip("Optional material for the tread renderers (null = default).")]
        public Material stepMaterial;

        /// <summary>Assemble the <see cref="StairwayBuilder.Params"/> from the Inspector fields.</summary>
        public StairwayBuilder.Params BuildParams()
        {
            Vector3 start = startAnchor != null ? startAnchor.position : transform.position;
            Vector3 end;
            if (endAnchor != null)
            {
                end = endAnchor.position;
            }
            else
            {
                // No explicit end: rise straight up and run forward along this transform's +Z.
                end = start + transform.forward * run + Vector3.up * rise;
            }

            return new StairwayBuilder.Params
            {
                Form         = form,
                Start        = start,
                End          = end,
                Width        = width,
                StepHeight   = stepHeight,
                TreadDepth   = treadDepth,
                SweepDegrees = sweepDegrees,
                Radius       = radius,
                Railings     = railings,
                ChordNavLink = chordNavLink,
                StepMaterial = stepMaterial,
            };
        }

        /// <summary>Editor button: (re)generate the stair geometry under this object.</summary>
        [ContextMenu("Generate Stairs")]
        public void GenerateStairs()
        {
            StairwayBuilder.Build(transform, GeneratedRootName, BuildParams());
            Debug.Log("[StairwayStructure] Generated stairs. Re-bake the scene NavMesh (editor) to walk them.");
        }

        /// <summary>Editor button: remove the generated stair geometry.</summary>
        [ContextMenu("Clear Generated Stairs")]
        public void ClearGeneratedStairs()
        {
            var prior = transform.Find(GeneratedRootName);
            if (prior != null)
            {
                if (Application.isPlaying) Destroy(prior.gameObject);
                else DestroyImmediate(prior.gameObject);
                Debug.Log("[StairwayStructure] Cleared generated stairs.");
            }
        }
    }
}
