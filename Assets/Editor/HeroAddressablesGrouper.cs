// =============================================================================
// HeroAddressablesGrouper — moves the per-hero body assets OUT of Resources and
// files them into LOCAL per-hero Addressable bundles so the ~138 MB of hero
// content stops shipping in the monolithic WebGL.data (WO-545 Tier-1,
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md).
// -----------------------------------------------------------------------------
// Assets/Resources/Heroes previously shipped in EVERY build because Resources is
// force-included. This tool is the GROUPING + MIGRATION half of the seam
// (HeroAssetLoader + HeroTextureLoader are the CODE half). Two things must both be
// true for the bytes to leave WebGL.data:
//   1. the assets are marked Addressable (so they ride in a bundle), AND
//   2. the assets no longer live under any Resources/ folder (else they double-ship
//      — once in the Resources block of the .data AND once in the bundle).
//
// TOPOLOGY produced:
//   • per-hero group  "Hero_<slug>"   — <slug>.fbx (address "Heroes/<slug>") +
//                                        <slug>.controller (same address, type-disambiguated).
//                                        The FBX's .fbm embedded textures ride as
//                                        implicit dependencies of this bundle.
//   • shared group    "Hero_Textures" — every atlas under Heroes/Textures at address
//                                        "Heroes/Textures/<name>" (the exact key
//                                        HeroTextureLoader queries). One shared bundle:
//                                        84 MB raw / <100 MB after the 2K+compressed
//                                        import caps → satisfies the Vercel per-file limit.
//   ⚠ ALL Hero_ GROUPS ARE **REMOTE** AS OF WO-1187 (2026-09-03) — the header above
//   describing LOCAL groups was the WO-545 design and is RETIRED. They now bind
//   Remote.BuildPath ('ServerData/[BuildTarget]') + Remote.LoadPath (the R2 bucket),
//   identical to the live Enemy_Art / Structure_Art groups, so the bytes are written
//   for the R2 push and NOT into StreamingAssets inside the APK. A LOCAL Hero_ group
//   is a silent regression: it ships the whole 94 MB in the initial download again
//   while every marker stays green. PointGroupAtRemote() re-asserts this on every run.
//
//   ⚠ AND THE ORDER FIX IS THE OTHER HALF: HeroAssetLoader called Resources.Load
//   BEFORE its Addressables probe, so running this tool WITHOUT that fix changed
//   nothing at all. Both halves or neither (fixed in WO-1187).
//
// MIGRATION target = Assets/HeroContent/ (NOT under Resources). Moved via
// AssetDatabase.MoveAsset (GUID- and .meta-preserving → the 2K+compressed import
// settings written by WebGLTextureOptimizer travel with the asset, so the bundle
// stays small; scene/prefab references by GUID do not break). Kept behind in
// Resources/Heroes: Props/ (gear + bow load "Heroes/Props/*" via Resources),
// Materials-adjacent *_tex/ (tiny), and SC_*.prefab (troops load "Heroes/SC_*").
//
// ⚠ WEBGL SYNC/ASYNC GATE (see WO-545 RESULT): once assets leave Resources the only
// load path is Addressables, and HeroAssetLoader/HeroTextureLoader use
// WaitForCompletion, which WebGL does NOT support for a bundle that still has to be
// downloaded. Run the MIGRATION only alongside the build check that confirms the
// hero bundle is warmed async before the sync load (or after the loaders go async).
// GroupHeroes()/GroupHeroTextures() are mark-only and safe to run any time.
//
// Run (menu): Defenders > Build > Group Heroes Addressable  (mark only, no move)
//             Defenders > Build > Migrate Heroes Out Of Resources (move only)
//             Defenders > Build > Group + Migrate Heroes (WO-545) (one-shot)
//   headless: -executeMethod DeNelle.Editor.HeroAddressablesGrouper.GroupAndMigrateHeroes
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
    /// <summary>Groups + migrates the Resources/Heroes body assets into LOCAL per-hero
    /// Addressable bundles (WO-545). Mark-only, migrate-only, and one-shot entry points;
    /// idempotent + guarded for the no-Addressables-settings case.</summary>
    public static class HeroAddressablesGrouper
    {
        // Pre-migration flat folder (the shipped location today).
        internal const string HeroesRoot = "Assets/Resources/Heroes";

        // Post-migration destination (NOT under any Resources/ folder).
        internal const string HeroContentRoot = "Assets/HeroContent";

        // Address prefixes — MUST match DeNelle.Core.HeroAssetLoader.HeroAddrPrefix and the
        // "Heroes/Textures/<name>" keys DeNelle.Core.HeroTextureLoader is called with.
        internal const string HeroAddrPrefix = "Heroes/";
        internal const string TexAddrPrefix  = "Heroes/Textures/";

        // Per-hero group name prefix (one bundle per hero) + the shared textures group.
        internal const string GroupPrefix    = "Hero_";
        internal const string SharedTexGroup = "Hero_Textures";

        // Textures sub-folder name (under whichever root is active).
        internal const string TexturesSub = "Textures";

        // ⛔ SLUGS THAT MUST STAY IN Resources/Heroes — DO NOT MIGRATE, DO NOT GROUP.
        //
        // WHY THIS SET EXISTS (2026-09-03, the reconciliation after WO-1187's first run):
        // the migration moved Knight.fbx to HeroContent and DataRegression went red with a real
        // break — 'troop-shieldguard' and 'troop-echo-legionnaire' both carry model "Knight" in
        // troops.json, and TroopFactory resolves a troop BODY through
        // VisualFactory.Skin(host, "Heroes/Knight", ...) -> StructureAssetLoader, whose
        // Addressables tier is the structure RESIDENT CACHE plus an EDITOR-ONLY synchronous probe.
        // In a player build that path has no Addressables arm at all: it lands on
        // Resources.Load("Heroes/Knight"), gets null, and the troop deploys as a tinted CAPSULE.
        // That is NOT the HeroAssetLoader seam — TroopFactory never calls it.
        //
        // The .controller half is the same story and was already known (see the long note in
        // GroupHeroes): TroopFactory Resources.Loads "Heroes/<cand>" for the animator directly.
        //
        // So Knight is BODY-local as well as CONTROLLER-local, and the cost is bounded: Knight.fbx
        // (1.3 MB) + Knight.fbm (11.5 MB of embedded atlases) + the five controllers (0.3 MB).
        // The ~81 MB that DOES leave is KnightV3 / knightV2 / Mage / Ranger + Heroes/Textures,
        // none of which any Resources-resolved call site asks for.
        //
        // ⚠ AND THE OTHER HALF OF KEEPING IT LOCAL: a kept-local slug must NOT also be registered
        // in an Addressables group. An asset present in Resources AND in a bundle SHIPS TWICE —
        // the build gets BIGGER, not smaller, and every marker stays green while it happens
        // (CLAUDE.md Sec. 16). Both loops below consult this set for exactly that reason, and
        // HeroRemoteContentRegression group 6 [no-double-ship] pins it by GUID.
        //
        // If a future WO wants Knight remote too, it must re-point TroopFactory's BODY and
        // CONTROLLER lookups through an Addressables-first seam in the SAME change — never one
        // without the other.
        internal static readonly HashSet<string> KeepLocalSlugs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Knight" };

        // REMOTE profile variable names (WO-1187). These are the SAME two variables the live
        // Enemy_Art / Structure_Art groups bind, i.e. the existing R2 pipeline (CLAUDE.md §16):
        //   Remote.BuildPath = 'ServerData/[BuildTarget]'
        //   Remote.LoadPath  = 'https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]'
        // Bind BY NAME, never by the raw profile id — the ids are settings-asset-local.
        internal const string RemoteBuildPathVar = "Remote.BuildPath";
        internal const string RemoteLoadPathVar  = "Remote.LoadPath";

        // ── Entry points ────────────────────────────────────────────────────────

        [MenuItem("Defenders/Build/Group + Migrate Heroes (WO-545)")]
        public static void GroupAndMigrateHeroes()
        {
            // Order: migrate FIRST (so grouping reads from the final, non-Resources location),
            // then group the FBX/controller + textures at that location.
            MigrateHeroesOutOfResources();
            GroupHeroes();
            GroupHeroTextures();
        }

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

            string root = ResolveActiveRoot();
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] heroes root '{root}' not found — nothing grouped.");
                return;
            }

            int prefabs = 0, controllers = 0, skipped = 0, heroes = 0;

            foreach (string slug in EnumerateHeroSlugs(root))
            {
                // See KeepLocalSlugs: this hero ships from Resources, so grouping it here would
                // put the SAME asset in the APK and in an R2 bundle at once (double-ship).
                if (KeepLocalSlugs.Contains(slug))
                {
                    Debug.Log($"[HeroAddressablesGrouper] slug '{slug}' is KEEP-LOCAL — not grouped " +
                              "(it ships from Resources; grouping it too would double-ship it).");
                    skipped++;
                    continue;
                }

                string groupName = GroupPrefix + slug;
                AddressableAssetGroup group = settings.FindGroup(groupName) ?? CreateBundledGroup(settings, groupName);
                if (group == null)
                {
                    Debug.LogWarning($"[HeroAddressablesGrouper] could not create group '{groupName}' — skipping {slug}.");
                    continue;
                }
                // Re-assert Remote even on a pre-existing group: an earlier run of this tool created
                // LOCAL groups, and a LOCAL Hero_ group silently ships the bytes inside the APK again.
                PointGroupAtRemote(settings, group);

                string address = HeroAddrPrefix + slug;
                heroes++;

                // Body prefab (<slug>.fbx imports as a GameObject).
                string fbxGuid = AssetDatabase.AssetPathToGUID($"{root}/{slug}.fbx");
                if (!string.IsNullOrEmpty(fbxGuid))
                {
                    if (MarkEntry(settings, group, fbxGuid, address)) prefabs++;
                    else skipped++;
                }
                else
                {
                    Debug.LogWarning($"[HeroAddressablesGrouper] no body prefab '{root}/{slug}.fbx' for '{slug}'.");
                }

                // ⚠ THE ANIMATOR CONTROLLER IS DELIBERATELY *NOT* GROUPED AND *NOT* MOVED (WO-1187).
                // Assets/_Modules/Village/Troops/TroopFactory.cs:465 hard-adds "Knight" to its
                // candidate list and loads it with a RAW Resources.Load<RuntimeAnimatorController>
                // ("Heroes/" + cand) that does not go through HeroAssetLoader. Moving the .controller
                // out of Resources therefore returns null there and every troop silently loses its
                // animation. The controllers are ~275 KB TOTAL, so keeping them local costs nothing
                // against the ~94 MB of fbx/fbm/atlases that DOES leave — and it is verified that no
                // controller references Knight/Ranger/Mage.fbx, so keeping them local does not drag
                // the moved bodies back into the build as clip dependencies.
                // If a future WO wants them remote too, route TroopFactory through HeroAssetLoader
                // in the SAME change — never one without the other.
                controllers += 0;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HeroAddressablesGrouper] Grouped {heroes} hero(es) from '{root}': marked {prefabs} prefab + " +
                      $"{controllers} controller entr(ies) Addressable ({skipped} already addressed/skipped).");
        }

        [MenuItem("Defenders/Build/Group Hero Textures Addressable")]
        public static void GroupHeroTextures()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[HeroAddressablesGrouper] Addressable settings null — hero textures NOT grouped.");
                return;
            }

            string texFolder = ResolveActiveRoot() + "/" + TexturesSub;
            if (!AssetDatabase.IsValidFolder(texFolder))
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] textures folder '{texFolder}' not found — nothing grouped.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(SharedTexGroup) ?? CreateBundledGroup(settings, SharedTexGroup);
            if (group == null)
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] could not create '{SharedTexGroup}' group — nothing grouped.");
                return;
            }
            PointGroupAtRemote(settings, group); // see GroupHeroes — never leave a Hero_ group LOCAL.

            int marked = 0, skipped = 0, dupes = 0;
            var seenAddr = new HashSet<string>(StringComparer.Ordinal);

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                // Address = the extension-less Resources-relative key the loader queries.
                string address = TexAddrPrefix + Path.GetFileNameWithoutExtension(path);
                if (!seenAddr.Add(address))
                {
                    // Two files share a base name (e.g. knight_basecolor.JPEG/.PNG). Marking both at
                    // one address is an Addressables build error — keep the first, warn on the rest.
                    dupes++;
                    Debug.LogWarning($"[HeroAddressablesGrouper] duplicate texture address '{address}' " +
                                     $"({path}) — skipped (first-seen wins; the runtime never loads this dupe).");
                    continue;
                }
                if (MarkEntry(settings, group, guid, address)) marked++;
                else skipped++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HeroAddressablesGrouper] Hero textures: marked {marked} in '{SharedTexGroup}' " +
                      $"({skipped} already addressed, {dupes} duplicate-name skipped) from '{texFolder}'.");
        }

        [MenuItem("Defenders/Build/Migrate Heroes Out Of Resources")]
        public static void MigrateHeroesOutOfResources()
        {
            if (!AssetDatabase.IsValidFolder(HeroesRoot))
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] '{HeroesRoot}' not found — nothing to migrate " +
                                 "(already migrated?).");
                return;
            }

            EnsureFolder(HeroContentRoot);
            int moved = 0, already = 0, failed = 0;

            // Move the per-hero fbx / controller / .fbm for every slug still in Resources.
            foreach (string slug in EnumerateHeroSlugs(HeroesRoot))
            {
                // fbx + its embedded-texture .fbm folder move. The .controller STAYS in Resources —
                // see the long note in GroupHeroes(): TroopFactory.cs:465 Resources.Loads "Heroes/Knight"
                // directly, and that file is out of scope for this WO.
                // See KeepLocalSlugs: moving this body out from under TroopFactory's raw
                // Resources.Load is the break that turned every "Knight"-model troop into a capsule.
                if (KeepLocalSlugs.Contains(slug))
                {
                    Debug.Log($"[HeroAddressablesGrouper] slug '{slug}' is KEEP-LOCAL — body + .fbm " +
                              "left in Resources (TroopFactory resolves it by raw Resources path).");
                    continue;
                }

                moved += TryMove($"{HeroesRoot}/{slug}.fbx", $"{HeroContentRoot}/{slug}.fbx", ref already, ref failed);
                moved += TryMove($"{HeroesRoot}/{slug}.fbm", $"{HeroContentRoot}/{slug}.fbm", ref already, ref failed);
            }

            // Move the whole Textures/ folder (the atlases the runtime paints on) and the
            // Materials/ folder (so a Resources material can't drag a moved texture back into
            // the .data). Props/, *_tex/, SC_*.prefab stay in Resources (loaded by Resources path).
            moved += TryMove($"{HeroesRoot}/{TexturesSub}", $"{HeroContentRoot}/{TexturesSub}", ref already, ref failed);
            moved += TryMove($"{HeroesRoot}/Materials",     $"{HeroContentRoot}/Materials",     ref already, ref failed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[HeroAddressablesGrouper] Migration: moved {moved} item(s) to '{HeroContentRoot}' " +
                      $"({already} already there, {failed} failed). Props/ *_tex/ SC_*.prefab kept in Resources.");
            if (failed > 0)
                Debug.LogWarning("[HeroAddressablesGrouper] one or more moves FAILED — see MoveAsset warnings above; " +
                                 "the .data win is incomplete until every hero asset leaves Resources.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns the migrated root (Assets/HeroContent) when it already holds hero FBXs,
        /// else the pre-migration Resources root. Lets grouping run correctly before OR after a move.</summary>
        internal static string ResolveActiveRoot()
        {
            if (AssetDatabase.IsValidFolder(HeroContentRoot))
            {
                foreach (string _ in EnumerateHeroSlugs(HeroContentRoot)) return HeroContentRoot; // has ≥1 fbx
            }
            return HeroesRoot;
        }

        /// <summary>Yields the hero slug for every top-level &lt;slug&gt;.fbx directly under
        /// <paramref name="root"/> (Knight, Ranger, Mage, Cleric). Skips nested folders + SC_ prefabs.</summary>
        internal static IEnumerable<string> EnumerateHeroSlugs(string root)
        {
            if (!AssetDatabase.IsValidFolder(root)) yield break;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { root });
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                // Direct child of root only (e.g. "<root>/Knight.fbx").
                if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), root, StringComparison.OrdinalIgnoreCase))
                    continue;

                string slug = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(slug)) continue;
                if (seen.Add(slug)) yield return slug;
            }
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
            Debug.LogWarning($"[HeroAddressablesGrouper] MoveAsset '{src}' -> '{dst}' FAILED: {err}");
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

        /// <summary>Create a REMOTE bundled group (WO-1187) — Remote.BuildPath / Remote.LoadPath, so the
        /// bundle is written to ServerData/&lt;target&gt;/ for the R2 push instead of into the APK's
        /// StreamingAssets. Mirrors the live 'Enemy_Art' / 'Structure_Art' groups exactly; those are the
        /// reference implementation for hero content and the reason we do NOT build a second content path
        /// (CLAUDE.md §16). Returns null if the group could not be created.</summary>
        private static AddressableAssetGroup CreateBundledGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.CreateGroup(
                groupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));

            if (group == null) return null;
            PointGroupAtRemote(settings, group);
            return group;
        }

        /// <summary>
        /// Bind a group's BundledAssetGroupSchema to the REMOTE profile variables and turn on the
        /// remote-serving flags the shipping Enemy_Art group uses (bundle cache + CRC + retries).
        /// Idempotent, and applied to EXISTING Hero_* groups too — a group created by an earlier run of
        /// this tool was LOCAL, and a local group would quietly put the 100 MB straight back into the APK.
        /// </summary>
        internal static void PointGroupAtRemote(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            if (settings == null || group == null) return;

            var schema = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                Debug.LogWarning($"[HeroAddressablesGrouper] group '{group.Name}' has no BundledAssetGroupSchema " +
                                 "— cannot point it at Remote. It would ship LOCALLY inside the APK.");
                return;
            }

            schema.BuildPath.SetVariableByName(settings, RemoteBuildPathVar);
            schema.LoadPath.SetVariableByName(settings, RemoteLoadPathVar);

            // Parity with Enemy_Art: cache the downloaded bundle, verify it, and retry a flaky fetch
            // twice before the operation is allowed to fail (HeroContentPrewarmer words the failure).
            schema.UseAssetBundleCache = true;
            schema.UseAssetBundleCrc = true;
            schema.RetryCount = 2;
            schema.IncludeInBuild = true;

            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);

            Debug.Log($"[HeroAddressablesGrouper] group '{group.Name}' -> REMOTE " +
                      $"({RemoteBuildPathVar} / {RemoteLoadPathVar}), cache+crc on, retryCount=2.");
        }

        /// <summary>Re-point every existing Hero_* group at Remote (repair path for groups an older,
        /// LOCAL-only run of this tool created). Safe to run any time; changes no asset locations.</summary>
        [MenuItem("Defenders/Build/Repoint Hero Groups To Remote (WO-1187)")]
        public static void RepointHeroGroupsToRemote()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[HeroAddressablesGrouper] Addressable settings null — nothing re-pointed.");
                return;
            }

            int touched = 0;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || group.Name == null) continue;
                if (!group.Name.StartsWith(GroupPrefix, StringComparison.Ordinal)) continue;
                PointGroupAtRemote(settings, group);
                touched++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HeroAddressablesGrouper] Re-pointed {touched} '{GroupPrefix}*' group(s) at Remote.");
        }
    }
}
