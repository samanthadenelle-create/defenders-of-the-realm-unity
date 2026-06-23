// =============================================================================
// KnightOrcImportGate — WO-481 Phase-1 (the rig/anim GATE, instrument-don't-guess).
// -----------------------------------------------------------------------------
// Imports the NEW ARMORED Tripo models staged (non-destructively) under
// Assets/Art/Incoming_Tripo/ and REPORTS, headless, the facts that gate every
// downstream phase:
//   • does each map to a valid Mecanim HUMANOID avatar? (so the donor Humanoid
//     clips in Assets/Action/ retarget onto the new armored body for free)
//   • how many mesh SECTIONS / submeshes (owner says ~6 → Mesh Baker 6→1 target)
//   • mesh height (upright/scale sanity) + material-slot count
//   • any animation clips embedded in the FBX
//
// Uses the SAME import settings HeroFbxImporter (WO-286) proved on the existing
// heroes: Read/Write + Humanoid + CreateFromThisModel + wiped humanDescription
// (so the avatar auto-maps from the NEW rig, no stale-meta T-pose).
//
// NON-DESTRUCTIVE: never touches Assets/Resources/Heroes/Knight.fbx (the naked
// body) — git preserves it; the donor clips are external (Assets/Action/), so the
// naked Knight is not even needed as a donor. Promotion of the armored body into
// the Resources runtime path is a SEPARATE, later step (after this gate + owner OK).
//
//   run-unity-method.ps1 -Method DeNelle.Editor.KnightOrcImportGate.Run -LogName knight-orc-gate.log
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>WO-481 Phase-1: import + Humanoid-config + audit the staged armored models.</summary>
    public static class KnightOrcImportGate
    {
        private static readonly string[] Models =
        {
            "Assets/Art/Incoming_Tripo/Heroes/Knight/Knight.fbx",
            "Assets/Art/Incoming_Tripo/Enemies/Orcs/Orc_Warrior/Orc_Warrior.fbx",
            "Assets/Art/Incoming_Tripo/Enemies/Orcs/Orc_Tank/Orc_Tank.fbx",
            "Assets/Art/Incoming_Tripo/Enemies/Orcs/Orc_Mage/Orc_Mage.fbx",
        };

        [MenuItem("Defenders/Tripo/Knight+Orc Import Gate (WO-481)")]
        public static void Run()
        {
            int total = 0, humanoidOk = 0;
            var report = new StringBuilder();
            report.AppendLine("[KnightOrcGate] WO-481 Phase-1 — import + rig/section/clip audit");

            foreach (var path in Models)
            {
                total++;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    report.AppendLine($"  MISSING (not imported): {path}");
                    Debug.LogError($"[KnightOrcGate] FBX not found / not imported: {path}");
                    continue;
                }

                // Proven WO-286 settings (HeroFbxImporter): Read/Write + Humanoid +
                // regenerate avatar from THIS rig + wipe stale humanDescription so the
                // new rig auto-maps (no preserved-meta T-pose).
                importer.isReadable    = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                var hd = importer.humanDescription;
                hd.human    = new HumanBone[0];
                hd.skeleton = new SkeletonBone[0];
                importer.humanDescription = hd;
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();

                // Avatar validity (the gate question).
                bool human = false, valid = false;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Avatar av) { human = av.isHuman; valid = av.isValid; }

                // Sections (sum of submeshes across skinned renderers) + height + materials.
                int sections = 0, materials = 0;
                float meshY = -1f;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr.sharedMesh != null)
                        {
                            sections += smr.sharedMesh.subMeshCount;
                            if (meshY < 0f) meshY = smr.sharedMesh.bounds.size.y;
                        }
                        materials += smr.sharedMaterials.Length;
                    }
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) sections += mf.sharedMesh.subMeshCount;
                        materials += mr.sharedMaterials.Length;
                    }
                }

                // Any animation clips shipped inside the FBX.
                var clips = new List<string>();
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is AnimationClip c && !c.name.StartsWith("__preview")) clips.Add(c.name);

                if (valid && human) humanoidOk++;
                report.AppendLine(
                    $"  {Path.GetFileName(path)}: Humanoid(valid={valid}, human={human})  " +
                    $"sections={sections}  materials={materials}  meshY={meshY:F2}  " +
                    $"embeddedClips=[{string.Join(",", clips)}]");
            }

            report.AppendLine(humanoidOk == total
                ? $"[KnightOrcGate] {humanoidOk}/{total} mapped Humanoid. KNIGHT_ORC_GATE_OK — donor Assets/Action clips will retarget."
                : $"[KnightOrcGate] {humanoidOk}/{total} mapped Humanoid. KNIGHT_ORC_GATE_INCOMPLETE — see non-Humanoid rows above.");
            Debug.Log(report.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
