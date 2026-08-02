# WORK ORDER 841 — Upgrade panel "Under construction" countdown ticks live

**Status:** READY TO IMPLEMENT
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — `BuildingUpgradePanelMvvm.cs` (+ 1-line reference to WO-816 for the queue bar).
**Origin:** owner felt-test 2026-08-02, Barracks Enhancements — *"the countdown doesn't move till refreshed and
should be visual in queue."*

---

## 1. RCA — why "Under construction — Ns" freezes (sourced from live code)
The countdown text is **baked once and never regenerated per second**:
- Built in `BuildDetailCta` → `BuildLockButton`: `"Under construction — " + (int)timerSvc.RemainingSeconds(vmId) + "s"`
  (`BuildingUpgradePanelMvvm.cs:895`) — only inside `RebuildUpgrade()` (the full teardown+rebuild).
- `RebuildUpgrade()` is gated by `ContentSignature()` (`:225-234`, `:244-261`). The signature hashes selection, title,
  and per-perk name/equipped/locked/affordable/lockReason/effect/cost — but **NOT `RemainingSeconds`/`Progress`**. So
  second-to-second the signature is identical → the rebuild is skipped → the label keeps its original frozen text.
- `Render()` DOES run ~1/s (Update polls `ObsidianQueueGate.Status.Version` `:192`; `BuildTimerService` republishes at
  1 Hz `:129`) — but the signature guard swallows the CTA rebuild. It only "catches up" when some OTHER state flips the
  signature (affordability crosses a threshold, the job completes so `IsBuilding` flips, or the player selects another
  tier). Exactly the reported "doesn't move till refreshed by another event."

## 2. Fix — cheap per-second label update (NOT a full rebuild)
Do **not** add `RemainingSeconds` to `ContentSignature()` — that would force the expensive `RebuildUpgrade()` +
fit-guard re-arm every second (the churn WO-fix 2026-07-19 deliberately removed). Instead update just the one label:
- When `BuildDetailCta` takes the "Under construction" branch (`:893-897`), stash the created `TMP_Text` + the `vmId`
  in fields (`_underConstructionLabel`, `_underConstructionId`); clear them on any other branch / on rebuild.
- In `Update()` (`:192`, already runs every frame), after the version poll: if `_underConstructionLabel != null` and
  `timerSvc.IsBuilding(_underConstructionId)`, recompute `(int)RemainingSeconds` and set the label text **only when the
  integer second changed** (guard on the last-shown second). One TMP string assignment — no teardown, no fit re-arm.
- Completion still works via the existing path: when the job finishes, `IsBuilding` flips → affordability/lock state
  changes → the signature changes once → `RebuildUpgrade()` swaps the CTA back to "Upgrade". Clear the cached fields there.

## 3. "Visual in queue" — already live text; the BAR is WO-816 (do NOT duplicate)
The build is ALREADY shown live in the queue, so this half is largely done:
- The persistent right-column **Builders chip / QueueStatus band** (`HudKitController.BuildQueueStatusChip` `:663-708`,
  repaint `:1686-1701`) shows "Builders N/M" + rows ("> Barracks 9m30s" working / "- <next>" queued) and its countdown
  text **ticks live** (repaints on the 1 Hz `ObsidianQueueGate` publish).
- The modal **Work Queue** (`ObsidianQueueHud`) also ticks live 1s while open (`:107-113`, `FormatJobLine` `:389`).
- **What's missing = a progress/fill BAR** (both surfaces are ASCII text only). That is ALREADY SPECCED in
  **`WorkOrders/WORK_ORDER_816_queue_timer_progress_bars.md`** — add `TotalSec` to `ObsidianQueueGate.QueueEntry`
  (`:44-49`, today it drops the total), populate from `BuildJobData.StartMs/DurationMs` (`BuildJobData.cs:79-94`;
  `BuildTimerService.Progress()` already returns 0..1 `:356-365`), and render a per-row bar (`ElarionUiKit.BuildObsidianBar`,
  already used for the wave bar `:646`). **Do NOT re-spec that here — prioritize/sequence WO-816 for the bar.**

## 4. Files to edit
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` — the cached-label live-tick (§2).
- (Queue progress bar = WO-816, separate; reference only.)

## 5. Acceptance criteria
- [ ] With a building upgrade under construction, the panel CTA "Under construction — Ns" **counts down every second**
      without needing another interaction, and flips to "Upgrade" on completion.
- [ ] No per-second full rebuild (no fit-guard warning spam); only the label text updates each second.
- [ ] Confirm the Builders chip countdown already ticks live (no regression); the progress BAR remains WO-816's scope.
- [ ] `CompileGate` green.

## 6. Do NOT
- Do NOT add `RemainingSeconds`/`Progress` to `ContentSignature()` (forces the expensive per-second rebuild).
- Do NOT duplicate WO-816's queue progress-bar work here — reference it.
- Do NOT hand-edit scenes.
