// =============================================================================
// VfxAddressablesGrouper — moves the VFX content OUT of Resources and files it
// into LOCAL Addressable bundles so the 81.1 MB of effect art stops shipping in
// EVERY build. Sibling of HeroAddressablesGrouper (WO-545 Tier-1,
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md).
// -----------------------------------------------------------------------------
// Assets/Resources/VFX ships in EVERY build because Resources/ is force-included,
// whether or not a single effect in it is ever played. Measured composition
// (verified — do not re-derive):
//     36.1 MB / 22 .tif   +   30.8 MB / 67 .prefab   +   13.3 MB / 44 .png
//     + small .mat / .shadergraph / .fbx.
// This tool is the GROUPING + MIGRATION half of the seam
// (DeNelle.Core.VfxAssetLoader is the CODE half). Two things must BOTH be true
// for the bytes to leave the payload:
//   1. the assets are marked Addressable (so they ride in a bundle), AND
//   2. the assets no longer live under any Resources/ folder (else they
//      double-ship — once in the Resources block AND once in the bundle).
//
// ── TOPOLOGY produced, and WHY ──────────────────────────────────────────────
//   • "Vfx_Shared"  — EVERYTHING under `_Shared/**` (66 textures, 56 materials,
//                     3 shaders, 2 models, 2 prefabs). This is the pool the big
//                     files live in: LargeFlame02.tif 6.6 MB, Explosion.tif 2.9,
//                     EnergyEffect.tif 2.8, SmokePuff01.tif 2.7, ...
//   • "Vfx_Effects" — every effect .prefab under the 13 category folders
//                     (Aura/ Boss/ Buffs/ Damage/ Death/ Env/ Harvest/ Markers/
//                     Portal/ Projectiles/ Status/ UI/ Weapon/), PLUS the two
//                     catalog ScriptableObjects at the VFX root.
//
//   WHY NOT one bundle per effect (or per category)? Because `_Shared/Textures`
//   feeds MANY effects. An asset that is NOT itself Addressable but is referenced
//   by an Addressable asset is pulled into that asset's bundle as an IMPLICIT
//   dependency — so with per-effect bundles the same 6.6 MB flame atlas would be
//   COPIED into every bundle whose prefab touches it. Marking the shared pool
//   explicitly into ONE group makes it a single shared bundle that the effect
//   bundles reference instead of duplicating. That is the whole reason the split
//   is drawn at `_Shared/**` and nowhere else: it is the de-duplication seam, not
//   an organisational preference. The effect prefabs then have no reason to be
//   split further — they are individually small (30.8 MB across 67), they are all
//   reachable from the catalogs (below), and more groups would only add bundle
//   overhead and more implicit-dependency edges to reason about.
//
//   WHY the CATALOGS ride with the effects: VFXCatalog.asset (76 GUID refs) and
//   HovlVfxCatalog.asset (153 GUID refs) hold DIRECT references to the effect
//   prefabs. THIS IS THE LOAD-BEARING FACT OF THE WHOLE MIGRATION — if the two
//   catalogs stayed in Resources they would drag every prefab they reference back
//   into the force-included Resources block and the migration would win ZERO
//   bytes. They must move, which is exactly why VFXManager.EnsureCatalog /
//   EnsureHovlCatalog were repointed at VfxAssetLoader first. Because the catalog
//   references every effect anyway, a separate catalog group would just become a
//   bundle that depends on the effects bundle — the same load graph with an extra
//   file. Tier-2 (a catalog that holds AssetReferences instead of hard refs) is
//   what would make effect loading genuinely lazy; this WO only takes the bytes
//   out of the force-included block.
//
//   All groups are LOCAL (default schema = the same Local.BuildPath/LoadPath the
//   shipping "Gear" and Hero_* bundles use → they land in
//   StreamingAssets/aa/<target>/*.bundle).
//
// MIGRATION target = Assets/VfxContent/ (NOT under any Resources/ folder). Moved
// via AssetDatabase.MoveAsset (GUID- and .meta-preserving → import settings travel
// with the asset and references by GUID do not break).
//
// ⚠ WEBGL SYNC/ASYNC GATE (see WO-545 RESULT): once assets leave Resources the
// only load path is Addressables, and VfxAssetLoader uses WaitForCompletion,
// which WebGL does NOT support for a bundle that still has to be downloaded. Run
// the MIGRATION only alongside the build check that confirms the VFX bundle is
// warmed async before the sync load (or after the loader goes async).
// GroupVfx() is mark-only and safe to run any time.
//
// Run (menu): Defenders > Build > Group VFX Addressable          (mark only, no move)
//             Defenders > Build > Migrate VFX Out Of Resources   (move only)
//             Defenders > Build > Group + Migrate VFX            (one-shot)
//   headless: -executeMethod DeNelle.Editor.VfxAddressablesGrouper.GroupAndMigrateVfx
// EDITOR-ONLY. Mutates the Addressables settings asset + moves assets; does NOT
// run gameplay, does NOT commit, does NOT touch any data JSON.
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
    /// <summary>Groups + migrates Assets/Resources/VFX into LOCAL Addressable bundles.
    /// Mark-only, migrate-only, and one-shot entry points; idempotent + guarded for the
    /// no-Addressables-settings case. See the file header for the topology rationale.</summary>
    public static class VfxAddressablesGrouper
    {
        // Pre-migration root (the shipped location today).
        internal const string VfxRoot = "Assets/Resources/VFX";

        // Post-migration destination (NOT under any Resources/ folder).
        internal const string VfxContentRoot = "Assets/VfxContent";

        // Address prefix — MUST match DeNelle.Core.VfxAssetLoader.VfxAddrPrefix. The address
        // is the extension-less path RELATIVE TO THE RESOURCES FOLDER, i.e. this prefix plus
        // the path under the VFX root:
        //   Assets/Resources/VFX/Aura/top_down_bomb_rainbow.prefab
        //     -> "VFX/Aura/top_down_bomb_rainbow"
        // Computed from the path, never from a hardcoded table.
        internal const string VfxAddrPrefix = "VFX/";

        // Group names (see header TOPOLOGY).
        internal const string SharedGroup  = "Vfx_Shared";
        internal const string EffectsGroup = "Vfx_Effects";

        // The shared-pool sub-folder — the de-duplication seam the topology is drawn at.
        internal const string SharedSub = "_Shared";

        // ── ⚠ KEEP-BEHIND LIST (read before changing anything here) ─────────────
        //
        // Anything still reached by a RAW Resources.Load that is NOT routed through
        // DeNelle.Core.VfxAssetLoader MUST stay under Resources/. Moving it would make that
        // load return null and SILENTLY kill the effect — no exception, no red line, just a
        // missing visual. Each entry is a path RELATIVE TO THE VFX ROOT (file or folder), and
        // each is justified by the file:line that still reaches it:
        //
        // ✅ THE LIST IS EMPTY, AND THAT IS THE FINISHED STATE — not an oversight.
        //
        // It held two entries when this file was written. Both were emptied the same session by
        // repointing the raw loads that justified them, so nothing under Resources/VFX is reached
        // by a load the seam does not own:
        //
        //   "Status" — was justified by AtbStatusVfx.cs Resources.Load<GameObject>(path) over its
        //       seven "VFX/Status/*" consts. NOW: VfxAssetLoader.LoadVfxPrefab(path). The consts
        //       were already exact full Resources-relative keys, so nothing about the keys changed.
        //
        //   "Portal/PortalCircleDarkStar.prefab" — was justified by
        //       DungeonWorldPortalSpawner.cs Resources.Load<GameObject>(CirclePrefabResourcePath).
        //       NOW: VfxAssetLoader.LoadVfxPrefab(CirclePrefabResourcePath).
        //
        // Emptying it is what makes the migration actually pay: a kept-behind prefab is referenced
        // by the migrated catalogs, so it would ride the effects bundle as an implicit dependency
        // AND stay in the force-included Resources block — i.e. DOUBLE-SHIP. Correct, but it wins
        // zero bytes for that asset. Empty means every byte under VFX/ genuinely leaves Resources.
        //
        // ⚠ IF YOU EVER ADD AN ENTRY BACK, add it WITH the file:line that justifies it, and treat
        // that as a bug to close rather than a state to live in — the right fix is almost always to
        // repoint the load at the seam (a one-token edit, since the keys already match), not to
        // pin the asset in Resources. Entries here are also SKIPPED BY GROUPING: marking an asset
        // Addressable while it still lives in Resources is the exact double-ship this WO removes.
        internal static readonly string[] KeepBehind = new string[0];

        // ── ⚠ MIGRATION BLOCKERS (editor-side raw Resources.Load of the catalogs) ─
        //
        // ✅ EMPTY — both known blockers were CLOSED the same session, not waived.
        //
        // A blocker here is an editor-side raw Resources.Load of a catalog. Resources.Load resolves
        // ONLY while the asset sits under a Resources/ folder (in the editor too), so the migration
        // moving the catalogs would turn each one null — silently. Two existed and both are fixed:
        //
        //   Assets/Editor/VfxProofCapture.cs — was Resources.Load of "VFX/VFXCatalog" +
        //       "VFX/HovlVfxCatalog"; now VfxAssetLoader.LoadVfxAsset<T>. Left raw, every VFX proof
        //       shot would have gone blank with no failing gate — evidence loss, the worst kind of
        //       break because the harness would still report success.
        //   Assets/Editor/Regression/SpawnBudgetAndVfxWarmRegression.cs — was Resources.Load of
        //       "VFX/HovlVfxCatalog"; now the seam. Left raw, the suite would have hard-redded on
        //       a null catalog for a reason unrelated to the spawn budget it measures.
        //
        // KEEP IT EMPTY. If a new editor-side raw catalog load appears, the fix is to route it
        // through VfxAssetLoader (same keys, one-token edit) — NOT to list it here and migrate
        // around it. This array exists so MigrateVfxOutOfResources can shout before it moves; an
        // entry in it means the migration is known-unsafe, which is a bug, not a configuration.
        private static readonly string[] MigrationBlockers = new string[0];

        // ── Entry points ────────────────────────────────────────────────────────

        [MenuItem("Defenders/Build/Group + Migrate VFX")]
        public static void GroupAndMigrateVfx()
        {
            // Order: migrate FIRST (so grouping reads from the final, non-Resources location),
            // then group at that location.
            MigrateVfxOutOfResources();
            GroupVfx();
        }

        [MenuItem("Defenders/Build/Group VFX Addressable")]
        public static void GroupVfx()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[VfxAddressablesGrouper] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null) — Addressables not " +
                    "initialised. Nothing grouped. VfxAssetLoader keeps using the Resources fallback.");
                return;
            }

            string root = ResolveActiveRoot();
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[VfxAddressablesGrouper] VFX root '{root}' not found — nothing grouped.");
                return;
            }

            AddressableAssetGroup shared  = settings.FindGroup(SharedGroup)  ?? CreateBundledGroup(settings, SharedGroup);
            AddressableAssetGroup effects = settings.FindGroup(EffectsGroup) ?? CreateBundledGroup(settings, EffectsGroup);
            if (shared == null || effects == null)
            {
                Debug.LogWarning($"[VfxAddressablesGrouper] could not create the '{SharedGroup}'/'{EffectsGroup}' " +
                                 "group(s) — nothing grouped.");
                return;
            }

            int markedShared = 0, markedEffects = 0, already = 0, dupes = 0, kept = 0;

            // (address|typeName) — two assets of the SAME TYPE at one address is an Addressables
            // BUILD ERROR, so detect and warn-and-skip (first-seen wins). Type is part of the key
            // because a prefab and a texture MAY legitimately share an address.
            var seenAddrType = new HashSet<string>(StringComparer.Ordinal);

            // "t:Object" = every asset under the root (an empty filter string is not reliably
            // supported); folders come back too and are filtered out below.
            foreach (string guid in AssetDatabase.FindAssets("t:Object", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;                 // folders are not entries
                if (!path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) continue;

                string rel = path.Substring(root.Length + 1).Replace('\\', '/'); // e.g. "Aura/foo.prefab"

                if (IsKeptBehind(rel))
                {
                    // Marking a still-in-Resources asset Addressable is the double-ship this WO removes.
                    kept++;
                    continue;
                }

                string address = AddressFor(rel);
                Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                string typeName = mainType != null ? mainType.Name : "Object";

                if (!seenAddrType.Add(address + "|" + typeName))
                {
                    dupes++;
                    Debug.LogWarning($"[VfxAddressablesGrouper] duplicate address+type '{address}' ({typeName}) " +
                                     $"at '{path}' — SKIPPED (first-seen wins). Two assets of the same type at one " +
                                     "address is an Addressables BUILD ERROR; rename one of the files.");
                    continue;
                }

                bool isShared = rel.StartsWith(SharedSub + "/", StringComparison.OrdinalIgnoreCase);
                AddressableAssetGroup group = isShared ? shared : effects;

                if (MarkEntry(settings, group, guid, address))
                {
                    if (isShared) markedShared++; else markedEffects++;
                }
                else
                {
                    already++;
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VfxAddressablesGrouper] Grouped from '{root}': marked {markedShared} into '{SharedGroup}' + " +
                      $"{markedEffects} into '{EffectsGroup}' ({already} already addressed, {dupes} duplicate-address " +
                      $"skipped, {kept} KEEP-BEHIND skipped — still Resources-loaded, see the KeepBehind list).");
        }

        [MenuItem("Defenders/Build/Migrate VFX Out Of Resources")]
        public static void MigrateVfxOutOfResources()
        {
            if (!AssetDatabase.IsValidFolder(VfxRoot))
            {
                Debug.LogWarning($"[VfxAddressablesGrouper] '{VfxRoot}' not found — nothing to migrate " +
                                 "(already migrated?).");
                return;
            }

            Debug.LogWarning("[VfxAddressablesGrouper] ⚠ MIGRATION BLOCKERS — these read the VFX catalogs through a " +
                             "RAW Resources.Load, which stops resolving (editor included) the moment the catalogs " +
                             "leave Resources. Repoint them in this same pass or they go null:\n  • " +
                             string.Join("\n  • ", MigrationBlockers));

            EnsureFolder(VfxContentRoot);
            int moved = 0, already = 0, failed = 0, kept = 0;

            MigrateFolder(VfxRoot, VfxContentRoot, ref moved, ref already, ref failed, ref kept);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VfxAddressablesGrouper] Migration: moved {moved} item(s) to '{VfxContentRoot}' " +
                      $"({already} already there / absent, {failed} failed, {kept} KEEP-BEHIND left in Resources " +
                      "because a raw Resources.Load still reaches them — see the KeepBehind list).");
            if (failed > 0)
                Debug.LogWarning("[VfxAddressablesGrouper] ⚠ one or more moves FAILED — see the MoveAsset warnings " +
                                 "above; the payload win is INCOMPLETE until every migrating VFX asset leaves " +
                                 "Resources (anything left behind double-ships).");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns the migrated root (Assets/VfxContent) when it already holds VFX content,
        /// else the pre-migration Resources root. Lets grouping run correctly before OR after a move.</summary>
        internal static string ResolveActiveRoot()
        {
            if (AssetDatabase.IsValidFolder(VfxContentRoot) &&
                AssetDatabase.FindAssets("t:Object", new[] { VfxContentRoot }).Length > 0)
                return VfxContentRoot;
            return VfxRoot;
        }

        /// <summary>Address = the extension-less path RELATIVE to the Resources folder, i.e.
        /// <see cref="VfxAddrPrefix"/> + <paramref name="relPath"/> minus its extension. Computed
        /// from the path so a new folder needs no table edit.</summary>
        internal static string AddressFor(string relPath)
        {
            string rel = relPath.Replace('\\', '/');
            string dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(rel);
            return string.IsNullOrEmpty(dir) ? VfxAddrPrefix + name : VfxAddrPrefix + dir + "/" + name;
        }

        /// <summary>True when <paramref name="relPath"/> (relative to the VFX root) is itself a
        /// KEEP-BEHIND entry, or lives under a KEEP-BEHIND folder.</summary>
        internal static bool IsKeptBehind(string relPath)
        {
            string rel = relPath.Replace('\\', '/').TrimEnd('/');
            foreach (string k in KeepBehind)
            {
                if (string.Equals(rel, k, StringComparison.OrdinalIgnoreCase)) return true;
                if (rel.StartsWith(k + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>True when anything at or under <paramref name="relPath"/> is KEEP-BEHIND —
        /// i.e. the folder cannot be moved wholesale and must be walked child by child.</summary>
        private static bool ContainsKeptBehind(string relPath)
        {
            string rel = relPath.Replace('\\', '/').TrimEnd('/');
            foreach (string k in KeepBehind)
            {
                if (string.Equals(rel, k, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.IsNullOrEmpty(rel)) return true;                      // root contains everything
                if (k.StartsWith(rel + "/", StringComparison.OrdinalIgnoreCase)) return true;
                if (rel.StartsWith(k + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Move the contents of <paramref name="srcFolder"/> into <paramref name="dstFolder"/>.
        /// A sub-tree with no KEEP-BEHIND item inside is moved WHOLESALE (one MoveAsset, cheapest and
        /// safest); a sub-tree that contains one is walked child by child so the kept item stays put.</summary>
        private static void MigrateFolder(string srcFolder, string dstFolder,
                                          ref int moved, ref int already, ref int failed, ref int kept)
        {
            foreach (string child in ChildEntries(srcFolder))
            {
                string leaf = Path.GetFileName(child);
                string dst = dstFolder + "/" + leaf;
                string rel = RelativeToVfxRoot(child);

                if (IsKeptBehind(rel))
                {
                    kept++;
                    Debug.Log($"[VfxAddressablesGrouper] KEEP-BEHIND: '{child}' stays in Resources " +
                              "(a raw Resources.Load still reaches it — see the KeepBehind list).");
                    continue;
                }

                bool isFolder = AssetDatabase.IsValidFolder(child);
                if (isFolder && ContainsKeptBehind(rel))
                {
                    EnsureFolder(dst);
                    MigrateFolder(child, dst, ref moved, ref already, ref failed, ref kept);
                    continue;
                }

                moved += TryMove(child, dst, ref already, ref failed);
            }
        }

        /// <summary>Path of <paramref name="assetPath"/> relative to <see cref="VfxRoot"/>
        /// ("" when it IS the root). Used to test the KEEP-BEHIND list.</summary>
        private static string RelativeToVfxRoot(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            if (p.StartsWith(VfxRoot + "/", StringComparison.OrdinalIgnoreCase))
                return p.Substring(VfxRoot.Length + 1);
            return string.Equals(p, VfxRoot, StringComparison.OrdinalIgnoreCase) ? string.Empty : p;
        }

        /// <summary>Immediate children (files + folders) of an asset folder, as asset paths.
        /// Skips .meta sidecars — AssetDatabase.MoveAsset carries those itself.</summary>
        private static IEnumerable<string> ChildEntries(string folder)
        {
            var results = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return results;

            foreach (string dir in Directory.GetDirectories(folder))
                results.Add(dir.Replace('\\', '/'));
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(file.Replace('\\', '/'));
            }
            results.Sort(StringComparer.Ordinal);
            return results;
        }

        /// <summary>Move an asset (file or folder) if it exists at <paramref name="src"/>. Increments
        /// <paramref name="already"/> when the source is gone (assumed already migrated) and
        /// <paramref name="failed"/> on a MoveAsset error. Returns 1 on a successful move, else 0.</summary>
        private static int TryMove(string src, string dst, ref int already, ref int failed)
        {
            if (!AssetDatabase.IsValidFolder(src) && AssetDatabase.AssetPathToGUID(src) == string.Empty)
            {
                already++;
                return 0;
            }
            string err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err)) return 1;
            failed++;
            Debug.LogWarning($"[VfxAddressablesGrouper] MoveAsset '{src}' -> '{dst}' FAILED: {err}");
            return 0;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Move the asset into the group and set its address. True when a change was
        /// made; false when already at this exact address (idempotent). Mirrors HeroAddressablesGrouper.</summary>
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

        /// <summary>Create a LOCAL bundled group with the standard bundled/content-update schemas
        /// (mirrors the Default Local Group + the shipping 'Gear'/'Hero_*' groups —
        /// Local.BuildPath/LoadPath, so the bundle lands in StreamingAssets/aa/&lt;target&gt;/).</summary>
        private static AddressableAssetGroup CreateBundledGroup(AddressableAssetSettings settings, string groupName)
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
