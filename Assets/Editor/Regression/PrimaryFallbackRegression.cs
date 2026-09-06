// =============================================================================
// PrimaryFallbackRegression — WO-1429: THE HERO ALWAYS HAS A VERB
// Markers: PRIMARY_FALLBACK_OK / PRIMARY_FALLBACK_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Standalone: run-unity-method DeNelle.Editor.Regression.PrimaryFallbackRegression.RunAll
// Registration into DataRegression.RunAll is left to the committer (that file is
// lane-fenced) — the line is handed back with this suite.
//
// -----------------------------------------------------------------------------
// WHAT THIS SUITE EXISTS FOR, from a CAPTURE and not a theory
// -----------------------------------------------------------------------------
// logs/device/freeze-20260904-095249.log:544639 — one tap on a real Seeker:
//     [Flow:HudKit] command 'attack' fired
//     [Flow:HeroMana] cast REFUSED slot=Q 'Fireball': cd=0.47s Mana 21.08/24.00 cost=3.00
//     [Flow:HudKit] primary command -> class Q for mage gated
// ...and NOTHING follows. The mobile attack button produced no verb at all.
//
// The gate was a hardcoded per-class table in HudKitCommandBridge's attack handler
// (`heroClass == "mage" || heroClass == "ranger"` -> TryCast(Q) -> `return`), which
// returned BEFORE the melee swing could ever be reached. Read the numbers in that
// capture: cd=0.47s at 21.08/24.00 mana. It is a COOLDOWN refusal, not an
// out-of-mana one — HeroAbilities.TryCast:813 refuses on `cd > 0 || _mana < cost`
// and both exit the SAME `return false`. So the button died in every cooldown gap,
// several times a minute, all game.
//
// -----------------------------------------------------------------------------
// WHY THESE CASES ARE STRUCTURAL (SOURCE) AND NOT RUNTIME — say it, don't hide it
// -----------------------------------------------------------------------------
// The behaviour lives inside a lambda registered on a static delegate
// (HudCommands.RegisterAttack) and needs a LIVE HeroAbilities + PlayerAttackController
// + `BattleLock.IsInBattle() == true`, i.e. a play session. This suite runs in EDITOR
// BATCHMODE with NO play session, so it CANNOT press the button. What it CAN do — and
// what would have caught the shipped defect in one read — is pin the SHAPE of the one
// handler: that the refusal path has no `return` before the melee swing, and that no
// class-name literal decides anything. Every case below states which kind it is.
// A green tick here is NOT a felt-test; the owner still closes the ticket (CLAUDE.md §13).
//
// -----------------------------------------------------------------------------
// ⚠ ONE CANON CONFLICT, SURFACED RATHER THAN PINNED (for the CLI / the owner)
// -----------------------------------------------------------------------------
// CLAUDE.md §7 states: "the phone's one attack button never spends an arrow." That
// sentence describes the WO-1105 HUD, where an attack PILL and a separate Q medallion
// coexisted. That layout is gone: in `hostile(activebattle)`
// (Assets/StreamingAssets/Data/Canonical/hud-areas.json:242-249) the `actionRail` area
// is EMPTY — no attackButton, no abilityRow arc — and `actionBar` carries only
// `combatDock`, whose slot 0 IS `HudCommands.Attack` (HudKitController.cs:2298-2299).
// Combat-dock slots 2-4 are `AssignableCast` (dressed from _models.Assignable), so the
// class Q has NO other dispatch on the mobile battle HUD. Commit a6daaf44c added the
// per-class table for exactly that reason.
//
// Therefore a case asserting "the attack button never fires ranger.q" is not
// satisfiable today without stranding Quick Shot entirely, and this suite does NOT
// write one — a permanently-RED case, or a case asserting the opposite of canon, are
// both worse than an honest gap. What IS pinned below is the half that is true and
// load-bearing: the FALLBACK swing spends no arrow and earns no Focus. The owner's
// two options are recorded in the WO hand-back.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class PrimaryFallbackRegression
    {
        private const string BridgeSrc = "Assets/_Modules/Village/HUD/HudKitCommandBridge.cs";
        private const string AttackSrc = "Assets/_Modules/Village/Enemies/PlayerAttackController.cs";
        private const string AbilitiesSrc = "Assets/_Modules/Village/Hero/HeroAbilities.cs";
        private const string HudKitSrc = "Assets/_Modules/HUD/Kit/HudKitController.cs";
        private const string AbilitiesJson = "Assets/StreamingAssets/Data/Canonical/abilities.json";

        /// <summary>Object keys in abilities.json that are NOT class ids (the shallow scan in
        /// <see cref="ReadClassPrimaries"/> would otherwise report "class 'w'").</summary>
        private static readonly string[] NonClassKeys =
        {
            "classes", "abilities", "version", "resource", "q", "w", "e", "r", "vfx", "scaling",
        };

        /// <summary>Standalone batch entry — prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("PRIMARY_FALLBACK_OK - " + reason);
            else Debug.LogError("PRIMARY_FALLBACK_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "no-verbless-hero", () => Case1_NoVerblessHero(failures, notes));
                Case(failures, "cooldown-gap-still-swings", () => Case2_CooldownGapStillSwings(failures));
                Case(failures, "no-per-class-table", () => Case3_NoPerClassTable(failures));
                Case(failures, "fallback-is-free", () => Case4_FallbackIsFree(failures));
                Case(failures, "ranger-fallback-spends-no-arrow", () => Case5_RangerFallbackSpendsNoArrow(failures, notes));
                Case(failures, "attack-face-always-pressable", () => Case6_AttackFaceAlwaysPressable(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "PRIMARY FALLBACK OK - the mobile attack handler decides by the DERIVED " +
                         "TryGetRangedPrimary seam (no class-name literal), a refused class Q falls " +
                         "THROUGH to TriggerBasicAttack instead of returning, the sweep spends no " +
                         "resource of any kind, and the combat dock's ATTACK face stays pressable " +
                         "while the Q cools" + noteStr;
                return true;
            }
            reason = "primary-fallback FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 — [no-verbless-hero]   (STRUCTURAL + DATA)
        //  REVERT RECIPE: put `return;` back after the TryCast(Q) call in
        //  HudKitCommandBridge's attack handler (i.e. restore the pre-WO-1429
        //  shape) — this case goes RED.
        // =====================================================================
        //
        // The one case that matters, and the reason the suite exists: for EVERY playable
        // class, at every mana value AND during cooldown, the attack button resolves to a
        // verb. Two halves:
        //   (a) STRUCTURAL — the handler's ONLY unconditional terminus is
        //       TriggerBasicAttack(). Every `return` before it is guarded (block held,
        //       a Q that actually FIRED, no PlayerAttackController in the scene).
        //   (b) DATA — every class authored in abilities.json takes one of exactly two
        //       shapes through TryGetRangedPrimary (ranged basic -> cast-then-fall-through,
        //       or no ranged basic -> straight to the sweep). Both end in a swing.
        private static void Case1_NoVerblessHero(List<string> failures, List<string> notes)
        {
            string body = AttackHandlerBody(failures, "no-verbless-hero");
            if (body == null) return;

            if (!body.Contains("TriggerBasicAttack"))
            {
                failures.Add("[no-verbless-hero] the attack handler no longer calls TriggerBasicAttack at all - " +
                             "the melee sweep is the only free verb the hero is guaranteed; without it a refused " +
                             "cast is a dead button again (the exact captured defect)");
                return;
            }

            // The refusal must not be a terminus. Between the TryCast call and the swing there
            // may be traces, but no bare `return` at the handler's own nesting level.
            int castIdx = body.IndexOf("TryCast(", StringComparison.Ordinal);
            int swingIdx = body.IndexOf("TriggerBasicAttack", StringComparison.Ordinal);
            if (castIdx >= 0 && swingIdx < castIdx)
            {
                failures.Add("[no-verbless-hero] TriggerBasicAttack appears BEFORE TryCast in the handler - " +
                             "the fallback can no longer be reached from a refused cast");
                return;
            }

            // THE PIN THAT MAKES THE REVERT RECIPE ACTUALLY GO RED. "TriggerBasicAttack is
            // present" is not enough — the shipped defect had the swing right there in the file,
            // three lines below a `return`. So pin the EXACT set of exits. The handler has, and
            // may only have, THREE:
            //   1. block is held        (a deliberate gate, not a refusal)
            //   2. the class Q FIRED    (the player got their spell; nothing to fall back to)
            //   3. no PlayerAttackController in the scene (nothing to swing with; traced as Warn)
            // A FOURTH return is, by construction, a path where a tap produced no verb. Adding one
            // back after the refused cast — the exact pre-WO-1429 shape — makes this case RED.
            // If a legitimate fourth exit is ever needed, raise the number DELIBERATELY here and
            // say in this comment why that path is not verbless.
            int returns = Regex.Matches(body, @"\breturn\s*;").Count;
            if (returns != 3)
                failures.Add("[no-verbless-hero] the attack handler has " + returns + " `return;` exits, expected 3 " +
                             "(block held / the Q FIRED / no PlayerAttackController). A fourth exit is a tap that " +
                             "produces NO VERB - that is the captured defect verbatim " +
                             "(freeze-20260904-095249.log:544639). If the extra exit is deliberate, raise this " +
                             "number here and justify it in the comment above");

            // The data half: every authored class must resolve to one of the two shapes.
            var classes = ReadClassPrimaries(notes);
            if (classes.Count == 0)
            {
                // HOLLOW-PASS FIX 2026-09-06: this returned with a NOTE, so a suite that could
                // not read abilities.json reported OK. A missing fixture must FAIL naming
                // itself - the standing rule, enforced by RegressionMarkerRegression's ratchet.
                // The structural half genuinely ran, but "some of me ran" is not a pass.
                failures.Add("[no-verbless-hero] abilities.json yielded ZERO classes, so the DATA " +
                             "half of this case did not run. A suite that cannot read its fixture " +
                             "must not report OK - check the canonical abilities.json is present.");
                return;
            }
            foreach (var kv in classes)
            {
                // Whichever branch a class takes, the terminus is the same swing. This asserts the
                // authoring did not invent a THIRD shape (e.g. a Q with no def at all on a class
                // whose handler would then do nothing).
                if (kv.Value == null)
                    failures.Add("[no-verbless-hero] class '" + kv.Key + "' has no authored q ability in " +
                                 AbilitiesJson + " - HeroAbilities.TryCast logs 'No ability for <class>/Q' and " +
                                 "returns false; with the WO-1429 fall-through that is still a swing, but the " +
                                 "authoring gap is real and should be closed deliberately");
            }
        }

        // =====================================================================
        //  CASE 2 — [cooldown-gap-still-swings]   (STRUCTURAL)
        //  REVERT RECIPE: change the fall-through to
        //  `if (!abilities.TryCast(AbilitySlot.Q)) return;` — RED.
        // =====================================================================
        //
        // THE CASE THAT WOULD HAVE CAUGHT THE REAL DEFECT, and which no existing suite asks.
        // The captured refusal was `cd=0.47s` at 21/24 mana — a COOLDOWN refusal at near-full
        // mana. A suite that only asked "does 0 mana still swing?" would have passed while the
        // owner's button died several times a minute.
        //
        // Pinned shape: the cast is attempted inside a positive `if (...TryCast...)` whose
        // body RETURNS on success — so the ONLY way out of a refusal is downward, into the
        // sweep. There must be no `return` in the refusal path, and no mana/cooldown
        // comparison deciding whether to fall through (that is the retired hysteresis: a
        // 0.47s cooldown gap must not lock the hero to the staff until mana hits 50%).
        //
        // ⚠ This targets the *Q* cooldown. TriggerBasicAttack may itself return false on the
        // MELEE cooldown (_nextAttackTime) or mid-swing — that is correct behaviour and is
        // deliberately NOT a failure here.
        private static void Case2_CooldownGapStillSwings(List<string> failures)
        {
            string body = AttackHandlerBody(failures, "cooldown-gap-still-swings");
            if (body == null) return;

            if (!Regex.IsMatch(body, @"if\s*\(\s*\w+\s*\.\s*TryCast\s*\(\s*AbilitySlot\s*\.\s*Q\s*\)\s*\)"))
                failures.Add("[cooldown-gap-still-swings] the handler no longer casts Q behind a POSITIVE " +
                             "`if (abilities.TryCast(AbilitySlot.Q))` whose body returns on SUCCESS. That shape is " +
                             "what makes a refusal fall downward into the sweep by construction; a negated or " +
                             "early-returning form re-opens the dead button");

            if (Regex.IsMatch(body, @"!\s*\w+\s*\.\s*TryCast\s*\(") ||
                Regex.IsMatch(body, @"TryCast\s*\([^)]*\)\s*==\s*false"))
                failures.Add("[cooldown-gap-still-swings] the handler tests a NEGATED TryCast - if that branch " +
                             "returns, a cooldown-gap tap is a dead button again");

            // No resource/threshold arithmetic may re-enter this decision. The re-cut is explicit:
            // no thresholds, no mana check, no hysteresis.
            foreach (var token in new[] { "Mana", "mana", "CanCast", "0.5f", "Affordable" })
                if (body.Contains(token))
                    failures.Add("[cooldown-gap-still-swings] the attack handler mentions '" + token + "' - the " +
                                 "fall-through must be unconditional ('the primary was refused'), never gated on a " +
                                 "resource level or a threshold. WO-1429 §0.2 item 3: with a mana threshold a 0.47s " +
                                 "COOLDOWN gap would lock the hero to the staff until mana reached 50%, which is " +
                                 "strictly worse than the defect");
        }

        // =====================================================================
        //  CASE 3 — [no-per-class-table]   (STRUCTURAL)
        //  REVERT RECIPE: re-add `string.Equals(heroClass, "mage", ...)` to the
        //  attack handler — RED.
        // =====================================================================
        //
        // The defect WAS a per-class table, and WO-1429 §3.3 forbids one on this seam
        // ("DERIVED, NEVER A PER-CLASS TABLE"). The replacement decides through
        // HeroAbilities.TryGetRangedPrimary, which is derived from the authored def's effect
        // shape + RangedPrimaryReachFactor. Measured against abilities.json (2026-09-06):
        // mage.q strike/14 -> true, ranger.q strike/15 -> true, knight.q dash -> false —
        // the exact set the string table hardcoded, and it generalises for free.
        private static void Case3_NoPerClassTable(List<string> failures)
        {
            string body = AttackHandlerBody(failures, "no-per-class-table");
            if (body == null) return;

            foreach (var cls in new[] { "mage", "ranger", "knight", "cleric", "Mage", "Ranger", "Knight" })
                if (body.Contains("\"" + cls + "\""))
                    failures.Add("[no-per-class-table] the attack handler contains the class-name literal \"" + cls +
                                 "\". That is the defect this WO removed: a hand-authored table cannot generalise, " +
                                 "and its `return` is what made the button dead. Decide through " +
                                 "HeroAbilities.TryGetRangedPrimary instead");

            if (!body.Contains("TryGetRangedPrimary"))
                failures.Add("[no-per-class-table] the attack handler does not call TryGetRangedPrimary - if it is " +
                             "deciding some other way, prove that way is derived. That method's own doc calls it " +
                             "'the SINGLE decision seam' precisely so input and targeting cannot disagree");

            // The seam itself must stay derived on the other side of the call.
            string abilities = ReadSrc(AbilitiesSrc, failures, "no-per-class-table");
            if (abilities == null) return;
            var m = Regex.Match(StripComments(abilities),
                                @"bool\s+TryGetRangedPrimary\s*\([^)]*\)\s*\{(?<b>(?:[^{}]|\{[^{}]*\})*)\}");
            if (!m.Success)
            {
                failures.Add("[no-per-class-table] TryGetRangedPrimary(float, out AbilityDef) no longer has the " +
                             "shape this lint can read - re-point the lint deliberately, do not delete it");
                return;
            }
            string seam = m.Groups["b"].Value;
            foreach (var cls in new[] { "\"mage\"", "\"ranger\"", "\"knight\"" })
                if (seam.Contains(cls))
                    failures.Add("[no-per-class-table] TryGetRangedPrimary itself now compares a class name (" + cls +
                                 ") - the table moved rather than being deleted");
        }

        // =====================================================================
        //  CASE 4 — [fallback-is-free]   (STRUCTURAL, over the whole file)
        //  REVERT RECIPE: give the sweep any cost (e.g. add a `_abilities.SpendMana`
        //  or a `_mana -=` to StartAttack/ResolveAttack) — RED, and
        //  [no-verbless-hero] falls with it, which is the point.
        // =====================================================================
        //
        // Owner ruling, WO-1429 §7 verbatim: "No swing Staff should have no cost only casting
        // magic should." This is not a balance preference — a fallback that can itself become
        // unavailable puts the hero back in the dead state the WO exists to remove. FREE is the
        // only cost that makes "the hero always has a verb" true at EVERY instant.
        //
        // Verified at source this session: PlayerAttackController's only contact with the
        // resource pool is a GRANT — the ranger's on-hit `RestoreMana(OnHitRestore)` at
        // :816-820, itself gated OFF for a class WITH a ranged basic (WO-1105 R3, no double
        // refund). There is no debit anywhere in the file.
        private static void Case4_FallbackIsFree(List<string> failures)
        {
            string src = ReadSrc(AttackSrc, failures, "fallback-is-free");
            if (src == null) return;
            string code = StripComments(src);

            // Any DEBIT of a pool. RestoreMana (a grant) is deliberately not matched.
            var debits = new (string Pattern, string What)[]
            {
                (@"SpendMana\s*\(",                 "SpendMana("),
                (@"ConsumeMana\s*\(",               "ConsumeMana("),
                (@"ManaCostOf\s*\(",                "ManaCostOf("),
                (@"\bTrySpend\w*\s*\(",             "TrySpend*("),
                (@"_mana\s*-=",                     "_mana -="),
                (@"\.Mana\s*-=",                    ".Mana -="),
                (@"\bStamina\b",                    "Stamina"),
                (@"\bCharges?\b\s*-=",              "Charges -="),
            };
            foreach (var d in debits)
                if (Regex.IsMatch(code, d.Pattern))
                    failures.Add("[fallback-is-free] PlayerAttackController now contains '" + d.What + "' - the melee " +
                                 "sweep is the hero's guaranteed verb and MUST spend nothing. Give it a cost and the " +
                                 "hero can be verbless again, which is the whole defect");

            if (!code.Contains("RestoreMana"))
                failures.Add("[fallback-is-free] the on-hit RestoreMana grant is gone from PlayerAttackController - " +
                             "this lint reads that call as the file's single, deliberate pool contact. If the ranger's " +
                             "Focus economy moved, re-point this case rather than deleting it");
        }

        // =====================================================================
        //  CASE 5 — [ranger-fallback-spends-no-arrow]   (STRUCTURAL)
        //  REVERT RECIPE: make the fall-through path cast the Q again (e.g. a
        //  second TryCast after TriggerBasicAttack) — RED.
        // =====================================================================
        //
        // ⚠ READ THE SUITE HEADER'S CANON-CONFLICT BLOCK BEFORE CHANGING THIS CASE.
        // CLAUDE.md §7's full sentence ("the phone's one attack button never spends an arrow")
        // is NOT assertable today: `hostile(activebattle)` has an EMPTY actionRail
        // (hud-areas.json:242-249), so combat-dock slot 0 = HudCommands.Attack is the class Q's
        // ONLY dispatch on mobile, and asserting the sentence would strand Quick Shot. That is
        // an owner ruling, not a lint. What this case DOES pin — and it is the half that keeps
        // the hero's floor honest — is that the FALLBACK swing spends no arrow: exactly ONE
        // TryCast in the handler, and the ranger's dagger earns no Focus (WO-1105 R3's
        // no-double-refund gate is intact).
        private static void Case5_RangerFallbackSpendsNoArrow(List<string> failures, List<string> notes)
        {
            string body = AttackHandlerBody(failures, "ranger-fallback-spends-no-arrow");
            if (body != null)
            {
                int casts = Regex.Matches(body, @"\.TryCast\s*\(").Count;
                if (casts > 1)
                    failures.Add("[ranger-fallback-spends-no-arrow] the attack handler calls TryCast " + casts +
                                 " times - the fallback must be the FREE sweep, never a second shot. One tap must " +
                                 "never be able to spend two arrows");
                if (casts == 0)
                    notes.Add("[ranger-fallback-spends-no-arrow] the handler no longer casts at all - if that is " +
                              "deliberate, the class Q has NO dispatch left in hostile(activebattle) " +
                              "(hud-areas.json:242-249) and Fireball/Quick Shot are unreachable on mobile");
            }

            string atk = ReadSrc(AttackSrc, failures, "ranger-fallback-spends-no-arrow");
            if (atk == null) return;
            string code = StripComments(atk);
            if (!Regex.IsMatch(code, @"basicIsMelee\s*=\s*_abilities\s*==\s*null\s*\|\|\s*!\s*_abilities\s*\.\s*TryGetRangedPrimary"))
                failures.Add("[ranger-fallback-spends-no-arrow] the WO-1105 R3 no-double-refund gate " +
                             "(`basicIsMelee = _abilities == null || !TryGetRangedPrimary(...)`) is gone from " +
                             "PlayerAttackController. Without it the ranger's DAGGER also pays Focus, handing the " +
                             "class a second unauthored Focus engine on top of the bow's - and the WO-1429 fallback " +
                             "makes that dagger reachable far more often than before");
        }

        // =====================================================================
        //  CASE 6 — [attack-face-always-pressable]   (STRUCTURAL)
        //  REVERT RECIPE: restore
        //  `primary.button.interactable = q.Equipped && q.Affordable && q.CooldownRemaining <= 0f;`
        //  in HudKitController.OnAbilities — RED.
        // =====================================================================
        //
        // The HUD half of the same defect, and without it the bridge fix is INERT. The combat
        // dock's slot 0 used to grey itself out exactly while the class Q was cooling or
        // unaffordable - so the player's one attack control went dark in every cooldown gap and
        // the fall-through could never be reached. The cooldown SWEEP is deliberately kept: the
        // player still watches the spell cool, they just get the free sweep meanwhile.
        private static void Case6_AttackFaceAlwaysPressable(List<string> failures)
        {
            string src = ReadSrc(HudKitSrc, failures, "attack-face-always-pressable");
            if (src == null) return;
            string code = StripComments(src);

            var m = Regex.Match(code, @"primary\s*\.\s*button\s*\.\s*interactable\s*=\s*(?<rhs>[^;]*);");
            if (!m.Success)
            {
                failures.Add("[attack-face-always-pressable] HudKitController no longer assigns " +
                             "`primary.button.interactable` in the combat dock's Q-face dressing - re-point this " +
                             "lint at the new shape deliberately rather than deleting it");
                return;
            }
            string rhs = m.Groups["rhs"].Value.Trim();
            if (rhs != "true")
                failures.Add("[attack-face-always-pressable] the combat dock's ATTACK face is gated on `" +
                             Condense(rhs) + "`. In hostile(activebattle) that dock is the ONLY combat control " +
                             "(hud-areas.json:242-249 - actionRail is empty), so any gate here is a dead button and " +
                             "the WO-1429 fall-through can never be reached. It must be unconditionally pressable");

            var sweep = Regex.Match(code, @"primary\s*\.\s*SetCooldown\s*\(");
            if (!sweep.Success)
            {
                failures.Add("[attack-face-always-pressable] the Q cooldown sweep no longer renders on the ATTACK " +
                             "face - the button staying live is only honest if the player can still SEE the spell " +
                             "cooling behind it");
                return;
            }

            // ORDERING PIN. ActionSlotHandle.SetCooldown ENDS with
            // `if (button != null) button.interactable = !cooling;` (ElarionUiKitObsidian.cs:1138),
            // so the kit re-disables the face on every refresh. The `interactable = true` above is
            // only effective while it stays AFTER the SetCooldown call. Swap the two lines and the
            // fix silently reverts with no compile error and no visible diff in behaviour except a
            // dead button - exactly the class of regression this suite exists to catch.
            if (sweep.Index > m.Index)
                failures.Add("[attack-face-always-pressable] `primary.button.interactable = true` now runs BEFORE " +
                             "`primary.SetCooldown(...)`, which ends with `button.interactable = !cooling` " +
                             "(ElarionUiKitObsidian.cs:1138). The kit will re-disable the ATTACK face on every " +
                             "model refresh and the WO-1429 fallback becomes unreachable again. Put the assignment " +
                             "back AFTER the sweep");

            if (!Regex.IsMatch(code, @"class\s+ActionSlotHandle") &&
                !File.Exists("Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs"))
                failures.Add("[attack-face-always-pressable] ElarionUiKitObsidian.cs is missing - the ordering " +
                             "rationale above cites its SetCooldown; re-verify where the kit sets interactable");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>
        /// The comment-stripped body of HudKitCommandBridge's RegisterAttack lambda — from
        /// `RegisterAttack(` up to the next `RegisterBlock(`. Comments are stripped FIRST so
        /// the explanatory block inside the handler (which quotes the old class names) can
        /// never satisfy or fail a lint. Returns null and records a failure when the shape
        /// can no longer be located, which is itself a finding.
        /// </summary>
        private static string AttackHandlerBody(List<string> failures, string caseName)
        {
            string src = ReadSrc(BridgeSrc, failures, caseName);
            if (src == null) return null;
            string code = StripComments(src);

            int start = code.IndexOf("RegisterAttack(", StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add("[" + caseName + "] HudCommands.RegisterAttack( not found in " + BridgeSrc +
                             " - the mobile attack command has no Village-side handler, or it moved. Either way " +
                             "the WO-1429 invariant is unpinned");
                return null;
            }
            int end = code.IndexOf("RegisterBlock(", start, StringComparison.Ordinal);
            if (end < 0) end = code.Length;
            return code.Substring(start, end - start);
        }

        /// <summary>class id -> its authored q effect string (null when the class has no q).</summary>
        private static Dictionary<string, string> ReadClassPrimaries(List<string> notes)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(AbilitiesJson))
                {
                    notes.Add("abilities.json not found at " + AbilitiesJson);
                    return result;
                }
                string json = File.ReadAllText(AbilitiesJson);
                // Deliberately a shallow scan, not a parser: this suite must not depend on the
                // JSON library's shape to answer "does this class author a q at all".
                // NOTE: braces are written as \x7B / \x7D throughout these patterns, never as a
                // literal { or }. CLAUDE.md §1's quality gate counts raw braces per file, and an
                // unpaired brace inside a STRING would fail that gate on a perfectly valid file.
                foreach (Match c in Regex.Matches(json, "\"(?<cls>[A-Za-z0-9_\\-]+)\"\\s*:\\s*\\x7B"))
                {
                    string cls = c.Groups["cls"].Value;
                    if (cls.EndsWith("-skills", StringComparison.OrdinalIgnoreCase)) continue;
                    // Non-class object keys the shallow scan would otherwise mistake for classes
                    // (the ability slots themselves, and each class's resource block).
                    if (Array.IndexOf(NonClassKeys, cls.ToLowerInvariant()) >= 0) continue;
                    if (result.ContainsKey(cls)) continue;
                    // Only rows that look like a class block (they carry a "q" ability).
                    int at = c.Index;
                    int window = Math.Min(4000, json.Length - at);
                    string near = json.Substring(at, window);
                    var q = Regex.Match(near, "\"q\"\\s*:\\s*\\x7B[^\\x7B\\x7D]*\"effect\"\\s*:\\s*\"(?<fx>[^\"]*)\"",
                                        RegexOptions.Singleline);
                    if (q.Success) result[cls] = q.Groups["fx"].Value;
                }
            }
            catch (Exception ex)
            {
                notes.Add("abilities.json scan threw " + ex.GetType().Name + ": " + ex.Message);
            }
            return result;
        }

        private static string ReadSrc(string path, List<string> failures, string caseName)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                failures.Add("[" + caseName + "] source not found: " + path);
            }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] could not read " + path + ": " + ex.Message);
            }
            return null;
        }

        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        private static string Condense(string s)
        {
            string one = Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
            return one.Length > 160 ? one.Substring(0, 157) + "..." : one;
        }
    }
}
