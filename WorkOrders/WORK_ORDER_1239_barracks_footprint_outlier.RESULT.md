# WO-1239 RESULT — the band was the defect, not the barracks

**Status:** IMPLEMENTED — awaiting the lead's batch gate
**Date:** 2026-08-26
**Files changed:** `Assets/Editor/Regression/StructureCadenceRegression.cs` (ONLY)
**Catalog changed:** ⛔ **NO.** No `structures-catalog.json` edit in either copy, so
**`CatalogFallbackData.g.cs` does NOT need regenerating.**

---

## 1. The population, before and after (acceptance item 2)

Same suite, same 27 rows, same ids. `Builds/wo1211-reg.log` (GREEN, 08-25 21:15) vs
`Builds/gate-r3` (RED, 08-26 17:23).

| | BEFORE (green) | AFTER (red) |
|---|---|---|
| population measured | **27** | **27** |
| family median widest | **4.32 m** (`pet-house`) | **3.78 m** (`mine_crystal` / `healing_caravan`) |
| 2.0x band | **8.64 m** | **7.56 m** |
| `lumberyard` / `foundry` / `silo` widest | 5.83 m each | **2.91 m each** |
| `barracks` widest | **7.64 m** | **7.64 m** — unchanged |

WO-1224 Slice A (`3cd28c86c`) set `heightMul: 0.5` on the three shared `GenericContainer` rows.
`heightMul` feeds a UNIFORM fit scale, so it halved their footprint as well as their height. Three
of 27 members dropped below the middle, the median fell one slot, and the derived band came down
past a building that had not moved. **WO-1224 predicted this in its own text (line 131).**

The ticket's FBX hypothesis is falsified: `mine_crystal` and `healing_caravan` are both in the
GREEN 08-25 log at 3.78 m. They arrived in `3eb499b88`, *older* than the `heightMul` commit. The
population size never changed. WO-1239's hypothesis section has been corrected in place rather than
deleted.

## 2. Step 2(a) — is `barracks` upright AT FIT TIME? YES.

- The row authors `orientation.euler [90, 0, 0]`, `manual: true`, **`corrected: true`**, with the
  note: *"owner 2026-08-19 upright X=90 flagged fixed — applied PRE-fit via
  StructureFactory.OptsFor LocalRotation (GROK_BRIEF). Was [0,0,0]…"*. The uprightness of this
  specific row is an **owner visual verification**, made after the pre-fit rotation change that
  causes this class of bug — i.e. the exact check step 2(a) asks for has already been performed on
  the shipped pose, by the only seat that can perform it.
- **⚠ The oracle's own "measured height 4.00 m" is NOT evidence of anything, and it misled this
  ticket.** `VisualFactory.Fit` divides by whatever axis is up, so *every* `heightMul: 1.0` row
  measures **exactly** `YHeightVariable` = 4.00 m whether it was posed upright or flat. The height
  can never distinguish the two cases. I have fixed the message to say so.
- The number that *does* discriminate is the **fitted aspect, widest : height**:

  | row | aspect | posed |
  |---|---|---|
  | `collector_farm` (the known pancake defect) | **2.56 : 1** | flat at fit time |
  | `barracks` | **1.91 : 1** | upright |
  | `wall_stone` | 1.86 : 1 | upright |
  | `collector_forge` | 1.69 : 1 | upright |
  | `gate_stone` | 1.50 : 1 | upright |

  `barracks` sits at the top of the honest, verified-upright band and nowhere near the pancake
  signature. A long low bunkhouse at ~1.9 : 1 is architecturally ordinary — 3% wider than
  `wall_stone`, which the same gate calls green.

**Conclusion: the art is correctly posed and the art is honestly wide.**

## 3. Step 2(b) — `repo.maxFootprint` on `barracks`: DELIBERATELY NOT AUTHORED

No value was authored, and this is the substantive finding, not an omission.

`collector_farm` is the precedent and it is a precedent for a *flat* model. The cap is applied as a
**uniform scale-down** (`localScale *= cap / widest`, `StructureCadenceRegression.TryMeasure`,
mirroring `VisualFactory`). On the farm that is free: the farm's 5.60 m height was itself an
artefact of dividing by a 0.391 m axis, so capping it to 5.6 m across restored the size the owner
had already accepted. On a correctly-posed building it is not free at all —

> capping `barracks` at its `placement.footprint` of **5.0 m** (the honest, in-metres, farm-precedent
> value) would take it from 7.64 × **4.00** × … to 5.0 × **2.62** m — **shorter than every house in
> town.**

That is the "shrunk farm" objection the owner already rejected in `31b41d19`, arriving through a
different key. Any smaller cap is worse; any cap large enough not to deform it (≥ 7.6 m) is pure
threshold gaming, which the ticket forbids by name. **There is no honest `maxFootprint` for this
row**, and the fact that both per-row dials are unusable is itself the evidence that the problem is
not in the row.

## 4. THE JUDGEMENT CALL — the BAND was wrong (acceptance item 4)

**Verdict: `barracks` is not wrong. The band became wrong when the population shifted.** Reasoning,
in the order I'd defend it:

