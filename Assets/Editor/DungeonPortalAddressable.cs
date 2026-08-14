// =============================================================================
// DungeonPortalAddressable — WO-983/1007: register the owner's Portal art as an
// Addressable instead of piling it into Resources/.
//
// WHY ADDRESSABLES AND NOT Resources/ (owner direction 2026-08-14).
// EVERYTHING under a Resources/ folder is force-included in every player build whether
// a scene references it or not. The Portal is 5.3 MB of FBX plus 2.2 MB of embedded
// textures — 7.5 MB added to every platform, including the web payload that already grew
// 42% (165 MB -> 234 MB) on 2026-08-10. That is precisely the cost WO-545/WO-282 were
// minted to remove, so adding to it would have been moving backwards.
//
// WHY THE ASSET MOVED OUT OF Resources/ FIRST.
// An asset that is BOTH under Resources/ and marked Addressable is included TWICE — once
// eagerly in the player, once in a bundle. It lives at Assets/Art/Dungeon/Exit/ (verified
// not gitignored, unlike Resources/Structures/ where it sat unreachable since 2026-05-25).
//
// ⚠ THIS IS ONLY SAFE BECAUSE WO-974 LANDED FIRST.
// Before that, whether bundles were built at all was decided by an UNCOMMITTED per-machine
// Editor preference, and no build entry point called BuildPlayerContent. Marking an asset
// Addressable under those conditions means it resolves here and resolves to NOTHING on a
// fresh clone or CI — strictly worse than the primitive fallback it replaces.
// AddressablesContentBuild.EnsureBuilt now runs in all four player-build seams.
//
// Run: Defenders/Dungeon/Register Portal Addressable  (or -executeMethod in batchmode).
// =============================================================================
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonPortalAddressable
    {
        public const string GroupName  = "Dungeon";
        public const string PortalPath = "Assets/Art/Dungeon/Exit/Portal.fbx";
        /// <summary>Stable runtime key. Content, not path — moving the file must not break it.</summary>
        public const string PortalKey  = "dungeon/exit/portal";

        [MenuItem("Defenders/Dungeon/Register Portal Addressable")]
        public static void Register()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("PORTAL_ADDRESSABLE_FAIL :: no AddressableAssetSettings in this project.");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(PortalPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"PORTAL_ADDRESSABLE_FAIL :: nothing at {PortalPath}. " +
                               "The asset moved or was never imported — do NOT create an entry for a path " +
                               "that does not resolve (that is the WO-975 defect: a tracked group asserting " +
                               "content a clone does not have).");
                return;
            }

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == GroupName);
            if (group == null)
            {
                group = settings.CreateGroup(GroupName, false, false, true, null,
                                             settings.DefaultGroup.Schemas.Select(s => s.GetType()).ToArray());
                Debug.Log($"[Addressables] created group '{GroupName}' (schemas cloned from the default group).");
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
            {
                Debug.LogError("PORTAL_ADDRESSABLE_FAIL :: CreateOrMoveEntry returned null.");
                return;
            }

            entry.address = PortalKey;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true, true);
            AssetDatabase.SaveAssets();

            // Assert the OUTCOME, not the call (INSTRUMENTATION_STANDARD §1.4b): re-read the entry
            // back out of the group so a silently-failed move cannot print as a success.
            var readBack = group.entries.FirstOrDefault(e => e != null && e.guid == guid);
            if (readBack == null || readBack.address != PortalKey)
            {
                Debug.LogError($"PORTAL_ADDRESSABLE_FAIL :: entry did not persist in '{GroupName}' " +
                               $"(readBack={(readBack == null ? "<null>" : readBack.address)}).");
                return;
            }

            Debug.Log($"PORTAL_ADDRESSABLE_OK key='{readBack.address}' group='{GroupName}' " +
                      $"guid={guid} path={PortalPath}");
        }
    }
}
