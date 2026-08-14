// =============================================================================
// UiSurfaceProbeRegression — WO-976: the `hasSurface` false green stays dead.
// Marker: UI_SURFACE_PROBE_OK
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DeNelle.Editor.DataRegression.RunAll.
//
// WHY THIS SUITE EXISTS — and why it is written the way it is:
// WO-976 fixed a trace that could not fail (`panelSettings=ok canvas=ok => hasSurface=True`
// printed green for a 0x0 / transparent / offscreen / buried panel). The WO's own acceptance
// criterion is the sharp one: *"a fix to a false-green that is not itself falsified is just a
// new false green."* So this suite does not check that the probe EXISTS. It FALSIFIES it:
// it constructs a deliberately-broken surface measurement of each of the four classes and
// asserts the probe reports that class and refuses to call it visible.
//
// It is deliberately split in two halves, because they prove different things:
//   HALF A — BEHAVIOURAL. Drives UiSurfaceProbe's classifier with synthetic measures. This is
//            the half that proves ZERO_SIZE, TRANSPARENT, OFFSCREEN and BEHIND can each FIRE,
//            independently, and that a healthy surface still passes (so the check is not
//            trivially always-red either). Zero-size a surface -> Visible==false. That is the
//            acceptance criterion, executed.
//   HALF B — SOURCE-LINT. Pins the AddressableUIManager call site: the retired `hasSurface=`
//            token must not come back, the measured verify must still be invoked, and the
//            batchmode NAMED SKIP must still be there. Half A cannot see a future edit that
//            deletes the call; Half B can.
//
// HONEST SCOPE (stated because WO-976 asked for it plainly): Half A proves the CLASSIFIER and
// the REPORTING can fail. It does NOT prove Unity's own measurement plumbing (worldBound /
// GetWorldCorners / resolvedStyle.opacity) returns what we think in a live player — an editor
// regression has no layout pass. That half is only provable by a played/captured run, and is
// listed as unproven in the WO-976 report rather than claimed here.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    /// <summary>
    /// Falsifies <see cref="UiSurfaceProbe"/>: each of the four failure classes must be able to
    /// fire on its own, and a healthy surface must still pass. Returns true (summary) / false
    /// (detail); never throws.
    /// </summary>
    public static class UiSurfaceProbeRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- UI SURFACE PROBE FALSIFIABILITY (WO-976) ---");

            RunBehavioural(failures, log);
            RunSourceLint(failures, log);

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log + "UI_SURFACE_PROBE_OK");
                return true;
            }

            reason = "ui-surface-probe: " + string.Join("; ", failures);
            Debug.LogError(log + "UI_SURFACE_PROBE_FAIL: " + reason);
            return false;
        }

        // ── HALF A: the four classes must each be able to FIRE ──────────────────────────

        private static UiSurfaceProbe.UiSurfaceMeasure Healthy()
        {
            return new UiSurfaceProbe.UiSurfaceMeasure
            {
                Measurable   = true,
                Kind         = UiSurfaceProbe.SurfaceKind.UGui,
                RectPx       = new Rect(100f, 100f, 400f, 300f),
                ViewportPx   = new Rect(0f, 0f, 1920f, 1080f),
                Opacity      = 1f,
                SortingOrder = 10f,
                Covered      = false,
            };
        }

        private static void RunBehavioural(List<string> failures, StringBuilder log)
        {
            // 0. CONTROL — a healthy surface must PASS. Without this case the suite could be
            //    satisfied by a probe that fails everything, which is its own kind of useless.
            var ok = Healthy();
            if (!ok.Visible)
                failures.Add("CONTROL FAILED: a 400x300px, opacity-1, on-screen, uncovered surface does not " +
                             "report Visible - the probe rejects healthy panels, which would train readers to ignore it");
            else
                log.AppendLine("  [control] healthy 400x300 surface -> Visible=true");

            // 1. ZERO_SIZE — the WO's named acceptance criterion, executed.
            //    Zero-size the surface; it must stop being visible AND must be attributed to
            //    ZeroSize specifically (not to some other class).
            var zero = Healthy();
            zero.RectPx = new Rect(100f, 100f, 0f, 0f);
            if (!zero.ZeroSize)
                failures.Add("ZERO_SIZE cannot fire: a 0x0 rect is not classified ZeroSize");
            if (zero.Visible)
                failures.Add("ZERO_SIZE is a FALSE GREEN: a 0x0 surface still reports Visible - this is exactly " +
                             "the WO-976 defect reintroduced");
            if (zero.Transparent || zero.Offscreen || zero.BehindOpaque)
                failures.Add("ZERO_SIZE bleeds into another class: a 0x0 rect also trips " +
                             $"(transparent={zero.Transparent}, offscreen={zero.Offscreen}, behind={zero.BehindOpaque}) - " +
                             "the four classes must read differently or they are one class wearing four names");

            // 1b. The FLOOR must be a real threshold, not just a zero-check: a 7px edge is
            //     sub-visible and must fail; 8px is the stated floor and must pass.
            var sliver = Healthy();
            sliver.RectPx = new Rect(100f, 100f, UiSurfaceProbe.MinEdgePx - 1f, 300f);
            if (!sliver.ZeroSize)
                failures.Add($"the {UiSurfaceProbe.MinEdgePx:0}px visibility floor is not enforced: a " +
                             $"{UiSurfaceProbe.MinEdgePx - 1f:0}px-wide sliver passes");
            var atFloor = Healthy();
            atFloor.RectPx = new Rect(100f, 100f, UiSurfaceProbe.MinEdgePx, UiSurfaceProbe.MinEdgePx);
            if (atFloor.ZeroSize)
                failures.Add($"the floor is off by one: a surface exactly at {UiSurfaceProbe.MinEdgePx:0}x{UiSurfaceProbe.MinEdgePx:0}px fails");
            log.AppendLine($"  [zero-size] 0x0 -> fail; {UiSurfaceProbe.MinEdgePx - 1f:0}px sliver -> fail; " +
                           $"{UiSurfaceProbe.MinEdgePx:0}px -> pass");

            // 2. TRANSPARENT — fully-transparent and below-floor alpha must both fail, and must
            //    NOT be reported as a sizing problem.
            var clear = Healthy();
            clear.Opacity = 0f;
            if (!clear.Transparent || clear.Visible)
                failures.Add("TRANSPARENT cannot fire: an opacity-0 surface is not classified Transparent / still reports Visible");
            if (clear.ZeroSize || clear.Offscreen)
                failures.Add("TRANSPARENT bleeds into another class: an opacity-0 surface of full size also trips " +
                             $"(zeroSize={clear.ZeroSize}, offscreen={clear.Offscreen})");
            var faint = Healthy();
            faint.Opacity = UiSurfaceProbe.MinOpacity - 0.01f;
            if (!faint.Transparent)
                failures.Add($"the {UiSurfaceProbe.MinOpacity:0.00} opacity floor is not enforced");
            log.AppendLine($"  [transparent] opacity 0 -> fail; {UiSurfaceProbe.MinOpacity - 0.01f:0.00} -> fail; 1.00 -> pass");

            // 3. OFFSCREEN — a full-size, opaque panel positioned outside the viewport must fail
            //    on OFFSCREEN alone. This is the class most likely to be mistaken for "fine".
            var off = Healthy();
            off.RectPx = new Rect(4000f, 4000f, 400f, 300f);
            if (!off.Offscreen || off.Visible)
                failures.Add("OFFSCREEN cannot fire: a panel at (4000,4000) outside a 1920x1080 viewport is not " +
                             "classified Offscreen / still reports Visible");
            if (off.ZeroSize || off.Transparent)
                failures.Add("OFFSCREEN bleeds into another class");
            var partly = Healthy();
            partly.RectPx = new Rect(-200f, 100f, 400f, 300f);   // half off the left edge - still seen
            if (partly.Offscreen)
                failures.Add("OFFSCREEN is too strict: a partially-visible panel is reported fully offscreen, " +
                             "which would spam failures for every edge-anchored HUD element");
            log.AppendLine("  [offscreen] (4000,4000) -> fail; half-off-left -> pass (intersection, not containment)");

            // 4. BEHIND — an opaque higher-sorted coverer fails; a translucent one does NOT
            //    (reported as advisory instead). The split matters: rect-containment is not
            //    per-pixel proof of occlusion, so only the opaque case earns a Fail.
            var buried = Healthy();
            buried.Covered = true; buried.CovererName = "FullscreenScrim";
            buried.CovererSortingOrder = 100f; buried.CovererOpacity = 1f;
            if (!buried.BehindOpaque || buried.Visible)
                failures.Add("BEHIND cannot fire: a surface fully covered by an opaque sortingOrder-100 canvas " +
                             "still reports Visible");
            if (buried.ZeroSize || buried.Transparent || buried.Offscreen)
                failures.Add("BEHIND bleeds into another class");

            var behindGlass = Healthy();
            behindGlass.Covered = true; behindGlass.CovererName = "DimVignette";
            behindGlass.CovererSortingOrder = 100f; behindGlass.CovererOpacity = 0.3f;
            if (behindGlass.BehindOpaque)
                failures.Add("BEHIND over-fires: a 0.30-opacity coverer is treated as opaque occlusion - " +
                             "every panel under a dim vignette would report as buried");
            log.AppendLine("  [behind] opaque coverer -> fail; 0.30 coverer -> advisory only");

            // 5. THE NAMED SKIP is not a pass. An unmeasurable result must never report Visible -
            //    this is the guard against the exact "weaken it until batchmode goes green" move
            //    that turns the fix back into a hollow line.
            var skipped = new UiSurfaceProbe.UiSurfaceMeasure
            {
                Measurable = false,
                SkipReason = "batchmode",
                RectPx     = new Rect(0f, 0f, 400f, 300f),
                ViewportPx = new Rect(0f, 0f, 1920f, 1080f),
                Opacity    = 1f,
            };
            if (skipped.Visible)
                failures.Add("A NAMED SKIP is being counted as a PASS: an unmeasurable surface reports Visible. " +
                             "'Not measured' and 'measured and fine' must never be the same value");
            if (skipped.Describe().IndexOf("SKIP", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("An unmeasurable measurement does not say SKIPPED in its own description - " +
                             "a silent skip reads as coverage");
            log.AppendLine("  [named-skip] unmeasurable -> Visible=false and self-describes as SKIPPED");
        }

        // ── HALF B: the call site must keep using it ─────────────────────────────────────

        private static void RunSourceLint(List<string> failures, StringBuilder log)
        {
            string path = null;
            try { path = Path.Combine(Application.dataPath, "_Modules", "Core", "UI", "AddressableUIManager.cs"); }
            catch { }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                failures.Add("Assets/_Modules/Core/UI/AddressableUIManager.cs not found - the WO-976 call site is gone");
                return;
            }

            string src = ReadOrEmpty(path);
            string code = StripComments(src);

            // The retired token. It is checked against COMMENT-STRIPPED source so the header may
            // keep explaining what `hasSurface` was and why it died.
            if (code.IndexOf("hasSurface", StringComparison.Ordinal) >= 0)
                failures.Add("AddressableUIManager.cs has reintroduced the `hasSurface` token in CODE - " +
                             "WO-976 retired it because two non-null checks were being printed as a visibility claim");

            foreach (var token in new[]
                     {
                         "VerifyRendersMeasured",              // the measured verify still exists
                         "UiSurfaceProbe.IsUnmeasurableEnvironment",  // ...and still names its skip
                         "UiSurfaceProbe.Measure",             // ...and actually measures
                         "UiSurfaceProbe.Report",              // ...and actually reports
                         "surfaceWired",                       // the wiring line kept honest language
                     })
            {
                if (code.IndexOf(token, StringComparison.Ordinal) < 0)
                    failures.Add($"AddressableUIManager.cs no longer contains '{token}' - the WO-976 measured " +
                                 "visibility verify has been removed or bypassed, which restores the false green");
            }

            // The measured verify must stay OFF the caller's critical path (it waits up to 8 frames).
            if (code.IndexOf("VerifyRendersMeasured(go, address).Forget()", StringComparison.Ordinal) < 0)
                failures.Add("the measured verify is no longer fire-and-forget (.Forget()) - it waits up to 8 frames " +
                             "for layout, and awaiting it makes every ShowAsync caller pay that latency");

            // The four classes must remain SEPARATELY named in the probe itself.
            string probePath = null;
            try { probePath = Path.Combine(Application.dataPath, "_Modules", "Core", "Diagnostics", "UiSurfaceProbe.cs"); }
            catch { }
            if (string.IsNullOrEmpty(probePath) || !File.Exists(probePath))
            {
                failures.Add("Assets/_Modules/Core/Diagnostics/UiSurfaceProbe.cs is missing");
            }
            else
            {
                string probe = ReadOrEmpty(probePath);
                foreach (var cls in new[] { "SURFACE_ZERO_SIZE", "SURFACE_TRANSPARENT", "SURFACE_OFFSCREEN", "SURFACE_BEHIND" })
                    if (probe.IndexOf(cls, StringComparison.Ordinal) < 0)
                        failures.Add($"UiSurfaceProbe no longer emits the distinct failure class '{cls}' - the four " +
                                     "classes have collapsed into one message and stopped being separately actionable");
            }

            log.AppendLine("  [source-lint] call site keeps the measured verify, the named skip, and four distinct classes");
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        /// <summary>Blank out comments so the lint tests CODE, not the prose explaining the fix.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    sb.Append(' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
