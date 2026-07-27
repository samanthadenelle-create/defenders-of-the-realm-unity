# WO-778 — Queue UX completion (labels · reachability · layout · Train-strip · flip · sell-time)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-26 (CLI, from relayed CoC-review of the WO-773 queue surface + gameplay-gap P0-A/P0-B)
**Lane:** Queue/HUD UX (single lane — owns the queue surface). Dispatch after the WO-771.9 code batch is banked.
**Anchor:** WO-773 (multi-channel queue), `PAIN_POINTS_2026-07-26.md` (F2 naming, §7 monetization), `docs/qa/GAMEPLAY_GAPS_2026-07-26.md` (P0-A/P0-B).

## Why
The WO-773 multi-channel model is correct (Builder/Train/Research, ASCII markers, offline-fair), but the SURFACE is incomplete and partly unreachable:
- **P0-A:** `ObsidianQueueGate.RequestToggle` has ZERO callers — no HUD button opens the queue panel. The headline feature ships DARK.
- **P0-B:** buy-slot / instant-finish / ad-skip exist only as `BuildTimerService` methods + config — no UI, no callers. V1's sell-time revenue model has no in-game path.
- Job lines are generic ("Train"/"Upgrade") with no target identity; `KindLabel` has no `BarracksUpgrade`/`TroopUpgrade` cases → raw enum string.
- Queue list is parented to `chrome.content` (not `layout.body`) with a fixed ~16 lines for 3 channels × slots+queues → busy state clips + risks the pet-roster title/Close overlap class.
- Train tab is still instant `TrainNow`; the timed `EnqueueTraining` path exists but the queue stays empty while Train feels instant (un-CoC).

## Scope
1. **Kind labels + target identity** (`ObsidianQueueHud` + its label helper): add `KindLabel` cases for `BarracksUpgrade`, `TroopUpgrade` (+ verify `TrainTroop`, all Builder kinds). Each active/pending job line carries its TARGET: "Footman ×1", "Barracks → L2", "Archer → L3", the structure display-name/id for build/upgrade/repair. Read the target from the `BuildJobData` payload (troop id, building id, level). Player-facing terms only — never "Obsidian".
2. **P0-A reachability**: add a persistent HUD button (in `VillageHudController`) that calls `ObsidianQueueGate.RequestToggle()` to open/close the WORK QUEUE panel. Small chip is fine ("Builders 1/2 · Training 0:42"). Panel must be openable in normal play.
3. **Layout/overflow fix**: reparent the queue list to `layout.body` (NOT `chrome.content`); make it scroll when jobs exceed visible slots. No clipped bottom, no title/Close collision on FrameCore (mirror the pet-roster fix — see memory `build-hud-mobile-design` / the Echo/pet overlap fixes).
4. **Barracks Train strip**: in `TroopTrainingPanel`, render the Train-channel queue (active + pending) inline so training progress is visible where you train.
5. **Train → queue flip** (WWCD: CoC training is timed): switch the live Train CTA (`TroopTrainingVM.Train` / `TroopDialogueCommands.Train`) from instant `ArmyStorage.TrainNow` to `BarracksService.EnqueueTraining` (Train channel + `TrainTroopEffect` grant on complete). Keep `TrainNow` as an explicit dev/cheat path only. Update `TroopTrainingVMTests` to the queued behavior. **Flag in the RESULT for owner felt-verify** (felt-speed change).
6. **P0-B sell-time surface**: on the queue HUD, per active job add buy-slot / instant-finish / rewarded-ad-skip buttons that call the existing `BuildTimerService` APIs (`BuySlot`, instant finish, ad-skip). No new economy logic — just the UI + wiring to the existing methods. Respect `BuildTimerConfig` knobs.

## Acceptance (data-verified)
- Extend `ObsidianQueueRegression`: assert `KindLabel` returns a non-enum, target-bearing string for `BarracksUpgrade`/`TroopUpgrade`/`TrainTroop`/build kinds; assert a HUD toggle caller for `ObsidianQueueGate.RequestToggle` exists (reachability); assert the list host is `layout.body`.
- EditMode: a test that a queued Train job appears in the Train strip (active) then grants to `ArmyStorage` on complete; updated `TroopTrainingVMTests` for the flip.
- **Felt (owner)**: open queue from HUD; train a Footman → see "Footman ×1" ticking in Training; a wall upgrade in Builders concurrently; buy-slot/instant buttons present; nothing clipped/overlapping on device.
- **UI screenshot-verify** headless before build (memory `headless-screenshot-verify-ui-before-build`): capture the WORK QUEUE panel + Barracks Train tab; confirm no overlap/clip, correct labels.

## Do NOT touch
- The queue ENGINE (`ObsidianQueueEngine`/`BuildTimerService` resolve/cascade logic — correct per WO-773); only ADD the label/target read + call the existing sell-time APIs.
- `ArmyStorage.TickRecovery` (separate lane — the wounded-recovery wiring).
- Raid UI (WO-774).
