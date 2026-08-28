// =============================================================================
// StarterLoadoutRegression [starter-loadout] (WO-860 + WO-861 Phase 0)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the two owner-felt defects WO-860 fixes and the roster un-hardcoding
// WO-861 Phase 0 lands:
//
//   1 [starter-data]  The per-class STARTER LOADOUT is real, equippable gear: the
//                     knight kit exists, its mainHand + offHand both resolve in
//                     weapons.json (BOTH dual-copies, content-identical), the main
//                     hand fits the class, and the off-hand really IS an off-hand
//                     item. A typo here silently drops the hero to auto-best - i.e.
//                     straight back to the bug (Flameblade instead of Squire's Blade).
//   2 [starter-beats-best] The starter is NOT the same pick auto-best would make.
//                     This is the whole point of WO-860 A2: GearCatalog.BestWeapon
//                     ranks by damageMult and returns knight_flameblade (1.2) over
//                     knight_starter (1.0). If these ever converge the fix has
//                     become a no-op and nobody would notice (reported as a NOTE if
//                     the catalog legitimately changes, FAIL only if the starter is
//                     unresolvable).
//   3 [prefs-clear]   ResetToNewGame ACTUALLY deletes every dotr-equip-* key for
//                     every known class (plus the per-class dotr-loadout-* bar).
//                     Executed for real against seeded keys, not source-linted - the
//                     original bug was precisely that the reset "looked" complete.
//                     The developer's own PlayerPrefs are snapshotted and restored.
//   4 [shelf-cap]     The thinned shelf: for the Forge + Armorer, at every authored
//                     level, Resolve returns <= perLevelCap rows PER REQUIRED LEVEL,
//                     ZERO excluded-prefix rows, and ZERO locked rows EXCEPT the
//                     WO-960 preview window: a vendor with lockedPreviewLevels > 0
//                     (armorer=5) may ship level-locked rows whose req sits in
//                     (level, level+window] - those are the owner-ruled greyed
//                     ladder, not a WO-860 regression. Class locks stay forbidden.
//   5 [shelf-oracle]  The surviving rows match an INDEPENDENT re-implementation of
//                     the documented sort written here from the JSON (bucket by
//                     req.level, power DESC, id ORDINAL ASC), and two consecutive
//                     Resolve calls agree - a shelf that reshuffles is a shelf no
//                     test and no player can pin.
//   6 [vendor-data]   perLevelCap / onlyEquippable / footerLine / excludeIdPrefixes
//                     are DATA on the forge + armorer rows in BOTH vendors.json
//                     copies (byte-identical, ASCII) - the WO-860 "tunable without a
//                     recompile" acceptance. A footerLine that exists only in code
//                     would fail the "no recompile to retune" contract.
//   7 [roster-truth]  The playable set has exactly ONE definition: PlayableHeroes.
//                     ChooseHero no longer hardcodes the Knight force, the vendor
//                     resolver no longer keeps its own FullRoster, and
//                     HeroSelectController no longer compares to a local const.
//                     Today's set is still { Knight } (ff.knightonly ON) - Phase 0
//                     removes the hardcoding, it does not flip content on.
//   8 [loadout-key]   HeroLoadout's W/E/R key is PER CLASS, and the knight's key is
//                     BYTE-IDENTICAL to the retired global "dotr-loadout-knight-v1"
//                     - which is the entire migration story: an existing save's
//                     Knight bar is read back unchanged, with no copy step.
//   9 [auto-best-owned-only] Above MainHandUpToLevel the auto-best power curve
//                     resumes - and it may only rank gear the player OWNS. A
//                     level-5 hero owning ONLY the granted starter kit resolves
//                     the STARTER weapon, not knight_flameblade (a 40w/120i Forge
//                     item auto-best used to hand over free the instant a knight
//                     hit level 2). Ranking + wiring are both pinned, plus the
//                     never-weaponless floor.
//
// Markers: STARTER_LOADOUT_OK / STARTER_LOADOUT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.StarterLoadoutRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class StarterLoadoutRegression
    {
        private const string VendorsRes = "Assets/Resources/Data/Canonical/vendors.json";
        private const string VendorsSA = "Assets/StreamingAssets/Data/Canonical/vendors.json";
        private const string WeaponsRes = "Assets/Resources/Data/Canonical/weapons.json";
        private const string WeaponsSA = "Assets/StreamingAssets/Data/Canonical/weapons.json";

        private const string GameStateSrc = "Assets/_Modules/Core/State/GameStateService.cs";
        private const string ResolverSrc = "Assets/_Modules/Village/Hero/VendorStockResolver.cs";
        private const string HeroSelectSrc = "Assets/_Modules/Onboarding/HeroSelectController.cs";
        private const string LoadoutSrc = "Assets/_Modules/Village/Hero/GearLoadout.cs";

        // The class whose starter kit is authored today. WO-861 adds ranger + mage rows;
        // this suite widens by adding them to this array, nothing else.
        private static readonly string[] AuthoredStarterClasses = { "knight" };

        // The gear vendors WO-860 thins. Both must carry the four data knobs.
        private static readonly string[] ThinnedVendors = { "forge", "armorer" };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("STARTER_LOADOUT_OK - " + reason);
            else Debug.LogError("STARTER_LOADOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "starter-data", () => Case1_StarterData(failures));
                Case(failures, "starter-beats-best", () => Case2_StarterBeatsAutoBest(failures, notes));
                Case(failures, "prefs-clear", () => Case3_PrefsClear(failures));
                Case(failures, "shelf-cap", () => Case4_ShelfCap(failures, notes));
                Case(failures, "shelf-oracle", () => Case5_ShelfOracle(failures));
                Case(failures, "vendor-data", () => Case6_VendorData(failures));
                Case(failures, "roster-truth", () => Case7_RosterTruth(failures));
                Case(failures, "loadout-key", () => Case8_LoadoutKey(failures));
                Case(failures, "auto-best-owned-only", () => Case9_AutoBestOwnedOnly(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "STARTER LOADOUT OK - per-class starter kit resolves + beats auto-best, " +
                         "ResetToNewGame really deletes every dotr-equip-*/dotr-loadout-* key, the gear " +
                         "shelf is capped per level with zero locked and zero placeholder rows and matches " +
                         "an independent sort oracle, the knobs are vendors.json data in both dual-copies, " +
                         "the playable roster has one definition, the W/E/R key is per class, and auto-best " +
                         "above the starter band ranks ONLY owned gear behind a never-weaponless floor" + noteStr;
                return true;
            }
            reason = "starter-loadout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 - the starter kit is real, equippable gear
        // =====================================================================
        private static void Case1_StarterData(List<string> failures)
        {
            // ⚠ weapons.json is NOT a byte-identical dual pair, and asserting that it is was a
            // FALSE oracle (it failed on arrival, 2026-08-02). Unlike every other canonical file,
            // this pair is a deliberate CURATION relationship, not a mirror:
            //   StreamingAssets = the full LIBRARY (~254KB, 435 rows) the tools edit
            //   Resources       = the CURATED runtime export (~56KB, 100 rows) the player loads
            // GearCurationExporter owns the merge, driven by Assets/Editor/GearCurationPicks.json.
            // Forcing byte-identity would either delete ~335 library rows or dump ~335 uncurated
            // rows into the shipped build - so the "fix" for this failure is strictly worse than
            // the failure. WO-861's acceptance line claiming these must be byte-identical is
            // simply wrong about this one file.
            // WHAT STILL MATTERS is the thing the dual-copy law protects against: a starter id
            // that resolves in the editor but NOT on device. That is covered precisely below -
            // every starter id is resolved through the RESOURCES copy (what the player actually
            // loads), so a library-only row still fails this suite.
            CompareDualCopy(VendorsRes, VendorsSA, "vendors.json", failures);

            foreach (var job in AuthoredStarterClasses)
            {
                var kit = StarterLoadout.For(job);
                if (kit == null)
                {
                    failures.Add($"[starter-data] StarterLoadout has NO kit for '{job}' - a new game would fall " +
                                 "straight back to GearCatalog.BestWeapon, which is the WO-860 bug (the owner's " +
                                 "'on start should be a sword and shield' becomes whatever has the highest damageMult)");
                    continue;
                }

                if (string.IsNullOrEmpty(kit.MainHand))
                {
                    failures.Add($"[starter-data] StarterLoadout['{job}'] has an EMPTY mainHand - the hero would " +
                                 "spawn on auto-best with no authored opening weapon");
                }
                else
                {
                    var w = GearCatalog.FindWeapon(kit.MainHand);
                    if (w == null)
                        failures.Add($"[starter-data] starter mainHand '{kit.MainHand}' ({job}) is NOT a row in " +
                                     "weapons.json - GearLoadout logs a Warn and silently falls back to auto-best, " +
                                     "so the fix would look applied and do nothing");
                    else
                    {
                        if (!GearCatalog.WeaponFitsClass(w, job))
                            failures.Add($"[starter-data] starter mainHand '{kit.MainHand}' does NOT fit class " +
                                         $"'{job}' (job='{w.job}') - the equip gate rejects it and the hero falls " +
                                         "back to auto-best");
                        if (w.IsOffHandItem)
                            failures.Add($"[starter-data] starter mainHand '{kit.MainHand}' is an OFF-HAND item " +
                                         "(category 'shield') - EnforceHandSlots would move it out of the main hand " +
                                         "and leave the hero unarmed");
                    }
                }

                if (!string.IsNullOrEmpty(kit.OffHand))
                {
                    var o = GearCatalog.FindWeapon(kit.OffHand);
                    if (o == null)
                        failures.Add($"[starter-data] starter offHand '{kit.OffHand}' ({job}) is NOT a row in " +
                                     "weapons.json - HeroBodySwapper's seed no-ops with a Warn and the hero spawns " +
                                     "shieldless (the other half of the owner's 'sword AND shield')");
                    else
                    {
                        if (!o.IsOffHandItem)
                            failures.Add($"[starter-data] starter offHand '{kit.OffHand}' is not an off-hand item " +
                                         $"(category='{o.category}') - EquipOffHandById rejects it outright");
                        if (!GearCatalog.WeaponFitsClass(o, job))
                            failures.Add($"[starter-data] starter offHand '{kit.OffHand}' does not fit class '{job}'");
                    }
                }

                if (kit.MainHandUpToLevel < 1)
                    failures.Add($"[starter-data] StarterLoadout['{job}'].MainHandUpToLevel = {kit.MainHandUpToLevel} " +
                                 "- a value below 1 means the starter never applies even on a level-1 hero");
            }
        }

        // =====================================================================
        //  CASE 2 - the starter is not just auto-best wearing a hat
        // =====================================================================
        private static void Case2_StarterBeatsAutoBest(List<string> failures, List<string> notes)
        {
            foreach (var job in AuthoredStarterClasses)
            {
                var kit = StarterLoadout.For(job);
                if (kit == null || string.IsNullOrEmpty(kit.MainHand)) continue;   // Case 1 already failed it

                var best = GearCatalog.BestWeapon(job, 1);
                if (best == null)
                {
                    failures.Add($"[starter-beats-best] GearCatalog.BestWeapon('{job}', 1) returned null - the " +
                                 "catalog cannot serve this class at all, so nothing about the starter can be judged");
                    continue;
                }
                if (string.Equals(best.id, kit.MainHand, StringComparison.OrdinalIgnoreCase))
                {
                    notes.Add($"'{job}' starter '{kit.MainHand}' now EQUALS auto-best - the WO-860 A2 override is " +
                              "currently a no-op for this class (fine if the catalog was retuned, but the felt bug " +
                              "'I get the Flameblade / an axe on a new game' will not be caught by this suite anymore)");
                }
            }
        }

        // =====================================================================
        //  CASE 3 - New Game really erases the equip PlayerPrefs
        // =====================================================================
        private static void Case3_PrefsClear(List<string> failures)
        {
            var clear = typeof(GameStateService).GetMethod(
                "ClearEquipPrefs", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (clear == null)
            {
                failures.Add("[prefs-clear] GameStateService.ClearEquipPrefs (static) not found - WO-860 A1 is not " +
                             "in this tree, so a once-equipped axe still survives every New Game");
                return;
            }

            // ResetToNewGame must actually CALL it - a helper nobody invokes fixes nothing.
            string src = ReadSource(GameStateSrc, failures);
            if (src != null && src.IndexOf("ClearEquipPrefs();", StringComparison.Ordinal) < 0)
                failures.Add("[prefs-clear] ResetToNewGame does not call ClearEquipPrefs() - the eraser exists but " +
                             "New Game never runs it");

            // Build the full key set the clear must cover.
            var keys = new List<string>();
            foreach (var job in PlayableHeroes.AllKnownJobKeys())
            {
                foreach (var prefix in EquipPrefKeys.AllSlotPrefixes) keys.Add(prefix + job);
                keys.Add(EquipPrefKeys.LoadoutKeyFor(job));
            }
            if (keys.Count == 0)
            {
                failures.Add("[prefs-clear] the key set is EMPTY (AllKnownJobKeys or AllSlotPrefixes is empty) - " +
                             "the reset would loop over nothing and pass vacuously");
                return;
            }

            // Snapshot the developer's real prefs so a batch run never eats their editor state.
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in keys)
                if (PlayerPrefs.HasKey(k)) snapshot[k] = PlayerPrefs.GetString(k, string.Empty);

            try
            {
                const string Sentinel = "regression_stale_axe";
                foreach (var k in keys) PlayerPrefs.SetString(k, Sentinel);
                PlayerPrefs.Save();

                clear.Invoke(null, null);

                var survivors = new List<string>();
                foreach (var k in keys) if (PlayerPrefs.HasKey(k)) survivors.Add(k);
                if (survivors.Count > 0)
                    failures.Add($"[prefs-clear] {survivors.Count} equip/loadout key(s) SURVIVED ResetToNewGame's " +
                                 $"clear: {string.Join(", ", survivors)} - each one is a slot that can hand a new " +
                                 "game a piece of gear from the previous one (the owner's 'I keep getting this axe')");
            }
            finally
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                foreach (var kv in snapshot) PlayerPrefs.SetString(kv.Key, kv.Value);
                PlayerPrefs.Save();
            }
        }

        // =====================================================================
        //  CASE 4 - the thinned shelf holds its contract
        // =====================================================================
        private static void Case4_ShelfCap(List<string> failures, List<string> notes)
        {
            var roster = new[] { "knight" };   // pinned: the suite must not depend on ff.knightonly

            foreach (var vendorId in ThinnedVendors)
            {
                var vendor = VendorRegistry.Find(vendorId);
                if (vendor == null)
                {
                    failures.Add($"[shelf-cap] vendors.json has no '{vendorId}' row - the thinned-store settings " +
                                 "have nowhere to live and the shelf falls back to the uncapped legacy heuristic");
                    continue;
                }

                foreach (int level in new[] { 1, 3, 6, 10 })
                {
                    var wares = VendorStockResolver.Resolve(vendorId, "knight", level, roster);
                    if (wares == null)
                    {
                        failures.Add($"[shelf-cap] Resolve('{vendorId}', knight, {level}) returned null");
                        continue;
                    }

                    var perLevel = new Dictionary<int, int>();
                    foreach (var ware in wares)
                    {
                        if (vendor.OnlyEquippable && !ware.Eligible)
                        {
                            // WO-960: the locked PREVIEW window re-admits class-appropriate rows
                            // locked ONLY by level, within (level, level + lockedPreviewLevels].
                            // Anything else locked is still the WO-860 violation.
                            int lockedReq = ReqLevelOf(ware);
                            bool previewOk = vendor.LockedPreviewLevels > 0 &&
                                             ware.LockReason != null &&
                                             ware.LockReason.StartsWith("Requires Lv ", StringComparison.Ordinal) &&
                                             lockedReq > level &&
                                             lockedReq <= level + vendor.LockedPreviewLevels;
                            if (!previewOk)
                                failures.Add($"[shelf-cap] '{vendorId}' Lv{level} returns LOCKED row '{ware.Id}' " +
                                             $"(reason '{ware.LockReason}') outside the WO-960 preview window - " +
                                             "onlyEquippable admits only class-ok level locks within " +
                                             $"lockedPreviewLevels ({vendor.LockedPreviewLevels})");
                        }

                        if (vendor.ExcludeIdPrefixes != null)
                            foreach (var p in vendor.ExcludeIdPrefixes)
                                if (!string.IsNullOrEmpty(p) && ware.Id != null &&
                                    ware.Id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                                    failures.Add($"[shelf-cap] '{vendorId}' Lv{level} stocks excluded id '{ware.Id}' " +
                                                 $"(prefix '{p}') - the ~65 placeholder rows are the whole overload");

                        int req = ReqLevelOf(ware);
                        perLevel.TryGetValue(req, out int n);
                        perLevel[req] = n + 1;
                    }

                    if (vendor.PerLevelCap > 0)
                        foreach (var kv in perLevel)
                            if (kv.Value > vendor.PerLevelCap)
                                failures.Add($"[shelf-cap] '{vendorId}' Lv{level} returns {kv.Value} rows requiring " +
                                             $"level {kv.Key}, over the authored perLevelCap {vendor.PerLevelCap}");

                    if (level == 1 && wares.Count == 0)
                        failures.Add($"[shelf-cap] '{vendorId}' is EMPTY for a level-1 knight - thinning must not " +
                                     "empty the shelf; a founder with nothing to buy is worse than clutter");

                    if (level == 1) notes.Add($"{vendorId}@Lv1 = {wares.Count} row(s)");
                }
            }
        }

        // =====================================================================
        //  CASE 5 - an INDEPENDENT oracle for the documented sort
        // =====================================================================
        private static void Case5_ShelfOracle(List<string> failures)
        {
            var roster = new[] { "knight" };
            var vendor = VendorRegistry.Find("forge");
            if (vendor == null || vendor.PerLevelCap <= 0) return;   // Case 4/6 report the real problem

            const int Level = 1;
            var got = VendorStockResolver.Resolve("forge", "knight", Level, roster);
            var again = VendorStockResolver.Resolve("forge", "knight", Level, roster);
            if (got == null || again == null) return;

            if (!SameIds(got, again))
                failures.Add("[shelf-oracle] two consecutive Resolve calls returned DIFFERENT shelves - the pick is " +
                             "leaking dictionary/enumeration order, so no oracle and no player can pin what the " +
                             "Forge sells");

            // Re-implemented straight from the catalog, deliberately NOT calling the resolver's
            // helpers, so it can DISAGREE rather than inherit a bug: eligible knight weapons,
            // minus excluded prefixes, bucketed by req.level, damageMult DESC, id ORDINAL ASC.
            var candidates = new List<WeaponDef>();
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || string.IsNullOrEmpty(w.id)) continue;
                if (!GearCatalog.WeaponFitsClass(w, "knight")) continue;
                int req = w.req != null ? w.req.level : 1;
                if (req > Level) continue;
                bool excluded = false;
                if (vendor.ExcludeIdPrefixes != null)
                    foreach (var p in vendor.ExcludeIdPrefixes)
                        if (!string.IsNullOrEmpty(p) && w.id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        { excluded = true; break; }
                if (excluded) continue;
                candidates.Add(w);
            }
            candidates.Sort((a, b) =>
            {
                int byLevel = (a.req != null ? a.req.level : 1).CompareTo(b.req != null ? b.req.level : 1);
                if (byLevel != 0) return byLevel;
                int byPower = b.damageMult.CompareTo(a.damageMult);
                if (byPower != 0) return byPower;
                return string.CompareOrdinal(a.id, b.id);
            });

            // WO-1254 changes the capped Forge contract deliberately: each level bucket
            // reserves one main-hand and one off-hand when both exist. Derive that answer
            // independently from catalog flags and the documented power/id ordering.
            var expected = new List<string>();
            var levels = new SortedSet<int>();
            foreach (var w in candidates) levels.Add(w.req != null ? w.req.level : 1);
            foreach (int req in levels)
            {
                WeaponDef main = null, offHand = null;
                foreach (var w in candidates)
                {
                    int rowReq = w.req != null ? w.req.level : 1;
                    if (rowReq != req) continue;
                    if (w.IsOffHandItem) { if (offHand == null) offHand = w; }
                    else if (main == null) main = w;
                }
                if (main != null) expected.Add(main.id);
                if (offHand != null && expected.Count < levels.Count * vendor.PerLevelCap) expected.Add(offHand.id);
            }

            var actual = new List<string>();
            foreach (var ware in got) actual.Add(ware.Id);

            if (string.Join(",", expected) != string.Join(",", actual))
                failures.Add($"[shelf-oracle] Forge Lv1 shelf is [{string.Join(",", actual)}] but the independent " +
                             $"sort oracle says [{string.Join(",", expected)}] - the implementation drifted from the " +
                             "documented rule 'per level: strongest main-hand + strongest off-hand, power DESC/id ordinal'");
        }

        // =====================================================================
        //  CASE 6 - the knobs are DATA, in both copies
        // =====================================================================
        private static void Case6_VendorData(List<string> failures)
        {
            CompareDualCopy(VendorsRes, VendorsSA, "vendors.json", failures);

            int nonAscii = FirstNonAsciiLine(ReadSource(VendorsRes, failures) ?? string.Empty);
            if (nonAscii > 0)
                failures.Add($"[vendor-data] non-ASCII character at line {nonAscii} of vendors.json - its emptyLine/" +
                             "footerLine are rendered in TMP and would come out as tofu on device");

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(VendorsRes)); }
            catch (Exception ex)
            {
                failures.Add($"[vendor-data] vendors.json failed to parse ({ex.GetType().Name}: {ex.Message})");
                return;
            }

            var arr = root["vendors"] as JArray;
            if (arr == null)
            {
                failures.Add("[vendor-data] vendors.json has no 'vendors' array");
                return;
            }

            foreach (var vendorId in ThinnedVendors)
            {
                JObject row = null;
                foreach (var v in arr)
                    if (string.Equals((string)v["id"], vendorId, StringComparison.OrdinalIgnoreCase))
                    { row = (JObject)v; break; }

                if (row == null)
                {
                    failures.Add($"[vendor-data] vendors.json has no '{vendorId}' row");
                    continue;
                }

                // The WO-860 acceptance is explicit: these are TUNABLE WITHOUT A RECOMPILE.
                // Living in C# would satisfy the behaviour and fail the contract.
                foreach (var field in new[] { "onlyEquippable", "perLevelCap", "footerLine", "excludeIdPrefixes" })
                    if (row[field] == null)
                        failures.Add($"[vendor-data] '{vendorId}' has no '{field}' in vendors.json - the store can " +
                                     "no longer be retuned as data (WO-860 acceptance: 'no recompile to retune')");

                if (row["perLevelCap"] != null && (int)row["perLevelCap"] <= 0)
                    failures.Add($"[vendor-data] '{vendorId}'.perLevelCap = {(int)row["perLevelCap"]} - a " +
                                 "non-positive cap means uncapped, i.e. the owner's '2 options per level' is off");

                var footer = (string)row["footerLine"];
                if (string.IsNullOrEmpty(footer))
                    failures.Add($"[vendor-data] '{vendorId}'.footerLine is empty - once the shelf is capped the " +
                                 "player has no 'come back after leveling for new stock' cue at all");

                // The live registry must AGREE with the raw JSON (catches a loader that parses
                // the file but drops the new fields - schema drift the eye cannot see).
                var def = VendorRegistry.Find(vendorId);
                if (def == null)
                    failures.Add($"[vendor-data] VendorRegistry.Find('{vendorId}') returned null even though the " +
                                 "JSON row exists (loader/schema drift)");
                else
                {
                    if (row["perLevelCap"] != null && def.PerLevelCap != (int)row["perLevelCap"])
                        failures.Add($"[vendor-data] '{vendorId}' perLevelCap is {(int)row["perLevelCap"]} in JSON " +
                                     $"but {def.PerLevelCap} on the loaded VendorDef - the [JsonProperty] name is wrong");
                    if (row["onlyEquippable"] != null && def.OnlyEquippable != (bool)row["onlyEquippable"])
                        failures.Add($"[vendor-data] '{vendorId}' onlyEquippable did not load onto VendorDef");
                    if (!string.IsNullOrEmpty(footer) && def.FooterLine != footer)
                        failures.Add($"[vendor-data] '{vendorId}' footerLine did not load onto VendorDef");
                    // af96fe788 (2026-08-14, WO-500 D2 Option A) deliberately EMPTIED the
                    // forge's exclusion to surface the finished 65 - so "must be non-empty"
                    // became a stale ruling this oracle was silently re-litigating. The
                    // falsifiable assertion is AGREEMENT: the loaded list matches the JSON
                    // array exactly, which still catches a loader that drops the field.
                    if (row["excludeIdPrefixes"] is JArray jsonPrefixes)
                    {
                        var authored = new List<string>();
                        foreach (var t in jsonPrefixes) authored.Add((string)t);
                        var loaded = def.ExcludeIdPrefixes ?? new List<string>();
                        bool agree = loaded.Count == authored.Count;
                        if (agree)
                            foreach (var p in authored)
                                if (!loaded.Contains(p)) { agree = false; break; }
                        if (!agree)
                            failures.Add($"[vendor-data] '{vendorId}' excludeIdPrefixes JSON " +
                                         $"[{string.Join(",", authored)}] != loaded VendorDef " +
                                         $"[{string.Join(",", loaded)}] - the loader dropped or mangled the field");
                    }
                }

                if (VendorStockResolver.FooterLineFor(vendorId) != footer)
                    failures.Add($"[vendor-data] VendorStockResolver.FooterLineFor('{vendorId}') does not return the " +
                                 "authored line - the VM/View has nothing correct to bind");
            }
        }

        // =====================================================================
        //  CASE 7 - ONE definition of "who is playable"
        // =====================================================================
        private static void Case7_RosterTruth(List<string> failures)
        {
            if (!PlayableHeroes.IsPlayable(PlayableHeroes.Default))
                failures.Add("[roster-truth] PlayableHeroes.Default is not itself playable - ChooseHero would coerce " +
                             "to a class the select screen renders LOCKED, i.e. an unenterable game");
            if (PlayableHeroes.All == null || PlayableHeroes.All.Count == 0)
                failures.Add("[roster-truth] PlayableHeroes.All is EMPTY - no hero could be chosen at all");
            if (PlayableHeroes.JobKey(HeroClass.Knight) != "knight")
                failures.Add("[roster-truth] PlayableHeroes.JobKey(Knight) != 'knight' - the shelf roster filter and " +
                             "the per-class PlayerPrefs keys both depend on this exact string");

            var keys = PlayableHeroes.JobKeys();
            if (keys == null || keys.Count != PlayableHeroes.All.Count)
                failures.Add("[roster-truth] PlayableHeroes.JobKeys() does not mirror All - the vendor shelf and the " +
                             "select screen would disagree about the roster size");

            // The three former copies of the rule must be GONE, not merely shadowed.
            string gs = ReadSource(GameStateSrc, failures);
            if (gs != null && gs.IndexOf("if (DeNelle.Core.FeatureFlags.KnightOnly) cls = HeroClass.Knight",
                                         StringComparison.Ordinal) >= 0)
                failures.Add("[roster-truth] GameStateService.ChooseHero still hardcodes the Knight force - a " +
                             "confirmed non-Knight would silently become a Knight no matter what the roster says");

            // Match the DECLARATION, not the word: the file's header legitimately explains what
            // the retired FullRoster used to be, and a lint that trips on its own documentation
            // teaches everyone to stop documenting.
            string rs = ReadSource(ResolverSrc, failures);
            if (rs != null && rs.IndexOf("string[] FullRoster", StringComparison.Ordinal) >= 0)
                failures.Add("[roster-truth] VendorStockResolver still declares its own FullRoster array - a second " +
                             "roster truth means the store can stock (or hide) gear for a hero the select screen " +
                             "disagrees about");
            if (rs != null && rs.IndexOf("PlayableHeroes.JobKeys", StringComparison.Ordinal) < 0)
                failures.Add("[roster-truth] VendorStockResolver.RosterClasses does not delegate to " +
                             "PlayableHeroes.JobKeys - the shelf is following something other than the playable set");

            string hs = ReadSource(HeroSelectSrc, failures);
            if (hs != null && hs.IndexOf("const HeroClass PlayableHero", StringComparison.Ordinal) >= 0)
                failures.Add("[roster-truth] HeroSelectController still declares its own PlayableHero const - " +
                             "unlocking a hero would need three coordinated edits with no compiler help");
            if (hs != null && hs.IndexOf("PlayableHeroes.IsPlayable", StringComparison.Ordinal) < 0)
                failures.Add("[roster-truth] HeroSelectController.IsPlayable does not delegate to PlayableHeroes");
        }

        // =====================================================================
        //  CASE 8 - the W/E/R bar is per class (and the knight's key is unchanged)
        // =====================================================================
        private static void Case8_LoadoutKey(List<string> failures)
        {
            const string RetiredGlobalKey = "dotr-loadout-knight-v1";

            string knight = HeroLoadout.PrefsKeyFor("knight");
            string ranger = HeroLoadout.PrefsKeyFor("ranger");
            string mage = HeroLoadout.PrefsKeyFor("mage");

            if (knight != RetiredGlobalKey)
                failures.Add($"[loadout-key] the knight's loadout key is '{knight}', not the retired global " +
                             $"'{RetiredGlobalKey}' - every existing save's Knight W/E/R bar would silently reset to " +
                             "the stock kit on first launch after this change (that identity IS the migration)");

            if (knight == ranger || knight == mage || ranger == mage)
                failures.Add($"[loadout-key] loadout keys COLLIDE (knight='{knight}' ranger='{ranger}' mage='{mage}') " +
                             "- every hero would load, and overwrite, the same bar, so Sylas would spawn holding " +
                             "Grom's melee kit");

            if (HeroLoadout.PrefsKeyFor("KNIGHT") != knight)
                failures.Add("[loadout-key] PrefsKeyFor is case-SENSITIVE - HeroAbilities lowercases its class but a " +
                             "companion binding could not, and the two would read different bars");

            if (EquipPrefKeys.LoadoutKeyFor("knight") != knight)
                failures.Add("[loadout-key] HeroLoadout and EquipPrefKeys disagree on the key shape - the New Game " +
                             "reset would delete a key nobody writes and leave the real one behind");

            // The equip-slot prefixes must all be distinct, or one slot's clear would eat another's.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in EquipPrefKeys.AllSlotPrefixes)
            {
                if (string.IsNullOrEmpty(p))
                    failures.Add("[loadout-key] EquipPrefKeys.AllSlotPrefixes contains an empty prefix");
                else if (!seen.Add(p))
                    failures.Add($"[loadout-key] duplicate equip-slot prefix '{p}'");
            }
            if (seen.Count < 5)
                failures.Add($"[loadout-key] only {seen.Count} equip-slot prefixes are registered - weapon, armor, " +
                             "off-hand, ring and amulet are all persisted, so a reset that knows fewer leaves stale gear");
        }

        // =====================================================================
        //  CASE 9 - auto-best may only rank gear the player OWNS
        // =====================================================================
        //  THE DEFECT (captured Player.log, 2026-08-08):
        //      [Flow:Gear] Refresh: job='knight' level=2 bestWeapon='knight_flameblade'
        //                  source=auto-best offHand='<null>' bestArmor='armor_knight_common'
        //  The authored starter kit only applies while level <= MainHandUpToLevel (== 1). At
        //  level 2 GearCatalog.BestWeapon took over, and it ranked by damageMult across the
        //  WHOLE CATALOG - so it returned `knight_flameblade` (1.2) over `knight_starter` (1.0).
        //  knight_flameblade is a PURCHASABLE Forge item. Every knight who levelled once without
        //  ever shopping was handed a paid weapon free, undercutting the Forge economy for every
        //  player; it is why the owner opened her demo recording holding a flaming sword.
        //
        //  THE RULING: auto-upgrade-on-level-up STAYS (WO-860's intended feature). Its CANDIDATE
        //  SET is what changes - auto-best may only rank OWNED gear.
        //
        //  WHAT THIS CASE CAN AND CANNOT DRIVE. The end-to-end statement is "a LEVEL-5 hero who
        //  owns ONLY the starter kit holds the starter weapon". A live GearLoadout probe cannot
        //  express the level half: level comes from HeroProgression, which has no public setter
        //  and restores from a GameStateService that is null in batchmode - so a probe is pinned
        //  at level 1, where the starter wins anyway and the assertion would be VACUOUS. This case
        //  therefore splits the statement in two and asserts BOTH halves rather than pretending:
        //    (a) the RANKING, driven for real at level 5 through the shipped GearCatalog.
        //        PickBestWeapon with a starter-kit-only ownership predicate;
        //    (b) the WIRING, source-linted, so the ranking cannot be correct while GearLoadout
        //        still calls the catalog-wide query. (a) without (b) passes on a dead code path.
        // =====================================================================
        private static void Case9_AutoBestOwnedOnly(List<string> failures, List<string> notes)
        {
            const int Level = 5;   // above every authored MainHandUpToLevel: auto-best territory

            foreach (var job in AuthoredStarterClasses)
            {
                var kit = StarterLoadout.For(job);
                if (kit == null || string.IsNullOrEmpty(kit.MainHand)) continue;   // Case 1 owns that failure

                // The owned set of a player who has NEVER shopped: the GRANTED starter kit and
                // nothing else. The kit is deliberately included - it is never written to
                // VillageInventory (it is granted, not purchased), so if the shipped ownership
                // resolver forgot it the hero's own starter would be filtered out of its own
                // fallback, which is the one way this fix could leave a hero unarmed.
                var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kit.MainHand };
                if (!string.IsNullOrEmpty(kit.OffHand)) ownedIds.Add(kit.OffHand);

                var wide  = GearCatalog.PickBestWeapon(job, Level, null);
                var owned = GearCatalog.PickBestWeapon(job, Level, id => ownedIds.Contains(id ?? string.Empty));

                if (!owned.OwnershipApplied)
                    failures.Add($"[auto-best-owned-only] PickBestWeapon('{job}', {Level}, <predicate>) reports " +
                                 "OwnershipApplied=false - a predicate was supplied and IGNORED, so every caller " +
                                 "that thinks it is filtering is still ranking the whole paid catalog");

                if (owned.Weapon == null)
                {
                    failures.Add($"[auto-best-owned-only] '{job}' at level {Level} owning ONLY the starter kit " +
                                 $"({string.Join("+", new List<string>(ownedIds).ToArray())}) resolves a NULL " +
                                 "main hand - the ownership gate has made the hero WEAPONLESS, which is a far " +
                                 "worse ship-day bug than the free flameblade it was added to stop");
                }
                else if (!string.Equals(owned.Weapon.id, kit.MainHand, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[auto-best-owned-only] '{job}' at level {Level} owning ONLY the starter kit " +
                                 $"resolves '{owned.Weapon.id}', not the starter '{kit.MainHand}' - the owned set " +
                                 "contains two ids and the winner is neither of the two the player has; the " +
                                 "ownership predicate is not gating the ranking loop");
                }

                if (owned.Owned > ownedIds.Count)
                    failures.Add($"[auto-best-owned-only] '{job}' L{Level}: PickBestWeapon counted {owned.Owned} " +
                                 $"owned candidates from an owned set of {ownedIds.Count} id(s) - the filter is " +
                                 "letting unowned rows through the count, so the trace line would lie about it");

                // Is the guard even doing anything? If the catalog-wide pick ALREADY returns the
                // starter at this level, this case can no longer catch the felt bug. Reported as a
                // NOTE (a legitimate catalog retune) exactly as Case 2 does, never as a silent pass.
                if (wide.Weapon == null)
                {
                    failures.Add($"[auto-best-owned-only] catalog-wide PickBestWeapon('{job}', {Level}) is NULL - " +
                                 "the class has no eligible main-hand row at all at this level, so nothing about " +
                                 "the ownership gate can be judged and the armed-hero floor has nothing to fall to");
                }
                else if (string.Equals(wide.Weapon.id, kit.MainHand, StringComparison.OrdinalIgnoreCase))
                {
                    notes.Add($"'{job}' L{Level}: catalog-wide auto-best ALREADY returns the starter " +
                              $"'{kit.MainHand}', so this case is currently vacuous - the free-shop-item leak it " +
                              "pins would not be caught by it anymore");
                }
                else
                {
                    notes.Add($"'{job}' L{Level}: catalog-wide would hand over '{wide.Weapon.id}' " +
                              $"(dmgMult {wide.Weapon.damageMult:0.00}, {wide.Eligible} eligible rows); owned-gated " +
                              $"returns '{owned.Weapon?.id}' from {owned.Owned} owned candidate(s)");
                }
            }

            // ---- (b) the WIRING: GearLoadout must actually use the gated query ----
            string src = ReadSource(LoadoutSrc, failures);
            if (src == null) return;
            string code = StripComments(src);   // prose about the old call must never satisfy a lint

            if (Regex.IsMatch(code, @"starter\s*\?\?\s*GearCatalog\.BestWeapon"))
                failures.Add("[auto-best-owned-only] GearLoadout.Refresh still reads " +
                             "`starter ?? GearCatalog.BestWeapon(job, level)` - that is the catalog-wide, " +
                             "damageMult-only pick verbatim, i.e. the original defect has returned and the " +
                             "level-2 knight is handed the Forge's flameblade again");

            if (code.IndexOf("ResolveAutoBestMainHand", StringComparison.Ordinal) < 0)
                failures.Add("[auto-best-owned-only] GearLoadout has no ResolveAutoBestMainHand - the ownership " +
                             "gate is not wired into the main-hand resolution chain at all, so case (a) above is " +
                             "asserting a code path the hero never takes");

            if (!Regex.IsMatch(code, @"PickBestWeapon\s*\([^;]*Owns"))
                failures.Add("[auto-best-owned-only] nothing in GearLoadout passes an ownership predicate to " +
                             "GearCatalog.PickBestWeapon - the gated overload exists but is called with null, " +
                             "which is byte-identical to the unfixed behaviour");

            // The hand-slot refill is the SECOND door onto the same paid shelf: EnforceHandSlots
            // refills the main hand after evicting a shield / dropping a conflicting 2H, and both
            // of its refill sites used to call the catalog-wide 1H query directly.
            //
            // Counted, not pattern-matched on the call site, because the fix KEEPS exactly one
            // catalog-wide call of each kind as the deliberate never-weaponless FLOOR - a lint
            // that banned the call outright would fail on the very safety net it should protect.
            // The invariant that actually matters is "there is only ONE of them, and it is the
            // floor": a second occurrence means someone re-opened a direct path.
            int wideOneHanded = CountOccurrences(code, "GearCatalog.BestOneHandedWeapon");
            if (wideOneHanded != 1)
                failures.Add($"[auto-best-owned-only] GearLoadout calls GearCatalog.BestOneHandedWeapon " +
                             $"{wideOneHanded} time(s); exactly ONE is expected (the never-weaponless floor inside " +
                             "ResolveOwnedOneHandedRefill). More than one means a hand-slot refill went back to an " +
                             "unowned catalog-wide pick, so a shield eviction or a 2H conflict re-opens the " +
                             "free-gear door Refresh just closed; zero means the floor itself was deleted");

            int wideBest = CountOccurrences(code, "GearCatalog.BestWeapon");
            if (wideBest != 1)
                failures.Add($"[auto-best-owned-only] GearLoadout calls GearCatalog.BestWeapon {wideBest} time(s); " +
                             "exactly ONE is expected (the floor inside StarterOrCatalogFloor, for classes with no " +
                             "authored starter kit). A second call is the ungated damageMult pick coming back");

            // Both EnforceHandSlots refill sites must route through the gated helper (1 declaration
            // + 2 call sites). Fewer means one of them slipped back to a direct catalog query.
            int refillUses = CountOccurrences(code, "ResolveOwnedOneHandedRefill");
            if (refillUses < 3)
                failures.Add($"[auto-best-owned-only] ResolveOwnedOneHandedRefill appears {refillUses} time(s) in " +
                             "GearLoadout; 3 are expected (its declaration plus BOTH EnforceHandSlots refill sites: " +
                             "the shield-evicted main hand and the 2H-vs-off-hand conflict). One unrouted site is " +
                             "enough to hand a knight a Forge weapon he never bought");

            // And the never-weaponless floor must still exist: an ownership gate with no floor is
            // how "no free flameblade" becomes "no weapon at all".
            if (code.IndexOf("StarterOrCatalogFloor", StringComparison.Ordinal) < 0)
                failures.Add("[auto-best-owned-only] GearLoadout has no StarterOrCatalogFloor - the ownership gate " +
                             "has no fallback for an empty/unresolvable owned set, so a hero whose bag the resolver " +
                             "cannot read spawns with EMPTY HANDS");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>Strips // and block comments so a source lint can never be satisfied by prose
        /// (this file's own headers name the retired calls it lints for).</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        /// <summary>Non-overlapping occurrences of a literal needle. Ordinal.</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static int ReqLevelOf(VendorWare ware)
        {
            if (ware.Kind == VendorWareKind.Weapon)
            {
                var w = GearCatalog.FindWeapon(ware.Id);
                return w != null && w.req != null ? w.req.level : 1;
            }
            var a = GearCatalog.FindArmor(ware.Id);
            return a != null && a.req != null ? a.req.level : 1;
        }

        private static bool SameIds(IReadOnlyList<VendorWare> a, IReadOnlyList<VendorWare> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i].Id, b[i].Id, StringComparison.Ordinal)) return false;
            return true;
        }

        private static void CompareDualCopy(string resPath, string saPath, string label, List<string> failures)
        {
            if (!File.Exists(resPath)) { failures.Add($"[dual-copy] {label} Resources copy missing: {resPath}"); return; }
            if (!File.Exists(saPath)) { failures.Add($"[dual-copy] {label} StreamingAssets copy missing: {saPath}"); return; }
            string na = Normalize(File.ReadAllText(resPath));
            string nb = Normalize(File.ReadAllText(saPath));
            if (na != nb)
                failures.Add($"[dual-copy] {label} DRIFT: Resources({na.Length}b) != StreamingAssets({nb.Length}b) - " +
                             "the shipped player loads Resources and the tools edit StreamingAssets, so the editor " +
                             "and the device would disagree about the shelf");
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[source] {path} not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add($"[source] could not read {path}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static int FirstNonAsciiLine(string src)
        {
            int line = 1;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '\n') { line++; continue; }
                if (c > (char)126 && c != '\r') return line;
            }
            return 0;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1);
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
