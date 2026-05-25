// =============================================================================
// HeroPortraitGenerator — renders the imported hero FBXs to PNG portraits
// (Mage / Knight / Ranger) so the HeroSelect cards can show a real preview
// instead of the M / K / R glyph fallback. Same trick will work for the
// IntroPets when KayKit pet meshes arrive.
//
// Strategy: spin up an off-screen GameObject hierarchy (camera + light +
// loaded FBX) on a hidden layer, render once to a RenderTexture, encode as
// PNG, drop into Assets/_Modules/Onboarding/Art/heroes/. Idempotent —
// re-running re-renders.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroPortraitGenerator
    {
        // Must live under a Resources/ folder so runtime can Resources.Load it.
        private const string OutputDir = "Assets/_Modules/Onboarding/Resources/HeroPortraits";
        private const int PortraitSize = 512;

        // Render the portraits from the ACTUAL in-game hero bodies (Resources/Heroes
        // — the owner's Tripo models). The old Assets/Models/* paths are gitignored
        // and absent, so portraits were not matching (or not generating) the bodies.
        private static readonly (string Slug, string FbxPath)[] Heroes =
        {
            ("mage",   "Assets/Resources/Heroes/Mage.fbx"),
            ("knight", "Assets/Resources/Heroes/Knight.fbx"),
            ("ranger", "Assets/Resources/Heroes/Ranger.fbx"),
        };

        [MenuItem("Defenders/Onboarding/Generate Hero Portraits")]
        public static void GenerateAll()
        {
            EnsureFolder(OutputDir);
            int wrote = 0, skipped = 0;
            foreach (var (slug, path) in Heroes)
            {
                if (Render(slug, path)) wrote++;
                else skipped++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[HeroPortraitGenerator] Done — wrote {wrote}, skipped {skipped} (FBX missing).");
        }

        private static bool Render(string slug, string fbxPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[HeroPortraitGenerator] FBX not found at '{fbxPath}' — skipping {slug}.");
                return false;
            }

            // Build the throwaway preview rig on a hidden layer so it doesn't
            // disturb the active scene.
            var root = new GameObject($"__portrait_{slug}");
            root.hideFlags = HideFlags.HideAndDontSave;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face camera

            // Auto-frame: compute bounds + place camera in front.
            Bounds bounds = ComputeBounds(instance);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float distance = size * 1.85f + 0.5f;

            var camGo = new GameObject("__portrait_cam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.04f, 0.13f, 1f); // matches card bg
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = distance * 4f;
            cam.transform.position = bounds.center + Vector3.back * distance + Vector3.up * (size * 0.18f);
            cam.transform.LookAt(bounds.center + Vector3.up * (size * 0.05f));

            var lightGo = new GameObject("__portrait_light");
            lightGo.hideFlags = HideFlags.HideAndDontSave;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(28f, 142f, 0f);

            // Render to RenderTexture, copy to Texture2D, encode PNG.
            var rt = new RenderTexture(PortraitSize, PortraitSize, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, PortraitSize, PortraitSize), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            string outPath = Path.Combine(OutputDir, $"{slug}.png").Replace('\\', '/');
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            // Mark it as a Sprite so UI Toolkit can render it as a background image.
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"[HeroPortraitGenerator] Wrote '{outPath}'.");
            return true;
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
