// =============================================================================
// ManaSpendRegression — pins the SPEND half of the class resource economy.
// -----------------------------------------------------------------------------
// regression-registry: registered by the committer (do NOT self-register here —
// DataRegression.cs is lane-fenced; the orchestrator adds the [mana-spend] row).
//
// WHY (owner report 2026-08-20, verbatim): "mana ... does not draw down on use
// and since it doesnt draw down and doesnt represent that on screen i can spam
// spells non stop making mage invincible".
//
// ClassResourceRegression already pins the AUTHORING side (every class has a
// resource block, costs fit the pool, some non-ultimate costs > 0). Nothing
// anywhere exercised the MECHANISM: that a cast actually CHARGES the pool and
// that an unaffordable cast is REFUSED. A suite that only reads JSON cannot fail
// the state the owner reported, so it is not a gate for it. This one drives the
// REAL component (a live HeroAbilities probe, the real AbilityCatalog, the real
// HeroAbilities.TryCast) and asserts the numbers:
//
//   Case 1 — CHARGE: for every authored ability with manaCost > 0 that has a
//            wind-up (castSeconds > 0, so the cast parks in CastRoutine and no
//            VFX/damage resolves in a batch run), TryCast must reduce the pool by
//            EXACTLY the effective cost (authored x MageManaCostMultiplier).
//            Deleting the `_mana -= manaCost` charge fails this outright.
//   Case 2 — REFUSAL: with the pool set just BELOW the cost, TryCast must return
//            false AND leave the pool untouched — no cast, no partial charge.
//            This is the "spam spells non stop" half.
//   Case 3 — INSTRUMENTATION (CLAUDE.md §12, instrumentation is permanent): the
//            charge and the refusal must each still emit a FlowTrace line naming
//            the numbers. Stripping them turns the next mana report back into a
//            no-evidence report.
//   Case 4 — PRESENTATION CHAIN (the separate on-screen half): the producer must
//            read live HeroAbilities.Mana into HeroVitalsModel.ManaExact, and the
//            kit must drive its ManaFill from ManaExact. Source-lint: the fill is
//            a runtime uGUI value no batch run can read.
//
// NOT IN SCOPE — BALANCE. This suite never asserts what a spell SHOULD cost.
// A spell authored at manaCost 0 (mage.fireball, knight.q, ranger.q today) is a
// legitimate free, cooldown-gated basic as far as this gate is concerned; the
// zero-cost roster is reported as a NOTE for the owner, never as a failure.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeNelle.Village;
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ManaSpendRegression
    {
        private const string AbilitiesSrc = "Assets/_Modules/Village/Hero/HeroAbilities.cs";
        private const string ProducersSrc = "Assets/_Modules/Village/HUD/HudModelProducers.cs";
        private const string HudKitSrc    = "Assets/_Modules/HUD/Kit/HudKitController.cs";

        private static readonly string[] PlayableClasses = { "mage", "knight", "ranger" };
        private const float Epsilon = 0.01f;

        /// <summary>Standalone batch entry — prints the MANA_SPEND_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("MANA_SPEND_OK - " + reason);
            else Debug.LogError("MANA_SPEND_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([mana-spend]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "charge+refusal", () => Case1And2_LiveChargeAndRefusal(failures, notes));
            Case(failures, "instrumentation", () => Case3_InstrumentationIsPermanent(failures));
            Case(failures, "presentation-chain", () => Case4_PresentationChain(failures));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "MANA SPEND OK - a live HeroAbilities probe charges the pool by exactly the " +
                         "effective cost on every costed wind-up ability, refuses (and charges nothing) " +
                         "when the pool is short, keeps its FlowTrace charge/refusal lines, and the " +
                         "producer -> HeroVitalsModel.ManaExact -> kit ManaFill chain is intact" + noteStr;
                return true;
            }
            reason = "MANA SPEND FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  Case 1 + 2 — the LIVE component probe
        // =====================================================================
        private static void Case1And2_LiveChargeAndRefusal(List<string> failures, List<string> notes)
        {
            var manaField = typeof(HeroAbilities).GetField("_mana",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (manaField == null)
            {
                failures.Add("[probe] HeroAbilities no longer has a private '_mana' field — the pool " +
                             "moved and this suite can no longer measure the charge. Re-point it.");
                return;
            }

            int costedTested = 0, freeSeen = 0;
            var freeIds = new List<string>();

            foreach (string cls in PlayableClasses)
            {
                foreach (AbilitySlot slot in new[] { AbilitySlot.Q, AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
                {
                    AbilityDef def = null;
                    var probe = NewProbe(cls, out var abilities);
                    try
                    {
                        def = abilities != null ? abilities.ResolvedDef(slot) : null;
                    }
                    finally { Destroy(probe); }

                    if (def == null)
                    {
                        notes.Add(cls + "/" + slot + " resolves no def (kit gap)");
                        continue;
                    }
                    if (def.ManaCost <= 0f)
                    {
                        freeSeen++;
                        freeIds.Add(cls + "/" + slot + " '" + def.Id + "'");
                        continue;   // BALANCE, not mechanism — reported as a note below.
                    }

                    float mult = HeroTalentModifiers.MageManaCostMultiplier(cls);
                    float expected = def.ManaCost * mult;

                    // ---- Case 2 (REFUSAL) — safe for every costed ability: nothing casts. ----
                    RunRefusalProbe(cls, slot, def, expected, manaField, failures);

                    // ---- Case 1 (CHARGE) — only wind-up spells, so the cast parks in
                    // CastRoutine and no damage/VFX/animator work resolves in a batch run.
                    if (def.CastSeconds > 0f)
                    {
                        RunChargeProbe(cls, slot, def, expected, manaField, failures);
                        costedTested++;
                    }
                }
            }

            if (costedTested == 0)
                failures.Add("[charge] NOTHING WAS MEASURED: no class authors a costed ability with a " +
                             "wind-up, so the charge assertion never ran. A gate that asserts nothing is " +
                             "not a gate — re-point this suite at whatever the costed kit is now.");

            notes.Add("charge asserted on " + costedTested + " costed wind-up abilities");
            if (freeSeen > 0)
                notes.Add("AUTHORED FREE (manaCost 0, cooldown-gated only — owner's balance call, not a " +
                          "failure): " + string.Join(", ", freeIds));
        }

        /// <summary>Pool set just under the cost -> the cast must be refused and charge NOTHING.</summary>
        private static void RunRefusalProbe(string cls, AbilitySlot slot, AbilityDef def, float expected,
                                            FieldInfo manaField, List<string> failures)
        {
            var probe = NewProbe(cls, out var abilities);
            try
            {
                if (abilities == null) return;
                float shortPool = Mathf.Max(0f, expected - 0.5f);
                manaField.SetValue(abilities, shortPool);

                bool fired;
                try { fired = abilities.TryCast(slot); }
                catch (Exception ex)
                {
                    failures.Add("[refuse] " + cls + "/" + slot + " '" + def.Id + "' THREW on an " +
                                 "unaffordable cast (" + ex.GetType().Name + ": " + ex.Message + ") — the " +
                                 "gate must refuse quietly, not blow up.");
                    return;
                }

                float after = (float)manaField.GetValue(abilities);
                if (fired)
                    failures.Add("[refuse] " + cls + "/" + slot + " '" + def.Id + "' CAST WITH AN EMPTY " +
                                 "POOL: cost " + expected.ToString("0.##") + " but pool was " +
                                 shortPool.ToString("0.##") + " and TryCast returned true. Spells are " +
                                 "unlimited — this is the 'spam spells non stop' state.");
                if (Mathf.Abs(after - shortPool) > Epsilon)
                    failures.Add("[refuse] " + cls + "/" + slot + " '" + def.Id + "' a REFUSED cast still " +
                                 "moved the pool (" + shortPool.ToString("0.##") + " -> " +
                                 after.ToString("0.##") + ").");

                if (abilities.CanCast(slot))
                    failures.Add("[refuse] " + cls + "/" + slot + " '" + def.Id + "' CanCast reports true " +
                                 "with pool " + shortPool.ToString("0.##") + " < cost " +
                                 expected.ToString("0.##") + " — the HUD affordability read and the real " +
                                 "gate disagree, so the button lies about what will happen.");
            }
            finally { Destroy(probe); }
        }

        /// <summary>Full pool -> the cast must charge EXACTLY the effective cost.</summary>
        private static void RunChargeProbe(string cls, AbilitySlot slot, AbilityDef def, float expected,
                                           FieldInfo manaField, List<string> failures)
        {
            var probe = NewProbe(cls, out var abilities);
            try
            {
                if (abilities == null) return;
                float before = (float)manaField.GetValue(abilities);
                if (before < expected)
                {
                    failures.Add("[charge] " + cls + "/" + slot + " '" + def.Id + "' the seeded pool (" +
                                 before.ToString("0.##") + ") cannot even pay the authored cost (" +
                                 expected.ToString("0.##") + ") — unaffordable-by-construction.");
                    return;
                }

                string threw = null;
                try { abilities.TryCast(slot); }
                catch (Exception ex) { threw = ex.GetType().Name; }   // wind-up coroutine cannot tick in edit mode

                float after = (float)manaField.GetValue(abilities);
                float drop = before - after;
                if (Mathf.Abs(drop - expected) > Epsilon)
                    failures.Add("[charge] " + cls + "/" + slot + " '" + def.Id + "' MANA DID NOT DRAW " +
                                 "DOWN BY ITS COST: pool " + before.ToString("0.##") + " -> " +
                                 after.ToString("0.##") + " (drop " + drop.ToString("0.##") + ") but the " +
                                 "effective cost is " + expected.ToString("0.##") + " (authored " +
                                 def.ManaCost.ToString("0.##") + ")" +
                                 (threw != null ? " [cast threw " + threw + " after the gate]" : "") + ".");
            }
            finally { Destroy(probe); }
        }

        /// <summary>
        /// A real HeroAbilities on a HideAndDontSave GameObject, class-seeded through the SAME public
        /// <see cref="HeroAbilities.SetHeroClass"/> the body swapper calls (that is what loads the
        /// class 'resource' block, so MaxMana is the authored pool, not the serialized default).
        /// <para>
        /// The pool is then filled through the public <see cref="HeroAbilities.RestoreMana"/> because
        /// EDIT MODE NEVER CALLS Awake — measured, not assumed: the first cut of this probe read a
        /// pool of 0 on every class ("the seeded pool (0) cannot even pay the authored cost (7)")
        /// while MaxMana already read 24, which is exactly the signature of Awake's `_mana =
        /// EffectiveMaxMana` never running. Filling it here keeps the CHARGE the thing under test.
        /// </para>
        /// Destroyed by the caller in a finally.
        /// </summary>
        private static GameObject NewProbe(string heroClass, out HeroAbilities abilities)
        {
            var go = new GameObject("ManaSpendProbe_" + heroClass) { hideFlags = HideFlags.HideAndDontSave };
            abilities = null;
            try
            {
                abilities = go.AddComponent<HeroAbilities>();
                abilities.SetHeroClass(heroClass);
                abilities.RestoreMana(abilities.MaxMana);   // edit mode ran no Awake — fill the pool
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ManaSpendRegression] probe build failed for '" + heroClass + "': " + ex.Message);
            }
            return go;
        }

        private static void Destroy(GameObject go)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // =====================================================================
        //  Case 3 — instrumentation is permanent (CLAUDE.md §12)
        // =====================================================================
        private static void Case3_InstrumentationIsPermanent(List<string> failures)
        {
            string src = ReadOrFail(failures, "instrumentation", AbilitiesSrc);
            if (src == null) return;

            if (!src.Contains("_mana -= manaCost"))
                failures.Add("[instrumentation] HeroAbilities no longer contains the charge line " +
                             "'_mana -= manaCost' — the deduct itself is gone.");
            if (!src.Contains("cast CHARGED"))
                failures.Add("[instrumentation] the 'cast CHARGED' FlowTrace line is gone from " +
                             "HeroAbilities. That line is the only place the cost, the pool before and " +
                             "the pool after are captured together; without it the next 'mana does not " +
                             "drain' report has no evidence again (CLAUDE.md §12 — never strip FlowTrace).");
            if (!src.Contains("cast REFUSED"))
                failures.Add("[instrumentation] the 'cast REFUSED' FlowTrace line is gone from " +
                             "HeroAbilities — the mana/cooldown gate is a silent refusal again.");
        }

        // =====================================================================
        //  Case 4 — the presentation chain (separate layer, separate defect)
        // =====================================================================
        private static void Case4_PresentationChain(List<string> failures)
        {
            string prod = ReadOrFail(failures, "presentation-chain", ProducersSrc);
            if (prod != null)
            {
                if (!prod.Contains("_abilities.Mana"))
                    failures.Add("[presentation-chain] HeroVitalsProducer no longer reads " +
                                 "'_abilities.Mana' — the HUD bar is no longer fed from the live pool, " +
                                 "so a spend cannot show on screen however correct the deduct is.");
                if (!prod.Contains("manaExact"))
                    failures.Add("[presentation-chain] HeroVitalsProducer no longer pushes the EXACT " +
                                 "mana float; the int-only push quantizes a small pool and the bar reads " +
                                 "as frozen between whole points.");
            }

            string kit = ReadOrFail(failures, "presentation-chain", HudKitSrc);
            if (kit != null)
            {
                if (!kit.Contains("ManaFill"))
                    failures.Add("[presentation-chain] HudKitController no longer drives a ManaFill — " +
                                 "nothing on screen represents the pool.");
                if (!kit.Contains("v.ManaExact"))
                    failures.Add("[presentation-chain] HudKitController no longer prefers " +
                                 "HeroVitalsModel.ManaExact for the fill, so sub-point spend/regen " +
                                 "stops reading on the bar.");
            }
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================
        private static void Case(List<string> failures, string tag, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + tag + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        private static string ReadOrFail(List<string> failures, string tag, string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[" + tag + "] cannot read " + path + " (" + ex.GetType().Name + ")");
                return null;
            }
        }
    }
}
