// =============================================================================
// BowPropBuilder — turns the committed KayKit bow FBX into the Resources prefab
// that HeroBowAttachment loads ("Heroes/Props/Bow"), replacing the procedural bow.
// -----------------------------------------------------------------------------
// The KayKit Adventurers bow + a class atlas were copied into
// Assets/Resources/Heroes/Props/ (the pack itself is gitignored, so a committed
// copy is the build-safe path). This builds a URP/Lit material wiring the atlas
// into _BaseMap (KayKit's flat palette look), assigns it to the bow's renderers,
// strips colliders, and saves Bow.prefab. Logs the bow's bounds so the grip
// scale in HeroBowAttachment can be tuned.
//
//   Defenders > Heroes > Build Bow Prop
//   (batchmode: DeNelle.Editor.BowPropBuilder.Build)
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BowPropBuilder
    {
        private const string Dir       = "Assets/Resources/Heroes/Props";
        private const string FbxPath   = Dir + "/Bow.fbx";
        private const string TexPath   = Dir + "/ranger_texture.png";
        private const string MatPath   = Dir + "/Bow.mat";
        private const string PrefabOut = Dir + "/Bow.prefab";

        [MenuItem("Defenders/Heroes/Build Bow Prop")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceSynchronousImport);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (tex == null) { Debug.LogError("[BowPropBuilder] Atlas texture not found at " + TexPath); return; }

            // URP/Lit material with the KayKit atlas — flat low-poly look (no spec/metal).
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "Bow" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, MatPath);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError("[BowPropBuilder] Bow FBX not found at " + FbxPath); return; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.name = "Bow";

            // Apply the atlas material to every renderer; strip physics (cosmetic prop).
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) if (c != null) Object.DestroyImmediate(c);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Object.DestroyImmediate(rb);

            PrefabUtility.SaveAsPrefabAsset(inst, PrefabOut);
            Object.DestroyImmediate(inst);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BowPropBuilder] DONE — built {PrefabOut}. Bow world bounds size = {bounds.size} " +
                      $"(use this to tune HeroBowAttachment grip scale). BOW_PROP_OK");
        }
    }
}
