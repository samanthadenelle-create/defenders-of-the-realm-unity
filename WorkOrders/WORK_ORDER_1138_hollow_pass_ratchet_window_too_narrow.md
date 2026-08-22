**Status:** READY TO IMPLEMENT

# WORK ORDER 1138 — The hollow-pass ratchet only sees 4 lines, so 5 of 6 hollow passes escaped it

**Minted:** 2026-08-21 (CLI, banner bumped 1137 -> 1139 in the SAME edit alongside WO-1137)
**Lane:** Regression harness. **Class:** THE GATE ITSELF — a detector with a blind spot.
**Found by:** the cooldown/hollow-oracle fix lane during the 2026-08-21 gate sweep.

## THE FINDING

A "hollow pass" is a regression case that returns GREEN while asserting nothing — typically
`if (dependencyMissing) { notes.Add("SKIPPED ..."); return; }` where the notes feed the SUCCESS
reason string. **The caller's only channel is the bool, so a skip IS a pass.**

There is already a ratchet that detects this (`FindHollowPassLines`, RULE 4). On 2026-08-21 it
caught **one** such site in `CosmeticApplyRegression.cs`. Manual review of that same file then found
**five more**. All six were real. The five escaped for one reason:

> ### The ratchet inspects a ~4-LINE WINDOW around the `return`, so a hollow pass whose guarding
> ### `if` sits further away is INVISIBLE to it.

That is not a tuning miss — it means the detector's coverage is a function of **code formatting**,
which is the least reliable signal available.

## WHY THIS IS THE MOST EXPENSIVE DEFECT CLASS IN THIS REPO

Canon already records the family (memory `gates-report-success-without-proving-it`; §8's
marker-not-exit-code law; §16's silently-missing bundles). A gate that reports success without
proving it does not merely fail to catch a bug — it **actively asserts the bug is absent**, which
is worse than having no gate, because work proceeds on the strength of it.

On 2026-08-21 alone, hollow passes were found in **two separate suites**:
- `CosmeticApplyRegression` — 6 sites (1 caught, 5 missed)
- `RaidCooldownRegression` — case 5 silently vacuous against a null list (missed entirely; found
  only because case 6 failed loudly for an unrelated reason and a human read the fixture)

## SCOPE

1. **Widen the detection beyond a line window.** Match the CONTROL-FLOW relationship (a `return
   true` / bare `return` reachable from a dependency-missing guard) rather than textual proximity.
   ⚠ Even a crude scope-aware walk beats a 4-line window; this does not need a full parser.
2. **Re-run the widened ratchet across EVERY registered suite** and triage what it surfaces. Expect
   more — two suites were examined by hand on one day and both were dirty.
3. **Adopt the three-way rule the cosmetics fix established** (it is the right taxonomy, apply it
   repo-wide):
   - **fixture-absent -> FAIL**, naming the missing path
   - **harness-capability-absent -> a VISIBLE stand-down** (`RegressionOutcome.PartialSkip`) that can
     never be read as a pass
   - **content/art-absent -> assert THROUGH it** (the proven fallback path is the assertion)
4. ⛔ **Do not add a broad opt-out.** Any per-site exemption must name the site and the reason, or
   this ticket recreates the problem it closes.

## ACCEPTANCE

- [ ] The ratchet catches all six known `CosmeticApplyRegression` sites when they are reintroduced,
      **including the five the 4-line window missed** — this is the regression test for the ratchet
- [ ] Detection no longer depends on formatting/proximity
- [ ] The full suite sweep is run and its findings triaged (fixed or ticketed, never waived)
- [ ] `RaidCooldownRegression` case 5's vacuous-against-null shape is covered

## NOT IN SCOPE

The six sites already fixed on 2026-08-21, and the raid-cooldown fixture teardown already repaired.
