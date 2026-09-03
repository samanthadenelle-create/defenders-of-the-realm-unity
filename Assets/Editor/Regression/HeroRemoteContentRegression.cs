// =============================================================================
// HeroRemoteContentRegression [hero-remote-content] — WO-1187 / hero art to R2.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT THIS PINS. ~100 MB of hero art (5 root .fbx, their .fbm sidecars and the
// Heroes/Textures atlases) shipped inside the initial download because BOTH hero
// loaders resolved Resources BEFORE Addressables. HeroAssetLoader's own header
// claimed "Addressables-FIRST, Resources-FALLBACK" while its code called
// Resources.Load<T> first and only then probed the catalog — so grouping a hero
// into a REMOTE Addressables group was a guaranteed NO-OP: the local copy always
// won, the CDN copy was dead weight, and nothing anywhere failed. That is the
// silent-success shape CLAUDE.md Sec. 16 exists to catch.
//
// Groups:
//   1 [order]             In BOTH hero loaders the first Addressables resolve/probe
//                         must appear BEFORE the first Resources.Load<> in the CODE.
//                         ⚠ THIS IS A SOURCE-ORDER ORACLE ON PURPOSE. The bug IS the
//                         statement order, and no runtime assertion can tell
//                         "Addressables first" from "Resources first" once BOTH
//                         resolve — both return a hero and the caller cannot see
//                         which arm produced it. The only observable difference is
//                         download size, which no editor test can measure. So the
//                         statement order is the invariant, and the source is where
//                         it lives. Read CODE ONLY (comments + string-literal
//                         contents blanked), because HeroAssetLoader's header
//                         paragraph NAMES Resources.Load at code-line 21 — a raw-text
//                         lint would score the header as the first call and pass on
//                         every day the defect shipped.
//   2 [resources-purged]  No hero art left under Assets/Resources/Heroes: no .fbx, no
//                         file inside a *.fbm sidecar folder, no texture. An ALLOWLIST
//                         (below, each entry with its reason) covers what DELIBERATELY
//                         stays local — moving those would break live Resources.Load
//                         call sites that have nothing to do with hero bodies.
//   3 [remote]            Every Hero_* Addressables group binds BuildPath/LoadPath to
//                         the REMOTE profile variables, copying the live Enemy_Art
//                         reference implementation. A Hero_ group left on Local.* is
//                         the whole defect wearing a different hat: the bytes ship in
//                         the APK exactly as before, and every marker stays green.
//                         NO Hero_ group at all FAILS — "the migration never ran" is
//                         precisely the state we want caught, not skipped.
//   4 [addresses]         Every <slug>.fbx under Assets/HeroContent has a catalog entry
//                         at address "Heroes/<slug>" — the address HeroAssetLoader
//                         builds (HeroAddrPrefix + slug). Editor-side catalog presence
//                         only; no download is asserted. An absent/empty HeroContent
//                         FAILS rather than passing vacuously.
//   5 [hygiene]           No embedded NUL in the touched sources (CLAUDE.md Sec. 0).
//   6 [no-double-ship]    NOTHING under Assets/Resources/Heroes is ALSO registered in an
//                         Addressables group, and nothing under Assets/Resources/Heroes
//                         DEPENDS on an addressed asset in Assets/HeroContent. Compared BY
//                         GUID, never by filename.
//                         WHY THIS GROUP EXISTS - 2026-09-03. The migration moved Knight.fbx
//                         out, DataRegression went red (troops deployed as capsules - see the
//                         allowlist note below), and the fix was to move the file BACK. Its
//                         .meta travelled with it, so its GUID never changed, so the
//                         Addressables entry the migration had created STILL RESOLVED TO IT:
//                         the asset was in Resources AND in a remote bundle at once. That
//                         state SHIPS THE BYTES TWICE - the build gets BIGGER, not smaller -
//                         while every marker stays green, which is the exact silent-success
//                         shape CLAUDE.md Sec. 16 records five incidents of.
//                         Group 2 CANNOT catch it: group 2 asks "is it still local?", and an
//                         allowlisted file is legitimately local. Only "local AND addressed"
//                         is the bug, and that is a different question, so it is its own group.
//                         The dependency arm is scoped to Assets/HeroContent deps on purpose -
//                         a Resources material pointing at a migrated atlas force-includes that
//                         atlas into the player (Resources pulls its whole dependency closure),
//                         which is the same double-ship one indirection out. Two orphan
//                         materials were doing exactly that when this group was written.
//
// Markers: HERO_REMOTE_CONTENT_OK / HERO_REMOTE_CONTENT_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.HeroRemoteContentRegression.RunAll
// Registered in DataRegression.RunAll as the "hero-remote-content suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class HeroRemoteContentRegression
    {
        private const string AssetLoaderSrc = "Assets/_Modules/Core/Addressables/HeroAssetLoader.cs";
        private const string TexLoaderSrc   = "Assets/_Modules/Core/Addressables/HeroTextureLoader.cs";

        private const string HeroResourcesRoot = "Assets/Resources/Heroes";
        private const string HeroContentRoot   = "Assets/HeroContent";

        /// <summary>The address prefix HeroAssetLoader builds (HeroAssetLoader.HeroAddrPrefix).
        /// Spelled literally here so a rename of the const fails this suite loudly instead of
        /// silently re-pointing the oracle at whatever the new prefix happens to be.</summary>
        private const string HeroAddrPrefix = "Heroes/";

        /// <summary>Group-name prefix the migration uses (Hero_Art, Hero_Textures, ...).</summary>
        private const string HeroGroupPrefix = "Hero_";

        /// <summary>Hero slugs whose BODY deliberately still ships from Resources. Kept in step
        /// with HeroAddressablesGrouper.KeepLocalSlugs; the long reason is in IsDeliberatelyLocal.
        /// Spelled here rather than read off the grouper so this oracle does not inherit whatever
        /// that tool happens to believe today - the two agreeing independently is the point.</summary>
        private static readonly string[] LocalHeroBodySlugs = { "Knight" };

        /// <summary>The live reference implementation group 3 copies its check from.</summary>
        private const string ReferenceGroup = "Enemy_Art";

        // The REMOTE profile variable names, from AddressableAssetSettings.asset
        // m_ProfileEntryNames. Enemy_Art's BundledAssetGroupSchema binds exactly these:
        //   Remote.BuildPath -> 'ServerData/[BuildTarget]'
        //   Remote.LoadPath  -> 'https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]'
        private const string RemoteBuildVar = "Remote.BuildPath";
        private const string RemoteLoadVar  = "Remote.LoadPath";

        // Raw profile ids for the same two variables — the FALLBACK comparison only, used
        // when ProfileValueReference.GetName() cannot resolve a name (a schema pointing at a
        // deleted variable returns null). Names are preferred because an id is opaque and
        // survives a rename that a human reading the group would call a change.
        private const string RemoteBuildId = "ad0e68328bd7fd54ea79f0a9ab1dd9b1";
        private const string RemoteLoadId  = "cf151d4962873af43b9302d323a9d707";

        // Texture extensions that count as "hero art still shipping locally". Extension-based
        // on purpose: it needs no asset import and gives the same answer in a cold batchmode
        // process as in a warm editor.
        private static readonly string[] TextureExts =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".bmp", ".gif"
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_REMOTE_CONTENT_OK - " + reason);
            else Debug.LogError("HERO_REMOTE_CONTENT_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                CheckResolutionOrder(failures);
                CheckResourcesPurged(failures);
                CheckHeroGroupsRemote(failures);
                CheckHeroAddressesResolve(failures);
                CheckHygiene(failures);
                CheckNoDoubleShip(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
                return false;
            }
            reason = "hero art is genuinely remote: both loaders resolve Addressables BEFORE " +
                     "Resources (so a grouped hero is actually consulted), Assets/Resources/Heroes " +
                     "holds no .fbx / .fbm / texture outside the documented local allowlist, every " +
                     "Hero_* group binds Remote.BuildPath + Remote.LoadPath like Enemy_Art, and " +
                     "every hero .fbx in Assets/HeroContent has a catalog entry at " +
                     HeroAddrPrefix + "<slug>. The deliberately-local set (" +
                     string.Join("/", LocalHeroBodySlugs) + " body + the controllers + " +
                     "Props/ Emotes/ SC_*.prefab) is local ONLY - not one of those assets is " +
                     "also registered in an Addressables group, and nothing local depends on " +
                     "a migrated asset, so no byte ships twice.";
            return true;
        }

        // -- 1 [order] --------------------------------------------------------
        // The whole defect in one assertion: index(first Addressables probe) <
        // index(first Resources.Load<). See the header for why this is a source oracle.
        private static void CheckResolutionOrder(List<string> failures)
        {
            AssertAddressablesFirst(AssetLoaderSrc, failures);
            AssertAddressablesFirst(TexLoaderSrc, failures);
        }

        private static void AssertAddressablesFirst(string path, List<string> failures)
        {
            string code = ReadCode(path, failures);
            if (code == null) return;

            int resourcesAt = code.IndexOf("Resources.Load<", StringComparison.Ordinal);

            // "First Addressables resolve/probe" = the EARLIER of the catalog probe and the
            // load call. Taking the minimum (rather than only LoadAssetAsync) means the
            // assertion follows the whole Addressables block wherever it moves, and cannot be
            // satisfied by hoisting the load call above a probe that still sits under Resources.
            int loadAt  = code.IndexOf("Addressables.LoadAssetAsync", StringComparison.Ordinal);
            int probeAt = code.IndexOf("AddressableRegistered", StringComparison.Ordinal);
            int addrAt = MinPresent(loadAt, probeAt);

            if (resourcesAt < 0 && addrAt < 0)
            {
                failures.Add("[order] " + path + " calls NEITHER Resources.Load<> nor Addressables - " +
                             "it can no longer load a hero at all, so no ordering exists to pin. " +
                             "If the loader was renamed or split, re-point this suite's source " +
                             "constants at the file that now owns hero resolution.");
                return;
            }
            if (addrAt < 0)
            {
                failures.Add("[order] " + path + " has NO Addressables resolve or probe left (no " +
                             "Addressables.LoadAssetAsync, no AddressableRegistered) - every hero " +
                             "would come from the local Resources copy, which is the 100 MB the " +
                             "remote migration exists to remove.");
                return;
            }
            if (resourcesAt < 0) return; // Addressables-only: strictly stronger than the rule.

            if (resourcesAt < addrAt)
                failures.Add("[order] " + path + " resolves Resources BEFORE Addressables " +
                             "(Resources.Load< at code-offset " + resourcesAt + ", first Addressables " +
                             "resolve/probe at code-offset " + addrAt + "). The local copy therefore " +
                             "ALWAYS wins and a hero grouped into a remote Addressables group is " +
                             "never consulted - so the bundle ships to R2 AND the art ships in the " +
                             "initial download, with no error anywhere. Move the Addressables " +
                             "Guard.Try block above the Resources.Load call; the header of this " +
                             "file has claimed Addressables-first since WO-545.");
        }

        // -- 2 [resources-purged] ---------------------------------------------
        private static void CheckResourcesPurged(List<string> failures)
        {
            if (!Directory.Exists(HeroResourcesRoot)) return; // fully purged - the goal state.

            var offenders = new List<string>();
            try
            {
                foreach (var raw in Directory.GetFiles(HeroResourcesRoot, "*", SearchOption.AllDirectories))
                {
                    string p = raw.Replace('\\', '/');
                    if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsDeliberatelyLocal(p)) continue;
                    string why = OffendingReason(p);
                    if (why != null) offenders.Add(p + " (" + why + ")");
                }
            }
            catch (Exception ex)
            {
                failures.Add("[resources-purged] scan of " + HeroResourcesRoot + " failed: " + ex.Message);
                return;
            }

            if (offenders.Count == 0) return;

            offenders.Sort(StringComparer.OrdinalIgnoreCase);
            int shown = Math.Min(offenders.Count, 12);
            var sb = new StringBuilder();
            sb.Append("[resources-purged] ").Append(offenders.Count)
              .Append(" hero art file(s) still ship in the initial download from ")
              .Append(HeroResourcesRoot)
              .Append(". Everything under Assets/Resources is packed into the player " +
                      "UNCONDITIONALLY - reachability is irrelevant - so these bytes are in the APK " +
                      "even when the same asset is also served from R2. Move them to ")
              .Append(HeroContentRoot)
              .Append(" and mark them Addressable in a REMOTE Hero_* group (HeroAddressablesGrouper). " +
                      "First ").Append(shown).Append(": ");
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(offenders[i]);
            }
            if (offenders.Count > shown) sb.Append(", ... (+").Append(offenders.Count - shown).Append(" more)");
            failures.Add(sb.ToString());
        }

        /// <summary>Why this file counts as hero art that must not ship locally, or null.</summary>
        private static string OffendingReason(string path)
        {
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return "hero mesh .fbx";

            // A *.fbm folder is the sidecar Unity extracts embedded FBX textures into. Its
            // contents are hero atlases by construction, whatever they are named.
            if (path.IndexOf(".fbm/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "embedded-texture sidecar inside a .fbm folder";

            foreach (var ext in TextureExts)
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return "hero texture atlas (" + ext + ")";

            return null;
        }

        /// <summary>
        /// THE ALLOWLIST — what deliberately stays in Resources, each with its reason. These are
        /// NOT oversights: a live runtime call site does Resources.Load on each of them, so moving
        /// them to Addressables without touching that call site would return null and the asset
        /// would silently vanish in the player. They are also not the 100 MB (the weight is the
        /// root .fbx bodies, their .fbm sidecars and Heroes/Textures).
        /// </summary>
        private static bool IsDeliberatelyLocal(string path)
        {
            // Props/** — weapons and attachments, loaded by literal path:
            // Resources.Load("Heroes/Props/*") from EnemyFactory and HeroBowAttachment.
            // Shared by ENEMIES too, so they are not hero-exclusive content at all.
            if (path.StartsWith(HeroResourcesRoot + "/Props/", StringComparison.OrdinalIgnoreCase))
                return true;

            // Emotes/** — Resources.Load<AnimationClip>("Heroes/Emotes/*"). Animation clips,
            // kilobytes each, and the emote plays the instant it is triggered: a remote fetch
            // here would trade ~0 MB of download for a visible hitch.
            if (path.StartsWith(HeroResourcesRoot + "/Emotes/", StringComparison.OrdinalIgnoreCase))
                return true;

            // SC_*.prefab — the Synty troop bodies TroopFactory resolves by Resources path.
            // Troops are not heroes; they are spawned in bulk during a raid and are not gated
            // behind the hero prewarm, so they stay local.
            string file = path.Substring(path.LastIndexOf('/') + 1);
            if (file.StartsWith("SC_", StringComparison.OrdinalIgnoreCase) &&
                file.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return true;

            // THE KEPT-LOCAL HERO BODY (2026-09-03). This is a NARROWING to a proven
            // constraint, not a softening of group 2 - read the constraint before touching it.
            //
            // troops.json gives 'troop-shieldguard' and 'troop-echo-legionnaire' the model
            // "Knight". TroopFactory resolves a troop BODY as
            // VisualFactory.Skin(host, "Heroes/Knight", ...) -> StructureAssetLoader, whose
            // Addressables tier is the structure RESIDENT CACHE plus an EDITOR-ONLY synchronous
            // probe. In a PLAYER build that path has no Addressables arm at all: it lands on
            // Resources.Load("Heroes/Knight"), gets null, and the troop deploys as a tinted
            // CAPSULE. TroopFactory never calls HeroAssetLoader, so the WO-1187 order fix does
            // not reach it. Migrating Knight.fbx is what turned DataRegression's
            // runtime-spawn-visual suite red, and it was a REAL break, not a stale oracle.
            // The .controller half is the same story (TroopFactory Resources.Loads
            // "Heroes/<cand>" for the animator directly) and is already covered: OffendingReason
            // does not flag .controller files, and the five controllers total ~0.3 MB.
            //
            // Cost of the exception, measured: Knight.fbx 1.3 MB + Knight.fbm/ 11.5 MB of
            // embedded atlases. The ~81 MB that DOES leave is KnightV3 / knightV2 / Mage /
            // Ranger + Heroes/Textures - none of which any Resources-resolved call site asks for.
            //
            // MATCHED EXACTLY, PER SLUG, DELIBERATELY. Not a prefix and not the directory:
            // "Knight" must not also admit KnightV3.fbx or KnightV3.fbm/ (which ARE migrated and
            // ARE remote), and allowing the whole folder would retire group 2 entirely.
            // Mirrors HeroAddressablesGrouper.KeepLocalSlugs, which stops the migration tool
            // moving or grouping the same slug on its next run. Whoever makes Knight remote must
            // re-point TroopFactory's BODY and CONTROLLER lookups in the SAME change, then delete
            // this entry - never one without the other.
            foreach (var slug in LocalHeroBodySlugs)
            {
                if (string.Equals(path, HeroResourcesRoot + "/" + slug + ".fbx",
                                  StringComparison.OrdinalIgnoreCase))
                    return true;
                if (path.StartsWith(HeroResourcesRoot + "/" + slug + ".fbm/",
                                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Any *_tex/ folder — the convention for a deliberately-local texture set kept
            // beside its Resources-loaded prefab. Matched as a whole path SEGMENT so a file
            // merely named "..._tex.png" does not slip through.
            foreach (var seg in path.Split('/'))
                if (seg.EndsWith("_tex", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        // -- 3 [remote] -------------------------------------------------------
        private static void CheckHeroGroupsRemote(List<string> failures)
        {
            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                failures.Add("[remote] no Addressables settings object exists - the hero migration " +
                             "cannot have run. (Deliberately NOT reading " +
                             "AddressableAssetSettingsDefaultObject.Settings here: that property " +
                             "CREATES the settings asset on access, so a probing oracle would " +
                             "author project state it was only supposed to observe.)");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.groups == null)
            {
                failures.Add("[remote] Addressables settings resolved null or has no groups.");
                return;
            }

            int heroGroups = 0;
            bool sawReference = false;
            foreach (var group in settings.groups)
            {
                if (group == null || string.IsNullOrEmpty(group.Name)) continue;
                if (string.Equals(group.Name, ReferenceGroup, StringComparison.Ordinal)) sawReference = true;
                if (!group.Name.StartsWith(HeroGroupPrefix, StringComparison.Ordinal)) continue;
                heroGroups++;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    failures.Add("[remote] group '" + group.Name + "' has no BundledAssetGroupSchema, " +
                                 "so it produces no bundle and its assets are not served from " +
                                 "anywhere. Add the schema and bind it like " + ReferenceGroup + ".");
                    continue;
                }

                AssertRemoteVar(group.Name, "BuildPath", schema.BuildPath, settings,
                                RemoteBuildVar, RemoteBuildId, failures);
                AssertRemoteVar(group.Name, "LoadPath", schema.LoadPath, settings,
                                RemoteLoadVar, RemoteLoadId, failures);
            }

            if (heroGroups == 0)
                failures.Add("[remote] NO Addressables group named " + HeroGroupPrefix + "* exists. The " +
                             "hero art migration has not run, so ~100 MB of hero .fbx/.fbm/atlases is " +
                             "still packed into the initial download. Run " +
                             "Assets/Editor/HeroAddressablesGrouper.cs to move the hero assets to " +
                             HeroContentRoot + " and group them REMOTE. (This is a FAILURE, not a " +
                             "skip, on purpose: 'the migration never ran' is exactly the state this " +
                             "suite exists to catch, and a skip here would have reported green on " +
                             "every day the defect shipped.)");

            if (!sawReference)
                failures.Add("[remote] the reference group '" + ReferenceGroup + "' is gone. This " +
                             "suite's Remote profile-variable expectations were copied from it; if " +
                             "the enemy art migration was restructured, re-derive them before " +
                             "trusting the Hero_* verdict above.");
        }

        /// <summary>
        /// Prefer the resolved profile-variable NAME (Remote.BuildPath / Remote.LoadPath) over the
        /// opaque id: the name is what a human reading the group inspector sees, and comparing it
        /// keeps the assertion readable if the project ever re-creates its profile variables (new
        /// ids, same names). The id compare is the fallback for when GetName cannot resolve.
        /// </summary>
        private static void AssertRemoteVar(string groupName, string label, ProfileValueReference reference,
                                            AddressableAssetSettings settings, string expectedName,
                                            string expectedId, List<string> failures)
        {
            if (reference == null)
            {
                failures.Add("[remote] group '" + groupName + "' has a null " + label +
                             " reference - it is bound to nothing.");
                return;
            }

            string name = null;
            try { name = reference.GetName(settings); }
            catch (Exception ex) { name = null; failures.Add("[remote] group '" + groupName + "' " + label + " GetName threw: " + ex.Message); }

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.Equals(name, expectedName, StringComparison.Ordinal))
                    failures.Add("[remote] group '" + groupName + "' binds " + label + " to profile " +
                                 "variable '" + name + "', expected '" + expectedName + "' (the " +
                                 ReferenceGroup + " binding). A Hero_* group left on Local.* ships " +
                                 "its bundle INSIDE the APK, so the migration moves the files and " +
                                 "changes the download size by zero - and every gate stays green.");
                return;
            }

            string id = reference.Id;
            if (!string.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase))
                failures.Add("[remote] group '" + groupName + "' " + label + " resolved no profile " +
                             "variable NAME and its raw id '" + (id ?? "<null>") + "' is not the " +
                             "Remote id '" + expectedId + "'. Either it points at a deleted variable " +
                             "or it is bound Local - both mean the hero bundle is not served from R2.");
        }

        // -- 6 [no-double-ship] -----------------------------------------------
        // LOCAL *AND* ADDRESSED is the bug. See the group-6 note in the header for the incident.
        private static void CheckNoDoubleShip(List<string> failures)
        {
            if (!Directory.Exists(HeroResourcesRoot)) return; // nothing local - nothing to double.

            // Deliberately NOT reading ...Object.Settings when none exists (that property CREATES
            // the asset). Groups 3/4 already fail loudly on a missing settings object, so this
            // group stays silent rather than adding a second failure for the same cause.
            if (!AddressableAssetSettingsDefaultObject.SettingsExists) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.groups == null)
            {
                // Fixture-absent -> FAIL naming it, never a silent return. An unreadable catalog
                // is indistinguishable from an empty one, and the empty answer here would read as
                // "nothing ships twice" - which is precisely the hollow pass this suite exists to
                // stop shipping.
                failures.Add("[no-double-ship] Addressables settings resolved null or has no " +
                             "groups, so the set of ADDRESSED guids cannot be read and " +
                             "'local AND addressed' cannot be evaluated at all.");
                return;
            }

            // addressed GUID -> "<group> @ '<address>'". Keyed by GUID because that is what an
            // Addressables entry actually stores: a file moved back into Resources carries its
            // .meta, so its GUID is unchanged and the entry silently keeps resolving to it.
            // Comparing filenames would have missed the whole 2026-09-03 incident.
            var addressed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null || group.entries == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.guid)) continue;
                    if (!addressed.ContainsKey(entry.guid))
                        addressed[entry.guid] = group.Name + " @ '" + entry.address + "'";
                }
            }
            if (addressed.Count == 0)
            {
                // Not one entry anywhere in the project. There is no addressed set to intersect
                // with Resources/Heroes, so this group can assert nothing - and an empty
                // intersection would otherwise report green. Group 3 reports the same underlying
                // state ("the migration never ran") from its own angle; a second, differently
                // worded failure costs one log line and removes an empty-set pass.
                failures.Add("[no-double-ship] not one Addressables entry exists in the whole " +
                             "project, so there is no addressed set to intersect with " +
                             HeroResourcesRoot + ". Failing rather than passing on an empty set.");
                return;
            }

            var direct = new List<string>();
            var dragged = new List<string>();
            try
            {
                foreach (var raw in Directory.GetFiles(HeroResourcesRoot, "*", SearchOption.AllDirectories))
                {
                    string p = raw.Replace('\\', '/');
                    if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                    string guid = UnityEditor.AssetDatabase.AssetPathToGUID(p);
                    if (!string.IsNullOrEmpty(guid) && addressed.TryGetValue(guid, out string where))
                        direct.Add(p + " [" + where + "]");

                    // The INDIRECT arm. Resources packs an asset's WHOLE dependency closure into
                    // the player, so a Resources-resident material referencing a migrated atlas
                    // force-includes that atlas too - the same double-ship, one indirection out.
                    // Scoped to Assets/HeroContent dependencies ON PURPOSE: an addressed asset
                    // from an unrelated group (Gear, Structure_Art) reached through a shared
                    // shader or material is a different question, and widening this arm to "any
                    // addressed dependency" would red on shapes this oracle has not reasoned
                    // about. Narrow and true beats broad and noisy.
                    foreach (var dep in UnityEditor.AssetDatabase.GetDependencies(p, true))
                    {
                        string dp = (dep ?? string.Empty).Replace('\\', '/');
                        if (dp.Length == 0) continue;
                        if (string.Equals(dp, p, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!dp.StartsWith(HeroContentRoot + "/", StringComparison.OrdinalIgnoreCase)) continue;

                        string dg = UnityEditor.AssetDatabase.AssetPathToGUID(dp);
                        if (!string.IsNullOrEmpty(dg) && addressed.TryGetValue(dg, out string dwhere))
                            dragged.Add(p + " -> " + dp + " [" + dwhere + "]");
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add("[no-double-ship] scan failed: " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            if (direct.Count > 0)
            {
                direct.Sort(StringComparer.OrdinalIgnoreCase);
                failures.Add("[no-double-ship] " + direct.Count + " asset(s) live under " +
                             HeroResourcesRoot + " AND are registered in an Addressables group, so " +
                             "each one SHIPS TWICE - once in the force-included Resources block and " +
                             "again in the bundle. The build gets BIGGER, not smaller, and no marker " +
                             "goes red. Pick ONE home: either move the file to " + HeroContentRoot +
                             " (remote only), or REMOVE its Addressables entry (Resources only). " +
                             "Offenders: " + JoinOffenders(direct));
            }

            if (dragged.Count > 0)
            {
                dragged.Sort(StringComparer.OrdinalIgnoreCase);
                failures.Add("[no-double-ship] " + dragged.Count + " Resources-resident asset(s) " +
                             "DEPEND on a migrated, addressed asset in " + HeroContentRoot +
                             ". Resources force-includes an asset's whole dependency closure, so the " +
                             "migrated file is pulled back into the player while ALSO being served " +
                             "from R2 - a double-ship one indirection out. Either move the depending " +
                             "asset out of Resources too, or re-point it at a local copy. Offenders: " +
                             JoinOffenders(dragged));
            }
        }

        /// <summary>First 12 offenders, comma-joined, with an honest "+N more" tail.</summary>
        private static string JoinOffenders(List<string> items)
        {
            int shown = Math.Min(items.Count, 12);
            var sb = new StringBuilder();
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i]);
            }
            if (items.Count > shown) sb.Append(", ... (+").Append(items.Count - shown).Append(" more)");
            return sb.ToString();
        }

        // -- 4 [addresses] ----------------------------------------------------
        // Every hero body that has been MOVED must also be REACHABLE at the address the
        // loader builds. Moving the file without registering the address is worse than not
        // moving it: the Resources copy is gone and the catalog has nothing, so the hero
        // renders as nothing at all.
        private static void CheckHeroAddressesResolve(List<string> failures)
        {
            if (!Directory.Exists(HeroContentRoot))
            {
                failures.Add("[addresses] " + HeroContentRoot + " does not exist, so there is not a " +
                             "single migrated hero body to address. The move half of the migration " +
                             "has not run. (A FAILURE, not a vacuous pass: 'zero heroes moved' would " +
                             "otherwise satisfy a for-each over an empty set and report green.)");
                return;
            }

            var fbx = new List<string>();
            try
            {
                foreach (var raw in Directory.GetFiles(HeroContentRoot, "*.fbx", SearchOption.AllDirectories))
                    fbx.Add(raw.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                failures.Add("[addresses] scan of " + HeroContentRoot + " failed: " + ex.Message);
                return;
            }

            if (fbx.Count == 0)
            {
                failures.Add("[addresses] " + HeroContentRoot + " exists but holds no *.fbx - no hero " +
                             "body has been migrated, so this group has nothing to assert. Failing " +
                             "rather than passing on an empty set.");
                return;
            }

            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                failures.Add("[addresses] " + fbx.Count + " hero .fbx sit in " + HeroContentRoot +
                             " but no Addressables settings object exists, so none of them is " +
                             "addressable and every hero resolves to nothing.");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.groups == null)
            {
                failures.Add("[addresses] Addressables settings resolved null or has no groups.");
                return;
            }

            // address -> group, and assetPath -> address. The second map turns the common
            // near-miss (entry exists, address left as the default asset path or guid) into a
            // message that names the wrong address instead of just "missing".
            var byAddress   = new Dictionary<string, string>(StringComparer.Ordinal);
            var byAssetPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null || group.entries == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;
                    if (!string.IsNullOrEmpty(entry.address) && !byAddress.ContainsKey(entry.address))
                        byAddress[entry.address] = group.Name;
                    string ap = entry.AssetPath;
                    if (!string.IsNullOrEmpty(ap))
                    {
                        ap = ap.Replace('\\', '/');
                        if (!byAssetPath.ContainsKey(ap)) byAssetPath[ap] = entry.address;
                    }
                }
            }

            fbx.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var path in fbx)
            {
                string slug = Path.GetFileNameWithoutExtension(path);
                string address = HeroAddrPrefix + slug;
                if (byAddress.ContainsKey(address)) continue;

                string actual;
                if (byAssetPath.TryGetValue(path, out actual))
                    failures.Add("[addresses] " + path + " IS addressable but at address '" +
                                 (string.IsNullOrEmpty(actual) ? "<empty>" : actual) + "', not '" +
                                 address + "'. HeroAssetLoader builds its key as HeroAddrPrefix + " +
                                 "slug, so this entry is unreachable by the only code that asks for " +
                                 "it - and the loader falls through to a Resources copy that the " +
                                 "migration deleted.");
                else
                    failures.Add("[addresses] no Addressables entry at address '" + address + "' for " +
                                 path + ". The file was moved out of Resources but never registered, " +
                                 "so the hero has no local copy AND no catalog entry.");
            }
        }

        // -- 5 [hygiene] ------------------------------------------------------
        private static void CheckHygiene(List<string> failures)
        {
            foreach (var path in new[] { AssetLoaderSrc, TexLoaderSrc })
            {
                try
                {
                    if (!File.Exists(path)) { failures.Add("[hygiene] missing " + path); continue; }
                    var bytes = File.ReadAllBytes(path);
                    for (int i = 0; i < bytes.Length; i++)
                        if (bytes[i] == 0) { failures.Add("[hygiene] embedded NUL in " + path); break; }
                }
                catch (Exception ex) { failures.Add("[hygiene] " + path + ": " + ex.Message); }
            }
        }

        // -- helpers ----------------------------------------------------------
        /// <summary>Smaller of two IndexOf results, ignoring the -1 "absent" sentinel.</summary>
        private static int MinPresent(int a, int b)
        {
            if (a < 0) return b;
            if (b < 0) return a;
            return a < b ? a : b;
        }

        private static string ReadSrc(string path, List<string> failures)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                failures.Add("[src] missing " + path);
            }
            catch (Exception ex) { failures.Add("[src] " + path + ": " + ex.Message); }
            return null;
        }

        /// <summary>ReadSrc reduced to CODE ONLY. Every source-lint in this suite uses this, never
        /// the raw text (the [hygiene] NUL scan reads bytes and is deliberately raw).</summary>
        private static string ReadCode(string path, List<string> failures)
        {
            string raw = ReadSrc(path, failures);
            return raw == null ? null : CodeText(raw);
        }

        /// <summary>Non-comment lines only, with STRING LITERAL CONTENTS blanked - so neither a
        /// header paragraph nor a seam named inside a FlowTrace message can satisfy or trip a
        /// rule. Same idiom as EchoWorldPresenceRegression.CodeText /
        /// SpirePlansCelebrationRegression.CodeLines; kept file-local, matching the convention
        /// across this folder. NOT cosmetic here: HeroAssetLoader's header NAMES
        /// Resources.Load&lt;T&gt; at code-line 21, ~500 bytes ahead of any real call, so a
        /// raw-text order lint would read the header as the first resolve and pass on the defect.
        /// The character offsets this suite reports are therefore offsets into the CODE TEXT, not
        /// into the file - which is the right frame, because the ordering rule is about statements.</summary>
        private static string CodeText(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            var sb = new StringBuilder(source.Length);
            foreach (var raw in source.Split('\n'))
            {
                string t = raw.TrimStart();
                // Whole-line comments: `//`, `///`, and the body/opening of a /* */ block.
                if (t.StartsWith("//", StringComparison.Ordinal) ||
                    t.StartsWith("*",  StringComparison.Ordinal) ||
                    t.StartsWith("/*", StringComparison.Ordinal)) { sb.Append('\n'); continue; }
                sb.Append(StripStringLiterals(raw)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Blanks the CONTENTS of every double-quoted literal on the line (the quotes stay,
        /// so the line still reads), honouring backslash escapes, and drops a trailing `//` comment
        /// that follows code.</summary>
        private static string StripStringLiterals(string line)
        {
            var sb = new StringBuilder(line.Length);
            bool inStr = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inStr && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;   // trailing comment
                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inStr = !inStr;
                    sb.Append(c);
                    continue;
                }
                sb.Append(inStr ? ' ' : c);
            }
            return sb.ToString();
        }
    }
}
