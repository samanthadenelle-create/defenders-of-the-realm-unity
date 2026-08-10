// =============================================================================
// HudHeroClassFallbackRegression [hud-class-fallback]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-967 (F8 seq 2312, owner verbatim: "in dungeon i have the knights action bar
// loading" / "as Thrain"). She was playing a MAGE. Three hand-written "knight"
// string literals in Assets/_Modules/Village/HUD/HudModelProducers.cs (the old
// :87 vitals, :139 party slot 0, :392 ability bar) made the HUD ASSERT a class
// instead of asking the state layer. A composed dungeon hero carries no
// HeroAbilities by construction (DungeonBaker.PopulateForPlay attaches only
// HeroLocomotion + HeroBodySwapper; HeroControlEnsurer.EnsureHeroCombatComponents
// provisions nine components and never that one), so FindAnyObjectByType returned
// null in every dungeon and the literal won.
//
// THIS DEFECT SHIPPED TWICE. GearLoadout.CurrentJob had the identical bug under
// F8 seq-642 - it armed a Knight body with a Mage staff and CORRUPTED A SAVE SLOT
// the owner never played (GearLoadout.cs:1251-1307). It was fixed there with the
// persisted-class fallback; the HUD reader never got the same treatment. This
// suite is what stops a THIRD occurrence.
//
// WHAT IT PROVES, AND HOW:
//
//   (a) BEHAVIOURAL PRECEDENCE - HudHeroClassResolver.ResolveFrom is exercised
//       directly for all four steps: live HeroAbilities > PERSISTED
//       GameState.HeroClass > the producer's own cached class > the loud catalog
//       default. The load-bearing case is the dungeon one: NO live abilities +
//       a persisted MAGE must resolve "mage", and must report it came from the
//       persisted state - never a literal. The pure overload is used on purpose:
//       standing up a real GameStateService in a batch run would touch the
//       player's actual PlayerPrefs save (its Awake Loads and can persist a
//       default class), which a regression must never do. The runtime callers
//       reach this exact method through Resolve(HeroAbilities, ...).
//
//   (b) SOURCE PIN (comment-stripped lint) - HudModelProducers.cs must contain
//       ZERO bare class-name string literals ("knight" / "mage" / "ranger" /
//       "cleric" / "healer"), all class reads must route through the shared
//       resolver, the resolver must still consult GameStateService, and its
//       provenance instrumentation (FlowTrace Once + the loud Warn on the
//       default path) must still be present - CLAUDE.md section 12 makes those
//       traces PERMANENT, and a silent default is precisely what let a Knight bar
//       sit on a Mage unnoticed. Comments are stripped first so this file's own
//       history notes (which quote the old literals) can never green-tick it.
//
//   NOT provable here: that the dungeon bar VISUALLY renders Fireball/Arcane
//   Shell/Mend/Meteor Strike - that is UI_CAPTURE + the owner's felt-verify
//   (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: HUD_CLASS_FALLBACK_OK / HUD_CLASS_FALLBACK_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HudHeroClassFallbackRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hud;

