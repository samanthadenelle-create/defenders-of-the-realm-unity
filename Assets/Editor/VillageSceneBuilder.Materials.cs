using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: mesh bounds, color/material/hex helpers, build settings, folders -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static Bounds ComputeMeshBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Builds a flat-colour URP material. A bare
        /// <c>new Material(Shader.Find("Universal Render Pipeline/Lit"))</c>
        /// renders WHITE in batchmode regardless of <c>_BaseColor</c> — it never
        /// goes through URP's material import/validation, so its shader keywords
        /// are not set up. The fix: COPY a known-good URP/Lit asset material (the
        /// KayKit atlas <c>.mat</c>, proven to render — the buildings use it) so
        /// the copy inherits the full keyword/property setup, then drop its
        /// texture and recolour it.
        /// </summary>
        private static Material MakeFlatMaterial(Color color)
        {
            Material mat;
            var baseMat = HexMaterial();
            if (baseMat != null)
            {
                mat = new Material(baseMat);          // inherits proper URP setup
                mat.SetTexture("_BaseMap", null);     // drop the atlas → flat colour
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            return mat;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = MakeFlatMaterial(color);
        }

        /// <summary>
        /// Flat-colour URP material on EVERY renderer of an instance (root +
        /// children, all submesh slots). Used where a KayKit model's atlas
        /// material does not resolve and a clean solid tint is wanted instead.
        /// </summary>
        private static void ApplyColorAll(GameObject go, Color color)
        {
            if (go == null) return;
            var mat = MakeFlatMaterial(color);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[slots];
                for (int i = 0; i < slots; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void ApplyEmissive(GameObject go, Color color, float intensity)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = MakeFlatMaterial(color);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            renderer.sharedMaterial = mat;
        }

        // ── Shared hex-atlas material (importer-remap fallback) ──────────────
        // The whole Medieval Hexagon Pack UV-maps onto ONE shared atlas
        // (hexagons_medieval.png), so a single URP material renders every model
        // in it correctly. Most instances resolve their material through the
        // FBX importer's external-material remap — but the decoration/nature
        // meshes (the Elarion tree + standing stones) render white because that
        // remap does not resolve at runtime (see unity-decisions.md 2026-05-19).
        // ForceHexMaterial side-steps the importer entirely by assigning the
        // shared .mat straight onto the scene renderers.
        private static Material _hexMaterial;

        private static Material HexMaterial()
        {
            if (_hexMaterial != null) return _hexMaterial;
            _hexMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                HexDecoNature + "hexagons_medieval_URP.mat");
            if (_hexMaterial == null)
                Debug.LogWarning("[VillageSceneBuilder] hexagons_medieval_URP.mat not found -- " +
                                 "run KayKitMaterials.FixAllMaterials first; nature meshes may " +
                                 "render untextured.");
            return _hexMaterial;
        }

        /// <summary>
        /// Force-assigns the shared hex-atlas URP material to every renderer of a
        /// model instance. No-op when the material can't be loaded (the instance
        /// then keeps whatever the importer gave it).
        /// </summary>
        private static void ForceHexMaterial(GameObject instance)
        {
            var mat = HexMaterial();
            if (mat == null || instance == null) return;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[slots];
                for (int i = 0; i < slots; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Parses a 6-digit hex string into a Color.</summary>
        private static Color HexColor(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.magenta;
        }

        /// <summary>Short alias for <see cref="HexColor"/> (placeholder palette).</summary>
        private static Color C(string rrggbb) => HexColor(rrggbb);

        // =====================================================================
        //  Build Settings + folder helpers
        // =====================================================================

        private static void EnsureBuildSettings()
        {
            var current = EditorBuildSettings.scenes;
            bool hasVillage = false;
            foreach (var s in current)
            {
                if (s.path == VillageScenePath) { hasVillage = true; break; }
            }
            if (hasVillage) return;

            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(TitleScenePath)) scenes.Add(new EditorBuildSettingsScene(TitleScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(VillageScenePath, true));
            if (File.Exists(DungeonScenePath)) scenes.Add(new EditorBuildSettingsScene(DungeonScenePath, true));
            if (File.Exists(BattleScenePath)) scenes.Add(new EditorBuildSettingsScene(BattleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
