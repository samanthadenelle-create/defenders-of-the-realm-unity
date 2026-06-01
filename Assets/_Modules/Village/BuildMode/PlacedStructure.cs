// =============================================================================
// PlacedStructure — runtime marker on every player-placed structure (WO-108).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Tags a GameObject as belonging to the player's editable BaseLayout and carries
// the live grid metadata (catalog id, cell, footprint cells, yaw steps, level,
// sell value). The save spine round-trips PlacedStructureData (Core); this is its
// in-scene twin. BuildModeController / BaseLayoutLoader read+write these to
// rebuild the BaseLayout on Exit and to drive select / sell (P2).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime metadata on a player-placed structure. The bridge between the live
    /// scene object and its persisted <see cref="PlacedStructureData"/> record.
    /// </summary>
    public sealed class PlacedStructure : MonoBehaviour
    {
        /// <summary>CatalogEntry id this structure was built from.</summary>
        public string itemId;

        /// <summary>The grid cell its footprint origin sits on.</summary>
        public Vector2Int gridCell;

        /// <summary>Footprint size in cells (e.g. 1×1 wall, 2×2 tower).</summary>
        public Vector2Int footprint = Vector2Int.one;

        /// <summary>Discrete yaw, 0..3 (× 90°).</summary>
        public int yawSteps;

        /// <summary>Upgrade level (1-based).</summary>
        public int level = 1;

        /// <summary>Crystals returned on sell (P2). 50% of build cost by convention.</summary>
        public int sellValue;

        /// <summary>Snapshot this live structure into its persisted record.</summary>
        public PlacedStructureData ToSaveData() =>
            new PlacedStructureData(itemId, gridCell.x, gridCell.y, yawSteps, level);

        // ── Selection highlight (WO-108 P2) ──────────────────────────────────────
        // A non-destructive emissive tint via a shared MaterialPropertyBlock — the
        // same proven approach GhostPreview uses, so it never leaks a material
        // instance and restores cleanly on deselect.

        private static readonly Color s_highlight = new Color(0.30f, 0.85f, 1f, 1f);
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private bool _highlighted;

        /// <summary>Toggle the selection highlight (emissive tint) on this structure.</summary>
        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderers.Count == 0)
                _renderers.AddRange(GetComponentsInChildren<Renderer>(true));

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                if (on)
                {
                    _mpb.SetColor("_EmissionColor", s_highlight);
                }
                else
                {
                    _mpb.SetColor("_EmissionColor", Color.black);
                }
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
