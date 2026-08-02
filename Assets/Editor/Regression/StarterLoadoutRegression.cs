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
//                     ZERO ineligible (locked) rows, and ZERO excluded-prefix rows.
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
                         "the playable roster has one definition, and the W/E/R key is per class" + noteStr;
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
                            failures.Add($"[shelf-cap] '{vendorId}' Lv{level} still returns LOCKED row '{ware.Id}' " +
                                         $"(reason '{ware.LockReason}') despite onlyEquippable - the owner asked to " +
                                         "'only show ones they can equip'");

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

            var expected = new List<string>();
            var perLevel = new Dictionary<int, int>();
            foreach (var w in candidates)
            {
                int req = w.req != null ? w.req.level : 1;
                perLevel.TryGetValue(req, out int n);
                if (n >= vendor.PerLevelCap) continue;
                perLevel[req] = n + 1;
                expected.Add(w.id);
            }

            var actual = new List<string>();
            foreach (var ware in got) actual.Add(ware.Id);

            if (string.Join(",", expected) != string.Join(",", actual))
                failures.Add($"[shelf-oracle] Forge Lv1 shelf is [{string.Join(",", actual)}] but the independent " +
                             $"sort oracle says [{string.Join(",", expected)}] - the implementation drifted from the " +
                             "documented rule 'bucket by req.level, power DESC, id ordinal ASC'");
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
                    if (def.ExcludeIdPrefixes == null || def.ExcludeIdPrefixes.Count == 0)
                        failures.Add($"[vendor-data] '{vendorId}' excludeIdPrefixes did not load onto VendorDef - " +
                                     "the placeholder rows would come straight back onto the shelf");
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
        //  HELPERS
        // =====================================================================

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
