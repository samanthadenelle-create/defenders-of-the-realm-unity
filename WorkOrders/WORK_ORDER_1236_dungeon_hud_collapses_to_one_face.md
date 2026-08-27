# WORK ORDER 1236 - In a dungeon the action bar collapses to ONE floating face, and the flag overlay eats the minimap

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 304/304 suites` (Builds/w5-c, Builds/w5-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** HUD / dungeon presentation
**Origin:** Owner FLAG on device, Seeker build `2026.08.26.342290`, scene `dg_ember_deep`, 2026-08-26.
**First capture ever delivered by the WO-1227 device bridge** (seq 3608) - the owner pressed FLAG and
it reached the seat without her asking.

**EVIDENCE:** `logs/f8-inbox/device/SM02G4061955851/flag_20260826-171239_01.png`, 2670x1200.
State: `Ashwood - Tier 4 - 14 threats`, Thrain Lv 4 Mage, Echoes 1/6, gold 1500.

---

## Defect 1 - the action bar is GONE except one face, and it floats over the hero

In town the calm bar shows five faces (Build / Bag / Raids / Quests / Manage - confirmed in the same
session's `break_02_possible_softlock.png`). **In `dg_ember_deep` only `Bag` renders**, and it is
drawn at roughly screen-centre-bottom **ON TOP OF THE HERO**, not in a bar.

WARNING - establish FIRST whether this is a MASK or a LAYOUT failure. They look identical and have
opposite fixes:
- **Mask:** the dungeon context legitimately computes a one-face mask, and the View's slot geometry
  centres a single face (so the "floating" is the bar working, with one item).
- **Layout:** the bar built for N faces and the rest failed to render.

`HudActionBarModel.ComputeMask` is the seam. DO NOT "fix" a correct mask - CLAUDE.md section 7
records that exact mistake costing a felt-test report and an RCA on 2026-08-26, when a five-face bar
in open town was reported as a missing sixth face. **State which it is, with the mask value, before
changing anything.**

If the mask IS correct, the defect is narrower and real: a dungeon offers the player almost nothing -
no Quests, no Manage, no advertised way out. Whether a dungeon bar should carry more faces is then an
OWNER RULING, not a bug fix. Raise it.

WARNING - related but NOT the same ticket: WO-967 (dungeon action bar defaults to knight) is about
the WRONG CLASS's faces. This is about there being almost NO faces. Check whether they share a root
before treating them separately; say which in the RESULT.

## Defect 2 - the FLAG confirmation covers the minimap

A white `FLAGGED` block is drawn over the minimap, and a second `FLAGGED` label overlaps the hero
name plate (`Thrain Lv 4 - Mana`). The acknowledgement is welcome - the owner needs to know the press
registered (WO-1226) - but it must not occupy the minimap, which is navigation, in a dark dungeon
where navigation is the whole difficulty.

**Required:** the acknowledgement gets its own band that overlaps nothing, and it must time out.
WO-1219 has ALREADY reserved a toast zone (centred above the action bar, overlapping nothing).
**Use that zone** - do not invent a second convention. Two transient-message conventions on one
screen is the divergence WO-1228 and WO-1230 were written to prevent.

## Also visible, NOT this ticket (recorded so nobody re-reports them)

- The drawn staff lies across the body - already fixed and awaiting the gate (WO-1226 ruling
  2026-08-26, `_staffGripEuler` -> `(90,0,0)`).
- The gold rail reads as a coin at one third and `1500` at the far right - WO-1230 item 5 / WO-1221.
- One ability lozenge for a Mage - verify against the loadout before assuming a defect; Q is the
  locked basic and W/E/R are loadout-swappable (CLAUDE.md section 7).

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. The RESULT states the dungeon mask VALUE and whether defect 1 is mask or layout, with the
   proving line.
3. A regression pinning the dungeon bar's expected face set, so a future change is a deliberate
   ruling rather than silent drift. Prove it RED first if it is a defect (WO-1138); if the mask is
   correct, the case pins the CORRECT value and says so.
4. A device screenshot in a dungeon showing the bar and a flag acknowledgement that overlaps nothing.
   `UI_CAPTURE_OK` alone is not acceptance.
5. Greyscale check - the owner is red/green colourblind.
6. Owner felt-verifies and CLOSES.

## What NOT to touch

- The FLAG button itself or `BreakCaptureHarness`. Both work; this is presentation.
- The dungeon's darkness / torch design. Owner-ruled risk-reward.
- `HudActionBarModel.ButtonCount` (7) or `MaxVisibleFaces`. Read CLAUDE.md section 7 first.

---

## OWNER RULING 2026-08-26 - THE ONE-FACE MASK IS CORRECT. KEEP IT.

Codex confirmed the `calm(explore)` mask is intentionally `0x04` (Bag only). The owner ruled it
STAYS: *"Dungeon HUD should stay quiet. Manage belongs in menus; exit should remain
contextual/world-based."*

STOP: do NOT permanently add Quests / Manage / Exit to the dungeon bar. **Defect 1 in this ticket is
CLOSED AS WORKING AS DESIGNED** - the bar is not broken, it is quiet on purpose.

**What REMAINS open is Defect 2 only:** the FLAG acknowledgement covering the minimap. Codex reports
the duplicate acknowledgement is removed and the shared toast zone is in use - verify that and close,
or state what is left.

WARNING: if the single face renders CENTRED OVER THE HERO rather than in a bar, that is still worth
answering even with one face - a lone floating button reads as breakage. Report the slot geometry.