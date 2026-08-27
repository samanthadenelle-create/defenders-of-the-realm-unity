// =============================================================================
// TroopStrikeVfxRegression - WO-935 Phase 3: the troop STRIKE presentation rows.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Source-lint only, no scene load, no Play mode.
//
// WO-935 Phase 3 gives every troop strike a visible contact beat. Three mutually
// exclusive reads, decided by ONE resolver (TroopFactory.ResolveRoleController):
//   mage   -> CombatCast          (shipped 2026-08-15)
//   melee  -> Impact_Physical arc (slice 935-1)
//   archer -> a released arrow    (slice 935-2)
//
// WHY THIS IS SOURCE-LINTED RATHER THAN PLAYED: the assertion is about WHICH
// presentation call the strike branch makes and IN WHAT ORDER relative to the
// damage call. A play-mode run can show that something flashed; only the source
// can show that the damage line did not move, and "the damage line did not move"
// is the whole acceptance for slice 935-2.
//
// COMMENTS DO NOT COUNT, ON PURPOSE. Every read goes through SourceLint.ReadCode,
// which strips comments AND string-literal contents. This file is dense with
// comments naming Impact_Physical and FireArrow; a lint that counted them would
// pass on a file where the calls had been deleted and only the prose remained.
//
// WHAT THIS DELIBERATELY DOES NOT PIN:
//   - The catapult / siege row (slice 935-3) is BLOCKED on an owner VFX tag: there
//     is no Impact_Earth* value in VFXType and the CLI does not pick one (memory
//     vfx-map-owner-tags-no-creative-pick). Today a catapult resolves to the Knight
//     controller and therefore plays the MELEE arc at 26 m range. That is a known,
//     owner-pinned wrong read, NOT a regression, so nothing here asserts it either
//     way - pinning it would freeze the wrong answer in place.
//   - Which VFX row an archer's arrow uses. RangedAttackVFX owns that; this suite
//     pins that the incumbent launcher is the one being called, not its contents.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "troop strike vfx suite", () => { if (!DeNelle.Editor.Regression.TroopStrikeVfxRegression.Run(out var troopVfxReason)) failures.Add(troopVfxReason); else log.AppendLine("[troop-strike-vfx] " + troopVfxReason); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-935 Phase 3 oracle - troop melee + archer strike presentation.</summary>
    public static class TroopStrikeVfxRegression
    {
        private const string ControllerPath = "_Modules/Village/Troops/TroopController.cs";
        private const string FactoryPath = "_Modules/Village/Troops/TroopFactory.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TROOP STRIKE VFX (WO-935 Phase 3) ---");

            try
            {
                string controller = SourceLint.ReadCode(ControllerPath, failures);
                string attack = SourceLint.Body(controller, @"private\s+void\s+Attack\s*\(\s*IDamageable\s+foe\s*\)");
                if (string.IsNullOrEmpty(attack))
                {
                    failures.Add("TroopController.Attack(IDamageable) not found - the strike seam moved and " +
                                 "this oracle can no longer see any of the three presentation rows");
                }
                else
                {
                    GateOne_MeleeRow(attack, failures, log);
                    GateTwo_ArcherRow(attack, failures, log);
                    GateThree_DamageUnmoved(attack, failures, log);
                }

                GateFour_NoSecondMover(controller, failures, log);
                GateFive_OneResolver(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("troop-strike-vfx oracle threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("troop strike vfx FAILED (").Append(failures.Count).Append("):");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                sb.Append('\n').Append(log);
                reason = sb.ToString();
                return false;
            }

            reason = "5 gates OK (melee row, archer row, damage call unmoved, no second mover, one resolver).";
            return true;
        }

        // =====================================================================
        //  GATE 1 - the melee row (slice 935-1)
        // =====================================================================
        private static void GateOne_MeleeRow(string attack, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 1] melee row");

            if (Count(attack, "VFXType.Impact_Physical") == 0)
                failures.Add("TroopController.Attack no longer plays VFXType.Impact_Physical. The blow landing " +
                             "was the only silent beat in a troop melee exchange - on a structure target, which " +
                             "shows no health bar, the player cannot tell a hit from a whiff without it");
            else log.AppendLine("  Impact_Physical arc present ok");

            if (Count(attack, "HitSurfaceVfx") == 0)
                failures.Add("TroopController.Attack no longer calls HitSurfaceVfx - the MATERIAL read layered " +
                             "on top of the contact arc is gone");
            else log.AppendLine("  HitSurfaceVfx surface burst present ok");

            if (Count(attack, "playSound: false") == 0 && Count(attack, "playSound:false") == 0)
                failures.Add("the melee connect VFX no longer passes playSound:false. The attack cue belongs to " +
                             "the animator; letting VFXManager layer a second one double-hits every swing");
            else log.AppendLine("  playSound:false held ok");

            if (Count(attack, "Guard.Try") < 2)
                failures.Add("fewer than two Guard.Try wrappers in Attack - a VFX fault must never cost the " +
                             "damage that has already landed (CLAUDE.md section 12, no silent failures)");
        }

        // =====================================================================
        //  GATE 2 - the archer row (slice 935-2)
        // =====================================================================
        private static void GateTwo_ArcherRow(string attack, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 2] archer row");

            if (Count(attack, "_useBowShot") == 0)
                failures.Add("TroopController.Attack no longer branches on _useBowShot - the archer's strike " +
                             "has fallen back to the melee slash arc, which reads as the wrong verb for a " +
                             "bow release");
            else log.AppendLine("  bow-shot branch present ok");

            int fire = Count(attack, "FireArrow(");
            if (fire == 0)
                failures.Add("TroopController.Attack no longer calls FireArrow - the archer's shot is invisible " +
                             "again (instant damage with no released arrow)");
            else if (fire > 1)
                failures.Add("TroopController.Attack calls FireArrow " + fire + " times - one strike must " +
                             "release one arrow");
            else log.AppendLine("  exactly one FireArrow release ok");

            // The arrow must be pure decoration: no arrival callback, or the damage
            // silently re-times to the flight and DPS moves.
            if (Count(attack, "FireArrow(hitPos)") == 0 && fire > 0)
                failures.Add("FireArrow is no longer called as FireArrow(hitPos) with NO onArrive callback. " +
                             "An arrival payload re-times the damage to the arrow's flight, which changes DPS " +
                             "and can fire after this troop has died - option (b) of the work order, and a " +
                             "different ticket");
            else if (fire > 0) log.AppendLine("  no arrival payload (decoration only) ok");
        }

        // =====================================================================
        //  GATE 3 - the damage call site is UNMOVED (the whole 935-2 acceptance)
        // =====================================================================
        private static void GateThree_DamageUnmoved(string attack, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 3] damage call unmoved");

            int dmgIdx = attack.IndexOf("foe.TakeDamage(dmg, _element);", StringComparison.Ordinal);
            if (dmgIdx < 0)
            {
                failures.Add("the non-cast branch's instant damage call foe.TakeDamage(dmg, _element) is gone " +
                             "or was rewritten. Slice 935-2 is PRESENTATION ONLY - the damage must stay " +
                             "instant, in place, byte-identical");
                return;
            }

            int fireIdx = attack.IndexOf("FireArrow(", StringComparison.Ordinal);
            if (fireIdx >= 0 && fireIdx < dmgIdx)
                failures.Add("FireArrow is called BEFORE the damage lands. The arrow is decoration over a hit " +
                             "that has already resolved; releasing first invites a later seat to move the " +
                             "damage onto the arrival and change combat maths");
            else log.AppendLine("  damage resolves before the release, unchanged ok");

            // Nothing in the non-cast branch may re-scale the damage after it lands.
            int cd = Count(attack, "_attackCdRemaining = _attackCooldown");
            if (cd != 1)
                failures.Add("the attack cooldown assignment appears " + cd + " times in Attack (expected 1) - " +
                             "the strike cadence, and therefore DPS, is no longer set exactly once per attack");
        }

        // =====================================================================
        //  GATE 4 - no second projectile mover was written
        // =====================================================================
        private static void GateFour_NoSecondMover(string controller, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 4] no second mover");

            string[] banned = { "HS_ProjectileMover", "MoverProjectilePool", "ProjectilePool" };
            var found = new List<string>();
            foreach (var b in banned)
                if (Count(controller, b) > 0) found.Add(b);

            if (found.Count > 0)
                failures.Add("TroopController now reaches a projectile mover/pool directly (" +
                             string.Join(", ", found.ToArray()) + "). The work order forbids a second " +
                             "projectile mover by name: RangedAttackVFX is the incumbent launcher and owns " +
                             "the pooled body, the release flash and the arrival impact");
            else log.AppendLine("  no direct mover/pool reference ok");

            if (Count(controller, "RangedAttackVFX") == 0)
                failures.Add("TroopController no longer references RangedAttackVFX - the archer row has lost " +
                             "its launcher, or someone replaced the incumbent with a bespoke one");
        }

        // =====================================================================
        //  GATE 5 - one resolver decides all three strike reads
        // =====================================================================
        private static void GateFive_OneResolver(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gate 5] one resolver");

            string factory = SourceLint.ReadCode(FactoryPath, failures);
            string bow = SourceLint.Body(factory, @"public\s+static\s+bool\s+UsesBowShot\s*\([^)]*\)");
            if (string.IsNullOrEmpty(bow))
            {
                failures.Add("TroopFactory.UsesBowShot not found - the archer predicate moved or was deleted");
                return;
            }

            if (Count(bow, "ResolveRoleController") == 0)
                failures.Add("UsesBowShot no longer derives from ResolveRoleController. A second reading of " +
                             "def.Role or def.AttackRange is wrong twice over: the battlemage is authored " +
                             "role 'ranged' but must CAST, and an archer body can be picked by MODEL with no " +
                             "role at all. One resolver, three mutually exclusive strike reads");
            else log.AppendLine("  UsesBowShot derives from ResolveRoleController ok");

            string cast = SourceLint.Body(factory, @"public\s+static\s+bool\s+UsesCastStrike\s*\([^)]*\)");
            if (!string.IsNullOrEmpty(cast) && Count(cast, "ResolveRoleController") == 0)
                failures.Add("UsesCastStrike no longer derives from ResolveRoleController either - the two " +
                             "predicates can now disagree about the same troop");
        }

        private static int Count(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }
    }
}
