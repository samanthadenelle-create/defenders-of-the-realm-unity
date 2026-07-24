// =============================================================================
// VfxGalleryBuilder — lays out EVERY catalogued VFX effect (all HovlVfxCatalog
// rows: PP_* Unity Particle Pack + Hovl keys) into ONE labeled grid scene the
// owner can open + Play to browse the whole usable palette and pick which effect
// goes on swords / staffs / turrets / dragon breath.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (Editor-only)
//
// The catalog type (HovlVfxCatalog) lives in DeNelle.Village which DeNelle.Editor
// does NOT reference (CLAUDE.md §5) — so we read the .asset's Rows[] via
// SerializedObject (Key + Prefab), exactly like MotionCasterWindow.LoadHovlVfxKeys.
//
// Each effect instance has playOnAwake+loop forced ON so the grid continuously
// showcases in Play mode; a TextMesh label above each shows its catalog KEY.
//
// Run (editor CLOSED — project lock): menu  Defenders/VFX/Build VFX Gallery Scene
//   or batchmode -executeMethod DeNelle.Editor.VfxGalleryBuilder.Build
// Output scene: Assets/Scenes/VfxGallery.unity  (dev showcase — not a ship scene).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Editor utility: builds a labeled grid scene of every catalogued VFX effect.</summary>
    public static class VfxGalleryBuilder
    {
        private const string CatalogPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";
        private const string OutScene    = "Assets/Scenes/VfxGallery.unity";
        private const int    Cols        = 8;
        private const float  SpacingX    = 6f;
        private const float  SpacingZ    = 6f;
        private const float  LabelHeight = 3.2f;

        [MenuItem("Defenders/VFX/Build VFX Gallery Scene")]
        public static void Build()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogPath);
            if (cat == null) { Debug.LogError("VFX_GALLERY_FAIL: catalog not found at " + CatalogPath); return; }

            // Read Rows[] (Key + Prefab) without a type ref, via SerializedObject.
            var so   = new SerializedObject(cat);
            var rows = so.FindProperty("Rows");
            if (rows == null || !rows.isArray) { Debug.LogError("VFX_GALLERY_FAIL: Rows[] not found on catalog."); return; }

            var items = new List<KeyValuePair<string, GameObject>>();
            for (int i = 0; i < rows.arraySize; i++)
            {
                var r   = rows.GetArrayElementAtIndex(i);
                var key = r.FindPropertyRelative("Key")?.stringValue;
                var pf  = r.FindPropertyRelative("Prefab")?.objectReferenceValue as GameObject;
                if (!string.IsNullOrEmpty(key) && pf != null)
                    items.Add(new KeyValuePair<string, GameObject>(key, pf));
            }
            if (items.Count == 0) { Debug.LogError("VFX_GALLERY_FAIL: no catalog rows with a prefab."); return; }
            items.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key)); // PP_* cluster together

            // Fresh empty scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (dark backdrop so effects read).
            var camGo = new GameObject("Gallery Camera");
            var cam   = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f, 1f);
            camGo.tag = "MainCamera";

            // Fill light so lit-particle layers (smoke) aren't black.
            var sunGo = new GameObject("Sun");
            var sun   = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var root = new GameObject("VFX Gallery").transform;
            int rowCount = (items.Count + Cols - 1) / Cols;

            for (int i = 0; i < items.Count; i++)
            {
                int c = i % Cols, rw = i / Cols;
                var pos = new Vector3(c * SpacingX, 0f, -rw * SpacingZ);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(items[i].Value);
                inst.transform.SetParent(root, true);
                inst.transform.position = pos;
                inst.transform.rotation = Quaternion.identity;

                // Force every particle system to continuously showcase in Play mode.
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.playOnAwake = true;
                    main.loop        = true;
                }

                // Floating key label.
                var labelGo = new GameObject("Label_" + items[i].Key);
                labelGo.transform.SetParent(root, true);
                labelGo.transform.position = pos + new Vector3(0f, LabelHeight, 0f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text          = items[i].Key;
                tm.characterSize = 0.12f;
                tm.fontSize      = 72;
                tm.anchor        = TextAnchor.MiddleCenter;
                tm.alignment     = TextAlignment.Center;
                tm.color         = Color.white;
            }

            // Frame the whole grid from front-above, and billboard the labels to the camera.
            float gridW   = (Cols - 1) * SpacingX;
            float gridD   = (rowCount - 1) * SpacingZ;
            var   center  = new Vector3(gridW * 0.5f, 1.5f, -gridD * 0.5f);
            camGo.transform.position = center + new Vector3(0f, gridD * 0.55f + 8f, gridD * 0.55f + 14f);
            camGo.transform.LookAt(center);
            cam.farClipPlane = Mathf.Max(1000f, gridD * 3f + 200f);

            foreach (Transform child in root)
                if (child.name.StartsWith("Label_"))
                    child.rotation = camGo.transform.rotation; // billboard toward camera

            Directory.CreateDirectory("Assets/Scenes");
            bool ok = EditorSceneManager.SaveScene(scene, OutScene);
            if (!ok) { Debug.LogError("VFX_GALLERY_FAIL: could not save " + OutScene); return; }

            Debug.Log($"VFX_GALLERY_OK: {items.Count} effects laid out in {OutScene} ({Cols} cols x {rowCount} rows). Open it and press Play to see them all loop.");
        }
    }
}
