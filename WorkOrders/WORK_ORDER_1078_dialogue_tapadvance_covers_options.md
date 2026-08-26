# WORK ORDER 1078 — DialogueView: TapAdvance covers every dialogue option row

**Status:** CLOSED 2026-08-26 — owner felt-tested PASS on APK `2026.08.26.342478` (source `bcef3be7`).

**Minted:** 2026-08-24, UI seat, from the `CLI_LANES_WO_NUMBERS.md` UI-seat block (1078; banner bumped 1075 → 1079 in the same edit).
**Parent:** WO-1060 (`WORK_ORDER_1060_touch_clamp_and_overlap_oracle.md`) — the touch/overlap oracle that found this.
**Silo:** UI / HUD. **File-disjoint from WO-1075, WO-1076, WO-1077** — run all four in parallel.

---

## The panel

`Assets/_Modules/HUD/DialogueView.cs` — oracle panel label `DialogueOptions`.

## What the oracle reports — 18 findings, all BUTTONS OVERLAP

Recorded output: `Builds/wo1060-capture.log:17380-17584`. Assert format string:
`Assets/_Modules/Core/UI/LayoutOracle.cs:127`.

Six per aspect x three aspects; two panel variants (`_2opt`, `_4opt`) x three pairs. **Every finding is
`Zone_Body/TapAdvance` against something underneath it.**

| Aspect | TapAdvance rect | vs `BodyWell/ScrollZone/Viewport` | vs `Options/.../Opt0` | vs `.../Opt1` |
|---|---|---|---|---|
| 1920x1080 | x −356.9..356.9, y −325.2..18.8 (2opt) / y −337.8..31.4 (4opt) | **703.9x88** (2opt), **703.9x54** (4opt) | **620.5x112** | **620.5x112** |
| 2340x1080 | x −395.5..395.5, y −303.7..15.6 | **781x63.4** (2opt), **781x54** (4opt) | **689.9x112** | **689.9x112** |
| 2670x1200 | x −401..401, y −299.4..13.6 | **792x57** (2opt), **792x54** (4opt) | **699.8x112** | **699.8x112** |

⭐ **The option overlaps are exactly 112 px tall at every aspect — the FULL height of the option row.**
The rows are authored at `ElarionUiKit.MinTouchPx = 112` (`Assets/_Modules/Core/UI/ElarionUiKit.cs:347`),
and `TapAdvance` covers all of it. Not a clipped corner: the whole button.

## Why it matters to a player

⭐ **This is the panel where the player makes choices, and an invisible advance-the-dialogue layer is
laid over every choice.** If `TapAdvance` wins the raycast, tapping "Opt0" or "Opt1" does not select
that option — it advances the dialogue, and the choice is made for the player by whatever the advance
path does. In a conversation system that gates quests and Echo unlocks, a mis-resolved choice is not a
cosmetic annoyance; it is the player being routed somewhere they did not pick, with no visible cause.

**The repo has already seen this exact failure once.** `DialogueView.cs:289` records an F8 fleet finding
of precisely this class — `CLICK-BLOCKED: 'CloseButton' covered by 'TapAdvance'` x7 — fixed at the time
with a `SetAsLastSibling` on the **Close only**. ⛔ **The option rows were never addressed.** That is
this ticket.

## Root cause — visible in the source

`DialogueView.cs:279`:

```csharp
var tapGo = new GameObject("TapAdvance", typeof(Image), typeof(Button));
tapGo.transform.SetParent(bodyZone, false);
trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
tapGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
```

The comment at `:277` claims it is *"deliberately contained to the panel — never a full-screen
catcher."* That is true and beside the point: it fills the **body zone**, and the body zone is where
the option rows live. Containment to the panel does not contain it away from the buttons.

**The fix:** scope `TapAdvance` to the body zone **minus** the `Options` region — the advance-catcher
should cover the prose well, not the choices. The prior `SetAsLastSibling` precedent at `:289` shows
sibling ordering was the earlier remedy; ⚠ **sibling order is invisible to the oracle**, so a
z-order-only fix leaves all 18 findings red. Geometry must change.

## ⚠ The same LEAD DECISION as WO-1077 hangs over this ticket

