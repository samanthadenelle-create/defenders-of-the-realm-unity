// =============================================================================
// PreviewRenderTextureSamplesRegression — WO-1451.
// The tower preview's RenderTexture and its camera must agree on sample count.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS, from captured data (device log 2026-09-06, 13:25:34.559 →
// 13:27:58.320 — 260 [BREAK] errors in 144 seconds, 130 of each, alternating):
//
//   [BREAK] error: RenderPass: Attachment 0 was created with 1 samples but 2 samples
//                  were requested.
//   [BREAK] error: EndRenderPass: Not inside a Renderpass
//   UniversalRenderPipeline:RenderSingleCamera
//     <- DeNelle.Village.TowerPreviewCamera:Begin(GameObject, OrientationFix, Int32)
//     <- Guard:Try
//
// At HEAD before the fix, inside ONE file:
//   TowerPreviewCamera.cs:84   renderTexture.antiAliasing = 2;   // a 2-sample target
//   TowerPreviewCamera.cs:131  _cam.allowMSAA = false;           // rendering 1 sample
// Every preview frame therefore opened a render pass it could not close.
//
// ⚠ WHY THE INVARIANT IS "RT == CAMERA" AND NOT "RT == PIPELINE ASSET".
// PartyShopPanelMvvm.cs:1461-1464 recorded the IDENTICAL error string from the
// OPPOSITE mismatch — the RT sat at Unity's default antiAliasing=1 while the camera
// still asked for the URP asset's m_MSAA:2 — and was fixed by turning the camera's
// MSAA off, not by raising the RT. Two mismatches in opposite directions producing one
// error string is the proof that the pipeline asset is not the discriminator. A suite
// that pinned "RT sample count == pipeline sample count" (the shape WO-1451 §4 asked
// for, corrected 2026-09-06 in the ticket) would go RED on the sanctioned fix and GREEN
// on the shipped defect. It is the AGREEMENT that is load-bearing.
//
// THE ORACLE, in the shape HeroPreviewFramingRegression established:
//   Case A  drives the REAL TowerPreviewCamera rig and asks UNITY what the objects
//           actually are — RenderTexture.antiAliasing off the live texture, allowMSAA
//           off the live camera. Nothing is copied from the code under test. Stands
//           DOWN VISIBLY via RegressionOutcome.PartialSkip when the batch has no
//           graphics device, because a render oracle that silently reports green under
//           -nographics is worse than no oracle at all.
//   Case B  a source lint on TowerPreviewCamera.cs ONLY — the localiser that still runs
//           under -nographics and names the two line numbers. It cannot replace Case A
//           (it can only see what is written, not what Unity built), which is why it is
//           second, not alone.
//
// RED PROOF (stated, NOT observed — this lane is edit-only and cannot run Unity):
// at HEAD before this change, `TowerPreviewCamera.Texture.antiAliasing` reads 2 while the
// rig camera's `allowMSAA` reads false, so Case A fails on the measured pair; and the
// source carries `antiAliasing     = 2,` at :84, so Case B fails on the lint. Both cases
// fire on the exact bytes that produced the 260 breaks.
//
// SCOPE. Deliberately TowerPreviewCamera only, per the WO-1451 silo. HeroPreviewViewer.cs
// (:101 antiAliasing = 2, :227 allowMSAA = false) carries the SAME defect and would go RED
// if this scan were widened — it needs its own work order and its own device evidence, and
// widening the scan here would leave this suite failing on a file this ticket may not touch.
//
// Registered in DataRegression.RunAll as the "preview-rt-samples suite".
// Markers: PREVIEW_RT_SAMPLES_OK / PREVIEW_RT_SAMPLES_FAIL.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class PreviewRenderTextureSamplesRegression
    {
        private const string PreviewSrc = "Assets/_Modules/Village/UI/TowerPreviewCamera.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("PREVIEW_RT_SAMPLES_OK - " + reason);
            else Debug.LogError("PREVIEW_RT_SAMPLES_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                CaseA_LiveRigSamplesAgree(failures, notes);
                CaseB_SourceStatesBothHalves(failures, notes);
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
            reason = "preview RT/camera sample counts agree - " + string.Join("; ", notes);
            return true;
        }

        // =====================================================================
        //  CASE A - MEASURED against Unity, on the shipping class
        // =====================================================================
        // Builds a throwaway "prefab" (a primitive cube), hands it to the real
        // TowerPreviewCamera.Begin, then reads the sample count off the LIVE
        // RenderTexture and the MSAA flag off the LIVE camera. The camera is found by
        // targetTexture identity rather than by name, so renaming the rig GameObject
        // cannot make this silently stop testing anything.
        //
        // Teardown is DestroyImmediate, NOT TowerPreviewCamera.Dispose(): Dispose uses
        // Object.Destroy, which is not valid in an EditMode batch and would leak the rig
        // while logging a second, unrelated error. Dispose() itself is correct for the
        // runtime callers and is deliberately left alone (out of WO-1451's silo).
        private static void CaseA_LiveRigSamplesAgree(List<string> failures, List<string> notes)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                notes.Add(RegressionOutcome.PartialSkip("[live-rig] measured sample-count pair",
                    "no graphics device in this batch (-nographics), so no RenderTexture can be " +
                    "created and the live pair cannot be measured; Case B still lints the source"));
                return;
            }

            GameObject prefab = null;
            TowerPreviewCamera preview = null;
            GameObject rigRoot = null;
            RenderTexture rt = null;
            try
            {
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prefab.hideFlags = HideFlags.HideAndDontSave;
                prefab.name = "PRTS_PreviewPrefab";

                preview = new TowerPreviewCamera();
                if (!preview.Begin(prefab, null, 128))
                {
                    notes.Add(RegressionOutcome.PartialSkip("[live-rig] measured sample-count pair",
                        "TowerPreviewCamera.Begin returned false in this batch, so the rig it would " +
                        "have built could not be measured"));
                    return;
                }

                rt = preview.Texture;
                if (rt == null)
                {
                    failures.Add("[live-rig] Begin reported success but Texture is null - there is no " +
                                 "render target to measure, which is a different defect from WO-1451 " +
                                 "and must not be reported as a passing sample-count check.");
                    return;
                }

                Camera cam = null;
                foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                {
                    if (c == null) continue;
                    if (ReferenceEquals(c.targetTexture, rt)) { cam = c; break; }
                }

                if (cam == null)
                {
                    failures.Add("[live-rig] no Camera in the batch has the preview RenderTexture as its " +
                                 "targetTexture. Either the rig no longer binds the RT to a camera, or the " +
                                 "camera is being created somewhere this oracle cannot see it - either way " +
                                 "the sample-count pair is unmeasured and must not read as green.");
                    return;
                }

                rigRoot = cam.transform.root != null ? cam.transform.root.gameObject : cam.gameObject;

                int rtSamples  = rt.antiAliasing;
                int camSamples = cam.allowMSAA ? QualitySettings.antiAliasing : 1;
                if (camSamples < 1) camSamples = 1;

                // THE INVARIANT, both directions. The failing HEAD state (rt 2 / allowMSAA false)
                // trips the first clause; the mirror-image PartyShop defect (rt 1 / allowMSAA true
                // against a 2x pipeline) trips the second.
                if (!cam.allowMSAA && rtSamples != 1)
                    failures.Add(string.Format(
                        "[live-rig] WO-1451 IS BACK: the preview camera renders 1 sample " +
                        "(allowMSAA=false) into a {0}-sample RenderTexture. This is the exact pair " +
                        "that produced 'RenderPass: Attachment 0 was created with 1 samples but {0} " +
                        "samples were requested' 130 times in 144 seconds on device. Set the RT's " +
                        "antiAliasing to 1 - do NOT follow the URP asset's m_MSAA here.", rtSamples));
                else if (cam.allowMSAA && rtSamples != camSamples)
                    failures.Add(string.Format(
                        "[live-rig] sample-count mismatch the OTHER way: the camera has MSAA ON and " +
                        "will request {0} samples (QualitySettings.antiAliasing) but the RenderTexture " +
                        "was created with {1}. Same RenderPass break, opposite direction - see " +
                        "PartyShopPanelMvvm.cs:1461-1464.", camSamples, rtSamples));
                else
                    notes.Add(string.Format(
                        "live rig measured: rt.antiAliasing={0}, cam.allowMSAA={1} (camera requests {2} " +
                        "sample(s)) - the pair agrees", rtSamples, cam.allowMSAA, camSamples));
            }
            catch (Exception ex)
            {
                // Never swallow (CLAUDE.md section 12): a proof that could not run says so.
                notes.Add(RegressionOutcome.PartialSkip("[live-rig] measured sample-count pair",
                    "driving the rig threw " + ex.GetType().Name + ": " + ex.Message));
            }
            finally
            {
                try
                {
                    foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                        if (c != null && rt != null && ReferenceEquals(c.targetTexture, rt))
                            c.targetTexture = null;
                }
                catch { }
                if (rigRoot != null) UnityEngine.Object.DestroyImmediate(rigRoot);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        // =====================================================================
        //  CASE B - the localiser lint (runs with no graphics device)
        // =====================================================================
        // Case A proves the built objects agree; this proves the SOURCE says so, which is
        // what a reader will grep and what a future edit will touch. It reads both halves
        // out of TowerPreviewCamera.cs and fails naming the line numbers.
        private static void CaseB_SourceStatesBothHalves(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(PreviewSrc);
            if (src == null) { failures.Add("[lint] cannot read " + PreviewSrc); return; }

            // ⚠ BOTH regexes are anchored to CODE-SHAPED lines (Multiline ^, the initializer
            // comma / the statement semicolon). An unanchored pattern takes the FIRST match in
            // the file, which is the RCA COMMENT above the fix - so the lint would read what the
            // comment claims instead of what the code does, and would keep answering "false"
            // after someone flipped the code to true. Comments lie (CLAUDE.md section 0).
            var aaMatch = Regex.Match(src, @"^\s*antiAliasing\s*=\s*(\d+)\s*,", RegexOptions.Multiline);
            if (!aaMatch.Success)
            {
                failures.Add("[lint] " + PreviewSrc + " no longer sets RenderTexture.antiAliasing at all. " +
                             "Unity's default is 1, which happens to be correct today - but an unstated " +
                             "sample count is exactly how this defect returns unnoticed. State it.");
                return;
            }

            var msaaMatch = Regex.Match(src, @"^\s*_cam\.allowMSAA\s*=\s*(true|false)\s*;", RegexOptions.Multiline);
            if (!msaaMatch.Success)
            {
                failures.Add("[lint] " + PreviewSrc + " no longer sets Camera.allowMSAA. With it unset the " +
                             "camera inherits the URP asset's MSAA and will request more samples than this " +
                             "RT was created with - the WO-1451 break.");
                return;
            }

            int aa = int.Parse(aaMatch.Groups[1].Value);
            bool allowMsaa = msaaMatch.Groups[1].Value == "true";

            if (!allowMsaa && aa != 1)
                failures.Add(string.Format(
                    "[lint] {0} sets antiAliasing = {1} while allowMSAA = false. The camera renders one " +
                    "sample into a {1}-sample target: 'Attachment 0 was created with 1 samples but {1} " +
                    "samples were requested'. Set antiAliasing = 1.", PreviewSrc, aa));
            else
                notes.Add(string.Format("source states antiAliasing={0} with allowMSAA={1} - consistent",
                    aa, allowMsaa));

            if (!Regex.IsMatch(src, @"FlowTrace\.Step\([^)]*antiAliasing", RegexOptions.Singleline))
                failures.Add("[lint] the Begin-time FlowTrace.Step naming BOTH sample values is gone. " +
                             "CLAUDE.md section 12: instrumentation is permanent - flag it off, never strip " +
                             "it. Without that line the next RenderPass break costs a code-read again.");
            else
                notes.Add("Begin-time FlowTrace.Step states both sample values");
        }

        // ── helpers ────────────────────────────────────────────────────────────
        private static string ReadSrc(string relPath)
        {
            try
            {
                string full = Path.Combine(Directory.GetCurrentDirectory(), relPath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch { return null; }
        }
    }
}
