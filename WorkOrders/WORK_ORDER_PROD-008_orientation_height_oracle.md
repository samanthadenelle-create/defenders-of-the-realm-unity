# PROD-008 — No oracle can see ORIENTATION: author the height-fidelity / aspect gate

**Status:** READY — PARTIAL. The oracle exists and measures correctly 2026-08-18 (`fc9b1eb69`) (`Assets/Editor/Regression/StructureOrientationOracle.cs`, markers `STRUCTURE_ORIENTATION_OK/_FAIL`). ⭐ **TWO OF THE THREE REMAINING ITEMS HAVE SINCE LANDED — verified at source 2026-08-24:** (1) §5.1 registration is **DONE** — it is invoked from `DataRegression.RunAll` (`Assets/Editor/Regression/DataRegression.cs:1154`) and the `regression-registry: standalone` opt-out is gone from its header; (2) the **owner ruling landed 2026-08-23** (`f1d2c5d9b`) — the ballista was ruled MIS-CLASSED, not mis-oriented (`heightMul` 1.2 → 0.75, joining catapult/siege tower; ⛔ the 1.2 tower floor was deliberately NOT widened and the art was NOT re-oriented), and both ballista entries cleared. **WHAT REMAINS:** the oracle was still red on other rows at that commit ("7 issues down to 5"), so a fresh lead-run is owed to say what is left; and (3) §5.2's known-bad direction was never run, so the gate is still proven in ONE direction only.

*(Board note 2026-08-24: bucket corrected. The line led with `PARTIALLY IMPLEMENTED`, a non-canonical verdict that fell through the parser's substring pass into **Done** — a row saying "IT IS NOT A GATE YET" was rendering as finished. Items 1 and 2 were re-checked at source and had landed; the remainder is restated without being re-verified by this pass.)*
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** HIGH — this is the gate whose absence let PROD-007 (and WO-928 before it) reach a LIVE store build.
**Silo:** Regression / catalog oracles. **Lane:** `Assets/Editor/Regression`. No scenes, no gameplay code.
**Provenance:** the PROD-007 investigation, 2026-08-18.

---

## 1. Why this ticket exists

Every orientation defect this project has shipped — the ArcaneSpire double-rotation, WO-928's tower,
PROD-007's five lying-down buildings — shipped **compile-green and regression-green**, because no
automated check can see which way a building is facing. Commit `f995c4706` said so about itself:
*"sits correctly in the town is a felt claim"*.

That is the actual defect: **the only oracle for orientation is the owner's eyes**, so every
orientation change costs a felt-test round trip and any regression between round trips is invisible.

## 2. The instrument already exists in this repo — do not invent one

`Assets/Editor/WoodenWatchtowerBuilder.cs:270-278`:

```csharp
// "Is this model standing up?" = bounds height / max(width, depth).
// MEASURED, not guessed: these three towers read 1.70-1.92 upright and
// ~0.52-0.59 lying down, so 1.2 separates the two states with a wide margin
// and cannot be satisfied by a tower on its side.
private const float UprightAspectMin = 1.2f;
```

It is already used to **refuse a bake onto an already-standing model**. The work here is to promote
that idea into a catalog-wide gate — not to design a new metric.

## 3. ⛔ THE DESIGN CONSTRAINT THAT MAKES THE OBVIOUS VERSION WRONG

**A single global aspect threshold FALSE-POSITIVES on wide buildings.** `House_Medieval_Medium`
measures **4.0 / 5.562 = 0.72 upright** — below the 1.2 band while perfectly correct. A global aspect
gate would fail the honest buildings and teach everyone to ignore it, which is worse than no gate.

So:

- **PRIMARY ASSERT = HEIGHT FIDELITY.** Measure the instantiated visual's world `bounds.size.y` and
  compare it to `YHeightVariable * heightMul` for that row. This is **threshold-free** — the catalog
  already declares the answer, and a lying-down model fails it hard because the fit measured the
  short axis (the `tower_ground_archer` note in `structures-catalog.json` walks through exactly this:
  a mis-measured fit produced 9.25x instead of 4.80x).
- **SECONDARY ASSERT = the 1.2 aspect band, SCOPED TO TOWER-CLASS ROWS ONLY**, where tall-and-narrow
  is a property of the class rather than a guess.

## 4. Scope note — say it, do not special-case it

**`RealmStore` is NOT a catalog row** (verified: `structures-catalog.json` holds 28 entries and none
matches `store`/`realm`). A catalog-driven oracle therefore does **not** cover it. Record that gap in
the oracle's own header rather than bolting on a special case — a special case in an oracle is a lie
about its coverage.

## 5. Acceptance criteria

1. A new EditMode/batchmode oracle with a distinct marker (e.g. `ORIENTATION_OK` /
   `ORIENTATION_FAIL`), wired into `DataRegression.RunAll`, tagged.
2. Run against HEAD **before** the PROD-007 catalog fix: it FAILS on
   `forge`/`workshop`/`jeweler`/`barracks`/`tower_ballista`. Run after: it PASSES. **A gate that does
   not fail the known-bad state is not a gate** — prove both directions.
3. It PASSES on the eight rows that legitimately keep `[-90,0,0]` (PROD-007 §3) and on
   `House_Medieval_Medium`-class wide buildings.
4. Its header states the `RealmStore` coverage gap (§4).

## 6. What NOT to do

- Do not apply the 1.2 aspect band globally (§3).
- Do not "fix" any catalog row from inside the oracle. It measures; it never authors.
- Do not delete or relax `WoodenWatchtowerBuilder`'s existing refusal — this gate is in addition to it.
