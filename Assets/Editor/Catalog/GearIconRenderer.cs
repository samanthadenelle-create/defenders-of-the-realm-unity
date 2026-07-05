// =============================================================================
// GearIconRenderer — editor pass that renders a REAL item thumbnail (PNG) for
// every gear catalog entry (weapons.json / armor.json) that has no iconPath yet,
// so the store shows a picture of the ACTUAL item ("one buy button which renders
// a picture of the actual item" — owner). Sibling of GearCatalogGenerator.
// -----------------------------------------------------------------------------
// docs/ITEM_MODEL.md §3 (the catalog⊥repo LOOK half: visual = prefabPath + iconPath,
// iconPath MISSING today) + docs/STORE_EQUIP_SPEC.md. WO-Item icon pass.
//
// WHAT IT DOES, per weapon/armor entry whose iconPath is null/empty:
//   1. RESOLVE the source prefab:
//        - Blink/Addressable row (prefabPath starts "gear/"): look up the
//          Addressables "Gear" group entry whose address == prefabPath (the
//          BlinkAddressableMarker stored it) → entry.guid → GUIDToAssetPath → load.
//        - Resources row (loadVia null/"resources"): Resources.Load(prefabPath).
//   2. RENDER a thumbnail via AssetPreview.GetAssetPreview (ASYNC — request, then
//      poll AssetPreview.IsLoadingAssetPreview / retry up to a cap until non-null).
//   3. SAVE Assets/Resources/ItemIcons/<id>.png, import as a Sprite (alpha, no
//      compression banding), set the entry iconPath = "ItemIcons/<id>" (Resources-
//      relative, no extension) so it is Resources-loadable at runtime.
//   4. WRITE both Resources + StreamingAssets copies of weapons.json / armor.json
//      in sync (mirrors GearCatalogGenerator's MergeAndWrite — NEVER overwrites a
//      manual:true or a hand-authored row's existing data; only fills iconPath).
//
// IDEMPOTENT: an entry that already has an iconPath AND an existing PNG on disk is
// skipped — a second run with no new entries is a no-op (same bytes out).
// SAFE: if AssetPreview can't produce a preview (shader/material issue, absent pack)
// it FlowTrace.Warn / LogWarning and LEAVES iconPath null (the store falls back to
// the category emoji) — never crashes, never writes a broken path.
//
// Run: Defenders > Catalog > Render Gear Icons
//   or headless -executeMethod DeNelle.Editor.Catalog.GearIconRenderer.RenderIcons
// EDITOR-ONLY. Reads assets + Addressables settings, writes PNGs + the gear JSON.
// Does NOT run Unity gameplay; does NOT commit.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Geometry;

namespace DeNelle.Editor.Catalog
{
    /// <summary>Renders a real PNG thumbnail for every gear entry missing an iconPath,
    /// writing the icon to Resources/ItemIcons and stamping iconPath onto the JSON
    /// (idempotent, manual/hand-authored-preserving). WO-Item icon pass.</summary>
    public static class GearIconRenderer
    {
        // Canonical gear JSON — BOTH copies written in sync (the CanonicalJson law),
        // matching GearCatalogGenerator's paths exactly.
        private const string WeaponsResources = "Assets/Resources/Data/Canonical/weapons.json";
        private const string WeaponsStreaming = "Assets/StreamingAssets/Data/Canonical/weapons.json";
        private const string ArmorResources   = "Assets/Resources/Data/Canonical/armor.json";
        private const string ArmorStreaming    = "Assets/StreamingAssets/Data/Canonical/armor.json";

        // Where the rendered icons land. Resources-relative iconPath = "ItemIcons/<id>".
        private const string IconAssetFolder = "Assets/Resources/ItemIcons";
        private const string IconResourcePrefix = "ItemIcons/";

        // Addressable address prefix the Blink rows use (shared with BlinkAddressableMarker).
        private const string AddressablePrefix = "gear/";

        // AssetPreview is async + thumbnail-cache-bounded. Poll up to this many editor
        // ticks per asset, calling Repaint-equivalent churn between, before giving up.
        private const int MaxPreviewPolls = 200;

