// =============================================================================
// SupercyanGearAddressableMarker — marks the gitignored Supercyan Fantasy prop
// prefabs Addressable so the gear catalog can reference them by a stable address.
// -----------------------------------------------------------------------------
// SAME RATIONALE AS BlinkAddressableMarker (read that file's header first): the
// Supercyan pack lives OUTSIDE Assets/Resources/ and is gitignored (.gitignore
// line "/Assets/Supercyan/"), so it is NOT Resources.Load-able. Mirroring it into
// Resources would force-include it in every build (WO-191/408) — Addressables is
// the right door, and it is the door EquipmentController already opens: a catalog
// row whose prefabPath starts "gear/" takes the ADDRESSABLE branch
// (EquipmentController.LoadsViaAddressable) and seats NATIVE.
//
// ADDRESS SCHEME: identical to the Blink weapons scheme so a catalog prefabPath IS
// the address —  "gear/weapon/<prefabFileName>"  e.g. gear/weapon/ShieldWithItemLogic.
//
// WHY THE FILE NAME IS THE KEY, NOT A PRETTIER SLUG: AttachmentOffsetRegistry is
// keyed on the LAST address segment (EquipmentController.VisualFromCatalog derives
// vis.mesh from it; AttachOffHandProp then looks the offset up by that mesh name).
// Renaming the address silently orphans any Offset Forge row the owner dials for it.
//
// The prefab carries Supercyan's own ItemLogic MonoBehaviour. That component is a
// PASSIVE data holder (serialized fields + getters; no Update, no Awake, no
// reparenting) — verified at Assets/Supercyan/Scripts/Items/ItemLogic.cs — so it
// cannot fight our seat. It is left on deliberately rather than authoring a
// stripped copy into the tree: the pack is gitignored, so a hand-made copy would
// not survive a re-import on another clone.
//
// IDEMPOTENT: an entry already at its target address is skipped (no churn).
// GUARDED: absent Addressables settings or an absent (not-yet-imported) pack
// LogWarnings and returns — never throws in the editor.
//
// Run: Defenders > Catalog > Mark Supercyan Gear Addressable
//   or headless -executeMethod DeNelle.Editor.Catalog.SupercyanGearAddressableMarker.MarkSupercyanGear
// EDITOR-ONLY. Mutates the Addressables settings asset; does NOT run gameplay,
// does NOT commit, does NOT touch the gear JSON.
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor.Catalog
{
    /// <summary>Marks the Supercyan Fantasy prop prefabs Addressable under the shared
    /// "Gear" group using the Blink address scheme. Idempotent + guarded.</summary>
    public static class SupercyanGearAddressableMarker
    {
        // The High Quality prop prefabs WITH Supercyan's ItemLogic component. Verified
        // on-disk 2026-08-18. The Mobile/ variants are deliberately NOT marked — one
        // entry per prop; a mobile LOD swap would be a separate, explicit decision.
        internal const string PropsRoot =
            "Assets/Supercyan/Prefabs/Fantasy/WithItemLogic/High Quality";

        /// <summary>The prefabs we actually want addressed, by file name (no extension).
        /// An allow-list, NOT a folder sweep: the pack ships props the catalog has no row
        /// for, and a sweep would publish addresses for gear nothing can equip. Add a name
        /// here in the same commit as its weapons.json row.</summary>
        internal static readonly string[] PropNames =
        {
            "ShieldWithItemLogic",   // knight_shield_starter — the default shield (2026-08-18)
        };

        [MenuItem("Defenders/Catalog/Mark Supercyan Gear Addressable")]
        public static void MarkSupercyanGear()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[SupercyanGearAddressableMarker] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null) — nothing marked. " +
                    "CONSEQUENCE: knight_shield_starter's addressable load FAILS at runtime and " +
                    "EquipmentController falls back to the legacy Resources shield_A mesh.");
                return;
            }

            AddressableAssetGroup group =
                settings.FindGroup(BlinkAddressableMarker.GearGroup) ?? CreateGearGroup(settings);
            if (group == null)
            {
                Debug.LogWarning("[SupercyanGearAddressableMarker] could not find or create the '" +
                    BlinkAddressableMarker.GearGroup + "' Addressables group — nothing marked.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PropsRoot))
            {
                Debug.LogWarning("[SupercyanGearAddressableMarker] props root '" + PropsRoot +
                    "' not found (gitignored Supercyan pack absent on this clone?) — nothing marked.");
                return;
            }

            int marked = 0, skipped = 0, missing = 0;
            foreach (string propName in PropNames)
            {
                string path = PropsRoot + "/" + propName + ".prefab";
                string address = Address(propName);

                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning("[SupercyanGearAddressableMarker] '" + path + "' not found — " +
                        "address '" + address + "' NOT created. A catalog row pointing at it falls " +
                        "back to the legacy Resources mesh.");
                    missing++;
                    continue;
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, postEvent: false);
                if (entry == null)
                {
                    Debug.LogWarning("[SupercyanGearAddressableMarker] CreateOrMoveEntry returned null " +
                        "for '" + path + "' — address '" + address + "' NOT created.");
                    missing++;
                    continue;
                }

                if (string.Equals(entry.address, address, StringComparison.Ordinal))
                {
                    skipped++;   // already addressed — no churn
                    continue;
                }

                entry.SetAddress(address, postEvent: false);
                marked++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification,
                              null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log("[SupercyanGearAddressableMarker] Marked " + marked + " prefab(s) Addressable in " +
                      "group '" + BlinkAddressableMarker.GearGroup + "' (" + skipped +
                      " already addressed, " + missing + " missing).");
        }

        /// <summary>The stable address for a prop file name — shares the Blink weapon prefix
        /// so a catalog prefabPath IS the address.</summary>
        internal static string Address(string prefabFileNameNoExt) =>
            BlinkAddressableMarker.WeaponAddrPrefix + prefabFileNameNoExt;

        /// <summary>Create the shared "Gear" group if the Blink marker has not already.
        /// Mirrors BlinkAddressableMarker.CreateGearGroup (same schemas) so whichever of the
        /// two runs first produces the identical group.</summary>
        private static AddressableAssetGroup CreateGearGroup(AddressableAssetSettings settings)
        {
            return settings.CreateGroup(
                BlinkAddressableMarker.GearGroup,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }
    }
}
