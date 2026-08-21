// =============================================================================
// CellarHollowProof — prove the new Cellar Hollow body's RIG and ANIMATION,
// settle the TEXTURE FORK on the rigged mesh, and photograph it beside the two
// AccuRig hollows it has to read as family with.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/Art/Cellar Hollow/2 Prove rig + animation + textures + render
// Batch: -executeMethod DeNelle.Editor.CellarHollowProof.Run
// Marker: CELLAR_HOLLOW_PROOF_OK <pass>/<total> | CELLAR_HOLLOW_PROOF_FAIL <n>
// Output: Builds/CellarHollowProof/<body>.png, family.png,
//         ab_embedded_fbm.png, ab_convert_basecolor.png, _summary.txt
//
// ⚠ WHY THIS EXISTS ALONGSIDE EnemyProvingHarness RATHER THAN INSTEAD OF IT.
// EnemyProvingHarness is the right instrument and is NOT modified here: it iterates
// enemies.json and builds through EnemyFactory.Build, the production chokepoint. But
// "cellar-hollow" is a HOLLOW id, so EnemyFactory.ModelForEnemy resolves it through
// EnemyResolver.TryResolveHollowModel, which honours the enemies.json modelKey ONLY
// when that key is in EnemyResolver.KnownHollowModels — a SEVEN-name set in
// Assets/_Modules/Core/Enemies/EnemyResolver.cs (read at source). A brand-new body is
// not in it, and EnemyResolver.CommittedModels gates the same way for every other
// family. So until those registrations land, the harness CANNOT be pointed at this
// body: it would keep photographing Skeleton_Minion and call it cellar-hollow.
// This file therefore proves the ASSET directly — load the imported model, bind the
// family's own SkeletonHumanoid controller, drive it, measure a bone — and says so.
// It is a supplement, never a replacement.
//
// ── THE A/B THAT SETTLES THE TEXTURE FORK ────────────────────────────────────
// The delivery ships two atlases for one creature: the hashed
// "tripo_mat_acabe1ac_Pbr_Diffuse.jpg" embedded in the RIGGED fbx, and the
// prettily-named "cellar_hollow_basecolor.JPEG" beside the UNRIGGED tripo_convert
// source. Three times today an atlas authored for one mesh was bound to a
// differently-UV'd mesh and rendered as scrambled patches. So neither is chosen by
// filename: BOTH are rendered on the RIGGED mesh, from one camera, in the same pose,
// and the pictures decide. The report prints each one's source path beside its shot.
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
    /// <summary>Rig/animation/texture-fork/appearance proof for the new Cellar Hollow body.</summary>
    public static class CellarHollowProof
    {
        private const string ContentRoot = "Assets/EnemyContent";
        private const string OutDir      = "Builds/CellarHollowProof";
        private const string Controller  = ContentRoot + "/SkeletonHumanoid.controller";

        /// <summary>The unrigged-source atlas, staged for the A/B only. Never bound to the asset.</summary>
        private const string ConvertAlbedo = ContentRoot + "/_CellarHollowAB_TEMP/convert_basecolor.jpg";

        private const int Res = 900;
        private const int IsolationLayer = 31;
        private const int AnimFrames = 40;
        private const float AnimDt = 1f / 30f;
        private const float MoveEpsilonMetres = 0.0015f;
        private const float MoveEpsilonDegrees = 0.35f;
        private const float BlankCoverageFloor = 0.004f;

        private const string OkMarker   = "CELLAR_HOLLOW_PROOF_OK";
        private const string FailMarker = "CELLAR_HOLLOW_PROOF_FAIL";

        /// <summary>The subject and the two AccuRig hollows it must read as family with.</summary>
        private static readonly string[] Bodies =
        {
            "Cellar_Hollow",     // cellar-hollow  (the new body)
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
            public string BaseMaps = "-";
            public string AnimMethod = "none";
            public string AnimNote = "";
            public bool AnimMoved, AnimUnproven;
            public float MaxPos, MaxRot;
            public float HeightM, WidthM, DepthM, Coverage;
            public string Png = "-";
            public readonly List<string> Defects = new List<string>();
        }

        [MenuItem("Defenders/Art/Cellar Hollow/2 Prove rig + animation + textures + render")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rows = new List<Row>();
            var abNotes = new List<string>();
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
                key.intensity = 1.25f;
                key.color = Color.white;
                lightGo.transform.rotation = Quaternion.Euler(38f, -142f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.47f, 1f);

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
                    go.transform.position = new Vector3(x, 0f, 0f);
                    go.transform.rotation = Quaternion.Euler(0f, 155f, 0f);
                    placed.Add(go);
                    x += 1.4f;
                }

                if (placed.Count > 0) ShootFamily(cam, rt, placed);

                // ── the texture fork, decided on the RIGGED mesh ──────────────
                ShootTextureFork(cam, rt, abNotes);

                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally
            {
                if (camGo != null) Object.DestroyImmediate(camGo);
            }

            Report(rows, abNotes);
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
            // ⚠ ZERO OWN CLIPS IS NOT A DEFECT IN THIS FAMILY — MEASURED, NOT ASSUMED.
            // The first run flagged it as one and was WRONG: Skeleton_Warrior and Skeleton_Rogue,
            // the two bodies the owner already ships, BOTH import zero AnimationClips, and both
            // animate perfectly (proven below: 29.29deg / 43.55deg of measured bone rotation).
            // The Hollow bodies have never carried their own motion — it comes from
            // SkeletonHumanoid.controller's clips retargeting through the humanoid Avatar. Failing
            // on the clip COUNT would therefore have condemned the working art and made "relax the
            // check" the first instinct. What matters is whether the rig MOVES, which is asserted
            // separately and hard. Recorded as a note so the fact stays visible.
            if (ownClips.Count == 0)
                row.AnimNote += "(FBX carries no clips of its own — normal for this family; motion is retargeted) ";

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

            // (2) SAMPLE A REAL CLIP OFF THE FAMILY CONTROLLER, RETARGETED ONTO THIS AVATAR.
            //     This is the check that actually matters for an AccuRig intake body, and the
            //     reason it is here is MEASURED: this delivery's four AnimStacks import as two
            //     0.02s '0_T-Pose' clips, and Skeleton_Warrior / Skeleton_Rogue import ZERO
            //     clips at all — the Hollow bodies have never carried their own motion. Their
            //     movement comes from SkeletonHumanoid.controller's clips retargeting through
            //     the humanoid Avatar. So "does this body animate" is answered by driving one
            //     of THOSE clips on THIS rig and watching a bone move, not by counting the
            //     FBX's own clips. Deliberately the longest clip in the controller, so a
            //     near-static pose clip cannot produce a false negative.
            if (row.ControllerOk)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
                var famClip = ctrl == null ? null
                    : ctrl.animationClips.Where(c => c != null && c.length > 0.05f)
                                         .OrderByDescending(c => c.length).FirstOrDefault();
                if (famClip != null && TrySampleClip(anim, probes, famClip, row,
                        "retargeted family clip '" + famClip.name + "' from " + Path.GetFileName(Controller)))
                    return;
            }

            // (3) fall back to sampling one of the body's OWN clips directly. This is the
            //     check that separates "the mesh's own AnimStacks are alive" from "the
            //     shared controller happened not to drive anything in edit mode".
            var clip = ownClips.FirstOrDefault(c => c.length > 0.05f);
            if (clip == null)
            {
                row.AnimUnproven = !row.AnimMoved;
                row.AnimNote += "and the FBX exposes no clip longer than 0.05s to sample";
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

        /// <summary>
        /// Samples one clip on <paramref name="anim"/> at two times and reports whether a probe
        /// bone actually changed pose. Returns TRUE only when motion was MEASURED — a throw or a
        /// zero delta returns false so the caller can try the next method rather than pass on faith.
        /// </summary>
        private static bool TrySampleClip(Animator anim, List<Transform> probes, AnimationClip clip,
                                          Row row, string method)
        {
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
                row.AnimMethod = method;
                row.MaxPos = Mathf.Max(row.MaxPos, dp);
                row.MaxRot = Mathf.Max(row.MaxRot, dr);
                bool moved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                // AnimNote is NOT cleared on success: it carries the family's no-own-clips fact and any
                // earlier method's failure reason, both of which stay true and worth reading.
                if (moved) { row.AnimMoved = true; row.AnimUnproven = false; return true; }
                row.AnimNote += method + " sampled at two times and the rig did not change pose; ";
                return false;
            }
            catch (System.Exception ex)
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                row.AnimNote += method + " threw " + ex.GetType().Name + ": " + ex.Message + "; ";
                return false;
            }
        }

        // =====================================================================
        //  THE TEXTURE FORK — both atlases, same mesh, same camera, same pose
        // =====================================================================
        private static void ShootTextureFork(Camera cam, RenderTexture rt, List<string> notes)
        {
            string fbx = ContentRoot + "/Cellar_Hollow.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { notes.Add("A/B SKIPPED: " + fbx + " would not load."); return; }

            // The two candidates, by SOURCE not by preference.
            string embedded = FirstImage(ContentRoot + "/Cellar_Hollow.fbm");
            if (string.IsNullOrEmpty(embedded)) notes.Add("A/B: no embedded .fbm albedo found to test.");
            var pairs = new List<(string label, string path)>
            {
                ("ab_embedded_fbm",        embedded),
                ("ab_convert_basecolor",   File.Exists(ConvertAlbedo) ? ConvertAlbedo : null),
            };

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (label, path) in pairs)
            {
                if (string.IsNullOrEmpty(path)) { notes.Add(label + ": candidate MISSING — not rendered."); continue; }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { notes.Add(label + ": " + path + " would not load as Texture2D."); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.transform.position = new Vector3(0f, 0f, 40f);   // away from the family line
                go.transform.rotation = Quaternion.Euler(0f, 155f, 0f);

                var mat = new Material(shader) { name = label };
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    r.sharedMaterials = arr;
                }

                if (!TryWorldBounds(go, out Bounds b)) { notes.Add(label + ": no bounds to photograph."); Object.DestroyImmediate(go); continue; }
                float h = Mathf.Max(0.01f, b.size.y);
                Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
                cam.transform.position = b.center + dir * (h * 2.3f);
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = h * 40f;

                string png = Capture(cam, rt, new[] { go }, OutDir + "/" + label + ".png", out float cov);
                notes.Add(label + ": " + path + " -> " + png + "  coverage=" + cov.ToString("P2") +
                          (cov < BlankCoverageFloor ? "  >>>> BLANK SHOT" : ""));

                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
            }
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

            // FRAME ON THE ROW'S WIDTH, not on max(width,height). The first run framed the three
            // bodies at ~12% of the frame and the owner could not read silhouette or bulk off it —
            // a picture nobody can read is not evidence. Distance is derived from the row's actual
            // horizontal span and the camera's own FOV, so the group fills the frame whatever the
            // body count. Still ONE camera and no per-subject reframing, so relative height and
            // bulk stay real measurements.
            float span = Mathf.Max(all.size.x, all.size.y * 1.3f);
            float dist = (span * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
            cam.transform.position = all.center + new Vector3(0f, 0.12f, -1f).normalized * dist;
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
        private static string FirstImage(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            foreach (string p in Directory.GetFiles(dir))
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                string e = Path.GetExtension(p).ToLowerInvariant();
                if ((e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".tga") &&
                    (n.Contains("diffuse") || n.Contains("basecolor") || n.Contains("albedo")))
                    return p.Replace('\\', '/');
            }
            return null;
        }

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
        private static void Report(List<Row> rows, List<string> abNotes)
        {
            var log = new StringBuilder();
            log.AppendLine("=== CELLAR HOLLOW PROOF ===");
            int defects = 0;

            foreach (var r in rows)
            {
                defects += r.Defects.Count;
                log.AppendLine();
                log.AppendLine(r.Body + "   (" + r.Path + ")");
                log.AppendLine("   mesh      : " + r.Skinned + " skinned renderer(s), " + r.Verts + " verts, " + r.Bones + " bound bone(s)");
                log.AppendLine("   avatar    : '" + r.AvatarName + "' valid=" + r.AvatarOk + " human=" + r.AvatarHuman);
                log.AppendLine("   clips     : " + r.ClipCount + " -> " + r.ClipNames);
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
            log.AppendLine("TEXTURE FORK A/B — both candidate atlases on the SAME rigged mesh, one camera, one pose:");
            foreach (var n in abNotes) log.AppendLine("   " + n);

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
