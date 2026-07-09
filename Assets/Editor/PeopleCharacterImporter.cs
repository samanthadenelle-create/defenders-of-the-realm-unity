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

        // WO-445 — the big brutes (Cave Troll / Demon / OgreMage). These FBX already
        // ship in Resources/Enemies as AccuRIG / Character-Creator HUMANOID rigs
        // (CC_Base_* bones). We re-assert Humanoid + a model-generated avatar IN PLACE
        // (Src == Dst, so no copy) so a fresh clone reliably has a valid Humanoid avatar,
        // then build the LargeHumanoid controller they retarget through. EnemyAnimatorFactory
        // routes Troll/Demon/OgreMage → LargeHumanoid (see RigFor).
        private static readonly ModelMap[] Brutes =
        {
            new ModelMap { Src = EnemyDir + "Troll.fbx",    Dst = EnemyDir + "Troll.fbx",    Label = "Troll (Cave Troll)" },
            new ModelMap { Src = EnemyDir + "Demon.fbx",    Dst = EnemyDir + "Demon.fbx",    Label = "Demon" },
            new ModelMap { Src = EnemyDir + "OgreMage.fbx", Dst = EnemyDir + "OgreMage.fbx", Label = "OgreMage" },
        };

        // AccuRig skeleton family (2026-07-05) — four CC_Base humanoid silhouettes that
        // share one biped. Ranger ships as Skeleton_Rogue.fbx (code slug) for back-compat.
        // KayKit Generic HumanoidEnemy clips cannot retarget these rigs — they need the
        // Mixamo SkeletonHumanoid controller built here (same pattern as OrcWarband).
        private static readonly ModelMap[] SkeletonFamily =
        {
            new ModelMap { Src = EnemyDir + "Skeleton_Mage.fbx",    Dst = EnemyDir + "Skeleton_Mage.fbx",    Label = "Skeleton_Mage" },
            new ModelMap { Src = EnemyDir + "Skeleton_Warrior.fbx", Dst = EnemyDir + "Skeleton_Warrior.fbx", Label = "Skeleton_Warrior" },
            new ModelMap { Src = EnemyDir + "Skeleton_Rogue.fbx",   Dst = EnemyDir + "Skeleton_Rogue.fbx",   Label = "Skeleton_Ranger (Rogue slug)" },
            new ModelMap { Src = EnemyDir + "Skeleton_Healer.fbx",  Dst = EnemyDir + "Skeleton_Healer.fbx",  Label = "Skeleton_Healer" },
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

            report.Add("-- Brutes (WO-445) → Resources/Enemies (in-place Humanoid) --");
            foreach (var m in Brutes) EnsureHumanoidInPlace(m, report);

            report.Add("-- Controllers --");
            BuildClericController(report);
            BuildOrcController(report);
            BuildLargeHumanoidController(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] DONE\n" + string.Join("\n", report));
        }

        /// <summary>Rebuild only SkeletonHumanoid.controller (combat locomotion + actions).</summary>
        public static void RebuildSkeletonHumanoidControllerOnly()
        {
            var report = new List<string>();
            BuildSkeletonHumanoidController(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] SKELETON_CTRL_OK\n" + string.Join("\n", report));
        }

        [MenuItem("Defenders/Animation/Import Skeleton Family (AccuRig)")]
        public static void ImportSkeletonFamily()
        {
            var report = new List<string>();
            report.Add("=== ImportSkeletonFamily (AccuRig CC_Base) ===");

            report.Add("-- Skeleton family → Resources/Enemies (in-place Humanoid) --");
            Avatar sharedAv = null;
            foreach (var m in SkeletonFamily)
            {
                EnsureHumanoidInPlace(m, report);
                if (sharedAv == null)
                {
                    var go   = AssetDatabase.LoadAssetAtPath<GameObject>(m.Dst);
                    var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
                    var av   = anim != null ? anim.avatar : null;
                    if (av != null && av.isValid && av.isHuman) sharedAv = av;
                }
            }

            if (sharedAv != null)
                RepairSkeletonAvatars(sharedAv, report);

            report.Add("-- Controllers --");
            BuildSkeletonHumanoidController(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] ImportSkeletonFamily DONE\n" + string.Join("\n", report));
        }

        /// <summary>Copy a proven Humanoid avatar across the skeleton family when a
        /// silhouette's CC_Base rig did not auto-map (same biped, like DEF-221 heroes).</summary>
        private static void RepairSkeletonAvatars(Avatar sourceAv, List<string> report)
        {
            foreach (var m in SkeletonFamily)
            {
                if (SkeletonAvatarVerdict(m.Dst, out _)) continue;

                var imp = AssetImporter.GetAtPath(m.Dst) as ModelImporter;
                if (imp == null) { report.Add($"  {m.Label}: NO IMPORTER — skipped avatar repair"); continue; }

                // Pass 1 — clear any stale copy-from-other state and retry auto-map.
                imp.animationType   = ModelImporterAnimationType.Human;
                imp.importAnimation = false;
                imp.sourceAvatar    = null;
                imp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
                if (SkeletonAvatarVerdict(m.Dst, out string pass1))
                {
                    report.Add($"  {m.Label}: REPAIRED via CreateFromThisModel ({pass1})");
                    continue;
                }

                // Pass 2 — shared CC_Base biped: copy a proven Humanoid avatar (Mage).
                if (sourceAv != null)
                {
                    imp.animationType   = ModelImporterAnimationType.Human;
                    imp.importAnimation = false;
                    imp.sourceAvatar    = sourceAv;
                    imp.SaveAndReimport();
                }
                if (SkeletonAvatarVerdict(m.Dst, out string pass2))
                {
                    report.Add($"  {m.Label}: REPAIRED via shared skeleton avatar ({pass2})");
                    continue;
                }

                // Pass 3 — copy the proven Mage humanoid bone map (same CC_Base naming).
                var mageImp = AssetImporter.GetAtPath(EnemyDir + "Skeleton_Mage.fbx") as ModelImporter;
                if (mageImp != null)
                {
                    imp.animationType     = ModelImporterAnimationType.Human;
                    imp.importAnimation   = false;
                    imp.sourceAvatar      = null;
                    imp.avatarSetup       = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.humanDescription  = mageImp.humanDescription;
                    imp.SaveAndReimport();
                }

                string verdict = SkeletonAvatarVerdict(m.Dst, out string final)
                    ? $"REPAIRED via copied Mage humanoid map ({final})"
                    : "FAIL avatar repair — hand-map in Unity if this silhouette T-poses";
                report.Add($"  {m.Label}: {verdict}");
            }
        }

        private static bool SkeletonAvatarVerdict(string dst, out string detail)
        {
            var go   = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var av   = anim != null ? anim.avatar : null;
            if (av != null && av.isValid && av.isHuman) { detail = "OK Humanoid"; return true; }
            if (av != null && av.isValid)               { detail = "WARN Generic"; return false; }
            detail = "FAIL no avatar";
            return false;
        }

        /// <summary>Build the Humanoid SkeletonHumanoid.controller for the AccuRig
        /// skeleton family. Mixamo Assets/Action clips retarget through each model's
        /// Humanoid avatar; wired with Enemy.cs params (Speed/Attack/Hit/Dead/Injured).</summary>
        private static void BuildSkeletonHumanoidController(List<string> report)
        {
            string path = EnemyDir + "SkeletonHumanoid.controller";

            AnimationClip idle   = LoadClip("Orc Idle") ?? LoadClip("standing idle 01");
            AnimationClip walk   = LoadClip("standing walk forward");
            AnimationClip run    = LoadClip("standing run forward");
            AnimationClip attack = LoadClip("Sword And Shield Attack");
            AnimationClip hit    = LoadClipAtPath("Assets/Action/Shared/Shared_Hit_Reaction.fbx");
            AnimationClip death  = LoadClip("Falling Back Death") ?? LoadClip("Dying") ?? LoadClip("Defeated");
            AnimationClip cast   = LoadClip("Standing 2H Magic Attack 05") ?? LoadClip("Standing 2H Magic Area Attack 01");

            AnimationClip injIdle = LoadClipAtPath("Assets/Action/Enemies/injured idle.fbx");
            AnimationClip injWalk = LoadClipAtPath("Assets/Action/Enemies/injured walk.fbx");
            AnimationClip injRun  = LoadClipAtPath("Assets/Action/Enemies/injured run.fbx");

            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",     AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Injured",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Cast",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("WindUp",   AnimatorControllerParameterType.Trigger);

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
            int n = 0;
            if (idle != null) { blend.AddChild(idle, 0f);   n++; }
            if (walk != null) { blend.AddChild(walk, 1.5f); n++; }
            if (run  != null) { blend.AddChild(run,  3.5f); n++; }

            var injuredState = sm.AddState("InjuredLocomotion");
            var injBlend = new BlendTree
            {
                name = "InjuredLocomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(injBlend, ctrl);
            injuredState.motion = injBlend;
            int injN = 0;
            if (injIdle != null) { injBlend.AddChild(injIdle, 0f);   injN++; }
            if (injWalk != null) { injBlend.AddChild(injWalk, 1.5f); injN++; }
            if (injRun  != null) { injBlend.AddChild(injRun,  3.5f); injN++; }
            if (injN == 0)
            {
                if (idle != null) injBlend.AddChild(idle, 0f);
                if (walk != null) injBlend.AddChild(walk, 1.5f);
                if (run  != null) injBlend.AddChild(run,  3.5f);
            }
            var toInjured = loco.AddTransition(injuredState);
            toInjured.hasExitTime = false; toInjured.duration = 0.2f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "Injured");
            var fromInjured = injuredState.AddTransition(loco);
            fromInjured.hasExitTime = false; fromInjured.duration = 0.2f;
            fromInjured.AddCondition(AnimatorConditionMode.IfNot, 0f, "Injured");

            AnimationClip combatIdle = LoadClipAtPath("Assets/Action/Shared/Shared_Combat_Idle.fbx")
                                       ?? LoadClip("Sword And Shield Idle");
            AnimationClip combatWalk = LoadClipAtPath("Assets/Action/Knight/sword and shield walk.fbx") ?? walk;
            AnimationClip combatRun  = LoadClipAtPath("Assets/Action/Knight/sword and shield run.fbx") ?? run;
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
                if (combatWalk != null) cblend.AddChild(combatWalk, 1.5f);
                if (combatRun  != null) cblend.AddChild(combatRun,  3.5f);

                var toCombat = loco.AddTransition(combatLoco);
                toCombat.hasExitTime = false; toCombat.duration = 0.25f;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
                var toCalm = combatLoco.AddTransition(loco);
                toCalm.hasExitTime = false; toCalm.duration = 0.25f;
                toCalm.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            }

            AnimationClip windUp = LoadClipAtPath("Assets/Action/Standing 2H Magic Attack 01.fbx");

            if (attack != null)
            {
                var st = sm.AddState("Attack");
                st.motion = attack;
                st.speed  = 1.15f;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                SkeletonActionReturn(st, loco, combatLoco, 0.8f, 0.2f);
            }

            if (windUp != null)
            {
                var st = sm.AddState("WindUp");
                st.motion = windUp;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "WindUp");
                SkeletonActionReturn(st, loco, combatLoco, 0.85f, 0.2f);
            }

            if (cast != null)
            {
                var st = sm.AddState("Cast");
                st.motion = cast;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
                SkeletonActionReturn(st, loco, combatLoco, 0.85f, 0.2f);
            }

            if (hit != null)
            {
                var st = sm.AddState("Hit");
                st.motion = hit;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                SkeletonActionReturn(st, loco, combatLoco, 0.8f, 0.2f);
            }

            if (death != null)
            {
                var st = sm.AddState("Death");
                st.motion = death;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.15f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            }

            EditorUtility.SetDirty(ctrl);
            report.Add($"  SkeletonHumanoid.controller built: Locomotion({n} clips)" +
                       $"{(combatLoco != null ? " + CombatLocomotion" : "")}" +
                       $"{(attack != null ? " + Attack" : "")}" +
                       $"{(windUp != null ? " + WindUp" : "")}" +
                       $"{(cast != null ? " + Cast" : "")}" +
                       $"{(hit != null ? " + Hit" : "")}" +
                       $"{(death != null ? " + Death" : "")} [Speed/InCombat/Attack/Hit/Dead/Injured/Cast/WindUp] ✓");
        }

        private static void SkeletonActionReturn(AnimatorState state, AnimatorState loco,
                                                 AnimatorState combatLoco, float exitTime, float duration)
        {
            if (combatLoco != null)
            {
                var toCombat = state.AddTransition(combatLoco);
                toCombat.hasExitTime = true; toCombat.exitTime = exitTime; toCombat.duration = duration;
                toCombat.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            }
            var toCalm = state.AddTransition(loco);
            toCalm.hasExitTime = true; toCalm.exitTime = exitTime; toCalm.duration = duration;
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

        /// <summary>WO-445 — assert Humanoid + a model-generated avatar on an FBX that is
        /// ALREADY at its Resources path (Src == Dst), WITHOUT copy/delete (the brute FBX
        /// is the asset itself, not a People-source we slug-copy). Idempotent: if the rig is
        /// already a valid Humanoid this is a no-op reimport. Reports the avatar verdict so
        /// the LargeHumanoid retarget is proven before the controller is wired.</summary>
        private static void EnsureHumanoidInPlace(ModelMap m, List<string> report)
        {
            var imp = AssetImporter.GetAtPath(m.Dst) as ModelImporter;
            if (imp == null) { report.Add($"  MISSING FBX, skipped: {m.Dst}"); return; }

            bool changed = imp.animationType != ModelImporterAnimationType.Human;
            imp.animationType = ModelImporterAnimationType.Human;
            if (imp.importAnimation)
            {
                imp.importAnimation = false;
                changed = true;
            }
            // Only (re)generate the avatar from the model if it isn't already set to do so —
            // re-running CreateFromThisModel on a hand-mapped rig would discard the mapping.
            if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel &&
                imp.sourceAvatar == null)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (changed) imp.SaveAndReimport();

            var go   = AssetDatabase.LoadAssetAtPath<GameObject>(m.Dst);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var av   = anim != null ? anim.avatar : null;
            string verdict;
            if (av != null && av.isValid && av.isHuman) verdict = "OK Humanoid avatar (retarget ready)";
            else if (av != null && av.isValid)          verdict = "WARN avatar valid but GENERIC (not human) — LargeHumanoid clips will NOT retarget";
            else                                        verdict = "FAIL no valid avatar — rig did NOT map (hand-map needed)";
            report.Add($"  {m.Label}: {verdict}{(changed ? " [reimported]" : " [already Humanoid]")} -> {m.Dst}");
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

        /// <summary>Build the Humanoid OrcWarband controller to the BuildOrcHumanoidController
        /// blend-tree STANDARD: a Speed 1-D locomotion blend (idle@0 / walk@1.5 / run@3.5,
        /// useAutomaticThresholds=false) + an InjuredLocomotion sub-tree (Injured bool) +
        /// Attack / Hit / Death states — the params Enemy.cs / ActorAnimator drive.
        /// FIXES THE SLIDE (WO route-through-AccuRIG): the prior build looked for a
        /// non-existent "simple_walk"/"walk" clip, so the Locomotion tree shipped with NO
        /// walk child (only idle@0 + run@5). A moving orc (~2.4–3.0 m/s) never reached the
        /// run threshold and stayed in a near-idle blend → slid at walk speed. The walk clip
        /// is the SAME one OrcHumanoid uses ("standing walk forward"), and the thresholds are
        /// the same orc-speed-tuned idle@0 / walk@1.5 / run@3.5 (NOT the old walk@2.6 / run@5
        /// values the orc speed never crossed). Injured clips give the wounded low-HP stance.</summary>
        private static void BuildOrcController(List<string> report)
        {
            string path = EnemyDir + "OrcWarband.controller";

            // Locomotion / shared clips — the EXACT Assets/Action sources BuildOrcHumanoidController
            // references (walk = "standing walk forward", the OrcHumanoid walk clip — the slide fix).
            AnimationClip idle   = LoadClip("Orc Idle") ?? LoadClip("standing idle 01");
            AnimationClip walk   = LoadClip("standing walk forward");
            AnimationClip run    = LoadClip("standing run forward");
            AnimationClip attack = LoadClip("Sword And Shield Attack");
            AnimationClip hit    = LoadClipAtPath("Assets/Action/Shared/Shared_Hit_Reaction.fbx");
            AnimationClip death  = LoadClip("Falling Back Death") ?? LoadClip("Dying") ?? LoadClip("Defeated");

            // Injured (wounded) locomotion sub-tree — same source clips as BuildOrcHumanoidController.
            AnimationClip injIdle = LoadClipAtPath("Assets/Action/Enemies/injured idle.fbx");
            AnimationClip injWalk = LoadClipAtPath("Assets/Action/Enemies/injured walk.fbx");
            AnimationClip injRun  = LoadClipAtPath("Assets/Action/Enemies/injured run.fbx");

            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed",   AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Injured", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            // Locomotion — 1-D blend on Speed. ORC-SPEED-TUNED thresholds idle@0 / walk@1.5 /
            // run@3.5 (mirrors BuildOrcHumanoidController): orcs move ~2.09–3.04 m/s, so walk@1.5
            // covers the whole range and run@3.5 catches the fast end / chase rep. CRITICAL:
            // useAutomaticThresholds=false so the explicit thresholds stick (Unity defaults true
            // and overwrites them, skipping walk — the classic slide bug).
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
            if (walk != null) { blend.AddChild(walk, 1.5f); n++; } // orc-speed-tuned walk threshold
            if (run  != null) { blend.AddChild(run,  3.5f); n++; } // orc-speed-tuned run threshold

            // Injured locomotion — a SECOND 1-D Speed blend entered on Injured==true. Falls back
            // to the healthy loco clips if the injured clips are absent (state never empty).
            var injuredState = sm.AddState("InjuredLocomotion");
            var injBlend = new BlendTree
            {
                name = "InjuredLocomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(injBlend, ctrl);
            injuredState.motion = injBlend;
            int injN = 0;
            if (injIdle != null) { injBlend.AddChild(injIdle, 0f);   injN++; }
            if (injWalk != null) { injBlend.AddChild(injWalk, 1.5f); injN++; }
            if (injRun  != null) { injBlend.AddChild(injRun,  3.5f); injN++; }
            if (injN == 0)
            {
                if (idle != null) injBlend.AddChild(idle, 0f);
                if (walk != null) injBlend.AddChild(walk, 1.5f);
                if (run  != null) injBlend.AddChild(run,  3.5f);
            }
            // Loco <-> InjuredLocomotion on the Injured bool.
            var toInjured = loco.AddTransition(injuredState);
            toInjured.hasExitTime = false; toInjured.duration = 0.2f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "Injured");
            var fromInjured = injuredState.AddTransition(loco);
            fromInjured.hasExitTime = false; fromInjured.duration = 0.2f;
            fromInjured.AddCondition(AnimatorConditionMode.IfNot, 0f, "Injured");

            // Attack — Any → Attack on the trigger, snappy, back to Locomotion.
            if (attack != null)
            {
                var st = sm.AddState("Attack");
                st.motion = attack;
                st.speed  = 1.15f;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = st.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.8f; back.duration = 0.2f;
            }

            // Hit — Any → Hit on the trigger, back to Locomotion (the flinch the param drives).
            if (hit != null)
            {
                var st = sm.AddState("Hit");
                st.motion = hit;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                var back = st.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.8f; back.duration = 0.2f;
            }

            // Death — Any → Death while Dead==true, no exit (stays down until destroyed).
            if (death != null)
            {
                var st = sm.AddState("Death");
                st.motion = death;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.15f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            }

            EditorUtility.SetDirty(ctrl);
            report.Add($"  OrcWarband.controller built: Locomotion({n} clips idle/walk/run)" +
                       $" + Injured({injN} clips)" +
                       $"{(attack != null ? " + Attack" : " + [no attack clip]")}" +
                       $"{(hit != null ? " + Hit" : " + [no hit clip]")}" +
                       $"{(death != null ? " + Death" : " + [no death clip]")} " +
                       "[Speed/Attack/Hit/Dead/Injured] ✓");
        }

        /// <summary>WO-445 — build the Humanoid LargeHumanoid.controller for the big brutes
        /// (Cave Troll / Demon / OgreMage). Same retargetable Mixamo Assets/Action library as
        /// OrcWarband (Humanoid clips retarget through the brute's Humanoid avatar), wired with
        /// the exact params Enemy.cs drives (Speed/Attack/Hit/Dead). Locomotion is a 1-D Speed
        /// blend biased SLOW (brute moveSpeed ~1.8–2.0): idle@0, walk@1.8, run@3.5. Attack uses
        /// a heavier 2H swing (with a sword-and-shield fallback); death is a back-fall.</summary>
        private static void BuildLargeHumanoidController(List<string> report)
        {
            string path = EnemyDir + "LargeHumanoid.controller";

            AnimationClip idle   = LoadClip("Orc Idle") ?? LoadClip("standing idle 01") ?? LoadClip("Sword And Shield Idle");
            AnimationClip walk   = LoadClip("standing walk forward") ?? LoadClip("Dwarf Walk") ?? LoadClip("simple_walk");
            AnimationClip run    = LoadClip("standing run forward") ?? LoadClip("Sword And Shield Run");
            AnimationClip attack = LoadClip("Standing 2H Magic Area Attack 01") ?? LoadClip("Sword And Shield Attack");
            AnimationClip death  = LoadClip("Falling Back Death") ?? LoadClip("Dying") ?? LoadClip("Defeated");

            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead",   AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            // Locomotion — 1-D blend on Speed, biased slow for the heavy brute gait.
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
            if (walk != null) { blend.AddChild(walk, 1.8f); n++; }
            if (run  != null) { blend.AddChild(run,  3.5f); n++; }

            // Attack — Any → Attack on the trigger, back to Locomotion after the swing.
            if (attack != null)
            {
                var st = sm.AddState("Attack");
                st.motion = attack;
                st.speed  = 1.0f;   // brutes swing slow + heavy — no snappiness boost
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = st.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.85f; back.duration = 0.1f;
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
            report.Add($"  LargeHumanoid.controller built: Locomotion({n} clips)" +
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

        // Like LoadClip but takes a FULL project path (LoadClip only reaches the
        // Assets/Action/ root; the injured/hit clips live in Shared/ and Enemies/).
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

        /// <summary>Headless entry: import an AccuRIG CC5 hero exported with EMBEDDED
        /// textures (the owner's re-export workflow). Unlike ImportCC5Hero (separate baked
        /// PNGs), this lets Unity import the FBX's standard materials, which already carry
        /// the embedded textures — so RetargetMaterialsToUrp picks them up at runtime and
        /// no separate texture copy / runtime override is needed. Verifies the renderer
        /// actually has a texture so we never ship grey. Ranger -> Resources/Heroes/Ranger.fbx.</summary>
        [MenuItem("Defenders/Animation/Import Ranger (embedded textures)")]
        public static void ImportRangerEmbedded()
        {
            var report = new List<string>();
            report.Add("=== Import Ranger (embedded textures) ===");
            ImportEmbeddedHero("Assets/Models/People/Human/Human_Ranger.fbx", "Ranger", report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeopleCharacterImporter] Ranger (embedded) DONE\n" + string.Join("\n", report));
        }

        /// <summary>Import a CC5 FBX whose textures are EMBEDDED: copy over the slug,
        /// flip to Humanoid (copy Mage avatar if it doesn't auto-map), and import the
        /// FBX's standard materials (materialImportMode=ImportStandard) so the embedded
        /// textures land on the material. Clears the slug's old _tex folder so a prior
        /// separate-bake (FighterClass) diffuse can't be mis-applied. Reports whether the
        /// renderer materials actually carry a texture (the anti-grey gate).</summary>
        private static void ImportEmbeddedHero(string srcFbxPath, string destSlug, List<string> report)
        {
            if (AssetImporter.GetAtPath(srcFbxPath) == null)
            {
                report.Add($"  MISSING SRC: {srcFbxPath}");
                return;
            }

            string destFbx = HeroDir + destSlug + ".fbx";
            string texDir  = HeroDir + destSlug + "_tex";

            // Drop any prior separate-bake textures for this slug so HeroBodySwapper's
            // ApplyExtractedTexture can't paint the OLD model's atlas onto the new mesh.
            if (AssetDatabase.IsValidFolder(texDir)) AssetDatabase.DeleteAsset(texDir);

            AssetDatabase.DeleteAsset(destFbx);
            if (!AssetDatabase.CopyAsset(srcFbxPath, destFbx))
            {
                report.Add($"  COPY FAILED: {srcFbxPath} -> {destFbx}");
                return;
            }

            var imp = AssetImporter.GetAtPath(destFbx) as ModelImporter;
            if (imp == null) { report.Add($"  NO IMPORTER at {destFbx}"); return; }

            imp.animationType      = ModelImporterAnimationType.Human;
            imp.avatarSetup        = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation    = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; // embedded textures ride the material
            imp.SaveAndReimport();

            // Avatar verdict (+ Mage-avatar copy fallback, same as ImportCC5Hero).
            var go   = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var av   = anim != null ? anim.avatar : null;
            string avatarVerdict;
            if (av != null && av.isValid && av.isHuman) avatarVerdict = "OK Humanoid (auto-mapped)";
            else
            {
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
                        : "FAILED — not Humanoid after Mage-avatar copy";
                }
                else avatarVerdict = "FAILED — not Humanoid and no Mage avatar to copy";
            }

            // ANTI-GREY GATE: confirm at least one renderer material carries a base texture.
            int slots = 0, textured = 0;
            go = AssetDatabase.LoadAssetAtPath<GameObject>(destFbx);
            if (go != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        slots++;
                        if (m == null) continue;
                        Texture t = null;
                        if (m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
                        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                        if (t != null) textured++;
                    }
                }
            }

            float height = 0f;
            if (go != null)
            {
                bool any = false; Bounds b = default;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (any) height = b.size.y;
            }

            report.Add($"  src -> dst: {srcFbxPath} -> {destFbx}");
            report.Add($"  avatar: {avatarVerdict}");
            report.Add($"  TEXTURE GATE: {textured}/{slots} material slot(s) carry a base texture " +
                       $"{(textured > 0 ? "(OK — not grey)" : "(*** GREY RISK — 0 textured ***)")}");
            report.Add($"  mesh bounds height: {height:F3} m");
        }
    }
}
