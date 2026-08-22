// =============================================================================
// OrcMageProof — the EVIDENCE for the 2026-08-20 Orc_Mage AccuRig intake.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/QA/Prove Orc_Mage (rig + motion + atlas A/B + family compare)
// Batch: -executeMethod DeNelle.Editor.OrcMageProof.RunBatch
// Marker: ORC_MAGE_PROOF_OK <shot>/<total>   |   ORC_MAGE_PROOF_FAIL <n>
// Output: Builds/OrcMageCaps/*.png + SUMMARY.md
//
// WHY A NEW FILE. EnemyProvingHarness only walks ids present in enemies.json, and
// at the moment this runs NO id wears Orc_Mage — so the harness cannot see this
// body. It is NOT modified (it is the check-in instrument for the whole roster);
// this follows its approach instead: isolation layer 31, one shared camera +
// key/fill/flat-ambient so subjects are comparable, framed on RENDERED BOUNDS at
// a distance in units of MODEL HEIGHT, motion proven by SAMPLING BONE POSES rather
// than by observing that a controller is assigned.
//
// THREE QUESTIONS, EACH ANSWERED BY A MEASUREMENT OR A PICTURE (CLAUDE.md §12):
//
//  1. IS IT RIGGED AND DOES IT MOVE? Not "does it have an Animator" — the pose of
//     eight bones spread across the skeleton is snapshotted, the REAL Animator is
//     driven 40 frames through the production controller (EnemyAnimatorFactory.Apply,
//     the same call every spawner makes), and the pose is re-read. A delta below the
//     epsilon is a STATIC rig and fails, however healthy the components look.
//
//  2. WHICH ATLAS BELONGS ON THIS MESH? The delivery ships two DIFFERENT diffuse
//     images (md5 b2bd4950… AccuRig vs f90e74b7… tripo_convert). Filenames cannot
//     settle it and neither can argument — the prettier name belongs to the UNRIGGED
//     convert. Every candidate is rendered ON THE RIGGED MESH, side by side, plus the
//     two STALE atlases (TripoTex/ and OrcTex/ Orc_Mage_basecolor) that were authored
//     for the SUPERSEDED sculpt this delivery replaces, plus a no-atlas CONTROL so
//     "bare" has a known appearance to compare against.
//
//  3. WHAT DOES IT LOOK LIKE NEXT TO THE BODIES IT MIGHT REPLACE? Orc_Shaman and
//     Orc_Berserker are shot AS THEY ARE (both currently bare) at the same framing,
//     so silhouette and bulk can be judged rather than described.
//
// THE FIRST SUBJECT IS THE IMPORTANT ONE. "00_Orc_Mage__AS_IMPORTED" renders the
// prefab with the materials THE IMPORTER BOUND — no atlas is applied by this harness.
// That is the only shot that proves TIER-1 binding, i.e. that the body carries its
// art in edit mode, in a build, and before Start() ever runs, with no dependence on
// the runtime fallback atlases. Every other shot is a hypothesis being tested.
//
// READ-ONLY BY CONSTRUCTION apart from one thing: it never edits an importer, a
// material asset, a .meta, enemies.json or a scene, and works in a SCRATCH scene that
// is never saved (CLAUDE.md §3). It writes only PNGs under Builds/OrcMageCaps/.
// =============================================================================

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class OrcMageProof
    {
        private const string OutDir = "Builds/OrcMageCaps";
        private const int Res = 1024;
        private const int IsolationLayer = 31;
        private const float BlankCoverageFloor = 0.005f;

        private const int AnimFrames = 40;
        private const float AnimDt = 1f / 30f;
        private const float MoveEpsilonMetres = 0.0015f;
        private const float MoveEpsilonDegrees = 0.35f;

        private const string EC = DeNelle.Core.AssetRoots.EnemyContent + "/";
        private const string TMP = EC + "_OrcMageAB_TEMP/";

        private sealed class Subject
        {
            public string Label;
            public string Fbx;
            /// <summary>null + KeepImportedMaterials=false renders the body WHITE — the
            /// deliberate control for "no art". Ignored when KeepImportedMaterials is true.</summary>
            public string BaseTex;
            public string NormalTex;
            /// <summary>Leave the importer's own material bindings alone. Only true for the
            /// shots that are testing TIER-1 binding rather than testing an atlas.</summary>
            public bool KeepImportedMaterials;
            public string Note;
        }

        private sealed class Row
        {
            public string Label, Png, Note, Fbx, BaseTex, BoundMaterials = "-";
            public float Coverage;
            public Vector3 BoundsSize;
            public int SubMeshCount;
            public string Defect;
        }

        private static readonly Subject[] Subjects =
        {
            // ── THE SHIPPED STATE — no atlas applied by this harness ─────────────────
            new Subject { Label = "00_Orc_Mage__AS_IMPORTED", Fbx = EC + "Orc_Mage.fbx",
                          KeepImportedMaterials = true,
                          Note = "THE SHIPPED BINDING. Materials exactly as the importer bound them " +
                                 "(sentinel + InPrefab + per-body Orc_Mage.mat). Proves TIER 1: the art " +
                                 "is on the mesh in edit mode and in a build, with no runtime fallback." },

            // ── THE TEXTURE FORK: two DIFFERENT images shipped in one delivery ───────
            new Subject { Label = "01_Orc_Mage__ACCURIG_atlas_b2bd4950", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "Orc_Mage.fbm/tripo_mat_2256a6d3_Pbr_Diffuse.jpg",
                          NormalTex = EC + "Orc_Mage.fbm/tripo_mat_2256a6d3_Pbr_Normal.jpg",
                          Note = "CANDIDATE A — the AccuRig-baked atlas from orcmage.fbm (md5 b2bd4950, 125 KB). " +
                                 "AccuRig re-baked its own atlas for its own mesh, so this SHOULD be the one " +
                                 "authored against these UVs." },
            new Subject { Label = "02_Orc_Mage__CONVERT_atlas_f90e74b7", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = TMP + "convert_basecolor.jpg",
                          NormalTex = TMP + "convert_normal.jpg",
                          Note = "CANDIDATE B — the tripo_convert atlas (md5 f90e74b7, 76 KB), the one with the " +
                                 "PRETTIER FILENAME (orcmage_basecolor). It belongs to the UNRIGGED convert mesh. " +
                                 "If it smears here, the nicer name is a trap and 01 is settled." },

            // ── THE STALE ATLASES: authored for the SUPERSEDED sculpt ────────────────
            new Subject { Label = "03_Orc_Mage__STALE_TripoTex_atlas", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "TripoTex/Orc_Mage_basecolor.jpg", NormalTex = null,
                          Note = "STALE. EnemyFactory.TryBasecolor probes TripoTex BEFORE OrcTex, so this is what " +
                                 "the RUNTIME fallback would bind for the model name 'Orc_Mage' — but it was " +
                                 "authored for the sculpt this delivery replaced. Shot to show what tier-1 " +
                                 "binding is protecting the body FROM." },
            new Subject { Label = "04_Orc_Mage__STALE_OrcTex_atlas", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "OrcTex/Orc_Mage_basecolor.jpg",
                          NormalTex = EC + "OrcTex/Orc_Mage_normal.jpg",
                          Note = "STALE, second-probe. AtbCombatantSwapper hardcodes this path for Orc_Mage. " +
                                 "Same test as 03 against the older atlas." },

            // ── CONTROL: what 'no art' looks like in this exact rig and lighting ─────
            new Subject { Label = "05_CONTROL_Orc_Mage__no_atlas", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = null, NormalTex = null,
                          Note = "CONTROL — same body, NO atlas. Anything that looks like this has no art." },

            // ── THE FAMILY COMPARISON: the bodies the owner is choosing between ──────
            new Subject { Label = "06_Orc_Shaman__as_is", Fbx = EC + "Orc_Shaman.fbx",
                          KeepImportedMaterials = true,
                          Note = "The body 'orc-shaman' wears TODAY, exactly as it imports. Currently bare " +
                                 "(no atlas exists for it anywhere in the project) — shot for silhouette/bulk." },
            new Subject { Label = "07_Orc_Berserker__as_is", Fbx = EC + "Orc_Berserker.fbx",
                          KeepImportedMaterials = true,
                          Note = "The body 'orc-berserker' and 'orc-raider' wear today, exactly as it imports. " +
                                 "Also bare — shot for silhouette/bulk comparison. NOT being re-pointed." },
        };

        [MenuItem("Defenders/QA/Prove Orc_Mage (rig + motion + atlas A/B + family compare)")]
        public static void RunMenu() => Run();

        public static void RunBatch() => Run();

        public static void Run()
        {
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutDir);

            var log = new StringBuilder();
            log.AppendLine("# Orc_Mage AccuRig intake — evidence  (" +
                           System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            log.AppendLine();

            // ── PART 1: RIG + MOTION, measured ───────────────────────────────────
            var motionDefects = new List<string>();
            ProveMotion(log, motionDefects);

            // ── PART 1b: is the STALE atlas reachable at runtime? ────────────────
            ProveStaleAtlasUnreachable(log, motionDefects);

            // ── PART 2: the pictures ─────────────────────────────────────────────
            var camGo = new GameObject("~OrcMageCapCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 35f;

            // Key light ALONG the view axis so albedo reads (a key from behind renders every
            // AccuRig body as a black silhouette, which cannot answer a texture question),
            // fill opposite, flat ambient so unlit faces are not pure black.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f, 1f);

            Vector3 camDir = new Vector3(0.75f, 0.34f, -1f).normalized;
            var keyGo = new GameObject("~OrcMageCapKey");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.5f;
            keyGo.transform.rotation = Quaternion.LookRotation(-camDir, Vector3.up);

            var fillGo = new GameObject("~OrcMageCapFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.85f, 0.88f, 1f, 1f);
            fillGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(-camDir.x, -0.25f, -camDir.z).normalized, Vector3.up);

            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var rows = new List<Row>();
            int index = 0;
            foreach (var s in Subjects)
            {
                var row = new Row
                {
                    Label = s.Label,
                    Note = s.Note,
                    Fbx = s.Fbx,
                    BaseTex = s.KeepImportedMaterials ? "(the importer's own binding)" : (s.BaseTex ?? "(none — control)")
                };
                rows.Add(row);

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(s.Fbx);
                if (src == null) { row.Defect = "FBX NOT FOUND at " + s.Fbx; continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                if (go == null) { row.Defect = "instantiate failed"; continue; }
                go.transform.position = new Vector3(index * 200f, 0f, 0f);
                index++;

                try
                {
                    if (s.KeepImportedMaterials) RecordBoundMaterials(go, row);
                    else ApplyAtlas(go, shader, s.BaseTex, s.NormalTex, row);
                    Capture(cam, rt, go, row);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo);

            // ── the report ───────────────────────────────────────────────────────
            log.AppendLine("## Renders");
            log.AppendLine();
            log.AppendLine("Each PNG is 2048x1024: LEFT = yaw 0, RIGHT = yaw 180 (same body, turned around),");
            log.AppendLine("so a sculpt facing away is still judgeable. Framed on rendered bounds at 2.3x model");
            log.AppendLine("height, identical camera and lighting for every subject, so bulk and silhouette are");
            log.AppendLine("comparable across shots and only the atlas changes.");
            log.AppendLine();

            int blank = 0, missing = 0;
            foreach (var r in rows)
            {
                if (r.Defect != null && r.Defect.StartsWith("FBX NOT FOUND")) missing++;
                else if (r.Coverage < BlankCoverageFloor) blank++;
                log.AppendLine($"### {r.Label}");
                log.AppendLine($"- mesh  : {r.Fbx}");
                log.AppendLine($"- atlas : {r.BaseTex}");
                log.AppendLine($"- bound : {r.BoundMaterials}");
                log.AppendLine($"- why   : {r.Note}");
                log.AppendLine($"- shot  : {r.Png}  bounds=({r.BoundsSize.x:F2},{r.BoundsSize.y:F2},{r.BoundsSize.z:F2}) " +
                               $"submeshes={r.SubMeshCount} coverage={r.Coverage:P2}");
                if (!string.IsNullOrEmpty(r.Defect)) log.AppendLine($"- DEFECT: {r.Defect}");
                log.AppendLine();
                Debug.Log($"[OrcMageProof] {r.Label}: coverage={r.Coverage:P2} " +
                          $"boundsY={r.BoundsSize.y:F2} submeshes={r.SubMeshCount} -> {r.Png} " +
                          $"{(string.IsNullOrEmpty(r.Defect) ? "" : "DEFECT: " + r.Defect)}");
            }

            string summary = Path.Combine(OutDir, "SUMMARY.md");
            File.WriteAllText(summary, log.ToString());
            Debug.Log("[OrcMageProof]\n" + log);

            int bad = blank + missing + motionDefects.Count;
            if (bad > 0)
            {
                foreach (var d in motionDefects) Debug.LogError("[OrcMageProof] DEFECT: " + d);
                Debug.LogError($"ORC_MAGE_PROOF_FAIL {bad} defect(s) " +
                               $"(blank={blank} missing={missing} motion={motionDefects.Count}) -> {summary}");
            }
            else
            {
                Debug.Log($"ORC_MAGE_PROOF_OK {rows.Count}/{rows.Count} subjects shot, rig+motion proven -> {summary}");
            }
        }

        // =====================================================================
        //  RIG + MOTION — a bone pose that actually changes, or it did not animate
        // =====================================================================
        private static void ProveMotion(StringBuilder log, List<string> defects)
        {
            log.AppendLine("## Rig + motion (measured, not assumed)");
            log.AppendLine();

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(EC + "Orc_Mage.fbx");
            if (src == null)
            {
                defects.Add("Orc_Mage.fbx did not load — no rig to prove");
                log.AppendLine("- **FAIL** Orc_Mage.fbx did not load.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                // ⛔ THE PRODUCTION PATH. Same call every spawner makes to dress an orc body,
                // so this proves the controller the GAME would use, not one chosen here.
                EnemyAnimatorFactory.Apply(go, "Orc_Mage");

                var anim = go.GetComponentInChildren<Animator>(true);
                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                var boneSet = new List<Transform>();
                int verts = 0;
                foreach (var s in smrs)
                {
                    if (s == null) continue;
                    if (s.sharedMesh != null) verts += s.sharedMesh.vertexCount;
                    if (s.bones == null) continue;
                    foreach (var b in s.bones) if (b != null && !boneSet.Contains(b)) boneSet.Add(b);
                }

                log.AppendLine($"- skinned renderers : {smrs.Length}, vertices {verts}");
                log.AppendLine($"- distinct bones    : {boneSet.Count}");
                log.AppendLine($"- Animator          : {(anim == null ? "MISSING" : "present")}, " +
                               $"controller={(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, " +
                               $"avatar={(anim != null && anim.avatar != null ? anim.avatar.name : "NULL")}, " +
                               $"isHuman={(anim != null && anim.isHuman)}, " +
                               $"avatarValid={(anim != null && anim.avatar != null && anim.avatar.isValid)}");

                if (smrs.Length == 0) defects.Add("no SkinnedMeshRenderer — the body imported UNRIGGED");
                if (boneSet.Count == 0) defects.Add("zero bones — dead rig");
                if (anim == null) { defects.Add("no Animator after EnemyAnimatorFactory.Apply"); return; }
                if (anim.runtimeAnimatorController == null)
                    defects.Add("no runtimeAnimatorController — OrcHumanoid_Mage did not resolve");
                if (anim.isHuman && (anim.avatar == null || !anim.avatar.isValid))
                    defects.Add("humanoid mesh with an invalid Avatar — the 'sliding statue' path");

                // Eight probes spread across the skeleton: enough that a partial rig (only the
                // cape moves) still registers, few enough to stay cheap.
                var probes = new List<Transform>();
                int step = Mathf.Max(1, boneSet.Count / 8);
                for (int i = 0; i < boneSet.Count && probes.Count < 8; i += step) probes.Add(boneSet[i]);
                if (probes.Count == 0)
                {
                    defects.Add("no bones to sample — motion cannot be proven");
                    return;
                }

                float dp = 0f, dr = 0f;
                string method = "none";
                string clipNames = "-";

                if (anim.runtimeAnimatorController != null)
                {
                    var ctrl = anim.runtimeAnimatorController;
                    var names = new List<string>();
                    if (ctrl.animationClips != null)
                        foreach (var c in ctrl.animationClips)
                            if (c != null && !names.Contains(c.name)) names.Add(c.name);
                    clipNames = names.Count == 0 ? "(none)" : string.Join(", ", names);

                    var keepCull = anim.cullingMode;
                    try
                    {
                        // With no camera looking at it a culled Animator writes no transforms and
                        // an honest rig reads as frozen. Force it to animate while we measure.
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        anim.applyRootMotion = false;
                        anim.Rebind();
                        anim.Update(0f);

                        var before = Snapshot(probes);
                        for (int i = 0; i < AnimFrames; i++) anim.Update(AnimDt);
                        var after = Snapshot(probes);
                        Compare(before, after, out dp, out dr);
                        method = "Animator.Update x" + AnimFrames;
                    }
                    finally { if (anim != null) anim.cullingMode = keepCull; }
                }

                bool moved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;

                // Fall back to sampling a clip directly if driving the Animator produced nothing.
                if (!moved && anim.runtimeAnimatorController != null)
                {
                    AnimationClip clip = null;
                    foreach (var c in anim.runtimeAnimatorController.animationClips)
                        if (c != null && c.length > 0.05f) { clip = c; break; }

                    if (clip != null)
                    {
                        try
                        {
                            AnimationMode.StartAnimationMode();
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(anim.gameObject, clip, 0f);
                            AnimationMode.EndSampling();
                            var before = Snapshot(probes);

                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(anim.gameObject, clip,
                                Mathf.Max(0.05f, clip.length * 0.4f));
                            AnimationMode.EndSampling();
                            var after = Snapshot(probes);
                            AnimationMode.StopAnimationMode();

                            Compare(before, after, out float dp2, out float dr2);
                            dp = Mathf.Max(dp, dp2);
                            dr = Mathf.Max(dr, dr2);
                            method = "AnimationMode clip '" + clip.name + "'";
                            moved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                        }
                        catch (System.Exception ex)
                        {
                            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                            log.AppendLine("- AnimationMode sampling threw " + ex.GetType().Name + ": " + ex.Message);
                        }
                    }
                }

                log.AppendLine($"- controller clips  : {clipNames}");
                log.AppendLine($"- motion            : {(moved ? "**MOVED**" : "**STATIC**")} via {method} — " +
                               $"max bone delta dPos={dp.ToString("F4", CultureInfo.InvariantCulture)} m, " +
                               $"dRot={dr.ToString("F2", CultureInfo.InvariantCulture)} deg " +
                               $"(threshold {MoveEpsilonMetres} m / {MoveEpsilonDegrees} deg)");
                log.AppendLine();

                if (!moved)
                    defects.Add($"the rig did NOT move: max dPos={dp:F4}m dRot={dr:F2}deg over " +
                                $"{AnimFrames} driven frames — retargeting is not reaching the bones");
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // =====================================================================
        //  THE STALE-ATLAS QUESTION — measured on the object the game builds
        // =====================================================================
        /// <summary>
        /// THE ONE THING THAT COULD STILL GO WRONG, and it is worth a dedicated instrument.
        /// The model name 'Orc_Mage' still resolves TWO atlases that were authored for the
        /// SUPERSEDED sculpt — `TripoTex/Orc_Mage_basecolor` (probed FIRST by
        /// EnemyFactory.TryBasecolor) and `OrcTex/Orc_Mage_basecolor`. Both render as
        /// camouflage scramble on this mesh; shots 03 and 04 are the pictures. If either
        /// reached the body at runtime we would ship exactly the defect this whole import
        /// was written to avoid.
        ///
        /// The runtime path is `EnemyFactory` -> `TripoMaterialFixer.SetFallbackTexture(...)`,
        /// and the fixer resolves its albedo in this documented order
        /// (Assets/_Modules/Core/TripoMaterialFixer.cs, inside the per-slot rebuild):
        ///
        ///     if (src.HasProperty("_MainTex"))                tex = src.GetTexture("_MainTex");
        ///     if (tex == null &amp;&amp; src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
        ///     ...
        ///     if (tex == null &amp;&amp; fallbackTex != null)        tex = fallbackTex;   // &lt;-- the stale atlas
        ///
        /// So the fallback is reachable ONLY through `tex == null` — i.e. only if the slot's
        /// own material carries NO base map. That is a PRECONDITION, and a precondition is
        /// measurable without running the game. This builds the enemy through the production
        /// chokepoint (EnemyFactory.Build) and reads, off the live object, whether every
        /// renderer slot already carries a source map. Every slot mapped = the guard is false
        /// on every slot = the stale atlas cannot be bound.
        ///
        /// ⚠ WHAT THIS DOES NOT PROVE, stated rather than glossed: the fixer's Start() does not
        /// fire in edit mode, so the final PIXEL is confirmed only by a play/device session.
        /// What is proven here is the precondition that decides it.
        /// </summary>
        private static void ProveStaleAtlasUnreachable(StringBuilder log, List<string> defects)
        {
            log.AppendLine("## Is the STALE atlas reachable at runtime?");
            log.AppendLine();

            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    log.AppendLine("- could not parse enemies.json: " + ex.Message);
                    return;
                }
            }

            EnemyDef def = null;
            if (catalog != null && catalog.Enemies != null)
                foreach (var d in catalog.Enemies)
                    if (d != null && d.Id == "orc-shaman") { def = d; break; }

            if (def == null)
            {
                log.AppendLine("- no 'orc-shaman' row found in enemies.json — nothing to measure.");
                return;
            }

            log.AppendLine($"- enemies.json row 'orc-shaman' modelKey = **{def.ModelKey}**");
            log.AppendLine($"- EnemyFactory.ModelForEnemy resolves it to **{EnemyFactory.ModelForEnemy(def)}**");

            GameObject go = null;
            try
            {
                var enemy = EnemyFactory.Build(def, new Vector3(0f, 0f, 900f), Quaternion.identity, null);
                go = enemy != null ? enemy.gameObject : null;
                if (go == null)
                {
                    defects.Add("EnemyFactory.Build returned NULL for orc-shaman");
                    log.AppendLine("- **FAIL** EnemyFactory.Build returned NULL.");
                    return;
                }

                var fixer = go.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>(true);
                if (fixer == null)
                {
                    log.AppendLine("- no TripoMaterialFixer is attached at all, so no fallback atlas exists to bind.");
                }
                else
                {
                    // SerializedObject, not reflection (CLAUDE.md §10): these are [SerializeField]
                    // fields and this is the sanctioned editor API for reading them.
                    var so = new SerializedObject(fixer);
                    string fb = so.FindProperty("_fallbackTextureName") != null
                        ? so.FindProperty("_fallbackTextureName").stringValue : "<field absent>";
                    string forced = so.FindProperty("_forcedTextureName") != null
                        ? so.FindProperty("_forcedTextureName").stringValue : "<field absent>";
                    var tintProp = so.FindProperty("_hasFallbackTint");
                    log.AppendLine($"- TripoMaterialFixer attached. fallbackTextureName = " +
                                   $"`{(string.IsNullOrEmpty(fb) ? "(none)" : fb)}`, " +
                                   $"forcedTextureName = `{(string.IsNullOrEmpty(forced) ? "(none)" : forced)}`, " +
                                   $"hasFallbackTint = {(tintProp != null && tintProp.boolValue)}");
                    if (!string.IsNullOrEmpty(forced))
                        defects.Add($"a FORCED texture '{forced}' is set — forced WINS over the source map " +
                                    "unconditionally, so the body would wear it regardless of tier-1 binding");
                }

                int slots = 0, mapped = 0, unmapped = 0;
                var detail = new List<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r.sharedMaterials == null) continue;
                    if (r.gameObject.name == "PlaceholderCapsule") continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        slots++;
                        Texture t = null;
                        if (m != null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                        if (t == null && m != null && m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
                        if (t == null) { unmapped++; detail.Add($"{(m == null ? "<NULL mat>" : m.name)} -> NO MAP"); }
                        else { mapped++; detail.Add($"{m.name} -> {AssetDatabase.GetAssetPath(t)}"); }
                    }
                }

                log.AppendLine($"- renderer slots on the built body: **{slots}**, carrying their own base map: " +
                               $"**{mapped}**, with NO map: **{unmapped}**");
                foreach (var d in detail) log.AppendLine($"    - {d}");

                if (unmapped > 0)
                {
                    defects.Add($"{unmapped}/{slots} slot(s) carry NO base map — for those slots " +
                                "`tex == null` is TRUE and the STALE atlas WOULD be bound at runtime");
                    log.AppendLine();
                    log.AppendLine("- **VERDICT: REACHABLE.** At least one slot has no map of its own, so the " +
                                   "`tex == null && fallbackTex != null` guard opens and the stale atlas binds. " +
                                   "Do NOT ship this wiring.");
                }
                else
                {
                    log.AppendLine();
                    log.AppendLine("- **VERDICT: UNREACHABLE.** Every renderer slot resolves its own base map from " +
                                   "the body's own `.fbm`, so `tex == null` is FALSE on every slot and the " +
                                   "`tex == null && fallbackTex != null` branch never runs. The stale " +
                                   "TripoTex/OrcTex `Orc_Mage_basecolor` atlases are dead paths for this body — " +
                                   "which is why they can be left on disk untouched for the assets that still " +
                                   "assert they exist.");
                }
                log.AppendLine();
            }
            catch (System.Exception ex)
            {
                defects.Add("exception while measuring the runtime atlas path: " + ex.GetType().Name + ": " + ex.Message);
                log.AppendLine("- **EXCEPTION** " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        private static List<KeyValuePair<Vector3, Quaternion>> Snapshot(List<Transform> probes)
        {
            var list = new List<KeyValuePair<Vector3, Quaternion>>(probes.Count);
            foreach (var t in probes)
            {
                list.Add(t != null
                    ? new KeyValuePair<Vector3, Quaternion>(t.localPosition, t.localRotation)
                    : new KeyValuePair<Vector3, Quaternion>(Vector3.zero, Quaternion.identity));
            }
            return list;
        }

        private static void Compare(List<KeyValuePair<Vector3, Quaternion>> a,
                                    List<KeyValuePair<Vector3, Quaternion>> b,
                                    out float maxPos, out float maxRot)
        {
            maxPos = 0f; maxRot = 0f;
            int n = Mathf.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
            {
                maxPos = Mathf.Max(maxPos, Vector3.Distance(a[i].Key, b[i].Key));
                maxRot = Mathf.Max(maxRot, Quaternion.Angle(a[i].Value, b[i].Value));
            }
        }

        // =====================================================================
        //  MATERIALS
        // =====================================================================
        /// <summary>Report what the IMPORTER bound, without changing it. This is the whole
        /// evidence for tier-1: a material asset path here means the art travels with the mesh.</summary>
        private static void RecordBoundMaterials(GameObject go, Row row)
        {
            var names = new List<string>();
            int slots = 0, nulls = 0, noMap = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterials == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    slots++;
                    if (m == null) { nulls++; names.Add("<NULL>"); continue; }
                    Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                    if (t == null) noMap++;
                    string mp = AssetDatabase.GetAssetPath(m);
                    string tp = t != null ? AssetDatabase.GetAssetPath(t) : "<no base map>";
                    names.Add($"{m.name} [{(string.IsNullOrEmpty(mp) ? "embedded in fbx" : mp)}] -> {tp}");
                }
            }
            row.SubMeshCount = slots;
            row.BoundMaterials = names.Count == 0 ? "(no renderers)" : string.Join(" ; ", names);
            if (nulls > 0) row.Defect = $"{nulls}/{slots} NULL material slot(s) — renders engine-default magenta";
            else if (noMap == slots && slots > 0) row.BoundMaterials += "   <<< NO BASE MAP ON ANY SLOT (bare body)";
        }

        /// <summary>Rebuild every material as URP/Lit and bind the CANDIDATE atlas. That is the
        /// point: the atlas under test is bound explicitly, so the picture answers "does THIS map
        /// belong on THIS mesh" and nothing else. A null atlas leaves the body white — the control.</summary>
        private static void ApplyAtlas(GameObject go, Shader shader, string basePath, string normalPath, Row row)
        {
            Texture2D baseTex = string.IsNullOrEmpty(basePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D nrmTex = string.IsNullOrEmpty(normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (!string.IsNullOrEmpty(basePath) && baseTex == null)
                row.Defect = "atlas NOT FOUND at " + basePath;

            var mat = new Material(shader) { name = "~OrcMageCapMat" };
            if (baseTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
            }
            if (nrmTex != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrmTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            int sub = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                int n = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
                sub += n;
                var mats = new Material[Mathf.Max(1, n)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            row.SubMeshCount = sub;
            row.BoundMaterials = baseTex != null
                ? "~OrcMageCapMat -> " + basePath
                : "~OrcMageCapMat -> (no base map, white control)";
        }

        // =====================================================================
        //  CAPTURE — two views composited into one PNG
        // =====================================================================
        private static void Capture(Camera cam, RenderTexture rt, GameObject go, Row row)
        {
            if (!TryWorldBounds(go, out Bounds b))
            {
                row.Defect = "no renderer bounds — nothing to photograph (the body did not render)";
                return;
            }
            row.BoundsSize = b.size;

            var saved = new Dictionary<Transform, int>();
            MoveToLayer(go.transform, IsolationLayer, saved);
            cam.cullingMask = 1 << IsolationLayer;

            var sheet = new Texture2D(Res * 2, Res, TextureFormat.RGB24, false);
            float coverage = 0f;
            Quaternion baseRot = go.transform.rotation;

            for (int view = 0; view < 2; view++)
            {
                go.transform.rotation = baseRot * Quaternion.Euler(0f, view * 180f, 0f);
                if (!TryWorldBounds(go, out Bounds vb)) vb = b;

                float h = Mathf.Max(0.01f, vb.size.y);
                Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
                cam.transform.position = vb.center + dir * (h * 2.3f);
                cam.transform.LookAt(vb.center);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = h * 40f;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                sheet.SetPixels(view * Res, 0, Res, Res, tex.GetPixels());
                if (view == 0) coverage = Coverage(tex, cam.backgroundColor);
                Object.DestroyImmediate(tex);
            }

            go.transform.rotation = baseRot;
            sheet.Apply();
            foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

            row.Coverage = coverage;
            string path = Path.Combine(OutDir, row.Label + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            row.Png = path.Replace('\\', '/');

            if (coverage < BlankCoverageFloor && string.IsNullOrEmpty(row.Defect))
                row.Defect = $"the shot is BLANK ({coverage:P2} non-background) — the body did not render into frame";
        }

        private static float Coverage(Texture2D tex, Color bg)
        {
            var px = tex.GetPixels32();
            int lit = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                float dr = Mathf.Abs(px[i].r / 255f - bg.r);
                float dg = Mathf.Abs(px[i].g / 255f - bg.g);
                float db = Mathf.Abs(px[i].b / 255f - bg.b);
                if (dr + dg + db > 0.06f) lit++;
            }
            return lit / (float)(px.Length / 7f);
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
