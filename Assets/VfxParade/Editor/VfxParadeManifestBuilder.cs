// =============================================================================
// DeNelle.Editor.VfxParadeManifestBuilder - bakes effect prefabs into the build.
// -----------------------------------------------------------------------------
// EDITOR step that scans Assets/Spells Pack for effect prefabs (default: paths
// containing "Casting", to keep the build lean) and writes/updates a
// VfxParadeManifest ScriptableObject at Assets/Resources/VfxParade/
// VfxParadeManifest.asset holding DIRECT GameObject references to those prefabs.
//
// WHY DIRECT REFERENCES: the Spells Pack folder is gitignored and lives OUTSIDE
// Resources, so a runtime build can NOT load its prefabs by path. A direct
// reference from a ScriptableObject that IS in Resources forces each referenced
// prefab into the player build, so the runtime overlay (VfxParadeRuntime) can
// spawn them in a standalone exe.
//
// Run from the menu (Tools > VFX Parade > Build Runtime Manifest ...) or headless
// via batchmode:
//   -executeMethod DeNelle.Editor.VfxParadeManifestBuilder.Build     (Casting only, lean)
//   -executeMethod DeNelle.Editor.VfxParadeManifestBuilder.BuildAll  (WHOLE Spells Pack, ~466)
// Prints the ASCII marker "VFX_PARADE_MANIFEST_OK count=<n>" on success.
// ASCII-only strings throughout.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VfxParade;

namespace DeNelle.Editor
{
    public static class VfxParadeManifestBuilder
    {
        private const string SourceFolder = "Assets/Spells Pack";
        private const string DefaultCategory = "Casting"; // keep the build lean
        private const string ManifestDir = "Assets/Resources/VfxParade";
        private const string ManifestAssetPath = "Assets/Resources/VfxParade/VfxParadeManifest.asset";

        [MenuItem("Tools/VFX Parade/Build Runtime Manifest (Casting)")]
        public static void BuildMenu()
        {
            Build();
        }

        [MenuItem("Tools/VFX Parade/Build Runtime Manifest (FULL Spells Pack)")]
        public static void BuildAllMenu()
        {
            BuildAll();
        }

        /// <summary>Batchmode entry point. Scans the source folder for prefabs whose
        /// path contains DefaultCategory and writes the manifest asset.</summary>
        public static void Build()
        {
            BuildForCategory(DefaultCategory);
        }

        /// <summary>Batchmode entry point for the WHOLE Spells Pack (~466 prefabs):
        /// no category filter, every prefab under the source folder is baked in.
        /// Headless: -executeMethod DeNelle.Editor.VfxParadeManifestBuilder.BuildAll.</summary>
        public static void BuildAll()
        {
            BuildForCategory(null);
        }

        /// <summary>Scan + bake for an explicit category substring (empty/null = all
        /// prefabs in the source folder).</summary>
        public static void BuildForCategory(string category)
        {
            try
            {
                if (!AssetDatabase.IsValidFolder(SourceFolder))
                {
                    Debug.LogWarning("[VfxParade] source folder is not a valid project folder: " +
                                     SourceFolder + " (Spells Pack is gitignored - is it imported?)");
                    Debug.Log("VFX_PARADE_MANIFEST_OK count=0");
                    return;
                }

                bool filter = !string.IsNullOrEmpty(category);
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder });
                var paths = new List<string>();
                if (guids != null)
                {
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (filter && path.IndexOf(category, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        paths.Add(path);
                    }
                }
                paths.Sort(StringComparer.OrdinalIgnoreCase);

                EnsureFolder();

                var manifest = AssetDatabase.LoadAssetAtPath<VfxParadeManifest>(ManifestAssetPath);
                bool created = false;
                if (manifest == null)
                {
                    manifest = ScriptableObject.CreateInstance<VfxParadeManifest>();
                    created = true;
                }
                if (manifest.entries == null) manifest.entries = new List<VfxParadeEntry>();
                manifest.entries.Clear();

                int loaded = 0;
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    GameObject prefab = null;
                    try
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[VfxParade] failed to load prefab '" + path + "': " + e.Message);
                        prefab = null;
                    }
                    if (prefab == null)
                    {
                        Debug.LogWarning("[VfxParade] skipping null/broken prefab at '" + path + "'.");
                        continue;
                    }

                    manifest.entries.Add(new VfxParadeEntry
                    {
                        prefab = prefab,                                  // DIRECT ref - forces into build
                        path = path,
                        name = Path.GetFileNameWithoutExtension(path)
                    });
                    loaded++;
                }

                if (created)
                    AssetDatabase.CreateAsset(manifest, ManifestAssetPath);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[VfxParade] manifest " + (created ? "created" : "updated") + " at " +
                          ManifestAssetPath + " (category='" + (filter ? category : "ALL") + "').");
                Debug.Log("VFX_PARADE_MANIFEST_OK count=" + loaded);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] manifest build failed: " + e.Message);
                Debug.Log("VFX_PARADE_MANIFEST_OK count=0");
            }
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(ManifestDir)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ManifestDir))
                AssetDatabase.CreateFolder("Assets/Resources", "VfxParade");
        }
    }
}
