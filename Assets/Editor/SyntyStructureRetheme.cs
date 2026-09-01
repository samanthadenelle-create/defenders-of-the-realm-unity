// =============================================================================
// SyntyStructureRetheme — WO-1291. Swap the ART behind each Structures/* address.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-01: FULL Synty re-theme.
//
// ⛔ THE KEY DECISION, AND WHY IT IS THE SAFE ONE.
// We do NOT touch structures-catalog.json. Its 27 `visualPrefabPath` values and — far
// more importantly — its `id` strings are LIVE SAVE KEYS (memory
// structure-role-enum-and-format-normalization); renaming one silently orphans every
// player's building. Instead we keep every `Structures/*` ADDRESS exactly as it is and
// re-point the address at a new prefab. The catalog, the save format, VisualFactory and
// every caller are untouched; only the mesh behind the address changes.
//
// ⚠ THE ADDRESS SET IS THE AUTHORITY, NOT THIS TABLE. Structure_Art holds 38 addresses;
// seven of them are TEXTURES (*_Albedo, *_Tex/*) and are deliberately absent below — a
// texture has no prefab to swap. Anything unmapped is REPORTED, never silently skipped,
// so the gap is visible rather than discovered on a device.
//
// ⚠ THESE ART ASSIGNMENTS ARE MINE, NOT THE OWNER'S. She delegates visual creative
// (memory owner-colorblind-delegate-visual-creative) but has NOT seen these pairings.
// They are a first pass to be looked at and corrected, not a ruling. Change the table.
//
// ⛔ SHIPPING: every run of this re-hashes the Addressable content, so the build CANNOT
// ship without tools\r2-ship.ps1 (CLAUDE.md §16 — content-hashed bundles, a missing push
// fails SILENTLY with placeholder buildings and no on-screen error; it has happened four
// times). Judge by R2_PUSH_OK + R2_PARITY_OK on a FRESH log, never the exit code.
//
// Batchmode: DeNelle.Editor.SyntyStructureRetheme.Run
// Menu:      Defenders/Art/Re-theme Structures to Synty
// Marker:    STRUCTURE_RETHEME_OK / STRUCTURE_RETHEME_FAIL
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class SyntyStructureRetheme
    {
        private const string Synty   = "Assets/Synty/PolygonFantasyKingdom/Prefabs/";
        // ⚠ DERIVED FROM AssetRoots, never re-typed. A second copy of a relocatable root is
        // how a relocation misses a call site, and the miss is SILENT — the builder just
        // quietly loads nothing. AssetRootsRegression enforces this and caught the literal.
        private static readonly string OutDir = AssetRoots.StructureContent + "/Synty";
        private const string StructureLayerName = "Structure";

        /// <summary>address leaf -> Synty prefab, relative to <see cref="Synty"/>.</summary>
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            // ── storefronts / town buildings ───────────────────────────────────
            { "armorer",              "Buildings/Presets/SM_Bld_Preset_Blacksmith_01_Optimized.prefab" },
            { "Forge",                "Buildings/Presets/SM_Bld_Preset_Blacksmith_01_Optimized.prefab" },
            { "ShopAndCrafting",      "Buildings/Presets/SM_Bld_Preset_Tavern_01_Optimized.prefab" },
            { "store",                "Buildings/Presets/SM_Bld_Preset_House_02_A_Optimized.prefab" },
            { "jeweler",              "Buildings/Presets/SM_Bld_Preset_House_03_Optimized.prefab" },
            { "lumbermill",           "Buildings/Presets/SM_Bld_Preset_House_06_Optimized.prefab" },
            { "farm",                 "Buildings/Presets/SM_Bld_Preset_Hut_01_Optimized.prefab" },
            { "barracks",             "Buildings/Presets/SM_Bld_Preset_Stables_01_Optimized.prefab" },
            { "House_Medieval_Medium","Buildings/Presets/SM_Bld_Preset_House_01_A_Optimized.prefab" },
            { "Windmill_Medieval",    "Buildings/Presets/SM_Bld_Preset_House_Windmill_01_Optimized.prefab" },
            { "Watermill_Medieval",   "Buildings/Presets/SM_Bld_Preset_House_Windmill_01_Optimized.prefab" },
            { "PetHouse2",            "Buildings/Presets/SM_Bld_Preset_Hut_02_Optimized.prefab" },

            // ── arcane line: the spire tiers read as a church/tower silhouette ──
            // ⚠ THE KEY HAS A SPACE IN IT: the live address is "Structures/arcane tower".
            // A first pass keyed it "arcane" because the diagnostic that dumped the address
            // list split on whitespace and silently truncated it. Addresses are free text —
            // never assume they are token-shaped.
            { "arcane tower",         "Buildings/Presets/SM_Bld_Preset_Tower_01_Optimized.prefab" },
            { "ArcaneSpire_1",        "Buildings/Presets/SM_Bld_Preset_Tower_01_Optimized.prefab" },
            // ⚠ NOT Church_01_A. STRUCTURE_ORIENTATION_FAIL measured that preset at upright
            // aspect 1.08, below the 1.2 floor every Tower-class row must clear: it is a wide
            // hall, not a tower silhouette. The oracle is explicit that widening the floor
            // would be an OWNER RULING, not a fix — so the ART changes, not the threshold.
            // The L tower keeps the tier escalating and measures ~2.5 aspect.
            { "ArcaneSpire_2",        "Castle/SM_Bld_Castle_Wall_Tower_L_01.prefab" },
            { "ArcaneSpire_3",        "Buildings/Presets/SM_Bld_Preset_Church_01_B_Optimized.prefab" },

            // ── defence: towers escalate S -> M -> L with the tier ─────────────
            { "Tower_Wooden_Watchtower",    "Castle/SM_Bld_Castle_Wall_Tower_S_01.prefab" },
            { "Tower_Wooden_Watchtower_L2", "Castle/SM_Bld_Castle_Wall_Tower_M_01.prefab" },
            { "Tower_Wooden_Watchtower_L3", "Castle/SM_Bld_Castle_Wall_Tower_L_01.prefab" },

            // ── perimeter pieces (same kit as the WO-1290 castle ring) ─────────
            { "Wall_Medieval_Stone",  "Castle/SM_Bld_Castle_Wall_01.prefab" },
            { "Wall_Medieval_Wood",   "Castle/SM_Bld_Castle_Hoarding_Wood_Wall_01.prefab" },
            { "Gate_Medieval_Medium", "Castle/SM_Bld_Castle_Wall_Gate_01.prefab" },

            // ── siege: real art, replacing the polyperfect stand-ins ───────────
            { "Catapult",             "SiegeEngines/SM_Wep_Catapult_01.prefab" },
            { "Ballista",             "SiegeEngines/SM_Wep_Ballista_Mobile_01.prefab" },
            { "Ballista_L1",          "SiegeEngines/SM_Wep_Ballista_Mobile_01.prefab" },
            { "Ballista_L2",          "SiegeEngines/SM_Wep_Ballista_Mounted_01.prefab" },
            { "Ballista_L3",          "SiegeEngines/SM_Wep_Trebuchet_01.prefab" },

            // ── props ──────────────────────────────────────────────────────────
            { "Well",                 "Props/SM_Prop_Well_01.prefab" },
            { "Torche_Wall",          "Props/SM_Prop_Torch_01.prefab" },
            { "HealingCaravan",       "Vehicles/SM_Veh_TraderWagon_01.prefab" },
        };

        /// <summary>
        /// True when the address currently points at something that is NOT a prefab — a
        /// texture, a material. There is nothing to swap for those.
        /// ⚠ DETECTED BY ASSET TYPE, NOT BY A NAME LIST. A hand-written list of texture
        /// addresses had to spell out a base-colour texture suffix, re-typing EnemyArtPaths'
        /// BaseColorSuffix token — the art-ledger oracle rejects a re-typed naming token
        /// because a literal at a call site cannot be re-pointed, traced, or asserted. It
        /// would also go stale the moment a new texture address was added. Asking the
        /// AssetDatabase what the thing IS has neither problem.
        /// </summary>
        private static bool IsNonPrefabAddress(AddressableAssetEntry entry)
        {
            if (entry == null) return true;
            string path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(path)) return true;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
        }

        [MenuItem("Defenders/Art/Re-theme Structures to Synty")]
        public static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) { Debug.LogError("STRUCTURE_RETHEME_FAIL Addressable settings not found."); return; }

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == "Structure_Art");
            if (group == null) { Debug.LogError("STRUCTURE_RETHEME_FAIL group 'Structure_Art' not found."); return; }

            // ⚠ CREATE THE FOLDER THROUGH THE ASSETDATABASE, NOT Directory.CreateDirectory.
            // A bare mkdir puts the folder on disk but leaves the AssetDatabase unaware of it,
            // and PrefabUtility.SaveAsPrefabAsset then THROWS on the first save into it
            // (observed 2026-09-01: the whole run aborted at the first BuildWrapper call).
            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                Directory.CreateDirectory(OutDir);
                AssetDatabase.Refresh();
                if (!AssetDatabase.IsValidFolder(OutDir))
                {
                    Debug.LogError("STRUCTURE_RETHEME_FAIL could not create asset folder " + OutDir);
                    return;
                }
            }
            int layer = LayerMask.NameToLayer(StructureLayerName);
            if (layer < 0)
                Debug.LogWarning("[SyntyRetheme] '" + StructureLayerName + "' layer missing — structures " +
                                 "left on Default; tower line-of-sight and nav carving will degrade.");

            // Snapshot the live addresses BEFORE touching anything: the address set is the
            // authority, this table is not.
            var liveEntries = group.entries.ToList();
            var swapped = new List<string>();
            var missingArt = new List<string>();
            var unmapped = new List<string>();

            foreach (var live in liveEntries)
            {
                string address = live.address;
                if (string.IsNullOrEmpty(address) || !address.StartsWith("Structures/")) continue;
                string leaf = address.Substring("Structures/".Length);

                if (IsNonPrefabAddress(live)) continue;              // texture/material, see method docs
                if (!Map.TryGetValue(leaf, out var rel)) { unmapped.Add(leaf); continue; }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Synty + rel);
                if (source == null) { missingArt.Add(leaf + " -> " + rel); continue; }

                string outPath = OutDir + "/" + leaf.Replace('/', '_') + ".prefab";
                // Guarded per CLAUDE.md §12: one bad source asset is LOGGED and skipped, never
                // allowed to abort the pass and leave the address set half-swapped.
                GameObject built = null;
                try { built = BuildWrapper(source, outPath, layer); }
                catch (System.Exception ex) { missingArt.Add(leaf + " (wrapper threw: " + ex.Message + ")"); continue; }
                if (built == null) { missingArt.Add(leaf + " (wrapper returned null)"); continue; }

                // Move the address onto the new prefab. CreateOrMoveEntry re-points an
                // existing address rather than duplicating it, so the catalog key is stable.
                string guid = AssetDatabase.AssetPathToGUID(outPath);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null) { missingArt.Add(leaf + " (entry move failed)"); continue; }
                entry.address = address;
                swapped.Add(leaf);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SyntyRetheme] swapped {swapped.Count}: {string.Join(", ", swapped)}");
            if (unmapped.Count > 0)
                Debug.LogWarning($"[SyntyRetheme] UNMAPPED {unmapped.Count} address(es) still on the OLD art — " +
                                 $"{string.Join(", ", unmapped)}. Not a silent skip: add them to Map or rule " +
                                 "them out explicitly.");
            if (missingArt.Count > 0)
                Debug.LogWarning($"[SyntyRetheme] ART MISSING {missingArt.Count}: {string.Join("; ", missingArt)}. " +
                                 "Is the Synty pack imported? It is gitignored (see .gitignore).");

            if (swapped.Count == 0) { Debug.LogError("STRUCTURE_RETHEME_FAIL nothing was swapped."); return; }
            Debug.Log($"STRUCTURE_RETHEME_OK swapped={swapped.Count} unmapped={unmapped.Count} " +
                      $"missing={missingArt.Count} -> {OutDir}");
        }

        /// <summary>Wrap a Synty source prefab in a tracked prefab carrying a fitted BoxCollider
        /// on the Structure layer. The wrapper exists so the gitignored pack is referenced from
        /// exactly one tracked place per address, the way the polyperfect walls always were.</summary>
        private static GameObject BuildWrapper(GameObject source, string outPath, int layer)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (root == null) root = Object.Instantiate(source);
            if (root == null) return null;
            try
            {
                root.name = Path.GetFileNameWithoutExtension(outPath);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                // BoxCollider fitted to the MEASURED bounds, not a MeshCollider: the Structure
                // layer is what every tower/hero line-of-sight linecast tests against, and a box
                // is both cheaper and stable under the nav carve.
                var rends = root.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    // ⚠ NO ?? HERE. GetComponent returns Unity's FAKE-NULL, which is not C# null,
                    // so `GetComponent<T>() ?? AddComponent<T>()` hands back the fake-null and the
                    // very next line throws "There is no 'BoxCollider' attached ... but a script is
                    // trying to access it". That one operator failed 27 of 29 structures on the
                    // 2026-09-01 first run. Explicit == null is the only correct test.
                    var box = root.GetComponent<BoxCollider>();
                    if (box == null) box = root.AddComponent<BoxCollider>();
                    box.center = b.center - root.transform.position;
                    box.size   = b.size;
                }
                if (layer >= 0) SetLayerRecursively(root, layer);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, outPath);
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
