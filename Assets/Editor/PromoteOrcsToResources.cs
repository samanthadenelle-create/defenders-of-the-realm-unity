// =============================================================================
// PromoteOrcsToResources — WO-481 slice 2a: make the new Tripo Orc family
// runtime-loadable so AtbCombatantSwapper can stage them in the ATB battle.
// -----------------------------------------------------------------------------
// Copies the 3 staged Orcs (Warrior/Tank/Mage) from Assets/Art/Incoming_Tripo/
// into Assets/Resources/Enemies/ (ADDITIVE — these names don't exist there yet,
// so nothing is overwritten), imports each as Humanoid + Read/Write + a native
// ~1.9-2.15m scale, and reports avatar validity so CLI confirms they load before
// the swapper wiring. Textures land in Resources/Enemies/OrcTex/ for the runtime
// TripoMaterialFixer fallback (slice 2c). No live-code changes.
//
//   run-unity-method.ps1 -Method DeNelle.Editor.PromoteOrcsToResources.Run -LogName promote-orcs.log
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PromoteOrcsToResources
    {
        private const string Src = "Assets/Art/Incoming_Tripo/Enemies/Orcs/";
        private const string DstDir = "Assets/Resources/Enemies";
        private const string TexDir = "Assets/Resources/Enemies/OrcTex";

        // name -> target stage height (tank is the meaty one)
        private static readonly (string name, float height)[] Orcs =
        {
            ("Orc_Warrior", 2.00f),  // leader
            ("Orc_Tank",    2.15f),
            ("Orc_Mage",    1.85f),
        };

        [MenuItem("Defenders/Tripo/Promote Orcs to Resources (WO-481 2a)")]
        public static void Run()
        {
            Directory.CreateDirectory(DstDir);
            Directory.CreateDirectory(TexDir);

            int ok = 0;
            foreach (var (name, height) in Orcs)
            {
                string srcFbx = $"{Src}{name}/{name}.fbx";
                string dstFbx = $"{DstDir}/{name}.fbx";
                if (!File.Exists(srcFbx)) { Debug.LogError($"[PromoteOrcs] missing staged fbx: {srcFbx}"); continue; }

                File.Copy(srcFbx, dstFbx, true);
                foreach (var map in new[] { "basecolor", "metallic", "normal", "roughness" })
                {
                    string s = $"{Src}{name}/{name}_{map}.jpg";
                    if (File.Exists(s)) File.Copy(s, $"{TexDir}/{name}_{map}.jpg", true);
                }
                AssetDatabase.ImportAsset(dstFbx, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(dstFbx) as ModelImporter;
                if (importer == null) { Debug.LogError($"[PromoteOrcs] import failed: {dstFbx}"); continue; }
                importer.isReadable    = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                var hd = importer.humanDescription;
                hd.human = new HumanBone[0]; hd.skeleton = new SkeletonBone[0];
                importer.humanDescription = hd;
                importer.SaveAndReimport();

                float meshY = MeshY(dstFbx);
                if (meshY > 0.001f && meshY < 1.4f)
                {
                    importer.useFileScale = false;
                    importer.globalScale  = height / meshY;
                    importer.SaveAndReimport();
                }

                bool human = false, valid = false;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(dstFbx))
                    if (a is Avatar av) { human = av.isHuman; valid = av.isValid; }
                if (valid && human) ok++;
                Debug.Log($"[PromoteOrcs] {name}: Humanoid(valid={valid}, human={human})  meshY(native)={MeshY(dstFbx):F2}  -> Resources/Enemies/{name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PromoteOrcs] {ok}/{Orcs.Length} orcs promoted Humanoid. " +
                (ok == Orcs.Length ? "PROMOTE_ORCS_OK" : "PROMOTE_ORCS_INCOMPLETE"));
        }

        private static float MeshY(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return -1f;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return (smr != null && smr.sharedMesh != null) ? smr.sharedMesh.bounds.size.y : -1f;
        }
    }
}
