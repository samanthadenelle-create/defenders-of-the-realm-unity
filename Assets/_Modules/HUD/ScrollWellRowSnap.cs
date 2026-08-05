// =============================================================================
// ScrollWellRowSnap - a masked scroll well may only ever show WHOLE rows (WO-882).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// WHAT BROKE (measured off Builds/ui-capture/HelpMenu_2340x1080.png, WO-882):
// the Help menu's WO-795 scroll well is fraction-anchored to the modal body, so
// its height is whatever the body leaves - 395 screen px against a 166 px row
// pitch. The RectMask2D therefore cut the THIRD row at 36 px of its 146 px: the
// top bevel and a slab of face rendered, the centred label did not. On screen
// that is a tappable button with no text - the exact "blank third button" the
// owner reported.
//
// THE RULE: a clipped HALF-row is never legible, so the well's height must be a
// whole number of row pitches. This component measures its OWN untrimmed height,
// floors it to a whole row count (after reserving a fixed bottom band for the
// caller's "there is more below" hint line), and raises its bottom edge by the
// remainder. Clipping then always lands in the GAP between rows, never across a
// label. Rows keep their full kit height, so the MinTouchPx floor is untouched -
// this only ever removes DEAD space, never shrinks a row.
//
// [ExecuteAlways] because the headless UI capture (UICaptureLaunch.CaptureHelpMenu)
// builds the modal in EDIT mode and renders it at two aspects - the snap has to
// re-run on every dimension change there too, or the capture keeps shipping the
// half row this component exists to kill.
// =============================================================================

using UnityEngine;

namespace DeNelle.HUD
{
    /// <summary>
    /// Trims a stretch-anchored scroll-well RectTransform so its height is an exact
    /// whole number of row pitches (row height + gap), keeping a fixed bottom band
    /// free. Layout only - it never touches content, rows or state.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScrollWellRowSnap : MonoBehaviour
    {
        /// <summary>Fixed row height in canvas units (the kit CTA height - never a fraction).</summary>
        public float rowHeightPx = 132f;

        /// <summary>Fixed gap between rows in canvas units (the kit column spacing).</summary>
        public float rowGapPx = 18f;

        /// <summary>Fixed band under the well the caller keeps free (0 when no hint is shown).</summary>
        public float reserveBottomPx;

        /// <summary>Whole rows the well can show after the last snap (0 until measured).</summary>
        [System.NonSerialized] public int VisibleRows;

        private RectTransform _rt;
        private float _lastFullHeight = -1f;

        private void OnEnable()
        {
            _rt = transform as RectTransform;
            Snap(true);
        }

        private void OnRectTransformDimensionsChange()
        {
            // Re-entrant by design: Snap writes offsetMin, which fires this again. The
            // measured FULL height is trim-invariant, so the second pass short-circuits
            // on the _lastFullHeight check below and the recursion stops at depth 1.
            Snap(false);
        }

        /// <summary>
        /// Floor the well's height to whole rows. <paramref name="force"/> re-measures even
        /// when the parent height has not moved (used after the caller changes the reserve).
        /// Safe to call before layout has resolved - it no-ops on a zero-height rect.
        /// </summary>
        public void Snap(bool force)
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_rt == null) return;

            float pitch = rowHeightPx + rowGapPx;
            if (pitch <= 1f) return;

            // offsetMin.y is OUR trim, so the untrimmed height is rect.height + offsetMin.y.
            float trimNow = _rt.offsetMin.y;
            float full = _rt.rect.height + trimNow;
            if (full <= 1f) return;                                  // layout not resolved yet
            if (!force && Mathf.Abs(full - _lastFullHeight) < 0.5f) return;
            _lastFullHeight = full;

            float usable = full - Mathf.Max(0f, reserveBottomPx);
            int rows = Mathf.FloorToInt((usable + rowGapPx) / pitch);
            if (rows < 1) rows = 1;                                  // never collapse to nothing

            float wanted = rows * pitch - rowGapPx;
            if (wanted > full) wanted = full;                        // degenerate well: show what we have
            float trim = full - wanted;
            if (trim < 0f) trim = 0f;

            if (Mathf.Abs(trim - trimNow) > 0.5f)
                _rt.offsetMin = new Vector2(_rt.offsetMin.x, trim);

            VisibleRows = rows;
        }
    }
}
