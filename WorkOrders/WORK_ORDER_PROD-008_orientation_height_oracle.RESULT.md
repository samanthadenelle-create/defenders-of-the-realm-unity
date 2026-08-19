# RESULT — PROD-008 — the orientation / height-fidelity oracle

**Verdict:** **PARTIALLY LANDED.** The oracle exists, measures correctly, and has already found a real
row. **It is NOT a gate:** nothing runs it, it is currently RED, and it has been proven in one direction
only.
**Commit:** `fc9b1eb69` — its own subject says so: *"landed UNREGISTERED"*, 2026-08-18 21:44.
**Written:** 2026-08-19 by a read-only verification pass (HEAD `399bfb900`). No Unity run for this file.

---

## 1. What was wrong

No oracle in this project can see ORIENTATION. Every orientation defect it has shipped — the ArcaneSpire
double-rotation, WO-928's tower, PROD-007's five lying-down buildings — went out **compile-green and
regression-green**; `f995c4706` conceded in its own message that *"sits correctly in the town is a felt
claim"*. The owner found ten double-corrected models **by eye, on a LIVE store build.**

## 2. What shipped

`Assets/Editor/Regression/StructureOrientationOracle.cs` (627 lines, `fc9b1eb69`). Markers
`STRUCTURE_ORIENTATION_OK` / `STRUCTURE_ORIENTATION_FAIL` (`:606`, `:613`). Three asserts, and the primary
one is threshold-free:

- **A1 channel collision** — a mesh with the axis conversion baked in *plus* a catalog orientation that
  still tips >1°. Data-only, every row, every tier.
- **A2 height fidelity (PRIMARY)** — measured world `bounds.size.y` vs `StructureFactory.OptsFor(entry).FitHeight`,
  read from the **real production helper** rather than re-derived. Threshold-free because `VisualFactory.Skin`
  fits to HEIGHT and `StructureFactory.Create` applies `entry.orientation` **after** Skin returns and never
  re-fits — so for any row whose correction does not tip the vertical axis the final height is *exactly*
  `YHeightVariable × heightMul`. A model lying down at fit time gets its DEPTH fitted, and the number says
  by how much.
- **A3 tower aspect ≥ 1.2**, scoped **by catalog data** (`type == Tower && heightMul >= 1.2`), never a name
  list. §3's constraint is honoured: the naive global band is deliberately absent, because
  `House_Medieval_Medium` reads 0.72 upright and a gate that reds correct buildings gets itself disabled.
- **§5.4 satisfied:** the `RealmStore` coverage gap is stated in the header and printed in the run output.

## 3. THE PROVING EVIDENCE — it ran, and it found something

`Builds/struct-orient.log` (on disk, 64,550 bytes, **2026-08-18 21:43**), line 571 onward:

```
STRUCTURE_ORIENTATION_FAIL: 2 issue(s):
  - 'tower_ballista'    NOT STANDING: upright aspect 0.70 (height 4.80 m / max(width 6.85, depth 3.76))
  - 'tower_ballista L2' NOT STANDING: upright aspect 0.94 (height 4.80 m / max(width 5.11, depth 2.75))
subjects=34 (base visuals + authored upgrade rungs), measured=34
A1 channel-collision checked on 34 model(s)
A2 height fidelity asserted on 26 model(s) at +/-0.05 m
A3 tower aspect asserted on 9 tower-class model(s) at >= 1.2
A2 NOT ASSERTED on 8 base visual(s) whose catalog orientation tips the vertical axis ...
NOT COVERED: RealmStore — it is not a catalog row (28 entries, no store/realm id) ...
```

Two things worth keeping:

- **Coverage is printed, so the OK line can never read as full coverage** — 34/34/26/9 with the 8 excluded
  rows named individually.
- **It refuted its author's own prediction.** The predicted red (`Tower_Wooden_Watchtower_L3`) did **not**
  fire; the case the author flagged as uncertain and refused to tune away did. The failure text names both
  possible causes — mis-oriented model, or mis-classed row — instead of assuming one.

## 4. WHAT IS MISSING — the three things that make this "partial"

1. **⛔ IT IS NOT REGISTERED, SO IT NEVER RUNS.** `StructureOrientationOracle.cs:8` carries
   `regression-registry: standalone`, the explicit opt-out token honoured at
   `Assets/Editor/Regression/RegressionMarkerRegression.cs:123` and `:291`. Grepping
   `DataRegression.cs` for `Orientation` returns **nothing**. Acceptance §5.1 is therefore **unmet**. The
   file's own header says the token is TEMPORARY and must be deleted in the same commit that registers the
   suite — *"an oracle that stays standalone is an oracle that never runs, which is the failure this file
   was written about."*
2. **It is RED on the current tree, and the red needs an OWNER RULING, not a threshold edit.**
   `tower_ballista` is typed `Tower` at `Assets/Resources/Data/Canonical/structures-catalog.json:87` on the
   1.2 tower cadence anchor while its art is a **wide siege machine**. The fix is a reclassification into
   the 0.75 siege group (like `tower_catapult`) — an owner call. **Registering it before that ruling would
   red the whole regression run**, which is why the commit landed it unregistered rather than widening the
   floor to make itself green. That was the right call; it is also why the ticket cannot close.
3. **Proven in ONE direction only.** Acceptance §5.2 requires running it against the pre-PROD-007 tree and
   seeing it FAIL on `forge`/`workshop`/`jeweler`/`barracks`/`tower_ballista`, then PASS after. Only the
   "after" run exists (§3 above). **A gate that has not been shown to fail the known-bad state is not yet
   proven to be a gate** — that is the ticket's own wording.

## 5. WHAT IS NOT PROVEN

- **A2 does not cover 8 of 34 base visuals** (pet-house, market, arcane-tower, collector_farm,
  collector_lumbermill, lumberyard, foundry, silo) — their catalog orientation tips the vertical axis, so
  the fit provably measures a different axis and the catalog declares no expected height. They have **A1
  coverage only**; they are *not* known-good.
- **`RealmStore` is invisible to this oracle** and its FBX **is** axis-baked, i.e. it sits in exactly the
  double-correcting state with no oracle over it.
- **The `Tower_Wooden_Watchtower_L3` non-red is an open question**, deliberately not explained away: either
  the theory is wrong or L3 falls outside A2/A3's scope. Worth one read before anyone acts on the L3 ticket.
- **The oracle measures geometry, not appearance.** It can see a building lying down; it cannot see one
  facing backwards, and it says nothing about materials, scale-vs-neighbours, or placement.

**What would settle the remaining work:** an owner ruling on the ballista row → reclassify → delete the
`standalone` token and add the one registration line in `DataRegression.RunAll` → run once against a
reconstructed pre-PROD-007 catalog to capture the known-bad FAIL.