        [MenuItem("Defenders/Catalog/Render Gear Icons")]
        public static void RenderIcons()
        {
            Directory.CreateDirectory(Path.GetFullPath(IconAssetFolder));

            // Build the address→guid map ONCE from the Addressables "Gear" group.
            Dictionary<string, string> addrToGuid = BuildAddressableGuidMap();

            int rendered = 0, skipped = 0, failed = 0;
            var failures = new List<string>();

            rendered += ProcessCatalog("weapons", WeaponsResources, WeaponsStreaming,
                                       addrToGuid, ref skipped, ref failed, failures);
            rendered += ProcessCatalog("armor", ArmorResources, ArmorStreaming,
                                       addrToGuid, ref skipped, ref failed, failures);

            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine($"[GearIconRenderer] Rendered {rendered}, skipped {skipped} " +
                          $"(already had icon), failed {failed} (no prefab / no preview).");
            if (failures.Count > 0)
            {
                sb.AppendLine("  Failures (iconPath left null → store falls back to emoji):");
                foreach (var f in failures) sb.AppendLine($"    - {f}");
            }
            Debug.Log(sb.ToString().TrimEnd());
        }

        // =====================================================================
        // Per-catalog pass
        // =====================================================================

        /// <summary>Process one catalog (weapons|armor): render an icon for each row with a
        /// null/empty iconPath, then write BOTH copies in sync if anything changed. Returns
        /// the number of icons rendered.</summary>
        private static int ProcessCatalog(string arrayKey, string resourcesPath, string streamingPath,
            Dictionary<string, string> addrToGuid, ref int skipped, ref int failed, List<string> failures)
        {
            JObject root = ReadJsonObject(resourcesPath) ?? ReadJsonObject(streamingPath);
            if (root == null)
            {
                Debug.LogWarning($"[GearIconRenderer] {arrayKey}: no catalog JSON found at " +
                                 $"{resourcesPath} (or streaming) — nothing to do.");
                return 0;
            }

            JArray rows = root[arrayKey] as JArray;
            if (rows == null || rows.Count == 0)
            {
                Debug.LogWarning($"[GearIconRenderer] {arrayKey}: empty '{arrayKey}' array — nothing to do.");
                return 0;
            }

            int rendered = 0;
            bool dirty = false;

            foreach (var tok in rows)
            {
                if (!(tok is JObject row)) continue;

                string id = row.Value<string>("id");
                if (string.IsNullOrEmpty(id)) continue;

                string existingIcon = row.Value<string>("iconPath");

                // IDEMPOTENT: skip a row that already has an iconPath AND the PNG exists.
                if (!string.IsNullOrEmpty(existingIcon) && IconPngExists(existingIcon))
                {
                    skipped++;
                    continue;
                }

                // RESOLVE the source prefab.
                GameObject prefab = ResolvePrefab(row, addrToGuid, out string why);
                if (prefab == null)
                {
                    failed++;
                    string msg = $"{id}: {why}";
                    failures.Add(msg);
                    FlowTrace.Warn("GearIcon", msg);
                    continue;
                }

                // RENDER → PNG bytes (Y-long / X-narrow first so icon silhouette == held shape).
                float heldLen = TargetHeldLength(row);
                byte[] png = RenderPreviewPngOriented(prefab, heldLen, row);
                if (png == null)
                {
                    failed++;
                    string msg = $"{id}: AssetPreview produced no preview for '{prefab.name}' " +
                                 "(shader/material issue?) — iconPath left null.";
                    failures.Add(msg);
                    FlowTrace.Warn("GearIcon", msg);
                    continue;
                }

                // SAVE + import as Sprite.
                string iconAssetPath = $"{IconAssetFolder}/{id}.png";
                if (!SavePngAndImportAsSprite(iconAssetPath, png))
                {
                    failed++;
                    string msg = $"{id}: failed to write/import PNG at {iconAssetPath} — iconPath left null.";
                    failures.Add(msg);
                    FlowTrace.Warn("GearIcon", msg);
                    continue;
                }

                row["iconPath"] = IconResourcePrefix + id; // "ItemIcons/<id>", no extension
                rendered++;
                dirty = true;
                FlowTrace.Step("GearIcon", $"{id}: rendered → {IconResourcePrefix + id}");
            }

            if (dirty)
            {
                if (root["version"] == null) root["version"] = 1;
                string json = root.ToString(Newtonsoft.Json.Formatting.Indented);
                WriteUtf8NoBom(resourcesPath, json);
                WriteUtf8NoBom(streamingPath, json);
                Debug.Log($"[GearIconRenderer] {arrayKey}: wrote {rendered} new iconPath(s) " +
                          $"to both copies ({resourcesPath} + StreamingAssets).");
            }

            return rendered;
        }

