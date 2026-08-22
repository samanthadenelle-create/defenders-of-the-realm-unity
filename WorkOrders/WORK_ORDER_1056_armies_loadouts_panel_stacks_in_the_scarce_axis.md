**Status:** READY TO IMPLEMENT

# WORK ORDER 1056 — Armies / Loadouts: seven controls stacked into a well that holds two

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1056 -> 1057 in the SAME edit)
**Assigned:** CLI implements. UI authored the layout; UI writes no `.cs` (CLAUDE.md §2).
**Lane:** UI presentation (CLAUDE.md §9 — isolated)
**Class:** DEFECT (layout collision) — **the same root cause as WO-1051, at three times the scale.**
**Screen:** `Assets/_Modules/Village/Troops/ArmyMusterPanel.cs` — `Build()`, lines 78-112 and 300-390.
**Evidence:** owner screenshot 2026-08-22 — three layers of buttons stacked on top of one another in
the right-hand well, the CTA covering the body text, and the wallet row crossing the panel split.
**Feature:** WO-934 (the army loadout bank, save v38 — `ArmyStorage.loadouts` + `activeLoadout`).

---

## 0. One-line truth

**This panel stacks vertically on a screen that is short and wide.** Every collision in the
screenshot is one symptom of that single decision: the right-hand well is ~317 reference units tall,
`MinTouchPx` is 112, and the design asks it to hold **seven** stacked interactive rows. It can hold
**two**.

⚠ **Note what is NOT wrong here.** Unlike WO-1051, this panel does the parenting *correctly* — it
resolves `layout.bodyLeft` / `layout.bodyRight` / `layout.footer` with proper fallbacks
(`:87-99`). The zone discipline is right. **The arithmetic against the touch floor is what is
missing**, and no amount of correct parenting saves a band authored at a fifth of the floor.

---

## 1. THE ARITHMETIC — why every one of those buttons is on top of another one

### 1.1 The vertical budget

| Quantity | Value | Source |
|---|---|---|
| Canvas reference | `1080 x 1920`, `MatchWidthOrHeight` | `ElarionUiKit.cs:107` |
| Post-scale canvas height, landscape | **~486 ref units** (a 2400x1080 device: `1080/2400 x 1080`) | derived |
| Panel rect | `(0.06, 0.05)-(0.94, 0.95)` -> **~437 units tall**, ~950 wide | `ArmyMusterPanel.cs:83` |
| `FrameCrafting.bodyRight` | `(0.490, 0.150, 0.955, 0.875)` -> **~317 units tall**, ~442 wide | `ElarionUiKit.cs:396` |
| Touch floor | **112** | `ElarionUiKit.MinTouchPx` |

**A 317-unit well divided by a 112-unit floor is 2.8. The right well holds TWO full-height
interactive rows.** It is currently given seven.

### 1.2 What each authored band actually resolves to

| Control | Authored band | Height in units | After `ClampMinTouch` | Growth |
|---|---|---:|---:|---:|
| 4x loadout slot chips (`:376-382`) | `y 0.80-0.88` = 0.08 | **~25** | 112 | **4.5x** |
| Name button (`:386`) | `y 0.70-0.78` = 0.08 | **~25** | 112 | **4.5x** |
| Save slot (`:388`) | `y 0.70-0.78` = 0.08 | **~25** | 112 | **4.5x** |
| Muster CTA (`:109`) | `y 0.02-0.11` = 0.09 | **~29** | 112 | **3.9x** |
| Steppers -/+ (`:306`, `:314`) | `x 0.105` of bodyLeft | **~45** wide | 112 | **2.5x** |

**The two button rows are authored 0.10 apart — about 32 units.** Each is force-grown to 112. They
therefore overlap by `112 - 32 = 80` units, **roughly 71% of their own height.** That is precisely
the three-layer stack in the screenshot: the slot chips, the second chip row and the Name/Save row
all occupying the same band.

**Nothing here is a mystery and nothing needs a repro.** `ClampMinTouch` is doing exactly what it
promises; the bands were never sized against it.

### 1.3 The rest of the screenshot, same cause

- **Muster CTA covering "Train queue: 0 of 5 used"** — it grows 29 -> 112 upward into the body text.
- **Wallet row crossing the panel split** — the third currency chip runs under the parchment well;
  the row is built into `footer`, whose band is `(0.060, 0.085, 0.940, 0.145)` = **~26 units tall**,
  also far below the floor, so it inflates too.
- **`Close` overlapping the frame's bottom ornament** — same family as WO-1051 §1.
- **"STAGED: Raid Push (slot 1)" hidden behind "Name: Raid Push"** — an inflated button over a label.

---

## 2. ⛔ THE FIX IS NOT NEW NUMBERS — the content does not fit the axis

