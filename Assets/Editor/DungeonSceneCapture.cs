// =============================================================================
// DungeonSceneCapture — headless SCENE screenshots of the baked composed dungeons.
// Marker: DUNGEON_CAPTURE_OK <n>   (distinct per CLAUDE.md §8 — never REGRESSION_OK)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only).
//
// WHY THIS FILE HAD TO EXIST BEFORE WO-1004 COULD BE ACCEPTED:
// WO-1004's acceptance says "headless-capture a re-baked composed dungeon, open the
// PNG". Nothing in the project could do that. UICaptureLaunch.RunCaptureHeadless
// photographs code-built uGUI PANELS; VfxProofCapture photographs runtime VFX. Neither
// opens a baked .unity and looks at the WORLD. So the three defects WO-1004 fixes —
// a rainbow floor, floating debug markers, daylight in a sealed room — were all
// invisible to every automated check we had. They could only ever be caught by the
// owner playing the game, which is exactly the loop CLAUDE.md §14 exists to end.
//
// INVOKE:
//   powershell -File .\run-unity-method.ps1 `
//     -Method DeNelle.Editor.DungeonSceneCapture.CaptureAll -LogName dungeon-capture.log
//
// OUTPUT: Builds/dungeon-capture/<scene>_<view>.png
//
// EDIT-MODE AND SYNCHRONOUS, deliberately. UICaptureLaunch's own header records the
// trap: under `-batchmode -quit -executeMethod`, a Play-mode capture returns
// immediately and Unity quits BEFORE Play ticks, writing ZERO pngs while reporting
// success. Everything here happens inside the one executeMethod call, so `-quit`
// takes effect only after the files are on disk.
//
// ── BLANKNESS IS THE WHOLE POINT ─────────────────────────────────────────────
// "Wrote 10 PNGs" is worth nothing if they are 10 black rectangles — and a dark
// dungeon is EXACTLY the subject most likely to produce them. That is the same
// false-green shape as an exit code of 0 on a gate that refused (project memory
// `gates-report-success-without-proving-it`). So every frame is measured: mean
// luminance and the count of DISTINCT colours. A frame that is uniform, or almost
// entirely one colour, is reported as BLANK and fails the marker.
//
// The tolerance is deliberately not symmetric. After WO-1004 these rooms are SUPPOSED
// to be dark, so "dark" cannot mean "broken" — a legitimately lit dungeon still has
// torch pools, fog gradients and geometry edges, and therefore many distinct colours.
// Uniformity, not darkness, is what proves nothing rendered.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class DungeonSceneCapture
    {
        private const string SceneFolder = "Assets/Scenes/DungeonCompose";
        private const string OutFolder = "Builds/dungeon-capture";

        /// <summary>
        /// Dungeon scenes that live OUTSIDE the composed folder because a different builder makes
        /// them. The starter outpost is hand-coded (KayKitChallengeOutpostBuilder), so none of the
        /// composed pipeline's fixes reach it — which is exactly why it needs photographing under
        /// the same lens. A capture harness that only sees one pipeline will keep reporting the
        /// project clean while a whole scene rots.
        /// </summary>
        private static readonly string[] ExtraScenes =
        {
            "Assets/Scenes/KayKitChallengeOutpost.unity",
        };
        private const int Width = 1280;
        private const int Height = 720;

        // A frame with fewer distinct colours than this rendered nothing worth looking at.
        //
        // RECALIBRATED 2026-08-07, and the reason matters more than the number. This was 24,
        // chosen against the PRE-relight dungeons. The relight then landed (ambient 0.08 -> 0.05,
        // fog on, directional 0.35 -> 0.18) and six legitimate frames tripped it — because a
        // darker scene genuinely HAS fewer distinct quantised colours. The threshold was
        // measuring the art direction, not the failure.
        //
        // So the load-bearing signal is TopShareBlank below: a frame that is ~entirely ONE colour
        // rendered nothing, at any brightness. Distinct-colour count is kept only as a floor
        // against a truly degenerate frame, and dropped to a value a dark room clears easily.
        private const int MinDistinctColours = 6;

        // The real blank test: share of the frame held by the single most common colour.
        // A solid fill scores ~1.0 here whether it is black, grey or sky blue.
        private const float TopShareBlank = 0.98f;

        /// <summary>Capture every baked composed dungeon. Entry point for the headless runner.</summary>
        public static void CaptureAll()
        {
            var log = new StringBuilder();
            log.AppendLine("=== DungeonSceneCapture: headless scene shots of the composed dungeons ===");

            string[] scenes;
            try { scenes = Directory.GetFiles(SceneFolder, "*.unity", SearchOption.TopDirectoryOnly); }
            catch (Exception ex)
            {
                Debug.LogError(log + $"DUNGEON_CAPTURE_FAIL: cannot enumerate {SceneFolder}: {ex.Message}");
                return;
            }

            // Fold in the out-of-folder scenes, naming any that are missing rather than silently
            // shooting fewer scenes than the caller believes.
            var sceneList = new List<string>(scenes);
            foreach (var extra in ExtraScenes)
            {
                if (File.Exists(extra)) sceneList.Add(extra);
                else log.AppendLine($"  [extra] {extra} NOT FOUND - skipped (this is a gap, not a pass)");
            }
            scenes = sceneList.ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError(log + $"DUNGEON_CAPTURE_FAIL: no .unity scenes under {SceneFolder} - nothing to photograph. " +
                                     "Has the composer run?");
                return;
            }

            Directory.CreateDirectory(OutFolder);

            int written = 0;
            var blanks = new List<string>();

            foreach (var scenePath in scenes)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                string norm = scenePath.Replace('\\', '/');

                try
                {
                    var scene = EditorSceneManager.OpenScene(norm, OpenSceneMode.Single);
                    if (!scene.IsValid())
                    {
                        log.AppendLine($"  [{sceneName}] scene failed to open - SKIPPED");
                        continue;
                    }

                    // Report what the bake actually left in RenderSettings. This is the data
                    // WO-1004's relight is judged on, and reading it costs nothing - a number
                    // in the log beats squinting at a PNG to guess whether fog is on.
                    log.AppendLine($"  [{sceneName}] ambient={Fmt(RenderSettings.ambientLight)} " +
                                   $"intensity={RenderSettings.ambientIntensity:0.###} " +
                                   $"fog={RenderSettings.fog} mode={RenderSettings.fogMode} " +
                                   $"range={RenderSettings.fogStartDistance:0.#}->{RenderSettings.fogEndDistance:0.#} " +
                                   $"fogColor={Fmt(RenderSettings.fogColor)} " +
                                   $"skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "NONE")}");

                    Bounds worldBounds = ComputeWorldBounds(scene);
                    Vector3 anchor = FindFloorAnchor(scene, worldBounds);
                    log.AppendLine($"    anchor={anchor.x:0.#},{anchor.y:0.#},{anchor.z:0.#} " +
                                   $"(bounds centre {worldBounds.center.x:0.#},{worldBounds.center.z:0.#})");

                    foreach (var view in Views(worldBounds, anchor))
                    {
                        string outPath = Path.Combine(OutFolder, $"{sceneName}_{view.Name}.png");
                        if (!RenderTo(outPath, view.Position, view.Rotation, view.FieldOfView, out string verdict))
                            blanks.Add($"{sceneName}_{view.Name} ({verdict})");
                        else
                            written++;
                        log.AppendLine($"    {view.Name,-10} -> {Path.GetFileName(outPath)}  {verdict}");
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine($"  [{sceneName}] THREW: {ex.GetType().Name}: {ex.Message}");
                    blanks.Add($"{sceneName} (threw)");
                }
            }

            log.AppendLine($"  wrote {written} non-blank frame(s) to {OutFolder}/");

            if (blanks.Count > 0)
            {
                Debug.LogError(log + $"DUNGEON_CAPTURE_FAIL: {blanks.Count} blank/failed frame(s): " +
                                     string.Join(", ", blanks) +
                                     " -- a uniform frame means nothing rendered; do NOT read it as 'the dungeon is dark'.");
                return;
            }

            Debug.Log(log + $"DUNGEON_CAPTURE_OK {written}");
        }

        private static string Fmt(Color c) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##},{1:0.##},{2:0.##})", c.r, c.g, c.b);

        private readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float FieldOfView;

            public View(string name, Vector3 pos, Quaternion rot, float fov)
            {
                Name = name; Position = pos; Rotation = rot; FieldOfView = fov;
            }
        }

        /// <summary>
        /// Three views per dungeon, each answering a different WO-1004 question:
        ///   overview — is there SKY? (the daylight defect is only visible from outside/above)
        ///   eye      — what does a PLAYER see standing in it? (the felt-test angle)
        ///   floor    — the rainbow-floor and stray-marker angle, looking down at surfaces
        /// One view cannot answer all three, and the sky defect in particular is invisible
        /// from inside a room that already has a ceiling.
        /// </summary>
        private static IEnumerable<View> Views(Bounds b, Vector3 anchor)
        {
            Vector3 c = b.center;
            float span = Mathf.Max(b.size.x, b.size.z);
            if (span < 1f) span = 40f;

            yield return new View("overview",
                c + new Vector3(0f, span * 0.85f, -span * 0.55f),
                Quaternion.Euler(52f, 0f, 0f), 60f);

            // Interior views stand on a REAL FLOOR (the anchor), not the bounding-box centre.
            // On the three largest dungeons the centre falls in the void BETWEEN wings, so the
            // camera photographed empty space and returned a frame of pure background - luma
            // 0.291 on all three, which is exactly the clear colour. Averaging a sprawling
            // layout gives you a point that is in none of it.
            yield return new View("eye",
                new Vector3(anchor.x, anchor.y + 1.7f, anchor.z),
                Quaternion.Euler(4f, 35f, 0f), 70f);

            // MUST stay BELOW the ceiling. This view was originally at min.y + 6m and returned a
            // 100%-uniform frame on 4 of 5 dungeons - it was photographing the TOP of the ceiling
            // Bake Wave 1 had just added. The blank check caught it; without that check it would
            // have written four grey rectangles and reported five clean captures.
            // Interior headroom is RoomForgeCanon.WallHeight (4m), so 2.5m is inside the room.
            // NOTE: b.min.y is the LOWEST floor of a multi-level dungeon, so this frames the
            // bottom floor only - upper floors need their own pass, which is owed, not done.
            yield return new View("floor",
                new Vector3(anchor.x, anchor.y + 2.5f, anchor.z),
                Quaternion.Euler(58f, 20f, 0f), 65f);
        }

        /// <summary>
        /// A point standing ON an actual floor surface, as close to the middle of the layout as a
        /// real room gets. Returns the TOP of the chosen floor renderer, so callers add eye height
        /// to it directly.
        ///
        /// Why not the bounding-box centre: averaging a 20-room sprawl lands you in the gap between
        /// wings. Three of five dungeons photographed pure background from there.
        ///
        /// Floors are identified the same way RoomForgeMaterials.ApplyToRoomRoot identifies them -
        /// by NAME. That heuristic is shared debt, not a choice made here; if no floor-named
        /// renderer exists we fall back to the lowest renderer near the centre rather than
        /// pretending we found one.
        /// </summary>
        private static Vector3 FindFloorAnchor(Scene scene, Bounds b)
        {
            Renderer best = null;
            float bestDist = float.MaxValue;
            Renderer lowestFallback = null;
            float lowestY = float.MaxValue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null) continue;

                    Vector3 rc = r.bounds.center;
                    if (rc.y < lowestY) { lowestY = rc.y; lowestFallback = r; }

                    if (r.gameObject.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Bottom floor only - a stacked dungeon's upper storeys would otherwise win
                    // on horizontal distance and put the camera on the wrong level.
                    if (rc.y > b.min.y + 3f) continue;

                    float d = (new Vector2(rc.x, rc.z) - new Vector2(b.center.x, b.center.z)).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = r; }
                }
            }

            var chosen = best ?? lowestFallback;
            if (chosen == null) return new Vector3(b.center.x, b.min.y, b.center.z);

            var cb = chosen.bounds;
            return new Vector3(cb.center.x, cb.max.y, cb.center.z);
        }

        /// <summary>World bounds of every renderer in the scene, so framing adapts to dungeon size.</summary>
        private static Bounds ComputeWorldBounds(Scene scene)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null) continue;
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }

            if (!any) bounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));
            return bounds;
        }

        /// <summary>
        /// Render one frame to disk. Returns false when the frame is uniform enough to prove
        /// nothing rendered. Uses a throwaway camera so the scene's own cameras (and whatever
        /// state the bake left them in) cannot change what we photograph.
        /// </summary>
        private static bool RenderTo(string outPath, Vector3 pos, Quaternion rot, float fov, out string verdict)
        {
            GameObject camGo = null;
            RenderTexture rt = null;
            RenderTexture prevActive = RenderTexture.active;
            Texture2D shot = null;

            try
            {
                camGo = new GameObject("~DungeonCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
                camGo.transform.SetPositionAndRotation(pos, rot);

                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 5000f;
                // Honour whatever the bake set. Forcing a background here would HIDE the exact
                // defect we are hunting: if the skybox is still on, the shot must show it.
                cam.clearFlags = CameraClearFlags.Skybox;

                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply(false);

                var pixels = shot.GetPixels32();
                int distinct = CountDistinct(pixels, out float meanLuma, out float topShare);

                File.WriteAllBytes(outPath, shot.EncodeToPNG());

                bool blank = distinct < MinDistinctColours || topShare > TopShareBlank;
                verdict = $"luma={meanLuma:0.###} colours={distinct} top={topShare:P1}" + (blank ? "  BLANK" : "");
                return !blank;
            }
            catch (Exception ex)
            {
                verdict = $"threw {ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// Distinct colour count (quantised to 5 bits/channel so imperceptible gradient noise
        /// does not read as detail), plus mean luminance and the share held by the single most
        /// common colour. topShare is the one that actually catches a blank frame: a solid fill
        /// scores ~100% there even if compression noise inflates the distinct count.
        /// </summary>
        private static int CountDistinct(Color32[] px, out float meanLuma, out float topShare)
        {
            var counts = new Dictionary<int, int>(1024);
            double lumaSum = 0d;
            int top = 0;

            for (int i = 0; i < px.Length; i++)
            {
                Color32 p = px[i];
                lumaSum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255d;

                int key = ((p.r >> 3) << 10) | ((p.g >> 3) << 5) | (p.b >> 3);
                counts.TryGetValue(key, out int n);
                n++;
                counts[key] = n;
                if (n > top) top = n;
            }

            meanLuma = px.Length > 0 ? (float)(lumaSum / px.Length) : 0f;
            topShare = px.Length > 0 ? top / (float)px.Length : 1f;
            return counts.Count;
        }
    }
}
