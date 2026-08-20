// =============================================================================
// EnemyProvingHarness — "build every enemy, watch it move, photograph it".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
// Menu:  Defenders/QA/Prove Every Enemy (rig + anim + textures + picture)
// Batch: -executeMethod DeNelle.Editor.EnemyProvingHarness.RunBatch
// Marker: ENEMY_PROVING_OK <pass>/<total>   |   ENEMY_PROVING_FAIL <n> defect(s)
// Output: Builds/EnemyCaps/<id>.png  +  Builds/EnemyCaps/_summary.txt
//
// OWNER REQUEST (2026-08-20): "build each enemy in a scene and see them go through
// motions, can you test that and tell if rigging is working and if animation is
// working, and textures are for each one" ... "so we dont have more fallout".
//
// WHY IT EXISTS. The enemy art seam moved three times in one day (the string-overload
// skin through StructureAssetLoader, the deletion of Assets/Resources/Enemies, the
// removal of the blocking WaitForCompletion that had been accidentally carrying the
// enemy addresses, and the re-pack into five per-family bundles). Every one of those
// changes was invisible to a compile gate and to every asset-level oracle, and the
// first instrument that noticed was the owner's device showing tinted capsules. This
// harness is the instrument that should have noticed first.
//
// WHAT MAKES IT EVIDENCE RATHER THAN A CLAIM (CLAUDE.md §12):
//  1. IT BUILDS THROUGH THE PRODUCTION PATH. EnemyFactory.Build — the same single
//     enemy-creation chokepoint every spawner uses — not a bespoke Instantiate. A
//     harness that builds enemies its own way proves nothing about the game. Editor
//     asset resolution goes through EnemyAssetLoader's sanctioned #if UNITY_EDITOR
//     resolver; the runtime path is not touched and nothing here blocks on a bundle.
//  2. IT PROVES MOTION, NOT INTENT. "A controller is assigned" is not evidence of
//     animation. We drive the real Animator (Rebind + Update across ~40 frames) and
//     assert a BONE'S LOCAL POSE ACTUALLY CHANGES. If the Animator cannot be driven in
//     edit mode we fall back to sampling the controller's clip through AnimationMode,
//     and if neither can run the enemy is reported UNPROVEN — never silently passed.
//  3. IT TAKES A PICTURE, and prints the measured numbers beside it. Follows
//     StorefrontOrientationCapture exactly: ONE shared camera and light, the subject
//     isolated on a spare culling layer so a neighbour cannot photobomb it, framed on
//     RENDERED BOUNDS (not the transform) at a distance in units of MODEL HEIGHT so
//     every subject fills the same fraction of frame. The image and the measurement
//     live in the same artifact so they cannot drift apart.
//  4. IT NAMES THE ART. Every row prints the RESOLVED MODEL and the SOURCE ASSET PATH
//     it actually loaded from, so a question like the owner's "there was also a skeleton
//     from kaykat" is answered by a path, not by an argument. This harness NEVER
//     substitutes a model — a model choice is a creative decision. It reports.
//
// FAIL CONDITIONS (any one fails the run):
//   • placeholder capsule / no renderable mesh   — the body did not load
//   • a renderer with a NULL material slot       — renders engine-default MAGENTA
//   • dead rig: SkinnedMeshRenderer with null sharedMesh or zero bones
//   • no Animator, or an Animator with no runtimeAnimatorController
//   • humanoid mesh with a null/invalid Avatar   — the "sliding statue" path
//   • animation sampled and NOTHING MOVED
// UNPROVEN (loud, not fatal): motion could not be sampled at all in edit mode.
// =============================================================================

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EnemyProvingHarness
    {
        private const string OutDir = "Builds/EnemyCaps";
        private const int Res = 900;

        /// <summary>Spare layer used to photograph one enemy with nothing in front of it.
        /// Same trick, same reason, as StorefrontOrientationCapture.</summary>
        private const int IsolationLayer = 31;

        /// <summary>Frames of Animator.Update to drive before re-reading the pose.</summary>
        private const int AnimFrames = 40;
        private const float AnimDt = 1f / 30f;

        /// <summary>Below this, a bone did not meaningfully move (metres / degrees).</summary>
        private const float MoveEpsilonMetres = 0.0015f;
        private const float MoveEpsilonDegrees = 0.35f;

        /// <summary>Under this fraction of non-background pixels the shot is blank —
        /// i.e. we photographed nothing, which is itself a finding.</summary>
        private const float BlankCoverageFloor = 0.004f;

        /// <summary>Art roots that are GITIGNORED: a material reaching one of these breaks
        /// on a fresh clone. Listed by prefix so the check needs no git.</summary>
        private static readonly string[] GitignoredArtRoots =
        {
            "Assets/polyperfect/",
            "Assets/Quaternius/",
            "Assets/Blink/",
            "Assets/Art/TripoStructures/",
            "Assets/Models/KayKit/",
            "Assets/Resources/Structures/",
        };

        // ── one row of the report ────────────────────────────────────────────────
        private sealed class Row
        {
            public string Id, Family, Model, SourcePath;
            /// <summary>The model the BODY ACTUALLY WEARS, read back off the built visual child —
            /// which is NOT always the requested one. The Wildlands deferral gate inside
            /// EnemyFactory.Build substitutes a Hollow body for a deferred id, so 'orc-raider'
            /// asks for Orc_Berserker and wears Skeleton_Warrior. Reporting only the request
            /// would have printed a model the player never sees.</summary>
            public string BuiltModel = "?";
            public string ArtSource = "";
            public bool Capsule;
            public int SkinnedCount, StaticMeshCount, BoneCount;
            public bool MeshOk, AnimatorPresent, AvatarOk, ControllerOk, IsHuman;
            public string ControllerName = "-";
            public string AvatarName = "-";
            public int MatSlots, NullMats, MissingMainTex;
            public bool RuntimeTinted;
            public string ShaderNames = "-";
            public string TextureNames = "-";
            public List<string> AssetPaths = new List<string>();
            public string AnimMethod = "none";
            public float MaxPosDelta, MaxRotDelta;
            public bool AnimMoved, AnimUnproven;
            public string AnimNote = "";
            public Vector3 BoundsSize;
            public float Coverage;
            public string Png = "-";
            public List<string> Defects = new List<string>();
            public List<string> Notes = new List<string>();

            public bool RigOk => MeshOk && BoneCount > 0 && AnimatorPresent && ControllerOk && AvatarOk;
            public bool TexOk => NullMats == 0 && MatSlots > 0;
        }

        [MenuItem("Defenders/QA/Prove Every Enemy (rig + anim + textures + picture)")]
        public static void ProveAllMenu() => ProveAll();

        /// <summary>Batchmode entry point.</summary>
        public static void RunBatch() => ProveAll();

        public static void ProveAll()
        {
            var rows = new List<Row>();
            var log = new StringBuilder();

            // ── the roster, read exactly as the game reads it ────────────────────
            EnemyCatalog catalog = null;
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EnemyProving] ENEMY_PROVING_FAIL enemies.json parse error: {ex.Message}");
                    return;
                }
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                Debug.LogError("[EnemyProving] ENEMY_PROVING_FAIL enemies.json produced 0 EnemyDef rows.");
                return;
            }

            // A scratch scene. NEVER saved, never one of the curated scenes (CLAUDE.md §3).
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutDir);

            // ONE camera + ONE light for every subject. Reusing them is the whole point:
            // a per-subject rig makes the images incomparable.
            var camGo = new GameObject("~EnemyCapCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 35f;

            // LIGHTING IS EVIDENCE, NOT DECORATION. The first run of this harness shot every
            // subject from the camera's side while the single key light came from BEHIND them,
            // so the AccuRig bodies rendered as near-black silhouettes — a picture that cannot
            // answer "is the texture right", which is half of what the owner asked. An empty
            // scene also has NO ambient and NO skybox, so unlit faces go to pure black. Key
            // light along the camera's own view direction + a fill from the opposite side +
            // flat ambient, so the albedo actually reads.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f, 1f);

            Vector3 camDir = new Vector3(0.75f, 0.34f, -1f).normalized;   // subject -> camera
            var lightGo = new GameObject("~EnemyCapKeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightGo.transform.rotation = Quaternion.LookRotation(-camDir, Vector3.up);

            var fillGo = new GameObject("~EnemyCapFillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.85f, 0.88f, 1f, 1f);
            fillGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(-camDir.x, -0.25f, -camDir.z).normalized, Vector3.up);

            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            int index = 0;
            foreach (var def in catalog.Enemies)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                // The schema-doc placeholder row keys its id with spaces (mirrors CheckEnemies).
                if (def.Id.Contains(" ")) continue;

                var row = new Row { Id = def.Id, Family = string.IsNullOrEmpty(def.Family) ? "hollow" : def.Family };
                GameObject go = null;
                try
                {
                    // Spread the subjects so a stray light/shadow neighbour cannot confuse a
                    // measurement; isolation handles the camera, spacing handles everything else.
                    Vector3 pos = new Vector3(index * 25f, 0f, 0f);

                    // ⛔ THE PRODUCTION PATH. Same call every spawner makes.
                    Enemy enemy = EnemyFactory.Build(def, pos, Quaternion.identity, null);
                    go = enemy != null ? enemy.gameObject : null;
                    if (go == null)
                    {
                        row.Defects.Add("EnemyFactory.Build returned NULL — no body at all");
                        rows.Add(row);
                        index++;
                        continue;
                    }

                    row.Model = EnemyFactory.ModelForEnemy(def);
                    var prefab = DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(row.Model);
                    row.SourcePath = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "<UNRESOLVED>";
                    if (prefab == null)
                        row.Defects.Add($"EnemyAssetLoader could not resolve 'Enemies/{row.Model}' in the editor");

                    InspectBody(go, row);

                    // The body may not be the model that was asked for. Say so, and re-point the
                    // source path at what the player actually sees.
                    if (!string.IsNullOrEmpty(row.BuiltModel) && row.BuiltModel != row.Model && !row.Capsule)
                    {
                        var builtPrefab = DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(row.BuiltModel);
                        string builtPath = builtPrefab != null ? AssetDatabase.GetAssetPath(builtPrefab) : row.SourcePath;
                        row.Notes.Add($"SUBSTITUTED: requested model '{row.Model}' but the built body wears " +
                                      $"'{row.BuiltModel}' ({builtPath}) — EnemyFactory.Build's Wildlands deferral gate.");
                        row.SourcePath = builtPath;
                    }
                    row.ArtSource = ArtSourceOf(row.BuiltModel != "?" ? row.BuiltModel : row.Model, row.SourcePath);
                    ProveMotion(go, row);
                    InspectMaterials(go, row);
                    Capture(cam, rt, go, row);
                }
                catch (System.Exception ex)
                {
                    row.Defects.Add("EXCEPTION while proving: " + ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    if (go != null) Object.DestroyImmediate(go);
                }

                Grade(row);
                rows.Add(row);
                index++;
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(fillGo);

            // ── the report: picture + numbers, one artifact ──────────────────────
            log.AppendLine("ENEMY PROVING HARNESS — built through EnemyFactory.Build (the production path).");
            log.AppendLine("Columns: id | family | resolved model | source path | RIG | ANIM (method, max delta) | TEX | boundsY | pixel coverage | png");
            log.AppendLine(new string('-', 120));

            int fails = 0, unproven = 0;
            foreach (var r in rows)
            {
                string rig = r.RigOk
                    ? $"OK(smr={r.SkinnedCount},bones={r.BoneCount},avatar={(r.IsHuman ? "humanoid " : "generic ")}{r.AvatarName},ctrl={r.ControllerName})"
                    : $"BAD(smr={r.SkinnedCount},bones={r.BoneCount},mesh={(r.MeshOk ? "y" : "n")},anim={(r.AnimatorPresent ? "y" : "n")},ctrl={(r.ControllerOk ? r.ControllerName : "NULL")},avatar={(r.AvatarOk ? r.AvatarName : "INVALID")})";

                string anim = r.AnimUnproven
                    ? $"UNPROVEN({r.AnimNote})"
                    : (r.AnimMoved
                        ? $"MOVED via {r.AnimMethod} (dPos={r.MaxPosDelta.ToString("F4", CultureInfo.InvariantCulture)}m dRot={r.MaxRotDelta.ToString("F2", CultureInfo.InvariantCulture)}deg)"
                        : $"STATIC via {r.AnimMethod} (dPos={r.MaxPosDelta.ToString("F4", CultureInfo.InvariantCulture)}m dRot={r.MaxRotDelta.ToString("F2", CultureInfo.InvariantCulture)}deg) {r.AnimNote}");

                string tex = $"{(r.TexOk ? "OK" : "BAD")}(slots={r.MatSlots},null={r.NullMats},noMainTex={r.MissingMainTex}{(r.RuntimeTinted ? ",colour=RUNTIME-TINTED/UNPROVEN" : "")})";

                log.AppendLine($"{r.Id,-20} | {r.Family,-7} | wears '{r.BuiltModel}' (requested '{r.Model}') | {r.SourcePath}");
                log.AppendLine($"{"",-20}   ART  {r.ArtSource}");
                log.AppendLine($"{"",-20}   RIG  {rig}");
                log.AppendLine($"{"",-20}   ANIM {anim}");
                log.AppendLine($"{"",-20}   TEX  {tex}  shaders=[{r.ShaderNames}]");
                log.AppendLine($"{"",-20}   TXTR {r.TextureNames}");
                log.AppendLine($"{"",-20}   SHOT bounds=({r.BoundsSize.x:F2},{r.BoundsSize.y:F2},{r.BoundsSize.z:F2}) coverage={r.Coverage:P2} -> {r.Png}");
                foreach (var p in r.AssetPaths) log.AppendLine($"{"",-20}   PATH {p}");
                foreach (var n in r.Notes) log.AppendLine($"{"",-20}   NOTE {n}");
                foreach (var d in r.Defects) log.AppendLine($"{"",-20}   >>>> DEFECT: {d}");
                log.AppendLine();

                if (r.Defects.Count > 0) fails++;
                if (r.AnimUnproven) unproven++;
            }

            // ── WHO SHARES WHAT ──────────────────────────────────────────────────
            // A per-row report cannot show a COLLISION: every row reads fine on its own while
            // four different silhouettes quietly wear one skin. Indexing texture -> ids and
            // model -> ids puts the sharing on the page, where an unintended one is obvious.
            log.AppendLine(new string('-', 120));
            log.AppendLine("TEXTURE SHARING (a texture worn by ids that are supposed to look different is a defect the per-row view cannot show):");
            var byTex = new Dictionary<string, List<string>>();
            foreach (var r in rows)
                foreach (var p in r.AssetPaths)
                {
                    if (!byTex.TryGetValue(p, out var ids)) byTex[p] = ids = new List<string>();
                    if (!ids.Contains(r.Id)) ids.Add(r.Id);
                }
            foreach (var kv in byTex)
                log.AppendLine($"  {kv.Value.Count,2} id(s) <- {kv.Key}   [{string.Join(", ", kv.Value)}]");

            log.AppendLine();
            log.AppendLine("BODY SHARING (model -> the ids that wear it):");
            var byModel = new Dictionary<string, List<string>>();
            foreach (var r in rows)
            {
                string key = r.BuiltModel ?? "?";
                if (!byModel.TryGetValue(key, out var ids)) byModel[key] = ids = new List<string>();
                ids.Add(r.Id);
            }
            foreach (var kv in byModel)
                log.AppendLine($"  {kv.Value.Count,2} id(s) <- {kv.Key}   [{string.Join(", ", kv.Value)}]");

            log.AppendLine();
            log.AppendLine("ART PROVENANCE (the owner's KayKit question, answered by name):");
            foreach (var r in rows)
                if (r.ArtSource != null && r.ArtSource.StartsWith("KAYKIT"))
                    log.AppendLine($"  {r.Id} wears '{r.BuiltModel}' — {r.ArtSource} — {r.SourcePath}");

            log.AppendLine();
            log.AppendLine(new string('-', 120));
            log.AppendLine($"total={rows.Count} clean={rows.Count - fails} defective={fails} unproven-animation={unproven}");

            string summaryPath = Path.Combine(OutDir, "_summary.txt");
            File.WriteAllText(summaryPath, log.ToString());
            Debug.Log("[EnemyProving]\n" + log);

            foreach (var r in rows)
                foreach (var d in r.Defects)
                    Debug.LogWarning($"[EnemyProving] DEFECT {r.Id} ({r.Model}): {d}");

            if (fails == 0)
                Debug.Log($"ENEMY_PROVING_OK {rows.Count}/{rows.Count} enemies proven (unproven-animation={unproven}) -> {summaryPath}");
            else
                Debug.LogError($"ENEMY_PROVING_FAIL {fails} of {rows.Count} enemies have defects (unproven-animation={unproven}) -> {summaryPath}");
        }

        // =====================================================================
        //  RIG
        // =====================================================================
        private static void InspectBody(GameObject go, Row row)
        {
            row.Capsule = go.transform.Find("PlaceholderCapsule") != null;

            // WHAT THE BODY ACTUALLY WEARS. VisualFactory.Skin instantiates the prefab as a child,
            // so the child's name (minus "(Clone)") is the model the player sees — the ONLY honest
            // answer when EnemyFactory.Build has substituted one (the Wildlands deferral gate).
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var c = go.transform.GetChild(i);
                if (c.name == "PlaceholderCapsule") continue;
                if (c.GetComponentInChildren<Renderer>(true) == null) continue;
                row.BuiltModel = c.name.Replace("(Clone)", "").Trim();
                break;
            }
            if (row.Capsule) row.BuiltModel = "PLACEHOLDER CAPSULE";

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            row.SkinnedCount = smrs.Length;

            // DISTINCT bones. Summing per-renderer bone arrays double-counts the shared skeleton
            // (nine renderers x one 100-bone rig read as "900 bones"), which looks like a healthy
            // rig no matter what the rig is. Count the skeleton, not the references to it.
            var boneSet = new HashSet<Transform>();
            bool anyMesh = false;
            foreach (var s in smrs)
            {
                if (s == null) continue;
                if (s.sharedMesh != null) anyMesh = true;
                if (s.bones != null) foreach (var b in s.bones) if (b != null) boneSet.Add(b);
            }
            int bones = boneSet.Count;
            foreach (var mf in mfs)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                // The placeholder capsule is a MeshFilter too — it must not count as art.
                if (mf.gameObject.name == "PlaceholderCapsule") continue;
                row.StaticMeshCount++;
                anyMesh = true;
            }
            row.BoneCount = bones;
            row.MeshOk = anyMesh && !row.Capsule;

            var anim = go.GetComponentInChildren<Animator>(true);
            row.AnimatorPresent = anim != null;
            if (anim != null)
            {
                row.IsHuman = anim.isHuman;
                row.ControllerOk = anim.runtimeAnimatorController != null;
                row.ControllerName = anim.runtimeAnimatorController != null
                    ? anim.runtimeAnimatorController.name : "NULL";
                bool avatarValid = anim.avatar != null && anim.avatar.isValid;
                row.AvatarName = anim.avatar != null ? anim.avatar.name : "NULL";
                // A GENERIC rig legitimately animates with no avatar of its own; a HUMANOID
                // one cannot — a humanoid clip needs the avatar to retarget, and without it
                // the body holds its bind pose while the agent slides it.
                row.AvatarOk = anim.isHuman ? avatarValid : true;
            }
        }

        // =====================================================================
        //  ART PROVENANCE — answer "is that a KayKit body?" with a name, not an argument
        // =====================================================================
        /// <summary>
        /// Which art pack a body comes from. The owner asked outright, having seen "a skeleton from
        /// kaykat" in play; WO-954 retired two KayKit bodies (Skeleton_Golem -> Skeleton_Golem_NEW,
        /// Necromancer -> Necromancer_NEW) but Skeleton_Minion was never replaced, so the answer is
        /// not obvious from the id, the path, or the ticket. The KAYKIT LEGACY SET IS DOCUMENTED, in
        /// docs/kaykit-asset-catalog.md ("KayKit legacy (still live): Skeleton_Minion, Skeleton_Golem,
        /// Necromancer") and in EnemyResolver's own header — this reads that documented fact back out
        /// so a KayKit body in the roster is NAMED IN THE REPORT rather than noticed on a device.
        /// <para>⚠ THIS REPORTS. It never substitutes: which body an enemy wears is a creative
        /// decision and belongs to the owner.</para>
        /// </summary>
        private static string ArtSourceOf(string model, string path)
        {
            switch (model)
            {
                case "Skeleton_Minion":
                case "Skeleton_Golem":
                case "Necromancer":
                    return "KAYKIT LEGACY (KayKit Skeletons 1.1) — the pack the owner asked about";
            }
            if (!string.IsNullOrEmpty(path) && path.IndexOf("KayKit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "KAYKIT (by asset path)";
            if (model != null && model.StartsWith("Skeleton_") && model.EndsWith("_NEW"))
                return "Tripo re-make (WO-954 replacement for the KayKit body)";
            if (model == "Necromancer_NEW") return "Tripo re-make (WO-954 replacement for the KayKit body)";
            if (model != null && (model.StartsWith("Orc_") || model.StartsWith("Troll") || model == "Demon"))
                return "Tripo / AccuRig";
            if (model != null && model.StartsWith("Skeleton_")) return "AccuRig CC_Base";
            return "unclassified";
        }

        // =====================================================================
        //  ANIMATION — prove a bone actually moves
        // =====================================================================
        private static void ProveMotion(GameObject go, Row row)
        {
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                row.AnimUnproven = true;
                row.AnimNote = "no Animator on the built body";
                return;
            }

            var probes = PickProbeBones(go);
            if (probes.Count == 0)
            {
                row.AnimUnproven = true;
                row.AnimNote = "no skinned bones to sample (nothing an animation could move)";
                return;
            }

            // ---- method 1: drive the REAL Animator -------------------------------
            if (anim.runtimeAnimatorController != null)
            {
                AnimatorCullingMode keepCull = anim.cullingMode;
                try
                {
                    // With no camera looking at it, a culled Animator writes no transforms and
                    // an honest rig would read as frozen. Force it to animate while we measure.
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anim.applyRootMotion = false;
                    anim.Rebind();
                    anim.Update(0f);

                    var before = Snapshot(probes);
                    for (int i = 0; i < AnimFrames; i++) anim.Update(AnimDt);
                    var after = Snapshot(probes);

                    Compare(before, after, out float dp, out float dr);
                    row.AnimMethod = "Animator.Update";
                    row.MaxPosDelta = dp; row.MaxRotDelta = dr;
                    row.AnimMoved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                    if (row.AnimMoved) return;
                    row.AnimNote = "Animator.Update drove nothing; retried via AnimationMode clip sampling";
                }
                catch (System.Exception ex)
                {
                    row.AnimNote = "Animator.Update threw " + ex.GetType().Name + "; ";
                }
                finally { if (anim != null) anim.cullingMode = keepCull; }
            }
            else
            {
                row.AnimNote = "no runtimeAnimatorController; ";
            }

            // ---- method 2: sample a clip directly through AnimationMode ----------
            AnimationClip clip = FirstUsableClip(anim);
            if (clip == null)
            {
                row.AnimUnproven = true;
                row.AnimNote += "and the controller exposes no AnimationClip to sample — motion could not be tested in edit mode";
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
                row.AnimMethod = $"AnimationMode clip '{clip.name}'";
                row.MaxPosDelta = Mathf.Max(row.MaxPosDelta, dp);
                row.MaxRotDelta = Mathf.Max(row.MaxRotDelta, dr);
                row.AnimMoved = dp > MoveEpsilonMetres || dr > MoveEpsilonDegrees;
                if (!row.AnimMoved)
                    row.AnimNote += $"clip '{clip.name}' ({clip.length:F2}s, humanMotion={clip.humanMotion}) sampled at two times and the rig did not change pose";
            }
            catch (System.Exception ex)
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                row.AnimUnproven = true;
                row.AnimNote += "AnimationMode sampling threw " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>Up to 8 bones spread across the skeleton — enough that a partial rig
        /// (e.g. only the cape moves) still registers, few enough to stay cheap.</summary>
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

        // =====================================================================
        //  TEXTURES
        // =====================================================================
        private static void InspectMaterials(GameObject go, Row row)
        {
            var shaders = new List<string>();
            var textures = new List<string>();

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.gameObject.name == "PlaceholderCapsule") continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (var m in mats)
                {
                    row.MatSlots++;
                    if (m == null)
                    {
                        row.NullMats++;
                        continue;   // a null slot renders the engine-default MAGENTA
                    }
                    string sh = m.shader != null ? m.shader.name : "<null shader>";
                    if (!shaders.Contains(sh)) shaders.Add(sh);

                    // Ungated reads. HasProperty is unreliable when shaders do not fully
                    // resolve headless (the hero white-Paladin lesson) — read the sheet.
                    Texture tex = null;
                    try { tex = m.mainTexture; } catch { }
                    if (tex == null) { try { tex = m.GetTexture("_BaseMap"); } catch { } }
                    if (tex == null) { try { tex = m.GetTexture("_BaseColorMap"); } catch { } }

                    if (tex == null)
                    {
                        row.MissingMainTex++;
                        string mp = AssetDatabase.GetAssetPath(m);
                        if (!textures.Contains("<no main texture on " + m.name + ">"))
                            textures.Add("<no main texture on " + m.name + ">");
                        if (!string.IsNullOrEmpty(mp) && !row.AssetPaths.Contains(mp)) row.AssetPaths.Add(mp);
                    }
                    else
                    {
                        string tp = AssetDatabase.GetAssetPath(tex);
                        string entry = tex.name + (string.IsNullOrEmpty(tp) ? " (embedded)" : " @ " + tp);
                        if (!textures.Contains(entry)) textures.Add(entry);
                        if (!string.IsNullOrEmpty(tp) && !row.AssetPaths.Contains(tp)) row.AssetPaths.Add(tp);
                        foreach (var ignored in GitignoredArtRoots)
                            if (!string.IsNullOrEmpty(tp) && tp.StartsWith(ignored, System.StringComparison.OrdinalIgnoreCase))
                                row.Defects.Add($"texture '{tex.name}' lives under GITIGNORED root '{ignored}' ({tp}) — breaks on a fresh clone");
                    }
                }
            }

            // ⚠ DECLARE THE GAP RATHER THAN PASSING OVER IT. The Tripo/AccuRig families (Troll*,
            // Orc_*, Demon, OgreMage, Skeleton_Golem_NEW, Necromancer_NEW) ship an UNBOUND _MainTex
            // and are coloured at RUNTIME by TripoMaterialFixer, whose Run() is driven from Start()
            // — which does not fire in edit mode. So the picture this harness takes of one of those
            // bodies is the UNTINTED body (it photographs solid white), and its final colour is
            // simply NOT PROVEN here. Saying "textures OK" for them would be a lie of omission; the
            // colour authority for these families is EnemyRigColorRegression, which asserts the
            // per-orc basecolor asset exists. This is the honest boundary between the two oracles.
            row.RuntimeTinted = go.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>(true) != null;
            if (row.RuntimeTinted)
                row.Notes.Add("COLOUR IS APPLIED AT RUNTIME by TripoMaterialFixer (Start() does not run in edit " +
                              "mode) — the PNG shows the UNTINTED body and its final colour is UNPROVEN by this harness.");
            else if (row.MissingMainTex > 0)
                row.Notes.Add($"{row.MissingMainTex} material(s) have NO main texture and NO runtime tint component — " +
                              "these submeshes render as flat untextured colour.");

            row.ShaderNames = shaders.Count > 0 ? string.Join(", ", shaders) : "-";
            row.TextureNames = textures.Count > 0 ? string.Join(" | ", textures) : "-";

            if (!string.IsNullOrEmpty(row.SourcePath))
                foreach (var ignored in GitignoredArtRoots)
                    if (row.SourcePath.StartsWith(ignored, System.StringComparison.OrdinalIgnoreCase))
                        row.Defects.Add($"BODY asset is under GITIGNORED root '{ignored}' ({row.SourcePath}) — breaks on a fresh clone");
        }

        // =====================================================================
        //  PICTURE (same rig for every subject — see StorefrontOrientationCapture)
        // =====================================================================
        private static void Capture(Camera cam, RenderTexture rt, GameObject go, Row row)
        {
            if (!TryWorldBounds(go, out Bounds b))
            {
                row.Defects.Add("no renderer bounds — nothing to photograph (the body did not render)");
                return;
            }
            row.BoundsSize = b.size;

            float h = Mathf.Max(0.01f, b.size.y);
            Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
            cam.transform.position = b.center + dir * (h * 2.3f);
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = h * 40f;

            var saved = new Dictionary<Transform, int>();
            MoveToLayer(go.transform, IsolationLayer, saved);
            cam.cullingMask = 1 << IsolationLayer;
            cam.Render();
            foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // BLANK GUARD: a PNG of the background colour is not evidence of a body.
            var px = tex.GetPixels32();
            var bg = cam.backgroundColor;
            int lit = 0;
            for (int i = 0; i < px.Length; i += 7)   // stride-sample; plenty for a coverage figure
            {
                float dr = Mathf.Abs(px[i].r / 255f - bg.r);
                float dg = Mathf.Abs(px[i].g / 255f - bg.g);
                float db = Mathf.Abs(px[i].b / 255f - bg.b);
                if (dr + dg + db > 0.06f) lit++;
            }
            row.Coverage = lit / (float)(px.Length / 7f);

            string path = Path.Combine(OutDir, row.Id + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            row.Png = path.Replace('\\', '/');

            if (row.Coverage < BlankCoverageFloor)
                row.Defects.Add($"the shot is BLANK ({row.Coverage:P2} non-background pixels) — the body did not render into frame");
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

        // =====================================================================
        //  VERDICT
        // =====================================================================
        private static void Grade(Row row)
        {
            if (row.Capsule)
                row.Defects.Add("built as the PLACEHOLDER CAPSULE — the real body did not load through EnemyAssetLoader");
            if (!row.MeshOk && !row.Capsule)
                row.Defects.Add("no renderable mesh on the built body");
            if (row.SkinnedCount > 0 && row.BoneCount == 0)
                row.Defects.Add("SkinnedMeshRenderer present but ZERO bones — a dead rig that cannot be animated");
            if (row.SkinnedCount == 0 && !row.Capsule)
                row.Defects.Add($"no SkinnedMeshRenderer at all (staticMeshes={row.StaticMeshCount}) — this body cannot animate");
            if (!row.AnimatorPresent)
                row.Defects.Add("no Animator on the built body");
            else if (!row.ControllerOk)
                row.Defects.Add("Animator has NO runtimeAnimatorController — it will slide with no animation");
            if (row.AnimatorPresent && !row.AvatarOk)
                row.Defects.Add("humanoid mesh with a NULL/INVALID Avatar — humanoid clips cannot retarget (the sliding-statue path)");
            if (row.NullMats > 0)
                row.Defects.Add($"{row.NullMats} NULL material slot(s) — those submeshes render engine-default MAGENTA");
            if (row.MatSlots == 0 && !row.Capsule)
                row.Defects.Add("no material slots at all on any renderer");
            if (!row.AnimUnproven && !row.AnimMoved)
                row.Defects.Add($"ANIMATION DID NOT MOVE ({row.AnimMethod}): {row.AnimNote}");
        }
    }
}
