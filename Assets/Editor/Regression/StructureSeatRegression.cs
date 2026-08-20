// =============================================================================
// StructureSeatRegression — a seated visual's BOUNDS BOTTOM lands on the ground plane.
// Marker: STRUCTURE_SEAT_OK / STRUCTURE_SEAT_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.   Standalone entry: RunAll().
//
// WHY THIS SUITE EXISTS, stated as the defect it pins:
//
// On 2026-08-20 a device capture was read as proof that VisualFactory.SeatOnGround was
// "leaving objects 2 metres in the air", because the trace line said:
//
//     [Flow:Xform] 'Forge' (entry='forge') after Fit+SeatOnGround: pos=(0.00, 2.00, 3.13)
//
// It was not floating. That line prints transform.localPosition — the model PIVOT relative
// to its host — and the very next line in the same capture reports the fitted world bounds:
//
//     [Flow:VisualFactory] skinned 'Forge' ... boundsSize=(4.57, 4.00, 6.29)
//
// A 4.00 m body whose pivot sits at its CENTRE must be lifted exactly +2.00 to put its
// BOTTOM on the host's y. local y = +2.00 was the correct seat, printed correctly.
//
// So the property worth pinning is not "localPosition.y is small" — that assertion would
// have FAILED on correct code and been "fixed" into a real defect. The property is:
//
//     ⛔ AFTER A SEAT, world bounds.min.y == the ground plane, WHATEVER the pivot is.
//
// This suite asserts that in BOTH directions, because a one-directional oracle proves
// nothing: a checker that only ever says "seated" passes a broken game.
//   PASS direction: an off-pivot body, fitted and seated, lands its bottom on the plane.
//   FAIL direction: the same body lifted 2 m off the plane is REPORTED as not seated.
// Plus the misread itself is pinned as a case: a centre-pivoted body with localPosition.y
// near half its height is SEATED, not floating.
//
// The oracle used is VisualFactory.IsSeatedOnGround — the SAME method the runtime seat
// calls to self-verify. A second copy of the epsilon/compare here is how a gate and the
// game come to disagree, so there is deliberately only one.
// =============================================================================

