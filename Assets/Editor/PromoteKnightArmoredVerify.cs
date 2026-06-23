// =============================================================================
// PromoteKnightArmoredVerify -- WO-481 Slice 4 promotion gate (batchmode-safe).
// -----------------------------------------------------------------------------
// Verifies that the ARMORED Tripo Knight has been promoted correctly into the
// RUNTIME hero path (Resources/Heroes/Knight.fbx) -- the body HeroBodySwapper
// loads for the playable Knight. This is the headless self-check (CLAUDE.md
// section 12: prove with captured data, do not assume) that the promoted asset:
//   * loads from Resources/Heroes/Knight,
//   * has a SkinnedMeshRenderer with a non-null mesh,
//   * generated a valid + human Avatar (Humanoid rig maps clean),
//   * has the animation-master controller available at Resources/Heroes/Knight,
//   * reports its mesh bounds height (promote-grade scale sanity).
// Prints PROMOTE_KNIGHT_OK or PROMOTE_KNIGHT_FAIL: <reason>.
//
//   run-unity-method.ps1 -Method DeNelle.Editor.PromoteKnightArmoredVerify.Run -LogName promote-knight-verify.log
// =============================================================================

using UnityEngine;

namespace DeNelle.Editor
{
    public static class PromoteKnightArmoredVerify
    {
        private const string KnightResPath = "Heroes/Knight"; // Resources path (no extension)

        public static void Run()
        {
            string reason = null;

            // 1) Load the promoted armored Knight from the runtime Resources path.
            var fbx = Resources.Load<GameObject>(KnightResPath);
            if (fbx == null)
            {
                Fail("Resources.Load<GameObject>(\"" + KnightResPath + "\") returned null -- " +
                     "the armored Knight FBX was not promoted to Resources/Heroes/Knight.fbx.");
                return;
            }

            // 2) SkinnedMeshRenderer + non-null mesh.
            SkinnedMeshRenderer smr = fbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null)
            {
                Fail("no SkinnedMeshRenderer found on the promoted Knight -- the armored body is not skinned.");
                return;
            }
            if (smr.sharedMesh == null)
            {
                Fail("the Knight SkinnedMeshRenderer has a null sharedMesh.");
                return;
            }

            int sections = 0;
            float boundsHeight = -1f;
            foreach (var s in fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s != null && s.sharedMesh != null)
                {
                    sections += s.sharedMesh.subMeshCount;
                    float h = s.sharedMesh.bounds.size.y;
                    if (h > boundsHeight) boundsHeight = h;
                }
            }

            // 3) Generated Avatar: must be valid AND human (Humanoid rig maps clean).
            Avatar avatar = null;
            var animator = fbx.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null) avatar = animator.avatar;
            if (avatar == null)
            {
                // The FBX root may carry the avatar as a sub-asset rather than on an Animator.
                var all = Resources.LoadAll(KnightResPath);
                foreach (var o in all)
                {
                    var av = o as Avatar;
                    if (av != null) { avatar = av; break; }
                }
            }
            if (avatar == null)
            {
                Fail("no generated Avatar found for the promoted Knight -- rig is not Humanoid " +
                     "(animationType must be 3 / Humanoid in Knight.fbx.meta).");
                return;
            }
            if (!avatar.isValid)
            {
                Fail("the generated Knight Avatar is NOT valid (avatar.isValid == false) -- " +
                     "Humanoid bone mapping failed on the armored rig.");
                return;
            }
            if (!avatar.isHuman)
            {
                Fail("the generated Knight Avatar is not Humanoid (avatar.isHuman == false).");
                return;
            }

            // 4) Animation-master controller present at the runtime path (do NOT load the asset
            //    type by Resources path collision -- the controller lives at Heroes/Knight.controller,
            //    which Resources.Load resolves by type when asked for RuntimeAnimatorController).
            var controller = Resources.Load<RuntimeAnimatorController>(KnightResPath);
            if (controller == null)
            {
                Fail("Resources.Load<RuntimeAnimatorController>(\"" + KnightResPath + "\") returned null -- " +
                     "the Knight.controller animation master is missing (retarget source for the armored avatar).");
                return;
            }

            // 5) Mesh bounds height (promote-grade scale sanity) + success line.
            Debug.Log("[PromoteKnightArmoredVerify] promoted Knight: sections=" + sections +
                      " meshBoundsHeight=" + boundsHeight.ToString("F3") +
                      " avatar(valid=" + avatar.isValid + ", human=" + avatar.isHuman + ")" +
                      " controller=" + controller.name +
                      " clips=" + (controller.animationClips != null ? controller.animationClips.Length : 0));

            if (reason == null)
                Debug.Log("[PromoteKnightArmoredVerify] PROMOTE_KNIGHT_OK");
        }

        private static void Fail(string reason)
        {
            Debug.LogError("[PromoteKnightArmoredVerify] PROMOTE_KNIGHT_FAIL: " + reason);
        }
    }
}
