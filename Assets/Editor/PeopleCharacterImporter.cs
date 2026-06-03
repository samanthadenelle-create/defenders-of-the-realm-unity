// =============================================================================
// PeopleCharacterImporter — DEF-221 Phase 1. Wires the Assets/Models/People set
// (4 heroes + 3 orcs) into Resources as HUMANOID rigs so they animate off the
// shared Assets/Action Mixamo library, and builds the two controllers the set
// needs (Cleric for the new hero body, OrcWarband for the orc family).
// -----------------------------------------------------------------------------
// THE KEY FACT (DEF-221): all 7 People FBX are the SAME biped (108 LimbNode / 85
// Deformer / 6 Spine), imported animationType:2 (Generic). Set them to Humanoid
// once and:
//   • heroes reuse the existing rig-agnostic Knight/Mage/Ranger.controller (and a
//     new Cleric.controller = a copy of Mage's caster kit);
//   • orcs get one new Humanoid OrcWarband.controller built here from the Action
//     library, wired with the exact params Enemy.cs drives (Speed/Attack/Hit/Dead).
//
// SAFE: copies (AssetDatabase.CopyAsset → fresh GUID, no .meta duplication) the
// source FBX over the Resources slug name, then flips the COPY to Humanoid. The
// originals in Assets/Models/People are untouched; the prior Resources FBX are
// replaced (git is the backup if a rig fails to map). Every step is logged with a
// per-model avatar verdict (Humanoid / Generic-fallback / failed) so the shared
// rig is proven ONCE — if one maps, all map.
//
// Run headless:  -executeMethod DeNelle.Editor.PeopleCharacterImporter.Run
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PeopleCharacterImporter
    {
        private const string ActionDir = "Assets/Action/";
        private const string HeroDir   = "Assets/Resources/Heroes/";
        private const string EnemyDir  = "Assets/Resources/Enemies/";

        private struct ModelMap { public string Src; public string Dst; public string Label; }

        // People/Human → hero slug (HeroBodySwapper loads Resources/Heroes/<slug>).
        private static readonly ModelMap[] Heroes =
        {
            new ModelMap { Src = "Assets/Models/People/Human/Human_Wizard.fbx", Dst = HeroDir + "Mage.fbx",   Label = "Mage (Human_Wizard)" },
            new ModelMap { Src = "Assets/Models/People/Human/human_tank.fbx",   Dst = HeroDir + "Knight.fbx", Label = "Knight (human_tank)" },
            new ModelMap { Src = "Assets/Models/People/Human/Human_Ranger.fbx", Dst = HeroDir + "Ranger.fbx", Label = "Ranger (Human_Ranger)" },
            new ModelMap { Src = "Assets/Models/People/Human/human_Cleric.fbx", Dst = HeroDir + "Cleric.fbx", Label = "Cleric (human_Cleric)" },
        };

        // People/Orc → Resources/Enemies model name (EnemyFactory.ModelForEnemy maps
        // the orc enemy ids to these; EnemyAnimatorFactory routes them to OrcWarband).
        private static readonly ModelMap[] Orcs =
        {
            new ModelMap { Src = "Assets/Models/People/Orc/Orc_Berserker.fbx",   Dst = EnemyDir + "Orc_Berserker.fbx",   Label = "Orc_Berserker" },
            new ModelMap { Src = "Assets/Models/People/Orc/Orc_Shaman.fbx",      Dst = EnemyDir + "Orc_Shaman.fbx",      Label = "Orc_Shaman" },
            new ModelMap { Src = "Assets/Models/People/Orc/orc necromancer.fbx", Dst = EnemyDir + "Orc_Necromancer.fbx", Label = "Orc_Necromancer" },
        };

        [MenuItem("Defenders/Animation/Import People Character Set (DEF-221)")]
        public static void Run()
        {
            var report = new List<string>();
            report.Add("=== PeopleCharacterImporter (DEF-221 Phase 1) ===");

            report.Add("-- Heroes → Resources/Heroes --");
            foreach (var m in Heroes) ImportHumanoid(m, report);

            report.Add("-- Orcs → Resources/Enemies --");
            foreach (var m in Orcs) ImportHumanoid(m, report);

            report.Add("-- Controllers --");
            BuildClericController(report);
            BuildOrcController(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] DONE\n" + string.Join("\n", report));
        }

        /// <summary>Copy the source FBX over the Resources slug name and flip the copy
        /// to a Humanoid rig with a model-generated avatar. Verifies + reports the
        /// avatar verdict. Mesh-only (importAnimation off): clips come from Action.</summary>
        private static void ImportHumanoid(ModelMap m, List<string> report)
        {
            if (AssetImporter.GetAtPath(m.Src) == null)
            {
                report.Add($"  MISSING SRC, skipped: {m.Src}");
                return;
            }

            AssetDatabase.DeleteAsset(m.Dst);
            if (!AssetDatabase.CopyAsset(m.Src, m.Dst))
            {
                report.Add($"  COPY FAILED: {m.Src} -> {m.Dst}");
                return;
            }

            var imp = AssetImporter.GetAtPath(m.Dst) as ModelImporter;
            if (imp == null) { report.Add($"  NO IMPORTER: {m.Dst}"); return; }

            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;   // character mesh carries no clips; Action library retargets on
            imp.SaveAndReimport();

            // Verify the rig mapped (the one DEF-221 risk — proven once for the shared rig).
            var go   = AssetDatabase.LoadAssetAtPath<GameObject>(m.Dst);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var av   = anim != null ? anim.avatar : null;
            string verdict;
            if (av != null && av.isValid && av.isHuman) verdict = "OK Humanoid avatar (retarget ready)";
            else if (av != null && av.isValid)          verdict = "WARN avatar valid but GENERIC (not human) — Tripo walk-clip fallback applies";
            else                                        verdict = "FAIL no valid avatar — rig did NOT map (hand-map needed)";
            report.Add($"  {m.Label}: {verdict} -> {m.Dst}");
        }

        /// <summary>Repair any hero that imported GENERIC (e.g. the binary-FBX Ranger
        /// whose rig didn't auto-map) by COPYING the Mage's proven Humanoid avatar —
        /// valid because all 7 People models are the same biped. CopyFromOtherAvatar
        /// makes the model retarget through the Mage avatar, so the hero controllers +
        /// Action library drive it like the others.</summary>
        [MenuItem("Defenders/Animation/Repair Generic Hero Avatars (DEF-221)")]
        public static void RepairGenericAvatars()
        {
            var report = new List<string>();
            report.Add("=== RepairGenericAvatars (DEF-221) ===");

            var mageGo   = AssetDatabase.LoadAssetAtPath<GameObject>(HeroDir + "Mage.fbx");
            var mageAnim = mageGo != null ? mageGo.GetComponentInChildren<Animator>() : null;
            var mageAv   = mageAnim != null ? mageAnim.avatar : null;
            if (mageAv == null || !mageAv.isValid || !mageAv.isHuman)
            {
                Debug.LogWarning("[PeopleCharacterImporter] Repair: no Humanoid Mage avatar to copy from — run Import first.");
                return;
            }

            foreach (var m in Heroes)
            {
                var go   = AssetDatabase.LoadAssetAtPath<GameObject>(m.Dst);
                var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
                var av   = anim != null ? anim.avatar : null;
                if (av != null && av.isValid && av.isHuman) { report.Add($"  {m.Label}: already Humanoid — skipped"); continue; }

                var imp = AssetImporter.GetAtPath(m.Dst) as ModelImporter;
                if (imp == null) { report.Add($"  {m.Label}: NO IMPORTER at {m.Dst}"); continue; }
                // Setting sourceAvatar on a Humanoid import IS the copy-from-other-avatar
                // path (the ModelImporterAvatarSetup enum has no CopyFromOtherAvatar member
                // in 6000.4 — assigning sourceAvatar is what selects copy mode).
                imp.animationType = ModelImporterAnimationType.Human;
                imp.sourceAvatar  = mageAv;
                imp.SaveAndReimport();

                go   = AssetDatabase.LoadAssetAtPath<GameObject>(m.Dst);
                anim = go != null ? go.GetComponentInChildren<Animator>() : null;
                av   = anim != null ? anim.avatar : null;
                string verdict = (av != null && av.isValid && av.isHuman)
                    ? "OK now Humanoid (copied Mage avatar) — retarget ready"
                    : "STILL not Humanoid — bone names likely differ; needs hand-map or keep prior FBX";
                report.Add($"  {m.Label}: {verdict}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] REPAIR DONE\n" + string.Join("\n", report));
        }

        /// <summary>Cleric gets its own body but the proven Mage caster controller —
        /// a straight copy so the new Cleric animates Walk/Cast immediately.</summary>
        private static void BuildClericController(List<string> report)
        {
            string src = HeroDir + "Mage.controller";
            string dst = HeroDir + "Cleric.controller";
            if (AssetImporter.GetAtPath(src) == null)
            {
                report.Add("  Cleric.controller: SKIPPED — Mage.controller absent (run Build Hero Animators first).");
                return;
            }
            AssetDatabase.DeleteAsset(dst);
            report.Add(AssetDatabase.CopyAsset(src, dst)
                ? "  Cleric.controller <- Mage.controller (caster kit) ✓"
                : "  Cleric.controller: COPY FAILED");
        }

        /// <summary>Build the Humanoid OrcWarband controller: a Speed blend (Idle/Walk/
        /// Run) + Attack (trigger) + Death (Dead bool), the params Enemy.cs drives. Hit
        /// is declared via Enemy's guard only if a react clip exists; we leave it to the
        /// param-guard (no flinch clip in the library) so SetTrigger("Hit") safely no-ops.</summary>
        private static void BuildOrcController(List<string> report)
        {
            string path = EnemyDir + "OrcWarband.controller";

            AnimationClip idle   = LoadClip("Orc Idle") ?? LoadClip("standing idle 01");
            AnimationClip walk   = LoadClip("simple_walk") ?? LoadClip("walk");
            AnimationClip run    = LoadClip("standing run forward");
            AnimationClip attack = LoadClip("Sword And Shield Attack");
            AnimationClip death  = LoadClip("Falling Back Death") ?? LoadClip("Dying") ?? LoadClip("Defeated");

            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",   AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            // Locomotion — 1-D blend on Speed (orc moveSpeed ~2.4–2.8 lands in walk).
            var loco = sm.AddState("Locomotion");
            sm.defaultState = loco;
            var blend = new BlendTree
            {
                name = "Locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, ctrl);
            loco.motion = blend;
            int n = 0;
            if (idle != null) { blend.AddChild(idle, 0f);   n++; }
            if (walk != null) { blend.AddChild(walk, 2.6f); n++; }
            if (run  != null) { blend.AddChild(run,  5f);   n++; }

            // Attack — Any → Attack on the trigger, snappy, back to Locomotion.
            if (attack != null)
            {
                var st = sm.AddState("Attack");
                st.motion = attack;
                st.speed  = 1.15f;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = st.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.8f; back.duration = 0.08f;
            }

            // Death — Any → Death while Dead==true, no exit (stays down until destroyed).
            if (death != null)
            {
                var st = sm.AddState("Death");
                st.motion = death;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            }

            EditorUtility.SetDirty(ctrl);
            report.Add($"  OrcWarband.controller built: Locomotion({n} clips)" +
                       $"{(attack != null ? " + Attack" : " + [no attack clip]")}" +
                       $"{(death != null ? " + Death" : " + [no death clip]")} " +
                       "[Speed/Attack/Hit/Dead] ✓");
        }

        /// <summary>Load the motion AnimationClip from an Assets/Action FBX by basename,
        /// skipping __preview__ and T-pose/bind takes. Null + warn if absent.</summary>
        private static AnimationClip LoadClip(string fbxBaseName)
        {
            string path = ActionDir + fbxBaseName + ".fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0) return null;
            AnimationClip fallback = null;
            foreach (var a in assets)
            {
                if (!(a is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;
                string nm = clip.name.ToLowerInvariant();
                if (nm.Contains("t-pose") || nm.Contains("tpose") || nm.Contains("bind"))
                {
                    fallback ??= clip;
                    continue;
                }
                return clip;
            }
            return fallback;
        }

        // =====================================================================
        // CC5 (Reallusion Character Creator) InstaLOD hero pipeline
        // ---------------------------------------------------------------------
        // A CC5 hero ships as ONE combined body mesh + a baked PBR texture set
        // (Diffuse/Normal/Metallic). The FBX's own CC material refs are the
        // broken Phong/Std_Skin set URP can't render, so we import with
        // materialImportMode=None and supply our OWN URP/Lit material remapped
        // onto the FBX's material slots. Generic + reusable: feed any future CC5
        // hero (e.g. a CC5 Elara/Cleric) through ImportCC5Hero with its paths.
        //
        // Ranger headless: -executeMethod DeNelle.Editor.PeopleCharacterImporter.ImportRangerCC5
        // =====================================================================

        /// <summary>Generic CC5 InstaLOD hero importer. Copies the raw FBX bytes
        /// (File.Copy — the source often sits in a Unity-reserved .fbm folder that
        /// CopyAsset refuses) to Resources/Heroes/&lt;slug&gt;.fbx, copies the baked
        /// textures into Resources/Heroes/&lt;slug&gt;_tex/, sets their importers,
        /// flips the FBX to a Humanoid rig (copying the proven Mage avatar if the
        /// rig doesn't auto-map), builds a URP/Lit material from the baked maps,
        /// and remaps the FBX's material slots onto it. Appends a verdict report.
        /// srcTexturePaths order is irrelevant — maps are classified by filename
        /// (Diffuse / Normal / Metallic).</summary>
        private static void ImportCC5Hero(string srcFbxPath, string[] srcTexturePaths, string destSlug, List<string> report)
        {
            report.Add($"-- ImportCC5Hero: {destSlug} --");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string destFbx     = HeroDir + destSlug + ".fbx";
            string texDir      = HeroDir + destSlug + "_tex";

            // 1a. Verify source FBX exists on disk (it lives in a .fbm so the
            //     AssetDatabase may not see it; check the raw path).
            string srcFbxFull = Path.Combine(projectRoot, srcFbxPath);
            if (!File.Exists(srcFbxFull))
            {
                report.Add($"  MISSING SRC FBX, aborted: {srcFbxPath}");
                return;
            }

            // 1b. Copy the FBX raw bytes over the slug. File.Copy (NOT CopyAsset):
            //     the source is inside a .fbm and CopyAsset won't touch it.
            string destFbxFull = Path.Combine(projectRoot, destFbx);
            try
            {
                File.Copy(srcFbxFull, destFbxFull, true);
            }
            catch (System.Exception e)
            {
                report.Add($"  FBX File.Copy FAILED: {e.Message}");
                return;
            }

            // 1c. Clean tex subfolder (under Resources so the build includes it).
            string texDirFull = Path.Combine(projectRoot, texDir);
            if (Directory.Exists(texDirFull)) Directory.Delete(texDirFull, true);
            Directory.CreateDirectory(texDirFull);

            // 1d. Copy each baked texture in, classifying by filename.
            string diffusePath = null, normalPath = null, metallicPath = null;
            int texCopied = 0;
            foreach (var src in srcTexturePaths)
            {
                if (string.IsNullOrEmpty(src)) continue;
                string srcFull = Path.Combine(projectRoot, src);
                if (!File.Exists(srcFull)) { report.Add($"  tex MISSING, skipped: {src}"); continue; }
                if (new FileInfo(srcFull).Length < 64) { report.Add($"  tex ~0 bytes, ignored: {src}"); continue; }

                string fileName = Path.GetFileName(src);
                string destTex  = texDir + "/" + fileName;
                File.Copy(srcFull, Path.Combine(texDirFull, fileName), true);
                texCopied++;

                string lower = fileName.ToLowerInvariant();
                if      (lower.Contains("normal"))   normalPath   = destTex;
                else if (lower.Contains("metallic")) metallicPath = destTex;
                else if (lower.Contains("diffuse") || lower.Contains("basecolor") || lower.Contains("albedo")) diffusePath = destTex;
            }

            AssetDatabase.Refresh();

            // 2. Texture importers: Normal -> NormalMap; Diffuse sRGB on; Metallic linear.
            if (normalPath != null)
            {
                var ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (ti != null) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
            if (diffusePath != null)
            {
                var ti = AssetImporter.GetAtPath(diffusePath) as TextureImporter;
                if (ti != null) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.SaveAndReimport(); }
            }
            if (metallicPath != null)
            {
                var ti = AssetImporter.GetAtPath(metallicPath) as TextureImporter;
                if (ti != null) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.SaveAndReimport(); }
            }

            // 3. FBX ModelImporter -> Humanoid, mesh-only, NO FBX materials.
            var imp = AssetImporter.GetAtPath(destFbx) as ModelImporter;
            if (imp == null) { report.Add($"  NO IMPORTER at {destFbx} after copy — aborted"); return; }

            imp.animationType      = ModelImporterAnimationType.Human;
            imp.avatarSetup        = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation    = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None; // supply our own URP material
            imp.SaveAndReimport();

            // Verify Humanoid; if not, copy the proven Mage avatar (same biped family).
            string avatarVerdict;
            {
                var go   = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
                var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
                var av   = anim != null ? anim.avatar : null;
                if (av != null && av.isValid && av.isHuman)
                {
                    avatarVerdict = "OK Humanoid (auto-mapped)";
                }
                else
                {
                    // Copy Mage avatar: assigning sourceAvatar IS the copy-from-other-avatar
                    // path in 6000.4 (no CopyFromOtherAvatar enum member).
                    var mageGo   = AssetDatabase.LoadAssetAtPath<GameObject>(HeroDir + "Mage.fbx");
                    var mageAnim = mageGo != null ? mageGo.GetComponentInChildren<Animator>() : null;
                    var mageAv   = mageAnim != null ? mageAnim.avatar : null;
                    if (mageAv != null && mageAv.isValid && mageAv.isHuman)
                    {
                        imp.animationType = ModelImporterAnimationType.Human;
                        imp.sourceAvatar  = mageAv;
                        imp.SaveAndReimport();

                        go   = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
                        anim = go != null ? go.GetComponentInChildren<Animator>() : null;
                        av   = anim != null ? anim.avatar : null;
                        avatarVerdict = (av != null && av.isValid && av.isHuman)
                            ? "copied-Mage-avatar (now Humanoid)"
                            : "FAILED — Mage-avatar copy did NOT yield a Humanoid rig (bone names differ)";
                    }
                    else
                    {
                        avatarVerdict = "FAILED — not Humanoid and no Mage avatar to copy from";
                    }
                }
            }

            // 4. Build the URP/Lit material from the baked maps.
            string matPath = texDir + "/" + destSlug + "Body.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material bodyMat;
            if (lit == null)
            {
                report.Add("  WARN URP/Lit shader not found — using Standard for the material");
                bodyMat = new Material(Shader.Find("Standard"));
            }
            else
            {
                bodyMat = new Material(lit);
            }
            bodyMat.SetColor("_BaseColor", Color.white);

            Texture2D diffuseTex  = diffusePath  != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath)  : null;
            Texture2D normalTex   = normalPath   != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)   : null;
            Texture2D metallicTex = metallicPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath) : null;

            if (diffuseTex != null)
            {
                bodyMat.SetTexture("_BaseMap", diffuseTex);
                bodyMat.SetTexture("_MainTex", diffuseTex); // belt-and-suspenders for Standard fallback
            }
            if (normalTex != null)
            {
                bodyMat.SetTexture("_BumpMap", normalTex);
                bodyMat.EnableKeyword("_NORMALMAP");
                bodyMat.SetFloat("_BumpScale", 1f);
            }
            if (metallicTex != null)
            {
                bodyMat.SetTexture("_MetallicGlossMap", metallicTex);
                bodyMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            bodyMat.SetFloat("_Metallic", 1f);
            bodyMat.SetFloat("_Smoothness", 0.4f);
            bodyMat.SetFloat("_Glossiness", 0.4f); // Standard fallback name

            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(bodyMat, matPath);
            AssetDatabase.SaveAssets();

            // 5. Remap the FBX's material slot(s) onto bodyMat. With
            //    materialImportMode=None the renderer materials are default/null,
            //    so we discover the FBX's source material identifiers (the names
            //    the FBX declares) and AddRemap each. Two passes: the first
            //    reimport above exposed the importer; gather identifiers now.
            int remapCount = 0;
            var matNames = new HashSet<string>();
            foreach (var id in imp.GetExternalObjectMap().Keys)
            {
                if (id.type == typeof(Material)) matNames.Add(id.name);
            }
            // GetExternalObjectMap is empty on a fresh None-import; discover the
            // declared material names from the FBX's imported sub-assets instead.
            if (matNames.Count == 0)
            {
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(destFbx);
                foreach (var sa in subAssets)
                {
                    if (sa is Material m && !string.IsNullOrEmpty(m.name)) matNames.Add(m.name);
                }
            }
            // Final fallback: read renderer material slot names off the GameObject.
            if (matNames.Count == 0)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
                if (go != null)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var sm in r.sharedMaterials)
                        {
                            if (sm != null && !string.IsNullOrEmpty(sm.name))
                                matNames.Add(sm.name.Replace(" (Instance)", ""));
                        }
                    }
                }
            }

            foreach (var name in matNames)
            {
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), bodyMat);
                remapCount++;
            }
            if (remapCount > 0) imp.SaveAndReimport();

            // Verify every renderer slot now points at bodyMat.
            int slotsOk = 0, slotsBad = 0;
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
                if (go != null)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var sm in r.sharedMaterials)
                        {
                            if (sm == bodyMat) slotsOk++;
                            else slotsBad++;
                        }
                    }
                }
            }

            // 6. Bounds height (confirm adult proportions, ~1.7–1.9m).
            float height = 0f;
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
                if (go != null)
                {
                    bool any = false;
                    Bounds b = default;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!any) { b = r.bounds; any = true; }
                        else b.Encapsulate(r.bounds);
                    }
                    if (any) height = b.size.y;
                }
            }

            report.Add($"  src -> dst: {srcFbxPath} -> {destFbx}");
            report.Add($"  textures copied: {texCopied} (diffuse={(diffusePath != null)}, normal={(normalPath != null)}, metallic={(metallicPath != null)})");
            report.Add($"  avatar: {avatarVerdict}");
            report.Add($"  material: {matPath} (remapped {remapCount} slot-name(s))");
            report.Add($"  renderer check: {slotsOk} slot(s) == bodyMat, {slotsBad} still null/other");
            report.Add($"  mesh bounds height: {height:F3} m (expect ~1.7–1.9 adult)");
        }

        /// <summary>Headless entry: import the CC5 InstaLOD Ranger (FighterClass
        /// remesh) into Resources/Heroes/Ranger.fbx with its baked PBR set. This
        /// REPLACES the prior archer-v2 Ranger FBX by design — HeroBodySwapper
        /// loads Resources/Heroes/Ranger.</summary>
        [MenuItem("Defenders/Animation/Import CC5 Ranger")]
        public static void ImportRangerCC5()
        {
            const string fbm = "Assets/Models/People/0_FighterClass_High_High_1024_LOD0.fbm/";
            var report = new List<string>();
            report.Add("=== Import CC5 Ranger ===");

            ImportCC5Hero(
                fbm + "ranger.fbx",
                new[]
                {
                    fbm + "remesh_12_combined_Bake_Diffuse.png",  // albedo (sRGB)
                    fbm + "remesh_12_combined_Bake_Normal.png",   // normal map
                    fbm + "remesh_12_combined_Bake_Metallic.png", // metallic (linear)
                    // remesh_12_combined_Bake_Reflection.png is ~0 bytes — intentionally omitted
                },
                "Ranger",
                report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] CC5 Ranger DONE\n" + string.Join("\n", report));
        }

        /// <summary>Headless entry: import the CC5 Cleric (owner's fresh adult body,
        /// CC_Base rig fixed for the staff) into Resources/Heroes/Cleric.fbx with its
        /// baked PBR set (HumanCleric_basecolor/normal/metallic). REPLACES the prior
        /// DEF-221 Cleric FBX — HeroBodySwapper loads Resources/Heroes/Cleric, and the
        /// Cleric is the Healer/Elara body.</summary>
        [MenuItem("Defenders/Animation/Import CC5 Cleric")]
        public static void ImportClericCC5()
        {
            const string dir = "Assets/Models/People/Human/human_Cleric/";
            var report = new List<string>();
            report.Add("=== Import CC5 Cleric ===");

            ImportCC5Hero(
                "Assets/Models/People/Human/human_Cleric.fbx",
                new[]
                {
                    dir + "HumanCleric_basecolor.JPEG", // albedo (sRGB)
                    dir + "HumanCleric_normal.JPEG",    // normal map
                    dir + "HumanCleric_metallic.JPEG",  // metallic (linear)
                    // HumanCleric_roughness.JPEG omitted — URP/Lit uses smoothness, not a roughness map slot
                },
                "Cleric",
                report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] CC5 Cleric DONE\n" + string.Join("\n", report));
        }
    }
}