`LayoutOracle.cs:141` already excludes graphic-less buttons from the BUTTON OVER TEXT assert
(`if (!HasVisibleGraphic(b)) continue;   // hit areas / scrims cannot collide visually`) but **not**
from BUTTONS OVERLAP. Extending that exclusion would drop **all 18** of this panel's findings and
**all 3** of WO-1077's — **21 of the 43** — in one edit. WO-1060 deliberately declined, on the grounds
that *"narrowing a rule to make a gate green is how a gate stops meaning anything."* The owner has
ruled this a **lead** call about the tool.

⛔ **This ticket does not make that call.** Two honest paths — **(a)** fix the geometry here (cannot
weaken the gate), **(b)** the lead rules the tool change, closing this and WO-1077 together.
**Surface both. If no decision arrives, do (a).** ⚠ Whichever path is taken, **it is ONE edit
coordinated with WO-1077** — never two seats editing `LayoutOracle.cs` at once.

⚠ **And note the clear-image catcher genuinely does steal raycasts** — `raycastTarget` stays on
through a fully transparent `Image`. So path (b) would make the oracle blind to a defect that is real.
That argues for (a) on this panel specifically, given `:289` proves the class has already shipped once.

## Acceptance

- [ ] The `[ui-touch-oracle]` suite (`Assets/Editor/Regression/UiTouchClampRegression.cs`) reports
      **ZERO** findings whose panel label starts `DialogueOptions`, at **all three** captured aspects
      and in **both** the `_2opt` and `_4opt` variants.
- [ ] `TapAdvance` does not intersect `Opt0` or `Opt1` (nor `Opt2`/`Opt3` in the 4opt variant) — prove
      it with the resolved rects from a fresh capture log, not by reading the source. Tolerance is
      `LayoutOracle.OverlapPadPx = 2f` (`LayoutOracle.cs:52`).
- [ ] **A capture proves an option tap selects that option** — the raycast winner is named from data,
      not inferred from sibling order. This is the criterion the `:289` fix was missing.
- [ ] Tap-to-advance still works over the prose well; if the catcher's area is reduced, the ticket
      states by how much.
- [ ] No `SUB-TOUCH-FLOOR BAND` finding appears on `DialogueOptions` — every option row's shortest
      side stays **>= 112** ref px.
- [ ] The repo-wide marker count drops by exactly 18 — from `UI_TOUCH_FAIL x43` to `x25` — with no
      other panel's findings changed **unless** path (b) is chosen, in which case WO-1077's 3 drop too
      and **both tickets must say so.**
- [ ] COMPILE_GATE_OK + the regression marker.

⛔ **THE ALLOW-LIST IS NOT AN OPTION.** `TouchBaseline` (`Assets/Editor/UICaptureLaunch.cs:3771`) stays
at its **two** entries — `ArmyMuster` and `EquipDrawer`. The owner ruled 2026-08-24 (batch 2, ruling 9):
*"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*
A tool-rule change (path b) is **not** an allow-list entry and must not be smuggled in as one.

⚠ **No acceptance criterion here depends on colour.** Every check is a pixel measurement, a finding
count, or a raycast identity — all readable without hue.

## Do NOT touch

- ⛔ `TouchBaseline` in `UICaptureLaunch.cs`.
- ⛔ `Assets/_Modules/Core/UI/LayoutOracle.cs` **unless the lead explicitly rules path (b)**, and then
      only as one edit shared with WO-1077.
- ⛔ `ElarionUiKit.MinTouchPx` — the option rows are authored AT the floor, which is correct. Do not
      shrink them to dodge the overlap.
- ⛔ The `SetAsLastSibling` on `CloseButton` (`DialogueView.cs:289`) — it fixed a real finding; leave it.

---

## Status corrected 2026-08-25 (CLI lead)

Proven by a FRESH capture: `Builds/uicap-0825am.log` (2026-08-25 06:00, marker `UI_CAPTURE_OK 89`). The panel returns **ZERO** `touch-oracle` findings.

The fix landed in `ee7763db3`; the status line was never flipped, so the board advertised finished work as available. That is the exact failure `docs/BOARD.md` section 2 exists to prevent - the board is only as truthful as these lines.

Awaiting the owner's felt-close (PO closes, not CLI - CLAUDE.md section 13).

!! The `UI_TOUCH_FAIL x43` baseline this ticket computed against is STALE. Measured total on 2026-08-25 is **21**, and none of it is this panel.

Previous status line, kept for the record:

> **Status:** READY TO IMPLEMENT
