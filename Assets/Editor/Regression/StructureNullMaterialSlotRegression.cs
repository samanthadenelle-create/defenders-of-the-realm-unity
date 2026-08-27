// =============================================================================
// StructureNullMaterialSlotRegression [structure-null-slot]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
// Markers: STRUCTURE_NULL_SLOT_OK / STRUCTURE_NULL_SLOT_FAIL.
// Registered ONCE in DataRegression.RunAll. NEVER throws.
//
// THE INCIDENT THIS GUARDS (WO-1251, owner F8 seq 3618/3619, 2026-08-27):
//   [Flow:StructureAssets] dep MISS on 'Structures/CrystalMine':
//   renderer 'CrystalMine' has a NULL material slot - that renderer draws engine-default
// A renderer with no material draws the engine default (colourless / magenta). That
// is the whole bug. MagentaGuard must NOT hide the renderer to silence it.
//
// WHAT THIS ASSERTS, for every catalogued structure visual AND every GameObject
// registered in the Structure_Art Addressables group:
//   * MeshRenderer / SkinnedMeshRenderer that owns a mesh must have a non-null
//     material in every slot, and at least as many slots as the mesh has submeshes.
//   * General form -- not a CrystalMine special case. The proving offender was
//     CrystalMine slot 0; the oracle names whichever renderer is empty.
//
// HOLLOW-PASS GUARD (WO-1138): if the catalog cannot be read AND Addressables
// cannot enumerate Structure_Art, OR if zero GameObjects actually load, the suite
// stands down via RegressionOutcome.Skip and ASSERTED NOTHING. It never returns
// quiet green on an empty set. Individual keys that do not resolve (gitignored
// pack, missing GUID) are PartialSkip -- the rest still assert.
//
// POSITIVE CONTROL (prove it can go red): null any MeshRenderer.sharedMaterials
// slot on a loaded structure prefab/FBX, re-run -- the suite MUST fail naming
// that asset / renderer / slot. The CrystalMine import with externalObjects: {}
// and no gem_mine_3d_model_basecolor.mat WAS that red (seq 3618).
//
// Deterministic, editor-only asset reads. No scene, no PlayMode.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class StructureNullMaterialSlotRegression
    {
        private const string FlowSys    = "StructureNullSlot";
        private const string MarkerOk   = "STRUCTURE_NULL_SLOT_OK";
        private const string MarkerFail = "STRUCTURE_NULL_SLOT_FAIL";
        private const string Tag        = "structure-null-slot";
        private const string CatalogRel = "Data/Canonical/structures-catalog.json";
        private const string StructureGroupName = "Structure_Art";

        [Serializable]
        private sealed class StructuresCatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static void RunStandalone()
        {
            string reason;
            bool pass = Run(out reason);
            Debug.Log("[" + Tag + "] standalone result: " + (pass ? "PASS" : "FAIL") + " - " + reason);
        }

        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = Tag + ": oracle threw " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError(MarkerFail + " - " + reason);
                return false;
            }
        }

        private static bool RunCore(out string reason)
        {
            using var _scope = FlowTrace.Enter(FlowSys, "StructureNullMaterialSlot.RunCore");

            var failures = new List<string>();
            var partials = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int inspected = 0;
            int slotsChecked = 0;
            int catalogKeys = 0;

            var catalogKeysSet = CollectCatalogKeys(partials, out bool catalogReadable);
            catalogKeys = catalogKeysSet.Count;

            // Addressables Structure_Art is the on-disk art set the device actually loads.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var group = settings.FindGroup(StructureGroupName);
                if (group == null)
                {
                    partials.Add(RegressionOutcome.PartialSkip("Structure_Art group",
                        "AddressableAssetSettings has no group named '" + StructureGroupName + "'"));
                }
                else if (group.entries != null)
                {
                    foreach (var entry in group.entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.guid)) continue;
                        string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        InspectPath(path, entry.address, seenPaths, failures, ref inspected, ref slotsChecked);
                    }
                }
            }
            else
            {
                partials.Add(RegressionOutcome.PartialSkip("AddressableAssetSettings",
                    "no AddressableAssetSettings object -- Structure_Art could not be enumerated"));
            }

            // Catalog keys that were not already covered by a Structure_Art GUID still
            // have to load -- they are what VisualFactory.Skin actually asks for.
            foreach (var key in catalogKeysSet)
            {
                if (string.IsNullOrEmpty(key)) continue;
                var go = StructureAssetLoader.LoadStructurePrefab(key);
                if (go == null)
                {
                    partials.Add(RegressionOutcome.PartialSkip(key,
                        "catalog visual did not resolve (gitignored pack / missing address is not a null-slot)"));
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path) && !seenPaths.Add(path)) continue;
                InspectGameObject(go, key, failures, ref inspected, ref slotsChecked);
            }

            if (!catalogReadable && settings == null)
            {
                FlowTrace.Warn(FlowSys, "could not enumerate the structure set");
                return RegressionOutcome.Skip(out reason, Tag,
                    "structures-catalog.json unreadable AND AddressableAssetSettings missing -- " +
                    "the structure renderer set could not be enumerated");
            }

            if (inspected == 0)
            {
                FlowTrace.Warn(FlowSys, "enumerated 0 GameObjects -- standing down");
                return RegressionOutcome.Skip(out reason, Tag,
                    "0 structure GameObjects loaded (catalog keys=" + catalogKeys +
                    "). A pass on an empty set would be a hollow green");
            }

            if (failures.Count > 0)
            {
                FlowTrace.Fail(FlowSys, "offenders=" + failures.Count + " across " + inspected + " asset(s)");
                reason = Tag + " FAIL (" + failures.Count + " finding(s); " + inspected +
                         " asset(s), " + slotsChecked + " slot(s)): " +
                         string.Join(" | ", failures.ToArray());
                Debug.LogError(MarkerFail + " - " + reason);
                return false;
            }

            string extra = partials.Count > 0 ? " " + string.Join(" ", partials.ToArray()) : "";
            FlowTrace.Step(FlowSys, "clean: assets=" + inspected + " slots=" + slotsChecked +
                                    " catalogKeys=" + catalogKeys + " partials=" + partials.Count);
            reason = Tag + " OK - " + inspected + " structure asset(s), " + slotsChecked +
                     " MeshRenderer/SkinnedMeshRenderer slot(s), no null material slot." + extra;
            Debug.Log(MarkerOk + " - " + reason);
            return true;
        }

        private static HashSet<string> CollectCatalogKeys(List<string> partials, out bool catalogReadable)
        {
            catalogReadable = false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string json = CanonicalJson.Read(CatalogRel);
            if (string.IsNullOrEmpty(json))
            {
                partials.Add(RegressionOutcome.PartialSkip("structures-catalog",
                    CatalogRel + " unreadable -- catalog keys not collected"));
                return keys;
            }

            StructuresCatalogFile file = null;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresCatalogFile>(json, settings);
            }
            catch (Exception ex)
            {
                partials.Add(RegressionOutcome.PartialSkip("structures-catalog",
                    "parse failed: " + ex.Message));
                return keys;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                partials.Add(RegressionOutcome.PartialSkip("structures-catalog",
                    "deserialized to 0 CatalogEntry objects"));
                return keys;
            }

            catalogReadable = true;
            for (int i = 0; i < file.Entries.Count; i++)
            {
                var e = file.Entries[i];
                if (e == null) continue;
                if (!string.IsNullOrEmpty(e.visualPrefabPath)) keys.Add(e.visualPrefabPath);
                if (e.repo == null || e.repo.upgradeVisualPath == null) continue;
                for (int u = 0; u < e.repo.upgradeVisualPath.Length; u++)
                {
                    string p = e.repo.upgradeVisualPath[u];
                    if (!string.IsNullOrEmpty(p)) keys.Add(p);
                }
            }
            return keys;
        }

        private static void InspectPath(string path, string label, HashSet<string> seenPaths,
            List<string> failures, ref int inspected, ref int slotsChecked)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!IsMeshAssetPath(path)) return;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                failures.Add("[structure-null-slot] missing GameObject at '" + path +
                             "' (label='" + label + "') — cannot inspect material slots");
                return;
            }
            if (!seenPaths.Add(path)) return;
            InspectGameObject(go, string.IsNullOrEmpty(label) ? path : label,
                failures, ref inspected, ref slotsChecked);
        }

        private static void InspectGameObject(GameObject go, string label,
            List<string> failures, ref int inspected, ref int slotsChecked)
        {
            if (go == null)
            {
                failures.Add("[structure-null-slot] InspectGameObject received a null GO ('" + label + "')");
                return;
            }
            inspected++;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                // Loaded, zero renderers: 0 slots is a recorded outcome (already in inspected),
                // not a skip. Fall through; the loop is a no-op.
                renderers = Array.Empty<Renderer>();
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                var mesh = MeshOf(renderer);
                if (mesh == null) continue;

                int subMeshes = Mathf.Max(1, mesh.subMeshCount);
                var mats = renderer.sharedMaterials;
                int slotCount = mats == null ? 0 : mats.Length;
                slotsChecked += Mathf.Max(slotCount, subMeshes);

                if (slotCount < subMeshes)
                {
                    failures.Add("'" + label + "' renderer '" + renderer.name +
                                 "' has " + slotCount + " material slot(s) for " + subMeshes +
                                 " submesh(es) -- trailing submeshes draw engine-default");
                    continue;
                }

                for (int i = 0; i < slotCount; i++)
                {
                    if (mats[i] != null) continue;
                    failures.Add("'" + label + "' renderer '" + renderer.name +
                                 "' slot " + i + " is NULL -- that renderer draws engine-default");
                }
            }
        }

        private static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        /// <summary>
        /// Structure_Art also holds albedo textures. Those are not MeshRenderers.
        /// Only mesh hosts can have a null material slot.
        /// </summary>
        private static bool IsMeshAssetPath(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.Equals(".fbx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".obj", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".blend", StringComparison.OrdinalIgnoreCase);
        }
    }
}
