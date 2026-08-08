> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 21d166c9; BuildingUpgradePanelMvvm.cs 1024 lines changed, and BuildingUpgradePanelLayoutTests.cs exists.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 895 — Building upgrade panel: "next upgrade only" redesign (kill the crammed tier rail)

**Status:** DONE (reconciled 2026-08-08) · **Silo:** UI / building upgrades · **For:** CLAUDE CLI · **Date:** 2026-08-05
**PO:** Samantha (owner) · **Author:** UI seat
**Owner ruling:** *"we don't need to see all the upgrades, just details on what they can get to next."* + "cleaner, not all smashed together."

## 0. Problem (grounded)
`Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` builds the LEFT "ENHANCEMENT PATH"
as **a HORIZONTAL row of 6 tier CARDS** (L23, L641-644), each trying to hold tier name + description + an Unlock
button. At 6-across every card truncates mid-word ("Muster the Bar", "Rea…", "Unlock 'Muste", "Standin g Army"),
and the right detail panel clips too. VM = `BuildingUpgradeVM`; data = `BuildingTierCatalog`/`BuildingUpgradeDef`.

## 1. Redesign — CURRENT → NEXT focus (replaces the 6-card rail)
Show only where they are and what's next. Layout, top→bottom, inside the existing Obsidian panel (keep the
title, the resource header bar, and the Upgrade/Skills tabs unchanged):

1. **Progress strip** — `Tier N of 6` label + a **6-segment bar** (N filled gold, rest dim) + current tier name (`Now: Muster the Barracks`). One slim row, full width. Replaces the card rail entirely.
2. **NEXT UPGRADE hero card** (the focus, generous padding):
   - Label `Next upgrade · Tier N+1` + the next tier **name** (e.g. `Drill Yard`) + its **icon** (56px).
   - **Full description** (no truncation, wraps): what the tier does.
   - **Bonuses** as a short bulleted list (icon + text), each on its own line: e.g. `Unlocks the Spearman troop` · `Troop health +8%` · `Structure HP +20`. Pull these from the `BuildingUpgradeDef` fields (do NOT jam them into one truncated string).
   - **Upgrade cost** row (wood / food / crystal with amounts), affordability-styled.
   - **One STATEFUL action button** (the WO-832 one-true-button, WO-841 live-tick) — see §1b.
3. **Max-tier state:** when already at Tier 6, the next card shows a "Fully enhanced" state (no action), progress bar full.

### 1b. Upgrade button STATE MACHINE (owner: "on click it should change to In progress / Queued / Missing resources")
The one action button reflects the live upgrade state; its label + interactivity are driven by the VM, never colour alone:

| State | When | Button label | Interactable | Notes |
|-------|------|--------------|--------------|-------|
| **Ready** | affordable + a build crew is free | `Upgrade to <NextName>` | yes | the default |
| **Missing resources** | can't afford the cost | `Missing resources` | no (informative) | show which resource(s) short in the cost row (the short one flagged by shape/icon, not colour) |
| **Queued** | clicked while all crews busy → upgrade enters the Obsidian build queue | `Queued` | no | the tier joins the queue channel; button holds Queued until a crew frees |
| **In progress** | a crew is actively upgrading this building | `In progress · M:SS` | no | **live countdown** (reuse the WO-841 live-tick); on completion the panel advances to the next tier |

- **On click (Ready):** spend resources, start the upgrade (or enqueue if crews busy), and the button IMMEDIATELY flips to `In progress · M:SS` (crew free) or `Queued` (crews busy) — no dead click, no need to reopen the panel.
- The state is READ from the same authority the queue/build system uses (do NOT invent a second state) — `BuildingUpgradeService`/the Obsidian queue. The button is a pure reflection of it.
- While `In progress`, the progress strip's current-tier segment can show a fill/pulse tied to the countdown (optional).

**Reference the rendered mockup** (this session) as the visual target. No horizontal tier-card rail survives.

## 2. Behavior / spacing
- Everything left-column full-width and vertically stacked; the old ~65/35 left/right split collapses — the NEXT card is the body; the small "Select a tier" detail text is folded into the NEXT card.
- Text never truncates: descriptions wrap; bonuses are separate rows; use the kit's block-fit (`FitBlock`) so copy never spills its band.
- Keep the resource header, tabs, and Close.

## 3. Files
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` — replace the ENHANCEMENT PATH card-rail builder (L641+) with the progress strip + NEXT card.
- `BuildingUpgradeVM.cs` — expose `CurrentTier`, `TotalTiers`, and the NEXT `BuildingUpgradeDef`'s name/description/bonuses[]/cost/canAfford/crewsBusy (if not already surfaced).
- Reuse `ElarionUiKit` components + `docs/UI_BLINK_TEMPLATE_CANON.md` conventions. `Assets/Tests/EditMode/BuildingUpgradePanelLayoutTests.cs` — update to the new layout.

## 4. Acceptance criteria
**Layout:**
- [ ] NO horizontal 6-tier card rail. The panel shows a progress strip + a single NEXT-upgrade card.
- [ ] **No truncated text anywhere** — tier name, description, bonuses, and cost all render in full (the exact failure in the screenshot is gone).
- [ ] Progress reads `Tier N of 6` with a matching segmented bar.
- [ ] Bonuses are separate readable lines (not one clipped run-on string).
- [ ] Exactly one action button (WO-832 one-true-button).
- [ ] **Button state machine works (§1b):** clicking Ready flips it immediately to `In progress · M:SS` (crew free) or `Queued` (crews busy); unaffordable shows `Missing resources`; `In progress` counts down live (WO-841) and the panel advances on completion. State is read from the real build/queue authority (no second state).
- [ ] Resource header, Upgrade/Skills tabs, Close unchanged and uncramped.
**Engineering:** `COMPILE_GATE_OK` + `REGRESSION_OK`; `BuildingUpgradePanelLayoutTests` pass; MVVM preserved.
- [ ] Headless UI capture at Tier 1→2 and a mid-tier + a max-tier building — **open the PNGs**, confirm nothing clips, attach to RESULT.
**Owner felt-close:** opens Barracks, immediately sees what the next upgrade gives and its cost, reads every word, one clear button.

## 5. RESULT
`WorkOrders/WORK_ORDER_895_building_upgrade_next_only_redesign.RESULT.md` — screenshots at low/mid/max tier.
