// OrcRigBindingAudit — asset oracle for Tripo orc mesh ↔ skeleton binding.
// RCA 2026-07-11: OrcHumanoid FBXs report OK Humanoid avatar but visible body is
// rigid tripo_part_* chunks — animator drives buried Hip chain, mesh never moves.
// Skeleton family also lists tripo_part_* in import meta but SMRs bind CC_Base bones
// at origin; discriminator = smr.rootBone + bones[] armature membership, NOT name alone.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public enum OrcBindingVerdict
    {
        Ok,
        NoSkinnedMesh,
        UnboundTripoChunks,
        RigidMeshChunks,
        BonesMissingArmature,
        DegenerateAvatar,
    }

    public static class OrcRigBindingAudit
    {
        private static readonly string[] OrcHumanoidModels =
        {
            "Orc_Warrior", "Orc_Tank", "Orc_Mage",
        };

        private static readonly string[] OrcWarbandModels =
        {
            "Orc_Shaman", "Orc_Berserker", "Orc_Necromancer",
        };

        [MenuItem("Defenders/Animation/Audit Orc Rig Binding (Tripo vs AccuRig)")]
        public static void RunMenu()
        {
            if (Run(out string reason))
                Debug.Log("[OrcRigBinding] ORC_BINDING_OK\n" + reason);
            else
                Debug.LogError("[OrcRigBinding] ORC_BINDING_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            foreach (var model in OrcHumanoidModels)
                AuditResourcesModel(model, failures, notes);

            foreach (var model in OrcWarbandModels)
                AuditResourcesModel(model, failures, notes);

            if (failures.Count > 0)
            {
                reason = string.Join(" | ", failures);
                return false;
            }

            var ok = new StringBuilder();
            ok.Append($"orc binding audit passed ({OrcHumanoidModels.Length + OrcWarbandModels.Length} models)");
            if (notes.Count > 0)
                ok.Append(". Notes: ").Append(string.Join("; ", notes));
            reason = ok.ToString();
            return true;
        }

        public static OrcBindingVerdict AuditPrefab(GameObject prefab, out string detail)
        {
            if (prefab == null)
            {
                detail = "prefab null";
                return OrcBindingVerdict.NoSkinnedMesh;
            }

            var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0)
            {
                detail = "no SkinnedMeshRenderer";
                return OrcBindingVerdict.NoSkinnedMesh;
            }

            int rigidChunks = 0;
            foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                if (mr.GetComponent<SkinnedMeshRenderer>() != null) continue;
                if (mr.GetComponent<MeshFilter>()?.sharedMesh != null)
                    rigidChunks++;
            }
            if (rigidChunks > 0)
            {
                detail = $"{rigidChunks} rigid MeshRenderer chunk(s) at root (Tripo export — not skinned)";
                return OrcBindingVerdict.RigidMeshChunks;
            }

            var anim = prefab.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                var av = anim.avatar;
                if (av == null || !av.isValid)
                {
                    detail = "avatar missing or invalid";
                    return OrcBindingVerdict.DegenerateAvatar;
                }
                if (!av.isHuman)
                {
                    detail = "avatar valid but GENERIC (Humanoid clips cannot pose mesh)";
                    return OrcBindingVerdict.DegenerateAvatar;
                }
            }

            foreach (var smr in smrs)
            {
                if (smr == null) continue;
                string rootName = smr.rootBone != null ? smr.rootBone.name : "<none>";
                if (IsTripoChunkName(rootName))
                {
                    detail = $"smr.rootBone={rootName} (mesh bound to rigid Tripo chunk, not armature)";
                    return OrcBindingVerdict.UnboundTripoChunks;
                }

                if (!SmrBonesIncludeArmature(smr))
                {
                    detail = $"smr '{smr.name}' bones[] has no Hip/CC_Base armature bone (rootBone={rootName})";
                    return OrcBindingVerdict.BonesMissingArmature;
                }
            }

            detail = "SMR bound to armature chain";
            return OrcBindingVerdict.Ok;
        }

        private static void AuditResourcesModel(string model, List<string> failures, List<string> notes)
        {
            string path = "Assets/Resources/Enemies/" + model + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"'{model}': MISSING at {path}");
                return;
            }

            var verdict = AuditPrefab(prefab, out string detail);
            if (verdict == OrcBindingVerdict.Ok)
            {
                notes.Add($"'{model}': {detail}");
                return;
            }

            string fix = verdict == OrcBindingVerdict.DegenerateAvatar
                ? "re-import Humanoid (ImportOrcFamily) or hand-map avatar"
                : "NEEDS AccuRig re-export — Unity import cannot rebind unweighted Tripo chunks";
            failures.Add($"'{model}': {verdict} — {detail}. Fix: {fix}");
        }

        internal static bool IsTripoChunkName(string boneName)
        {
            return !string.IsNullOrEmpty(boneName) &&
                   boneName.StartsWith("tripo_part", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsArmatureBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return false;
            if (boneName == "Hip" || boneName == "Pelvis") return true;
            if (boneName.StartsWith("CC_Base", System.StringComparison.Ordinal)) return true;
            if (boneName.StartsWith("L_Thigh", System.StringComparison.Ordinal)) return true;
            if (boneName.StartsWith("R_Thigh", System.StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool SmrBonesIncludeArmature(SkinnedMeshRenderer smr)
        {
            if (smr.bones == null || smr.bones.Length == 0) return false;
            for (int i = 0; i < smr.bones.Length; i++)
            {
                var b = smr.bones[i];
                if (b != null && IsArmatureBoneName(b.name))
                    return true;
            }
            return false;
        }
    }
}