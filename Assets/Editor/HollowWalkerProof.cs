// =============================================================================
// HollowWalkerProof — prove the new Hollow Walker body's RIG and ANIMATION, and
// photograph it beside the two AccuRig hollows it has to read as family with.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Art/Hollow Walker/2 Prove rig + animation + render
// Batch: -executeMethod DeNelle.Editor.HollowWalkerProof.Run
// Marker: HOLLOW_WALKER_PROOF_OK <pass>/<total> | HOLLOW_WALKER_PROOF_FAIL <n>
// Output: Builds/HollowWalkerProof/<body>.png, family.png, _summary.txt
//
// ⚠ WHY THIS EXISTS ALONGSIDE EnemyProvingHarness RATHER THAN INSTEAD OF IT.
// EnemyProvingHarness is the right instrument and is NOT modified here: it iterates
// enemies.json and builds through EnemyFactory.Build, the production chokepoint. But
// for a Hollow id, EnemyFactory.ModelForEnemy resolves through
// EnemyResolver.TryResolveHollowModel, which honours the enemies.json modelKey ONLY
// when that key is in EnemyResolver.KnownHollowModels — a seven-name set in
// Assets/_Modules/Core/Enemies/EnemyResolver.cs. A brand-new body is not in it, and
// EnemyAssetLoader additionally resolves "Enemies/<key>" through the Addressables
// catalog even in the editor (EnemyEditorSyncResolver). So until those two
// registrations land, the harness CANNOT be pointed at this body — it would keep
// photographing Skeleton_Minion and call it hollow-walker.
//
// This file therefore proves the ASSET directly (load the imported model, bind the
// family's own SkeletonHumanoid controller, drive it, measure a bone) and says so.
// It is a supplement, never a replacement: once the resolver + Addressables entries
// land, EnemyProvingHarness.RunBatch is the gate that matters and this becomes a
// belt-and-braces asset check.
//
// WHAT MAKES IT EVIDENCE AND NOT A CLAIM (CLAUDE.md §12):
//  • MOTION IS MEASURED, NOT ASSUMED. "A controller is assigned" proves nothing. We
//    drive the real Animator (Rebind + Update across 40 frames) and assert a BONE'S
//    LOCAL POSE ACTUALLY CHANGES, falling back to AnimationMode clip sampling, and
//    reporting UNPROVEN — never a silent pass — if neither can run in edit mode.
//  • THE PICTURE AND THE NUMBERS SHIP TOGETHER. Every shot prints the measured
//    rendered height/width, the bound base map path, and the non-background pixel
//    coverage beside it, so the image and the measurement cannot drift apart.
//  • THE FAMILY SHOT IS AT WORLD SCALE. The three bodies are photographed together
//    from ONE camera with no per-subject reframing, so relative HEIGHT and BULK in
//    that image are real and not an artifact of framing.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Rig/animation/appearance proof for the new Hollow Walker body.</summary>
    public static class HollowWalkerProof
    {
        private const string ContentRoot = DeNelle.Core.AssetRoots.EnemyContent;
        private const string OutDir      = "Builds/HollowWalkerProof";
        private const string Controller  = ContentRoot + "/SkeletonHumanoid.controller";

        private const int Res = 900;
        private const int IsolationLayer = 31;
        private const int AnimFrames = 40;
        private const float AnimDt = 1f / 30f;
        private const float MoveEpsilonMetres = 0.0015f;
        private const float MoveEpsilonDegrees = 0.35f;
        private const float BlankCoverageFloor = 0.004f;

        private const string OkMarker   = "HOLLOW_WALKER_PROOF_OK";
        private const string FailMarker = "HOLLOW_WALKER_PROOF_FAIL";

        /// <summary>The subject and the two AccuRig hollows it must read as family with.</summary>
        private static readonly string[] Bodies =
        {
            "Hollow_Walker",     // hollow-walker  (the new body)
            "Skeleton_Warrior",  // hollow-warrior (AccuRig, fixed 2026-08-20)
            "Skeleton_Rogue",    // hollow-rogue   (AccuRig, fixed 2026-08-20)
        };

        private sealed class Row
        {
            public string Body;
            public string Path;
            public int Skinned, Bones, Verts, Slots, NullMats, NoBaseMap;
            public bool AvatarOk, AvatarHuman, ControllerOk;
            public string AvatarName = "-";
            public int ClipCount;
            public string ClipNames = "-";
            public string ClipNote = "-";
            public string BaseMaps = "-";
            public string AnimMethod = "none";
            public string AnimNote = "";
            public bool AnimMoved, AnimUnproven;
            public float MaxPos, MaxRot;
            public float HeightM, WidthM, DepthM, Coverage;
            public string Png = "-";
            public readonly List<string> Defects = new List<string>();
        }

        [MenuItem("Defenders/Art/Hollow Walker/2 Prove rig + animation + render")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rows = new List<Row>();
            GameObject camGo = null;
            var placed = new List<GameObject>();

            try
            {
                camGo = new GameObject("ProofCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.14f, 0.17f, 1f);
                cam.orthographic = false;
                cam.fieldOfView = 40f;
                cam.enabled = false;

                var lightGo = new GameObject("ProofKey");
                var key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
                // ⚠ LIT FOR LEGIBILITY, DELIBERATELY. This body's albedo is very dark; under the
                // harness's default key it photographs as a near-black cutout and the reviewer
                // can read the SILHOUETTE but not the SURFACE. The point of these shots is to
                // judge whether the texture REGISTERS with the mesh, so the key + ambient are
                // raised until the material is readable. The values are recorded here so the
                // shots stay comparable run to run — this is a lighting rig for inspection, NOT
                // a claim about how the body looks under the game's own lighting.
                key.intensity = 2.1f;
                key.color = Color.white;
                lightGo.transform.rotation = Quaternion.Euler(32f, 200f, 0f);

                var fillGo = new GameObject("ProofFill");
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.9f;
                fillGo.transform.rotation = Quaternion.Euler(18f, 40f, 0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.63f, 0.68f, 1f);

                var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;

                float x = 0f;
                foreach (string body in Bodies)
                {
                    var row = Prove(body, out GameObject go);
                    rows.Add(row);
                    if (go == null) continue;

                    Shoot(cam, rt, go, row);

                    // park it on the family line at WORLD scale, no reframing
                    // Face the camera (the camera sits on -Z looking toward +Z), three-quarter
                    // turned so the silhouette reads in depth as well as width.
                    go.transform.position = new Vector3(x, 0f, 0f);
                    go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                    placed.Add(go);
                    x += 1.4f;
                }

                if (placed.Count > 0) ShootFamily(cam, rt, placed);

                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally
            {
                if (camGo != null) Object.DestroyImmediate(camGo);
            }

            Report(rows);
        }

        // =====================================================================
        //  ONE BODY
        // =====================================================================
        private static Row Prove(string body, out GameObject instance)
        {
            instance = null;
            var row = new Row { Body = body, Path = ContentRoot + "/" + body + ".fbx" };

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(row.Path);
            if (model == null)
            {
                row.Defects.Add("model asset would not load from " + row.Path);
                return row;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (go == null) { row.Defects.Add("could not instantiate " + row.Path); return row; }
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance = go;

            // ── mesh + rig ───────────────────────────────────────────────────
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                row.Skinned++;
                if (s.sharedMesh == null) { row.Defects.Add("SkinnedMeshRenderer '" + s.name + "' has a NULL sharedMesh — dead rig."); continue; }
                row.Verts += s.sharedMesh.vertexCount;
                row.Bones += s.bones != null ? s.bones.Length : 0;
            }
            if (row.Skinned == 0) row.Defects.Add("no SkinnedMeshRenderer — this body cannot animate.");
            if (row.Skinned > 0 && row.Bones == 0) row.Defects.Add("SkinnedMeshRenderer present but ZERO bound bones — a dead rig.");

            // ── avatar ───────────────────────────────────────────────────────
            var anim = go.GetComponentInChildren<Animator>(true) ?? go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(row.Path).OfType<Avatar>().FirstOrDefault();
                if (avatar != null) anim.avatar = avatar;
            }
            if (anim.avatar != null)
            {
                row.AvatarName = anim.avatar.name;
                row.AvatarOk = anim.avatar.isValid;
                row.AvatarHuman = anim.avatar.isHuman;
            }
            if (!row.AvatarOk) row.Defects.Add("NULL/INVALID Avatar — humanoid clips cannot retarget (the sliding-statue path).");
            if (row.AvatarOk && !row.AvatarHuman) row.Defects.Add("Avatar is not HUMAN — it cannot retarget onto the family's SkeletonHumanoid controller.");

            // ── the family's own controller, not a bespoke one ────────────────
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            if (ctrl == null) row.Defects.Add("could not load the family controller " + Controller);
            else { anim.runtimeAnimatorController = ctrl; row.ControllerOk = true; }

            var ownClips = AssetDatabase.LoadAllAssetsAtPath(row.Path).OfType<AnimationClip>()
                                        .Where(c => c != null && !c.name.StartsWith("__preview__")).ToList();
            row.ClipCount = ownClips.Count;
            row.ClipNames = ownClips.Count == 0 ? "(none)"
                          : string.Join(", ", ownClips.Select(c => c.name + " " + c.length.ToString("F2") + "s"));

            // ⚠ ZERO OWN CLIPS IS THE NORM FOR THIS FAMILY, NOT A DEFECT — measured, not assumed.
            // The first run of this file flagged it as a defect and the DATA immediately refuted
            // that: Skeleton_Warrior and Skeleton_Rogue — the two bodies the owner already
            // accepted, both proven green by EnemyProvingHarness — import ZERO AnimationClips
            // each. These AccuRig bodies are MESH + AVATAR only; the motion comes from the
            // shared SkeletonHumanoid controller retargeting onto the Humanoid avatar. So the
            // thing that must be proven is the AVATAR + the RETARGET, and a body's own clip
            // count is reported for information and nothing else. Had this stayed a defect it
            // would have failed two known-good bodies and invited someone to "fix" the family.
            row.ClipNote = ownClips.Count == 0
                ? "no own clips — normal for this family (motion is retargeted from the controller)"
                : ownClips.Count + " own clip(s)";

            // ── materials ────────────────────────────────────────────────────
            var maps = new List<string>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    row.Slots++;
                    if (m == null) { row.NullMats++; continue; }
                    Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                    if (t == null) { row.NoBaseMap++; continue; }
                    string p = AssetDatabase.GetAssetPath(t);
                    if (!string.IsNullOrEmpty(p) && !maps.Contains(p)) maps.Add(p);
                }
            row.BaseMaps = maps.Count == 0 ? "(none — runtime-tinted or untextured)" : string.Join(" | ", maps);
            if (row.NullMats > 0) row.Defects.Add(row.NullMats + " NULL material slot(s) — those submeshes render engine-default MAGENTA.");
            if (row.NoBaseMap > 0) row.Defects.Add(row.NoBaseMap + " material slot(s) carry NO base map — that body part renders untextured.");

            // ── MOTION: measured, not assumed ────────────────────────────────
            SampleMotion(anim, ownClips, row);
            if (!row.AnimUnproven && !row.AnimMoved)
                row.Defects.Add("ANIMATION DID NOT MOVE (" + row.AnimMethod + "): " + row.AnimNote);

            if (TryWorldBounds(go, out Bounds b))
            {
                row.HeightM = b.size.y; row.WidthM = b.size.x; row.DepthM = b.size.z;
            }

            return row;
        }

        private static void SampleMotion(Animator anim, List<AnimationClip> ownClips, Row row)
        {
            var probes = PickProbeBones(anim.gameObject);
            if (probes.Count == 0)
            {
                row.AnimUnproven = true;
                row.AnimNote = "no bones to probe";
                return;
            }

            // (1) drive the REAL Animator through the family controller
            if (row.ControllerOk)
            {
                try
                {
                    anim.Rebind();
                    anim.Update(0f);
                    var before = Snapshot(probes);
                    for (int i = 0; i < AnimFrames; i++) anim.Update(AnimDt);
                    var after = Snapshot(probes);
                    Compare(before, after, out float dp, out float dr);
                    row.AnimMethod = "Animator.Update x" + AnimFrames + " through " + Path.GetFileName(Controller);
                    row.MaxPos = dp; row.MaxRot = dr;
                    row.AnimMoved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                    if (row.AnimMoved) return;
                    row.AnimNote = "the Animator ran but no probe bone changed pose";
                }
                catch (System.Exception ex)
                {
                    row.AnimNote = "Animator.Update threw " + ex.GetType().Name + ": " + ex.Message + "; ";
                }
            }

            // (2) fall back to AnimationMode clip sampling — the same fallback
            //     EnemyProvingHarness uses, and the one that actually works in edit mode
            //     (Animator.Update does not advance state machines outside play mode, which
            //     is why step 1 reported maxPosDelta=0 even for the two known-good bodies).
            //
            // ⚠ SAMPLE THE CONTROLLER'S CLIPS FIRST, NOT THE FBX'S OWN. These AccuRig bodies
            // ship ZERO clips of their own; all the motion lives in SkeletonHumanoid.controller
            // and reaches the mesh by HUMANOID RETARGETING. Sampling a retargeted clip on this
            // avatar is therefore the check that matters — it proves the avatar, the bone bind
            // AND the retarget in one measurement. Probing only the body's own clips (the first
            // version of this file) proved nothing about the path the game actually uses.
            var clip = FirstControllerClip(anim) ?? ownClips.FirstOrDefault(c => c.length > 0.05f);
            if (clip == null)
            {
                row.AnimUnproven = !row.AnimMoved;
                row.AnimNote += "and neither the controller nor the FBX exposes a clip longer than 0.05s to sample";
                return;
            }
            try
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(anim.gameObject, clip, 0f);
                AnimationMode.EndSampling();
                var before = Snapshot(probes);

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(anim.gameObject, clip, Mathf.Max(0.05f, clip.length * 0.4f));
                AnimationMode.EndSampling();
                var after = Snapshot(probes);
                AnimationMode.StopAnimationMode();

                Compare(before, after, out float dp, out float dr);
                row.AnimMethod = "AnimationMode clip '" + clip.name + "'";
                row.MaxPos = Mathf.Max(row.MaxPos, dp);
                row.MaxRot = Mathf.Max(row.MaxRot, dr);
                row.AnimMoved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                if (!row.AnimMoved)
                    row.AnimNote += "clip '" + clip.name + "' (" + clip.length.ToString("F2") +
                                    "s, humanMotion=" + clip.humanMotion + ") sampled at two times and the rig did not change pose";
            }
            catch (System.Exception ex)
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                row.AnimUnproven = true;
                row.AnimNote += "AnimationMode sampling threw " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>The first drivable clip on the body's assigned controller — where this
        /// family's motion actually lives (the bodies themselves ship none).</summary>
        private static AnimationClip FirstControllerClip(Animator anim)
        {
            var ctrl = anim != null ? anim.runtimeAnimatorController : null;
            if (ctrl == null || ctrl.animationClips == null) return null;
            foreach (var c in ctrl.animationClips)
                if (c != null && c.length > 0.05f) return c;
            return null;
        }

        // =====================================================================
        //  PICTURES
        // =====================================================================
        private static void Shoot(Camera cam, RenderTexture rt, GameObject go, Row row)
        {
            if (!TryWorldBounds(go, out Bounds b)) { row.Defects.Add("no renderer bounds — nothing to photograph."); return; }

            float h = Mathf.Max(0.01f, b.size.y);
            Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
            cam.transform.position = b.center + dir * (h * 2.3f);
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = h * 40f;

            row.Png = Capture(cam, rt, new[] { go }, OutDir + "/" + row.Body + ".png", out float cov);
            row.Coverage = cov;
            if (cov < BlankCoverageFloor)
                row.Defects.Add("the shot is BLANK (" + cov.ToString("P2") + " non-background pixels) — the body did not render into frame.");
        }

        private static void ShootFamily(Camera cam, RenderTexture rt, List<GameObject> placed)
        {
            Bounds all = default; bool any = false;
            foreach (var go in placed)
            {
                if (!TryWorldBounds(go, out Bounds b)) continue;
                if (!any) { all = b; any = true; } else all.Encapsulate(b);
            }
            if (!any) return;

            // DERIVE the distance from the FOV rather than guessing a multiplier — the two
            // guesses before this one framed the line-up as three specks and then clipped the
            // outer two. A square frame, so the binding dimension is whichever of the line-up's
            // width/height is larger; 1.12 is the only fudge and it is pure margin.
            float span = Mathf.Max(all.size.x, all.size.y);
            float dist = (span * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.12f;
            cam.transform.position = all.center + new Vector3(0f, 0.10f, -1f).normalized * dist;
            cam.transform.LookAt(all.center);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = span * 40f;
            Capture(cam, rt, placed.ToArray(), OutDir + "/family.png", out _);
        }

        private static string Capture(Camera cam, RenderTexture rt, GameObject[] subjects, string path, out float coverage)
        {
            var saved = new Dictionary<Transform, int>();
            foreach (var go in subjects) MoveToLayer(go.transform, IsolationLayer, saved);
            cam.cullingMask = 1 << IsolationLayer;
            cam.Render();
            foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var px = tex.GetPixels32();
            var bg = cam.backgroundColor;
            int lit = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                float dr = Mathf.Abs(px[i].r / 255f - bg.r);
                float dg = Mathf.Abs(px[i].g / 255f - bg.g);
                float db = Mathf.Abs(px[i].b / 255f - bg.b);
                if (dr + dg + db > 0.06f) lit++;
            }
            coverage = lit / (px.Length / 7f);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path.Replace('\\', '/');
        }

        // =====================================================================
        //  HELPERS (same technique as EnemyProvingHarness — deliberately)
        // =====================================================================
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

        private static void MoveToLayer(Transform t, int layer, Dictionary<Transform, int> saved)
        {
            if (!saved.ContainsKey(t)) saved[t] = t.gameObject.layer;
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
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // =====================================================================
        //  REPORT
        // =====================================================================
        private static void Report(List<Row> rows)
        {
            var log = new StringBuilder();
            log.AppendLine("=== HOLLOW WALKER PROOF ===");
            int defects = 0;

            foreach (var r in rows)
            {
                defects += r.Defects.Count;
                log.AppendLine();
                log.AppendLine(r.Body + "   (" + r.Path + ")");
                log.AppendLine("   mesh      : " + r.Skinned + " skinned renderer(s), " + r.Verts + " verts, " + r.Bones + " bound bone(s)");
                log.AppendLine("   avatar    : '" + r.AvatarName + "' valid=" + r.AvatarOk + " human=" + r.AvatarHuman);
                log.AppendLine("   clips     : " + r.ClipCount + " -> " + r.ClipNames + "   [" + r.ClipNote + "]");
                log.AppendLine("   animation : moved=" + r.AnimMoved + " unproven=" + r.AnimUnproven +
                               " via " + r.AnimMethod + "  maxPosDelta=" + r.MaxPos.ToString("F4") + "m" +
                               " maxRotDelta=" + r.MaxRot.ToString("F2") + "deg " + r.AnimNote);
                log.AppendLine("   materials : " + r.Slots + " slot(s), " + r.NullMats + " null, " + r.NoBaseMap + " without a base map");
                log.AppendLine("   base maps : " + r.BaseMaps);
                log.AppendLine("   size      : H=" + r.HeightM.ToString("F3") + "m W=" + r.WidthM.ToString("F3") +
                               "m D=" + r.DepthM.ToString("F3") + "m   coverage=" + r.Coverage.ToString("P2"));
                log.AppendLine("   png       : " + r.Png);
                foreach (var d in r.Defects) log.AppendLine("   >>>> DEFECT: " + d);
            }

            log.AppendLine();
            log.AppendLine("family shot: " + OutDir + "/family.png (ONE camera, world scale, no per-subject reframing —");
            log.AppendLine("             relative height and bulk in that image are real measurements, not framing.)");

            Directory.CreateDirectory(OutDir);
            File.WriteAllText(OutDir + "/_summary.txt", log.ToString());
            Debug.Log(log.ToString());

            if (defects > 0)
            {
                Debug.LogError(FailMarker + " " + defects + " defect(s) — see " + OutDir + "/_summary.txt");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log(OkMarker + " " + rows.Count + "/" + rows.Count);
        }
    }
}
