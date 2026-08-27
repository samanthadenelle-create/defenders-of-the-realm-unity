// =============================================================================
// StarterArmourOwnershipRegression [starter-armour] (WO-1240)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the owner ruling of 2026-08-26 - "both a progression bug and an economy
// hole" - which has TWO halves that only work together:
//
//   1. THE STARTER EQUIPMENT CONTRACT. Every hero begins OWNING one authored
//      starter armour item. Without it, gating auto-equip to owned gear resolves
//      to null on a fresh save and drops the hero to ArmorDefense 0 - which is
//      exactly why the previous seat refused to ship the gate alone.
//   2. ONE LAW FOR AUTO-EQUIP. Auto-equip may choose only from items the player
//      OWNS. No shop preview, no catalog entry, no locked gear, no unowned item
//      may ever participate.
//
// THE DEFECT, in the tree this suite was written against (proved before the fix,
// by evaluating the shipped gates over HEAD's armor.json at level 1):
//      knight  BestArmor(L1) = blink_armor_centurion   def 0.08   (starter 0.06)
//      ranger  BestArmor(L1) = blink_armor_beasthunter def 0.08   (starter 0.05)
//      mage    BestArmor(L1) = blink_armor_beasthunter def 0.08   (starter 0.03)
//      cleric  BestArmor(L1) = blink_armor_centurion   def 0.08   (starter 0.04)
// Every one of those is Armorer stock. `GearLoadout.Refresh` ran
// `EquippedArmor = GearCatalog.BestArmor(job, level)` catalog-wide, so a
// brand-new hero of EVERY class walked out of character creation wearing armour
// the player had never bought, on the very first Refresh.
//
// CASES
//   1 [starter-armour-data]  Every known class key has an authored starter
//                            armour id; it resolves in the RESOURCES armor.json
//                            (what the device loads), passes BOTH class gates
//                            (job list AND light/heavy weight), meets req at
//                            level 1, and has defense > 0. A null or unresolvable
//                            row here IS the ArmorDefense-0 trap.
//   2 [starter-armour-worn]  A NEW save of EVERY class - driven for real through
//                            a live GearLoadout with the class's equip prefs
//                            cleared - wears its starter armour and reports
//                            ArmorDefense > 0.
//   3 [auto-equip-owned-only] Driven with a strictly BETTER unowned row present
//                            (every class has one at level 1), the resolved
//                            armour is the starter, never the better unowned
//                            piece; the ownership predicate is honoured; and the
//                            WIRING is linted so case (a) cannot be asserting a
//                            code path the hero never takes.
//   4 [owned-upgrade-worn]   THE GOOD PATH, and it is not optional: a gate that
//                            refuses everything would pass cases 1-3. A
//                            legitimately OWNED better piece IS auto-worn.
//   5 [armour-floor]         The never-naked floor still exists and the single
//                            catalog-wide GearCatalog.BestArmor call in
//                            GearLoadout is that floor and nothing else.
//
// Markers: STARTER_ARMOUR_OK / STARTER_ARMOUR_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.StarterArmourOwnershipRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class StarterArmourOwnershipRegression
    {
        private const string LoadoutSrc = "Assets/_Modules/Village/Hero/GearLoadout.cs";
        private const string ArmorRes   = "Assets/Resources/Data/Canonical/armor.json";

        // The level a BRAND-NEW save is at. The contract is about character creation, so every
        // driven case runs here; a live GearLoadout probe carries no HeroProgression and is
        // therefore pinned at level 1 anyway, which is the case that matters.
        private const int NewSaveLevel = 1;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("STARTER_ARMOUR_OK - " + reason);
            else Debug.LogError("STARTER_ARMOUR_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "starter-armour-data", () => Case1_StarterArmourData(failures));
                Case(failures, "starter-armour-worn", () => Case2_StarterArmourWorn(failures, notes));
                Case(failures, "auto-equip-owned-only", () => Case3_OwnedOnly(failures, notes));
                Case(failures, "owned-upgrade-worn", () => Case4_OwnedUpgradeWorn(failures, notes));
                Case(failures, "armour-floor", () => Case5_ArmourFloor(failures));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "STARTER ARMOUR OK - every class owns an authored starter armour row that " +
                         "resolves and fits, a new save of every class wears it with ArmorDefense > 0, " +
                         "auto-equip never selects an unowned piece even with a strictly better one in " +
                         "the catalog, an OWNED upgrade is still auto-worn, and the never-naked floor " +
                         "is the one catalog-wide armour query left" + noteStr;
                return true;
            }
            reason = "starter-armour FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>Every class key the game has ever persisted gear under. Read off
        /// PlayableHeroes, never re-listed here - a second list is how a class silently loses
        /// its contract row the day it is added.</summary>
        private static IReadOnlyList<string> AllClasses() => PlayableHeroes.AllKnownJobKeys();

        /// <summary>
        /// True when this process holds NO gear ownership beyond the granted starter kit - i.e.
        /// the live probe really is standing on a fresh save.
        ///
        /// WHY IT IS ASKED. The strict "a new save wears its STARTER" assertion is only sound
        /// while the bag is empty. In the batchmode gate it always is (GameStateService.Instance
        /// is null and VillageInventory is absent), but an interactive editor seat with a loaded
        /// save owns real gear, and an OWNED upgrade winning there is the FEATURE working, not a
        /// regression. Failing on that would be this suite lying about a correct build - so the
        /// strict half downgrades to a note instead, and the counts say why.
        /// </summary>
        private static bool FreshSaveOwnership(out string why)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            int fromSave = state != null && state.GearInventory != null ? state.GearInventory.Count : 0;

            var inv = DeNelle.Village.Crafting.VillageInventory.Instance;
            int fromRuntime = inv != null && inv.Counts != null ? inv.Counts.Count : 0;

            why = $"gearInventory={fromSave} villageInventory={fromRuntime}";
            return fromSave == 0 && fromRuntime == 0;
        }

        // =====================================================================
        //  CASE 1 - the starter armour row EXISTS for every class and is wearable
        // =====================================================================
        private static void Case1_StarterArmourData(List<string> failures)
        {
            if (!File.Exists(ArmorRes))
            {
                failures.Add($"[starter-armour-data] {ArmorRes} not found - the RESOURCES copy is what the " +
                             "shipped player loads, so without it nothing below can be judged");
                return;
            }

            foreach (var job in AllClasses())
            {
                string id = StarterLoadout.ArmorFor(job);
                if (string.IsNullOrEmpty(id))
                {
                    failures.Add($"[starter-armour-data] class '{job}' has NO authored starter armour in " +
                                 "StarterLoadout. This is the WO-1240 contract itself: with no row to resolve " +
                                 "to, the owned-only auto-equip gate lands on null and a brand-new hero of " +
                                 "this class spawns at ArmorDefense 0 - the exact trap that kept the gate " +
                                 "closed. Author the row; do not relax the gate");
                    continue;
                }

                var a = GearCatalog.FindArmor(id);
                if (a == null)
                {
                    failures.Add($"[starter-armour-data] StarterLoadout['{job}'].Armor = '{id}' is NOT in " +
                                 "armor.json (Resources copy) - a starter id that resolves nowhere is worse " +
                                 "than no starter id, because the floor falls through to the catalog-wide " +
                                 "pick and silently hands over unowned Armorer stock again");
                    continue;
                }

                // BOTH class gates, asked separately, because they are different questions and a
                // row can be legal by one and illegal by the other (WO-1241 exists for that gap).
                if (!GearCatalog.ArmorJobMatches(a, job))
                    failures.Add($"[starter-armour-data] '{id}' has job='{a.job ?? "<null>"}', which does NOT " +
                                 $"admit class '{job}' - the class's own starter armour is refused by the JOB " +
                                 "gate, so the floor drops it and the hero is dressed by the catalog instead");

                if (!GearCatalog.ArmorFitsClass(a, job))
                    failures.Add($"[starter-armour-data] '{id}' has weight='{a.weight ?? "<none>"}' but class " +
                                 $"'{job}' wears '{GearCatalog.ClassWeight(job)}' - the WEIGHT gate refuses the " +
                                 "class's own starter armour. This is the WO-1241 shape: legal by job, illegal " +
                                 "by weight");

                if (!GearCatalog.MeetsReq(a.req, NewSaveLevel))
                    failures.Add($"[starter-armour-data] '{id}' requires level " +
                                 $"{GearCatalog.RequiredLevel(a.req)} - it is handed to a brand-new level-" +
                                 $"{NewSaveLevel} hero, so any gate above {NewSaveLevel} means the contract row " +
                                 "can never actually be worn on the save it exists for");

                if (a.defense <= 0f)
                    failures.Add($"[starter-armour-data] '{id}' has defense={a.defense:0.###} - a starter row " +
                                 "with no defense satisfies the contract on paper and still leaves the hero at " +
                                 "ArmorDefense 0, which is the whole outcome this WO exists to prevent");
            }
        }

        // =====================================================================
        //  CASE 2 - a NEW save of EVERY class WEARS its starter armour
        // ---------------------------------------------------------------------
        //  Driven for real through a live GearLoadout (the EditMode lifecycle pattern:
        //  new GameObject + AddComponent + BindOwnerClass + DestroyImmediate), with the
        //  class's equip prefs cleared first and the developer's own prefs snapshotted and
        //  restored afterwards. A source lint could not make this statement: "the table has
        //  a row" and "the hero is wearing it" are different claims, and the second one is
        //  the one the owner felt.
        // =====================================================================
        private static void Case2_StarterArmourWorn(List<string> failures, List<string> notes)
        {
            // HARNESS-CAPABILITY-ABSENT -> a DECLARED stand-down, never a plain note. "Batchmode is
            // always fresh" is an assumption about the environment, not an assertion about the
            // product: if the seat owns gear, the strict half genuinely tested nothing, and a bare
            // note for that lands in the GREEN column exactly like the hollow passes the ratchet
            // exists to catch. The token is what lets the reporting layer subtract it.
            bool fresh = FreshSaveOwnership(out string why);
            if (!fresh)
                notes.Add(RegressionOutcome.PartialSkip("[starter-armour-worn] strict 'wears the STARTER' half",
                          "this seat OWNS gear (" + why + "), so an owned upgrade winning is legitimate here and " +
                          "the identity assertion cannot be made. ArmorDefense > 0 is still asserted for every class"));

            foreach (var job in AllClasses())
            {
                string starterId = StarterLoadout.ArmorFor(job);
                // DELEGATED, not skipped: Case 1 loops the SAME AllClasses() and fails on exactly
                // this condition, so whenever this fires the suite is already RED. That is the
                // only reason a bare continue is legitimate here.
                if (string.IsNullOrEmpty(starterId)) continue;

                WithFreshEquipPrefs(job, () =>
                {
                    var probe = NewProbe(job, out var go);
                    try
                    {
                        if (probe == null)
                        {
                            failures.Add($"[starter-armour-worn] could not build a GearLoadout probe for '{job}'");
                            return;
                        }

                        if (probe.EquippedArmor == null)
                        {
                            failures.Add($"[starter-armour-worn] a NEW save of '{job}' resolves NO armour at all " +
                                         "- the ownership gate stranded the hero naked, which is a worse ship-day " +
                                         "bug than the free Armorer piece it was added to stop");
                            return;
                        }

                        if (!string.Equals(probe.EquippedArmor.id, starterId, StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = $"a NEW save of '{job}' wears '{probe.EquippedArmor.id}', not the " +
                                         $"authored starter '{starterId}'. On a fresh save the ONLY armour the " +
                                         "player owns is the granted starter, so anything else here came from " +
                                         "the catalog and was never bought";
                            if (fresh) failures.Add("[starter-armour-worn] " + msg);
                            else notes.Add(RegressionOutcome.PartialSkip(
                                "[starter-armour-worn] '" + job + "' starter-identity assertion",
                                msg + " -- not asserted because this seat owns gear (" + why + ")"));
                        }

                        if (!(probe.ArmorDefense > 0f))
                            failures.Add($"[starter-armour-worn] a NEW save of '{job}' reports ArmorDefense=" +
                                         $"{probe.ArmorDefense:0.###} wearing '{probe.EquippedArmor.id}' - the " +
                                         "contract exists precisely so gating auto-equip cannot drop a fresh hero " +
                                         "to zero, so a zero here means the fix traded an economy hole for a " +
                                         "silent difficulty spike");
                        else
                            notes.Add($"'{job}' new save: armour='{probe.EquippedArmor.id}' " +
                                      $"ArmorDefense={probe.ArmorDefense:0.###}");
                    }
                    finally { DestroyProbe(go); }
                });
            }
        }

        // =====================================================================
        //  CASE 3 - auto-equip NEVER selects an unowned item
        // ---------------------------------------------------------------------
        //  (a) DRIVEN, with a strictly BETTER unowned row present. That precondition is
        //      asserted, not assumed: a case that silently became vacuous would pass forever
        //      while the leak was wide open.
        //  (b) The RANKING, through the shipped GearCatalog.PickBestArmor with a
        //      starter-only ownership predicate.
        //  (c) The WIRING, source-linted, so (a) and (b) cannot be correct while
        //      GearLoadout.Refresh still calls the catalog-wide query. (a) without (c)
        //      passes on a dead code path.
        // =====================================================================
        private static void Case3_OwnedOnly(List<string> failures, List<string> notes)
        {
            // The DRIVEN half needs the better piece to actually be UNOWNED on this seat. In the
            // batchmode gate it always is; on a seat with a loaded save it may not be, and wearing
            // gear you bought is the feature. The RANKING and WIRING halves below are unconditional.
            bool fresh = FreshSaveOwnership(out string why);

            foreach (var job in AllClasses())
            {
                string starterId = StarterLoadout.ArmorFor(job);
                var starter = string.IsNullOrEmpty(starterId) ? null : GearCatalog.FindArmor(starterId);
                // DELEGATED, not skipped: Case 1 loops the SAME AllClasses() and fails on exactly
                // this condition, so whenever this fires the suite is already RED. That is the
                // only reason a bare continue is legitimate here.
                if (starter == null) continue;

                var better = BestUnownedBetterThan(job, starter);
                if (better == null)
                {
                    // The catalog stopped serving a better row for this class, so the driven half has
                    // nothing to drive WITH. Declared rather than noted: a vacuous case that reads as
                    // prose is indistinguishable in the log from a case that actually proved something.
                    notes.Add(RegressionOutcome.PartialSkip($"[auto-equip-owned-only] '{job}' driven half",
                              $"no class-eligible armour at level {NewSaveLevel} beats the starter '{starterId}' " +
                              $"(def {starter.defense:0.###}), so the leak this case pins could not be caught " +
                              "here anymore"));
                }
                else if (!fresh)
                {
                    // HARNESS-CAPABILITY-ABSENT: the driven half needs the better piece to really be
                    // UNOWNED, and on a seat with a loaded save it may not be. Declared, so the live
                    // half's absence is visible rather than passing as prose. (b) and (c) below still run.
                    notes.Add(RegressionOutcome.PartialSkip($"[auto-equip-owned-only] '{job}' LIVE probe",
                              "this seat owns gear (" + why + "), so '" + better.id + "' cannot be relied on to " +
                              "be unowned and an owned upgrade beating the starter would be legitimate"));
                }
                else
                {
                    // (a) LIVE: the better piece exists in the catalog and is NOT owned. A fresh
                    // save must still come out wearing the starter.
                    WithFreshEquipPrefs(job, () =>
                    {
                        var probe = NewProbe(job, out var go);
                        try
                        {
                            if (probe == null)
                            {
                                // WAS A BARE `return;` - the live half asserted nothing and the case
                                // still read green. Case 2 treats an unbuildable probe as a FAILURE and
                                // owns that verdict for the whole suite; here the guarantee is also
                                // carried by the RANKING half (b) and the WIRING lint (c) below, so the
                                // honest state is a DECLARED partial stand-down, not silence.
                                notes.Add(RegressionOutcome.PartialSkip(
                                    $"[auto-equip-owned-only] '{job}' LIVE probe",
                                    "a GearLoadout probe could not be built in this harness; the ranking and " +
                                    "wiring halves of this case still ran, and [starter-armour-worn] fails on " +
                                    "the same condition"));
                                return;
                            }
                            if (probe.EquippedArmor != null &&
                                string.Equals(probe.EquippedArmor.id, better.id, StringComparison.OrdinalIgnoreCase))
                                failures.Add($"[auto-equip-owned-only] a NEW save of '{job}' is wearing " +
                                             $"'{better.id}' (def {better.defense:0.###}) - a catalog/Armorer row " +
                                             "the player has never bought, auto-equipped over the granted starter " +
                                             $"'{starterId}' (def {starter.defense:0.###}). This IS the economy " +
                                             "hole: the best armour the class qualifies for is being handed out " +
                                             "free on the first Refresh");
                        }
                        finally { DestroyProbe(go); }
                    });

                    notes.Add($"'{job}': drove the case with unowned '{better.id}' (def {better.defense:0.###}) " +
                              $"beating starter '{starterId}' (def {starter.defense:0.###})");
                }

                // (b) the RANKING, through the shipped query.
                var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { starterId };
                var pick = GearCatalog.PickBestArmor(job, NewSaveLevel, id => ownedIds.Contains(id ?? string.Empty));

                if (!pick.OwnershipApplied)
                    failures.Add($"[auto-equip-owned-only] PickBestArmor('{job}', {NewSaveLevel}, <predicate>) " +
                                 "reports OwnershipApplied=false - a predicate was supplied and IGNORED, so every " +
                                 "caller that thinks it is filtering is still ranking the whole paid catalog");

                if (pick.Armor == null)
                    failures.Add($"[auto-equip-owned-only] '{job}' owning ONLY its starter '{starterId}' resolves " +
                                 "a NULL armour - the ownership gate has made the hero naked");
                else if (!string.Equals(pick.Armor.id, starterId, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[auto-equip-owned-only] '{job}' owning ONLY '{starterId}' resolves " +
                                 $"'{pick.Armor.id}' - the winner is not the one row the player has, so the " +
                                 "ownership predicate is not gating the ranking loop");

                if (pick.Owned > ownedIds.Count)
                    failures.Add($"[auto-equip-owned-only] '{job}': PickBestArmor counted {pick.Owned} owned " +
                                 $"candidates from an owned set of {ownedIds.Count} id(s) - the filter is letting " +
                                 "unowned rows through the count, so the trace line would lie about it");
            }

            // (c) the WIRING.
            // ReadSource has ALREADY recorded a [source] failure naming the path (fixture-absent ->
            // FAIL), so this return leaves the suite RED, not green. It is a bail, not a stand-down.
            string src = ReadSource(LoadoutSrc, failures);
            if (src == null) return;
            string code = StripComments(src);   // prose about the old call must never satisfy a lint

            if (Regex.IsMatch(code, @"EquippedArmor\s*=\s*GearCatalog\.BestArmor"))
                failures.Add("[auto-equip-owned-only] GearLoadout.Refresh still assigns " +
                             "`EquippedArmor = GearCatalog.BestArmor(job, level)` - that is the catalog-wide, " +
                             "defense-only pick verbatim, i.e. the original defect has returned and every hero " +
                             "auto-wears the best Armorer piece their class qualifies for");

            if (code.IndexOf("ResolveAutoBestArmor", StringComparison.Ordinal) < 0)
                failures.Add("[auto-equip-owned-only] GearLoadout has no ResolveAutoBestArmor - the ownership " +
                             "gate is not wired into the armour resolution chain at all, so the ranking asserted " +
                             "above is a code path the hero never takes");

            if (!Regex.IsMatch(code, @"PickBestArmor\s*\([^;]*Owns"))
                failures.Add("[auto-equip-owned-only] nothing in GearLoadout passes an ownership predicate to " +
                             "GearCatalog.PickBestArmor - the gated overload exists but is called with null, " +
                             "which is byte-identical to the unfixed behaviour");

            // The starter armour must be inside the OWNED set, or the starter is filtered out of
            // its own fallback - the one way this fix could leave a hero at ArmorDefense 0.
            if (!Regex.IsMatch(code, @"ids\.Add\(kit\.Armor\)"))
                failures.Add("[auto-equip-owned-only] ResolveOwnedGear does not seed kit.Armor into the owned " +
                             "set - the GRANTED starter armour is never written to VillageInventory, so leaving " +
                             "it out means the ownership gate filters out the very row the contract authored");
        }

        // =====================================================================
        //  CASE 4 - THE GOOD PATH: a legitimately OWNED upgrade IS still auto-worn
        // ---------------------------------------------------------------------
        //  NOT optional. Cases 1-3 are all satisfied by a gate that refuses EVERYTHING and
        //  falls to the starter every time - and this repo has already shipped exactly that
        //  bug (a pin guard that aborted every good run while exiting 0). This case is the
        //  one that fails when the gate over-refuses.
        // =====================================================================
        private static void Case4_OwnedUpgradeWorn(List<string> failures, List<string> notes)
        {
            int driven = 0;
            foreach (var job in AllClasses())
            {
                string starterId = StarterLoadout.ArmorFor(job);
                var starter = string.IsNullOrEmpty(starterId) ? null : GearCatalog.FindArmor(starterId);
                // DELEGATED, not skipped: Case 1 loops the SAME AllClasses() and fails on exactly
                // this condition, so whenever this fires the suite is already RED. That is the
                // only reason a bare continue is legitimate here.
                if (starter == null) continue;

                var upgrade = BestUnownedBetterThan(job, starter);
                // DELEGATED: Case 3 stamps a RegressionOutcome.PartialSkip for exactly this class,
                // and the driven==0 backstop below fails if it turns out to be true for ALL of them.
                if (upgrade == null) continue;

                // The player has now BOUGHT it: starter + upgrade are both owned.
                var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { starterId, upgrade.id };
                var pick = GearCatalog.PickBestArmor(job, NewSaveLevel, id => ownedIds.Contains(id ?? string.Empty));
                driven++;

                if (pick.Armor == null)
                    failures.Add($"[owned-upgrade-worn] '{job}' owning starter '{starterId}' AND the better " +
                                 $"'{upgrade.id}' resolves NULL - the gate is refusing owned gear outright");
                else if (!string.Equals(pick.Armor.id, upgrade.id, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[owned-upgrade-worn] '{job}' owns both '{starterId}' (def " +
                                 $"{starter.defense:0.###}) and the strictly better '{upgrade.id}' (def " +
                                 $"{upgrade.defense:0.###}) and still resolves '{pick.Armor.id}' - a purchased " +
                                 "upgrade that never gets worn is the progression half of this WO failing in the " +
                                 "opposite direction. Auto-upgrade among OWNED gear is the FEATURE; only ranking " +
                                 "unowned stock was the bug");
                else
                    notes.Add($"'{job}' good path: owning starter + '{upgrade.id}' auto-wears '{pick.Armor.id}' " +
                              $"(def {pick.Armor.defense:0.###})");

                if (pick.Owned != ownedIds.Count)
                    notes.Add($"'{job}': {pick.Owned} of {ownedIds.Count} owned id(s) were class-eligible at " +
                              $"level {NewSaveLevel} (of {pick.Eligible} eligible rows)");
            }

            if (driven == 0)
                failures.Add("[owned-upgrade-worn] NOT ONE class could be driven with an owned upgrade - every " +
                             "class's starter is already the best armour it qualifies for at level " +
                             NewSaveLevel + ", so the good path is untested everywhere and a gate that refuses " +
                             "all owned gear would pass this suite");
        }

        // =====================================================================
        //  CASE 5 - the never-naked floor survives, and is the ONLY catalog-wide query
        // =====================================================================
        private static void Case5_ArmourFloor(List<string> failures)
        {
            // As in Case 3: ReadSource already failed naming the path, so this bail is RED.
            string src = ReadSource(LoadoutSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            if (code.IndexOf("StarterOrCatalogArmorFloor", StringComparison.Ordinal) < 0)
                failures.Add("[armour-floor] GearLoadout has no StarterOrCatalogArmorFloor - the ownership gate " +
                             "has no fallback for an empty/unresolvable owned set, so a hero whose bag the " +
                             "resolver cannot read (every pre-load boot frame) spawns at ArmorDefense 0");

            // Counted, not banned: the fix KEEPS exactly one catalog-wide armour call as the
            // deliberate floor. A lint that banned it outright would fail on the safety net it
            // should protect; the invariant that matters is "there is only ONE, and it is the floor".
            int wide = CountOccurrences(code, "GearCatalog.BestArmor");
            if (wide != 1)
                failures.Add($"[armour-floor] GearLoadout calls GearCatalog.BestArmor {wide} time(s); exactly ONE " +
                             "is expected (the never-naked floor inside StarterOrCatalogArmorFloor). More than " +
                             "one means an armour path went back to an unowned catalog-wide pick; zero means the " +
                             "floor itself was deleted and a hero with an unreadable bag has nothing to fall to");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>The highest-defense class-eligible armour at <see cref="NewSaveLevel"/> that
        /// is strictly BETTER than <paramref name="starter"/> - i.e. the piece the catalog-wide
        /// pick would have handed over free. Null when the starter is already the best.</summary>
        private static ArmorDef BestUnownedBetterThan(string job, ArmorDef starter)
        {
            ArmorDef best = null;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null || starter == null) continue;
                if (string.Equals(a.id, starter.id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!GearCatalog.ArmorJobMatches(a, job) || !GearCatalog.ArmorFitsClass(a, job)) continue;
                if (!GearCatalog.MeetsReq(a.req, NewSaveLevel)) continue;
                if (a.defense <= starter.defense) continue;
                if (best == null || a.defense > best.defense) best = a;
            }
            return best;
        }

        /// <summary>Build a live GearLoadout bound to <paramref name="job"/>. BindOwnerClass
        /// triggers Refresh, so the probe reports what a hero of that class actually resolves.</summary>
        private static GearLoadout NewProbe(string job, out GameObject go)
        {
            go = new GameObject("StarterArmourProbe(" + job + ")");
            var loadout = go.AddComponent<GearLoadout>();
            loadout.BindOwnerClass(job);
            return loadout;
        }

        private static void DestroyProbe(GameObject go)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Run <paramref name="body"/> with this class's five equip prefs DELETED (a genuinely
        /// new save), then restore whatever the developer had. Without the snapshot this suite
        /// would silently wipe the running seat's own equipped gear; without the delete it would
        /// read a PREVIOUS session's choice and pass while the contract was broken.
        /// </summary>
        private static void WithFreshEquipPrefs(string job, Action body)
        {
            string key = (job ?? string.Empty).ToLowerInvariant();
            string[] keys =
            {
                EquipPrefKeys.Weapon + key,
                EquipPrefKeys.Armor + key,
                EquipPrefKeys.OffHand + key,
                EquipPrefKeys.Ring + key,
                EquipPrefKeys.Amulet + key,
            };

            var saved = new Dictionary<string, string>();
            foreach (var k in keys)
                if (PlayerPrefs.HasKey(k)) saved[k] = PlayerPrefs.GetString(k, string.Empty);

            try
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                PlayerPrefs.Save();
                body();
            }
            finally
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                foreach (var kv in saved) PlayerPrefs.SetString(kv.Key, kv.Value);
                PlayerPrefs.Save();
            }
        }

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
    }
}