        // =====================================================================
        // RESOLVE — prefab for a row (Addressable Blink vs Resources)
        // =====================================================================

        /// <summary>Resolve the source prefab for a gear row. Blink/Addressable rows (prefabPath
        /// starts "gear/") resolve via the Addressables "Gear" group address→guid map; Resources
        /// rows load via Resources.Load. Returns null + a reason string on failure.</summary>
        private static GameObject ResolvePrefab(JObject row, Dictionary<string, string> addrToGuid, out string why)
        {
            why = null;
            string prefabPath = row.Value<string>("prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
            {
                why = "no prefabPath on the row — cannot render (store keeps the emoji).";
                return null;
            }

            string loadVia = row.Value<string>("loadVia");
            bool addressable = string.Equals(loadVia, "addressable", StringComparison.OrdinalIgnoreCase)
                               || prefabPath.StartsWith(AddressablePrefix, StringComparison.OrdinalIgnoreCase);

            if (addressable)
            {
                if (addrToGuid == null || addrToGuid.Count == 0)
                {
                    why = $"prefabPath '{prefabPath}' is Addressable but the 'Gear' group is " +
                          "empty/absent (gitignored Blink pack not imported, or marker not run).";
                    return null;
                }
                if (!addrToGuid.TryGetValue(prefabPath, out string guid) || string.IsNullOrEmpty(guid))
                {
                    why = $"no Addressables 'Gear' entry with address '{prefabPath}'.";
                    return null;
                }
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    why = $"address '{prefabPath}' resolved guid {guid} but no asset path.";
                    return null;
                }
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null)
                    why = $"address '{prefabPath}' → {assetPath}, but it is not a loadable GameObject.";
                return go;
            }

            // Resources row.
            var res = Resources.Load<GameObject>(prefabPath);
            if (res == null)
                why = $"Resources.Load('{prefabPath}') returned null (asset moved/absent?).";
            return res;
        }

        /// <summary>Build an address→guid map from the Addressables "Gear" group (the marker
        /// stored each Blink prefab under a stable address). Empty when settings/group absent.</summary>
        private static Dictionary<string, string> BuildAddressableGuidMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.SettingsExists
                ? AddressableAssetSettingsDefaultObject.Settings
                : null;
            if (settings == null)
            {
                FlowTrace.Warn("GearIcon", "Addressables settings null — no Blink (gear/*) icons " +
                                           "can be resolved this run (Resources rows still render).");
                return map;
            }

