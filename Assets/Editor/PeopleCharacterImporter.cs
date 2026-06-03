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
    }
}
