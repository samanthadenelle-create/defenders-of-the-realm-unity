// =============================================================================
// EnemyContentMigrator — move enemy art out of Resources into a REMOTE
// Addressable group, and prove nothing was lost doing it.
// -----------------------------------------------------------------------------
// Second application of the procedure proven on structures (2026-08-18). Enemy
// art is 112 MB after the Blink removal, 107 MB of it FBX, and Unity
// FORCE-INCLUDES everything under a Resources/ folder — an enemy the player never
// fights still ships in every APK.
//
// WHY THIS ONE IS SAFER THAN THE FIRST: the runtime was ALREADY clean before the
// move — zero raw Resources.Load("Enemies/…") call sites; everything resolves
// through EnemyAssetLoader (Addressables-first, Resources-fallback). Only the 71
// editor hardcodes needed repointing, and those now read
// DeNelle.Core.AssetRoots.EnemyContent.
//
// ⛔ LESSONS BAKED IN FROM THE STRUCTURES MIGRATION — each cost real time:
//  1. MOVE THE PARENT FOLDER, NEVER PER-FILE. AssetDatabase.MoveAsset refuses to
//     move files out of a "<name>.fbm" folder AND refuses to move the folder
//     itself. Per-file moves stranded 20 models away from their textures and left
//     100 regression failures. A folder move is one operation: all-or-nothing.
//  2. NEVER route around MoveAsset with filesystem calls. Doing so nested folders
//     into themselves and tripped Unity's collision auto-rename.
//  3. COUNT BEFORE AND AFTER and fail loudly on a mismatch. "It reported success"
//     is not evidence; a file count is.
//  4. The bytes only LEAVE the APK if the group is REMOTE. A local Addressable
//     bundle is copied into StreamingAssets and ships anyway.
//  5. The uploaded bundle must sit under the [BuildTarget] segment. Remote.LoadPath
//     ends in /[BuildTarget], so the player requests /Android/<bundle>; uploading
//     to the bucket root 404s every asset while every static gate stays green.
// =============================================================================

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Moves Resources/Enemies to a remote Addressable group.</summary>
    public static class EnemyContentMigrator
    {
        private const string ResourcesRoot = "Assets/Resources/Enemies";
        private const string ContentRoot   = DeNelle.Core.AssetRoots.EnemyContent;
        private const string GroupName     = "Enemy_Art";
        private const string OkMarker      = "ENEMY_MIGRATE_OK";

        /// <summary>Remote.BuildPath / Remote.LoadPath profile variable ids (Default profile).</summary>
        private const string RemoteBuildPathId = "ad0e68328bd7fd54ea79f0a9ab1dd9b1";
        private const string RemoteLoadPathId  = "cf151d4962873af43b9302d323a9d707";

        [MenuItem("Defenders/Art/Enemies -> Addressables (MOVE FOLDER)")]
        public static void MoveFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
            {
                Debug.LogError($"[EnemyMigrate] '{ResourcesRoot}' not found — already moved, or wrong project.");
                return;
            }

            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[EnemyMigrate] Addressables settings missing — ABORTED before touching anything. " +
                               "Moving art out of Resources with no Addressables home makes every enemy unloadable.");
                return;
            }

            int before = System.IO.Directory
                .GetFiles(ResourcesRoot, "*", System.IO.SearchOption.AllDirectories)
                .Count(p => !p.EndsWith(".meta"));

            string err = AssetDatabase.MoveAsset(ResourcesRoot, ContentRoot);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[EnemyMigrate] FOLDER MOVE FAILED '{ResourcesRoot}' -> '{ContentRoot}': {err}. " +
                               "Nothing moved — MoveAsset on a folder is all-or-nothing.");
                return;
            }
            AssetDatabase.Refresh();

            int after = System.IO.Directory
                .GetFiles(ContentRoot, "*", System.IO.SearchOption.AllDirectories)
                .Count(p => !p.EndsWith(".meta"));
            Debug.Log($"[EnemyMigrate] folder moved: {before} file(s) -> {after} at the new root.");
            if (before != after)
            {
                Debug.LogError($"[EnemyMigrate] FILE COUNT CHANGED ({before} -> {after}) — files lost or duplicated. " +
                               "Investigate before building.");
                return;
            }

            // ---- group, pointed at the REMOTE profile ----------------------------
            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(
                GroupName, setAsDefaultGroup: false, readOnly: false, postEvent: false,
                schemasToCopy: null,
                types: new[] { typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                               typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema) });

            var schema = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BuildPath.SetVariableById(settings, RemoteBuildPathId);
                schema.LoadPath.SetVariableById(settings, RemoteLoadPathId);

                // ⛔ DIRTY + SAVE, THEN VERIFY BY READING IT BACK.
                // The first run set these and logged "group set to the REMOTE profile" — and the
                // values did NOT persist. The group built LOCAL, its bundle went into the APK, and
                // the APK grew 8.5 MB while the migrator reported success. A setter whose result is
                // never read back is a claim, not a fact; on a size migration the whole point is the
                // byte movement, so this is exactly the thing that must be proven.
                EditorUtility.SetDirty(schema);
                EditorUtility.SetDirty(group);
                AssetDatabase.SaveAssets();

                string buildId = schema.BuildPath.Id;
                string loadId  = schema.LoadPath.Id;
                if (buildId != RemoteBuildPathId || loadId != RemoteLoadPathId)
                {
                    Debug.LogError($"[EnemyMigrate] REMOTE PROFILE DID NOT STICK — build='{buildId}' load='{loadId}' " +
                                   $"(expected '{RemoteBuildPathId}' / '{RemoteLoadPathId}'). The group will build " +
                                   "LOCAL and its bytes will ship INSIDE the APK. Set the ids in the schema asset " +
                                   "directly before building.");
                }
                else
                {
                    Debug.Log("[EnemyMigrate] group verified REMOTE (ServerData/[BuildTarget] -> r2.dev/[BuildTarget]).");
                }
            }
            else
            {
                Debug.LogError("[EnemyMigrate] group has no BundledAssetGroupSchema — it will default to LOCAL " +
                               "and the bytes will still ship in the APK. Fix before building.");
            }

            // ---- address every model the resolver can ask for --------------------
            // Addresses are the EXACT keys EnemyAssetLoader builds: "Enemies/<slug>". Do not invent
            // a second scheme — the loader and the grouper must agree on the string, verbatim.
            int marked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject t:Model t:AnimatorController", new[] { ContentRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                string rel = path.Substring(ContentRoot.Length + 1);
                string address = "Enemies/" + System.IO.Path.ChangeExtension(rel, null).Replace('\\', '/');

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry == null) continue;
                entry.SetAddress(address, postEvent: false);
                marked++;
            }

            settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings
                                  .ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyMigrate] marked {marked} asset(s) addressable in '{GroupName}'.");
            Debug.Log($"{OkMarker} {after} files, {marked} addressable");
        }
    }
}
