// =============================================================================
// EnemyAddressablesGrouper — marks the enemy art Addressable and (separately)
// migrates it OUT of Resources, so the ~539 MB under Assets/EnemyContent
// stops shipping in every build. Sibling of HeroAddressablesGrouper (WO-545,
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md); same shape, enemy address space.
// -----------------------------------------------------------------------------
// WHY: Unity FORCE-INCLUDES everything under a Resources/ folder in EVERY player
// build, spawned or not. Assets/EnemyContent is 539 MB (Blink/ alone is
// 427 MB, of which ~290 MB is Blink/Textures) — the single largest line item in
// the payload, landing whole in WebGL.data / the APK.
//
// TWO CONDITIONS MUST BOTH HOLD for the bytes to actually leave the build:
//   1. the assets are marked Addressable (so they ride in a bundle), AND
//   2. the assets no longer live under ANY Resources/ folder — otherwise they
//      DOUBLE-ship: once in the Resources block of the .data/APK AND once in the
//      bundle. Marking alone wins nothing; moving alone breaks every loader.
// Hence the two independently-runnable entry points, plus the one-shot.
//
// TOPOLOGY produced (all LOCAL bundled groups — Local.BuildPath/LoadPath, the same
// schemas the shipping "Gear"/"Hero_*" groups use → StreamingAssets/aa/<target>/):
//   • "Enemy_Blink"       — every prefab / mesh FBX / controller under <root>/Blink.
//                           THE BIG ONE (427 MB). Blink/Textures + Blink/Materials are
//                           deliberately NOT marked: they ride as implicit dependencies
//                           of this same bundle (same rationale as the hero .fbm folders),
//                           which keeps the encounter's art in one bundle.
//   • "Enemy_Models"      — top-level *.fbx / *.prefab  (address "Enemies/<name>")
//   • "Enemy_Controllers" — top-level *.controller      (address "Enemies/<name>")
//   • "Enemy_Textures"    — loose top-level textures + textures/ (NOT OrcTex/TripoTex —
//                           see keep-behind — and NOT *.fbm/, which ride with their FBX).
//
// ADDRESS RULE (computed, never a table): the address is the extension-less path
// RELATIVE TO the Resources folder, used verbatim —
//     Assets/EnemyContent/Orc_Warrior.fbx        -> "Enemies/Orc_Warrior"
//     Assets/EnemyContent/OrcHumanoid.controller -> "Enemies/OrcHumanoid"
//     Assets/EnemyContent/Boss_Dragon.prefab     -> "Enemies/Boss_Dragon"
//     Assets/EnemyContent/Blink/BlinkOrc.controller -> "Enemies/Blink/BlinkOrc"
// Post-migration the same relative path under Assets/EnemyContent yields the SAME
// address, so grouping is correct before OR after the move (ResolveActiveRoot).
// A prefab and a controller MAY share one address (the loader queries type-filtered,
// exactly as HeroAssetLoader does); TWO ASSETS OF THE SAME TYPE at one address is an
// Addressables BUILD ERROR — detected and warn-and-skipped, first-seen wins.
//
// MIGRATION target = Assets/EnemyContent/ (NOT under any Resources/ folder), via
// AssetDatabase.MoveAsset (GUID- and .meta-preserving → import settings, texture
// caps and every scene/prefab GUID reference survive the move).
//
// ⛔ KEEP-BEHIND LIST — these STAY in Assets/EnemyContent and are neither moved
//    NOR marked, because a RAW Resources.Load still reaches them and would silently
//    return null (a nulled VFX set / atlas is invisible until it is on screen):
//   • EnemyVfxSet_Default.asset
//       Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs:62  (key const)
//       Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs:111 (raw
//       Resources.Load<EnemyTypeVfxSet>("Enemies/EnemyVfxSet_Default")) — NOT routed
//       through EnemyAssetLoader.
//   • VfxSets/  (folder; not authored yet — reserved so a future per-family asset
//       lands on the surviving Resources path)
//       Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs:65  (path format)
//       Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs:104 (raw
//       Resources.Load<EnemyTypeVfxSet>("Enemies/VfxSets/EnemyVfxSet_<family>"))
//   • OrcTex/   (~1 MB — no payload win, all downside)
//       Assets/Editor/Regression/EnemyRigColorRegression.cs:171 — RAW
//       Resources.Load<Texture>("Enemies/OrcTex/<model>_basecolor"); the rig-colour
//       audit fails (reads as "UNCOLORED") the moment this folder leaves Resources.
//       Also hardcoded as an absolute Resources path by
//       Assets/Editor/PromoteOrcsToResources.cs:25 (TexDir) and
//       Assets/Editor/BattleAnchorStageVerify.cs:41-45 (TexDir).
//       Runtime consumers go through HeroTextureLoader (Addressables-first,
//       Resources-fallback): Assets/_Modules/Village/Enemies/EnemyFactory.cs:489,
//       Assets/_Modules/BattleATB/AtbCombatantSwapper.cs:756-758 → fine either way.
//   • TripoTex/ (~1 MB) — sibling atlas set probed immediately before OrcTex at
//       Assets/_Modules/Village/Enemies/EnemyFactory.cs:487; kept with OrcTex so the
//       two-rung basecolor probe never straddles two storage mechanisms.
//
// FOLLOW-UP (not a keep-behind, no runtime load): Assets/Editor/BlinkOrcImporter.cs:48
// stages into DeNelle.Core.AssetRoots.EnemyContent + "/Blink" — re-running that INTAKE tool after the
// migration re-creates the folder inside Resources. Repoint it (or re-run this migrator)
// whenever new Blink art is imported.
//
// CODE HALF: DeNelle.Core.EnemyAssetLoader
// (Assets/_Modules/Core/Addressables/EnemyAssetLoader.cs) — Addressables-first /
// Resources-fallback; EnemyAddrPrefix there MUST equal EnemyAddrPrefix here.
//
// Run (menu): Defenders > Build > Group Enemies Addressable        (mark only, no move)
//             Defenders > Build > Migrate Enemies Out Of Resources (move only)
//             Defenders > Build > Group + Migrate Enemies          (one-shot)
//   headless: -executeMethod DeNelle.Editor.EnemyAddressablesGrouper.GroupAndMigrateEnemies
// EDITOR-ONLY. Mutates the Addressables settings asset + moves assets; does NOT run
// gameplay, does NOT commit, does NOT touch any data JSON.
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
    /// <summary>Groups + migrates the Resources/Enemies art into LOCAL Addressable bundles.
    /// Mark-only, migrate-only and one-shot entry points; idempotent, keep-behind aware and
    /// guarded for the no-Addressables-settings case.</summary>
    public static class EnemyAddressablesGrouper
    {
        // Pre-migration folder (the shipped location today — force-included, 539 MB).
        internal const string EnemiesRoot = DeNelle.Core.AssetRoots.EnemyContent;

        // Post-migration destination (NOT under any Resources/ folder).
        internal const string EnemyContentRoot = "Assets/EnemyContent";

        // Address prefix — MUST match DeNelle.Core.EnemyAssetLoader.EnemyAddrPrefix.
        internal const string EnemyAddrPrefix = "Enemies/";

        // Group names (one bundle each).
        internal const string BlinkGroup       = "Enemy_Blink";
        internal const string ModelsGroup      = "Enemy_Models";
        internal const string ControllersGroup = "Enemy_Controllers";
        internal const string TexturesGroup    = "Enemy_Textures";

        // Sub-folder that becomes its own bundle (the 427 MB bulk).
        internal const string BlinkSub = "Blink";

        /// <summary>Root-relative paths that must NEVER move or be marked — a raw Resources.Load
        /// still reaches them (see the KEEP-BEHIND block in the file header, with call sites).</summary>
        internal static readonly string[] KeepBehind =
        {
            "EnemyVfxSet_Default.asset",   // EnemyTypeVfxLibrary.cs:111 (raw Resources.Load)
            "VfxSets",                     // EnemyTypeVfxLibrary.cs:104 (raw Resources.Load, per-family)
            "OrcTex",                      // EnemyRigColorRegression.cs:171 (raw Resources.Load<Texture>)
            "TripoTex",                    // EnemyFactory.cs:487 — kept with OrcTex (two-rung probe)
        };

        // Texture extensions we mark explicitly (everything else rides as an implicit dependency).
        private static readonly string[] TextureExts =
        {
            ".png", ".tga", ".jpg", ".jpeg", ".psd", ".tif", ".tiff", ".exr", ".bmp"
        };

        // ── Entry points ────────────────────────────────────────────────────────

        [MenuItem("Defenders/Build/Group + Migrate Enemies")]
        public static void GroupAndMigrateEnemies()
        {
            // Same ordering rationale as HeroAddressablesGrouper: migrate FIRST so the
            // grouping pass reads (and addresses) the assets at their FINAL, non-Resources
            // location — no second pass needed to re-point entries after a move.
            MigrateEnemiesOutOfResources();
            GroupEnemies();
        }

        [MenuItem("Defenders/Build/Group Enemies Addressable")]
        public static void GroupEnemies()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[EnemyAddressablesGrouper] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null) — Addressables not " +
                    "initialised. Nothing grouped. EnemyAssetLoader keeps using the Resources fallback.");
                return;
            }

            int marked = 0, already = 0, dupes = 0, roots = 0;
            // (address|type) pairs already claimed. A prefab + a controller may share an address
            // (type disambiguates at load); two assets of the SAME type at one address is an
            // Addressables BUILD ERROR — first-seen wins, the rest warn-and-skip.
            var seenAddr = new HashSet<string>(StringComparer.Ordinal);

            foreach (string root in ActiveRoots())
            {
                roots++;
                foreach (string path in EnumerateGroupableAssets(root))
                {
                    string rel = RootRelative(root, path);
                    if (string.IsNullOrEmpty(rel)) continue;

                    string groupName = GroupFor(rel);
                    if (groupName == null) continue; // not a kind we mark (rides as implicit dependency)

                    string address = AddressFor(rel);
                    Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                    string key = address + "|" + (mainType != null ? mainType.Name : "?");
                    if (!seenAddr.Add(key))
                    {
                        dupes++;
                        Debug.LogWarning($"[EnemyAddressablesGrouper] duplicate address+type '{key}' " +
                                         $"({path}) — skipped (first-seen wins; marking both is an " +
                                         "Addressables BUILD ERROR).");
                        continue;
                    }

                    AddressableAssetGroup group = settings.FindGroup(groupName) ?? CreateBundledGroup(settings, groupName);
                    if (group == null)
                    {
                        Debug.LogWarning($"[EnemyAddressablesGrouper] could not create group '{groupName}' — skipping {path}.");
                        continue;
                    }

                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) continue;

                    if (MarkEntry(settings, group, guid, address)) marked++;
                    else already++;
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnemyAddressablesGrouper] Grouped enemies from {roots} root(s): marked {marked} entr(ies) " +
                      $"Addressable across '{BlinkGroup}'/'{ModelsGroup}'/'{ControllersGroup}'/'{TexturesGroup}' " +
                      $"({already} already at address, {dupes} duplicate-address skipped). " +
                      "Keep-behind (EnemyVfxSet_Default / VfxSets / OrcTex / TripoTex) deliberately NOT marked.");
        }

        [MenuItem("Defenders/Build/Migrate Enemies Out Of Resources")]
        public static void MigrateEnemiesOutOfResources()
        {
            if (!AssetDatabase.IsValidFolder(EnemiesRoot))
            {
                Debug.LogWarning($"[EnemyAddressablesGrouper] '{EnemiesRoot}' not found — nothing to migrate " +
                                 "(already migrated?).");
                return;
            }

            EnsureFolder(EnemyContentRoot);
            int moved = 0, already = 0, failed = 0, kept = 0;

            // Sub-folders first (Blink/ is the 427 MB win; *.fbm/ must travel with their FBX;
            // Materials/ and textures/ move so a Resources material cannot drag a moved texture
            // back into the .data). OrcTex/, TripoTex/, VfxSets/ are keep-behind.
            foreach (string folder in TopLevelFolders(EnemiesRoot))
            {
                string leaf = Path.GetFileName(folder);
                if (IsKeepBehind(leaf)) { kept++; continue; }
                moved += TryMove(folder, $"{EnemyContentRoot}/{leaf}", ref already, ref failed);
            }

            // Then the loose top-level files (fbx / controller / prefab / mat / json / png / asset).
            foreach (string file in TopLevelFiles(EnemiesRoot))
            {
                string leaf = Path.GetFileName(file);
                if (IsKeepBehind(leaf)) { kept++; continue; }
                moved += TryMove(file, $"{EnemyContentRoot}/{leaf}", ref already, ref failed);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyAddressablesGrouper] Migration: moved {moved} item(s) to '{EnemyContentRoot}' " +
                      $"({already} already there, {failed} failed, {kept} kept behind on purpose). " +
                      "Kept in Resources: EnemyVfxSet_Default.asset, VfxSets/, OrcTex/, TripoTex/ — each is " +
                      "still reached by a RAW Resources.Load (see the file header for file:line).");
            if (failed > 0)
                Debug.LogWarning("[EnemyAddressablesGrouper] one or more moves FAILED — see the MoveAsset warnings " +
                                 "above; the payload win is INCOMPLETE until every non-keep-behind enemy asset " +
                                 "leaves Resources (anything left over still force-ships AND double-ships).");
        }

        // ── Address / grouping rules ────────────────────────────────────────────

        /// <summary>The address for a root-relative path: the extension-less, Resources-relative
        /// key, used verbatim by EnemyAssetLoader. Computed from the path — never a table.</summary>
        internal static string AddressFor(string rootRelativePath)
        {
            if (string.IsNullOrEmpty(rootRelativePath)) return null;
            string rel = rootRelativePath.Replace('\\', '/');
            int dot = rel.LastIndexOf('.');
            int slash = rel.LastIndexOf('/');
            if (dot > slash) rel = rel.Substring(0, dot);
            return EnemyAddrPrefix + rel;
        }

        /// <summary>Which bundle a root-relative asset belongs to, or null when it should NOT be
        /// marked (it rides as an implicit dependency of whatever references it).</summary>
        internal static string GroupFor(string rootRelativePath)
        {
            string rel = rootRelativePath.Replace('\\', '/');
            string ext = Path.GetExtension(rel).ToLowerInvariant();

            bool isBlink = rel.StartsWith(BlinkSub + "/", StringComparison.OrdinalIgnoreCase);
            if (isBlink)
            {
                // Prefabs / meshes / controllers only. Blink/Textures + Blink/Materials ride as
                // implicit dependencies of this same bundle (one encounter, one bundle).
                if (ext == ".prefab" || ext == ".fbx" || ext == ".controller") return BlinkGroup;
                return null;
            }

            if (ext == ".controller") return ControllersGroup;
            if (ext == ".prefab" || ext == ".fbx") return ModelsGroup;
            if (Array.IndexOf(TextureExts, ext) >= 0) return TexturesGroup;
            return null; // .mat / .json / .asset / .anim — referenced by GUID, pulled as dependencies
        }

        /// <summary>True when the root-relative path is (or lives under) a keep-behind entry.</summary>
        internal static bool IsKeepBehind(string rootRelativePath)
        {
            if (string.IsNullOrEmpty(rootRelativePath)) return false;
            string rel = rootRelativePath.Replace('\\', '/');
            foreach (string keep in KeepBehind)
            {
                if (string.Equals(rel, keep, StringComparison.OrdinalIgnoreCase)) return true;
                if (rel.StartsWith(keep + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // ── Enumeration helpers ─────────────────────────────────────────────────

        /// <summary>Returns the migrated root (Assets/EnemyContent) when it already holds enemy
        /// assets, else the pre-migration Resources root. Lets grouping run correctly before OR
        /// after a move (mirrors HeroAddressablesGrouper.ResolveActiveRoot).</summary>
        internal static string ResolveActiveRoot()
        {
            if (AssetDatabase.IsValidFolder(EnemyContentRoot))
            {
                foreach (string _ in EnumerateGroupableAssets(EnemyContentRoot)) return EnemyContentRoot;
            }
            return EnemiesRoot;
        }

        /// <summary>The active root, plus the other root when a PARTIAL migration left groupable
        /// assets in both places (so a half-finished move still gets addressed correctly).</summary>
        internal static IEnumerable<string> ActiveRoots()
        {
            string primary = ResolveActiveRoot();
            yield return primary;

            string other = primary == EnemyContentRoot ? EnemiesRoot : EnemyContentRoot;
            if (!AssetDatabase.IsValidFolder(other)) yield break;
            foreach (string _ in EnumerateGroupableAssets(other)) { yield return other; yield break; }
        }

        /// <summary>Every asset under <paramref name="root"/> that is a candidate for marking:
        /// skips keep-behind, skips *.fbm/ embedded-texture folders (they ride with their FBX),
        /// skips folders and .meta.</summary>
        internal static IEnumerable<string> EnumerateGroupableAssets(string root)
        {
            if (!AssetDatabase.IsValidFolder(root)) yield break;

            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;

                string rel = RootRelative(root, path);
                if (string.IsNullOrEmpty(rel)) continue;
                if (IsKeepBehind(rel)) continue;
                if (rel.IndexOf(".fbm/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (GroupFor(rel) == null) continue;

                yield return path;
            }
        }

        /// <summary>Path relative to <paramref name="root"/> (forward slashes), or null when
        /// <paramref name="path"/> is not under it.</summary>
        internal static string RootRelative(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path)) return null;
            string p = path.Replace('\\', '/');
            string r = root.Replace('\\', '/').TrimEnd('/') + "/";
            if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return null;
            return p.Substring(r.Length);
        }

        /// <summary>Immediate sub-folder asset paths of <paramref name="root"/> (forward slashes).</summary>
        private static IEnumerable<string> TopLevelFolders(string root)
        {
            var list = new List<string>(AssetDatabase.GetSubFolders(root));
            foreach (string f in list) yield return f.Replace('\\', '/');
        }

        /// <summary>Immediate file asset paths of <paramref name="root"/> (no .meta, forward slashes).</summary>
        private static IEnumerable<string> TopLevelFiles(string root)
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), root);
            if (!Directory.Exists(abs)) yield break;

            var names = new List<string>();
            foreach (string file in Directory.GetFiles(abs))
            {
                string leaf = Path.GetFileName(file);
                if (leaf.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                names.Add(leaf);
            }
            names.Sort(StringComparer.Ordinal);
            foreach (string leaf in names) yield return root + "/" + leaf;
        }

        // ── Move / mark helpers (mirrors HeroAddressablesGrouper) ───────────────

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
            Debug.LogWarning($"[EnemyAddressablesGrouper] MoveAsset '{src}' -> '{dst}' FAILED: {err}");
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
        /// made; false when already at this exact address (idempotent — re-running never churns
        /// addresses). Mirrors HeroAddressablesGrouper.MarkEntry.</summary>
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
