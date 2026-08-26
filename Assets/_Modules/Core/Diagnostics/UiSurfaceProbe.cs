// =============================================================================
// UiSurfaceProbe — MEASURED visibility probe for a UI surface (WO-976)
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (CLAUDE.md §12, WO-976, docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md):
//   AddressableUIManager used to emit "panelSettings=ok canvas=ok => hasSurface=True".
//   Both halves were non-null reference checks, so a panel that was 0x0, fully
//   transparent, entirely offscreen, or buried behind a higher-sorted opaque surface
//   printed GREEN and SUPPRESSED the Fail underneath it. A trace that cannot fail is
//   worse than no trace: it steers the next reader away from the broken thing.
//
// WHAT THIS MEASURES (values a run can contradict):
//   * resolved rect, in PIXELS, read AFTER layout has settled  — not the authored value
//   * resolved opacity (UI Toolkit) / CanvasGroup alpha chain (uGUI)
//   * sorting order, and whether a higher-sorted surface COVERS this one
//   * intersection with the viewport
//
// FOUR FAILURE CLASSES, NAMED SEPARATELY — they are four different bugs with four
// different fixes, so they must never collapse into one "panel not visible" line:
//   ZERO_SIZE   — resolved rect below the 8x8 px floor
//   TRANSPARENT — resolved opacity/alpha below 0.05
//   OFFSCREEN   — no intersection with the viewport
//   BEHIND      — a higher-sorted surface of the same mechanism covers this rect
//
// NOT MEASURABLE => NAMED SKIP, NEVER A SILENT PASS. In batchmode there is no layout
// or render pass, so every measurement would read 0 and the probe would emit spurious
// failures — and the next person "fixes" that by weakening the check, straight back to
// a hollow line. So the skip is explicit, named, logged, and reported as SKIPPED (never
// as a pass).
//
// SHARED HELPER (WO-976 note): this is deliberately reusable Core surface. WO-952's
// `MeasureEndStateFit` in Assets/Editor/UICaptureLaunch.cs keeps its own rect math
// (different mechanism — editor capture vs runtime panel check, and a different lane
// owns that file). It should later be RE-POINTED at MeasureRectPx/Viewport here rather
// than maintaining a second copy of this arithmetic.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Runtime MEASURED visibility probe for a UI surface (UI Toolkit UIDocument or uGUI Canvas).
    /// Produces values that can FAIL — see <see cref="UiSurfaceMeasure"/>.
    /// </summary>
    public static class UiSurfaceProbe
    {
        /// <summary>Minimum resolved edge, in px, for a surface to count as visible.</summary>
        public const float MinEdgePx = 8f;

        /// <summary>Minimum resolved opacity/alpha for a surface to count as visible.</summary>
        public const float MinOpacity = 0.05f;

        /// <summary>A coverer at or above this opacity is treated as opaque enough to hide what it covers.</summary>
        public const float OpaqueCovererOpacity = 0.95f;

        /// <summary>Which draw mechanism a measurement came from.</summary>
        public enum SurfaceKind { None = 0, UiToolkit = 1, UGui = 2 }

        /// <summary>
        /// One measured surface. <see cref="Measurable"/> false means the probe could NOT
        /// evaluate it (batchmode, no layout yet, no surface) — that is a NAMED SKIP carried
        /// in <see cref="SkipReason"/>, never a pass.
        /// </summary>
        public struct UiSurfaceMeasure
        {
            public bool       Measurable;
            public string     SkipReason;
            public SurfaceKind Kind;

            public Rect  RectPx;        // resolved rect in px, viewport-space
            public Rect  ViewportPx;    // the surface's viewport, in px
            public float Opacity;       // resolved opacity (UIT) / CanvasGroup alpha chain (uGUI)
            public float SortingOrder;  // UIDocument.sortingOrder / Canvas.sortingOrder

            // Occlusion: the highest-sorted same-mechanism surface that CONTAINS RectPx.
            public bool   Covered;
            public string CovererName;
            public float  CovererSortingOrder;
            public float  CovererOpacity;

            public float WidthPx  => RectPx.width;
            public float HeightPx => RectPx.height;

            public bool ZeroSize   => RectPx.width < MinEdgePx || RectPx.height < MinEdgePx;
            public bool Transparent => Opacity < MinOpacity;
            public bool Offscreen  => !RectPx.Overlaps(ViewportPx);
            public bool BehindOpaque => Covered && CovererOpacity >= OpaqueCovererOpacity;

            /// <summary>True only when the surface measured AND cleared every visibility floor.</summary>
            public bool Visible => Measurable && !ZeroSize && !Transparent && !Offscreen && !BehindOpaque;

            /// <summary>Compact, greppable rendering of every measured value.</summary>
            public string Describe()
            {
                if (!Measurable) return $"kind={Kind} MEASURE_SKIPPED({SkipReason})";
                return $"kind={Kind} rect={RectPx.width:0}x{RectPx.height:0}px @({RectPx.x:0},{RectPx.y:0}) " +
                       $"viewport={ViewportPx.width:0}x{ViewportPx.height:0} opacity={Opacity:0.00} " +
                       $"sortingOrder={SortingOrder:0.#}" +
                       (Covered
                            ? $" coveredBy='{CovererName}'(sort={CovererSortingOrder:0.#},opacity={CovererOpacity:0.00})"
                            : " coveredBy=<none>");
            }
        }

        // ── Named skip: environments where a measurement is structurally impossible ──────

        /// <summary>
        /// True when NO measurement is possible in this process — batchmode has no layout or
        /// render pass, so every rect reads 0 and every alpha reads whatever was authored.
        /// MANDATORY: callers must emit a NAMED skip on this, never a silent pass and never a
        /// weakened check. See the header.
        /// </summary>
        public static bool IsUnmeasurableEnvironment(out string reason)
        {
            if (Application.isBatchMode)
            {
                reason = "batchmode — no layout/render pass runs, so every rect measures 0 and every " +
                         "measurement would be a spurious failure";
                return true;
            }
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                reason = $"no viewport (Screen={Screen.width}x{Screen.height})";
                return true;
            }
            reason = null;
            return false;
        }

        // ── Measurement ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Measures the first usable draw surface under <paramref name="go"/>. Never throws —
        /// a throw becomes an unmeasurable result with the exception named in SkipReason, so a
        /// broken probe can never masquerade as a passing panel.
        /// </summary>
        public static UiSurfaceMeasure Measure(GameObject go)
        {
            var m = new UiSurfaceMeasure { Measurable = false, Kind = SurfaceKind.None };

            if (go == null) { m.SkipReason = "instance is null"; return m; }
            if (!go.activeInHierarchy) { m.SkipReason = "root is INACTIVE in hierarchy"; return m; }
            if (IsUnmeasurableEnvironment(out string envReason)) { m.SkipReason = envReason; return m; }

            try
            {
                UiSurfaceMeasure uit = default;
                bool triedUit = false;

                var doc = go.GetComponentInChildren<UIDocument>(false);
                if (doc != null && doc.panelSettings != null)
                {
                    triedUit = true;
                    uit = MeasureUiToolkit(doc);
                    if (uit.Measurable) return uit;
                }

                // A UIDocument that could not be measured must NOT block the uGUI path — a prefab can
                // carry both, and returning the UIT skip early would hide a measurable Canvas.
                var canvas = go.GetComponentInChildren<Canvas>(false);
                if (canvas != null)
                {
                    var ugui = MeasureUGui(go, canvas);
                    if (ugui.Measurable || !triedUit) return ugui;
                    return uit;   // neither measured — report the UIT skip, which is the richer reason
                }

                if (triedUit) return uit;

                m.SkipReason = "no ACTIVE UIDocument(+PanelSettings) and no ACTIVE Canvas under the root";
                return m;
            }
            catch (System.Exception ex)
            {
                m.Measurable = false;
                m.SkipReason = $"probe THREW: {ex.GetType().Name}: {ex.Message}";
                return m;
            }
        }

        private static UiSurfaceMeasure MeasureUiToolkit(UIDocument doc)
        {
            var m = new UiSurfaceMeasure { Kind = SurfaceKind.UiToolkit, Measurable = false };

            VisualElement root = doc.rootVisualElement;
            if (root == null) { m.SkipReason = "UIDocument.rootVisualElement is null (panel not attached yet)"; return m; }
            if (root.panel == null) { m.SkipReason = "rootVisualElement has no panel (not attached yet)"; return m; }

            m.RectPx       = root.worldBound;
            m.ViewportPx   = root.panel.visualTree != null ? root.panel.visualTree.worldBound : new Rect(0, 0, Screen.width, Screen.height);
            m.SortingOrder = doc.sortingOrder;

            // Resolved opacity is MULTIPLICATIVE down the tree — a transparent ancestor hides an
            // opaque child, so walk the chain rather than reading only the root.
            float opacity = 1f;
            for (var ve = root; ve != null; ve = ve.parent)
            {
                var rs = ve.resolvedStyle;
                if (rs.display == DisplayStyle.None) { opacity = 0f; break; }
                if (ve.resolvedStyle.visibility == Visibility.Hidden) { opacity = 0f; break; }
                opacity *= Mathf.Clamp01(rs.opacity);
            }
            m.Opacity = opacity;

            // NaN rects happen before the first layout pass — that is a SKIP, not a failure.
            if (float.IsNaN(m.RectPx.width) || float.IsNaN(m.RectPx.height))
            {
                m.SkipReason = "worldBound is NaN (layout has not run yet)";
                return m;
            }

            FindCoverer(ref m, doc);
            m.Measurable = true;
            return m;
        }

        /// <summary>
        /// WO-1221: measure a RectTransform that is a CHILD of somebody else's canvas — a rail,
        /// a row, a band. <see cref="Measure(GameObject)"/> requires a Canvas or UIDocument UNDER
        /// the root, which is true of Addressable panel prefabs and false of every code-built HUD
        /// sub-surface; those would otherwise always come back as the named skip "no ACTIVE
        /// UIDocument … and no ACTIVE Canvas", i.e. never falsifiable. The canvas is resolved from
        /// the ANCESTORS instead, and the arithmetic is the same shared core — do not fork it.
        /// </summary>
        public static UiSurfaceMeasure MeasureRect(RectTransform rt)
        {
            var m = new UiSurfaceMeasure { Kind = SurfaceKind.UGui, Measurable = false };

            if (rt == null) { m.SkipReason = "RectTransform is null"; return m; }
            if (!rt.gameObject.activeInHierarchy)
            {
                m.SkipReason = "rect is INACTIVE in hierarchy (an ancestor or the object itself is off)";
                return m;
            }
            if (IsUnmeasurableEnvironment(out string envReason)) { m.SkipReason = envReason; return m; }

            try
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                if (canvas == null) { m.SkipReason = "no Canvas ancestor — the rect is not on any draw surface"; return m; }
                return MeasureRectCore(rt, rt.gameObject, canvas.rootCanvas != null ? canvas.rootCanvas : canvas);
            }
            catch (System.Exception ex)
            {
                m.Measurable = false;
                m.SkipReason = $"probe THREW: {ex.GetType().Name}: {ex.Message}";
                return m;
            }
        }

        private static UiSurfaceMeasure MeasureUGui(GameObject go, Canvas canvas)
        {
            var m = new UiSurfaceMeasure { Kind = SurfaceKind.UGui, Measurable = false };

            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = canvas.GetComponent<RectTransform>();
            if (rt == null) { m.SkipReason = "Canvas has no RectTransform"; return m; }

            // Alpha stays sourced from `go` (not rt.gameObject) so the fallback-to-canvas-rect
            // branch above keeps the exact behaviour WO-976's callers were written against.
            return MeasureRectCore(rt, go, canvas.rootCanvas != null ? canvas.rootCanvas : canvas);
        }

        /// <summary>The one copy of the uGUI measurement. Both entry points route here.</summary>
        private static UiSurfaceMeasure MeasureRectCore(RectTransform rt, GameObject alphaSource, Canvas rootCanvas)
        {
            var m = new UiSurfaceMeasure { Kind = SurfaceKind.UGui, Measurable = false };

            m.SortingOrder = rootCanvas.sortingOrder;
            m.ViewportPx   = new Rect(0, 0, Screen.width, Screen.height);
            m.RectPx       = ScreenRectOf(rt, rootCanvas);
            m.Opacity      = EffectiveAlpha(alphaSource, rootCanvas);

            FindCoverer(ref m, rootCanvas);
            m.Measurable = true;
            return m;
        }

        /// <summary>
        /// A RectTransform's resolved rect in SCREEN px. Public so the editor-capture lane
        /// (WO-952 MeasureEndStateFit) can re-point at this instead of keeping its own copy.
        /// </summary>
        public static Rect ScreenRectOf(RectTransform rt, Canvas rootCanvas)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Camera cam = null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>Alpha after the whole CanvasGroup chain (any group with alpha 0 hides the subtree).</summary>
        private static float EffectiveAlpha(GameObject go, Canvas rootCanvas)
        {
            if (rootCanvas != null && !rootCanvas.enabled) return 0f;

            // GetComponentsInParent INCLUDES this GameObject's own CanvasGroup, so the chain is
            // complete here — multiplying a "self" pass on top would square the root group's alpha
            // and report a 0.5 panel as 0.25.
            float a = 1f;
            var groups = go.GetComponentsInParent<CanvasGroup>(true);
            if (groups != null)
                for (int i = 0; i < groups.Length; i++)
                    if (groups[i] != null) a *= Mathf.Clamp01(groups[i].alpha);

            return a;
        }

        // ── Occlusion (same mechanism only) ─────────────────────────────────────────────
        // HONEST SCOPE: this finds the highest-sorted surface of the SAME mechanism whose
        // measured rect CONTAINS ours. It does not do per-pixel coverage — a coverer with a
        // hole in it would still register. That is why only an OPAQUE coverer (>= 0.95) is
        // treated as a failure; a translucent one is reported and left to the reader.
        // Cross-mechanism sorting (UIT panel vs uGUI canvas) is NOT comparable, so it is
        // deliberately not compared.

        private static void FindCoverer(ref UiSurfaceMeasure m, UIDocument self)
        {
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            if (docs == null) return;

            for (int i = 0; i < docs.Length; i++)
            {
                var d = docs[i];
                if (d == null || d == self || !d.isActiveAndEnabled) continue;
                if (d.sortingOrder <= m.SortingOrder) continue;

                var r = d.rootVisualElement;
                if (r == null || r.panel == null) continue;

                Rect other = r.worldBound;
                if (float.IsNaN(other.width) || !Contains(other, m.RectPx)) continue;
                if (d.sortingOrder <= m.CovererSortingOrder && m.Covered) continue;

                float op = 1f;
                for (var ve = r; ve != null; ve = ve.parent) op *= Mathf.Clamp01(ve.resolvedStyle.opacity);

                m.Covered              = true;
                m.CovererName          = d.gameObject.name;
                m.CovererSortingOrder  = d.sortingOrder;
                m.CovererOpacity       = op;
            }
        }

        private static void FindCoverer(ref UiSurfaceMeasure m, Canvas self)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases == null) return;

            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null || c == self || !c.isActiveAndEnabled) continue;
                if (c.rootCanvas != c) continue;                       // roots only — children inherit
                if (c.sortingOrder <= m.SortingOrder) continue;

                var crt = c.GetComponent<RectTransform>();
                if (crt == null) continue;

                Rect other = ScreenRectOf(crt, c);
                if (!Contains(other, m.RectPx)) continue;
                if (m.Covered && c.sortingOrder <= m.CovererSortingOrder) continue;

                m.Covered             = true;
                m.CovererName         = c.gameObject.name;
                m.CovererSortingOrder = c.sortingOrder;
                m.CovererOpacity      = EffectiveAlpha(c.gameObject, c);
            }
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return outer.xMin <= inner.xMin && outer.yMin <= inner.yMin &&
                   outer.xMax >= inner.xMax && outer.yMax >= inner.yMax &&
                   outer.width > 0f && outer.height > 0f;
        }

        // ── Reporting ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Emits the measured verdict for <paramref name="label"/> on the given FlowTrace system.
        /// Each failure class prints its OWN Fail line with its own name and its own consequence —
        /// "panel not visible" would be four bugs wearing one hat. An unmeasurable surface emits a
        /// NAMED SKIP (Warn), never a pass. Returns true only when the surface measured AND cleared
        /// every floor.
        /// </summary>
        public static bool Report(string system, string label, in UiSurfaceMeasure m)
        {
            if (!m.Measurable)
            {
                FlowTrace.Warn(system,
                    $"{label}: MEASURED VISIBILITY VERIFY **SKIPPED** — {m.SkipReason}. " +
                    "Named skip, not a pass: nothing here asserts the panel is visible.");
                return false;
            }

            bool ok = true;

            if (m.ZeroSize)
            {
                ok = false;
                FlowTrace.Fail(system,
                    $"{label}: SURFACE_ZERO_SIZE — resolved rect {m.RectPx.width:0}x{m.RectPx.height:0}px is below the " +
                    $"{MinEdgePx:0}x{MinEdgePx:0} px visibility floor. The panel occupies no space; the player sees nothing. " +
                    $"({m.Describe()})");
            }

            if (m.Transparent)
            {
                ok = false;
                FlowTrace.Fail(system,
                    $"{label}: SURFACE_TRANSPARENT — resolved opacity {m.Opacity:0.00} is below the {MinOpacity:0.00} floor " +
                    $"(display/visibility/opacity or a CanvasGroup alpha in the chain). The panel is laid out but invisible. " +
                    $"({m.Describe()})");
            }

            if (m.Offscreen)
            {
                ok = false;
                FlowTrace.Fail(system,
                    $"{label}: SURFACE_OFFSCREEN — rect @({m.RectPx.x:0},{m.RectPx.y:0}) {m.RectPx.width:0}x{m.RectPx.height:0}px " +
                    $"does not intersect the {m.ViewportPx.width:0}x{m.ViewportPx.height:0} viewport. The panel renders outside the screen. " +
                    $"({m.Describe()})");
            }

            if (m.BehindOpaque)
            {
                ok = false;
                FlowTrace.Fail(system,
                    $"{label}: SURFACE_BEHIND — fully covered by '{m.CovererName}' at sortingOrder {m.CovererSortingOrder:0.#} > " +
                    $"{m.SortingOrder:0.#} with opacity {m.CovererOpacity:0.00}. The panel draws, then something opaque draws over it. " +
                    $"({m.Describe()})");
            }
            else if (m.Covered)
            {
                FlowTrace.Warn(system,
                    $"{label}: SURFACE_BEHIND_TRANSLUCENT — covered by '{m.CovererName}' (sort={m.CovererSortingOrder:0.#} > " +
                    $"{m.SortingOrder:0.#}) whose opacity is only {m.CovererOpacity:0.00}. Reported, not failed: coverage is " +
                    $"rect-containment, not per-pixel, so a translucent coverer may still be readable. ({m.Describe()})");
            }

            if (ok)
            {
                FlowTrace.Step(system,
                    $"{label}: MEASURED VISIBLE — {m.Describe()} (cleared: >= {MinEdgePx:0}x{MinEdgePx:0}px, " +
                    $"opacity >= {MinOpacity:0.00}, intersects viewport, not covered by an opaque higher-sorted surface).");
            }

            return ok;
        }
    }
}
