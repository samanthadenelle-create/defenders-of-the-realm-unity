// =============================================================================
// HeroAddressablesGrouper — marks the per-hero body prefab + animator controller
// Addressable so heroes can be pulled per-selection instead of all shipping in
// Resources (WO-545 Tier-1, docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md).
// -----------------------------------------------------------------------------
// Today Assets/Resources/Heroes ships ~138 MB in EVERY build because Resources is
// always included. This tool is the GROUPING half of the seam (HeroAssetLoader is
// the CODE half): it scans Assets/Resources/Heroes for each top-level <slug>.fbx +
// matching <slug>.controller and files them under a per-hero Addressables group at
// address "Heroes/<slug>" (the SAME address HeroAssetLoader queries — the asset TYPE
// disambiguates the prefab vs controller locations sharing that address).
//
// It ONLY marks assets Addressable. It does NOT move or delete anything out of
// Resources — that verified migration (so the bytes stop shipping in Resources) is a
// LATER step the lead runs with a build. Until then HeroAssetLoader's Resources
// fallback keeps V1 working whether or not this tool has been run.
//
// IDEMPOTENT: an entry already at its target address is skipped (no churn).
// GUARDED: if the Addressables settings asset is null it LogWarnings and returns.
//
// Run: Defenders > Build > Group Heroes Addressable
//   or headless -executeMethod DeNelle.Editor.HeroAddressablesGrouper.GroupHeroes
// EDITOR-ONLY. Mutates the Addressables settings asset; does NOT run gameplay,
// does NOT commit, does NOT touch any data JSON.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Files each Resources/Heroes/&lt;slug&gt; body prefab + controller into a per-hero
    /// Addressables group at address "Heroes/&lt;slug&gt;". Mark-only (never moves/deletes files);
    /// idempotent + guarded for the no-Addressables-settings case.</summary>
    public static class HeroAddressablesGrouper
    {
        // The flat folder holding the per-hero <slug>.fbx + <slug>.controller pairs.
        internal const string HeroesRoot = "Assets/Resources/Heroes";

        // Address prefix — MUST match DeNelle.Core.HeroAssetLoader.HeroAddrPrefix.
        internal const string HeroAddrPrefix = "Heroes/";

        // Per-hero group name prefix (one group per hero so the lead can flip an individual
        // hero's group remote/local for WebGL on-demand delivery without touching the others).
        internal const string GroupPrefix = "Hero_";

        [MenuItem("Defenders/Build/Group Heroes Addressable")]
        public static void GroupHeroes()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[HeroAddressablesGrouper] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null) — Addressables not " +
                    "initialised. Nothing grouped. HeroAssetLoader keeps using the Resources fallback.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(HeroesRoot))
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] heroes root '{HeroesRoot}' not found — nothing grouped.");
                return;
            }

            int prefabs = 0, controllers = 0, skipped = 0, heroes = 0;

            foreach (string slug in EnumerateHeroSlugs())
            {
                string groupName = GroupPrefix + slug;
                AddressableAssetGroup group = settings.FindGroup(groupName) ?? CreateHeroGroup(settings, groupName);
                if (group == null)
                {
                    Debug.LogWarning($"[HeroAddressablesGrouper] could not create group '{groupName}' — skipping {slug}.");
                    continue;
                }

                string address = HeroAddrPrefix + slug;
                heroes++;

                // Body prefab (<slug>.fbx imports as a GameObject).
                string fbxGuid = AssetDatabase.AssetPathToGUID($"{HeroesRoot}/{slug}.fbx");
                if (!string.IsNullOrEmpty(fbxGuid))
                {
                    if (MarkEntry(settings, group, fbxGuid, address)) prefabs++;
                    else skipped++;
                }
                else
                {
                    Debug.LogWarning($"[HeroAddressablesGrouper] no body prefab '{HeroesRoot}/{slug}.fbx' for '{slug}'.");
                }

                // Animator controller (<slug>.controller) — SAME address; type disambiguates.
                string ctrlGuid = AssetDatabase.AssetPathToGUID($"{HeroesRoot}/{slug}.controller");
                if (!string.IsNullOrEmpty(ctrlGuid))
                {
                    if (MarkEntry(settings, group, ctrlGuid, address)) controllers++;
                    else skipped++;
                }
                else
                {
                    Debug.LogWarning($"[HeroAddressablesGrouper] no controller '{HeroesRoot}/{slug}.controller' for '{slug}'.");
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HeroAddressablesGrouper] Grouped {heroes} hero(es): marked {prefabs} prefab + " +
                      $"{controllers} controller entr(ies) Addressable ({skipped} already addressed/skipped). " +
                      "Files NOT moved out of Resources (later migration step).");
        }

        /// <summary>Yields the hero slug for every top-level &lt;slug&gt;.fbx directly under HeroesRoot
        /// (e.g. Knight, Ranger, Mage, Cleric). Skips nested folders + non-hero prefabs (SC_*).</summary>
        internal static IEnumerable<string> EnumerateHeroSlugs()
        {
            // Only the FLAT folder — FindAssets recurses, so filter to direct children of HeroesRoot.
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { HeroesRoot });
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                // Direct child of HeroesRoot only (e.g. "Assets/Resources/Heroes/Knight.fbx").
                if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), HeroesRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                string slug = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(slug)) continue;
                if (seen.Add(slug)) yield return slug;
            }
        }

        /// <summary>Move the asset into the hero group and set its address. True when a change was
        /// made; false when already at this exact address (idempotent). Mirrors BlinkAddressableMarker.</summary>
        private static bool MarkEntry(AddressableAssetSettings settings, AddressableAssetGroup group,
                                      string guid, string address)
        {
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, postEvent: false);
            if (entry == null) return false;

            if (string.Equals(entry.address, address, StringComparison.Ordinal))
                return false; // already addressed — no churn

            entry.SetAddress(address, postEvent: false);
            return true;
        }

        /// <summary>Create a per-hero group with the standard bundled/content-update schemas
        /// (mirrors the Default Local Group + BlinkAddressableMarker's 'Gear' group).</summary>
        private static AddressableAssetGroup CreateHeroGroup(AddressableAssetSettings settings, string groupName)
        {
            return settings.CreateGroup(
                groupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }
    }
}