namespace DeNelle.Editor.Regression
{
    public static class HudHeroClassFallbackRegression
    {
        private const string ProducersSrc = "Assets/_Modules/Village/HUD/HudModelProducers.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HUD_CLASS_FALLBACK_OK - " + reason);
            else Debug.LogError("HUD_CLASS_FALLBACK_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "precedence",   () => Case1_Precedence(failures));
                Case(failures, "no-literals",  () => Case2_SourcePin(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "HUD HERO CLASS OK - the HUD resolves the hero class live-abilities > " +
                         "PERSISTED GameState.HeroClass > cached > loud catalog default, a null " +
                         "HeroAbilities with a persisted MAGE resolves 'mage' from the state (not a " +
                         "literal), every step names its source, and HudModelProducers.cs carries no " +
                         "bare class-name literal and still routes all three producers through the " +
                         "shared resolver with its provenance traces intact.";
                return true;
            }
            reason = "hud-class-fallback FAIL x" + failures.Count + ": " + string.Join(" | ", failures.ToArray());
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the resolution ORDER, exercised
        // =====================================================================

        private static void Case1_Precedence(List<string> failures)
        {
            string source;

            // 1. A live HeroAbilities class always wins - the town path. This must stay an
            //    identity so WO-967 is a pure no-op in the village.
            string cls = HudHeroClassResolver.ResolveFrom("ranger", "knight", "cleric", out source);
            if (cls != "ranger" || source != HudHeroClassResolver.SourceLive)
                failures.Add("[precedence] a LIVE HeroAbilities class must win outright - got '" + cls +
                             "' (source=" + source + "), expected 'ranger' from " +
                             HudHeroClassResolver.SourceLive + ". The village bar would now disagree " +
                             "with the hero's own component.");

            // 2. THE WO-967 CASE. No live abilities (every composed dungeon hero) + a persisted
            //    MAGE must resolve 'mage' FROM THE STATE. This is the assert that fails if anyone
            //    ever reintroduces a hardcoded class.
            cls = HudHeroClassResolver.ResolveFrom(null, "mage", null, out source);
            if (cls != "mage")
                failures.Add("[precedence] WO-967: with NO HeroAbilities (a composed dungeon hero) and " +
                             "GameState.HeroClass = Mage, the HUD resolved '" + cls + "' instead of 'mage'. " +
                             "This is the exact defect the owner reported - a Knight action bar on a Mage " +
                             "in Dungeon_HealersCottage - and it is the SECOND time this class default " +
                             "shipped (GearLoadout, F8 seq-642, corrupted a save slot).");
            if (source != HudHeroClassResolver.SourcePersisted)
                failures.Add("[precedence] the no-abilities + persisted-Mage path reported source='" + source +
                             "', expected " + HudHeroClassResolver.SourcePersisted + ". The class must be " +
                             "READ from the state layer, not assumed - presentation never owns game state.");

            // 3. Persisted Knight resolves Knight - right BECAUSE the state says so, not because a
            //    literal said so. (Same code path, opposite answer: proves it is a real read.)
            cls = HudHeroClassResolver.ResolveFrom(null, "knight", null, out source);
            if (cls != "knight" || source != HudHeroClassResolver.SourcePersisted)
                failures.Add("[precedence] a persisted KNIGHT must resolve 'knight' from " +
                             HudHeroClassResolver.SourcePersisted + " - got '" + cls + "' (source=" +
                             source + ").");

            // 4. The persisted state OUT-RANKS the producer's own cached class: the cache is
            //    presentation memory, the state is truth (it is what HeroBodySwapper built the BODY
            //    from). They agree in every real flow; when they disagree the state wins.
            cls = HudHeroClassResolver.ResolveFrom(null, "mage", "knight", out source);
            if (cls != "mage" || source != HudHeroClassResolver.SourcePersisted)
                failures.Add("[precedence] a stale HUD cache ('knight') beat the persisted state ('mage') - " +
                             "got '" + cls + "' (source=" + source + "). Presentation memory must never " +
                             "out-rank the state layer.");

            // 5. Cache is the last memory before the default - it must still be honoured when no
            //    state service is up (an early-boot poll), so the bar does not flicker to a default.
            cls = HudHeroClassResolver.ResolveFrom(null, null, "mage", out source);
            if (cls != "mage" || source != HudHeroClassResolver.SourceCache)
                failures.Add("[precedence] with no live component and no persisted class, the producer's " +
                             "cached 'mage' must be used (source=" + HudHeroClassResolver.SourceCache +
                             ") - got '" + cls + "' (source=" + source + ").");

            // 6. Nothing answered: the catalog default, reported AS the default. Asserted against
            //    AbilityCatalog.DefaultClass rather than a spelled-out class, so this case can never
            //    itself become the hardcoded literal it exists to forbid.
            cls = HudHeroClassResolver.ResolveFrom(null, null, null, out source);
            if (cls != AbilityCatalog.DefaultClass || source != HudHeroClassResolver.SourceDefault)
                failures.Add("[precedence] with nothing to read, the HUD must fall back to " +
                             "AbilityCatalog.DefaultClass ('" + AbilityCatalog.DefaultClass + "') and SAY so " +
                             "(source=" + HudHeroClassResolver.SourceDefault + ") - got '" + cls +
                             "' (source=" + source + ").");
        }

        // =====================================================================
        //  Case 2 - the source pin: no class literal may come back
        // =====================================================================

        private static void Case2_SourcePin(List<string> failures)
        {
            if (!File.Exists(ProducersSrc))
            {
                failures.Add("[no-literals] " + ProducersSrc + " not found - the WO-967 source pin cannot run. " +
                             "If the producers moved, move this pin with them; do not delete it.");
                return;
            }

            string src = StripComments(File.ReadAllText(ProducersSrc));

            // (1) THE PIN. A bare class-name string literal in the HUD producers is the bug itself.
            //     Quotes are part of the pattern, so ability ids like "knight.q" are untouched.
            var literal = new Regex("\"(knight|mage|ranger|cleric|healer)\"", RegexOptions.IgnoreCase);
            var hits = literal.Matches(src);
            if (hits.Count > 0)
            {
                var found = new List<string>();
                foreach (Match m in hits) found.Add(m.Value);
                failures.Add("[no-literals] " + ProducersSrc + " contains " + hits.Count +
                             " hardcoded hero-class string literal(s) " + string.Join(",", found.ToArray()) +
                             ". THIS IS WO-967 REGRESSING. A HUD producer must ASK " +
                             "HudHeroClassResolver for the class (live HeroAbilities > persisted " +
                             "GameState.HeroClass > cached > catalog default), never assert one - a " +
                             "composed dungeon hero has no HeroAbilities, so the literal always wins there " +
                             "and the player gets another class's kit.");
            }

            // (2) All three readers (vitals, party slot 0, ability bar) must route through the resolver.
            int routed = Regex.Matches(src, @"HudHeroClassResolver\s*\.\s*Resolve\s*\(").Count;
            if (routed < 3)
                failures.Add("[no-literals] only " + routed + " call site(s) route through " +
                             "HudHeroClassResolver.Resolve - expected at least 3 (HeroVitalsProducer, " +
                             "PartyProducer slot 0, AbilityLoadoutProducer). A producer that stopped asking " +
                             "the resolver has gone back to inventing a class.");

            // (3) The resolver must still READ the persisted state - the whole point of the fix.
            if (!Regex.IsMatch(src, @"GameStateService\s*\.\s*Instance") ||
                !Regex.IsMatch(src, @"PlayableHeroes\s*\.\s*JobKey"))
                failures.Add("[no-literals] the HUD class resolver no longer reads " +
                             "GameStateService.Instance.State.HeroClass through PlayableHeroes.JobKey - " +
                             "that persisted read IS the WO-967 fix (and the same one GearLoadout.CurrentJob " +
                             "needed under F8 seq-642).");

            // (4) The provenance instrumentation is PERMANENT (CLAUDE.md section 12). The default path
            //     must stay the LOUD one - a silent wrong-class default is what cost the owner a session.
            if (!Regex.IsMatch(src, @"FlowTrace\s*\.\s*Once\s*\(\s*""HudModel"""))
                failures.Add("[no-literals] the class-provenance FlowTrace.Once is gone from the HUD class " +
                             "resolver - the persisted-class path would resolve silently again and the next " +
                             "capture could not prove where the class came from.");
            if (!Regex.IsMatch(src, @"FlowTrace\s*\.\s*Warn\s*\(\s*""HudModel"""))
                failures.Add("[no-literals] the loud FlowTrace.Warn on the catalog-default path is gone - a " +
                             "SILENT default is exactly the no-silent-failure violation CLAUDE.md section 12 " +
                             "forbids, and exactly how a Knight bar sat on a Mage unnoticed.");
            if (!Regex.IsMatch(src, @"ability bar bound"))
                failures.Add("[no-literals] the 'ability bar bound' FlowTrace.Step is gone - the right-hand " +
                             "ability bar emitted NOTHING before WO-967 (zero hits for the Knight skill names " +
                             "in the owner's whole session log), which is why only her eyes could catch this.");
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
