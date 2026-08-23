// =============================================================================
// StructurePoseCapture - render structure models to PNG so a human can SEE the pose.
// -----------------------------------------------------------------------------
// WHY (2026-08-22): "for visual/spatial defects the SCREENSHOT is the data" - the
// FlowTrace shows what the code believes, the image shows what the player sees. The
// lying-down tower thread burned three static theories, and the one thing nobody had
// was a picture of the failing asset. There was no capture of the L3 archer tower at
// all; the only tower shot on disk is a build-mode GHOST from 2026-08-05, which is
// the BASE level and cannot show an L3 defect.
//
// ⭐ IT SHOOTS THE PREFAB AND THE MODEL SEPARATELY, ON PURPOSE. Measurement proved
// the prefab WRAPPER is the orientation authority, not the FBX: the wrapper holds a
// nested PrefabInstance whose m_Modifications carry transform overrides, so a model
// can be upright while the prefab that ships is on its side. Capturing only one of
// them is how that distinction stays invisible. Two images, side by side, make it
// obvious which layer is wrong.
//
// Neutral framing: camera distance is derived from each subject's own bounds, so a
// big model and a small one are directly comparable and nothing is cropped. The
// image is deliberately plain - no HUD, no ground - because the question is only
// "which way up is it".
//
// ASCII-only. Judge by the MARKER, never the exit code (CLAUDE.md section 8).
//
//   .\run-unity-method.ps1 -Method DeNelle.Editor.StructurePoseCapture.Run `
//       -LogName posecap.log -ExpectMarker STRUCTURE_POSE_CAPTURE_OK
// =============================================================================

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class StructurePoseCapture
    {
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;
        private const string OutDir = "docs/ui-evidence/structure-pose-2026-08-22";
        private const int Size = 900;

        private static readonly string[] Names =
        {
            "Tower_Wooden_Watchtower",       // control - oracle PASSES
            "Tower_Wooden_Watchtower_L2",    // control - oracle PASSES
            "Tower_Wooden_Watchtower_L3",    // FAILS - aspect 0.58
            "Ballista_L1",                   // FAILS - native 90, lying down
            "Ballista_L2",                   // FAILS
            // Owner-reported 2026-08-22, reproduced on a fresh HEAD build: the jeweler
            // renders UPSIDE DOWN on default-village load (stone base up, roof into the
            // ground) while the runtime seats it at euler=(90,0,0) with uniform scale.
            // armorer/barracks are the CONTROLS: same Tripo family, same
            // bakeAxisConversion:1, and both look correct - so whatever is wrong is
            // specific to this mesh, not to the flag.
            "jeweler",
            "armorer",
            "barracks",
            // WO-1153: gate_stone claims its MEASURED mesh (the carve-out is Wall-only).
            // Its native XZ:Y aspect is what decides whether it over-claims the 3.00 m cell.
            "Gate_Medieval_Medium",
        };

        [MenuItem("Defenders/Art/Capture Structure Poses")]
        public static void Run()
        {
            int shot = 0;
            try
            {
                Directory.CreateDirectory(OutDir);
                foreach (string name in Names)
                {
                    shot += Capture(name, ".prefab", "prefab") ? 1 : 0;
                    shot += Capture(name, ".fbx", "model") ? 1 : 0;
                }

                if (shot == 0)
                {
                    // A capture run that produced NO images must not read as success.
                    Debug.LogError("STRUCTURE_POSE_CAPTURE_FAIL - zero images written. That is a failure, not a pass.");
                    return;
                }
                AssetDatabase.Refresh();
                Debug.Log("STRUCTURE_POSE_CAPTURE_OK " + shot + " image(s) -> " + OutDir);
            }
            catch (Exception ex)
            {
                Debug.LogError("STRUCTURE_POSE_CAPTURE_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool Capture(string name, string ext, string tag)
        {
            string path = Root + "/" + name + ext;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;   // not every model has a wrapper prefab

            GameObject inst = null;
            Camera cam = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                if (inst == null) return false;
                inst.transform.position = Vector3.zero;

                var rs = inst.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0)
                {
                    Debug.LogWarning("[PoseCap] " + name + ext + " has NO renderers - nothing to show. " +
                                     "That is itself the finding for a wrapper prefab.");
                    return false;
                }

                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                // Frame from the subject's own size so every image is comparable.
                float radius = Mathf.Max(b.size.magnitude * 0.5f, 0.001f);
                var camGo = new GameObject("PoseCapCam");
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cam.orthographic = false;
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 40f + 100f;

                Vector3 dir = new Vector3(0.75f, 0.42f, -1f).normalized;   // 3/4 view, slightly above
                cam.transform.position = b.center + dir * (radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f);
                cam.transform.LookAt(b.center);

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                string outPath = OutDir + "/" + name + "__" + tag + ".png";
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                bool upright = b.size.y >= Mathf.Max(b.size.x, b.size.z);
                Debug.Log("[PoseCap] " + outPath + "  size=(" + b.size.x.ToString("0.00") + " x " +
                          b.size.y.ToString("0.00") + " x " + b.size.z.ToString("0.00") + ")  " +
                          (upright ? "UPRIGHT" : "LYING DOWN"));
                return true;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) { cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); }
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        // =====================================================================
        // ORIENTATION AUDIT (2026-08-23) - the five STRUCTURE_ORIENTATION_FAIL
        // "DOUBLE-CORRECTED" rows: workshop/forge/armorer/jeweler/barracks.
        //
        // WHY A RENDER AND NOT A NUMBER: the oracle's A1 is a DATA check - it reads
        // bakeAxisConversion off the .meta and the euler off the catalog and concludes
        // both corrections apply. It never looks at geometry. If the importer flag was
        // set but the mesh was never actually re-imported with it, the data reads
        // "double-corrected" while the shipped pixels are correct, and zeroing the
        // euler on the oracle's word would break a building the owner has confirmed.
        // So: shoot each model at the two candidate pitches and print the TAPER
        // (JewelerPitchSolver.TaperRatio) beside each image. AABB cannot tell +90 from
        // -90 or upright from upside-down; the taper and the picture can.
        //
        //   run-unity-method.ps1 -Method DeNelle.Editor.StructurePoseCapture.RunOrientationAudit
        //       -LogName orientaudit.log -ExpectMarker STRUCTURE_ORIENT_AUDIT_OK
        // =====================================================================

        private const string AuditOutDir = "docs/ui-evidence/structure-orientation-2026-08-23";

        /// <summary>Model STEMS on disk for the five failing catalog rows (catalog id -> stem:
        /// workshop -> ShopAndCrafting, forge -> Forge; the other three match).</summary>
        private static readonly string[] AuditSubjects = { "ShopAndCrafting", "Forge", "armorer", "jeweler", "barracks" };

        /// <summary>The two poses under test: identity (what the catalog would produce if the euler
        /// were zeroed, i.e. the oracle's remedy) and X=90 (what the catalog authors today).</summary>
        private static readonly float[] AuditPitches = { 0f, 90f };

        [MenuItem("Defenders/Art/Capture Structure Orientation Audit")]
        public static void RunOrientationAudit()
        {
            int shot = 0;
            try
            {
                Directory.CreateDirectory(AuditOutDir);
                foreach (string name in AuditSubjects)
                    foreach (float pitch in AuditPitches)
                        shot += CaptureAt(name, pitch) ? 1 : 0;

                if (shot == 0)
                {
                    Debug.LogError("STRUCTURE_ORIENT_AUDIT_FAIL - zero images written. That is a failure, not a pass.");
                    return;
                }
                AssetDatabase.Refresh();
                Debug.Log("STRUCTURE_ORIENT_AUDIT_OK " + shot + " image(s) -> " + AuditOutDir);
            }
            catch (Exception ex)
            {
                Debug.LogError("STRUCTURE_ORIENT_AUDIT_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool CaptureAt(string name, float pitch)
        {
            string path = Root + "/" + name + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning("[OrientAudit] no asset at " + path); return false; }

            GameObject inst = null;
            GameObject lightGo = null;
            Camera cam = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                if (inst == null) return false;
                inst.transform.position = Vector3.zero;
                inst.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                var rs = inst.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { Debug.LogWarning("[OrientAudit] " + name + " has NO renderers."); return false; }

                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

                float taper = JewelerPitchSolver.TaperRatio(inst.transform, b);

                // The stage owns its light - URP/Lit renders black without one, and a wall of
                // black PNGs would read as "the model is missing" instead of "unlit".
                lightGo = new GameObject("~AuditLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(45f, 35f, 0f);

                float radius = Mathf.Max(b.size.magnitude * 0.5f, 0.001f);
                var camGo = new GameObject("OrientAuditCam");
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 40f + 100f;

                // Eye-level-ish 3/4 view. Deliberately NOT top-down: a top-down camera cannot
                // show which end of a building is in the ground.
                Vector3 dir = new Vector3(0.75f, 0.30f, -1f).normalized;
                cam.transform.position = b.center + dir * (radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f);
                cam.transform.LookAt(b.center);

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                string outPath = AuditOutDir + "/" + name + "__pitch" +
                                 Mathf.RoundToInt(pitch).ToString() + ".png";
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                string verdict = taper < 0.80f ? "UPRIGHT (peak up)"
                               : taper > 1.25f ? "UPSIDE DOWN (peak down)"
                               : "ambiguous taper";
                Debug.Log("[OrientAudit] " + outPath + "  pitch=" + pitch.ToString("0") +
                          "  bounds=(" + b.size.x.ToString("0.00") + " x " + b.size.y.ToString("0.00") +
                          " x " + b.size.z.ToString("0.00") + ")  taper=" + taper.ToString("0.00") +
                          "  " + verdict);
                return true;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) { cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); }
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (lightGo != null) UnityEngine.Object.DestroyImmediate(lightGo);
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        // =====================================================================
        // WO-1062 - THE PORTAL ORBIT. "Does the portal read as a portal from every
        // heading?" is a question ONLY a picture can answer, and a single 3/4 shot
        // (everything above this line) structurally cannot: the defect the owner
        // photographed is that the SAME object looks like three different objects
        // from three headings. So this walks a camera all the way around it.
        //
        // ⛔ WHY IT LIVES IN THIS FILE and is not a new harness: this is the same
        // job as the pose capture - instantiate an asset, frame it off its own
        // bounds, write a PNG a human opens. Only the camera path differs. A fourth
        // capture harness would be a fourth place for the framing to drift.
        //
        // WHAT IT PROVES, and what it deliberately does NOT
        //   PROVES: from a given heading, does the owner's dark-star circle draw
        //           bright pixels, or does the camera see an unlit BACK FACE? The
        //           before/after pair is the evidence: "before" stages the ONE plane
        //           that shipped, "after" stages the TWO outward planes WO-1062 asks
        //           for, from the identical headings at the identical framing.
        //   DOES NOT: judge orientation from numbers. An AABB is identical at +90
        //           and -90 (CLAUDE.md-adjacent trap, JewelerPitchSolver), so there
        //           is no numeric orientation gate here on purpose. The per-heading
        //           BRIGHT-PIXEL COUNT is a viewing-direction measure, not an
        //           orientation one, and the PNG is still the primary evidence.
        //
        // It stages the SAME assets the runtime stages, with the SAME math copied
        // from DungeonWorldPortalSpawner.AttachPortalCircle (rotFront/rotBack/halfGap)
        // - and it says so at every site, because a divergence here would prove the
        // wrong thing convincingly.
        //
        //   .\run-unity-method.ps1 -Method DeNelle.Editor.StructurePoseCapture.RunPortalAngles `
        //       -LogName portalangles.log -ExpectMarker PORTAL_ANGLE_CAPTURE_OK
        // =====================================================================

        private const string PortalOutDir = "docs/ui-evidence/portal-angles-2026-08-23";

        /// <summary>The owner's portal art. This is the asset behind the Addressable key
        /// <c>PortalStructure.Address</c> ("dungeon/exit/portal") - registered from this exact
        /// path by DungeonPortalAddressable.PortalPath. Loaded by PATH here because an editor
        /// batch has no content build to resolve the key through; it is the same GameObject.</summary>
        private const string PortalArtPath = "Assets/Art/Dungeon/Exit/Portal.fbx";

        /// <summary>The owner's "Magic circle dark star" mirror - the same asset
        /// DungeonWorldPortalSpawner.CirclePrefabResourcePath ("VFX/Portal/PortalCircleDarkStar")
        /// resolves at runtime. Nothing is substituted (memory: vfx-map-owner-tags-no-creative-pick).</summary>
        private const string PortalCirclePath = "Assets/Resources/VFX/Portal/PortalCircleDarkStar.prefab";
        private const string PortalThresholdAuraPath =
            "Assets/Mirza Beig/Particle Systems/Ultimate VFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_portalBlue.prefab";

        /// <summary>DungeonWorldPortalSpawner.PortalHeight (6 m) - the height the live swap
        /// normalizes the art to (spawner :1032). Referenced, never re-guessed.</summary>
        private const float PortalTargetHeight = 6f;

        /// <summary>DungeonWorldPortalSpawner.OpeningSpanFraction (1/3), MinFitScale, MaxFitScale.</summary>
        private const float PortalOpeningSpanFraction = 1f / 3f;
        private const float PortalMinFitScale = 0.02f;
        private const float PortalMaxFitScale = 20f;

        /// <summary>DungeonWorldPortalSpawner.CircleSeparation - OWNER-AUTHORED ("put .25 between
        /// them"). Mirrored, not re-tuned.</summary>
        private const float PortalCircleSeparation = 0.25f;

        /// <summary>Particle sim time. A LOOP needs about a second to reach steady state; at t=0 it
        /// is a spawn ring and proves nothing (the same reasoning VfxProofCapture states for its
        /// loop shots).</summary>
        private const float PortalSimTime = 1.5f;

        /// <summary>Eight headings, 45 apart, measured as a yaw offset from the portal's OWN
        /// forward. 0 = the authored front, 180 = the opposite approach (the owner's black-shard
        /// NE), 90/270 = dead edge-on (which WO-1062 rules is CORRECT and must read as a thin
        /// sheet in a stone doorway - it is captured so a reviewer can confirm that, not "fix" it).</summary>
        private static readonly float[] PortalHeadings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        [MenuItem("Defenders/Art/Capture Portal Angles (WO-1062)")]
        public static void RunPortalAngles()
        {
            int shots = 0;
            try
            {
                Directory.CreateDirectory(PortalOutDir);

                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    // -nographics writes blank frames. Say it LOUD rather than let a wall of
                    // black PNGs read as "the portal renders nothing from every angle".
                    Debug.LogError("PORTAL_ANGLE_CAPTURE_FAIL - NO graphics device (-nographics). " +
                                   "Every frame would be blank. Re-run WITHOUT -nographics.");
                    return;
                }

                var art = AssetDatabase.LoadAssetAtPath<GameObject>(PortalArtPath);
                var circle = AssetDatabase.LoadAssetAtPath<GameObject>(PortalCirclePath);
                var aura = AssetDatabase.LoadAssetAtPath<GameObject>(PortalThresholdAuraPath);
                if (art == null)
                {
                    Debug.LogError("PORTAL_ANGLE_CAPTURE_FAIL - portal art missing at " + PortalArtPath +
                                   " (DungeonPortalAddressable.PortalPath). Nothing to orbit.");
                    return;
                }
                if (circle == null)
                {
                    Debug.LogError("PORTAL_ANGLE_CAPTURE_FAIL - the owner's dark-star mirror is missing at " +
                                   PortalCirclePath + ". Run DeNelle.Editor.PortalCircleVfxMirror.Run " +
                                   "(marker PORTAL_CIRCLE_VFX_OK) first - capturing without it would " +
                                   "photograph the fallback and call it the fix.");
                    return;
                }

                // THREE configurations, because there are three states worth telling apart and
                // only pictures can tell them apart:
                //   before_1plane        - the single X-axis plane that shipped originally.
                //   before_2planes_xaxis - HEAD as committed for WO-1062: two planes, but on the
                //                          LOCAL X axis, so both normals sit on +-Root.forward.
                //                          This one still shows the defect - that is the finding.
                //   after_2planes_zaxis  - the fix: local Z, normals on +-Root.right, which the
                //                          orbit proves is this art's actual doorway normal.
                shots += CapturePortalOrbit(art, circle, planes: 1, zAxis: false, tag: "before_1plane");
                shots += CapturePortalOrbit(art, circle, planes: 2, zAxis: false, tag: "before_2planes_xaxis");
                shots += CapturePortalOrbit(art, circle, planes: 2, zAxis: true, tag: "after_2planes_zaxis");

                // WO-1156 adjacent suspect: stage the EXACT threshold-aura prefab using today's
                // Root.rotation (yaw 0), then the doorway-normal hypothesis (yaw +90 -> Root.right).
                // Open the resulting images; this comparison is evidence, never a numeric gate.
                if (aura != null)
                {
                    shots += CapturePortalOrbit(art, aura, planes: 1, zAxis: false,
                                                tag: "threshold_current_root_rotation", localYaw: 0f);
                    shots += CapturePortalOrbit(art, aura, planes: 1, zAxis: false,
                                                tag: "threshold_doorway_right", localYaw: 90f);
                }
                else Debug.LogWarning("[PortalAngles] threshold aura source unavailable at " +
                                      PortalThresholdAuraPath + " - circle captures still written.");

                if (shots == 0)
                {
                    Debug.LogError("PORTAL_ANGLE_CAPTURE_FAIL - zero images written. That is a failure, not a pass.");
                    return;
                }
                AssetDatabase.Refresh();
                Debug.Log("PORTAL_ANGLE_CAPTURE_OK " + shots + " image(s) -> " + PortalOutDir);
            }
            catch (Exception ex)
            {
                Debug.LogError("PORTAL_ANGLE_CAPTURE_FAIL - " + ex.GetType().Name + ": " + ex.Message + "\n" + ex);
            }
        }

        /// <summary>Stage one configuration (1 plane = what shipped, 2 planes = WO-1062) and
        /// render it from every heading. Everything the stage creates is parented to one root and
        /// destroyed in the finally - the light included, because a stage light that outlives its
        /// stage is a documented leak in this repo.</summary>
        private static int CapturePortalOrbit(GameObject art, GameObject circle, int planes,
                                              bool zAxis, string tag, float localYaw = float.NaN)
        {
            GameObject stage = null;
            Camera cam = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            int written = 0;

            try
            {
                stage = new GameObject("~PortalAngleStage_" + tag);
                stage.transform.position = Vector3.zero;

                // The stage OWNS its light. Without one, URP/Lit stone renders black and every
                // heading would read as the "dark slab" this capture exists to disprove.
                var lightGo = new GameObject("~StageLight");
                lightGo.transform.SetParent(stage.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(45f, 35f, 0f);

                // The portal art, seated and height-normalized exactly as the live swap does
                // (PortalStructure.NormalizeToHeight at DungeonWorldPortalSpawner.cs:1032).
                var portal = (GameObject)PrefabUtility.InstantiatePrefab(art);
                portal.transform.SetParent(stage.transform, false);
                portal.transform.localPosition = Vector3.zero;
                portal.transform.localRotation = Quaternion.identity;
                DeNelle.Core.World.PortalStructure.NormalizeToHeight(portal, PortalTargetHeight);

                // The SAME measured reference the runtime uses: bounds -> centre -> 1/3 span.
                Bounds b = DeNelle.Core.World.PortalStructure.MeasureBounds(portal);
                Vector3 centre = b.center;
                float target = Mathf.Max(Mathf.Min(b.size.x, b.size.y), 0.001f) * PortalOpeningSpanFraction;
                float scale = DeNelle.Village.VFXManager.ResolveFitScale(circle, target,
                                                                        PortalMinFitScale, PortalMaxFitScale);

                // ============ THE MATH UNDER TEST ============
                // Copied verbatim from DungeonWorldPortalSpawner.AttachPortalCircle. If these two
                // ever diverge this harness proves the wrong thing, so they are stated together.
                //   X axis (the OLD math):  Euler(+-90,0,0)  normal -> +-Root.forward
                //   Z axis (the FIX):       Euler(0,0,-+90)  normal -> +-Root.right
                //   halfGap                                 along that same normal
                Quaternion rotFront = !float.IsNaN(localYaw)
                    ? stage.transform.rotation * Quaternion.Euler(0f, localYaw, 0f)
                    : stage.transform.rotation *
                      (zAxis ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.Euler(90f, 0f, 0f));
                Quaternion rotBack = stage.transform.rotation *
                    (zAxis ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(-90f, 0f, 0f));
                Vector3 normal = zAxis ? stage.transform.right : stage.transform.forward;
                Vector3 halfGap = normal * (PortalCircleSeparation * 0.5f);

                if (planes == 1)
                {
                    // The BEFORE: one plane, no gap - literally what shipped before WO-1062.
                    StagePortalCircle(circle, stage.transform, centre, rotFront, scale, "Front");
                }
                else
                {
                    StagePortalCircle(circle, stage.transform, centre + halfGap, rotFront, scale, "Front");
                    StagePortalCircle(circle, stage.transform, centre - halfGap, rotBack, scale, "Back");
                }

                // Mirror the runtime's post-load material sweep before judging magenta. The raw
                // Mirza prefab bypasses VFXManager in this editor stage, so without this call the
                // capture can photograph an editor-only shader miss the player never receives.
                DeNelle.Core.MagentaGuard.SweepGameObject(stage, "StructurePoseCapture.PortalAngles");

                // Drive the loops to steady state. Deterministic: same t, same frame, every run.
                foreach (var ps in stage.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Simulate(PortalSimTime, true, true, true);

                float radius = Mathf.Max(b.size.magnitude * 0.5f, 0.001f);
                var camGo = new GameObject("PortalAngleCam");
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 40f + 100f;
                float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.35f;

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                foreach (float heading in PortalHeadings)
                {
                    // Orbit the portal's OWN forward so a heading label means the same thing
                    // whatever yaw the stage sits at. Eye line a little above centre, the way
                    // the owner's shots are framed.
                    Vector3 dir = Quaternion.Euler(0f, heading, 0f) * stage.transform.forward;
                    cam.transform.position = centre + dir * dist + Vector3.up * (radius * 0.25f);
                    cam.transform.LookAt(centre);
                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    tex.Apply();

                    string outPath = PortalOutDir + "/portal_" + tag + "_" +
                                     Mathf.RoundToInt(heading).ToString("000") + "deg.png";
                    File.WriteAllBytes(outPath, tex.EncodeToPNG());

                    // Two cheap measures, both REPORTED not gated: bright pixels (does the face
                    // draw light toward this camera at all?) and magenta (WO-1062 section 3).
                    int bright = 0, magenta = 0;
                    var px = tex.GetPixels();
                    for (int i = 0; i < px.Length; i++)
                    {
                        Color c = px[i];
                        if (c.r > 0.9f && c.b > 0.9f && c.g < 0.3f) magenta++;
                        if (c.r + c.g + c.b > 1.35f) bright++;
                    }
                    UnityEngine.Object.DestroyImmediate(tex);
                    written++;

                    Debug.Log("[PortalAngles] " + outPath +
                              "  heading=" + heading.ToString("000") + "deg" +
                              "  bright=" + bright + " (" + (100f * bright / px.Length).ToString("0.00") + "%)" +
                              "  magenta=" + magenta +
                              (magenta > 0 ? "  <== MAGENTA ON THE PORTAL" : ""));
                }

                Debug.Log("[PortalAngles] staged '" + tag + "': planes=" + planes +
                          " axis=" + (zAxis ? "Z (normal +-Root.right)" : "X (normal +-Root.forward)") +
                          " bounds=(" + b.size.x.ToString("0.00") + " x " + b.size.y.ToString("0.00") +
                          " x " + b.size.z.ToString("0.00") + ") centre=" + b.center +
                          " target=" + target.ToString("0.00") + "m scale=" + scale.ToString("0.000") +
                          " gap=" + (planes == 2 ? PortalCircleSeparation.ToString("0.00") + "m" : "n/a"));
                return written;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) { cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); }
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>One plane of the portal face, staged the way AttachPortalCircle stages it.</summary>
        private static void StagePortalCircle(GameObject prefab, Transform parent, Vector3 pos,
                                              Quaternion rot, float scale, string which)
        {
            var go = UnityEngine.Object.Instantiate(prefab, pos, rot, parent);
            go.name = "[PortalCircle_DarkStar_" + which + "]";
            go.transform.localScale = Vector3.one * scale;
        }
    }
}
