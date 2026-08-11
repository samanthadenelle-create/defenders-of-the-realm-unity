// =============================================================================
// HeroBarClassRebindRegression [hero-bar-rebind]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-1019 PART A (owner felt-test 2026-08-10, verbatim, on Thrain the Mage:
// "can you review the default values for thrain in actionbar? He should have all
// magic spells and he inherits the hotswap from previous character and has nothing
// explicit for dps").
//
// THE AUTHORED DATA WAS NEVER THE PROBLEM. abilities.json classes.mage.abilities is
// already a complete all-magic bar WITH explicit DPS: mage.fireball (Q, strike, 30
// dmg @14 m), mage.shell (W), mage.heal (E), mage.meteor (R). Case 1 pins that so a
// later "fix" cannot quietly move the goalposts by editing the data instead.
//
// THE DEFECT WAS THE BINDING, and it lived in PERSISTED STATE, not in the producer
// and not in the view: AssignableSkillBar (the HOT-SWAP rail the owner named)
// persisted under ONE GLOBAL PlayerPrefs key, "dotr-skillbar-extra-v1". Every hero
// read and overwrote it, so a switch Grom -> Thrain re-rendered the KNIGHT's assigned
// extras on the Mage. HeroLoadout (the W/E/R rail) had the identical defect and was
// fixed per-class in WO-861 Phase 0; this bar was left behind even though its own
// header claims to "MIRROR the HeroLoadout persistence pattern".
//
// THE RULE THIS SUITE PROVES: switching the hero's class REBINDS both bars to that
// class's own persisted picks, and NO ability id from the previous class survives -
// enforced twice, by a per-class key AND by a class-validity drop
// (AbilityCatalog.IsUsableByClass, which answers from the abilities.json CLASS KEY an
// id is authored under, not from the id's prefix).
//
// WHAT IT PROVES HEADLESSLY, AND WHAT IT CANNOT:
//   (a) DATA - the mage kit is all-magic and carries an explicit damage Q (Case 1).
//   (b) PURE CONTRACT - class ownership of an id, incl. the universal pool and the
//       unknown-id case (Case 2). No Unity objects, so the ORDER is provable in batch.
//   (c) LIVE COMPONENT PROBE - real HeroAbilities + HeroLoadout + AssignableSkillBar
//       on an INACTIVE, HideAndDontSave GameObject (AddComponent defers Awake, so no
//       rig / Addressables / loadout machinery is touched): seeded knight picks read
//       back as knight, SetHeroClass("mage") rebinds BOTH rails to the mage picks with
//       zero knight ids surviving, and switching back restores the knight picks
//       (Case 3). Case 4 replays the ACTUAL owner symptom: a legacy GLOBAL bar holding
//       a knight id + a mage id, loaded by a mage, must yield the mage id only.
//   (d) SOURCE INVARIANT - the per-class key + the WO-967/WO-1019 "ability bar bound"
//       trace are pinned at source, so a revert to a global key or a stripped trace
//       fails here rather than in the owner's next felt-test (Case 5).
//
//   NOT provable here: what the medallions actually LOOK like after a switch - that is
//   UI_CAPTURE_OK plus the owner's felt-verify (PO closes, docs/TICKET_PIPELINE.md).
//
// PLAYERPREFS SAFETY: Cases 3-4 must write real PlayerPrefs (that IS the seam under
// test). Every key touched is SNAPSHOTTED before and RESTORED in a finally, including
// the absent-key case, so running this suite can never cost a developer their bar.
//
// Markers: HERO_BAR_REBIND_OK / HERO_BAR_REBIND_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HeroBarClassRebindRegression.RunAll
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
    public static class HeroBarClassRebindRegression
    {
        private const string SkillBarSrc = "Assets/_Modules/Village/Hero/AssignableSkillBar.cs";
        private const string LoadoutSrc  = "Assets/_Modules/Village/Hero/HeroLoadout.cs";
        private const string ProducerSrc = "Assets/_Modules/Village/HUD/HudModelProducers.cs";

        // The seeded picks. Every id is real (verified against abilities.json) so the
        // class-validity filter is exercised on genuine data, never on invented strings.
        private const string KnightWer   = "w=knight.snare-arrow;e=knight.mending-salve";
        private const string KnightExtra = "0=knight.thunderbolt";
        private const string MageWer     = "w=mage.frost-nova;e=mage.void-rift";
        private const string MageExtra   = "0=mage.blink";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_BAR_REBIND_OK - " + reason);
            else Debug.LogError("HERO_BAR_REBIND_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "mage-kit-data",  () => Case1_MageKitIsAllMagicWithDps(failures));
                Case(failures, "class-owner",    () => Case2_ClassOwnershipContract(failures));
                Case(failures, "rebind-switch",  () => Case3_SwitchingClassRebindsBothBars(failures));
                Case(failures, "legacy-global",  () => Case4_LegacyGlobalBarIsFiltered(failures));
                Case(failures, "wiring-lint",    () => Case5_WiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "HERO BAR REBIND OK - the mage kit is all-magic with an explicit damage Q " +
                         "(mage.fireball), ability ownership resolves from the abilities.json class key " +
                         "(universal shared, unknown rejected), a live class switch REBINDS both the " +
                         "W/E/R rail and the HOT-SWAP rail to that class's own picks with NO id from the " +
                         "previous class surviving in either direction, a legacy GLOBAL hot-swap bar is " +
                         "inherited FILTERED, and the per-class key + the 'ability bar bound' trace are " +
                         "present at source.";
                return true;
            }
            reason = "hero-bar-rebind FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the MAGE KIT is already all-magic and already has the DPS
        // =====================================================================

        // ⚠ PART B WILL CHANGE THE EXPECTED IDS BELOW, BY DESIGN. The owner's ruling
        // (WO-1019 Part B) moves E: mage.heal -> mage.drain and R: mage.meteor -> mage.poison.
        // That is a DATA edit awaiting her tuning pass; when it lands, update this table in the
        // same commit. The two invariants around it - "every default is authored under the mage
        // class" and "Q is an explicit damage ability at range" - hold either way and are the
        // part that must never be edited to make a failure go away.
        private static void Case1_MageKitIsAllMagicWithDps(List<string> failures)
        {
            AbilityCatalog.Reload();

            var expected = new[]
            {
                new { Slot = AbilitySlot.Q, Id = "mage.fireball" },
                new { Slot = AbilitySlot.W, Id = "mage.shell"    },
                new { Slot = AbilitySlot.E, Id = "mage.heal"     },
                new { Slot = AbilitySlot.R, Id = "mage.meteor"   },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                var def = AbilityCatalog.Find("mage", expected[i].Slot);
                if (def == null)
                {
                    failures.Add("[mage-kit-data] classes.mage.abilities has NO def for slot " +
                                 expected[i].Slot + " - the Mage's default bar is incomplete in abilities.json.");
                    continue;
                }
                if (!string.Equals(def.Id, expected[i].Id, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[mage-kit-data] slot " + expected[i].Slot + " is '" + (def.Id ?? "<null>") +
                                 "', expected '" + expected[i].Id + "'. WO-1019 Part A rests on this kit being " +
                                 "the authored default; a data change here needs an owner ruling, not a silent edit.");

                // "all magic": every default the Mage gets must be authored under the mage class.
                if (!AbilityCatalog.IsUsableByClass(def.Id, "mage"))
                    failures.Add("[mage-kit-data] slot " + expected[i].Slot + " id '" + (def.Id ?? "<null>") +
                                 "' is not authored under the mage class (owner='" +
                                 (AbilityCatalog.OwningClassOf(def.Id) ?? "<unknown>") + "') - the Mage would be " +
                                 "presenting another class's spell as a default.");
            }

            // THE OWNER'S "nothing explicit for dps": Q must be a real damage ability at range.
            var q = AbilityCatalog.Find("mage", AbilitySlot.Q);
            if (q != null)
            {
                if (q.Damage <= 0f)
                    failures.Add("[mage-kit-data] the Mage's Q '" + (q.Id ?? "<null>") + "' deals " + q.Damage +
                                 " damage - the class has NO explicit DPS default, which is exactly the " +
                                 "complaint WO-1019 proved was a binding bug and not a data bug.");
                if (q.Range <= 0f)
                    failures.Add("[mage-kit-data] the Mage's Q '" + (q.Id ?? "<null>") + "' has range " + q.Range +
                                 " - a caster's primary must reach.");
                if (q.EffectEnum != AbilityEffect.Strike)
                    failures.Add("[mage-kit-data] the Mage's Q effect is '" + (q.Effect ?? "<null>") +
                                 "' - the single-target primary nuke is expected to be a strike.");
            }
        }

        // =====================================================================
        //  Case 2 - the PURE class-ownership contract
        // =====================================================================

        private static void Case2_ClassOwnershipContract(List<string> failures)
        {
            AbilityCatalog.Reload();

            // Owning class comes from the abilities.json CLASS KEY, with the "-skills" pool
            // suffix stripped - NOT from the id prefix.
            ExpectOwner(failures, "mage.fireball", "mage");     // classes.mage      (the kit)
            ExpectOwner(failures, "mage.blink", "mage");        // classes.mage-skills (the pool)
            ExpectOwner(failures, "knight.q", "knight");
            ExpectOwner(failures, "knight.thunderbolt", "knight");
            ExpectOwner(failures, "ranger.multishot", "ranger");
            ExpectOwner(failures, "universal.mend", AbilityCatalog.UniversalPoolClass);

            // A hero may hold its own class's ids...
            ExpectUsable(failures, "mage.fireball", "mage", true);
            ExpectUsable(failures, "mage.blink", "mage", true);
            ExpectUsable(failures, "knight.thunderbolt", "knight", true);

            // ...and NEVER another class's. This single predicate is the whole rule.
            ExpectUsable(failures, "knight.thunderbolt", "mage", false);
            ExpectUsable(failures, "knight.q", "mage", false);
            ExpectUsable(failures, "mage.fireball", "knight", false);
            ExpectUsable(failures, "ranger.multishot", "mage", false);

            // The universal pool is shared by construction.
            ExpectUsable(failures, "universal.mend", "mage", true);
            ExpectUsable(failures, "universal.mend", "knight", true);
            ExpectUsable(failures, "universal.dash", "ranger", true);

            // The Cleric aliases onto the mage loadout (WO-226) and has no abilities.json block,
            // so without the alias this predicate would drop her ENTIRE bar.
            ExpectUsable(failures, "mage.fireball", "cleric", true);

            // An id that is no longer in the catalog belongs to nobody: it cannot render or cast,
            // so keeping it bound would only preserve a lie on the bar.
            ExpectUsable(failures, "mage.does-not-exist", "mage", false);
            ExpectUsable(failures, null, "mage", false);
            ExpectUsable(failures, "", "mage", false);
        }

        private static void ExpectOwner(List<string> failures, string id, string expected)
        {
            string actual = AbilityCatalog.OwningClassOf(id);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                failures.Add("[class-owner] OwningClassOf('" + id + "') = '" + (actual ?? "<null>") +
                             "', expected '" + expected + "'.");
        }

        private static void ExpectUsable(List<string> failures, string id, string cls, bool expected)
        {
            bool actual = AbilityCatalog.IsUsableByClass(id, cls);
            if (actual != expected)
                failures.Add("[class-owner] IsUsableByClass('" + (id ?? "<null>") + "','" + cls + "') = " +
                             actual + ", expected " + expected + ".");
        }

        // =====================================================================
        //  Case 3 - THE REBIND, on live components
        // =====================================================================

        private static void Case3_SwitchingClassRebindsBothBars(List<string> failures)
        {
            AbilityCatalog.Reload();
            var snapshot = SnapshotBarPrefs();
            GameObject go = null;
            try
            {
                PlayerPrefs.SetString(EquipPrefKeys.LoadoutKeyFor("knight"), KnightWer);
                PlayerPrefs.SetString(EquipPrefKeys.SkillBarKeyFor("knight"), KnightExtra);
                PlayerPrefs.SetString(EquipPrefKeys.LoadoutKeyFor("mage"), MageWer);
                PlayerPrefs.SetString(EquipPrefKeys.SkillBarKeyFor("mage"), MageExtra);
                PlayerPrefs.DeleteKey(EquipPrefKeys.SkillBarLegacyGlobalKey);

                go = NewProbe(out var abilities, out var loadout, out var bar);
                abilities.SetHeroClass("knight");

                // --- the previous character ---
                AssertBar(failures, "knight(initial)", loadout, bar, "knight",
                          expectW: "knight.snare-arrow", expectExtra0: "knight.thunderbolt");

                // --- THE SWITCH (this is the seam WO-1019 fixes) ---
                abilities.SetHeroClass("mage");
                AssertBar(failures, "mage(after switch)", loadout, bar, "mage",
                          expectW: "mage.frost-nova", expectExtra0: "mage.blink");

                // --- and back, so the rebind is proven in BOTH directions ---
                abilities.SetHeroClass("knight");
                AssertBar(failures, "knight(switched back)", loadout, bar, "knight",
                          expectW: "knight.snare-arrow", expectExtra0: "knight.thunderbolt");

                // A write while a mage must land in the MAGE's key and leave the knight's alone -
                // the other half of the "one store, many heroes" defect.
                abilities.SetHeroClass("mage");
                bar.Assign(1, "mage.manaweave");
                string knightRaw = PlayerPrefs.GetString(EquipPrefKeys.SkillBarKeyFor("knight"), string.Empty);
                if (knightRaw.IndexOf("mage.manaweave", StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add("[rebind-switch] a MAGE's hot-swap assign was written into the KNIGHT's key ('" +
                                 knightRaw + "') - the per-class key is not being resolved on write.");
                if (bar.AbilityIdForSlot(1) != "mage.manaweave")
                    failures.Add("[rebind-switch] the mage's own assign did not take (slot 1 = '" +
                                 (bar.AbilityIdForSlot(1) ?? "<null>") + "') - within-class picks must persist normally.");

                // And a cross-class assign must be refused outright, not merely dropped on reload.
                if (bar.Assign(2, "knight.thunderbolt"))
                    failures.Add("[rebind-switch] a MAGE was allowed to assign the knight ability " +
                                 "'knight.thunderbolt' to the hot-swap bar - the write-side class guard is missing.");
                if (loadout.Equip(AbilitySlot.R, "knight.thunderbolt"))
                    failures.Add("[rebind-switch] a MAGE was allowed to equip 'knight.thunderbolt' into R - " +
                                 "the W/E/R write-side class guard is missing.");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                RestoreBarPrefs(snapshot);
            }
        }

        /// <summary>
        /// Reads BOTH rails and proves (a) they carry this class's picks and (b) NO id from any
        /// other class survives anywhere on them - the acceptance criterion stated as one check.
        /// </summary>
        private static void AssertBar(List<string> failures, string stage, HeroLoadout loadout,
                                      AssignableSkillBar bar, string cls, string expectW, string expectExtra0)
        {
            string w = loadout.AbilityIdForSlot(AbilitySlot.W);
            if (!string.Equals(w, expectW, StringComparison.OrdinalIgnoreCase))
                failures.Add("[rebind-switch] " + stage + ": W/E/R slot W = '" + (w ?? "<null>") +
                             "', expected '" + expectW + "' - the rail did not rebind to the active class.");

            string x0 = bar.AbilityIdForSlot(0);
            if (!string.Equals(x0, expectExtra0, StringComparison.OrdinalIgnoreCase))
                failures.Add("[rebind-switch] " + stage + ": HOT-SWAP slot 0 = '" + (x0 ?? "<null>") +
                             "', expected '" + expectExtra0 + "' - this is the owner's \"he inherits the " +
                             "hotswap from previous character\".");

            // The absolute rule: not one foreign id, on either rail, in any slot.
            foreach (var slot in new[] { AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                string id = loadout.AbilityIdForSlot(slot);
                if (!string.IsNullOrEmpty(id) && !AbilityCatalog.IsUsableByClass(id, cls))
                    failures.Add("[rebind-switch] " + stage + ": W/E/R slot " + slot + " still holds '" + id +
                                 "' (owner='" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") +
                                 "'), which class '" + cls + "' may not have.");
            }
            for (int i = 0; i < AssignableSkillBar.SlotCount; i++)
            {
                string id = bar.AbilityIdForSlot(i);
                if (!string.IsNullOrEmpty(id) && !AbilityCatalog.IsUsableByClass(id, cls))
                    failures.Add("[rebind-switch] " + stage + ": HOT-SWAP slot " + i + " still holds '" + id +
                                 "' (owner='" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") +
                                 "'), which class '" + cls + "' may not have.");
            }
        }

        // =====================================================================
        //  Case 4 - the owner's exact save shape: ONE global hot-swap bar
        // =====================================================================

        private static void Case4_LegacyGlobalBarIsFiltered(List<string> failures)
        {
            AbilityCatalog.Reload();
            var snapshot = SnapshotBarPrefs();
            GameObject go = null;
            try
            {
                // A pre-WO-1019 save: one shared bar holding whatever the last hero played left.
                PlayerPrefs.DeleteKey(EquipPrefKeys.SkillBarKeyFor("mage"));
                PlayerPrefs.SetString(EquipPrefKeys.SkillBarLegacyGlobalKey,
                                      "0=knight.thunderbolt;1=mage.blink;2=universal.dash");

                go = NewProbe(out var abilities, out _, out var bar);
                abilities.SetHeroClass("mage");

                if (bar.AbilityIdForSlot(0) != null)
                    failures.Add("[legacy-global] the mage inherited the KNIGHT entry '" +
                                 bar.AbilityIdForSlot(0) + "' from the legacy global hot-swap key - " +
                                 "that is the reported bug, unfixed.");
                if (bar.AbilityIdForSlot(1) != "mage.blink")
                    failures.Add("[legacy-global] the mage did NOT inherit its own entry 'mage.blink' (slot 1 = '" +
                                 (bar.AbilityIdForSlot(1) ?? "<null>") + "') - the migration must keep what the " +
                                 "hero legitimately owns, not wipe the bar.");
                if (bar.AbilityIdForSlot(2) != "universal.dash")
                    failures.Add("[legacy-global] the mage did NOT inherit the UNIVERSAL entry 'universal.dash' " +
                                 "(slot 2 = '" + (bar.AbilityIdForSlot(2) ?? "<null>") + "') - the shared pool is " +
                                 "usable by every class.");

                // The filtered result must be written to the class's OWN key, so the contamination
                // cannot come back through the legacy path on the next session.
                string mageRaw = PlayerPrefs.GetString(EquipPrefKeys.SkillBarKeyFor("mage"), null);
                if (mageRaw == null)
                    failures.Add("[legacy-global] the migrated bar was not persisted under '" +
                                 EquipPrefKeys.SkillBarKeyFor("mage") + "' - the legacy blob would be re-read " +
                                 "(and re-filtered) forever instead of being settled once.");
                else if (mageRaw.IndexOf("knight.", StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add("[legacy-global] the migrated mage key still contains a knight id: '" + mageRaw + "'.");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                RestoreBarPrefs(snapshot);
            }
        }

        // =====================================================================
        //  Case 5 - the wiring, pinned at source (comment-stripped)
        // =====================================================================

        private static void Case5_WiringLint(List<string> failures)
        {
            string bar      = StripComments(File.ReadAllText(SkillBarSrc));
            string loadout  = StripComments(File.ReadAllText(LoadoutSrc));
            string producer = StripComments(File.ReadAllText(ProducerSrc));

            // (1) The hot-swap bar must never go back to a hardcoded global key.
            if (Regex.IsMatch(bar, "\"dotr-skillbar-extra-v1\""))
                failures.Add("[wiring-lint] " + SkillBarSrc + " has re-introduced the literal GLOBAL key " +
                             "\"dotr-skillbar-extra-v1\" - the per-class key (EquipPrefKeys.SkillBarKeyFor) is " +
                             "the whole WO-1019 fix. The legacy literal belongs in EquipPrefKeys only, for the " +
                             "filtered migration read and the New Game reset.");
            if (!bar.Contains("SkillBarKeyFor"))
                failures.Add("[wiring-lint] " + SkillBarSrc + " no longer resolves its key through " +
                             "EquipPrefKeys.SkillBarKeyFor - the hot-swap bar is shared between heroes again.");
            if (!bar.Contains("EnsureCurrentKey"))
                failures.Add("[wiring-lint] " + SkillBarSrc + " no longer re-reads on a class-key change - " +
                             "a hero switch would keep the previous character's bar in memory.");

            // (2) BOTH rails must drop ids that are not the wearer's.
            if (!bar.Contains("IsUsableByClass"))
                failures.Add("[wiring-lint] " + SkillBarSrc + " no longer consults " +
                             "AbilityCatalog.IsUsableByClass - the class-validity drop is gone.");
            if (!loadout.Contains("IsUsableByClass"))
                failures.Add("[wiring-lint] " + LoadoutSrc + " no longer consults " +
                             "AbilityCatalog.IsUsableByClass - the W/E/R rail could present another class's kit.");

            // (3) The instrumentation is PERMANENT (CLAUDE.md §12): both rails must report what
            //     they bound. WO-967 added the qwer line; WO-1019 added bar= + the hotswap line.
            if (!producer.Contains("bar=qwer"))
                failures.Add("[wiring-lint] " + ProducerSrc + " no longer emits the 'ability bar bound: bar=qwer' " +
                             "trace - the W/E/R rail's binding is invisible in captures again (WO-967).");
            if (!producer.Contains("bar=hotswap"))
                failures.Add("[wiring-lint] " + ProducerSrc + " no longer emits the 'ability bar bound: bar=hotswap' " +
                             "trace - the rail the owner actually reported would be invisible in captures.");
            if (!producer.Contains("(was '"))
                failures.Add("[wiring-lint] " + ProducerSrc + " no longer names the PREVIOUS class on a rebind - " +
                             "a destination-only line cannot show a switch that failed to rebind.");
        }

        // =====================================================================
        //  Probe + PlayerPrefs safety
        // =====================================================================

        /// <summary>
        /// A real hero rig's ability components on an INACTIVE, HideAndDontSave GameObject.
        /// Inactive first is deliberate: AddComponent defers Awake/OnEnable, so the probe
        /// exercises only the class-key + validity seam - no rig, no Addressables, no VFX - and
        /// a batch run can never dirty an open scene. Both bars load lazily on first read via
        /// EnsureCurrentKey, which is precisely the seam under test.
        /// </summary>
        private static GameObject NewProbe(out HeroAbilities abilities, out HeroLoadout loadout,
                                           out AssignableSkillBar bar)
        {
            var go = new GameObject("HeroBarClassRebindProbe") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            abilities = go.AddComponent<HeroAbilities>();
            loadout   = go.AddComponent<HeroLoadout>();
            bar       = go.AddComponent<AssignableSkillBar>();
            return go;
        }

        // PlayerPrefs has no transaction, so snapshot every key this suite writes (absent keys
        // included - restoring an absent key means DELETING it, not writing "").
        private static Dictionary<string, string> SnapshotBarPrefs()
        {
            var keys = new List<string> { EquipPrefKeys.SkillBarLegacyGlobalKey };
            foreach (var cls in PlayableHeroes.AllKnownJobKeys())
            {
                keys.Add(EquipPrefKeys.LoadoutKeyFor(cls));
                keys.Add(EquipPrefKeys.SkillBarKeyFor(cls));
            }
            var snap = new Dictionary<string, string>(keys.Count, StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                snap[keys[i]] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i], string.Empty) : null;
            return snap;
        }

        private static void RestoreBarPrefs(Dictionary<string, string> snap)
        {
            if (snap == null) return;
            foreach (var kvp in snap)
            {
                if (kvp.Value == null) PlayerPrefs.DeleteKey(kvp.Key);
                else PlayerPrefs.SetString(kvp.Key, kvp.Value);
            }
            PlayerPrefs.Save();
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
