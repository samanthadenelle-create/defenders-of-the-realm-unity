# WORK ORDER 1077 — EndStateView: the full-panel dismiss catcher sits on top of the Repair All CTA

**Status:** READY TO IMPLEMENT

**Minted:** 2026-08-24, UI seat, from the `CLI_LANES_WO_NUMBERS.md` UI-seat block (1077; banner bumped 1075 → 1079 in the same edit).
**Parent:** WO-1060 (`WORK_ORDER_1060_touch_clamp_and_overlap_oracle.md`) — the touch/overlap oracle that found this.
**Silo:** UI / Village-EndState. **File-disjoint from WO-1075, WO-1076, WO-1078** — run all four in parallel.

---

## The panel

`Assets/_Modules/Village/UI/EndState/EndStateView.cs`

## What the oracle reports — verbatim, 3 findings (one per aspect)

Recorded output: `Builds/wo1060-capture.log:17344-17368`. Assert format string:
`Assets/_Modules/Core/UI/LayoutOracle.cs:127`.

```
BUTTONS OVERLAP [EndStateWaveClear_repairAll_1920x1080 @1920x1080]
 'ObsidianPanel/TapDismiss' (x -637.1..637.1, y -413.7..367.4) and
 'ObsidianPanel/PanelContent/Zone_CompactCta/ObsBtn_Repair All - 120 wood, 40 iron' (x -157..157, y -350.1..-235)
 share 314x115.1 ref px -- two tap targets in one place; only one can win the raycast.
```

| Aspect | TapDismiss rect | Overlap |
|---|---|---|
| 1920x1080 | x −637.1..637.1, y −413.7..367.4 | **314x115.1** |
| 2340x1080 | x −703.3..703.3, y −391..332.4 | **314x115.1** |
| 2670x1200 | x −712.7..712.7, y −385.9..328 | **314x115.1** |

The overlap is the **entire CTA** (314 x 115.1) at every aspect, because `TapDismiss` is a full-panel
catcher and the CTA lives wholly inside it.

## Why it matters to a player

⭐ **The end-of-wave screen offers "Repair All — 120 wood, 40 iron", and an invisible full-panel
dismiss layer covers 100% of that button.** If the catcher wins the raycast, the player who taps
Repair All gets the panel dismissed instead — the repair never happens, the resources are never
spent, and nothing on screen says why. They are left with a damaged town and the belief that they
already paid to fix it. That is the most expensive mis-tap on the screen, because the player's mental
model diverges from the save state.

## ⚠ THIS ONE IS DISPUTED — read before you change anything

`EndStateView.cs:720` carries a comment stating the layering is **deliberate** (WO-672): the catcher is
sent behind the CTA so the CTA wins.

`EndStateView.cs:726`:

```csharp
var tap = new GameObject("TapDismiss", typeof(Image), typeof(Button));
tap.transform.SetParent(chrome.root.transform, false);
tapRt.anchorMin = Vector2.zero; tapRt.anchorMax = Vector2.one;
tapImg.color = Color.clear;      // invisible; raycastTarget still catches taps
...
if (hasBannerCta) tap.transform.SetAsFirstSibling();
```

So the sibling ordering may already resolve the raycast correctly at runtime. **The oracle is
geometric — it cannot see sibling order.** ⛔ **Do not "fix" this by deleting the dismiss-anywhere
behaviour until you have proved, from a capture, which one actually receives the tap.** §12 applies:
instrument first, cite the data line.

⭐ **There is a LEAD DECISION sitting under this ticket, and it is a decision about the TOOL, not this
panel.** `LayoutOracle.cs:141` already excludes graphic-less buttons from the BUTTON OVER TEXT assert
(`if (!HasVisibleGraphic(b)) continue;   // hit areas / scrims cannot collide visually`) — but **not**
from BUTTONS OVERLAP. Extending that same exclusion to BUTTONS OVERLAP would drop **all 3** of this
panel's findings and **all 18** of WO-1078's — **21 of the 43** — in one edit.

WO-1060 deliberately did not do this, on the stated grounds that *"narrowing a rule to make a gate
green is how a gate stops meaning anything."* The owner has ruled this a **lead** call about the tool.
⛔ **This ticket does not make that call and must not make it unilaterally.** Two honest paths:

- **(a) Panel fix:** shrink `TapDismiss` so it does not cover the CTA (e.g. catcher spans the panel
  *minus* `Zone_CompactCta`). Keeps the oracle strict; costs a little dismiss area.
- **(b) Tool fix (LEAD ONLY):** extend the `HasVisibleGraphic` exclusion to BUTTONS OVERLAP, which
  closes this ticket and WO-1078 together — but weakens the rule for every future panel, and the
  clear-image catcher **does** still steal raycasts, so the rule is not obviously wrong today.

**Surface both to the lead before implementing. If no decision arrives, do (a)** — it is the option
that cannot weaken the gate.

## Acceptance

- [ ] The `[ui-touch-oracle]` suite (`Assets/Editor/Regression/UiTouchClampRegression.cs`) reports
      **ZERO** findings whose panel label starts `EndState`, at **all three** captured aspects.
- [ ] **A capture proves which control receives a tap at the CTA's centre** — the raycast winner is
      named from data, not from the sibling-order comment. (If path (b) is chosen, this criterion
      still stands: the exclusion may only be added once the raycast is proven safe.)
- [ ] `Repair All` remains tappable and the dismiss-anywhere behaviour is not silently deleted; if it
      is reduced, the ticket states by how much.
- [ ] No `SUB-TOUCH-FLOOR BAND` finding appears on `EndState` — every button's shortest side stays
      **>= 112** ref px (`ElarionUiKit.MinTouchPx`, `Assets/_Modules/Core/UI/ElarionUiKit.cs:347`).
- [ ] The repo-wide marker count drops by at least 3 — from `UI_TOUCH_FAIL x43` — with no other
      panel's findings changed **unless** path (b) is chosen, in which case WO-1078's 18 drop too and
      **both tickets must say so.**
- [ ] COMPILE_GATE_OK + the regression marker.

⛔ **THE ALLOW-LIST IS NOT AN OPTION.** `TouchBaseline` (`Assets/Editor/UICaptureLaunch.cs:3771`) stays
at its **two** entries — `ArmyMuster` and `EquipDrawer`. The owner ruled 2026-08-24 (batch 2, ruling 9):
*"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*
Note that a tool-rule change (path b) is **not** an allow-list entry — it is a different decision with
a different owner, and it must not be smuggled in as one.

⚠ **No acceptance criterion here depends on colour.** Every check is a pixel measurement, a finding
count, or a raycast identity — all readable without hue.

## Do NOT touch

- ⛔ `TouchBaseline` in `UICaptureLaunch.cs`.
- ⛔ `Assets/_Modules/Core/UI/LayoutOracle.cs` **unless the lead explicitly rules path (b)** — and then
      it is one edit, coordinated with WO-1078, never two seats editing the oracle at once.
- ⛔ `ElarionUiKit.MinTouchPx` / `CanonCtaHeight`.
