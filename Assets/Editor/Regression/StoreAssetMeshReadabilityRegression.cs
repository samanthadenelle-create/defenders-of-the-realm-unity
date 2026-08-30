// =============================================================================
// StoreAssetMeshReadabilityRegression [store-mesh-readable]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (UnityEditor + UnityEngine only — no gameplay
// types; this suite reads the ASSET DATABASE and the importer .meta files, nothing
// that needs Play mode).
//
// WO-1284 (owner, 2026-08-30: "we need to add a test for all store assets"),
// generalising the single-asset guard AttachmentOffsetRegression.Case11.
//
// -----------------------------------------------------------------------------
// WHY THIS DEFECT CLASS IS INVISIBLE IN THE EDITOR — the whole reason the suite exists
// -----------------------------------------------------------------------------
// An imported model defaults to `isReadable: 0` (Read/Write DISABLED). **The Editor
// keeps mesh data CPU-side REGARDLESS of that flag**, so `mesh.vertices` reads fine
// in Play mode and every held prop looks correct on screen. In a PLAYER BUILD the
// same mesh returns ZERO vertices.
//
// The orientation code measures held props to decide how they sit in the hand:
//   * EquipmentController.CollectWidthProfile (EquipmentController.cs:1957/1977)
//     bins mesh vertices along prop-local Y to find the hilt.
//   * WeaponOrientHelper / WeaponBoundsOrient read `sharedMesh.vertices` for the
//     long-axis and taper signals (SheatheSign), and ShieldHandleSide reads them to
//     decide which of a shield plate's two faces points outward.
// Every one of those call sites is correctly guarded with `!isReadable -> continue`,
// so with the flag off NOTHING THROWS AND NOTHING ERRORS. The code *degrades*, in its
// own words (device trace 2026-08-30, PROD-019):
//
//   "ShieldHandleSide 'EquipmentProp_OffHand_Mesh': only 0 readable vertices — 1 of 1
//    mesh(es) have Read/Write DISABLED ... so NO flip is applied ... it may be worn
//    strap-outward."
//   "SheatheSign 'EquipmentProp_Weapon': ... taper unavailable (0 readable vertices —
//    Read/Write is OFF on this prop, which is the SHIPPED state of the live weapons)"
//
// Read that last clause again: **"the SHIPPED state of the live weapons."** The sword
// is guessing its sheathe sign on a coin flip today and happens to land right. This is
// not one bad asset — it is the DEFAULT state of the gear catalogue, and it is green
// in every editor-side proof we own. PROD-019 cost an evening and three commits
// (30a3e7a1e, ac40ab578, 74d9e6546) dialling a seat that was never wrong.
//
// -----------------------------------------------------------------------------
// SCOPE — held props only, deliberately (WO-1284 "what NOT to do")
// -----------------------------------------------------------------------------
// Read/Write keeps a permanent CPU copy of the mesh, so enabling it project-wide
// costs memory for nothing. This suite covers exactly the meshes the orientation
// code actually MEASURES:
//   (a) the Gear Addressable group's HELD-PROP entries — address prefix
//       "gear/weapon/" — resolved GUID -> asset path -> prefab -> every
//       MeshFilter/SkinnedMeshRenderer mesh -> that mesh's SOURCE MODEL .meta.
//   (b) Assets/Resources/Heroes/Props/Weapons/ — the Resources branch
//       EquipmentController.LoadWeaponMesh uses (sword_A, staff_A, shield_A, ...).
// "gear/armor/" and "hero/base/" are OUT of scope on purpose: armor is skinned onto
// the body and is never orientation-measured, and HeroBodySwapper.PlantFeetOnGround
// measures it through SkinnedMeshRenderer.BakeMesh, which does NOT require Read/Write.
//
// THE LIST IS BUILT FROM THE AUTHORITIES, NEVER HAND-TYPED. A hand-typed asset list
// goes stale the first time someone adds gear — the same duplicated-state failure
// CLAUDE.md sec 2 (the stale WO number block) and sec 5 (the retired dependency
// table) both record. Add gear to the group and this suite covers it automatically.
//
// -----------------------------------------------------------------------------
// SKIPS ARE NOT PASSES
// -----------------------------------------------------------------------------
// Assets/Blink/, Assets/Supercyan/ and Assets/polyperfect/ are gitignored paid packs.
// A clone that has not re-imported them legitimately has no file, so an ABSENT asset
// or .meta is a counted SKIP with a Debug.LogWarning — never a failure, and never a
// silent pass: the summary line always prints checked / passed / skipped so a run on
// a bare clone cannot be mistaken for a clean bill of health.
//
// Only a PRESENT .meta with the flag off is an offender, and EVERY offender is
// reported in one run (one warning line each, plus a capped roll-up in the failure
// reason) — stopping at the first would hide the catalogue-wide shape of the defect.
//
// Markers: STORE_ASSET_MESH_OK / STORE_ASSET_MESH_FAIL.
// Standalone: run-unity-method
//   DeNelle.Editor.Regression.StoreAssetMeshReadabilityRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class StoreAssetMeshReadabilityRegression
    {
        /// <summary>The Addressable group that publishes every equippable gear prop.
        /// THE authority for "which store assets ship" — never a hand-typed list.</summary>
        private const string GearGroupAsset =
            "Assets/AddressableAssetsData/AssetGroups/Gear.asset";

        /// <summary>The Resources branch EquipmentController.LoadWeaponMesh loads from
        /// (see EquipmentController.WeaponPropResourceDir = "Heroes/Props/Weapons/").</summary>
        private const string WeaponResourcesDir = "Assets/Resources/Heroes/Props/Weapons";

        /// <summary>Held props only. "gear/armor/" is skinned onto the body and is never
        /// orientation-measured; a CPU copy there costs memory and proves nothing.</summary>
        private const string HeldPropAddressPrefix = "gear/weapon/";

        /// <summary>How many offenders are named inline in the failure reason. The FULL
        /// list always goes to the log, one Debug.LogWarning per offender.</summary>
        private const int MaxNamedInReason = 8;

        private static readonly string[] ModelExtensions =
            { ".fbx", ".obj", ".dae", ".blend", ".gltf", ".glb", ".max", ".ma", ".mb" };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("STORE_ASSET_MESH_OK - " + reason);
            else Debug.LogError("STORE_ASSET_MESH_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string summary = "";
            try
            {
                summary = Sweep(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "STORE ASSET MESH READABILITY OK - " + summary;
                return true;
            }
            reason = "store-mesh-readable FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures) + " || " + summary;
            return false;
        }

        // =====================================================================
        //  The sweep
        // =====================================================================

        /// <summary>
        /// One model path -> the addresses/prefabs that consume it. Deduped, because a
        /// single source FBX can back many Addressable entries and we want ONE verdict
        /// per file with all of its consumers named.
        /// </summary>
        private sealed class ModelUse
        {
            public readonly HashSet<string> Consumers = new HashSet<string>();
        }

        private static string Sweep(List<string> failures)
        {
            var models = new SortedDictionary<string, ModelUse>(StringComparer.Ordinal);
            var skips = new List<string>();

            CollectFromGearGroup(models, skips, failures);
            CollectFromWeaponResources(models, skips);

            int checkedCount = 0, passed = 0;
            var offenders = new List<string>();
            var malformed = new List<string>();

            foreach (var kv in models)
            {
                string modelPath = kv.Key;
                string metaPath = modelPath + ".meta";

                if (!File.Exists(metaPath))
                {
                    // Gitignored paid pack, not re-imported on this clone. WARN + count.
                    skips.Add(modelPath + " (no .meta on disk)");
                    Debug.LogWarning("[store-mesh-readable] SKIPPED - " + metaPath +
                        " not present. The art packs (Blink / Supercyan / polyperfect) are " +
                        "gitignored; re-import them before trusting this suite. This is a " +
                        "WARNING, not a pass.");
                    continue;
                }

                checkedCount++;
                string meta;
                try { meta = File.ReadAllText(metaPath); }
                catch (Exception ex)
                {
                    malformed.Add(modelPath + " (unreadable .meta: " + ex.GetType().Name + ")");
                    continue;
                }

                Match m = Regex.Match(meta, @"^\s*isReadable:\s*(\d+)\s*$", RegexOptions.Multiline);
                if (!m.Success)
                {
                    malformed.Add(modelPath + " (no 'isReadable:' key)");
                    continue;
                }

                if (m.Groups[1].Value == "1") { passed++; continue; }

                string consumers = Describe(kv.Value);
                offenders.Add(modelPath + " isReadable=" + m.Groups[1].Value + " <- " + consumers);
                Debug.LogWarning("[store-mesh-readable] OFFENDER - " + modelPath +
                    " has isReadable=" + m.Groups[1].Value + " (MUST be 1). Consumed by: " +
                    consumers + ". In a player build this mesh yields ZERO vertices, the " +
                    "orientation code silently skips its measurement, and the prop is seated " +
                    "on a guess (PROD-019). Enable Read/Write on the model importer; do NOT " +
                    "add a face/orientation heuristic to compensate.");
            }

            if (malformed.Count > 0)
            {
                failures.Add("[importer-format] " + malformed.Count + " model .meta file(s) do " +
                    "not expose a readable 'isReadable:' key - this guard is no longer reading " +
                    "the flag it claims to read. FIX THE GUARD, do not delete it. First: " +
                    string.Join(", ", malformed.Take(MaxNamedInReason)));
            }

            if (offenders.Count > 0)
            {
                failures.Add("[read-write-off] " + offenders.Count + " of " + checkedCount +
                    " measured store meshes import with Read/Write DISABLED. They look correct " +
                    "in the Editor and return ZERO vertices in a player build, so the held-prop " +
                    "orientation degrades to a coin flip with no error on screen (PROD-019). " +
                    "Every offender is logged individually this run; first " +
                    Math.Min(MaxNamedInReason, offenders.Count) + ": " +
                    string.Join(" ;; ", offenders.Take(MaxNamedInReason)));
            }

            if (skips.Count > 0)
            {
                Debug.LogWarning("[store-mesh-readable] " + skips.Count + " asset(s) SKIPPED " +
                    "(absent on this clone - gitignored packs). A SKIP IS NOT A PASS. First: " +
                    string.Join(", ", skips.Take(12)));
            }

            return "checked=" + checkedCount + " passed=" + passed +
                   " offenders=" + offenders.Count + " malformed=" + malformed.Count +
                   " skipped=" + skips.Count +
                   " (sources: " + GearGroupAsset + " '" + HeldPropAddressPrefix + "*' + " +
                   WeaponResourcesDir + ")";
        }

        // =====================================================================
        //  Authority (a): the Gear Addressable group
        // =====================================================================

        private static void CollectFromGearGroup(
            SortedDictionary<string, ModelUse> models, List<string> skips, List<string> failures)
        {
            if (!File.Exists(GearGroupAsset))
            {
                skips.Add(GearGroupAsset + " (group asset absent)");
                Debug.LogWarning("[store-mesh-readable] SKIPPED - " + GearGroupAsset +
                    " not present; the Addressable half of this sweep covered NOTHING. " +
                    "This is a WARNING, not a pass.");
                return;
            }

            string yaml = File.ReadAllText(GearGroupAsset);
            var entries = Regex.Matches(
                yaml, @"-\s+m_GUID:\s*([0-9a-fA-F]{32})\s*[\r\n]+\s*m_Address:\s*(\S+)");

            if (entries.Count == 0)
            {
                failures.Add("[gear-group] parsed 0 entries out of " + GearGroupAsset +
                    " - the group serialization format changed and this sweep is now blind. " +
                    "Fix the parser, do not delete the suite.");
                return;
            }

            foreach (Match e in entries)
            {
                string guid = e.Groups[1].Value;
                string address = e.Groups[2].Value;
                if (!address.StartsWith(HeldPropAddressPrefix, StringComparison.Ordinal)) continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                {
                    skips.Add(address + " (guid " + guid + " unresolved / file absent)");
                    continue;
                }

                if (IsModel(assetPath)) { Note(models, assetPath, address); continue; }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    skips.Add(address + " (" + assetPath + " did not load as a GameObject)");
                    continue;
                }

                string consumer = address + " [" + assetPath + "]";
                int found = 0;

                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (NoteMeshSource(models, mf.sharedMesh, consumer, skips)) found++;
                }
                foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    if (NoteMeshSource(models, smr.sharedMesh, consumer, skips)) found++;
                }

                if (found == 0) skips.Add(consumer + " (no MeshFilter/SkinnedMeshRenderer mesh)");
            }
        }

        /// <summary>Mesh -> its SOURCE MODEL file. Returns false (and records a skip) for a
        /// mesh with no model behind it: a built-in primitive, a standalone .asset mesh, or
        /// a runtime-generated mesh has no importer flag to assert.</summary>
        private static bool NoteMeshSource(
            SortedDictionary<string, ModelUse> models, Mesh mesh, string consumer, List<string> skips)
        {
            string src = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(src) || !IsModel(src))
            {
                skips.Add(consumer + " mesh '" + mesh.name + "' has no source model (" +
                          (string.IsNullOrEmpty(src) ? "<none>" : src) + ")");
                return false;
            }
            Note(models, src, consumer);
            return true;
        }

        // =====================================================================
        //  Authority (b): the Resources weapon props
        // =====================================================================

        private static void CollectFromWeaponResources(
            SortedDictionary<string, ModelUse> models, List<string> skips)
        {
            if (!Directory.Exists(WeaponResourcesDir))
            {
                skips.Add(WeaponResourcesDir + " (directory absent)");
                Debug.LogWarning("[store-mesh-readable] SKIPPED - " + WeaponResourcesDir +
                    " not present; the Resources half of this sweep covered NOTHING. " +
                    "This is a WARNING, not a pass.");
                return;
            }

            foreach (string file in Directory.GetFiles(WeaponResourcesDir))
            {
                string path = file.Replace('\\', '/');
                if (!IsModel(path)) continue;
                Note(models, path,
                     "Resources: LoadWeaponMesh('" + Path.GetFileNameWithoutExtension(path) + "')");
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void Note(
            SortedDictionary<string, ModelUse> models, string modelPath, string consumer)
        {
            modelPath = modelPath.Replace('\\', '/');
            if (!models.TryGetValue(modelPath, out ModelUse use))
            {
                use = new ModelUse();
                models[modelPath] = use;
            }
            use.Consumers.Add(consumer);
        }

        private static bool IsModel(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.ToLowerInvariant();
            for (int i = 0; i < ModelExtensions.Length; i++)
                if (ModelExtensions[i] == ext) return true;
            return false;
        }

        private static string Describe(ModelUse use)
        {
            var ordered = use.Consumers.OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (ordered.Count <= 3) return string.Join(", ", ordered);
            return string.Join(", ", ordered.Take(3)) + " (+" + (ordered.Count - 3) + " more)";
        }
    }
}
