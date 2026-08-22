**Status:** READY TO IMPLEMENT — owner-requested 2026-08-22 (*"yes do the clamp oracle"*)

# WORK ORDER 1060 — The clamp oracle: make layout collisions FAIL THE GATE

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1060 -> 1061 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** Core / regression tooling
**Class:** ORACLE. Prevents a defect class rather than fixing one instance.
**Cause:** four panels in three days — WO-1051 (Daily Chest), WO-1056 (Armies/Loadouts),
WO-1058 §5 (Manage/Queues), and the equip drawer in the 2026-08-22 screenshots.

---

## 0. One-line truth

**PROD-008 established that no oracle can see orientation, because "looks wrong" is not computable.
Layout is different: the moment a layout is about to break is a discrete, deterministic, observable
event — `ClampMinTouch` growing a control.** We cannot assert that a panel looks good. We can assert
that **nothing had to be rescued**, and that catches the entire class.

---

## 1. Why this class keeps shipping

Every one of the four panels went out compile-green and regression-green. The pattern is identical
each time:

1. A control is authored as a fraction of a small sub-zone.
2. The resolved rect lands **below `MinTouchPx = 112`**.
3. `ClampMinTouch` force-grows it — by 2.5x, 3.9x, **4.5x** in the measured cases.
4. It inflates past its authored band into its neighbours.
5. Nothing anywhere notices. The owner finds it by eye, days later.

**`ClampMinTouch` is a safety net that has become a silent failure mode.** By the time it fires, the
layout it was meant to protect is already destroyed — and it never says so.

---

## 2. The oracle — two asserts, one marker

### Assert A — THE CLAMP MUST NEVER FIRE

Instrument `ElarionUiKit.ClampMinTouch` to record every growth into a static, test-readable list:

```
ClampGrowth { panelName, controlPath, authoredW, authoredH, grownW, grownH }
```

**Any recorded growth on a panel under test is a FAILURE**, reported with the numbers so the fix is
obvious from the log line alone:

```
[touch-oracle] FAIL ArmyMusterPanel/slot-chip-0: authored 197x25 -> grown 197x112 (4.5x on H).
               Author the band above MinTouchPx(112); do not rely on the clamp.
```

⛔ **Do NOT weaken `ClampMinTouch` itself.** It stays exactly as it is — it is the correct runtime
behaviour for a build that ships wrong. The oracle only makes its firing *visible at gate time*.

### Assert B — NO TWO INTERACTIVE RECTS MAY INTERSECT

Assert A cannot catch two correctly-sized controls authored on top of each other — which is exactly
**WO-1058's hazard**, where `Cancel` (0.885-0.98) sits inside where `Upgrade` (0.76-0.98) was, both
comfortably above the floor.

So, after layout, for every pair of interactive rects sharing a canvas: **compute the intersection.
Non-empty = FAIL.**

```
[touch-oracle] FAIL ManageScreenPanel: 'Upgrade'(0.76-0.98) intersects 'Cancel'(0.885-0.98)
               on x by 0.095 of row width.
```

⚠ **Include the shared Close.** Burying it is WO-1051's defect and it must be caught by the same
rule — the Close is an interactive rect like any other.

### The marker

Emit **`UI_TOUCH_OK <n>/<n> panels`** on a clean pass. Judge by the MARKER on a fresh log, never by
the exit code — this repo's runners exit 0 on refusals and FAILs (memory
`gates-report-success-without-proving-it`).

---

## 3. ⚠ The hard part: measuring AFTER the scaler, not during

`ElarionUiKit.cs:1057` records the trap in its own words: **`rect.height` returns RAW SCREEN PIXELS
until the CanvasScaler has applied** — that was F8-5's root cause.

So the oracle must sample **after a layout pass has completed on a canvas configured exactly as the
game's is** (`referenceResolution 1080x1920`, `MatchWidthOrHeight`). A measurement taken during build
will read plausible numbers and prove nothing.

**Test at the landscape aspect the build actually ships** (portrait autorotate is `0`). The whole
defect class exists *because* a portrait reference resolution on a landscape screen makes the vertical
axis far smaller than the fractions suggest.

⚠ **Run at more than one aspect** — at minimum the Seeker's and a 16:9. A band that clears 112 at one
aspect can fall under it at another, and that is a shipping bug the single-aspect version would miss.

---

## 4. Where the panel list comes from

**Do not hand-maintain a list** — a list nobody updates is how the fifth panel ships broken.

`Assets/Editor/UICaptureLaunch.cs` + the headless capture path already enumerate and open panels for
screenshots. **Reuse that enumeration.** A panel that can be captured can be measured, and any panel
added to the capture set is automatically covered by the oracle.

Register the suite with `DataRegression.RunAll` so it runs in the standard gate
(`REGRESSION_OK <n>/<n> suites`).

---

## 5. Rollout — the existing debt must not block the gate

Four panels are known-bad **today**. Turning the oracle on red would block every commit.

1. **Land the oracle reporting-only**, with a **baseline allow-list** of the four known offenders
   (WO-1051, 1056, 1058, and the equip drawer), each entry naming its WO.
2. **Each fix removes its own allow-list entry** in the same commit.
3. **When the list empties, the allow-list mechanism is DELETED** — not left empty. An empty
   suppression list is an invitation to add to it.

⛔ **The allow-list may only ever shrink.** Adding an entry requires an owner ruling, exactly like
re-tagging an impulse SKU `shelfCurated`. Otherwise the oracle becomes a place to record defects
rather than prevent them.

---

## 6. Acceptance

1. `UI_TOUCH_OK <n>/<n> panels` on a fresh log for a clean tree; **marker absent = failure**.
2. **Proven RED before green:** run it against `ArmyMusterPanel` as it stands today and capture the
   FAIL naming the 4.5x growth. An oracle never seen red is not evidence (PROD-008's rule).
3. Assert A catches sub-floor authoring; Assert B catches same-size overlap. **Prove each with its
   own case** — a deliberately shrunk control, and two deliberately overlapping ones.
4. Measurement happens post-scaler at **>= 2 aspect ratios**, landscape.
5. The panel set is **derived** from the capture enumeration, not hand-listed.
6. The baseline allow-list has exactly four entries, each naming its WO.
7. `COMPILE_GATE_OK`; brace-check every `.cs`.

---

## 7. What this does NOT do

- **It does not assert that a panel looks good.** No oracle can (PROD-008). It asserts that nothing
  had to be rescued and that nothing overlaps — a proxy, and the proxy that catches this class.
- **It does not replace screenshots.** `UI_CAPTURE_OK` and opening the PNGs still bind; the oracle
  catches geometry, the eye catches everything else.
- **It does not touch any panel's layout.** Those are WO-1051 / 1056 / 1058 / 1061.

## 8. Files

**Create:** `Assets/Editor/Regression/UiTouchClampRegression.cs` (or the project's suite convention).

**Edit:** `Assets/_Modules/Core/UI/ElarionUiKit.cs` — **record-only** instrumentation inside
`ClampMinTouch`; behaviour unchanged (§2 Assert A). Register the suite in `DataRegression`.

**Read:** `Assets/Editor/UICaptureLaunch.cs` (the enumeration to reuse) · `ElarionUiKit.cs:1057`
(the post-scaler trap) · WO-1051 §3.5, WO-1056 §1, WO-1058 §1 (the measured cases).