1. **The trigger runs backwards.** The gate went red because three *other* buildings got **smaller**
   — the town getting *better*, and the direct fix to an owner felt-report. A threshold that reds an
   untouched building when an unrelated building is corrected is not measuring what it names.
2. **Nothing about `barracks` is out of family.** It is 3% wider than `wall_stone` (green), its
   aspect is inside the verified-upright range, and it has read 7.64 m in every green log for a
   week.
3. **A median over the measured population is structurally unstable, and gets *more* unstable as the
   family gets more diverse.** This population is not one family — it contains decoration (0.78 m),
   siege engines, walls and gates, and buildings. The median lands on whichever sub-family happens
   to be numerous that week; here it slid from `pet-house` to a crystal-mine/caravan pair. **Every
   future art or `heightMul` change silently re-thresholds every other row, in both directions.**
   That also means the gate can go *quietly green* over a real defect if enough rows grow — the
   failure mode nobody would have noticed.
4. **Both per-row dials are unusable here** (§3 above), which is the tell: when the only remedies
   available all deform a correct building, the defect is upstream of the row.

### The proposal, implemented

Compare against something that cannot be moved by who else is in the room:

```
band = StructureFactory.YHeightVariable * CadenceWidthRatio = 4.0 m * 2.6 = 10.40 m
```

- **`YHeightVariable` preserves the original design intent.** The reason a family-relative band was
  chosen in the first place was *"it holds if the owner re-scales the whole town"* — and
  `YHeightVariable` **is** that one number (*"change THIS ONE number and the entire town re-scales
  together"*, `StructureFactory`). The band still tracks a whole-town re-scale; it just can no
  longer be dragged by one unrelated row.
- **Deliberately the FLAT base, not the row's own fit height** (`YHeightVariable * heightMul`). A
  row-relative ceiling would have given `collector_farm` (heightMul 1.4) a 5.6 × 2.6 = **14.56 m**
  allowance, and the measured **14.34 m** defect this whole suite exists for would have walked
  straight through it. That option was tested against the defect and rejected.
- **2.6 is justified in metres, by the same bracketing the old 2.0 used**, re-expressed against the
  base: widest honest structure in the shipped town = `barracks` **7.64 m = 1.91x base**
  (`wall_stone` 7.42 = 1.86x); the measured defect = **14.34 m = 3.58x base**. 2.6 is the
  **geometric midpoint** — `sqrt(1.91 × 3.58) = 2.615` — i.e. equal multiplicative margin either
  side: **1.36x headroom** over the widest honest row, **1.38x of bite** before the known defect.
  Ceiling **10.40 m**. This is not "2.0 loosened to make barracks pass"; it is a different reference
  with its own bracket, and against it `barracks` sits at 73% of the band, not 101%.
- **The C0 self-test now proves it in both directions with the real extremes.** The synthetic clean
  family gained `wall_stone` 7.42 and `barracks` 7.64 — without them its widest row was 5.83 m, so
  the self-test *structurally could not* have caught a band that reds a correct building, which is
  precisely what happened. The known-bad 14.34 m pancake is still caught, still named.

## 5. Should the oracle report its population size? YES — and it now does both things

**Answer: yes, and reporting it is the weaker half of the fix.** A threshold that depends on who
else is in the room should (a) say who was in the room, and (b) preferably not depend on it.

- The `STRUCTURE_CADENCE_OK` line already carried the count; it now says *why* the count is stated
  and that the band no longer depends on it.
- The detail line changed from `family median … band = 2.0x median` to
  `band = 10.40 m (2.6x the 4.0 m base fit height, population-independent). POPULATION (reported,
  not used as a threshold): 27 measured base visual(s), median widest-horizontal-extent 3.78 m.`
  **The median is still computed and printed — as observability, never as a threshold** — so a
  reader can still see a family shift, but a family shift can no longer move the line under anyone.
- The failure message now prints the **fitted aspect** and states in-line that the height alone is
  not diagnostic, so the next reader is not sent down the path this ticket was.

## 6. Expected effect on the gate

`barracks` 7.64 m and `wall_stone` 7.42 m are both far under the new 10.40 m band; the widest row in
the catalog is 7.64 m. C0/C1/C3/C4/C5 are untouched in substance (C0's clean family gained two rows
and both pass; C5's `heightMul 0.5` assert on the three containers is untouched, as required).
Expect `STRUCTURE_CADENCE_OK` and `REGRESSION_OK 292/292`.

## 7. Constraint compliance

- ✅ No `heightMul` lowered anywhere. WO-1224's `heightMul: 0.5` on the three containers untouched
  (and C5 still pins it).
- ✅ No `maxFootprint` authored — reasoned above, not omitted.
- ✅ The 2.0x band was not "loosened": the *reference* was replaced, with the population data, and
  the new ratio is independently bracketed. Constraint explicitly permitted this on proof; §4 is the
  proof.
- ✅ `VisualFactory.Fit`'s uniform-scale behaviour untouched.
- ✅ No catalog edit → no `CatalogFallbackData.g.cs` regeneration needed.
- ✅ No git add/commit/push, no Unity gate run.

## 8. File quality gate

`Assets/Editor/Regression/StructureCadenceRegression.cs` — braces **80 open / 80 close (balanced)**,
**0 NUL bytes**, LF line endings matching HEAD. It is the only `.cs` touched.
