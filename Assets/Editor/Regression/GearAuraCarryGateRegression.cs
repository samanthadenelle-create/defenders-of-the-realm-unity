// =============================================================================
// GearAuraCarryGateRegression [gear-aura-carry]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-959 (owner ruling 2026-08-10, F8 seq 2297, verbatim: "can we agree to only show
// the flames on the sword when unsheathed?"): element weapon auras render ONLY while
// the weapon is DRAWN. The shipped seam is two-sided:
//
//   * EquipmentController.IsWeaponDrawn - the carry-state truth (the same predicate
//     ApplyHoldPose physically seats the prop by), plus OnCarryStateChanged, raised
//     ONCE per flip after the re-seat.
//   * GearAura.Refresh - the ONE gate: the weapon-seat want resolves to None while
//     not drawn, so acquire-on-draw / release-on-sheathe ride the existing verified
//     StartWeapon/StopWeapon paths.
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) LIVE COMPONENT PROBE - the carry-state contract runs on a REAL
//       EquipmentController: fresh = sheathed; SetCombatActive(true) flips
//       IsWeaponDrawn and raises the event exactly once with `true`; a repeat call
//       raises NOTHING (the per-frame no-change re-assert must never fire it at
//       frame rate); SetCombatActive(false) raises exactly once with `false`.
//       The probe GameObject is INACTIVE (AddComponent defers Awake/OnEnable, so no
//       rig / loadout / Addressables machinery is touched) and HideAndDontSave so a
//       batch run can never dirty an open scene.
//
//   (b) SOURCE INVARIANT (comment-stripped lint) - the aura gate itself needs a live
//       VFXManager + a play session to run, so the wiring is pinned at source
//       instead of green-ticked over a null: GearAura must consult IsWeaponDrawn in
//       its resolve, subscribe OnCarryStateChanged, and EquipmentController must
//       actually invoke the event. The lint fails if the gate is quietly removed.
//
//   NOT provable here: the flame actually appearing/disappearing on the blade -
//   that is the owner's felt-verify (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: GEAR_AURA_CARRY_OK / GEAR_AURA_CARRY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.GearAuraCarryGateRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class GearAuraCarryGateRegression
    {
        private const string GearAuraSrc  = "Assets/_Modules/Village/Hero/GearAura.cs";
        private const string EquipSrc     = "Assets/_Modules/Village/Hero/EquipmentController.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("GEAR_AURA_CARRY_OK - " + reason);
            else Debug.LogError("GEAR_AURA_CARRY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "carry-contract", () => Case1_CarryStateContract(failures));
                Case(failures, "gate-wiring",    () => Case2_GateWiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "GEAR AURA CARRY OK - a real EquipmentController starts sheathed, flips " +
                         "IsWeaponDrawn on SetCombatActive, raises OnCarryStateChanged exactly once " +
                         "per FLIP (never on the per-frame no-change re-assert), and the WO-959 " +
                         "drawn-only gate + subscription + event invoke are all present at source.";
                return true;
            }
            reason = "gear-aura-carry FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the LIVE carry-state contract on a real EquipmentController
        // =====================================================================

        private static void Case1_CarryStateContract(List<string> failures)
        {
            GameObject go = null;
            try
            {
                // INACTIVE first: AddComponent on an inactive GameObject defers Awake/OnEnable,
                // so the probe exercises ONLY the carry-state seam - no rig cache, no loadout
                // subscribe, no Addressables. That is deliberate: this case is about the
                // contract WO-959's gate consumes, not about equipping.
                go = new GameObject("GearAuraCarryGateProbe") { hideFlags = HideFlags.HideAndDontSave };
                go.SetActive(false);
                var equip = go.AddComponent<EquipmentController>();

                if (equip.IsWeaponDrawn)
                    failures.Add("[carry-contract] a FRESH EquipmentController reports IsWeaponDrawn=true - " +
                                 "a new hero must start SHEATHED (town carry), or the flame renders in town " +
                                 "before any combat signal ever fires.");

                var events = new List<bool>();
                equip.OnCarryStateChanged += d => events.Add(d);

                // Draw.
                equip.SetCombatActive(true);
                if (!equip.IsWeaponDrawn)
                    failures.Add("[carry-contract] SetCombatActive(true) did not flip IsWeaponDrawn to true.");
                if (events.Count != 1 || !events[0])
                    failures.Add("[carry-contract] the DRAW flip raised " + Describe(events) +
                                 " - expected exactly one event with drawn=true (raised after the re-seat).");

                // Per-frame no-change re-assert (HeroLocomotion calls SetCombatActive every frame;
                // its no-change path still runs ApplyHoldPose). MUST be silent or the event fires
                // at frame rate and every subscriber Refreshes 60x/sec.
                equip.SetCombatActive(true);
                equip.SetCombatActive(true);
                if (events.Count != 1)
                    failures.Add("[carry-contract] the no-change SetCombatActive(true) re-assert raised the " +
                                 "event again (" + Describe(events) + ") - it must be change-only.");

                // Sheathe.
                equip.SetCombatActive(false);
                if (equip.IsWeaponDrawn)
                    failures.Add("[carry-contract] SetCombatActive(false) did not flip IsWeaponDrawn to false.");
                if (events.Count != 2 || events[1])
                    failures.Add("[carry-contract] the SHEATHE flip raised " + Describe(events) +
                                 " - expected a second event with drawn=false (the release edge WO-959 " +
                                 "puts the flame out on).");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static string Describe(List<bool> events)
        {
            return events.Count + " event(s) [" + string.Join(",", events) + "]";
        }

        // =====================================================================
        //  Case 2 - the gate wiring, pinned at source (comment-stripped)
        // =====================================================================

        // The aura side of the gate cannot run headless (StartWeapon needs a live VFXManager +
        // a measured blade prop), so its three load-bearing lines are pinned as source
        // invariants instead - a green tick over a null VFXManager would be worthless.
        private static void Case2_GateWiringLint(List<string> failures)
        {
            string aura  = StripComments(File.ReadAllText(GearAuraSrc));
            string equip = StripComments(File.ReadAllText(EquipSrc));

            // (1) GearAura's resolve consults the drawn state - the gate itself.
            if (!Regex.IsMatch(aura, @"!\s*IsWeaponDrawn\s*\(\s*\)"))
                failures.Add("[gate-wiring] " + GearAuraSrc + " no longer consults !IsWeaponDrawn() - " +
                             "the WO-959 drawn-only gate has been removed from the aura resolve.");

            // (2) GearAura subscribes the flip edge (acquire-on-draw / release-on-sheathe).
            if (!aura.Contains("OnCarryStateChanged +="))
                failures.Add("[gate-wiring] " + GearAuraSrc + " no longer subscribes " +
                             "EquipmentController.OnCarryStateChanged - stance flips would not re-resolve " +
                             "the weapon seat.");

            // (3) EquipmentController actually raises the event (a subscribed-but-never-raised
            //     event is the silent variant of the same regression).
            if (!Regex.IsMatch(equip, @"handler\s*\(\s*drawn\s*\)") &&
                !Regex.IsMatch(equip, @"OnCarryStateChanged\s*\?\s*\.\s*Invoke"))
                failures.Add("[gate-wiring] " + EquipSrc + " no longer invokes OnCarryStateChanged from " +
                             "the hold-pose path - the flip edge is dead.");
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
