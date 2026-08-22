// =============================================================================
// BlinkOrcImporter — WO-680: activate the Blink Stylized Orcs Bundle as a
// side-by-side enemy family (ADDITIVE — the Tripo Orc_* assets are untouched).
// -----------------------------------------------------------------------------
// The bundle (Assets/Blink/Art/NPCs/Stylized/Orcs, SME: docs/SME/BLINK_SME.md
// §1.3/§5.1) ships 4 Humanoid-rigged archetypes (Warrior/Hunter/Warlock/Boss,
// fbx meta animationType:3 — verified) x 3 skins, two 22-clip Humanoid anim sets
// (Animations_Orcs + Animations_OrcBoss) and URP/Lit materials. It had ZERO code
// references; this importer stages it through the sanctioned mirror-to-Resources
// path (Assets/Blink is GITIGNORED — nothing may reference it at runtime):
//
//   1. Mesh FBX  -> Assets/EnemyContent/Blink/Blink_Orc_<A>_Mesh.fbx
//      (CopyAsset = fresh GUID; Humanoid verdict per PeopleCharacterImporter).
//   2. Skin-1 materials + textures (read off the vendor Orc_<A>1.prefab)
//      -> Blink/Materials + Blink/Textures, with texture refs re-pointed to the
//      copies so the committed set has no gitignored GUIDs.
//   3. A committed prefab Blink/Blink_Orc_<A>.prefab — the staged FBX instance
//      with the copied materials assigned (renderer-name match against the
//      vendor prefab), saved via SaveAsPrefabAsset. This is what
//      EnemyFactory/VisualFactory loads: Resources "Enemies/Blink/Blink_Orc_<A>".
//   4. Both 22-clip anim sets -> Blink/Anim + Blink/AnimBoss (Humanoid FBX,
//      loop flags asserted on Idle/Run/Strafe/*Loop clips).
//   5. Controllers Blink/BlinkOrc.controller + Blink/BlinkOrcBoss.controller
//      built from the COPIED clips to the BuildOrcController standard (Speed 1-D
//      locomotion blend, useAutomaticThresholds=false) with the exact params
//      ActorAnimator drives (Speed/InCombat/Attack/Cast/Hit/Dead/Injured).
//      The bundled OrcAnimator.controller is NOT used: it carries ZERO animator
//      parameters (verified in its YAML) — a demo showpiece Enemy.cs can't drive.
//
// Rig-binding is audited with OrcRigBindingAudit.AuditPrefab (the Tripo-chunk
// oracle). Pack absent (fresh clone/CI) => LogWarning + no-op; the committed
// mirror keeps working.
//
// Run headless:  -executeMethod DeNelle.Editor.BlinkOrcImporter.Run
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BlinkOrcImporter
    {
        private const string PackRoot  = "Assets/Blink/Art/NPCs/Stylized/Orcs";

        // ⚠ StageDir IS NOT A CONST ANYMORE, AND MUST NOT BECOME ONE AGAIN.
        //
        // The Blink orcs are the single heaviest thing in the game: ~427 MB, of which ~290 MB is
        // Textures/. Everything under a folder named "Resources" is force-included in EVERY player
        // build whether or not it is ever spawned, so that art is being migrated OUT to
        // Assets/EnemyContent/Blink and served through Addressables (DeNelle.Core.EnemyAssetLoader
        // + EnemyAddressablesGrouper).
        //
        // THE TRAP THIS CLOSES: this importer is the ART INTAKE tool — it CREATES the staging
        // folder (EnsureFolder(StageDir) below) and writes meshes, prefabs, materials, textures and
        // controllers into it. While StageDir was hardcoded to DeNelle.Core.AssetRoots.EnemyContent + "/Blink",
        // the next Blink art intake after a migration would have silently RE-CREATED that folder
        // inside Resources and re-inflated the build by up to 427 MB — with no error, no failing
        // gate, and nothing to attribute it to months later. A generator that quietly undoes an
        // optimisation is worse than never doing the optimisation.
        //
        // So: stage into the MIGRATED root once it exists, else the pre-migration Resources root.
        // Same resolve rule as EnemyAddressablesGrouper.ResolveActiveRoot and
        // EnemyRigControllerCoherenceRegression.EnemyRoot — one rule, three call sites, no drift.
        private const string StageResourcesDir = DeNelle.Core.AssetRoots.EnemyContent + "/Blink";
        private const string StageContentDir   = DeNelle.Core.AssetRoots.EnemyContent + "/Blink";

        /// <summary>
        /// Where freshly-imported Blink orc art is staged: the migrated
        /// <c>Assets/EnemyContent/Blink</c> once that folder exists, else the pre-migration
        /// <c>Assets/EnemyContent/Blink</c>. Existence-probed (not model-probed) because this
        /// tool must stage into the migrated root even when it is still empty.
        /// </summary>
        private static string StageDir =>
            AssetDatabase.IsValidFolder(StageContentDir) ? StageContentDir : StageResourcesDir;

        private static string MatDir      => StageDir + "/Materials";
        private static string TexDir      => StageDir + "/Textures";
        private static string AnimDir     => StageDir + "/Anim";
        private static string BossAnimDir => StageDir + "/AnimBoss";

        // Archetype -> vendor mesh FBX + skin-1 vendor prefab. ADDITIVE naming
        // (Blink_ prefix + Blink/ subfolder) so the Tripo Orc_Warrior/Tank/Mage
        // family in Resources/Enemies is never shadowed or overwritten.
        private struct Archetype { public string Name; public string MeshFbx; public string SkinPrefab; }
        private static readonly Archetype[] Archetypes =
        {
            new Archetype { Name = "Warrior", MeshFbx = PackRoot + "/Meshes_Orcs/Orc_Warrior.fbx", SkinPrefab = PackRoot + "/Prefabs_Orcs/Orc_Warrior1.prefab" },
            new Archetype { Name = "Hunter",  MeshFbx = PackRoot + "/Meshes_Orcs/Orc_Hunter.fbx",  SkinPrefab = PackRoot + "/Prefabs_Orcs/Orc_Hunter1.prefab" },
            new Archetype { Name = "Warlock", MeshFbx = PackRoot + "/Meshes_Orcs/Orc_Warlock.fbx", SkinPrefab = PackRoot + "/Prefabs_Orcs/Orc_Warlock1.prefab" },
            new Archetype { Name = "Boss",    MeshFbx = PackRoot + "/Meshes_Orcs/Orc_Boss.fbx",    SkinPrefab = PackRoot + "/Prefabs_Orcs/Orc_Boss1.prefab" },
        };

        [MenuItem("Defenders/Enemies/Import Blink Stylized Orcs (WO-680)")]
        public static void Run()
        {
            var report = new List<string>();
            report.Add("=== BlinkOrcImporter (WO-680 — Blink Stylized Orcs activation) ===");

            // Pack gate — Assets/Blink is gitignored; absent on fresh clone/CI.
            if (!AssetDatabase.IsValidFolder(PackRoot))
            {
                Debug.LogWarning("[BlinkOrcImporter] Blink orc pack not found at '" + PackRoot +
                                 "' (Assets/Blink is gitignored — re-import the Stylized Orcs Bundle). " +
                                 "No-op; any previously committed Enemies/Blink mirror keeps working.");
                return;
            }

            EnsureFolder(StageDir);
            EnsureFolder(MatDir);
            EnsureFolder(TexDir);
            EnsureFolder(AnimDir);
            EnsureFolder(BossAnimDir);

            // Dedupe caches: several archetypes share Body_/Eyes_ materials + textures.
            var matCopies = new Dictionary<string, Material>();
            var texCopies = new Dictionary<string, Texture>();

            report.Add("-- Archetypes → " + StageDir + " --");
            int staged = 0;
            foreach (var a in Archetypes)
                if (StageArchetype(a, matCopies, texCopies, report)) staged++;

            report.Add("-- Animation sets --");
            int clipCount     = CopyAnimSet(PackRoot + "/Animations_Orcs", AnimDir, report);
            int bossClipCount = CopyAnimSet(PackRoot + "/Animations_OrcBoss", BossAnimDir, report);
            report.Add($"  copied {clipCount} orc clips + {bossClipCount} boss clips (Humanoid FBX, loop flags asserted)");

            report.Add("-- Controllers (built from the COPIED clips — vendor OrcAnimator has 0 params, unusable) --");
            BuildBlinkOrcController(StageDir + "/BlinkOrc.controller", AnimDir, "Orc", report);
            BuildBlinkOrcController(StageDir + "/BlinkOrcBoss.controller", BossAnimDir, "OrcBoss", report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string marker = staged == Archetypes.Length ? "BLINK_ORC_IMPORT_OK" : "BLINK_ORC_IMPORT_PARTIAL";
            Debug.Log($"[BlinkOrcImporter] DONE — {marker} ({staged}/{Archetypes.Length} archetypes)\n" +
                      string.Join("\n", report));
        }

        // ── Per-archetype staging ────────────────────────────────────────────

        private static bool StageArchetype(Archetype a, Dictionary<string, Material> matCopies,
                                           Dictionary<string, Texture> texCopies, List<string> report)
        {
            string stagedFbx    = $"{StageDir}/Blink_Orc_{a.Name}_Mesh.fbx";
            string stagedPrefab = $"{StageDir}/Blink_Orc_{a.Name}.prefab";

            if (AssetImporter.GetAtPath(a.MeshFbx) == null)
            {
                report.Add($"  Blink_Orc_{a.Name}: MISSING SRC FBX {a.MeshFbx} — skipped");
                return false;
            }
            var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(a.SkinPrefab);
            if (srcPrefab == null)
            {
                report.Add($"  Blink_Orc_{a.Name}: MISSING SRC PREFAB {a.SkinPrefab} — skipped");
                return false;
            }

            // 1. FBX copy (fresh GUID) + Humanoid import settings. The vendor meta is
            // already animationType:3; assert it on the copy + strip camera/light debris.
            AssetDatabase.DeleteAsset(stagedFbx);
            if (!AssetDatabase.CopyAsset(a.MeshFbx, stagedFbx))
            {
                report.Add($"  Blink_Orc_{a.Name}: COPY FAILED {a.MeshFbx} -> {stagedFbx}");
                return false;
            }
            var imp = AssetImporter.GetAtPath(stagedFbx) as ModelImporter;
            if (imp == null) { report.Add($"  Blink_Orc_{a.Name}: NO IMPORTER at {stagedFbx}"); return false; }
            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;      // clips come from the copied Animations sets
            imp.importCameras   = false;
            imp.importLights    = false;
            imp.importVisibility = false;
            imp.SaveAndReimport();

            // Avatar verdict (PeopleCharacterImporter pattern — prove the retarget once).
            var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(stagedFbx);
            var anim  = fbxGo != null ? fbxGo.GetComponentInChildren<Animator>() : null;
            var av    = anim != null ? anim.avatar : null;
            bool humanoid = av != null && av.isValid && av.isHuman;
            if (!humanoid)
            {
                string v = av != null && av.isValid ? "WARN avatar GENERIC" : "FAIL no valid avatar";
                report.Add($"  Blink_Orc_{a.Name}: {v} on staged FBX — NOT staged as prefab (Blink clips would T-pose it)");
                return false;
            }

            // 2. Mirror the vendor skin-1 materials + their Blink textures into
            // committed folders, re-pointing every texture ref onto the copies so
            // the staged set carries no gitignored Assets/Blink GUIDs.
            var srcRenderers = srcPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var matByRendererName = new Dictionary<string, Material[]>();
            foreach (var r in srcRenderers)
            {
                var mats = r.sharedMaterials;
                var copies = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                    copies[i] = MirrorMaterial(mats[i], matCopies, texCopies, report);
                matByRendererName[r.name] = copies;
            }

            // 3. Committed prefab: staged-FBX instance + copied materials, matched to
            // the vendor prefab's renderers BY NAME (same FBX hierarchy → same names).
            AssetDatabase.DeleteAsset(stagedPrefab);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxGo);
            try
            {
                int bound = 0, unmatched = 0;
                foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (matByRendererName.TryGetValue(r.name, out var copies))
                    {
                        // Length-guard: keep the FBX slot count, fill what we have.
                        var target = r.sharedMaterials;
                        for (int i = 0; i < target.Length && i < copies.Length; i++)
                            if (copies[i] != null) target[i] = copies[i];
                        r.sharedMaterials = target;
                        bound++;
                    }
                    else unmatched++;
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(inst, stagedPrefab);
                if (saved == null)
                {
                    report.Add($"  Blink_Orc_{a.Name}: SaveAsPrefabAsset FAILED at {stagedPrefab}");
                    return false;
                }

                // 4. Rig-binding oracle (Tripo-chunk detector — should be OK on Blink).
                var verdict = OrcRigBindingAudit.AuditPrefab(saved, out string detail);
                string bind = verdict == OrcBindingVerdict.Ok ? "binding OK" : $"BINDING {verdict}: {detail}";
                report.Add($"  Blink_Orc_{a.Name}: OK Humanoid avatar, {bound} renderer(s) skinned" +
                           $"{(unmatched > 0 ? $" ({unmatched} unmatched)" : "")}, {bind} -> {stagedPrefab}");
                return verdict == OrcBindingVerdict.Ok;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>CopyAsset a vendor .mat into the committed Materials folder and
        /// re-point every texture property that still references Assets/Blink onto a
        /// committed texture copy. Dedupes by source path (skins share Body_/Eyes_ mats).
        /// Vendor mats are already URP/Lit (shader guid 933532a4… — verified), so no
        /// shader swap is needed. Null/non-Blink materials pass through unchanged.</summary>
        private static Material MirrorMaterial(Material src, Dictionary<string, Material> matCopies,
                                               Dictionary<string, Texture> texCopies, List<string> report)
        {
            if (src == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(srcPath) || !srcPath.StartsWith("Assets/Blink")) return src;
            if (matCopies.TryGetValue(srcPath, out var cached)) return cached;

            string dst = $"{MatDir}/{Path.GetFileName(srcPath)}";
            AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(srcPath, dst))
            {
                report.Add($"    material COPY FAILED: {srcPath}");
                matCopies[srcPath] = src;   // fall back to the vendor asset (works locally)
                return src;
            }
            var copy = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (copy == null) { matCopies[srcPath] = src; return src; }

            foreach (string prop in copy.GetTexturePropertyNames())
            {
                var tex = copy.GetTexture(prop);
                if (tex == null) continue;
                string texPath = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(texPath) || !texPath.StartsWith("Assets/Blink")) continue;
                copy.SetTexture(prop, MirrorTexture(texPath, texCopies, report));
            }
            EditorUtility.SetDirty(copy);
            matCopies[srcPath] = copy;
            return copy;
        }

        private static Texture MirrorTexture(string srcPath, Dictionary<string, Texture> texCopies, List<string> report)
        {
            if (texCopies.TryGetValue(srcPath, out var cached)) return cached;
            string dst = $"{TexDir}/{Path.GetFileName(srcPath)}";
            if (AssetImporter.GetAtPath(dst) == null)
            {
                if (!AssetDatabase.CopyAsset(srcPath, dst))
                {
                    report.Add($"    texture COPY FAILED: {srcPath}");
                    var orig = AssetDatabase.LoadAssetAtPath<Texture>(srcPath);
                    texCopies[srcPath] = orig;
                    return orig;
                }
            }
            var copy = AssetDatabase.LoadAssetAtPath<Texture>(dst);
            texCopies[srcPath] = copy;
            return copy;
        }

        // ── Animation staging ────────────────────────────────────────────────

        /// <summary>Copy every anim FBX under the vendor set (recursive) flat into the
        /// staging dir, keep Humanoid import + animation ON, and assert loopTime on the
        /// looping clips (Idle/Run/Strafe/*Loop) so locomotion never one-shots.</summary>
        private static int CopyAnimSet(string srcRoot, string dstDir, List<string> report)
        {
            if (!AssetDatabase.IsValidFolder(srcRoot))
            {
                report.Add($"  anim set MISSING: {srcRoot}");
                return 0;
            }
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { srcRoot }))
            {
                string src = AssetDatabase.GUIDToAssetPath(guid);
                if (!src.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                string dst = $"{dstDir}/{Path.GetFileName(src)}";
                AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(src, dst))
                {
                    report.Add($"    clip COPY FAILED: {src}");
                    continue;
                }
                var imp = AssetImporter.GetAtPath(dst) as ModelImporter;
                if (imp != null)
                {
                    bool dirty = imp.animationType != ModelImporterAnimationType.Human;
                    imp.animationType = ModelImporterAnimationType.Human;
                    if (!imp.importAnimation) { imp.importAnimation = true; dirty = true; }
                    if (EnsureLoopFlags(imp, Path.GetFileNameWithoutExtension(dst))) dirty = true;
                    if (dirty) imp.SaveAndReimport();
                }
                count++;
            }
            return count;
        }

        /// <summary>Set loopTime on clips whose FBX name marks a cycle
        /// (Idle / Run / Strafe / Loop). Returns true if anything changed.</summary>
        private static bool EnsureLoopFlags(ModelImporter imp, string baseName)
        {
            string n = baseName.ToLowerInvariant();
            bool shouldLoop = n.Contains("idle") || n.Contains("run") ||
                              n.Contains("strafe") || n.Contains("loop");
            if (!shouldLoop) return false;

            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return false;

            bool changed = false;
            foreach (var c in clips)
            {
                if (c.loopTime) continue;
                c.loopTime = true;
                changed = true;
            }
            if (changed) imp.clipAnimations = clips;
            return changed;
        }

        // ── Controller build (BuildOrcController standard, Blink clip sources) ──

        /// <summary>Build a Blink orc controller at <paramref name="path"/> from the
        /// copied clip set in <paramref name="animDir"/> (clip files named
        /// <paramref name="prefix"/>_*). Mirrors PeopleCharacterImporter.BuildOrcController:
        /// Speed 1-D locomotion blend (useAutomaticThresholds=false — the classic slide
        /// bug guard), InCombat idle swap, Attack/Cast/Hit one-shots, Dead hold; params are
        /// exactly what ActorAnimator drives (Has()-guarded, so extras are harmless).
        /// The set has NO walk clip (runs + strafes only), so the blend is idle@0/run@1.5 —
        /// orcs move ~2.1–3.0 m/s, fully inside the run band.</summary>
        private static void BuildBlinkOrcController(string path, string animDir, string prefix, List<string> report)
        {
            AnimationClip idle       = LoadClipAtPath($"{animDir}/{prefix}_Idle.fbx");
            AnimationClip run        = LoadClipAtPath($"{animDir}/{prefix}_RunForward.fbx");
            AnimationClip combatIdle = LoadClipAtPath($"{animDir}/{prefix}_IdleCombat.fbx");
            AnimationClip attack     = LoadClipAtPath($"{animDir}/{prefix}_MeleeAttack_OneHanded.fbx");
            AnimationClip cast       = LoadClipAtPath($"{animDir}/{prefix}_SpellCast.fbx");
            AnimationClip hit        = LoadClipAtPath($"{animDir}/{prefix}_GetHit.fbx");
            AnimationClip death      = LoadClipAtPath($"{animDir}/{prefix}_Death.fbx");

            if (idle == null || run == null)
            {
                report.Add($"  {Path.GetFileName(path)}: SKIPPED — core clips missing " +
                           $"(idle={(idle != null)}, run={(run != null)}) in {animDir}");
                return;
            }

            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Cast",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",     AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Injured",  AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            var loco = sm.AddState("Locomotion");
            sm.defaultState = loco;
            var blend = new BlendTree
            {
                name = "Locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, ctrl);
            loco.motion = blend;
            blend.AddChild(idle, 0f);
            blend.AddChild(run, 1.5f);   // no walk clip in the pack — run covers the 2.1–3.0 m/s orc band

            // Combat locomotion: IdleCombat swap while InCombat (SkeletonHumanoid pattern).
            AnimatorState combatLoco = null;
            if (combatIdle != null)
            {
                combatLoco = sm.AddState("CombatLocomotion");
                var cblend = new BlendTree
                {
                    name = "CombatLocomotion", blendType = BlendTreeType.Simple1D,
                    blendParameter = "Speed", useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(cblend, ctrl);
                combatLoco.motion = cblend;
                cblend.AddChild(combatIdle, 0f);
                cblend.AddChild(run, 1.5f);

                var toCombat = loco.AddTransition(combatLoco);
                toCombat.hasExitTime = false; toCombat.duration = 0.25f;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
                var toCalm = combatLoco.AddTransition(loco);
                toCalm.hasExitTime = false; toCalm.duration = 0.25f;
                toCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            }

            AddOneShot(sm, loco, combatLoco, "Attack", attack, 1.1f, 0.8f);
            AddOneShot(sm, loco, combatLoco, "Cast",   cast,   1.0f, 0.85f);
            AddOneShot(sm, loco, combatLoco, "Hit",    hit,    1.0f, 0.8f);

            if (death != null)
            {
                var st = sm.AddState("Death");
                st.motion = death;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.15f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            }

            EditorUtility.SetDirty(ctrl);
            report.Add($"  {Path.GetFileName(path)} built: Locomotion(idle/run)" +
                       $"{(combatLoco != null ? " + CombatLocomotion" : "")}" +
                       $"{(attack != null ? " + Attack" : "")}" +
                       $"{(cast != null ? " + Cast" : "")}" +
                       $"{(hit != null ? " + Hit" : "")}" +
                       $"{(death != null ? " + Death" : "")} " +
                       "[Speed/InCombat/Attack/Cast/Hit/Dead/Injured] ✓");
        }

        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState loco, AnimatorState combatLoco,
                                       string trigger, AnimationClip clip, float speed, float exitTime)
        {
            if (clip == null) return;
            var st = sm.AddState(trigger);
            st.motion = clip;
            st.speed  = speed;
            var t = sm.AddAnyStateTransition(st);
            t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (combatLoco != null)
            {
                var toCombat = st.AddTransition(combatLoco);
                toCombat.hasExitTime = true; toCombat.exitTime = exitTime; toCombat.duration = 0.2f;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            }
            var back = st.AddTransition(loco);
            back.hasExitTime = true; back.exitTime = exitTime; back.duration = 0.2f;
        }

        /// <summary>Motion clip from an FBX by full path, skipping __preview__ and
        /// T-pose/bind takes (PeopleCharacterImporter.LoadClipAtPath pattern).</summary>
        private static AnimationClip LoadClipAtPath(string fullPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fullPath);
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