using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Runtime oracle: <see cref="VisualFactory"/> seats a skinned body's world-bounds BOTTOM on
    /// the host's ground plane, regardless of where the model's pivot sits. Returns true (summary)
    /// / false (detail); never throws.
    /// </summary>
    public static class StructureSeatRegression
    {
        // The ground plane is deliberately NOT y=0. A seat implementation that ignored the host and
        // simply zeroed y would pass at y=0 and ship a whole town sunk into a raised plaza.
        private const float GroundY = 3.0f;

        // Fit target, matching the town cadence (StructureFactory.YHeightVariable is 4 m).
        private const float FitHeightMetres = 4f;

        public static bool Run(out string reason)
        {
            var log = new StringBuilder();
            log.AppendLine("[StructureSeatRegression] seat oracle: world bounds bottom lands on the ground plane.");

            GameObject host = null, model = null, visual = null;
            reason = null;

            try
            {
                host = new GameObject("SeatRegression_Host");
                host.hideFlags = HideFlags.HideAndDontSave;
                host.transform.position = new Vector3(7f, GroundY, -11f);

                // The probe REPRODUCES THE MISREAD MODEL EXACTLY: a body whose pivot sits at its
                // VERTICAL CENTRE and off-centre in Z — the shape of the 'Forge' in the capture
                // (boundsSize=(4.57, 4.00, 6.29), seated at localPosition=(0.00, 2.00, 3.13)).
                // Fitted to 4 m, such a body MUST end at localPosition.y = +2.00 to put its bottom
                // on the plane. Case 2 below asserts that exact number is CORRECT, so the 2026-08-20
                // misdiagnosis cannot be re-committed as a "fix".
                model = new GameObject("SeatRegression_Model");
                model.hideFlags = HideFlags.HideAndDontSave;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "CentrePivotCube";
                cube.transform.SetParent(model.transform, false);
                cube.transform.localPosition = new Vector3(0f, 0f, 0.8f);

                var opts = new SkinOptions
                {
                    FitHeight    = FitHeightMetres,
                    SeatOnGround = true,
                };

                visual = VisualFactory.Skin(host.transform, model, opts);
                if (visual == null)
                {
                    reason = "STRUCTURE_SEAT_FAIL: VisualFactory.Skin returned null for the probe model " +
                             "— cannot evaluate the seat (Skin treated a plain cube as render-broken).";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }

                // ── CASE 1 (PASS direction): the seat put the BOTTOM on the plane ────────────
                bool seated = VisualFactory.IsSeatedOnGround(visual, GroundY, out float bottomY);
                log.AppendLine($"  case1 seated-body: bounds bottom y={bottomY:F3} vs ground y={GroundY:F3} " +
                               $"(tolerance {VisualFactory.SeatEpsilonMetres:F2} m) -> seated={seated}");
                if (!seated)
                {
                    reason = $"STRUCTURE_SEAT_FAIL: after Fit+SeatOnGround the body's bounds bottom is " +
                             $"y={bottomY:F3}, but the ground plane is y={GroundY:F3} (off by " +
                             $"{bottomY - GroundY:F3} m). A seated structure must touch the ground.";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }

                // ── CASE 2 (the 2026-08-20 misread, pinned) ──────────────────────────────────
                // A centre-pivoted body fitted to 4 m seats at localPosition.y = +2.00 — HALF ITS
                // HEIGHT — and that is the CORRECT seat, not a 2 m float. Assert the number, so a
                // future session cannot "fix" the trace line's 2.00 down to 0.00 and sink the town.
                float localY = visual.transform.localPosition.y;
                float expectedPivotLift = FitHeightMetres * 0.5f;
                log.AppendLine($"  case2 pivot-vs-bottom: localPosition.y={localY:F3} (expected " +
                               $"{expectedPivotLift:F2} = half the fitted height) while the bottom rests on the " +
                               "plane — the [Flow:Xform] line prints THIS number, not the bottom.");
                if (Mathf.Abs(localY - expectedPivotLift) > VisualFactory.SeatEpsilonMetres)
                {
                    reason = $"STRUCTURE_SEAT_FAIL: a centre-pivoted body fitted to {FitHeightMetres:F2} m seated " +
                             $"with localPosition.y={localY:F3}; it must be {expectedPivotLift:F2} (half the " +
                             "height) for its BOTTOM to touch the plane. Forcing that pivot value to ~0 is the " +
                             "2026-08-20 misdiagnosis — it would bury every centre-pivoted structure to its waist.";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }

                // ── CASE 3 (FAIL direction): the KNOWN-BAD state must be REPORTED as unseated ──
                // Lift the seated body 2 m. This is the real defect shape (a post-skin scale or
                // offset applied after the seat). The oracle must say NO.
                const float lift = 2f;
                visual.transform.position += new Vector3(0f, lift, 0f);
                bool stillSeated = VisualFactory.IsSeatedOnGround(visual, GroundY, out float liftedBottomY);
                log.AppendLine($"  case3 lifted-by-{lift:F1}m: bounds bottom y={liftedBottomY:F3} -> seated={stillSeated} (want False)");
                if (stillSeated)
                {
                    reason = $"STRUCTURE_SEAT_FAIL: a body lifted {lift:F1} m off the ground plane " +
                             $"(bottom y={liftedBottomY:F3} vs ground y={GroundY:F3}) was still reported as " +
                             "SEATED. The oracle cannot detect a float, so it proves nothing about the game.";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }
                if (Mathf.Abs((liftedBottomY - GroundY) - lift) > 0.05f)
                {
                    reason = $"STRUCTURE_SEAT_FAIL: the reported float height is wrong — bottom y={liftedBottomY:F3} " +
                             $"is {liftedBottomY - GroundY:F3} m above ground y={GroundY:F3} after a {lift:F1} m lift. " +
                             "The offending Y in the warning would be misleading.";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }

                // ── CASE 4: an UNMEASURABLE body is a fail, never a silent pass ───────────────
                var empty = new GameObject("SeatRegression_NoRenderer");
                empty.hideFlags = HideFlags.HideAndDontSave;
                empty.transform.position = new Vector3(0f, GroundY, 0f);
                bool emptySeated = VisualFactory.IsSeatedOnGround(empty, GroundY, out float emptyBottom);
                Object.DestroyImmediate(empty);
                log.AppendLine($"  case4 no-renderer body: seated={emptySeated} bottom={emptyBottom} (want False/NaN)");
                if (emptySeated)
                {
                    reason = "STRUCTURE_SEAT_FAIL: a body with NO measurable renderer bounds was reported as " +
                             "SEATED. An object whose bottom cannot be measured has not been proven seated — " +
                             "reporting it as seated is exactly the silent failure §12 forbids.";
                    log.AppendLine(reason);
                    Debug.LogError(log.ToString());
                    return false;
                }

                reason = "STRUCTURE_SEAT_OK: seat lands the world-bounds bottom on the ground plane " +
                         "(off-pivot body), a 2 m float is detected, and an unmeasurable body is not " +
                         "passed off as seated.";
                log.AppendLine(reason);
                Debug.Log(log.ToString());
                return true;
            }
            catch (System.Exception ex)
            {
                reason = "STRUCTURE_SEAT_FAIL: threw — " + ex.Message;
                log.AppendLine(reason);
                Debug.LogError(log.ToString());
                return false;
            }
            finally
            {
                // Probes are HideAndDontSave; tear them down whatever happened so a failing run
                // never leaves junk in the open scene.
                if (visual != null) Object.DestroyImmediate(visual);
                if (model  != null) Object.DestroyImmediate(model);
                if (host   != null) Object.DestroyImmediate(host);
            }
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
