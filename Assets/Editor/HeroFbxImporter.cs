// =============================================================================
// HeroFbxImporter — WO-286: durable import settings for the hero FBX in
// Resources/Heroes/ (mirrors ActionClipImporter's Assets/Action/ approach).
// -----------------------------------------------------------------------------
// The 2026-06-06 hero FBX swap kept the OLD .meta files (to preserve GUIDs), so
// the new Tripo meshes imported with stale settings → solid-green T-pose lying
// flat + "isReadable is false / ReadWrite must be enabled" console spam.
//
// This AssetPostprocessor enforces, for every FBX under Assets/Resources/Heroes/:
//   • Read/Write Enabled — HeroBodySwapper reads baked/shared mesh vertices at
//     runtime to plant the hero's feet; isReadable=false threw every frame.
//   • Humanoid + Create-From-This-Model avatar — regenerates a valid avatar for
//     the NEW rig (the preserved meta's avatar mapped the OLD rig → no retarget →
//     T-pose; once a valid Humanoid avatar exists the idle clip stands it upright).
//
// So any future hero FBX dropped here auto-imports correct — this stops recurring
// on the next swap. The batch method below also logs avatar validity + mesh Y so
// CLI can report which heroes mapped without opening the editor.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>WO-286: forces Read/Write + Humanoid avatar on Resources/Heroes/*.fbx.</summary>
    public sealed class HeroFbxImporter : AssetPostprocessor
    {
        private const string HeroFolder = "Assets/Resources/Heroes/";
        private static readonly string[] HeroFbx = { "Cleric.fbx", "Knight.fbx", "Mage.fbx", "Ranger.fbx" };

        private bool IsHeroFbx
        {
            get
            {
                string p = assetPath.Replace('\\', '/');
                if (!p.StartsWith(HeroFolder, System.StringComparison.OrdinalIgnoreCase)) return false;
                foreach (var f in HeroFbx)
                    if (p.EndsWith("/" + f, System.StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        private void OnPreprocessModel()
        {
            if (!IsHeroFbx) return;
            var importer = (ModelImporter)assetImporter;

            // WO-286 #1: HeroBodySwapper reads sharedMesh/baked vertices at runtime.
            importer.isReadable = true;

            // WO-286 #2: Humanoid + regenerate the avatar from THIS (new) rig.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            // WO-286: WIPE the stale humanDescription inherited from the preserved old
            // .meta. A re-rigged mesh has a new bone hierarchy (e.g. root's parent is
            // 'Cleric', not the old 'Armature'), and Unity validates CreateFromThisModel
            // against the stored map → "Parent for 'root' differs from one found in
            // HumanDescription" → avatar fails. Clearing the human/skeleton arrays forces
            // a fresh auto-map from the new rig; self-heals on every future swap too.
            var hd = importer.humanDescription;
            hd.human    = new HumanBone[0];
            hd.skeleton = new SkeletonBone[0];
            importer.humanDescription = hd;

            // These are visible meshes — keep their materials/textures importing
            // (only fix a fully-stripped slot; never strip a hero like a motion clip).
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        /// <summary>
        /// Headless reimport of the four hero FBX with the WO-286 settings, then a
        /// per-hero diagnostic (Humanoid avatar valid?, mesh Y-extent) so CLI can
        /// report which rigs mapped + sanity-check upright/scale without the editor.
        /// -executeMethod DeNelle.Editor.HeroFbxImporter.FixHeroFbx
        /// </summary>
        [MenuItem("Defenders/Animation/Fix Hero FBX Import (WO-286)")]
        public static void FixHeroFbx()
        {
            foreach (var f in HeroFbx)
            {
                string path = HeroFolder + f;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { Debug.LogWarning("[HeroFbxImporter] FBX not found: " + path); continue; }

                importer.isReadable    = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();

                // Diagnostics — avatar validity + local mesh Y-extent (upright/scale sanity).
                bool human = false, valid = false;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Avatar av) { human = av.isHuman; valid = av.isValid; }

                float meshY = -1f;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null && smr.sharedMesh != null) meshY = smr.sharedMesh.bounds.size.y;
                    else
                    {
                        var mf = go.GetComponentInChildren<MeshFilter>(true);
                        if (mf != null && mf.sharedMesh != null) meshY = mf.sharedMesh.bounds.size.y;
                    }
                }
                Debug.Log($"[HeroFbxImporter] {f}: Read/Write=ON  humanoidAvatar(valid={valid}, human={human})  meshY={meshY:F3}");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HeroFbxImporter] FixHeroFbx done — Read/Write + Humanoid + avatar regen on 4 hero FBX.");
        }
    }
}
