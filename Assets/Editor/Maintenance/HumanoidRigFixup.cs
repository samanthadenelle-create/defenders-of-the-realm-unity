// =============================================================================
// HumanoidRigFixup — flip the 2026-08-09 AccuRig enemy FBXs from Generic to Humanoid
// IN PLACE, and VERIFY the avatar actually came back valid.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS. Seven AccuRig CC_Base meshes were staged into
// Assets/EnemyContent/ and Unity auto-imported them with its DEFAULT
// animationType: 2 (Generic). The rig is fine — every one carries real skinning
// (Deformer/Cluster/Pose) and a 29-34 bone CC_Base skeleton. What is missing is the
// Humanoid AVATAR: without it Unity cannot retarget, so none of the 191 shared
// animationType:3 clips under Assets/Action/** can pose them. A Humanoid clip on a
// rig with no avatar leaves the Animator in bind/T-pose while the NavMeshAgent slides
// it — the "sliding statue" WO-445 symptom.
//
// RE-RUNNING ACCURIG WOULD NOT FIX THIS. The FBX is already correct; the IMPORT is
// what is wrong. This flips the two importer flags and reimports.
//
// ⚠ IN PLACE, NEVER DELETE-AND-REIMPORT. The .meta guids are already written. Deleting
// and re-adding re-randomises them and breaks every reference that points at the mesh.
// AssetImporter.SaveAndReimport preserves the guid.
//
// SELF-VERIFYING (§12): flipping a flag is not proof. After the reimport this reloads
// the Avatar and asserts isHuman && isValid, then prints a per-file verdict —
// mirroring PeopleCharacterImporter.EnsureHumanoidInPlace. Only "OK" is a pass;
// "GENERIC" and "NO AVATAR" both mean it would T-pose on a shipped build.
//
// Run: DeNelle.Editor.HumanoidRigFixup.Run   (batchmode, editor closed)
// Emits: HUMANOID_RIG_OK <ok>/<total>  |  HUMANOID_RIG_FAIL <reason>
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HumanoidRigFixup
    {
        private const string Dir = DeNelle.Core.AssetRoots.EnemyContent;

        /// <summary>The 2026-08-09 AccuRig staging set. Explicit, not a wildcard sweep -
        /// this must never silently re-import an unrelated mesh.</summary>
        private static readonly string[] Targets =
        {
            "Troll", "Troll_Mage", "Troll_Overlord",
            "Orc_Warlord", "Orc_Mage",
            "Skeleton_Golem_NEW", "Necromancer_NEW",
        };

        [MenuItem("Defenders/Maintenance/Fix AccuRig Enemy Rigs (Generic -> Humanoid)")]
        public static void Run()
        {
            var report = new List<string>();
            int ok = 0, changed = 0;

            foreach (var name in Targets)
            {
                string path = Dir + "/" + name + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    report.Add("MISSING  " + name + " (no file at " + path + ")");
                    continue;
                }

                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null)
                {
                    report.Add("NOT-A-MODEL  " + name);
                    continue;
                }

                if (mi.animationType != ModelImporterAnimationType.Human ||
                    mi.avatarSetup   != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.SaveAndReimport();     // preserves the guid
                    changed++;
                }

                // --- VERIFY. A flipped flag is not an avatar. ---
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (avatar == null)
                {
                    // The Avatar can be a sub-asset; sweep the representation.
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (sub is Avatar a) { avatar = a; break; }
                }

                if (avatar == null)
                    report.Add("FAIL     " + name + " — NO AVATAR produced (would T-pose / slide)");
                else if (!avatar.isValid)
                    report.Add("FAIL     " + name + " — avatar INVALID (would T-pose / slide)");
                else if (!avatar.isHuman)
                    report.Add("WARN     " + name + " — avatar valid but GENERIC (cannot retarget shared clips)");
                else
                {
                    report.Add("OK       " + name + " — Humanoid avatar, retarget ready");
                    ok++;
                }
            }

            AssetDatabase.SaveAssets();

            foreach (var line in report) Debug.Log("[HumanoidRigFixup] " + line);
            Debug.Log("[HumanoidRigFixup] reimported " + changed + " of " + Targets.Length + " target(s)");

            if (ok == Targets.Length)
                Debug.Log("HUMANOID_RIG_OK " + ok + "/" + Targets.Length);
            else
                Debug.LogError("HUMANOID_RIG_FAIL " + ok + "/" + Targets.Length +
                               " Humanoid — see the per-file lines above. A non-OK rig ships as a " +
                               "sliding statue; do NOT wire it into RigFor until it reads OK.");
        }
    }
}
