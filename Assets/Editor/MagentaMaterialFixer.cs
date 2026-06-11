// =============================================================================
// MagentaMaterialFixer — repairs the magenta renderers found by
// MagentaMaterialScanner. WO-409 Bug 1 fix. Idempotent.
// -----------------------------------------------------------------------------
// Strategy (per offending material):
//   * Material exists but uses a built-in / error shader (Standard, Legacy,
//     Specular setup, Hidden/InternalErrorShader): swap to
//     "Universal Render Pipeline/Lit", carrying base colour + main texture +
//     emission via the standard URP-upgrade mapping (_Color->_BaseColor,
//     _MainTex->_BaseMap). This mirrors PolyperfectUrpFix but works on ANY
//     material asset, wherever it lives.
//   * sharedMaterial == null (empty slot): assign a sensible URP/Lit default
//     (a shared neutral material we create once under Assets/Materials).
//
// If every offender is under Assets/polyperfect, this first delegates to the
// existing Defenders/Art/Fix Polyperfect URP Materials pass (PolyperfectUrpFix)
// so we reuse the project's proven fix, then sweeps anything left over.
//
// Material-asset swaps are in-place (same GUID) so baked prefabs/scenes pick up
// the fix with no re-bake. Null-slot fixes that touch prefab/scene instances are
// written back to the prefab asset and the open scene respectively.
//
// Run (batchmode):  -executeMethod DeNelle.Editor.MagentaMaterialFixer.Run
// Menu:             Defenders/Art/Fix Magenta Materials
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class MagentaMaterialFixer
    {
        private const string DefaultMatPath = "Assets/Materials/MagentaFix_DefaultLit.mat";

        [MenuItem("Defenders/Art/Fix Magenta Materials")]
        public static void Run()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[MagentaMaterialFixer] 'Universal Render Pipeline/Lit' not found — is URP installed? Aborting.");
                return;
            }

            // 1) Reuse the project's proven polyperfect fix first (idempotent; no-op if pack absent).
            try { PolyperfectUrpFix.Fix(); }
            catch (System.Exception e) { Debug.LogWarning($"[MagentaMaterialFixer] PolyperfectUrpFix pass skipped: {e.Message}"); }

            // 2) Sweep ALL material assets in the project for built-in/error shaders.
            int matSwaps = SweepAllMaterials(lit);

            // 3) Repair null sharedMaterial slots on prefabs + build scenes with a URP default.
            Material def = GetOrCreateDefaultMaterial(lit);
            int prefabNullFixes = FixNullSlotsInPrefabs(def);
            int sceneNullFixes = FixNullSlotsInScenes(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MagentaMaterialFixer] DONE — converted {matSwaps} built-in/error material asset(s) → URP/Lit; " +
                      $"assigned default to {prefabNullFixes} null prefab slot(s) + {sceneNullFixes} null scene slot(s).");
        }

        // ---- material-asset shader swap (covers built-in + InternalError) ----
        private static int SweepAllMaterials(Shader lit)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            int converted = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                // never touch package/read-only materials we can't author
                if (!path.StartsWith("Assets/")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (UpgradeMaterialToUrp(mat, lit))
                    converted++;
            }
            return converted;
        }

        /// <summary>Swap a built-in / error-shader material to URP/Lit in place. Idempotent. Returns true if changed.</summary>
        public static bool UpgradeMaterialToUrp(Material mat, Shader lit)
        {
            if (mat == null || lit == null) return false;
            var sh = mat.shader;
            string sn = sh != null ? sh.name : "";

            bool needsUpgrade =
                sh == null ||
                sn == "Standard" ||
                sn == "Standard (Specular setup)" ||
                sn.StartsWith("Legacy Shaders/") ||
                sn.Contains("InternalError") ||
                sn.Contains("Hidden/InternalError");

            if (!needsUpgrade) return false;

            // Capture legacy props before the swap drops them.
            Color baseCol = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color emis = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            mat.shader = lit;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (emis.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emis);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            return true;
        }

        // ---- null-slot repair: prefabs --------------------------------------
        private static int FixNullSlotsInPrefabs(Material def)
        {
            if (def == null) return 0;
            int fixes = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.StartsWith("Assets/")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;
                bool dirty = false;

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (AssignDefaultToNullSlots(r, def)) dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixes++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            return fixes;
        }

        // ---- null-slot repair: build scenes ---------------------------------
        private static int FixNullSlotsInScenes(Material def)
        {
            if (def == null) return 0;
            int fixes = 0;
            string activeBefore = SceneManager.GetActiveScene().path;

            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry == null || !entry.enabled) continue;
                string scenePath = entry.path;
                if (string.IsNullOrEmpty(scenePath) || !System.IO.File.Exists(scenePath)) continue;

                Scene scene;
                try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MagentaMaterialFixer] could not open scene {scenePath}: {e.Message}");
                    continue;
                }

                bool dirty = false;
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        if (AssignDefaultToNullSlots(r, def)) dirty = true;

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    fixes++;
                }
            }

            if (!string.IsNullOrEmpty(activeBefore) && System.IO.File.Exists(activeBefore))
            {
                try { EditorSceneManager.OpenScene(activeBefore, OpenSceneMode.Single); }
                catch { /* best-effort restore */ }
            }
            return fixes;
        }

        /// <summary>Fill any null entry in the renderer's shared-material array with the default. Returns true if changed.</summary>
        private static bool AssignDefaultToNullSlots(Renderer r, Material def)
        {
            if (r == null) return false;
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterials = new[] { def };
                EditorUtility.SetDirty(r);
                return true;
            }

            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                {
                    mats[i] = def;
                    changed = true;
                }
            }
            if (changed)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
            return changed;
        }

        // ---- shared default material ----------------------------------------
        private static Material GetOrCreateDefaultMaterial(Shader lit)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(DefaultMatPath);
            if (existing != null) return existing;

            const string dir = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var mat = new Material(lit) { name = "MagentaFix_DefaultLit" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f, 1f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, DefaultMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
