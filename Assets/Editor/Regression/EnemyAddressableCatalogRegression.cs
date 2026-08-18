// =============================================================================
// EnemyAddressableCatalogRegression — the enemy ADDRESSES must actually be in the
// Addressables catalog once the art leaves Resources.
// -----------------------------------------------------------------------------
// THE BUG CLASS THIS GUARDS. `Assets/EnemyContent` (~539 MB) is migrating into
// an Addressable group so it stops being force-included in every build. The runtime
// seam DeNelle.Core.EnemyAssetLoader is Addressables-FIRST with a Resources-FALLBACK,
// which is exactly what makes the migration safe — and also exactly what makes it
// SILENT when it goes wrong. Once the Resources copy is gone, an enemy address that
// was never marked Addressable (or was quietly unmarked, or whose group was dropped
// from the content build) resolves to NOTHING: the seam falls back to Resources, finds
// nothing there either, and EnemyFactory spawns a tinted capsule. The wave still runs.
// Nothing throws. The player just fights coloured pills.
//
// Every other enemy oracle in this folder now asks the SEAM ("does this model load?"),
// which is the right question in the editor — but in the editor the Addressables
// PLAY-MODE script can serve assets straight from the AssetDatabase, so a mesh can
// "load" in the gate and still be absent from the built catalog. This oracle asks the
// one question the seam cannot: is the ADDRESS REGISTERED, and does its entry still
// point at an asset that is on disk?
//
// MIGRATION-STATE AWARE — green before the move, green after, red only in the
// dangerous state:
//   • PRE-migration  (Assets/EnemyContent still holds models): grouping has not
//     happened yet by design. Missing addresses are REPORTED as a progress note, not
//     failed — failing here would be demanding work that is not due.
//   • POST-migration (the Resources copy is gone): every committed model address AND
//     every shared controller address MUST be registered. This is the assertion that
//     stops a future seat quietly unmarking a group and shipping a roster of capsules.
//   • ALWAYS: a DANGLING enemy entry (address registered, but its GUID resolves to no
//     file on disk) is a FAILURE in either state. The .asset keeps the GUID after the
//     source is deleted or gitignored, so a dangling entry is a registration that
//     LOOKS fine and loads nothing — the same trap DataWebRegression catches for gear.
//
// SHARED AUTHORITY, NOT A SECOND COPY. The required address list is derived from
// EnemyResolver.CommittedModelKeys and EnemyAnimatorFactory.ResolveControllerName —
// the same members the runtime consults — prefixed with
// EnemyAssetLoader.EnemyAddrPrefix. Re-typing an address list here is how a gate and
// the game come to disagree while both report success.
//
// Contract mirrors its siblings: public static bool Run(out string reason). Never
// throws. Editor-only asset reads, no scene, no PlayMode. Registered in
// DataRegression.RunAll covenant-style.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Enemies;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EnemyAddressableCatalogRegression
    {
        /// <summary>Where the pre-migration Resources copy lives. Its ABSENCE is the migration signal.</summary>
        private const string ResourcesEnemyRoot = DeNelle.Core.AssetRoots.EnemyContent;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // --- 1. Which side of the migration are we on? ------------------------------
            // "Still in Resources" = the folder exists AND still contains at least one model
            // or prefab. An empty leftover folder counts as MIGRATED (the art is gone), which
            // is the state the strict assertion is for.
            bool resourcesCopyPresent = false;
            if (AssetDatabase.IsValidFolder(ResourcesEnemyRoot))
            {
                var models  = AssetDatabase.FindAssets("t:Model",  new[] { ResourcesEnemyRoot });
                var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ResourcesEnemyRoot });
                resourcesCopyPresent = (models != null && models.Length > 0) ||
                                       (prefabs != null && prefabs.Length > 0);
            }

            // --- 2. Read the catalog as authored (settings -> groups -> entries) --------
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                // No Addressables settings object at all. Pre-migration that is simply the
                // untouched default; post-migration it means the art has NO home.
                if (resourcesCopyPresent)
                {
                    reason = "ENEMY_ADDR_CATALOG_OK (pre-migration): no AddressableAssetSettings yet, and the " +
                             "Resources copy at " + ResourcesEnemyRoot + " is still the live path — nothing to assert.";
                    return true;
                }
                reason = "ENEMY_ADDR_CATALOG_FAIL: " + ResourcesEnemyRoot + " no longer holds enemy art AND there is " +
                         "no AddressableAssetSettings object — EnemyAssetLoader can resolve NOTHING, so every enemy " +
                         "in the game spawns as a tinted capsule.";
                return false;
            }

            var addresses = new HashSet<string>(StringComparer.Ordinal);
            var dangling  = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address)) continue;
                    addresses.Add(entry.address);

                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(assetPath) ||
                        (!File.Exists(assetPath) && !Directory.Exists(assetPath)))
                        dangling.Add(entry.address);
                }
            }

            // --- 3. The addresses the runtime WILL ask for (shared authority) ----------
            var requiredBodies = new SortedSet<string>(StringComparer.Ordinal);
            var requiredCtrls  = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var key in EnemyResolver.CommittedModelKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                requiredBodies.Add(EnemyAssetLoader.EnemyAddrPrefix + key);

                string ctrl = EnemyAnimatorFactory.ResolveControllerName(key);
                if (!string.IsNullOrEmpty(ctrl))
                    requiredCtrls.Add(EnemyAssetLoader.EnemyAddrPrefix + ctrl);
            }

            // --- 4. ALWAYS-ON: a dangling enemy entry is broken in either state ---------
            var danglingEnemy = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var a in dangling)
                if (a.StartsWith(EnemyAssetLoader.EnemyAddrPrefix, StringComparison.Ordinal))
                    danglingEnemy.Add(a);
            if (danglingEnemy.Count > 0)
                failures.Add($"{danglingEnemy.Count} enemy address(es) are registered but DANGLING (the entry keeps the " +
                             "GUID after the asset was deleted/moved/gitignored, so it looks registered and loads " +
                             $"nothing -> tinted capsule): {Preview(danglingEnemy)}");

            // --- 5. Registration coverage ----------------------------------------------
            var missingBodies = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var a in requiredBodies)
                if (!addresses.Contains(a)) missingBodies.Add(a);

            var missingCtrls = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var a in requiredCtrls)
                if (!addresses.Contains(a)) missingCtrls.Add(a);

            if (!resourcesCopyPresent)
            {
                // POST-MIGRATION: the fallback is gone, so the catalog is the ONLY path.
                if (missingBodies.Count > 0)
                    failures.Add($"{missingBodies.Count} of {requiredBodies.Count} committed enemy body address(es) are " +
                                 $"NOT registered in any Addressable group, and {ResourcesEnemyRoot} no longer holds the " +
                                 "art — EnemyAssetLoader resolves these via neither Addressables nor Resources, so every " +
                                 $"enemy steered to them spawns a tinted capsule: {Preview(missingBodies)}");
                if (missingCtrls.Count > 0)
                    failures.Add($"{missingCtrls.Count} of {requiredCtrls.Count} shared enemy CONTROLLER address(es) are " +
                                 "NOT registered and the Resources copy is gone — EnemyAnimatorFactory gets a null " +
                                 "controller, so the body spawns and then slides with no clip (WO-436 Failure B): " +
                                 Preview(missingCtrls));
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"ENEMY_ADDR_CATALOG_FAIL: {failures.Count} issue(s):");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                reason = sb.ToString();
                return false;
            }

            var ok = new StringBuilder("ENEMY_ADDR_CATALOG_OK — ");
            if (resourcesCopyPresent)
            {
                ok.Append("PRE-migration (").Append(ResourcesEnemyRoot).Append(" still holds the live art, so ")
                  .Append("EnemyAssetLoader's Resources fallback is the working path). Grouping progress: ")
                  .Append(requiredBodies.Count - missingBodies.Count).Append('/').Append(requiredBodies.Count)
                  .Append(" body address(es) and ")
                  .Append(requiredCtrls.Count - missingCtrls.Count).Append('/').Append(requiredCtrls.Count)
                  .Append(" controller address(es) already registered; no dangling enemy entries. ")
                  .Append("The strict registration assertion ARMS ITSELF automatically the moment the Resources ")
                  .Append("copy is removed.");
            }
            else
            {
                ok.Append("POST-migration (").Append(ResourcesEnemyRoot).Append(" no longer holds enemy art): all ")
                  .Append(requiredBodies.Count).Append(" committed body address(es) and ").Append(requiredCtrls.Count)
                  .Append(" shared controller address(es) are registered in the Addressables catalog and every entry ")
                  .Append("still points at an asset on disk.");
            }
            reason = ok.ToString();
            return true;
        }

        private static string Preview(SortedSet<string> set)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var s in set)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(s);
                if (++i >= 8) { if (set.Count > 8) sb.Append(", … (+").Append(set.Count - 8).Append(" more)"); break; }
            }
            return sb.ToString();
        }
    }
}