            AddressableAssetGroup group = settings.FindGroup(BlinkAddressableMarker.GearGroup);
            if (group == null)
            {
                FlowTrace.Warn("GearIcon", $"Addressables group '{BlinkAddressableMarker.GearGroup}' " +
                                           "not found — no Blink (gear/*) icons resolvable this run.");
                return map;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.address) || string.IsNullOrEmpty(entry.guid))
                    continue;
                if (!map.ContainsKey(entry.address)) map[entry.address] = entry.guid;
            }
            return map;
        }

        // Canonical held lengths (mirror EquipmentController presets) for icon framing.
        private static float TargetHeldLength(JObject row)
        {
            switch ((row.Value<string>("category") ?? "").ToLowerInvariant())
            {
                case "bow":    return 0.92f;
                case "dagger": return 0.40f;
                case "axe":    return 0.80f;
                case "hammer":
                case "mace":   return 0.85f;
                case "staff":  return 1.30f;
                case "wand":   return 0.45f;
                case "shield": return 0.48f;
                default:       return 0.95f; // sword / unknown melee
            }
        }

        /// <summary>Instantiate, seat Y-long X-narrow, then capture the oriented instance so the
        /// shop icon reads the same shape the hero holds.</summary>
        private static byte[] RenderPreviewPngOriented(GameObject prefab, float heldLength, JObject row)
        {
            var root = new GameObject("GearIconOrientRoot") { hideFlags = HideFlags.HideAndDontSave };
            GameObject inst = null;
            try
            {
                inst = UnityEngine.Object.Instantiate(prefab);
                inst.hideFlags = HideFlags.HideAndDontSave;
                var grip = new GameObject("GearIconGrip") { hideFlags = HideFlags.HideAndDontSave };
                grip.transform.SetParent(root.transform, false);
                string cat = (row?.Value<string>("category") ?? "").ToLowerInvariant();
                bool resolveHilt = cat != "bow" && cat != "shield" && cat != "staff" && cat != "wand";
                WeaponBoundsOrient.NormalizeInto(inst, grip.transform, heldLength,
                    WeaponBoundsOrient.GripAnchor.Centre, resolveHilt);
                return RenderPreviewPng(inst);
            }
            finally
            {
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =====================================================================
        // RENDER — AssetPreview (async) → PNG bytes
        // =====================================================================

        /// <summary>Request AssetPreview.GetAssetPreview (async, thumbnail-cache-bounded), polling
        /// IsLoadingAssetPreview until a non-null preview arrives or the poll cap is hit. Encodes
        /// the resulting Texture2D to PNG. Returns null when no preview could be produced.</summary>
        private static byte[] RenderPreviewPng(GameObject prefab)
        {
            var entityId = prefab.GetEntityId(); // was GetInstanceID — only consumed by IsLoadingAssetPreview below

            // Kick off the async preview request.
            Texture2D preview = AssetPreview.GetAssetPreview(prefab);

            int polls = 0;
            while (preview == null && polls < MaxPreviewPolls)
            {
                // Yield to the editor preview-render loop. AssetPreview renders on the main
                // thread between ticks; pumping the asset DB + a tiny spin lets it complete.
                if (!AssetPreview.IsLoadingAssetPreview(entityId) && polls > 2)
                {
                    // Not loading and still null after a couple of ticks: re-request once
                    // (the cache may have evicted/not-started) then keep polling.
                    preview = AssetPreview.GetAssetPreview(prefab);
                }
                System.Threading.Thread.Sleep(20);
                preview = AssetPreview.GetAssetPreview(prefab);
                polls++;
            }

            if (preview == null) return null;

            // AssetPreview textures are not CPU-readable directly; blit through a temp RT
            // into a readable Texture2D, then EncodeToPNG.
            return EncodeReadable(preview);
        }

        /// <summary>Copy a (possibly non-readable) source texture into a readable RGBA32
        /// Texture2D via a temporary RenderTexture, then encode to PNG.</summary>
        private static byte[] EncodeReadable(Texture src)
        {
            int w = src.width, h = src.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                readable.Apply();
                byte[] png = readable.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(readable);
                return png;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // =====================================================================
        // SAVE — write PNG + import as a Sprite
        // =====================================================================

        /// <summary>Write the PNG bytes to the asset path and import it as a Sprite (single,
        /// alpha-from-input, no compression so there is no banding on the flat icon). Returns
        /// false on any IO/import failure.</summary>
        private static bool SavePngAndImportAsSprite(string assetPath, byte[] png)
        {
            try
            {
                string full = Path.GetFullPath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[GearIconRenderer] no TextureImporter for {assetPath}.");
                    return false;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // no banding
                importer.SaveAndReimport();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GearIconRenderer] failed to save/import {assetPath}: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // I/O helpers (mirror GearCatalogGenerator)
        // =====================================================================

        /// <summary>True when an iconPath ("ItemIcons/&lt;id&gt;") has its PNG present on disk.</summary>
        private static bool IconPngExists(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return false;
            // iconPath is Resources-relative w/o extension; the file lives under IconAssetFolder.
            string id = iconPath.StartsWith(IconResourcePrefix, StringComparison.OrdinalIgnoreCase)
                ? iconPath.Substring(IconResourcePrefix.Length)
                : iconPath;
            string full = Path.GetFullPath($"{IconAssetFolder}/{id}.png");
            return File.Exists(full);
        }

        private static JObject ReadJsonObject(string assetPath)
        {
            try
            {
                string full = Path.GetFullPath(assetPath);
                if (!File.Exists(full)) return null;
                string text = File.ReadAllText(full);
                if (string.IsNullOrWhiteSpace(text)) return null;
                return JObject.Parse(text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GearIconRenderer] could not read {assetPath}: {ex.Message}");
                return null;
            }
        }

        private static void WriteUtf8NoBom(string assetPath, string contents)
        {
            string full = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // UTF-8 without BOM, LF — the project's canonical JSON convention (NUL/compile gate safe).
            File.WriteAllText(full, contents.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }
}
