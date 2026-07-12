// =============================================================================
// EnemyAnimatorFactory — one entry point that puts the right SHARED animator
// controller on any enemy mesh (human OR animal), for the runtime-built arena.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The canonical AnimatorSetup (docs/enemy-codex.md §5) builds a tiny set of
// SHARED controllers from KayKit's shared rigs — HumanoidEnemy (Rig_Medium),
// LargeEnemy (Rig_Large), Boss, Dragon — each wired with idle/move/attack/hit/
// death and the exact params Enemy.cs already drives (Speed/Attack/Hit/Dead).
// EnemyAnimatorSetup copies those controllers into Resources/Enemies so this
// runtime factory can load them (the editor pipeline outputs to Generated/).
//
// So animating an enemy is one call: pick the controller by rig family and stamp
// it on the mesh. Enemy.cs's DriveAnimator then makes it walk/attack/die for free.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Rig family of an enemy mesh — selects which shared controller to apply.</summary>
    public enum EnemyRig { HumanoidMedium, HumanoidLarge, Boss, Dragon, OrcWarband, LargeHumanoid, OrcHumanoid, SkeletonHumanoid, BlinkOrc, BlinkOrcBoss }

    public static class EnemyAnimatorFactory
    {
        /// <summary>Resolves the rig family for a Resources/Enemies model name.</summary>
        public static EnemyRig RigFor(string modelName)
        {
            switch (modelName)
            {
                case "Skeleton_Golem": return EnemyRig.HumanoidLarge;
                // WO-445: the big brutes (Cave Troll, Demon, OgreMage) are AccuRIG /
                // Character-Creator HUMANOID rigs (FBX animationType: Humanoid, CC_Base_*
                // bones). They were previously routed to the LargeEnemy controller, but
                // LargeEnemy's clips come from KayKit's GENERIC Rig_Large skeleton — a
                // Generic clip cannot retarget onto a Humanoid avatar, so the brutes
                // T-posed / slid (the WO-445 symptom). They now share the LargeHumanoid
                // controller, built from the Mixamo Assets/Action Humanoid clip library
                // (same retargetable source the OrcWarband family uses) so the brute's
                // Humanoid avatar drives idle/walk/attack/death correctly.
                case "Troll":
                case "Demon":
                case "OgreMage":       return EnemyRig.LargeHumanoid;
                case "Necromancer":    return EnemyRig.Boss;
                case "Dragon":         return EnemyRig.Dragon;
                // DEF-221: the orc family is HUMANOID (Tripo), so it CANNOT use the
                // KayKit Generic HumanoidEnemy controller — it gets its own.
                case "Orc_Berserker":
                case "Orc_Shaman":
                case "Orc_Necromancer": return EnemyRig.OrcWarband;
                // WO-482: the NEW Tripo orc FAMILY (Warrior leader / Tank / Mage — WO-481 roster) is a
                // distinct humanoid rig with its own controller (OrcHumanoid, already in Resources/Enemies,
                // proven by ATB slice 2c). Kept separate from the older OrcWarband orcs so each family
                // drives its own clip set. These are the overworld-encounter combatants.
                case "Orc_Warrior":
                case "Orc_Tank":
                case "Orc_Mage":        return EnemyRig.OrcHumanoid;
                // AccuRig skeleton family (2026-07-05) — CC_Base Humanoid rigs retarget
                // through SkeletonHumanoid (Mixamo), not KayKit Generic HumanoidEnemy.
                case "Skeleton_Mage":
                case "Skeleton_Warrior":
                case "Skeleton_Rogue":
                case "Skeleton_Healer": return EnemyRig.SkeletonHumanoid;
                // WO-680: Blink Stylized Orcs — professionally Humanoid-rigged vendor family
                // staged ADDITIVELY by BlinkOrcImporter into Resources/Enemies/Blink/ (side-by-
                // side with the Tripo orcs for a felt-compare). Their controllers are built from
                // the pack's own 22-clip Humanoid sets (the bundled OrcAnimator.controller has
                // ZERO parameters — a demo asset Enemy.cs cannot drive), wired with the standard
                // ActorAnimator params. Model names carry the Blink/ Resources subfolder.
                case "Blink/Blink_Orc_Warrior":
                case "Blink/Blink_Orc_Hunter":
                case "Blink/Blink_Orc_Warlock": return EnemyRig.BlinkOrc;
                case "Blink/Blink_Orc_Boss":    return EnemyRig.BlinkOrcBoss;
                default:               return EnemyRig.HumanoidMedium; // Minion + legacy Generic
            }
        }

        private static string Controller(EnemyRig rig)
        {
            switch (rig)
            {
                case EnemyRig.HumanoidLarge:  return "LargeEnemy";
                case EnemyRig.Boss:           return "Boss";
                case EnemyRig.Dragon:         return "Dragon";
                case EnemyRig.OrcWarband:     return "OrcWarband";    // DEF-221 Humanoid orc controller
                case EnemyRig.OrcHumanoid:    return "OrcHumanoid";   // WO-482/491 new Tripo orc family base (Orc_Warrior/Tank/Mage)
                case EnemyRig.LargeHumanoid:  return "LargeHumanoid"; // WO-445 Humanoid brute controller (Troll/Demon/OgreMage)
                case EnemyRig.SkeletonHumanoid: return "SkeletonHumanoid";
                case EnemyRig.BlinkOrc:       return "Blink/BlinkOrc";     // WO-680 Blink Stylized Orcs (built from the pack's own clips)
                case EnemyRig.BlinkOrcBoss:   return "Blink/BlinkOrcBoss"; // WO-680 Blink orc boss (OrcBoss_* clip set)
                default:                      return "HumanoidEnemy";
            }
        }

        /// <summary>
        /// WO-491: resolves the per-MODEL controller name for the shared OrcHumanoid rig.
        /// The Tripo orc family (Warrior / Tank / Mage) all pull the SAME rig, so each role
        /// loads a per-role AnimatorOverrideController (OrcHumanoid_Mage/_Warrior/_Tank) that
        /// swaps only its action clips over the shared base (cast / swing / heavy + idle).
        /// An AnimatorOverrideController IS a RuntimeAnimatorController, so Resources.Load
        /// resolves it the same way. Falls back to the base name for any non-role orc.
        /// </summary>
        private static string ControllerForModel(EnemyRig rig, string modelName)
        {
            if (rig == EnemyRig.OrcHumanoid)
            {
                switch (modelName)
                {
                    case "Orc_Mage":    return "OrcHumanoid_Mage";
                    case "Orc_Warrior": return "OrcHumanoid_Warrior";
                    case "Orc_Tank":    return "OrcHumanoid_Tank";
                    default:            return "OrcHumanoid";   // base fallback (other orc-family ids)
                }
            }
            return Controller(rig);
        }

        /// <summary>Stamps the shared controller for <paramref name="modelName"/> onto
        /// <paramref name="visual"/>'s Animator (adds one if missing). Root motion off —
        /// the NavMesh agent / air glide drives position; the clip just animates limbs.
        /// No-op-safe if the controller is absent (run EnemyAnimatorSetup to build it).</summary>
        public static void Apply(GameObject visual, string modelName)
        {
            if (visual == null) return;
            var anim = visual.GetComponentInChildren<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            anim.applyRootMotion = false;

            // WO-53 (perf): off-screen animator culling. EVERY enemy in the project is
            // built through EnemyFactory.Build -> EnemyAnimatorFactory.Apply (the single
            // enemy-creation path), so setting it ONCE here culls every wave/roaming/
            // tribe/outpost/garrison enemy at spawn — no per-call-site edits needed.
            // CullUpdateTransforms keeps the state machine (and any anim events Enemy.cs
            // drives) running while skipping transform/mesh writes when off-camera; we do
            // NOT use CullCompletely (it can desync gameplay-driven anim events).
            if (anim != null) anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            EnemyRig rig = RigFor(modelName);
            // WO-491: per-role override controller for the shared Tripo orc rig (Mage/Warrior/Tank);
            // base name for every other enemy.
            string ctrlName = ControllerForModel(rig, modelName);
            var ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/" + ctrlName);
            // WO-436 (§12): step in/out of the controller load so a HEADLESS capture PROVES
            // whether the RuntimeAnimatorController resolved. Failure B (WO-436 RCA) = this load
            // returns null → the Animator idles in its empty default state → the NavMeshAgent
            // slides the transform with no clip playing ("no animation, slides across ground").
            FlowTrace.Step("EnemyAnim", $"Load controller for {modelName}: {(ctrl == null ? "NULL" : "OK")}");
            // WO-491: if the per-role override asset is missing (e.g. controllers not yet
            // rebuilt), fall back to the shared base so the orc still walks/attacks.
            if (ctrl == null && rig == EnemyRig.OrcHumanoid && ctrlName != "OrcHumanoid")
            {
                ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/OrcHumanoid");
                if (ctrl != null)
                    FlowTrace.Warn("Enemy",
                        $"animator: model '{modelName}' role override 'Enemies/{ctrlName}' MISSING " +
                        "- using shared base 'Enemies/OrcHumanoid' (run BuildOrcHumanoidController)");
                ctrlName = "OrcHumanoid";
            }
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                FlowTrace.Once("Enemy", $"anim-{modelName}",
                    $"animator: model '{modelName}' -> rig {rig} -> controller '{ctrlName}' OK");
                // WO route-through-AccuRIG (§12 self-report): a Humanoid controller's clips can
                // ONLY pose the rig through a valid Humanoid avatar — with none, the Animator holds
                // the bind/T-pose while the NavMeshAgent slides it (the "sliding statue" ship path).
                // Mirror HeroBodySwapper.cs:475 (avatar != null && avatar.isValid). Do NOT hide the
                // enemy — just self-report LOUDLY so a headless run pinpoints the un-mapped model
                // (enemies must still spawn).
                bool humanoidClips = ControllerHasHumanoidClips(ctrl);
                if (anim.isHuman && (anim.avatar == null || !anim.avatar.isValid))
                {
                    FlowTrace.Fail("Enemy",
                        $"animator: model '{modelName}' -> rig {rig} -> controller '{ctrlName}' bound but " +
                        "the Animator has NO valid Humanoid avatar (avatar=" +
                        $"{(anim.avatar == null ? "<null>" : "invalid")}) — a humanoid clip will hold the " +
                        "bind/T-pose while the agent slides it (the sliding-statue path). Re-import the " +
                        "model as Humanoid with a valid avatar (PeopleCharacterImporter).");
                }
                // QA 2026-07-11 (capture-20260711-181253, orcs frozen in T-pose): the guard above
                // was gated 'if (anim.isHuman ...)', so a GENERIC rig under a Humanoid-clip
                // controller — the EXACT failure — never logged. Un-gated: a Humanoid clip cannot
                // pose a Generic rig at all.
                else if (!anim.isHuman && humanoidClips)
                {
                    FlowTrace.Fail("Enemy",
                        $"animator: model '{modelName}' rig is GENERIC but controller '{ctrlName}' " +
                        "carries Humanoid clips — bind/T-pose (sliding statue). Re-import Humanoid " +
                        "(PeopleCharacterImporter).");
                }

                // Deferred pose-verify (§12, mirrors HeroBodySwapper.VerifyHeroPoseThenReport):
                // sample the rig over the next ~8 frames; if nothing ever drives it, FAIL LOUD
                // under [Flow:EnemyPose]. Once per spawned enemy — the verifier component
                // destroys itself after the window, so no per-frame cost remains.
                EnemyPoseVerifier.Attach(visual, anim, modelName, ctrlName, humanoidClips);
            }
            else
            {
                FlowTrace.Warn("Enemy",
                    $"animator: model '{modelName}' -> rig {rig} -> controller 'Enemies/{ctrlName}' " +
                    "MISSING — enemy has no walk/attack/die anim (run EnemyAnimatorSetup)");
                // WO-436 Step 4 (§12 permanent guard): a null controller after every fallback means
                // this enemy WILL slide with no animation. Warn under the EnemyAnim tag so it never
                // silently slides again — a headless capture flags exactly which model type is null.
                FlowTrace.Warn("EnemyAnim",
                    $"Controller NULL for {modelName} ('Enemies/{ctrlName}') — enemy will SLIDE with no " +
                    "animation (run 'Build Animator Controllers' + EnemyAnimatorSetup to populate Resources/Enemies).");
            }
        }

        /// <summary>True if any clip on the controller is Humanoid motion — such a clip
        /// can only pose the rig through a valid Humanoid avatar (Generic rig = T-pose).</summary>
        private static bool ControllerHasHumanoidClips(RuntimeAnimatorController ctrl)
        {
            if (ctrl == null) return false;
            var clips = ctrl.animationClips;
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i].humanMotion) return true;
            return false;
        }
    }

    /// <summary>
    /// Deferred pose-verify for a spawned enemy (QA 2026-07-11 T-pose RCA; mirrors
    /// HeroBodySwapper.VerifyHeroPoseThenReport:1003's shape). Over ~8 frames after spawn,
    /// confirm the Animator actually DRIVES the rig: a clip is playing on layer 0
    /// (GetCurrentAnimatorClipInfoCount(0) > 0) or a sampled bone transform moved. If
    /// nothing ever drives it → FlowTrace.Fail under [Flow:EnemyPose] so a headless run /
    /// F8 capture names the frozen model — the owner is never the detector. Runs ONCE per
    /// spawned enemy, then destroys itself (zero per-frame cost after the window).
    /// </summary>
    internal sealed class EnemyPoseVerifier : MonoBehaviour
    {
        private const int MaxPoseFrames = 8;

        private Animator _anim;
        private string _model;
        private string _ctrl;
        private bool _humanoidClips;

        /// <summary>Attach + start the one-shot verify. No-op if a verifier is already
        /// on this visual (once per spawned enemy).</summary>
        internal static void Attach(GameObject visual, Animator anim, string model,
                                    string ctrl, bool humanoidClips)
        {
            if (visual == null || anim == null) return;
            if (visual.GetComponent<EnemyPoseVerifier>() != null) return;   // already verifying/verified
            if (!visual.activeInHierarchy) return;                          // coroutine needs an active host

            // Headless/culling guard (RCA 2026-07-11): EnemyAnimatorFactory sets
            // cullingMode=CullUpdateTransforms, so with no renderer visible (batchmode /
            // -nographics fleets, or simply offscreen) the Animator legitimately does NOT
            // drive bone transforms — a frozen verdict there is a false positive (proven:
            // Skeleton_Minion flagged frozen headless while visibly walking in the owner's
            // rendered session). Skip the verify instead of FlowTrace.Fail-ing it.
            var guardSmr = anim.GetComponentInChildren<SkinnedMeshRenderer>();
            bool culledOffscreen = anim.cullingMode != AnimatorCullingMode.AlwaysAnimate &&
                                   guardSmr != null && !guardSmr.isVisible;
            if (Application.isBatchMode || culledOffscreen)
            {
                FlowTrace.Step("EnemyPose",
                    $"model={model}: pose-verify skipped (headless/culled — bones not driven offscreen; " +
                    $"batchMode={Application.isBatchMode} cullingMode={anim.cullingMode} " +
                    $"smrVisible={(guardSmr != null ? guardSmr.isVisible.ToString() : "<no-smr>")}).");
                return;
            }

            var v = visual.AddComponent<EnemyPoseVerifier>();
            v._anim = anim;
            v._model = model;
            v._ctrl = ctrl;
            v._humanoidClips = humanoidClips;
            v.StartCoroutine(v.VerifyPoseThenReport());
        }

        private System.Collections.IEnumerator VerifyPoseThenReport()
        {
            // Sample a real bone so "animator says it's playing but bones are frozen"
            // (the exact QA capture) is caught: clip-info can report a clip while a
            // rig-type mismatch leaves every bone at bind pose.
            var smr = _anim != null ? _anim.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            Transform bone = FindSampleBone(_anim, smr);
            Transform hip = _anim != null && _anim.isHuman
                ? _anim.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            Quaternion boneRot = bone != null ? bone.localRotation : Quaternion.identity;
            Vector3 bonePos = bone != null ? bone.localPosition : Vector3.zero;
            Quaternion hipRot = hip != null ? hip.localRotation : Quaternion.identity;
            Vector3 hipPos = hip != null ? hip.localPosition : Vector3.zero;

            bool everPlayed = false;
            bool boneMoved = false;
            bool hipMoved = false;
            for (int i = 0; i < MaxPoseFrames; i++)
            {
                yield return null;
                if (this == null || _anim == null) yield break;
                if (_anim.isActiveAndEnabled && _anim.runtimeAnimatorController != null &&
                    _anim.GetCurrentAnimatorClipInfoCount(0) > 0)
                    everPlayed = true;
                if (bone != null &&
                    (Quaternion.Angle(boneRot, bone.localRotation) > 0.1f ||
                     (bonePos - bone.localPosition).sqrMagnitude > 1e-6f))
                    boneMoved = true;
                if (hip != null &&
                    (Quaternion.Angle(hipRot, hip.localRotation) > 0.1f ||
                     (hipPos - hip.localPosition).sqrMagnitude > 1e-6f))
                    hipMoved = true;
                if (everPlayed && (boneMoved || hipMoved || bone == null)) break;
            }

            bool smrRootIsTripo = smr != null && smr.rootBone != null &&
                                  smr.rootBone.name.StartsWith("tripo_part", System.StringComparison.OrdinalIgnoreCase);
            bool meshUnbound = everPlayed && hipMoved && smrRootIsTripo && !boneMoved;
            bool frozen = !everPlayed || meshUnbound ||
                          (bone != null && !boneMoved && !(hip != null && hipMoved));
            if (frozen)
            {
                string rigType = _anim == null ? "<gone>"
                    : _anim.isHuman ? "Humanoid"
                    : _anim.avatar != null ? "Generic"
                    : "no-avatar";
                var avatar = _anim != null ? _anim.avatar : null;
                string avatarName = avatar != null ? avatar.name : "<none>";
                string avatarValid = avatar != null ? avatar.isValid.ToString() : "<none>";
                string isHuman = _anim != null ? _anim.isHuman.ToString() : "<gone>";
                string sampleBoneName = bone != null ? bone.name : "<none>";
                string hipBoneName = hip != null ? hip.name : "<none>";
                string smrRootName = smr != null && smr.rootBone != null ? smr.rootBone.name : "<none>";
                string verdict = meshUnbound
                    ? "NEEDS_ACCURIG_REEXPORT (animator drives Hip chain; visible mesh is rigid tripo_part chunks)"
                    : (!avatarValid.Equals("True", System.StringComparison.OrdinalIgnoreCase) || rigType == "Generic")
                        ? "DEGENERATE_AVATAR (hand-map or donor repair may help)"
                        : "FROZEN_BIND (re-import Humanoid or check controller)";
                FlowTrace.Fail("EnemyPose",
                    $"id={gameObject.GetInstanceID()} model={_model}: everPlayed={everPlayed} " +
                    $"boneMoved={boneMoved} hipMoved={hipMoved} — frozen T-pose (rig={rigType} vs " +
                    $"{(_humanoidClips ? "Humanoid" : "Generic")} clips on controller '{_ctrl}'). " +
                    $"verdict={verdict} avatar={avatarName} avatarValid={avatarValid} isHuman={isHuman} " +
                    $"sampleBone={sampleBoneName} hipBone={hipBoneName} smrRoot={smrRootName}.");
            }
            else
            {
                FlowTrace.Step("EnemyPose",
                    $"id={gameObject.GetInstanceID()} model={_model}: pose OK " +
                    $"(everPlayed={everPlayed} boneMoved={boneMoved}).");
            }

            Destroy(this);   // one-shot — no per-frame cost after the window
        }

        /// <summary>Pick a representative bone: first skinned bone under the animator
        /// (skip the root — root motion is off, the agent moves the root).</summary>
        private static Transform FindSampleBone(Animator anim, SkinnedMeshRenderer smr)
        {
            if (anim == null) return null;
            if (smr == null)
                smr = anim.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.bones != null)
                for (int i = 0; i < smr.bones.Length; i++)
                    if (smr.bones[i] != null && smr.bones[i] != anim.transform)
                        return smr.bones[i];
            return null;
        }
    }
}
