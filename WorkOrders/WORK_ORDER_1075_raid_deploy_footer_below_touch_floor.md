# WORK ORDER 1075 — RaidDeployScreen: the assault footer falls under the touch floor on tall landscape

**Status:** READY TO IMPLEMENT

**Minted:** 2026-08-24, UI seat, from the `CLI_LANES_WO_NUMBERS.md` UI-seat block (1075; banner bumped 1075 → 1079 in the same edit).
**Parent:** WO-1060 (`WORK_ORDER_1060_touch_clamp_and_overlap_oracle.md`) — the touch/overlap oracle that found this.
**Silo:** UI / Village-Hero. **File-disjoint from WO-1076, WO-1077, WO-1078** — run all four in parallel.

---

## The panel

`Assets/_Modules/Village/Hero/RaidDeployScreen.cs`

## What the oracle reports — verbatim, 4 findings

Recorded output: `Builds/wo1060-capture.log:17596-17632`. Assert format string:
`Assets/_Modules/Core/UI/LayoutOracle.cs:178`.

```
SUB-TOUCH-FLOOR BAND [RaidDeploy_2340x1080 @2340x1080]
  'ObsidianPanel/PanelContent/Zone_Footer/ObsBtn_Army Ready?'    resolves 422.6x103   -- shortest side 103 is 9 px UNDER ElarionUiKit.MinTouchPx (112)
  'ObsidianPanel/PanelContent/Zone_Footer/ObsBtn_BEGIN ASSAULT'  resolves 1003.6x103  -- shortest side 103 is 9 px UNDER ElarionUiKit.MinTouchPx (112)
SUB-TOUCH-FLOOR BAND [RaidDeploy_2670x1200 @2670x1200]
  'ObsBtn_Army Ready?'    resolves 428.2x101.7  -- shortest side 101.7 is 10.3 px UNDER 112
  'ObsBtn_BEGIN ASSAULT'  resolves 1017x101.7   -- shortest side 101.7 is 10.3 px UNDER 112
```

⭐ **Both buttons CLEAR the floor at 1920x1080 and fall under it at the two taller-landscape
aspects.** That is what makes this worth a ticket rather than a nudge: the defect is invisible on the
aspect most likely to be checked.

The floor: `ElarionUiKit.MinTouchPx = 112f` (`Assets/_Modules/Core/UI/ElarionUiKit.cs:347`,
documented at `:332` as *"~= 50 dp / ~44 pt — the hard floor, right AT Apple's minimum"*).
The canonical CTA height beside it: `CanonCtaHeight = 132f` (`:341`).

## Why it matters to a player

**These are the two buttons the entire raid flow funnels into** — `BEGIN ASSAULT` is the commit, and
it is the one that is 1003 px wide and **9 to 10 px too short**. On a tall-landscape phone the primary
action of the raid screen sits below the platform touch minimum, so near-misses along the top and
bottom edges drop. A player who taps and gets nothing does not conclude "I missed"; they conclude the
game did not respond, and they tap again — on a button that commits an army.

⚠ **And the runtime clamp makes it worse, not better.** The oracle's own message says so: `ClampMinTouch`
will grow an undersized band **symmetrically about its centre**, spilling it into both neighbours. So
the shipped behaviour is not "a slightly small button" — it is a button that silently eats the margins
around it at exactly the aspects where space is tightest.

## Root cause — visible in the source, no investigation needed

`RaidDeployScreen.cs:450` (`BuildDeployBar`) authors the footer band in **fractions**, not pixels:

```csharp
ElarionUiKit.Button(footer, "Army Ready?", ElarionUiKit.ButtonKind.Quiet,
    new Vector2(0.00f, 0.05f), new Vector2(0.28f, 0.95f), OnAutoRecommend);
...
var deployBtn = ElarionUiKit.Button(footer, "BEGIN ASSAULT", ElarionUiKit.ButtonKind.Confirm,
    new Vector2(0.32f, 0.05f), new Vector2(0.985f, 0.95f), OnDeploy);
```

The `0.05 .. 0.95` vertical fraction takes **0.90 of the footer band**. Once the band itself shrinks at
2340x1080 / 2670x1200, 90% of it lands under 112.

**The fix the oracle names:** author the button height **at the floor in pixels** (`MinTouchPx`, or
`CanonCtaHeight` if the band allows), not as a fraction of a band whose height varies by aspect.
⛔ Do not rely on the clamp — that is the message's closing sentence and the whole point of the rule.

## Acceptance

- [ ] The `[ui-touch-oracle]` suite (`Assets/Editor/Regression/UiTouchClampRegression.cs`) reports
      **ZERO** findings whose panel label starts `RaidDeploy_`, at **all three** captured aspects
      (1920x1080, 2340x1080, 2670x1200).
- [ ] Both `ObsBtn_Army Ready?` and `ObsBtn_BEGIN ASSAULT` resolve to a shortest side **>= 112** ref px
      at every captured aspect. Prove it with the resolved numbers from a fresh capture log, not by
      reading the source.
- [ ] No new finding of any of the three classes appears on `RaidDeploy_` (do not trade a
      SUB-TOUCH-FLOOR for a BUTTONS OVERLAP by growing into a neighbour).
- [ ] The repo-wide marker count drops by exactly 4 — from `UI_TOUCH_FAIL x43` to `x39` — with no other
      panel's findings changed.
- [ ] COMPILE_GATE_OK + the regression marker.

⛔ **THE ALLOW-LIST IS NOT AN OPTION.** `TouchBaseline` (`Assets/Editor/UICaptureLaunch.cs:3771`) stays
at its **two** entries — `ArmyMuster` and `EquipDrawer`. The owner ruled 2026-08-24 (batch 2, ruling 9):
*"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*
Adding `RaidDeploy` to it fails this ticket.

⚠ **No acceptance criterion here depends on colour.** Every check is a pixel measurement or a finding
count, both readable without hue.

## Do NOT touch

- ⛔ `Assets/_Modules/Core/UI/LayoutOracle.cs` — do not narrow the rule to go green.
- ⛔ `TouchBaseline` in `UICaptureLaunch.cs`.
- ⛔ `ElarionUiKit.MinTouchPx` / `CanonCtaHeight` — those are the floor, not a variable.
- ⛔ The raid readiness gate. WO-823 Phase E is rewiring `RaidDeployScreen.cs:477` and `:526`
      (the duplicate `_vm.DeployableCount` checks) — **that is a different silo in the same file.**
      Coordinate: this ticket owns `BuildDeployBar` geometry only.
