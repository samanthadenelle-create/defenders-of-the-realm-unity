// =============================================================================
// StarRatingRow — SHARED procedural star-rating row (publisher polish 2026-07-02).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHY THIS EXISTS: the ★ (U+2605) text glyph is NOT in any SDF font shipped in
// this project (verified 2026-07-02 by scanning every *SDF*.asset for
// m_Unicode: 9733 — zero hits, LiberationSans/Titillium/Acme included), so every
// "★★★" TMP label renders as tofu boxes in a build. EndStateView already solved
// this with three procedural gold diamonds (rotated Image quads — deliberately
// sprite-free, see EndStateView.BuildStarRow); this helper extracts that exact
// pattern so other screens (RaidSelectionScreen / RaidDeployScreen) reuse it
// instead of re-tofuing. Kept OUT of ElarionUiKit by direction (kit is frozen to
// another lane); KIT-PROMOTION CANDIDATE once the kit reopens.
//
// NOTE for VM-layer star markers (e.g. BuildingUpgradeVM signature perks): a VM
// emits STRINGS, not Images — there use a font-safe ASCII marker ("* "), never ★.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Builds a horizontal row of procedural "star" diamonds (45°-rotated gold
    /// Image quads — EndStateView's tofu-proof pattern) inside a normalized
    /// anchor box of <paramref name="parent"/>. Filled stars use the Obsidian
    /// gold trim; unfilled are faint white, matching EndStateView.
    /// </summary>
    public static class StarRatingRow
    {
        /// <summary>Same dim tint EndStateView uses for an unearned star.</summary>
        private static readonly Color DimStar = new Color(1f, 1f, 1f, 0.14f);

        /// <param name="parent">Transform the row is parented under.</param>
        /// <param name="filled">How many stars render gold (clamped to total).</param>
        /// <param name="total">How many diamonds to draw.</param>
        /// <param name="x0">Anchor box within parent (normalized).</param>
        /// <param name="sizePx">Diamond edge in px (EndStateView uses 26).</param>
        /// <returns>The row GameObject (raycast-transparent, decorative only).</returns>
        public static GameObject Build(Transform parent, int filled, int total,
                                       float x0, float y0, float x1, float y1,
                                       float sizePx = 14f)
        {
            var rowGo = new GameObject("Stars", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(x0, y0);
            rowRt.anchorMax = new Vector2(x1, y1);
            rowRt.offsetMin = Vector2.zero;
            rowRt.offsetMax = Vector2.zero;

            for (int i = 0; i < total; i++)
            {
                var go = new GameObject("Star" + i, typeof(Image));
                go.transform.SetParent(rowRt, false);
                var img = go.GetComponent<Image>();
                img.color = i < filled ? ElarionUiKit.ObsidianTrim : DimStar;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                float cx = (i + 0.5f) / total;               // even spread across the box
                rt.anchorMin = new Vector2(cx, 0.5f);
                rt.anchorMax = new Vector2(cx, 0.5f);
                rt.sizeDelta = new Vector2(sizePx, sizePx);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // diamond
            }
            return rowGo;
        }
    }
}