Seven stacked rows at 112 need **784 units**. The well has **317**. Re-authoring the fractions cannot
create 467 units of height that do not exist, and the whole *panel* is only 437 tall.

**The screen is landscape: it is short and wide. Width is the abundant axis and height is the scarce
one — and this design spends the scarce one.** Fix the axis, not the decimals.

### 2.1 Budget: at most THREE interactive bands in the entire panel

`437 / 112 = 3.9`, and header + frame chrome take part of that. **Three is the ceiling.** Design to
it:

| Band | Content | Geometry |
|---|---|---|
| **1 — Loadout selector** | The 4 chips (`Raid` · `Hold` · `Siege` · `Clear`) as ONE horizontal row, full panel width, directly under the header | 4 chips across ~950 units = **~237 each** — over twice the floor, no inflation |
| **2 — Roster rows** | `bodyLeft`, one row per troop type, each **112 tall**, in the existing `MakeScrollZone` | 317-unit well shows ~2.8 rows and **scrolls** — correct, since the roster grows |
| **3 — Action strip** | `Name` · `Save slot` · `Muster` as one horizontal row, full width, at the base | 3 controls across ~950 = **~300 each** |

**`bodyRight` becomes READ-ONLY.** Composition, cost, time, queue — text only, no interactive
element. That is what a parchment detail well is for, and it is the change that makes the budget
work.

### 2.2 Steppers: give them the width, not the height

`-` / `+` at `x 0.105` of a 429-unit `bodyLeft` = ~45 units, inflated to 112, and the two are only
~92 apart so they collide. Author each at **>= 0.26 of bodyLeft width** (~112) with the count between
them, and let the row own a full 112 of height. One roster row, three cells, all above the floor.

### 2.3 The general rule to take away

**Author every interactive rect above 112 on its short side, then verify the RESOLVED size.**
`ClampMinTouch` is a safety net, not a layout engine — when it fires, it has already destroyed the
layout. WO-1051 §3.5 says the same thing about the Daily Chest; this is the second screen in two days
with the same cause, so treat it as a class, not an incident.

⚠ **Measure after layout, not during.** `ElarionUiKit.cs:1057` warns that `rect.height` returns raw
screen pixels until the CanvasScaler has applied — that was F8-5's root cause.

---

## 3. ⛔ What NOT to touch

- **The zone resolution at `:87-99`.** It is correct — `bodyLeft`/`bodyRight`/`footer` with proper
  fallbacks. Do not "simplify" it to `chrome.content`; that is the WO-1051 defect.
- **`FrameCrafting`'s zone rects** (`ElarionUiKit.cs:388-399`). Shared art-measured values; changing
  them moves every crafting-framed panel. **If the footer band genuinely cannot hold a 112 control,
  log it as a separate kit ticket** — same finding as WO-1051's parked default-zone issue.
- **`MinTouchPx = 112`.** It is the owner's mobile-touch standard. Layouts bend to it.
- **The loadout MODEL.** `ArmyStorage.loadouts`, `activeLoadout`, save v38, `ArmyMusterService`,
  costs and train times. **Presentation only.**
- **The shared Close.** Move things off it; never substitute an X (`ElarionUiKit.cs:858`).

---

## 4. Acceptance

1. **No two interactive rects intersect.** Assert it as arithmetic on the RESOLVED rects, not by eye.
2. **`ClampMinTouch` is a no-op on this panel** — it grows nothing. This is the real test; if it
   fires, the layout was authored wrong.
3. All four loadout chips are readable and individually tappable; `STAGED:` is not covered.
4. The Muster CTA does not overlap the detail text; `Train queue:` is fully visible.
5. The wallet row stays inside its own band and does not cross the panel split.
6. `Close` clears the frame's bottom ornament.
7. Roster scrolls when troop types exceed the visible rows.
8. **Greyscale pass** — selected loadout slot is distinguishable without hue.
9. `COMPILE_GATE_OK`; brace-check every `.cs`; screenshots opened, not just taken — including a
   **before/after pair** against the owner's 2026-08-22 shot.

---

## 5. Files

**Edit:** `Assets/_Modules/Village/Troops/ArmyMusterPanel.cs`

**Read, do not edit:** `Assets/_Modules/Core/UI/ElarionUiKit.cs` (`MinTouchPx`, `ClampMinTouch`,
`ZonesFor` -> `FrameCrafting` at `:382-399`, the scaler note at `:1057`) ·
`WorkOrders/WORK_ORDER_1051_daily_chest_panel_layout.md` (same class, smaller scale)

**Separate ticket, named not folded:** whether `FrameCrafting.footer` (~26 units) can hold a
MinTouch control at all — a kit-level question affecting every panel on that frame.
