// =============================================================================
// SyntyStructureRetheme — WO-1291. Swap the ART behind each Structures/* address.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-01: FULL Synty re-theme.
//
// ⛔ THE KEY DECISION, AND WHY IT IS THE SAFE ONE.
// We do NOT touch structures-catalog.json. Its 27 `visualPrefabPath` values and — far
// more importantly — its `id` strings are LIVE SAVE KEYS (memory
// structure-role-enum-and-format-normalization); renaming one silently orphans every
// player's building. Instead we keep every `Structures/*` ADDRESS exactly as it is and
// re-point the address at a new prefab. The catalog, the save format, VisualFactory and
// every caller are untouched; only the mesh behind the address changes.
//
// ⚠ THE ADDRESS SET IS THE AUTHORITY, NOT THIS TABLE. Structure_Art holds 38 addresses;
// seven of them are TEXTURES (*_Albedo, *_Tex/*) and are deliberately absent below — a
// texture has no prefab to swap. Anything unmapped is REPORTED, never silently skipped,
// so the gap is visible rather than discovered on a device.
//
// ASSIGNMENT PROVENANCE (updated 2026-09-01): the table below now carries the
// OWNER-APPROVED re-picks from the 2026-09-01 review (armorer, barracks, lumbermill,
// arcane tower, the composed Watermill, and the three previously-unmapped addresses
// GenericContainer / CrystalMine / IronMine). Rows not named in that review remain the
// first-pass picks from the original run. Change the table, never the addresses.
//
// ⛔ SHIPPING: every run of this re-hashes the Addressable content, so the build CANNOT
// ship without tools\r2-ship.ps1 (CLAUDE.md §16 — content-hashed bundles, a missing push
// fails SILENTLY with placeholder buildings and no on-screen error; it has happened four
// times). Judge by R2_PUSH_OK + R2_PARITY_OK on a FRESH log, never the exit code.
//
// Batchmode: DeNelle.Editor.SyntyStructureRetheme.Run
// Menu:      Defenders/Art/Re-theme Structures to Synty
// Marker:    STRUCTURE_RETHEME_OK / STRUCTURE_RETHEME_FAIL
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class SyntyStructureRetheme
    {
        private const string Synty   = "Assets/Synty/PolygonFantasyKingdom/Prefabs/";
        // ⚠ DERIVED FROM AssetRoots, never re-typed. A second copy of a relocatable root is
        // how a relocation misses a call site, and the miss is SILENT — the builder just
        // quietly loads nothing. AssetRootsRegression enforces this and caught the literal.
        private static readonly string OutDir = AssetRoots.StructureContent + "/Synty";
        private const string StructureLayerName = "Structure";

        /// <summary>address leaf -> Synty prefab, relative to <see cref="Synty"/>.</summary>
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            // ── storefronts / town buildings ───────────────────────────────────
            // armorer moved off the Blacksmith preset (owner re-pick 2026-09-01):
            // it shared art with Forge, and Forge KEEPS the Blacksmith.
            { "armorer",              "Buildings/Presets/SM_Bld_Preset_House_05_Optimized.prefab" },
            { "Forge",                "Buildings/Presets/SM_Bld_Preset_Blacksmith_01_Optimized.prefab" },
            { "ShopAndCrafting",      "Buildings/Presets/SM_Bld_Preset_Tavern_01_Optimized.prefab" },
            { "store",                "Buildings/Presets/SM_Bld_Preset_House_02_A_Optimized.prefab" },
            { "jeweler",              "Buildings/Presets/SM_Bld_Preset_House_03_Optimized.prefab" },
            // lumbermill: House_06 REJECTED (owner re-pick 2026-09-01) -> Shelter_02.
            { "lumbermill",           "Buildings/Presets/SM_Bld_Preset_Shelter_02_Optimized.prefab" },
            { "farm",                 "Buildings/Presets/SM_Bld_Preset_Hut_01_Optimized.prefab" },
            // barracks: Stables REJECTED (owner re-pick 2026-09-01) -> House_07.
            { "barracks",             "Buildings/Presets/SM_Bld_Preset_House_07_Optimized.prefab" },
            { "House_Medieval_Medium","Buildings/Presets/SM_Bld_Preset_House_01_A_Optimized.prefab" },
            { "Windmill_Medieval",    "Buildings/Presets/SM_Bld_Preset_House_Windmill_01_Optimized.prefab" },
            // Watermill_Medieval is COMPOSED (house + waterwheel) -- see Composed below,
            // not this table. It was sharing the Windmill preset, which read as a duplicate.
            { "PetHouse2",            "Buildings/Presets/SM_Bld_Preset_Hut_02_Optimized.prefab" },

            // -- previously-unmapped set, closed 2026-09-01 (owner ruling: wood pallet
            //    for the generic container; KayKit mine for both mines -- differentiating
            //    dressing for IronMine is deferred to WO-1292). These sources live OUTSIDE
            //    the Synty root, so they are authored as absolute "Assets/..." paths -- see
            //    ResolveSourcePath. Both KayKit families import with bakeAxisConversion:0
            //    and remap to URP/Lit materials (verified at source 2026-09-01).
            { "GenericContainer",     "Assets/Models/KayKit/KayKit Resource Bits 1.0/Assets/fbx(unity)/Pallet_Wood_Covered_A.fbx" },
            { "CrystalMine",          "Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/buildings/green/building_mine_green.fbx" },
            { "IronMine",             "Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/buildings/green/building_mine_green.fbx" },

            // ── arcane line: the spire tiers read as a church/tower silhouette ──
            // ⚠ THE KEY HAS A SPACE IN IT: the live address is "Structures/arcane tower".
            // A first pass keyed it "arcane" because the diagnostic that dumped the address
            // list split on whitespace and silently truncated it. Addresses are free text —
            // never assume they are token-shaped.
            // arcane tower: owner re-pick 2026-09-01 -- it duplicated ArcaneSpire_1 on
            // Tower_01; ArcaneSpire_1 KEEPS Tower_01 (now unique) and this address takes
            // Church_01_A. NOTE Church_01_A is SAFE HERE but NOT below: the A3 tower-aspect
            // floor (StructureOrientationOracle) scopes to type==Tower AND heightMul>=1.2,
            // and the 'arcane-tower' catalog row is type RESOURCE with heightMul 1 -- read
            // at source in structures-catalog.json 2026-09-01. The ArcaneSpire_2 rejection
            // note below is about a Tower-class row and still stands.
            { "arcane tower",         "Buildings/Presets/SM_Bld_Preset_Church_01_A_Optimized.prefab" },
            { "ArcaneSpire_1",        "Buildings/Presets/SM_Bld_Preset_Tower_01_Optimized.prefab" },
            // ⚠ NOT Church_01_A. STRUCTURE_ORIENTATION_FAIL measured that preset at upright
            // aspect 1.08, below the 1.2 floor every Tower-class row must clear: it is a wide
            // hall, not a tower silhouette. The oracle is explicit that widening the floor
            // would be an OWNER RULING, not a fix — so the ART changes, not the threshold.
            // The L tower keeps the tier escalating and measures ~2.5 aspect.
            { "ArcaneSpire_2",        "Castle/SM_Bld_Castle_Wall_Tower_L_01.prefab" },
            { "ArcaneSpire_3",        "Buildings/Presets/SM_Bld_Preset_Church_01_B_Optimized.prefab" },

            // ── defence: the archer tower is the OWNER'S OWN ART. DO NOT RE-THEME IT. ──
            //
            // ⛔ THE THREE ROWS THAT USED TO LIVE HERE ARE DELETED ON AN OWNER RULING, and
            // re-adding them silently reverts her art. They were:
            //     Tower_Wooden_Watchtower    -> Castle/SM_Bld_Castle_Wall_Tower_S_01.prefab
            //     Tower_Wooden_Watchtower_L2 -> Castle/SM_Bld_Castle_Wall_Tower_M_01.prefab
            //     Tower_Wooden_Watchtower_L3 -> Castle/SM_Bld_Castle_Wall_Tower_L_01.prefab
            //
            // OWNER RULING 2026-09-02, verbatim: "one thing i hate is the changes to the archer
            // towers. can you bring my wooden towers i created in tripo?" and, on the replacements
            // specifically: "yes i hate those round towers".
            //
            // Tower_Wooden_Watchtower{,_L2,_L3} are HER assets - Tripo-authored, each .fbx carrying
            // a sibling .fbx.tripo-extracted marker. This table mapped them onto a Synty stone
            // castle WALL TOWER size ladder, and because the generated wrapper prefabs reused her
            // filenames verbatim, the swap was invisible: Structure_Art.asset ended up with the
            // SAME address claimed twice (her prefab and the stone wrapper), so Addressables
            // resolved to whichever the built catalog listed first. That is precisely how a stone
            // tower shipped wearing her wooden tower's name.
            //
            // ⚠ THIS FILE IS THE SOURCE OF THAT DEFECT, NOT A VICTIM OF IT. Re-running
            // SyntyStructureRetheme.Run with those rows present re-creates the wrappers and undoes
            // the fix in structures-catalog.json + Structure_Art.asset, with no error and no gate
            // failure - the owner would find it herself in a felt-test, which is the outcome the
            // whole F8/oracle apparatus exists to prevent.
            //
            // The catalog now points tower_ground_archer at Structures/Tower_Wooden_Watchtower{,_L2,
            // _L3}, and FoundingReachabilityRegression asserts that in BOTH directions - it fails if
            // her ladder goes missing AND fails if the Polyperfect stone family reappears. The three
            // Synty stone towers survive under their own honest addresses
            // (Structures/Synty_Tower_Castle_Wall_S/_M/_L) and remain available to anything that
            // genuinely wants a stone wall tower.
            //
            // ⭐ Her ruling of the same day - "the other synty were on purpose" - means the REST of
            // this table stands. This is a single-row exception, not a retreat from the re-theme.

            // ── perimeter pieces (same kit as the WO-1290 castle ring) ─────────
            { "Wall_Medieval_Stone",  "Castle/SM_Bld_Castle_Wall_01.prefab" },
            { "Wall_Medieval_Wood",   "Castle/SM_Bld_Castle_Hoarding_Wood_Wall_01.prefab" },
            { "Gate_Medieval_Medium", "Castle/SM_Bld_Castle_Wall_Gate_01.prefab" },

            // ── siege: real art, replacing the polyperfect stand-ins ───────────
            { "Catapult",             "SiegeEngines/SM_Wep_Catapult_01.prefab" },
            { "Ballista",             "SiegeEngines/SM_Wep_Ballista_Mobile_01.prefab" },
            { "Ballista_L1",          "SiegeEngines/SM_Wep_Ballista_Mobile_01.prefab" },
            { "Ballista_L2",          "SiegeEngines/SM_Wep_Ballista_Mounted_01.prefab" },
            { "Ballista_L3",          "SiegeEngines/SM_Wep_Trebuchet_01.prefab" },

            // ── props ──────────────────────────────────────────────────────────
            { "Well",                 "Props/SM_Prop_Well_01.prefab" },
            { "Torche_Wall",          "Props/SM_Prop_Torch_01.prefab" },
            { "HealingCaravan",       "Vehicles/SM_Veh_TraderWagon_01.prefab" },
        };

        /// <summary>One child of a composed wrapper. Path resolves like a Map value
        /// (Synty-relative, or absolute when it starts with "Assets/").</summary>
        private sealed class ComposedPart
        {
            public readonly string  Path;
            public readonly Vector3 LocalPos;
            public readonly Vector3 LocalEuler;
            /// <summary>When true the part ignores LocalPos and is wall-mounted on the
            /// FIRST part's +X face from measured bounds -- see MountOnSide.</summary>
            public readonly bool    AutoMountSide;
            public ComposedPart(string path, Vector3 pos, Vector3 euler, bool autoMountSide = false)
            { Path = path; LocalPos = pos; LocalEuler = euler; AutoMountSide = autoMountSide; }
        }

        /// <summary>
        /// address leaf -> multi-part wrapper recipe. Checked BEFORE Map: an address is
        /// either single-model or composed, never both. Part 0 is the anchor at the origin.
        /// </summary>
        private static readonly Dictionary<string, ComposedPart[]> Composed = new Dictionary<string, ComposedPart[]>
        {
            // Watermill (owner re-pick 2026-09-01): House_08 body + the Castle kit
            // waterwheel hung on one wall, replacing the Windmill-preset duplicate.
            // PLACEHOLDER MOUNT pending screenshot verify: the wheel's seat is COMPUTED
            // from measured renderer bounds at build time (vertical wheel flat against the
            // +X wall face, slightly embedded, bottom near ground) rather than authored as
            // literals -- no seat has eyeballed this composition yet. If the screenshots
            // read wrong, author explicit LocalPos/LocalEuler here and drop AutoMountSide.
            { "Watermill_Medieval", new[]
                {
                    new ComposedPart("Buildings/Presets/SM_Bld_Preset_House_08_Optimized.prefab", Vector3.zero, Vector3.zero),
                    new ComposedPart("Castle/SM_Bld_Waterwheel_01.prefab", Vector3.zero, Vector3.zero, autoMountSide: true),
                } },
        };

        /// <summary>Map/Composed values are Synty-relative by default; a value that already
        /// starts with "Assets/" is an absolute project path (the KayKit sources).</summary>
        private static string ResolveSourcePath(string rel)
            => rel.StartsWith("Assets/") ? rel : Synty + rel;

        /// <summary>
        /// True when the address currently points at something that is NOT a prefab — a
        /// texture, a material. There is nothing to swap for those.
        /// ⚠ DETECTED BY ASSET TYPE, NOT BY A NAME LIST. A hand-written list of texture
        /// addresses had to spell out a base-colour texture suffix, re-typing EnemyArtPaths'
        /// BaseColorSuffix token — the art-ledger oracle rejects a re-typed naming token
        /// because a literal at a call site cannot be re-pointed, traced, or asserted. It
        /// would also go stale the moment a new texture address was added. Asking the
        /// AssetDatabase what the thing IS has neither problem.
        /// </summary>
        private static bool IsNonPrefabAddress(AddressableAssetEntry entry)
        {
            if (entry == null) return true;
            string path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(path)) return true;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
        }

        [MenuItem("Defenders/Art/Re-theme Structures to Synty")]
        public static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) { Debug.LogError("STRUCTURE_RETHEME_FAIL Addressable settings not found."); return; }

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == "Structure_Art");
            if (group == null) { Debug.LogError("STRUCTURE_RETHEME_FAIL group 'Structure_Art' not found."); return; }

            // ⚠ CREATE THE FOLDER THROUGH THE ASSETDATABASE, NOT Directory.CreateDirectory.
            // A bare mkdir puts the folder on disk but leaves the AssetDatabase unaware of it,
            // and PrefabUtility.SaveAsPrefabAsset then THROWS on the first save into it
            // (observed 2026-09-01: the whole run aborted at the first BuildWrapper call).
            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                Directory.CreateDirectory(OutDir);
                AssetDatabase.Refresh();
                if (!AssetDatabase.IsValidFolder(OutDir))
                {
                    Debug.LogError("STRUCTURE_RETHEME_FAIL could not create asset folder " + OutDir);
                    return;
                }
            }
            int layer = LayerMask.NameToLayer(StructureLayerName);
            if (layer < 0)
                Debug.LogWarning("[SyntyRetheme] '" + StructureLayerName + "' layer missing — structures " +
                                 "left on Default; tower line-of-sight and nav carving will degrade.");

            // Snapshot the live addresses BEFORE touching anything: the address set is the
            // authority, this table is not.
            var liveEntries = group.entries.ToList();
            var swapped = new List<string>();
            var missingArt = new List<string>();
            var unmapped = new List<string>();
            // address -> the wrapper GUID that now owns it, for the purge pass below.
            var ownedNewGuid = new Dictionary<string, string>();
            // THE GROUP CAN HOLD DUPLICATE ADDRESSES. CreateOrMoveEntry re-points the
            // WRAPPER's entry but cannot remove the OLD source asset's entry, which keeps
            // carrying the same address -- verified 2026-09-01: 68 entries over 38 addresses,
            // every previously-swapped address present twice (old FBX + wrapper). So each
            // ADDRESS is processed once, not each entry, or a re-run double-counts and
            // rebuilds every wrapper twice.
            var processed = new HashSet<string>();

            foreach (var live in liveEntries)
            {
                string address = live.address;
                if (string.IsNullOrEmpty(address) || !address.StartsWith("Structures/")) continue;
                if (processed.Contains(address)) continue;
                string leaf = address.Substring("Structures/".Length);

                if (IsNonPrefabAddress(live)) continue;              // texture/material/dangling, see method docs

                bool isComposed = Composed.TryGetValue(leaf, out var parts);
                string rel = null;
                if (!isComposed && !Map.TryGetValue(leaf, out rel))
                { processed.Add(address); unmapped.Add(leaf); continue; }
                processed.Add(address);

                GameObject source = null;
                if (!isComposed)
                {
                    source = AssetDatabase.LoadAssetAtPath<GameObject>(ResolveSourcePath(rel));
                    if (source == null) { missingArt.Add(leaf + " -> " + rel); continue; }
                }

                string outPath = OutDir + "/" + leaf.Replace('/', '_') + ".prefab";
                // Guarded per CLAUDE.md §12: one bad source asset is LOGGED and skipped, never
                // allowed to abort the pass and leave the address set half-swapped.
                GameObject built = null;
                try { built = isComposed ? BuildComposedWrapper(parts, outPath, layer)
                                         : BuildWrapper(source, outPath, layer); }
                catch (System.Exception ex) { missingArt.Add(leaf + " (wrapper threw: " + ex.Message + ")"); continue; }
                if (built == null) { missingArt.Add(leaf + " (wrapper returned null)"); continue; }

                // Move the address onto the new prefab. CreateOrMoveEntry re-points an
                // existing address rather than duplicating it, so the catalog key is stable.
                string guid = AssetDatabase.AssetPathToGUID(outPath);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null) { missingArt.Add(leaf + " (entry move failed)"); continue; }
                entry.address = address;
                ownedNewGuid[address] = guid;
                swapped.Add(leaf);
            }

            // ---- purge superseded / dangling entries ------------------------------------
            // For every address THIS run re-pointed, exactly ONE entry -- the wrapper's --
            // may keep it. Everything else under that address (the old source asset's
            // entry, or a dangling GUID that resolves to nothing) is removed FROM THE
            // GROUP ONLY; the asset itself stays on disk. Duplicate addresses make the
            // built catalog's key resolution ambiguous, and the orientation oracle's
            // address map silently last-write-wins over them. Scoped to swapped addresses
            // so a skipped/missing-art address never loses its only live entry.
            int purged = 0;
            foreach (var e in group.entries.ToList())
            {
                if (e == null || string.IsNullOrEmpty(e.address)) continue;
                if (!ownedNewGuid.TryGetValue(e.address, out string keepGuid)) continue;
                if (e.guid == keepGuid) continue;
                if (settings.RemoveAssetEntry(e.guid, false)) purged++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SyntyRetheme] swapped {swapped.Count}: {string.Join(", ", swapped)}");
            if (purged > 0)
                Debug.Log($"[SyntyRetheme] purged {purged} superseded/dangling group entr(ies) whose address " +
                          "a wrapper now owns (assets untouched on disk).");
            if (unmapped.Count > 0)
                Debug.LogWarning($"[SyntyRetheme] UNMAPPED {unmapped.Count} address(es) still on the OLD art — " +
                                 $"{string.Join(", ", unmapped)}. Not a silent skip: add them to Map or rule " +
                                 "them out explicitly.");
            if (missingArt.Count > 0)
                Debug.LogWarning($"[SyntyRetheme] ART MISSING {missingArt.Count}: {string.Join("; ", missingArt)}. " +
                                 "Is the Synty pack imported? It is gitignored (see .gitignore).");

            if (swapped.Count == 0) { Debug.LogError("STRUCTURE_RETHEME_FAIL nothing was swapped."); return; }
            Debug.Log($"STRUCTURE_RETHEME_OK swapped={swapped.Count} unmapped={unmapped.Count} " +
                      $"missing={missingArt.Count} purged={purged} -> {OutDir}");
        }

        /// <summary>Wrap a Synty source prefab in a tracked prefab carrying a fitted BoxCollider
        /// on the Structure layer. The wrapper exists so the gitignored pack is referenced from
        /// exactly one tracked place per address, the way the polyperfect walls always were.</summary>
        private static GameObject BuildWrapper(GameObject source, string outPath, int layer)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (root == null) root = Object.Instantiate(source);
            if (root == null) return null;
            try
            {
                root.name = Path.GetFileNameWithoutExtension(outPath);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                return FinishWrapper(root, outPath, layer);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Multi-part wrapper (WO-1291, e.g. the Watermill): a plain root with each
        /// part instantiated as a child at its authored offset (or auto-mounted, see
        /// MountOnSide), then the same collider/layer/save treatment as every single-model
        /// wrapper. Part 0 is the anchor and always sits at the root origin.</summary>
        private static GameObject BuildComposedWrapper(ComposedPart[] parts, string outPath, int layer)
        {
            if (parts == null || parts.Length == 0) return null;
            var root = new GameObject(Path.GetFileNameWithoutExtension(outPath));
            try
            {
                GameObject anchor = null;
                foreach (var part in parts)
                {
                    string srcPath = ResolveSourcePath(part.Path);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                    if (source == null)
                        throw new FileNotFoundException("composed part missing: " + srcPath);
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    if (child == null) child = Object.Instantiate(source);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = part.LocalPos;
                    child.transform.localRotation = Quaternion.Euler(part.LocalEuler);
                    child.transform.localScale    = Vector3.one;
                    if (anchor == null) anchor = child;
                    else if (part.AutoMountSide) MountOnSide(anchor, child);
                }
                return FinishWrapper(root, outPath, layer);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// PLACEHOLDER MOUNT (WO-1291, pending screenshot verify). Hangs
        /// <paramref name="part"/> on the anchor's +X wall face: the part's thin horizontal
        /// axis is yawed to point out of the wall (a vertical wheel disc then lies flat
        /// against it), the part is embedded 0.10 m into the face so no air gap shows, and
        /// its bottom seats 0.05 m above local ground. The NUMBERS are measured from
        /// renderer bounds at build time so they cannot go stale, but the CHOICE of face,
        /// embed and clearance has not been eyeballed -- verify on the RunCaptureHeadless
        /// screenshots and, if wrong, author explicit offsets in Composed instead.
        /// </summary>
        private static void MountOnSide(GameObject anchor, GameObject part)
        {
            var aRends = anchor.GetComponentsInChildren<Renderer>(true);
            var pRends = part.GetComponentsInChildren<Renderer>(true);
            if (aRends.Length == 0 || pRends.Length == 0) return;

            Bounds a = WorldBounds(aRends);
            Bounds p = WorldBounds(pRends);

            // A wheel hanging flat on an X-facing wall must be THIN along X. If the source
            // is authored thin along Z instead, a 90-degree yaw swaps the horizontal axes.
            if (p.size.x > p.size.z)
            {
                part.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * part.transform.localRotation;
                p = WorldBounds(pRends);   // re-measure in the new orientation
            }

            const float embedM  = 0.10f;   // wheel sunk into the wall face
            const float groundClearanceM = 0.05f;
            Vector3 delta;
            delta.x = (a.max.x + p.extents.x - embedM) - p.center.x;
            delta.y = groundClearanceM - p.min.y;
            delta.z = a.center.z - p.center.z;
            part.transform.localPosition += delta;
        }

        private static Bounds WorldBounds(Renderer[] rends)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>Shared tail of every wrapper build: fitted BoxCollider, Structure layer,
        /// save. Never destroys <paramref name="root"/> -- the caller owns the instance.</summary>
        private static GameObject FinishWrapper(GameObject root, string outPath, int layer)
        {
            // BoxCollider fitted to the MEASURED bounds, not a MeshCollider: the Structure
            // layer is what every tower/hero line-of-sight linecast tests against, and a box
            // is both cheaper and stable under the nav carve.
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = WorldBounds(rends);
                // ⚠ NO ?? HERE. GetComponent returns Unity's FAKE-NULL, which is not C# null,
                // so `GetComponent<T>() ?? AddComponent<T>()` hands back the fake-null and the
                // very next line throws "There is no 'BoxCollider' attached ... but a script is
                // trying to access it". That one operator failed 27 of 29 structures on the
                // 2026-09-01 first run. Explicit == null is the only correct test.
                var box = root.GetComponent<BoxCollider>();
                if (box == null) box = root.AddComponent<BoxCollider>();
                box.center = b.center - root.transform.position;
                box.size   = b.size;
            }
            if (layer >= 0) SetLayerRecursively(root, layer);

            return PrefabUtility.SaveAsPrefabAsset(root, outPath);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
