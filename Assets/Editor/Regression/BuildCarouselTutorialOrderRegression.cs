// =============================================================================
// BuildCarouselTutorialOrderRegression [build-carousel-order]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-963 (owner ask 2026-08-10, F8 seq 2302, verbatim: "Can we order the carousel in
// order of how the tutorial presents them?"). The build palette had NO sort at all --
// the carousel order was simply the authoring order of entries[] in
// structures-catalog.json. It now sorts on a catalog-authored, owner-tunable
// CatalogEntry.displayOrder, seeded to the tutorial's teaching order.
//
// THE ARCHITECTURAL POINT THIS SUITE EXISTS TO DEFEND (WO-963 sec3):
//   The palette must NEVER read tutorial-steps.json at runtime. Presentation does not
//   take a teaching script as an input (ARCHITECTURE_PRINCIPLES sec1/sec2) -- a step
//   rename would silently reshuffle the shop shelf. So the CATALOG carries the order,
//   the tutorial simply AGREES with it, and THIS SUITE is the thing that keeps them
//   agreeing: a future tutorial re-author that changes what is taught first fails the
//   gate instead of quietly disagreeing with the shelf.
//
// WHAT IT PROVES:
//   (1) dual-copy      - the two structures-catalog.json copies are BYTE-identical and
//                        the version was bumped past the pre-WO-963 15.
//   (2) authored-order - the SHELF ids (collector_lumbermill -> workshop -> forge)
//                        all carry a displayOrder, strictly ascending in that order,
//                        and the Lumbermill holds the LOWEST authored order in the file
//                        (it is what the tutorial teaches first). Parsed through the
//                        PRODUCTION serializer settings, so a field that does not
//                        actually deserialize reads as unauthored and fails here.
//   (3) tutorial-agree - the anchors are still IN tutorial-steps.json and still in that
//                        relative order: step order 20 highlights
//                        'build.card.collector_lumbermill'; the armor nudge triggers on
//                        'build.structure_placed:forge' (the WEAPONS roof); that nudge's
//                        objective names the catalog displayName of 'armorer', and never
//                        the weapons shop's.
//                        ⛔ RE-POINTED 2026-08-23 (owner: iron is the ARMORER's resource).
//                        This case used to demand the trigger 'workshop' and the word on
//                        row 'forge'. Both were the CROSSED-label state: vendors.json has
//                        'forge' selling weapons and 'workshop' selling nothing. Nothing
//                        was relaxed - the case still fails if the armour beat stops
//                        following the weapons roof, or starts naming the weapons shop.
//   (4) stable-sort    - the REAL shipped seam (BuildPaletteVM.SortForDisplay) is driven
//                        over a synthetic list AND over the live catalog: authored rows
//                        lead in ascending order, and every UNAUTHORED row keeps its
//                        exact relative position (WO-963 acceptance 3 -- rows are
//                        deliberately left unauthored and asserted).
//   (5) no-tutorial-read - source lint: BuildPaletteVM names no tutorial symbol/file, and
//                        Rebuild still routes its rows through SortForDisplay.
//
// DELIBERATELY NOT ASSERTED: any TOWER displayOrder. The tutorial's defense beat (step
// order 30) highlights a TAB ('build.tab_defenses'), not a tower id, and
// tower_ground_archer is already the first Tower row in the catalog, so the Defense
// group teaches the archer tower first with no key authored. Case (2) pins that
// tower-first property instead, so a reshuffle of the tower rows still fails here.
//
// NOT provable here: how the carousel FEELS to scroll. That is the owner's felt-verify
// (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: BUILD_CAROUSEL_ORDER_OK / BUILD_CAROUSEL_ORDER_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.BuildCarouselTutorialOrderRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class BuildCarouselTutorialOrderRegression
    {
        private const string ResCatalog = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string SaCatalog  = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string TutorialSteps = "Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json";
        private const string PaletteSrc = "Assets/_Modules/Village/BuildMode/BuildPaletteVM.cs";

        /// <summary>The version the WO-963 seed shipped at; the pre-WO-963 file was 15.</summary>
        private const int MinCatalogVersion = 16;

        private const string LumbermillId = "collector_lumbermill";

        // ── The 2c-bis nudge chain, RE-POINTED 2026-08-23 (WO-1161 follow-up) ──
        // Owner ruling that day: iron is the ARMORER's resource. Straightening the catalog
        // names from vendors.json (function is the authority) showed that the tutorial's
        // armour beat had the crossing baked in: it triggered on 'workshop' - which sells
        // NOTHING, it is the crafting station - and pointed the armour nudge at 'forge',
        // which sells WEAPONS. The beat only ever read correctly because the labels were
        // crossed to match it. The truthful chain is:
        //     weapons roof = 'forge'   (role weaponsmith)
        //  -> then armour  = 'armorer' (role armorer)
        /// <summary>The row whose placement is the weapons roof - the armour nudge's trigger.</summary>
        private const string WeaponsShopId = "forge";
        /// <summary>The row the armour nudge points at; its displayName is the word taught.</summary>
        private const string ArmorerId     = "armorer";

        /// <summary>
        /// The order the CATALOG authors on displayOrder, which is what cases 2 and 4 assert.
        /// ⚠ This is the SHELF order, not the nudge chain above: `workshop` carries
        /// displayOrder 20 in structures-catalog.json and the two must keep agreeing or the
        /// carousel opens on rows nobody seeded. Re-seeding the shelf to the corrected chain
        /// is a CATALOG edit (and forces a CatalogFallbackGenerator re-run, WO-1137), so it
        /// is deliberately not smuggled in here - but nothing was weakened either: these
        /// cases still pin the authored head exactly as they always did.
        /// </summary>
        private static readonly string[] AuthoredCarouselOrder = { LumbermillId, "workshop", WeaponsShopId };

        [Serializable]
        private sealed class CatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BUILD_CAROUSEL_ORDER_OK - " + reason);
            else Debug.LogError("BUILD_CAROUSEL_ORDER_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                var catalog = ParseCatalog(failures);
                Case(failures, "dual-copy",         () => Case1_DualCopy(catalog, failures));
                Case(failures, "authored-order",    () => Case2_AuthoredOrder(catalog, failures));
                Case(failures, "tutorial-agree",    () => Case3_TutorialAgreement(catalog, failures));
                Case(failures, "stable-sort",       () => Case4_StableSort(catalog, failures));
                Case(failures, "no-tutorial-read",  () => Case5_NoTutorialRead(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "BUILD CAROUSEL ORDER OK - both structures-catalog copies are byte-identical " +
                         "at v" + MinCatalogVersion + "+, the authored displayOrder runs " +
                         string.Join(" -> ", AuthoredCarouselOrder) + ", tutorial-steps.json still opens on " +
                         "the Lumbermill and still teaches weapons ('" + WeaponsShopId + "') before armour ('" +
                         ArmorerId + "'), the palette reads NO tutorial data, and BuildPaletteVM.SortForDisplay " +
                         "leads with the authored rows while every unauthored row keeps its catalog " +
                         "position (stable).";
                return true;
            }
            reason = "build-carousel-order FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Parse - through the PRODUCTION serializer settings
        // =====================================================================

        // CatalogBootstrap.LoadFromJson's settings VERBATIM. Parsing any other way would
        // green-tick a displayOrder that the shipping loader silently drops.
        private static CatalogFile ParseCatalog(List<string> failures)
        {
            if (!File.Exists(ResCatalog))
            {
                failures.Add("[parse] " + ResCatalog + " is MISSING - the palette has no catalog to order.");
                return null;
            }
            var settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            var file = JsonConvert.DeserializeObject<CatalogFile>(File.ReadAllText(ResCatalog), settings);
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[parse] " + ResCatalog + " parsed to ZERO entries through the production settings.");
                return null;
            }
            return file;
        }

        private static CatalogEntry Find(CatalogFile file, string id)
        {
            if (file == null || file.Entries == null) return null;
            foreach (var e in file.Entries)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        // =====================================================================
        //  Case 1 - DUAL-COPY LAW + version bump
        // =====================================================================

        private static void Case1_DualCopy(CatalogFile file, List<string> failures)
        {
            if (!File.Exists(SaCatalog))
            {
                failures.Add("[dual-copy] " + SaCatalog + " is MISSING - the StreamingAssets copy is the " +
                             "non-WebGL source and must exist alongside the Resources copy.");
                return;
            }
            byte[] a = File.ReadAllBytes(ResCatalog);
            byte[] b = File.ReadAllBytes(SaCatalog);
            bool same = a.Length == b.Length;
            if (same)
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) { same = false; break; }
            if (!same)
                failures.Add("[dual-copy] the two structures-catalog.json copies DIFFER (" + a.Length +
                             " vs " + b.Length + " bytes) - Resources WINS at runtime, so an editor tool " +
                             "reading StreamingAssets would order the shelf differently from the game.");

            if (file != null && file.Version < MinCatalogVersion)
                failures.Add("[dual-copy] structures-catalog version is " + file.Version + ", expected >= " +
                             MinCatalogVersion + " - the WO-963 displayOrder seed must ship with a bumped version.");
        }

        // =====================================================================
        //  Case 2 - the AUTHORED order
        // =====================================================================

        private static void Case2_AuthoredOrder(CatalogFile file, List<string> failures)
        {
            if (file == null) return;

            int previous = 0;
            string previousId = null;
            foreach (var id in AuthoredCarouselOrder)
            {
                var e = Find(file, id);
                if (e == null)
                {
                    failures.Add("[authored-order] the carousel is seeded on '" + id + "' but no such catalog row " +
                                 "exists - the shelf cannot present a row that is not there.");
                    continue;
                }
                if (e.displayOrder <= 0)
                {
                    failures.Add("[authored-order] '" + id + "' carries NO displayOrder (" + e.displayOrder +
                                 ") - the shelf is seeded on it, so it must be authored or it sorts to the tail " +
                                 "with the unauthored rows.");
                    continue;
                }
                if (previousId != null && e.displayOrder <= previous)
                    failures.Add("[authored-order] '" + id + "' has displayOrder " + e.displayOrder +
                                 " which is NOT after '" + previousId + "' (" + previous + ") - the catalog " +
                                 "order contradicts the seeded shelf order " + string.Join(" -> ", AuthoredCarouselOrder) + ".");
                previous = e.displayOrder;
                previousId = id;
            }

            // The first thing taught must be the first thing on the shelf: nothing else in the
            // whole catalog may claim a lower authored order.
            var lumber = Find(file, LumbermillId);
            if (lumber != null && lumber.displayOrder > 0)
                foreach (var e in file.Entries)
                    if (e != null && e.displayOrder > 0 && e.displayOrder < lumber.displayOrder)
                        failures.Add("[authored-order] '" + e.id + "' (displayOrder " + e.displayOrder +
                                     ") outranks the Lumbermill (" + lumber.displayOrder + ") - the tutorial's " +
                                     "FIRST placement must be the carousel's first card.");

            // The defense beat names a TAB, not a tower id, so the Defense group teaches by row
            // order: the archer tower must stay the first Tower row (see the header note on why
            // no tower carries a displayOrder).
            foreach (var e in file.Entries)
            {
                if (e == null || e.type != CatalogType.Tower) continue;
                if (!string.Equals(e.id, "tower_ground_archer", StringComparison.OrdinalIgnoreCase))
                    failures.Add("[authored-order] the first Tower row is '" + e.id + "', not " +
                                 "'tower_ground_archer' - the tutorial's defense beat sends the player to the " +
                                 "Defense tab expecting the archer tower to lead it. Either restore the row " +
                                 "order or author displayOrder on the towers. WO-1137: there is no longer a " +
                                 "hand-written mirror to keep in step -- CatalogBootstrap.RegisterFallback " +
                                 "EMBEDS this file verbatim -- but you must re-run " +
                                 "DeNelle.Editor.CatalogFallbackGenerator.Generate afterwards or " +
                                 "BuildEconomyRegression gate 12 goes red on staleness.");
                break;
            }
        }

        // =====================================================================
        //  Case 3 - the tutorial still teaches what the catalog claims
        // =====================================================================

        private static void Case3_TutorialAgreement(CatalogFile file, List<string> failures)
        {
            if (file == null) return;
            if (!File.Exists(TutorialSteps))
            {
                failures.Add("[tutorial-agree] " + TutorialSteps + " is MISSING - the order the catalog is " +
                             "seeded to cannot be verified.");
                return;
            }

            var root = JObject.Parse(File.ReadAllText(TutorialSteps));
            var steps = root["steps"] as JArray;
            if (steps == null || steps.Count == 0)
            {
                failures.Add("[tutorial-agree] tutorial-steps.json has no steps[] array.");
                return;
            }

            // Anchor A - the FIRST placement beat highlights the Lumbermill card by id.
            int lumberStepOrder = int.MinValue;
            foreach (var s in steps)
            {
                var hl = s["highlight"] as JArray;
                if (hl == null) continue;
                foreach (var h in hl)
                    if (string.Equals((string)h, "build.card." + LumbermillId, StringComparison.OrdinalIgnoreCase))
                        lumberStepOrder = (int?)s["order"] ?? int.MinValue;
                if (lumberStepOrder != int.MinValue) break;
            }
            if (lumberStepOrder == int.MinValue)
                failures.Add("[tutorial-agree] NO tutorial step highlights 'build.card." + LumbermillId +
                             "' any more - the catalog seeds the Lumbermill first BECAUSE the tutorial " +
                             "taught it first. Re-seed structures-catalog displayOrder to whatever is now " +
                             "taught first (WO-963 sec3: the two must agree, and the palette must NOT read " +
                             "this file at runtime).");

            // Anchor B - the armour nudge fires off the WEAPONS ROOF's placement signal.
            // RE-POINTED 2026-08-23: that roof is catalog row 'forge' (role weaponsmith),
            // not 'workshop' (the crafting station, which sells nothing). vendors.json is
            // the authority; the old contract was written against the crossed labels.
            JToken armorStep = null;
            foreach (var s in steps)
            {
                string sig = (string)(s["trigger"] != null ? s["trigger"]["signal"] : null);
                if (string.Equals(sig, "build.structure_placed:" + WeaponsShopId, StringComparison.OrdinalIgnoreCase))
                { armorStep = s; break; }
            }
            if (armorStep == null)
            {
                failures.Add("[tutorial-agree] NO tutorial step triggers on 'build.structure_placed:" +
                             WeaponsShopId + "' - the weapons-roof-then-armourer teaching chain is gone. " +
                             "The armour nudge must follow the placement of the row that actually sells " +
                             "weapons (role '" + StructureRole.Weaponsmith + "').");
                return;
            }

            int armorStepOrder = (int?)armorStep["order"] ?? int.MinValue;
            if (lumberStepOrder != int.MinValue && armorStepOrder != int.MinValue &&
                armorStepOrder <= lumberStepOrder)
                failures.Add("[tutorial-agree] the armorer nudge (order " + armorStepOrder + ") is now taught " +
                             "BEFORE the Lumbermill beat (order " + lumberStepOrder + ") - the catalog's " +
                             "displayOrder still puts the Lumbermill first.");

            // Anchor C - that nudge names the ARMOURER: the displayName of catalog row
            // 'armorer'. RE-POINTED 2026-08-23 - it used to read the word off row 'forge',
            // which is the WEAPONS shop; the beat only agreed because the labels were crossed.
            // The word itself is never hardcoded: it comes off whichever row the catalog says
            // it is, so a rename carries this oracle with it.
            var armorer = Find(file, ArmorerId);
            var weapons = Find(file, WeaponsShopId);
            string objective = (string)(armorStep["objective"] != null ? armorStep["objective"]["text"] : null) ?? "";

            // The two rows must still BE what the chain assumes - function is the authority
            // (vendors.json), and the role field is where that is recorded. If these ever
            // swap again, this is where it is caught, before the copy silently re-crosses.
            if (weapons != null && !string.Equals(weapons.role, StructureRole.Weaponsmith, StringComparison.OrdinalIgnoreCase))
                failures.Add("[tutorial-agree] catalog row '" + WeaponsShopId + "' claims role '" +
                             (weapons.role ?? "<none>") + "', not '" + StructureRole.Weaponsmith + "' - the " +
                             "armour nudge triggers off its placement precisely BECAUSE it is the weapons roof.");
            if (armorer != null && !string.Equals(armorer.role, StructureRole.Armorer, StringComparison.OrdinalIgnoreCase))
                failures.Add("[tutorial-agree] catalog row '" + ArmorerId + "' claims role '" +
                             (armorer.role ?? "<none>") + "', not '" + StructureRole.Armorer + "' - the nudge " +
                             "would be teaching the player to build something that is not the armourer.");

            // The INVERSION GUARD, kept and re-pointed: naming the weapons shop is the defect
            // now. It goes RED the moment the armour beat starts saying "Forge" again.
            if (weapons != null && armorer != null &&
                !string.IsNullOrEmpty(weapons.displayName) && !string.IsNullOrEmpty(armorer.displayName) &&
                !string.Equals(weapons.displayName, armorer.displayName, StringComparison.OrdinalIgnoreCase) &&
                objective.IndexOf(weapons.displayName, StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[tutorial-agree] the armor nudge's objective (\"" + objective + "\") names the " +
                             "WEAPONS shop '" + weapons.displayName + "' (catalog row '" + WeaponsShopId +
                             "') - the name inversion is back in the teaching copy.");

            if (armorer == null || string.IsNullOrEmpty(armorer.displayName))
                failures.Add("[tutorial-agree] catalog row '" + ArmorerId + "' is missing or has no displayName " +
                             "- the armour nudge points at nothing.");
            else if (objective.IndexOf(armorer.displayName, StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[tutorial-agree] the armor nudge's objective (\"" + objective + "\") no longer " +
                             "names '" + armorer.displayName + "' (catalog row '" + ArmorerId + "') - the " +
                             "armour beat no longer names the armourer.");
        }

        // =====================================================================
        //  Case 4 - the REAL sort, and its STABILITY
        // =====================================================================

        private static void Case4_StableSort(CatalogFile file, List<string> failures)
        {
            // (a) Synthetic - authored rows lead in ascending order; the three deliberately
            //     UNAUTHORED rows (including a negative, which is also "unauthored") come after
            //     them in their ORIGINAL relative order. This is the acceptance-3 proof.
            var input = new List<CatalogEntry>
            {
                MakeEntry("unauthored_A", 0),
                MakeEntry("authored_late", 30),
                MakeEntry("unauthored_B", 0),
                MakeEntry("authored_early", 10),
                MakeEntry("unauthored_C", -5),
            };
            var sorted = BuildPaletteVM.SortForDisplay(input);
            string got = Ids(sorted);
            const string want = "authored_early,authored_late,unauthored_A,unauthored_B,unauthored_C";
            if (got != want)
                failures.Add("[stable-sort] SortForDisplay returned [" + got + "], expected [" + want +
                             "] - authored rows must lead ascending and unauthored rows must hold their " +
                             "original relative order (a row with no authored order must never jump).");
            if (sorted != null && sorted.Count != input.Count)
                failures.Add("[stable-sort] the sort changed the row COUNT (" + input.Count + " in, " +
                             sorted.Count + " out) - it must re-order only, never add or drop (the locked-id " +
                             "UNION filter runs after it).");

            // Null / single-row contracts (Rebuild's null-guard depends on null-in-null-out).
            if (BuildPaletteVM.SortForDisplay(null) != null)
                failures.Add("[stable-sort] SortForDisplay(null) must return null - BuildPaletteVM.Rebuild " +
                             "null-guards the query result on it.");
            var one = new List<CatalogEntry> { MakeEntry("solo", 0) };
            var oneOut = BuildPaletteVM.SortForDisplay(one);
            if (oneOut == null || oneOut.Count != 1 || oneOut[0] == null || oneOut[0].id != "solo")
                failures.Add("[stable-sort] a single-row list did not pass through SortForDisplay intact.");

            if (file == null) return;

            // (b) LIVE catalog - the real rows through the real sort.
            var live = BuildPaletteVM.SortForDisplay(file.Entries);
            if (live == null) { failures.Add("[stable-sort] the live catalog sorted to null."); return; }

            // Head = the teaching order.
            var head = new List<string>();
            foreach (var e in live)
            {
                if (e == null || e.displayOrder <= 0) break;
                head.Add(e.id);
            }
            string headGot = string.Join(",", head.ToArray());
            string headWant = string.Join(",", AuthoredCarouselOrder);
            if (headGot != headWant)
                failures.Add("[stable-sort] the live catalog's authored head is [" + headGot + "], expected [" +
                             headWant + "] - the shipped carousel would not open on the tutorial's order.");

            // Every UNAUTHORED row keeps its exact catalog relative position.
            var catalogUnauthored = new List<string>();
            foreach (var e in file.Entries)
                if (e != null && e.displayOrder <= 0) catalogUnauthored.Add(e.id);
            var sortedUnauthored = new List<string>();
            foreach (var e in live)
                if (e != null && e.displayOrder <= 0) sortedUnauthored.Add(e.id);
            if (catalogUnauthored.Count == 0)
                failures.Add("[stable-sort] EVERY catalog row is authored - WO-963 acceptance 3 requires at " +
                             "least one deliberately unauthored row to prove the sort is stable.");
            else if (string.Join(",", catalogUnauthored.ToArray()) != string.Join(",", sortedUnauthored.ToArray()))
                failures.Add("[stable-sort] the unauthored rows were PERMUTED by the sort - they must hold " +
                             "their catalog row order exactly. catalog=[" +
                             string.Join(",", catalogUnauthored.ToArray()) + "] sorted=[" +
                             string.Join(",", sortedUnauthored.ToArray()) + "]");
        }

        private static CatalogEntry MakeEntry(string id, int order)
        {
            return new CatalogEntry { id = id, displayName = id, displayOrder = order };
        }

        private static string Ids(IReadOnlyList<CatalogEntry> list)
        {
            if (list == null) return "<null>";
            var parts = new List<string>(list.Count);
            foreach (var e in list) parts.Add(e != null ? e.id : "<null>");
            return string.Join(",", parts.ToArray());
        }

        // =====================================================================
        //  Case 5 - the palette reads NO tutorial data (source lint)
        // =====================================================================

        private static void Case5_NoTutorialRead(List<string> failures)
        {
            if (!File.Exists(PaletteSrc))
            {
                failures.Add("[no-tutorial-read] " + PaletteSrc + " is MISSING.");
                return;
            }
            string src = StripComments(File.ReadAllText(PaletteSrc));

            // Comments explain the rule; CODE naming a tutorial symbol/file breaks it.
            if (Regex.IsMatch(src, @"[Tt]utorial"))
                failures.Add("[no-tutorial-read] " + PaletteSrc + " now names a TUTORIAL symbol/file in code - " +
                             "the palette must never take the teaching script as an input (WO-963 sec3). The " +
                             "catalog carries the order; this suite is what keeps the two agreeing.");

            if (!src.Contains("SortForDisplay"))
                failures.Add("[no-tutorial-read] BuildPaletteVM no longer calls SortForDisplay - the carousel " +
                             "is back to raw catalog authoring order and WO-963 is silently reverted.");
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
