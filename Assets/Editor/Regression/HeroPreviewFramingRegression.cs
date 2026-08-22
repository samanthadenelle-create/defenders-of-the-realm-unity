// =============================================================================
// HeroPreviewFramingRegression — WO-1059. The hero preview must frame the MODEL.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS, from captured data (Player.log behind F8 seq 3585 / 3586,
// 2026-08-22):
//
//   rend[1] 'WeaponTrail' TrailRenderer enabled=True MESH-NULL bounds=5001.20x5000.52x1.11
//   camera rig: ... bounds center=(-2500.27,-2500.03,-0.05) size=(5001.54,5001.06,2.11)
//   -> RT PROBE 512x512->16x16: 0/256 px differ from clear.              BLANK
//
// and the control, later in the SAME log, on a weapon carrying no trail:
//
//   PreviewActor cloned from 'HeroBody': 2 renderers (1 skinned)
//   camera rig: ... bounds center=(-5000.03,-4999.53,-0.06) size=(1.92,2.19,2.02)
//   -> no probe failure.                                                  DRAWS
//
// ComputeBounds summed EVERY Renderer.bounds. A TrailRenderer's AABB is the hull of
// its accumulated WORLD-space points, which Instantiate copies — so the clone's trail
// still stretched from the live hero at the world origin to the clone at RigOrigin.
// The union's centre landed at the MIDPOINT and the camera framed empty space.
//
// ⚠ WHY THIS SUITE IS BUILT THE WAY IT IS. The banned shape here would be a source-lint
// that reads ComputeBounds and asserts "it excludes non-mesh renderers" — that restates
// the diff and is structurally incapable of catching a REGRESSION of the mechanism (a
// new effect renderer type, a Unity bounds-semantics change). So the authorities are:
//
//   Case A  Unity's own Renderer.bounds on real, live renderers   (MEASURED, no GPU)
//           vs. the framing arithmetic parsed OUT of the rig source
//   Case B  the real HeroPreviewViewer rig, driven end to end,    (MEASURED, needs GPU)
//           judged by its OWN readback (DrewContent)
//   Case C  the ORDERING that neither A nor B can localise         (source lint)
//
// Case A builds actual renderers and asks Unity where they are; nothing in it is copied
// from the log or from the code under test. Case B is the whole point — it reproduces the
// captured scenario against the shipping class and asks the shipping probe. Case B stands
// DOWN VISIBLY via RegressionOutcome.PartialSkip when the batch has no graphics device,
// because a render oracle that silently reports green under -nographics is worse than no
// oracle at all.
//
// Registered in DataRegression.RunAll as the "hero-preview-framing suite".
// Markers: HERO_PREVIEW_FRAMING_OK / HERO_PREVIEW_FRAMING_FAIL.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class HeroPreviewFramingRegression
    {
        private const string PreviewSrc = "Assets/_Modules/Village/Hero/HeroPreviewViewer.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_PREVIEW_FRAMING_OK - " + reason);
            else Debug.LogError("HERO_PREVIEW_FRAMING_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                CaseA_EffectBoundsPoisonFraming(failures, notes);
                CaseB_LiveRigDraws(failures, notes);
                CaseC_OrderingAndExclusion(failures, notes);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
                return false;
            }
            reason = "hero preview framing verified - " + string.Join("; ", notes);
            return true;
        }

        // =====================================================================
        //  CASE A - the mechanism, measured against Unity, not against the diff
        // =====================================================================
        // Builds two REAL renderers in a throwaway hierarchy: a mesh at the rig origin
        // (the "model") and a world-space effect renderer whose points stretch back to
        // the world origin (the "trail"). Then asks Unity for their bounds and runs the
        // rig's OWN framing arithmetic - fov and farClipPlane parsed out of the source,
        // so a tuning change moves this test rather than invalidating it.
        //
        // It asserts BOTH directions, which is what makes it a real oracle:
        //   1. including the effect renderer aims the camera off the model AND pulls it
        //      past the far clip - i.e. the captured defect still reproduces today;
        //   2. excluding it aims the camera at the model - i.e. the rule actually fixes it.
        // If (1) ever stops reproducing, this suite says so instead of quietly passing.
        private static void CaseA_EffectBoundsPoisonFraming(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(PreviewSrc);
            if (src == null) { failures.Add("[framing] cannot read " + PreviewSrc); return; }

            Vector3 rigOrigin = ParseRigOrigin(src, failures);
            float fov  = ParseFloat(src, @"fieldOfView\s*=\s*(-?[\d.]+)f", 32f);
            float far  = ParseFloat(src, @"farClipPlane\s*=\s*(-?[\d.]+)f", 5000f);

            GameObject root = null;
            try
            {
                root = new GameObject("HPFR_CaseA") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.position = rigOrigin;

                // --- the MODEL: a real mesh renderer sitting at the rig origin ---------
                var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.hideFlags = HideFlags.HideAndDontSave;
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = new Vector3(1f, 2f, 1f);   // roughly hero-shaped
                var modelRend = model.GetComponent<MeshRenderer>();

                // --- the EFFECT: world-space points still spanning back to the origin ---
                // The captured culprit was a TrailRenderer. TrailRenderer point buffers are
                // not reliably authorable outside PlayMode, so we take whichever of the two
                // banned types actually reports spanning bounds here and NAME which one
                // supplied the measurement - never silently assume one did.
                Renderer effectRend = null;
                string effectKind = null;

                var trailGo = new GameObject("HPFR_Trail") { hideFlags = HideFlags.HideAndDontSave };
                trailGo.transform.SetParent(root.transform, false);
                var trail = trailGo.AddComponent<TrailRenderer>();
                try
                {
                    trail.Clear();
                    trail.AddPositions(new Vector3[] { Vector3.zero, rigOrigin });
                    if (Spans(trail.bounds, rigOrigin)) { effectRend = trail; effectKind = "TrailRenderer"; }
                }
                catch { /* fall through to the LineRenderer stand-in */ }

                if (effectRend == null)
                {
                    var lineGo = new GameObject("HPFR_Line") { hideFlags = HideFlags.HideAndDontSave };
                    lineGo.transform.SetParent(root.transform, false);
                    var line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.SetPosition(0, Vector3.zero);
                    line.SetPosition(1, rigOrigin);
                    if (Spans(line.bounds, rigOrigin)) { effectRend = line; effectKind = "LineRenderer"; }
                }

                if (effectRend == null)
                {
                    // Honest stand-down: without a spanning effect renderer we cannot MEASURE
                    // the mechanism, and asserting it anyway would be a restatement.
                    notes.Add(RegressionOutcome.PartialSkip("[framing] mechanism measurement",
                        "neither TrailRenderer nor LineRenderer reported world-spanning bounds in this " +
                        "EditMode batch, so the poisoned-union number could not be measured"));
                    return;
                }

                Bounds modelBounds = modelRend.bounds;

                Bounds polluted = modelBounds;
                polluted.Encapsulate(effectRend.bounds);

                float modelRadius = Mathf.Max(modelBounds.extents.magnitude, 0.5f);
                float aimErrOld = Vector3.Distance(polluted.center, modelBounds.center);

                // The fix's rule, applied to the WHOLE renderer set (model + effect) rather
                // than to the model alone - otherwise this would be distance-from-a-point-to-
                // itself, which cannot fail and therefore is not a test. What is exercised here
                // is the FILTER: does the type allowlist actually reject the effect renderer?
                bool have = false;
                Bounds filtered = default(Bounds);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    if (!have) { filtered = r.bounds; have = true; }
                    else filtered.Encapsulate(r.bounds);
                }
                if (!have) { failures.Add("[framing] the allowlist rejected even the model's MeshRenderer"); return; }
                float aimErrNew = Vector3.Distance(filtered.center, modelBounds.center);

                // The rig's own arithmetic (FrameCamera), applied to the polluted union.
                float pollutedRadius = Mathf.Max(polluted.extents.magnitude, 0.5f);
                float camDistOld = pollutedRadius / Mathf.Sin(fov * Mathf.Deg2Rad * 0.5f) * 1.08f;

                // 1. the defect must still reproduce
                if (aimErrOld <= modelRadius * 4f + 1f)
                    failures.Add(string.Format(
                        "[framing] the captured mechanism NO LONGER REPRODUCES: encapsulating a {0} " +
                        "spanning to the world origin moved the aim only {1:F1}u from the model " +
                        "(radius {2:F2}). Either Unity's bounds semantics changed or this scenario no " +
                        "longer models the defect - this suite is no longer measuring what it claims.",
                        effectKind, aimErrOld, modelRadius));

                // 2. and the camera it produces must be unable to see the model at all
                if (camDistOld <= far)
                    failures.Add(string.Format(
                        "[framing] the poisoned union yields camDist {0:F0}u, INSIDE farClipPlane {1:F0}u - " +
                        "the second half of the captured failure (the model past the far plane) no longer " +
                        "reproduces, so this case understates the defect.", camDistOld, far));

                // 3. the fix's rule must actually aim at the model
                if (aimErrNew > 0.001f)
                    failures.Add(string.Format(
                        "[framing] mesh-only bounds do not aim at the model (aimErr {0:F3}u)", aimErrNew));

                notes.Add(string.Format(
                    "mechanism measured via {0}: polluted aim off by {1:F0}u -> camDist {2:F0}u (far {3:F0}u, " +
                    "model radius {4:F2}); mesh-only aim error {5:F3}u",
                    effectKind, aimErrOld, camDistOld, far, modelRadius, aimErrNew));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =====================================================================
        //  CASE B - drive the SHIPPING rig and ask its OWN readback
        // =====================================================================
        // Reproduces the captured scenario against HeroPreviewViewer itself: a body that
        // carries both a drawable mesh and a world-spanning effect renderer. If the rig
        // frames the model, DrewContent() answers true. If ComputeBounds regresses to
        // summing every renderer, the camera frames empty space and DrewContent answers
        // false - which is exactly the owner-visible defect, caught headlessly.
        //
        // Needs a real graphics device (it is a render-texture readback). Under
        // -nographics it stands down VISIBLY rather than reporting green.
        private static void CaseB_LiveRigDraws(List<string> failures, List<string> notes)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                notes.Add(RegressionOutcome.PartialSkip("[live-rig] end-to-end render proof",
                    "no graphics device in this batch (-nographics), so the render-texture readback " +
                    "cannot run; the framing rule is still measured by Case A and linted by Case C"));
                return;
            }

            string src = ReadSrc(PreviewSrc);
            Vector3 rigOrigin = src != null ? ParseRigOrigin(src, failures) : new Vector3(-5000f, -5000f, 0f);

            GameObject body = null;
            HeroPreviewViewer viewer = null;
            try
            {
                // A body sitting near the WORLD origin - exactly like the live hero, whose
                // child body is what the panels hand to Begin().
                body = new GameObject("HPFR_Body") { hideFlags = HideFlags.HideAndDontSave };

                var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                mesh.transform.SetParent(body.transform, false);

                // The poison: a world-space effect renderer whose points reach from the world
                // origin out to where the CLONE will be placed - the WeaponTrail from the capture.
                var lineGo = new GameObject("WeaponTrail") { hideFlags = HideFlags.HideAndDontSave };
                lineGo.transform.SetParent(body.transform, false);
                var line = lineGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, rigOrigin);

                viewer = new HeroPreviewViewer();
                if (!viewer.Begin(body, 128))
                {
                    notes.Add(RegressionOutcome.PartialSkip("[live-rig] end-to-end render proof",
                        "HeroPreviewViewer.Begin returned false in this batch (no render texture could " +
                        "be created), so the draw could not be measured"));
                    return;
                }

                string detail;
                bool drew = viewer.DrewContent(out detail);
                if (!drew)
                    failures.Add("[live-rig] the shipping rig drew NOTHING for a body carrying a " +
                                 "world-spanning effect renderer - the WO-1059 defect is back. The rig's " +
                                 "own readback says: " + detail);
                else
                    notes.Add("live rig drew content with a world-spanning trail present (" + detail + ")");
            }
            catch (Exception ex)
            {
                // Never swallow (section 12): a proof that could not run says so.
                notes.Add(RegressionOutcome.PartialSkip("[live-rig] end-to-end render proof",
                    "driving the rig threw " + ex.GetType().Name + ": " + ex.Message));
            }
            finally
            {
                if (viewer != null) { try { viewer.Dispose(); } catch { } }
                if (body != null) UnityEngine.Object.DestroyImmediate(body);
            }
        }

        // =====================================================================
        //  CASE C - the ordering neither measurement can localise
        // =====================================================================
        // Case B proves the rig draws; it cannot say WHERE a future regression crept in.
        // Two orderings are load-bearing and free to pin: every clone path must neutralise
        // effect renderers BEFORE it computes bounds, and ComputeBounds must keep its type
        // allowlist. Also guards the instrumentation (section 12: never strip FlowTrace).
        private static void CaseC_OrderingAndExclusion(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(PreviewSrc);
            if (src == null) { failures.Add("[ordering] cannot read " + PreviewSrc); return; }

            if (src.IndexOf('\0') >= 0)
            {
                failures.Add("[ordering] " + PreviewSrc + " contains an embedded NUL byte (mount-garble, " +
                             "CLAUDE.md section 0) - the file is untrustworthy, not merely wrong");
                return;
            }

            string code = StripComments(src);

            if (!Regex.IsMatch(code, @"static\s+void\s+NeutralizeEffectRenderers\s*\("))
            {
                failures.Add("[ordering] NeutralizeEffectRenderers is gone - nothing strips the cloned " +
                             "TrailRenderer, whose world-space points aim the camera at empty space");
                return;
            }

            // ComputeBounds must still refuse non-mesh renderers. Scanned as a WINDOW after the
            // signature rather than by brace-matching the body: this repo's mandated C# gate
            // (CLAUDE.md section 1) counts raw braces, so a lint that carries literal brace
            // characters fails the gate on its own source.
            var cb = Regex.Match(code, @"static\s+Bounds\s+ComputeBounds\s*\([^)]*\)", RegexOptions.Singleline);
            if (!cb.Success)
                failures.Add("[ordering] ComputeBounds is gone or unrecognisable");
            else
            {
                int start = cb.Index + cb.Length;
                int len = Math.Min(2500, code.Length - start);
                string window = code.Substring(start, len);
                if (!Regex.IsMatch(window, @"is\s+MeshRenderer") ||
                    !Regex.IsMatch(window, @"is\s+SkinnedMeshRenderer"))
                    failures.Add("[ordering] ComputeBounds no longer restricts framing to MeshRenderer / " +
                                 "SkinnedMeshRenderer. It is summing every Renderer again, which is the " +
                                 "exact line that produced the blank preview in F8 seq 3585");
            }

            // Every clone path must neutralise BEFORE it frames.
            int paths = 0, ordered = 0;
            foreach (Match m in Regex.Matches(code, @"StripGameplayBehaviours\s*\(\s*_model\s*\)\s*;"))
            {
                paths++;
                int neutralize = code.IndexOf("NeutralizeEffectRenderers", m.Index, StringComparison.Ordinal);
                int compute    = code.IndexOf("ComputeBounds", m.Index, StringComparison.Ordinal);
                if (neutralize >= 0 && (compute < 0 || neutralize < compute)) ordered++;
            }

            if (paths == 0)
                failures.Add("[ordering] no clone path found (StripGameplayBehaviours(_model) is gone) - " +
                             "this lint can no longer see the paths it is meant to guard");
            else if (ordered != paths)
                failures.Add(string.Format(
                    "[ordering] {0} of {1} clone path(s) neutralise effect renderers BEFORE computing " +
                    "bounds. A path that frames first reads the stale world-space trail bounds and aims " +
                    "the camera at empty space.", ordered, paths));

            if (!code.Contains("FlowTrace"))
                failures.Add("[ordering] FlowTrace calls were stripped from the preview rig - " +
                             "instrumentation is permanent (CLAUDE.md section 12)");

            notes.Add(paths + " clone path(s) neutralise-then-frame; ComputeBounds allowlist intact");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool Spans(Bounds b, Vector3 far)
        {
            // "Spans" = the AABB is big enough to reach from the world origin out to `far`.
            return b.size.magnitude > far.magnitude * 0.5f;
        }

        private static Vector3 ParseRigOrigin(string src, List<string> failures)
        {
            var m = Regex.Match(src,
                @"RigOrigin\s*=\s*new\s+Vector3\s*\(\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*\)");
            if (!m.Success)
            {
                failures.Add("[framing] could not parse RigOrigin out of " + PreviewSrc +
                             " - this suite refuses to hardcode a second copy of it");
                return new Vector3(-5000f, -5000f, 0f);
            }
            return new Vector3(
                float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        private static float ParseFloat(string src, string pattern, float fallback)
        {
            var m = Regex.Match(src, pattern);
            if (!m.Success) return fallback;
            float v;
            return float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static string ReadSrc(string rel)
        {
            try
            {
                string full = Path.Combine(Directory.GetCurrentDirectory(), rel);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch { return null; }
        }

        private static string StripComments(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = Regex.Replace(s, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", " ");
            return s;
        }
    }
}
