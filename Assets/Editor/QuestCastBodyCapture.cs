// =============================================================================
// QuestCastBodyCapture — photograph the two quest-cast NPC bodies in their REAL
// idle pose so the owner can approve or reject the casting with one word.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/QA/Capture Quest-Cast NPC Bodies
// Batch: -executeMethod DeNelle.Editor.QuestCastBodyCapture.Run
// Marker: QUESTCAST_CAPS_OK <n> file=<paths>   |   QUESTCAST_CAPS_FAIL: <reason>
// Output: Builds/QuestCastCaps/<Name>.png  +  Builds/QuestCastCaps/_summary.txt
//
// WHY THIS EXISTS. The two quest-cast NPCs in QuestCastNpcInjector were retagged
// off KayKit placeholder bodies onto purchased CraftPix people. Nothing but a
// PICTURE can tell the owner whether "Village Elder" reads as an authority figure
// at the world tree and whether "Fenn Wildmane" reads as a beast handler at the
// pet-house — those are creative judgements and they belong to her. This tool
// REPORTS. It never changes the casting.
//
// WHAT MAKES IT EVIDENCE RATHER THAN A CLAIM (CLAUDE.md §12):
//  1. IT PROVES THE POSE. The whole point of the retag is that these bodies play a
//     CIVILIAN idle (AC_CraftPixTownsfolk -> Idle) and not the Knight combat
//     standby. A bind/T-posed render would silently misinform the owner, so we
//     drive the real Animator (cullingMode=AlwaysAnimate, Rebind, then 60 steps of
//     1/30s) and ASSERT a sampled bone actually moved. Nothing moved => FAIL, named.
//  2. ONE RIG, BOTH SUBJECTS. Same camera, same lights, same background, framed on
//     RENDERED BOUNDS — copied from StorefrontOrientationCapture / EnemyProvingHarness
//     so the two shots are comparable with each other and with the enemy caps.
//  3. THE SUBJECT IS ISOLATED on spare culling layer 31 (StorefrontOrientationCapture's
//     trick) so the other body, the lights' gizmos, or anything else cannot photobomb.
//  4. THE BACKGROUND IS MID-GREY, deliberately NOT green and NOT red: the owner is
//     red/green colourblind (memory: owner-colorblind-delegate-visual-creative) and a
//     tinted backdrop would cost her the one channel she can read — contrast.
// =============================================================================

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class QuestCastBodyCapture
    {
        private const string OutDir = "Builds/QuestCastCaps";
        private const int ResX = 900;
        private const int ResY = 1200;

        /// <summary>Spare layer used to photograph one body with nothing in front of it.
        /// Same trick, same reason, as StorefrontOrientationCapture.</summary>
        private const int IsolationLayer = 31;

        private const int AnimFrames = 60;
        private const float AnimDt = 1f / 30f;

        /// <summary>Below this a bone did not meaningfully move (metres / degrees).</summary>
        private const float MoveEpsilonMetres = 0.0015f;
        private const float MoveEpsilonDegrees = 0.35f;

        /// <summary>Above this width/height silhouette ratio the body is standing in a T-pose.
        /// A T-posed humanoid is about as wide as it is tall; arms-down idles measure ~0.30-0.45.
        /// 0.70 sits well clear of both, so neither a broad-shouldered idle nor a narrow T-pose
        /// can land on the wrong side of it.</summary>
        private const float TPoseWidthRatio = 0.70f;

        /// <summary>Under this fraction of non-background pixels the shot is blank —
        /// i.e. we photographed nothing, which is itself a finding.</summary>
        private const float BlankCoverageFloor = 0.004f;

        private const string ControllerPath =
            "Assets/Resources/NPCs/CraftPixPeople/AC_CraftPixTownsfolk.controller";

        /// <summary>The CIVILIAN idle the retag was supposed to be getting. AC_CraftPixTownsfolk's
        /// Idle state actually points at Assets/Action/Shared/Shared_Idle.fbx — the SAME clip
        /// Knight/Cleric/Mage/Ranger use — so the shipped pose is the hero combat standby retargeted
        /// onto an auto-mapped CraftPix avatar. We shoot this clip as an A/B so the owner can see the
        /// difference. ⚠ SAMPLED ONLY: nothing here edits the controller. Which idle these NPCs play
        /// is a decision, and decisions belong to the owner.</summary>
        private const string CivilianIdlePath =
            "Assets/Supercyan/Animations/CharacterPackAnimations/MovementAnimations/common_people@idle.FBX";

        /// <summary>The casting under review. name -> prefab asset path. READ-ONLY:
        /// this table mirrors QuestCastNpcInjector, it does not decide anything.</summary>
        private static readonly (string Label, string Role, string PrefabPath)[] Cast =
        {
            ("Village Elder",  "quest giver anchored at the Heart of Elarion",
             "Assets/Resources/NPCs/CraftPixPeople/NPC_King.prefab"),
            ("Fenn Wildmane",  "beast/pet trainer anchored at the pet-house",
             "Assets/Resources/NPCs/CraftPixPeople/NPC_Peasant_4.prefab"),
        };

        /// <summary>Three-quarter FRONT: the camera sits in front of the model (+Z, the CraftPix
        /// forward) and off to one side, a touch above eye height. ONE direction for every shot —
        /// that is what makes the bind / shipped / alt-idle images comparable.</summary>
        private static readonly Vector3 CamDir = new Vector3(0.62f, 0.16f, 1f).normalized;

        [MenuItem("Defenders/QA/Capture Quest-Cast NPC Bodies")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            var log = new StringBuilder();
            var failures = new List<string>();
            var written = new List<string>();

            // A scratch scene. NEVER saved, never one of the curated scenes (CLAUDE.md §3).
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutDir);

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
                log.AppendLine($"NOTE: controller not found at {ControllerPath} — relying on each prefab's own.");

            // LIGHTING IS EVIDENCE, NOT DECORATION. An empty scene has no ambient and no
            // skybox, so an unlit face goes to pure black and the clothing — the whole
            // reason for the shot — cannot be read. Key light along the camera's own view
            // direction, fill from the opposite side, flat neutral ambient.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.48f, 0.50f, 1f);

            Vector3 camDir = CamDir;

            var camGo = new GameObject("~QuestCastCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Plain mid-grey. NOT green, NOT red — the owner is red/green colourblind.
            cam.backgroundColor = new Color(0.46f, 0.46f, 0.47f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 30f;

            var keyGo = new GameObject("~QuestCastKeyLight");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.45f;
            key.color = Color.white;
            keyGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(-camDir.x, -0.35f, -camDir.z).normalized, Vector3.up);

            var fillGo = new GameObject("~QuestCastFillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.75f;
            fill.color = new Color(0.90f, 0.92f, 1f, 1f);
            fillGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(camDir.x, -0.20f, camDir.z * 0.4f).normalized, Vector3.up);

            var rt = new RenderTexture(ResX, ResY, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            log.AppendLine("QUEST-CAST BODY CAPTURE — the two retagged NPCs, photographed in their driven idle pose.");
            log.AppendLine($"camera fov={cam.fieldOfView} dir={camDir} bg=mid-grey(0.46) res={ResX}x{ResY}");
            log.AppendLine(new string('-', 110));

            int index = 0;
            foreach (var entry in Cast)
            {
                string label = entry.Label;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                if (prefab == null)
                {
                    failures.Add($"{label}: prefab missing at {entry.PrefabPath}");
                    log.AppendLine($"{label,-16} >>>> prefab MISSING at {entry.PrefabPath}");
                    continue;
                }

                GameObject go = null;
                try
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.position = new Vector3(index * 30f, 0f, 0f);
                    go.transform.rotation = Quaternion.identity;

                    // A NavMeshAgent with no navmesh warps the transform and would move the
                    // body out from under the framing. It has nothing to do with the pose.
                    foreach (var agent in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
                        if (agent != null) agent.enabled = false;

                    // ── SHOT 1 of 3: THE BIND POSE, taken BEFORE anything drives the rig.
                    // The costume is what the owner is actually approving, and a broken idle
                    // would contaminate that read. This shot shows the body alone.
                    string bindPath = Path.Combine(OutDir, SafeName(label) + "__BIND.png").Replace('\\', '/');
                    float bindCoverage = Shoot(cam, rt, go, bindPath);
                    if (bindCoverage >= BlankCoverageFloor) written.Add(bindPath);

                    var anim = go.GetComponentInChildren<Animator>(true);
                    if (anim == null)
                    {
                        failures.Add($"{label}: no Animator on the body — cannot be posed");
                        log.AppendLine($"{label,-16} >>>> no Animator on the built body");
                        continue;
                    }
                    if (anim.runtimeAnimatorController == null && controller != null)
                        anim.runtimeAnimatorController = controller;

                    string ctrlName = anim.runtimeAnimatorController != null
                        ? anim.runtimeAnimatorController.name : "NULL";

                    // ── PROVE THE POSE ───────────────────────────────────────────────
                    var probes = PickProbeBones(go);
                    if (probes.Count == 0)
                    {
                        failures.Add($"{label}: no skinned bones to sample — nothing an idle could move");
                        log.AppendLine($"{label,-16} >>>> no skinned bones on the body");
                        continue;
                    }

                    float dPos = 0f, dRot = 0f;
                    string animMethod = "none";
                    bool moved = false;
                    if (anim.runtimeAnimatorController != null)
                    {
                        AnimatorCullingMode keepCull = anim.cullingMode;
                        AnimatorUpdateMode keepMode = anim.updateMode;
                        try
                        {
                            // With no camera looking at it a culled Animator writes no
                            // transforms and an honest rig reads as frozen. Force it.
                            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                            anim.updateMode = AnimatorUpdateMode.Normal;
                            anim.applyRootMotion = false;
                            anim.Rebind();
                            anim.Update(0f);

                            var before = Snapshot(probes);
                            for (int i = 0; i < AnimFrames; i++) anim.Update(AnimDt);
                            var after = Snapshot(probes);
                            Compare(before, after, out dPos, out dRot);
                            animMethod = "Animator.Update x" + AnimFrames;
                            moved = dPos > MoveEpsilonMetres || dRot > MoveEpsilonDegrees;
                        }
                        catch (System.Exception ex)
                        {
                            log.AppendLine($"{label,-16}      Animator.Update threw {ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            if (anim != null) { anim.cullingMode = keepCull; anim.updateMode = keepMode; }
                        }
                    }

                    // Fallback: sample the controller's clip directly. Same ladder as
                    // EnemyProvingHarness — an unsampleable rig is reported, never passed.
                    if (!moved)
                    {
                        AnimationClip clip = FirstUsableClip(anim);
                        if (clip != null)
                        {
                            try
                            {
                                AnimationMode.StartAnimationMode();
                                AnimationMode.BeginSampling();
                                AnimationMode.SampleAnimationClip(anim.gameObject, clip, 0f);
                                AnimationMode.EndSampling();
                                var b0 = Snapshot(probes);

                                AnimationMode.BeginSampling();
                                AnimationMode.SampleAnimationClip(anim.gameObject, clip,
                                    Mathf.Max(0.05f, clip.length * 0.4f));
                                AnimationMode.EndSampling();
                                var b1 = Snapshot(probes);
                                AnimationMode.StopAnimationMode();

                                Compare(b0, b1, out float p2, out float r2);
                                if (p2 > dPos) dPos = p2;
                                if (r2 > dRot) dRot = r2;
                                animMethod = $"AnimationMode clip '{clip.name}'";
                                moved = dPos > MoveEpsilonMetres || dRot > MoveEpsilonDegrees;
                            }
                            catch (System.Exception ex)
                            {
                                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                                log.AppendLine($"{label,-16}      AnimationMode sampling threw {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }

                    // ⛔ THE T-POSE GUARD. "A bone moved" is NOT proof the body is posed — and this
                    // harness learned that the expensive way on its own first run: the shipped idle
                    // produced dPos=0.02m / dRot=3.6deg (comfortably over the epsilons, so the
                    // movement assertion PASSED) while the body stood in a dead T-pose, because the
                    // deltas were a breathing wobble layered on an unretargeted bind pose. An
                    // epsilon on deltas can never see that; the SHAPE of the pose can.
                    // A T-posed humanoid is as WIDE as it is TALL (arms straight out). A relaxed
                    // idle has the arms down, so the silhouette is roughly a third as wide as tall.
                    // This is deliberately a measurement of the SILHOUETTE, not of a colour or a
                    // bone name: it works on any rig and it is what the owner would see.
                    TryWorldBounds(go, out Bounds poseB);
                    float widthOverHeight = poseB.size.y > 0.01f ? poseB.size.x / poseB.size.y : 0f;
                    bool tPosed = widthOverHeight > TPoseWidthRatio;
                    if (tPosed)
                        failures.Add($"{label}: T-POSE — the silhouette is {widthOverHeight:F2}x as wide as tall " +
                                     $"(a relaxed idle is under {TPoseWidthRatio:F2}). The rig moved " +
                                     $"(dPos={dPos:F4}m dRot={dRot:F2}deg) but that is a breathing wobble on an " +
                                     $"UNRETARGETED bind pose — the Idle clip is not reaching this avatar. " +
                                     "A render of this would misinform the owner about the pose.");

                    string poseVerdict = moved
                        ? $"POSED via {animMethod} (dPos={dPos.ToString("F4", CultureInfo.InvariantCulture)}m " +
                          $"dRot={dRot.ToString("F2", CultureInfo.InvariantCulture)}deg)"
                        : $"NOT POSED — bind/T pose risk ({animMethod}, dPos={dPos:F4}m dRot={dRot:F2}deg)";

                    if (!moved)
                        failures.Add($"{label}: the rig DID NOT MOVE ({animMethod}) — the render would show a " +
                                     "bind/T pose and silently misinform the owner");

                    // ── SHOT 2 of 3: THE POSE THE GAME ACTUALLY SHIPS ────────────────
                    string path = Path.Combine(OutDir, SafeName(label) + ".png").Replace('\\', '/');
                    float coverage = Shoot(cam, rt, go, path);
                    if (coverage < BlankCoverageFloor)
                        failures.Add($"{label}: the shot is BLANK ({coverage:P2} non-background pixels)");
                    else
                        written.Add(path);

                    // ── SHOT 3 of 3: A/B THE CIVILIAN IDLE ───────────────────────────
                    // Same precedent as StorefrontOrientationCapture's alternate-pitch shot:
                    // photograph the alternative from the identical rig so the two can be
                    // compared, and change nothing. Read-only — the controller is untouched.
                    string altPath = "-";
                    string altNote = "not sampled";
                    var civ = FirstClipInAsset(CivilianIdlePath);
                    if (civ == null)
                    {
                        altNote = $"civilian idle not found at {CivilianIdlePath}";
                    }
                    else
                    {
                        try
                        {
                            AnimationMode.StartAnimationMode();
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(anim.gameObject, civ, Mathf.Max(0.05f, civ.length * 0.3f));
                            AnimationMode.EndSampling();

                            altPath = Path.Combine(OutDir, SafeName(label) + "__ALTIDLE.png").Replace('\\', '/');
                            float altCov = Shoot(cam, rt, go, altPath);
                            // SAY SO WHEN THE A/B PROVED NOTHING. AnimationMode.SampleAnimationClip
                            // does NOT run the humanoid retarget, so sampling a humanMotion clip onto
                            // a different avatar leaves the body in its bind pose — an image that looks
                            // like an answer and is not one. Measure the silhouette again and label it.
                            TryWorldBounds(go, out Bounds altB);
                            float altRatio = altB.size.y > 0.01f ? altB.size.x / altB.size.y : 0f;
                            bool altRetargeted = altRatio <= TPoseWidthRatio;
                            AnimationMode.StopAnimationMode();
                            altNote = $"clip '{civ.name}' ({civ.length:F2}s, humanMotion={civ.humanMotion}) " +
                                      $"coverage={altCov:P2} silhouette={altRatio:F2} " +
                                      (altRetargeted
                                        ? "-> retargeted, arms down: this is what a civilian idle looks like on this body"
                                        : "-> ⚠ DID NOT RETARGET (AnimationMode does not run the humanoid retarget). " +
                                          "This PNG is effectively the BIND POSE and proves NOTHING about the civilian idle — do not read it as one.");
                            if (altCov >= BlankCoverageFloor) written.Add(altPath);
                        }
                        catch (System.Exception ex)
                        {
                            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                            altNote = $"sampling threw {ex.GetType().Name}: {ex.Message}";
                        }
                    }

                    TryWorldBounds(go, out Bounds b);
                    log.AppendLine($"{label,-16} role: {entry.Role}");
                    log.AppendLine($"{"",-16}   PREFAB  {entry.PrefabPath}");
                    log.AppendLine($"{"",-16}   ANIM    controller='{ctrlName}' probes={probes.Count} {poseVerdict}");
                    log.AppendLine($"{"",-16}   SHAPE   silhouette width/height={widthOverHeight:F2} (>{TPoseWidthRatio:F2} = T-POSE) -> {(tPosed ? "T-POSED" : "arms down, relaxed")}");
                    log.AppendLine($"{"",-16}   IDLECLIP the controller's Idle motion is '{ShippedIdleName(anim)}'");
                    log.AppendLine($"{"",-16}   BIND    coverage={bindCoverage:P2} -> {bindPath}");
                    log.AppendLine($"{"",-16}   SHIPPED bounds=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) " +
                                   $"coverage={coverage:P2} -> {path}");
                    log.AppendLine($"{"",-16}   ALTIDLE {altNote} -> {altPath}");
                    log.AppendLine();
                    Debug.Log($"[QuestCastCap] {label}: {poseVerdict} coverage={coverage:P2} -> {path}");
                }
                catch (System.Exception ex)
                {
                    failures.Add($"{label}: EXCEPTION {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    if (go != null) Object.DestroyImmediate(go);
                    index++;
                }
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo);

            log.AppendLine(new string('-', 110));
            log.AppendLine($"subjects={Cast.Length} captured={written.Count} failures={failures.Count}");
            foreach (var f in failures) log.AppendLine("  FAIL: " + f);

            string summaryPath = Path.Combine(OutDir, "_summary.txt").Replace('\\', '/');
            File.WriteAllText(summaryPath, log.ToString());
            Debug.Log("[QuestCastCap]\n" + log);

            // Each subject must yield at least its BIND and its SHIPPED shot. The ALTIDLE A/B
            // is diagnostic garnish — its absence is logged, not fatal.
            if (failures.Count > 0 || written.Count < Cast.Length * 2)
            {
                string reason = failures.Count > 0
                    ? string.Join(" ; ", failures)
                    : $"only {written.Count} PNGs written, expected at least {Cast.Length * 2}";
                Debug.LogError($"QUESTCAST_CAPS_FAIL: {reason}");
                return;
            }

            Debug.Log($"QUESTCAST_CAPS_OK {written.Count} file={string.Join(",", written)}");
        }

        /// <summary>Frame the body on its RENDERED BOUNDS and write one PNG. Returns the
        /// fraction of non-background pixels — a PNG of the backdrop is not evidence of a body.
        /// Identical rig for every call so the shots stay comparable.</summary>
        private static float Shoot(Camera cam, RenderTexture rt, GameObject go, string path)
        {
            if (!TryWorldBounds(go, out Bounds b)) return 0f;

            float h = Mathf.Max(0.01f, b.size.y);
            // Fit the FULL height into the vertical FOV with a margin, so a taller body is
            // not simply cropped and every subject fills the same fraction of frame.
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (h * 0.62f) / Mathf.Tan(halfFov);
            // Aim a touch above centre — head and shoulders carry the read.
            Vector3 aim = new Vector3(b.center.x, b.min.y + h * 0.56f, b.center.z);
            cam.transform.position = aim + CamDir * dist;
            cam.transform.LookAt(aim);
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = dist * 8f;

            var saved = new Dictionary<Transform, int>();
            MoveToLayer(go.transform, IsolationLayer, saved);
            cam.cullingMask = 1 << IsolationLayer;
            cam.Render();
            foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(ResX, ResY, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ResX, ResY), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var px = tex.GetPixels32();
            var bg = cam.backgroundColor;
            int lit = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                float rr = Mathf.Abs(px[i].r / 255f - bg.r);
                float gg = Mathf.Abs(px[i].g / 255f - bg.g);
                float bb = Mathf.Abs(px[i].b / 255f - bg.b);
                if (rr + gg + bb > 0.06f) lit++;
            }
            float coverage = lit / (float)(px.Length / 7f);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return coverage;
        }

        /// <summary>The name of the clip the controller's Idle state actually plays. NAMING it in
        /// the report is the point: the retag was believed to be getting a civilian idle, and only
        /// the clip's own name can settle that.</summary>
        private static string ShippedIdleName(Animator anim)
        {
            var clip = FirstUsableClip(anim);
            if (clip == null) return "<none>";
            string p = AssetDatabase.GetAssetPath(clip);
            return string.IsNullOrEmpty(p) ? clip.name : clip.name + " @ " + p;
        }

        private static AnimationClip FirstClipInAsset(string assetPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var c = o as AnimationClip;
                if (c != null && c.length > 0.05f && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        private static string SafeName(string label)
        {
            var sb = new StringBuilder(label.Length);
            foreach (char c in label) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        /// <summary>Up to 8 bones spread across the skeleton — enough that a partial idle
        /// (only the chest breathes) still registers, few enough to stay cheap.</summary>
        private static List<Transform> PickProbeBones(GameObject go)
        {
            var all = new List<Transform>();
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s == null || s.bones == null) continue;
                foreach (var b in s.bones) if (b != null && !all.Contains(b)) all.Add(b);
            }
            var probes = new List<Transform>();
            if (all.Count == 0) return probes;
            int step = Mathf.Max(1, all.Count / 8);
            for (int i = 0; i < all.Count && probes.Count < 8; i += step) probes.Add(all[i]);
            return probes;
        }

        private static List<(Vector3 p, Quaternion r)> Snapshot(List<Transform> probes)
        {
            var list = new List<(Vector3, Quaternion)>(probes.Count);
            foreach (var t in probes)
                list.Add(t != null ? (t.localPosition, t.localRotation) : (Vector3.zero, Quaternion.identity));
            return list;
        }

        private static void Compare(List<(Vector3 p, Quaternion r)> a, List<(Vector3 p, Quaternion r)> b,
                                    out float maxPos, out float maxRot)
        {
            maxPos = 0f; maxRot = 0f;
            int n = Mathf.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
            {
                maxPos = Mathf.Max(maxPos, Vector3.Distance(a[i].p, b[i].p));
                maxRot = Mathf.Max(maxRot, Quaternion.Angle(a[i].r, b[i].r));
            }
        }

        private static AnimationClip FirstUsableClip(Animator anim)
        {
            var ctrl = anim.runtimeAnimatorController;
            if (ctrl != null && ctrl.animationClips != null)
                foreach (var c in ctrl.animationClips)
                    if (c != null && c.length > 0.05f) return c;
            return null;
        }

        private static void MoveToLayer(Transform t, int layer, Dictionary<Transform, int> saved)
        {
            saved[t] = t.gameObject.layer;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) MoveToLayer(t.GetChild(i), layer, saved);
        }

        private static bool TryWorldBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
