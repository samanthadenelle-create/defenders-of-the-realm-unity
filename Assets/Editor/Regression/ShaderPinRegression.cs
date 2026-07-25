// =============================================================================
// ShaderPinRegression [shader-pin] -- proves the URP shaders survive a build (no
// pink/magenta materials in the player because a shader was stripped).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Two proofs:
//   (1) a FIRST-PARTY IPreprocessBuildWithReport hook exists AND references
//       EnsureShadersIncluded (so the always-included list is pinned on every build),
//       and EnsureShadersIncluded itself is present, AND
//   (2) GraphicsSettings.asset's m_AlwaysIncludedShaders ALREADY contains
//       "Universal Render Pipeline/Lit", "Universal Render Pipeline/Terrain/Lit",
//       "Universal Render Pipeline/Unlit" and "Sprites/Default" (read via
//       SerializedObject -- the same list the pin edits). The last two keep the
//       runtime marker/decal quads (RepairHighlight etc.) off magenta.
//
// Marker: SHADER_PIN_OK / SHADER_PIN_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!ShaderPinRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[shader-pin] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ShaderPinRegression
    {
        private const string UrpLit = "Universal Render Pipeline/Lit";
        private const string UrpTerrainLit = "Universal Render Pipeline/Terrain/Lit";
        // Runtime-built marker/decal quads (RepairHighlight, TowerRangeRing, HeroReachRing,
        // StructureAttackAlert, HeroTargetIndicator, DecalSpawner) Shader.Find these at run
        // time and render MAGENTA if stripped - so the pin MUST include them too.
        private const string UrpUnlit = "Universal Render Pipeline/Unlit";
        private const string SpritesDefault = "Sprites/Default";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SHADER PIN (first-party build hook -> EnsureShadersIncluded + GraphicsSettings always-included) ---");

            // (1a) A first-party IPreprocessBuildWithReport hook exists.
            var hooks = new List<string>();
            var ipbType = typeof(IPreprocessBuildWithReport);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsInterface || t.IsAbstract) continue;
                    if (!ipbType.IsAssignableFrom(t)) continue;
                    if (t.Namespace != null && t.Namespace.StartsWith("DeNelle", StringComparison.Ordinal))
                        hooks.Add(t.FullName);
                }
            }
            log.AppendLine($"  first-party IPreprocessBuildWithReport hooks: {(hooks.Count > 0 ? string.Join(", ", hooks) : "<none>")}");
            if (hooks.Count == 0)
                failures.Add("[shader-pin] no first-party IPreprocessBuildWithReport hook found -- nothing pins shaders on build");

            // (1b) EnsureShadersIncluded exists.
            var ensureT = FindType("DeNelle.Editor.EnsureShadersIncluded");
            if (ensureT == null)
                failures.Add("[shader-pin] DeNelle.Editor.EnsureShadersIncluded not found (the pin utility is gone)");

            // (1c) Some first-party build hook file references EnsureShadersIncluded.
            bool hookReferencesEnsure = false;
            try
            {
                string editorDir = Path.Combine(Application.dataPath, "Editor");
                if (Directory.Exists(editorDir))
                {
                    foreach (var path in Directory.GetFiles(editorDir, "*.cs", SearchOption.AllDirectories))
                    {
                        string text = File.ReadAllText(path);
                        if (text.IndexOf("IPreprocessBuildWithReport", StringComparison.Ordinal) >= 0 &&
                            text.IndexOf("EnsureShadersIncluded", StringComparison.Ordinal) >= 0)
                        { hookReferencesEnsure = true; log.AppendLine("  build hook references EnsureShadersIncluded: " + Path.GetFileName(path)); break; }
                    }
                }
            }
            catch (Exception ex) { log.AppendLine("  hook-scan note: " + ex.Message); }
            if (!hookReferencesEnsure)
                failures.Add("[shader-pin] no build hook file references EnsureShadersIncluded -- the pin is not invoked from OnPreprocessBuild");

            // (2) GraphicsSettings always-included list contains both URP shaders.
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (assets == null || assets.Length == 0 || assets[0] == null)
                {
                    failures.Add("[shader-pin] could not load ProjectSettings/GraphicsSettings.asset");
                }
                else
                {
                    var so = new SerializedObject(assets[0]);
                    var arr = so.FindProperty("m_AlwaysIncludedShaders");
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    if (arr != null && arr.isArray)
                        for (int i = 0; i < arr.arraySize; i++)
                        {
                            var shader = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                            if (shader != null) names.Add(shader.name);
                        }
                    log.AppendLine($"  m_AlwaysIncludedShaders holds {names.Count} shader(s)");
                    if (!names.Contains(UrpLit))
                        failures.Add($"[shader-pin] always-included shaders do NOT contain '{UrpLit}' -- URP Lit would strip (pink materials in the player)");
                    if (!names.Contains(UrpTerrainLit))
                        failures.Add($"[shader-pin] always-included shaders do NOT contain '{UrpTerrainLit}' -- the URP Terrain shader would strip (pink terrain)");
                    if (!names.Contains(UrpUnlit))
                        failures.Add($"[shader-pin] always-included shaders do NOT contain '{UrpUnlit}' -- runtime marker/decal quads (RepairHighlight etc.) would strip (magenta quads in the player)");
                    if (!names.Contains(SpritesDefault))
                        failures.Add($"[shader-pin] always-included shaders do NOT contain '{SpritesDefault}' -- the marker/decal built-in fallback would strip (magenta quads in the player)");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[shader-pin] GraphicsSettings read threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "SHADER_PIN_OK");
                reason = "SHADER PIN OK -- a first-party build hook pins EnsureShadersIncluded and GraphicsSettings always-includes URP Lit + Terrain Lit";
                return true;
            }
            reason = "shader-pin: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "SHADER_PIN_FAIL: " + reason);
            return false;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
