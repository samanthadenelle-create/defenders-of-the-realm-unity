// =============================================================================
// BlinkAddressableMarker — marks the gitignored Blink RPG gear bundle (weapons +
// full-body armor outfit sets) as Addressable so the gear catalog can reference
// them by a stable address and a future runtime equip-resolve can load on demand.
// -----------------------------------------------------------------------------
// docs/BLINK_NOTES.md ("the Addressables gear enabler") + docs/ITEM_MODEL.md §5.
// The Blink packs live OUTSIDE Assets/Resources/ (and are gitignored), so they are
// NOT Resources.Load-able. The right path is Addressables (NOT a Resources mirror,
// which would bloat the WebGL build, WO-191/408). This utility:
//   1. ensures a single "Gear" Addressables group exists,
//   2. for every Blink weapon prefab + canonical (Male) armor-set prefab,
//      CreateOrMoveEntry + SetAddress to a stable, scheme'd address.
//
// ADDRESS SCHEME (shared with BlinkGearSource so the catalog prefabPath = address):
//   weapon : "gear/weapon/<prefabFileName>"   e.g. gear/weapon/Sword1h_01
//   armor  : "gear/armor/<setName>_<gender>"   e.g. gear/armor/Centurion_Male
//            (canonical = the HumanMale variant only — one entry per SET)
//
// IDEMPOTENT: an entry already at its target address is skipped (no churn). GUARDED:
// if the Addressables Settings asset is null (e.g. a fresh clone where the gitignored
// Blink pack is absent, or Addressables not yet initialised) it LogWarnings and
// returns — never crashes the editor.
//
// Run: Defenders > Catalog > Mark Blink Gear Addressable
//   or headless -executeMethod DeNelle.Editor.Catalog.BlinkAddressableMarker.MarkBlinkGear
// EDITOR-ONLY. Mutates the Addressables settings asset; does NOT run gameplay,
// does NOT commit, does NOT touch the gear JSON (that is the generator's job).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor.Catalog
{
    /// <summary>Marks the Blink gear bundle Addressable under a single "Gear" group with a
    /// stable address scheme (gear/weapon/&lt;name&gt;, gear/armor/&lt;set&gt;_&lt;gender&gt;).
    /// Idempotent + guarded for the gitignored-absent case.</summary>
    public static class BlinkAddressableMarker
    {
        // The Blink pack roots (verified on-disk 2026-06-18). Weapons are nested one
        // level deep in category subfolders; armor is a flat folder of Male/Female pairs.
        internal const string WeaponsRoot =
            "Assets/Blink/Art/Weapons/LowPoly/MegaWeaponPack1/_Prefabs_MWP1";
        internal const string ArmorRoot =
            "Assets/Blink/Art/Characters/LowPoly/Humans_LowPoly/ArmorPacks/Prefabs";

        // The single Addressables group every Blink gear asset is filed under.
        internal const string GearGroup = "Gear";

        // Address scheme prefixes (shared contract with BlinkGearSource).
        internal const string WeaponAddrPrefix = "gear/weapon/";
        internal const string ArmorAddrPrefix  = "gear/armor/";

        [MenuItem("Defenders/Catalog/Mark Blink Gear Addressable")]
        public static void MarkBlinkGear()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[BlinkAddressableMarker] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null). The gitignored " +
                    "Blink pack may be absent or Addressables is not initialised — nothing marked.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(GearGroup) ?? CreateGearGroup(settings);
            if (group == null)
            {
                Debug.LogWarning("[BlinkAddressableMarker] could not find or create the 'Gear' " +
                                 "Addressables group — nothing marked.");
                return;
            }

            int weapons = 0, armor = 0, skipped = 0;

            // ── Weapons: every prefab under the category subfolders ──
            foreach (var (guid, address) in EnumerateWeaponPrefabs())
            {
                if (MarkEntry(settings, group, guid, address)) weapons++;
                else skipped++;
            }

            // ── Armor: ONE entry per SET (canonical = the HumanMale variant) ──
            foreach (var (guid, address) in EnumerateArmorSetPrefabs())
            {
                if (MarkEntry(settings, group, guid, address)) armor++;
                else skipped++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification,
                              null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BlinkAddressableMarker] Marked {weapons} weapon + {armor} armor-set " +
                      $"prefabs Addressable in group '{GearGroup}' ({skipped} already addressed/skipped).");
        }

        // =====================================================================
        // Enumeration (READ-ONLY over AssetDatabase) — shared with BlinkGearSource
        // via the same scheme so prefabPath == the Addressable address.
        // =====================================================================

        /// <summary>Yields (guid, address) for every Blink weapon prefab. Address =
        /// "gear/weapon/&lt;fileNameNoExt&gt;" (e.g. gear/weapon/Sword1h_01).</summary>
        internal static IEnumerable<(string guid, string address)> EnumerateWeaponPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(WeaponsRoot))
            {
                Debug.LogWarning($"[BlinkAddressableMarker] weapon root '{WeaponsRoot}' not found " +
                                 "(gitignored Blink pack absent?) — no weapons enumerated.");
                yield break;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponsRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                string nameNoExt = Path.GetFileNameWithoutExtension(path);
                if (nameNoExt.StartsWith("_", StringComparison.Ordinal)) continue; // demo/backup
                yield return (guid, WeaponAddrPrefix + nameNoExt);
            }
        }

        /// <summary>Yields (guid, address) for the canonical (HumanMale) variant of every
        /// Blink armor SET. Address = "gear/armor/&lt;setName&gt;_Male" (e.g.
        /// gear/armor/Centurion_Male). The HumanFemale variant is intentionally NOT
        /// marked here — one Addressable entry per SET (the catalog emits one Gear row
        /// per set). The skinned female variant can be marked separately later if the
        /// equip system needs gendered visuals.</summary>
        internal static IEnumerable<(string guid, string address)> EnumerateArmorSetPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(ArmorRoot))
            {
                Debug.LogWarning($"[BlinkAddressableMarker] armor root '{ArmorRoot}' not found " +
                                 "(gitignored Blink pack absent?) — no armor enumerated.");
                yield break;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ArmorRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                string nameNoExt = Path.GetFileNameWithoutExtension(path);
                if (!TryParseArmorSet(nameNoExt, out string setName, out string gender)) continue;
                // Canonical = the Male variant only (one entry per set).
                if (!string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase)) continue;

                yield return (guid, ArmorAddrPrefix + setName + "_Male");
            }
        }

        /// <summary>Splits "Centurion_HumanMale" → set "Centurion", gender "Male"
        /// (and "Basic10_HumanFemale" → "Basic10", "Female"). Returns false for any
        /// name that does not carry the "_Human{Male|Female}" suffix.</summary>
        internal static bool TryParseArmorSet(string fileNameNoExt, out string setName, out string gender)
        {
            setName = null;
            gender = null;
            if (string.IsNullOrEmpty(fileNameNoExt)) return false;

            const string maleSuffix   = "_HumanMale";
            const string femaleSuffix = "_HumanFemale";

            if (fileNameNoExt.EndsWith(maleSuffix, StringComparison.OrdinalIgnoreCase))
            {
                setName = fileNameNoExt.Substring(0, fileNameNoExt.Length - maleSuffix.Length);
                gender = "Male";
            }
            else if (fileNameNoExt.EndsWith(femaleSuffix, StringComparison.OrdinalIgnoreCase))
            {
                setName = fileNameNoExt.Substring(0, fileNameNoExt.Length - femaleSuffix.Length);
                gender = "Female";
            }
            else
            {
                return false;
            }

            return setName.Length > 0;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Move the asset into the Gear group and set its address. Returns true when
        /// a change was made; false when the entry is already at this exact address (idempotent).</summary>
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

        /// <summary>Create the "Gear" group with the standard bundled/content-update schemas
        /// (mirrors what the Default Local Group ships with).</summary>
        private static AddressableAssetGroup CreateGearGroup(AddressableAssetSettings settings)
        {
            return settings.CreateGroup(
                GearGroup,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }
    }
}
