# WORK ORDER 1225 - A toast rendered UNDER a modal is still a silent grant

**Status:** FIXED 2026-08-26 - `COMPILE_GATE_OK` + `REGRESSION_OK 292/292`; post-fix APK visibility/felt verification queued
**Silo:** HUD / presentation ordering
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26 12:14.
Owner verbatim: *"it didt show or if it did it was under the echo introdution window that popped."*
Her read was correct and the log confirms it.

---

## PROOF — captured from the device, three milliseconds apart

```
12:14:16.401 [Flow:DailyChest] claimed +1000 Gold path=rewarded_double day=2026-08-26
12:14:16.404 [Flow:UI]         kit toast -> 'Claimed - 1,000 Gold added to your realm.' tone=Info
12:14:16.405 [Flow:DailyChest] claim toast path=rewarded_double -> 'Claimed - 1,000 Gold added to your realm.'
12:14:16.405 [Flow:UI]         external presentation END kind=rewarded ad place.daily.chest
                               caller=Daily Chest current=EchoUnlockDialogue depth=0
```

⭐ **WO-1213 WORKED.** The grant landed, `AcknowledgeClaim` ran, `ElarionUiKit.ShowToast` was called
with the correct text. **Do not re-open WO-1213 or re-implement its toast** — it is committed, green
and correct.

**The defect is one layer up:** `EchoUnlockDialogue` became the current presentation in the same
frame and rendered over the toast. The toast lives on its own `KitTransientToast` GameObject at
scene root with `sortingOrder 720`; the modal outranks it.

## Why this matters beyond the chest

This is **the same defect class WO-1213 was written to kill**, arriving through a different door. A
grant the player never sees is indistinguishable from a grant that never happened — and here the
code can even *prove* it fired, which makes it worse, not better: the trace says success and the
player saw nothing. Two hours earlier the same shape appeared in WO-1221 (`opener live=True` while
nothing rendered).

⚠ **It is NOT specific to the daily chest.** Any toast raised while a modal opens is occluded the
same way. Enumerate the toast callers before choosing a fix — `BankOverflowToastPresenter` and the
WO-1213 claim path are two; find the rest.

## ⭐ REQUIRED — OWNER RULING 2026-08-26 supersedes the z-order fix

Owner verbatim, on being shown the occlusion:
***"can it show streamers and +1000 showing to gold? counting up animation?"***

**Do NOT fix this by winning a sorting-order race.** The acknowledgement moves OFF the toast layer
entirely and ONTO the thing that is always on screen: **the gold chip.**

The shape:
1. **A `+1000` floats from the claim** and **flies to the gold chip** in the resource rail.
2. **The gold counter COUNTS UP** to its new value rather than snapping.
3. **Streamers / a celebratory burst** mark the moment.

**Why this is the better answer, and not just the prettier one:** a toast is a separate surface that
something can land on top of — which is the entire defect. The gold chip is already persistent HUD.
An acknowledgement anchored to the counter cannot be occluded by a modal that opens beside it, and
it reads as a reward rather than a notification.

### ⛔ REUSE — these already exist. Do NOT greenfield a floating-number system.

Read all of these before writing anything, and report what you reused:
- **`Assets/_Modules/Village/Enemies/DamageNumberSpawner.cs`** — floating numbers, already shipping.
  This is the closest precedent and the likeliest host.
- **`Enemy.cs`** — already pops an earned label at the corpse (`ShowFieldKillReward`), the same
  "+N at a world point" idiom.
- **`EchoService.cs`** — carries the harvest "+N" pop.
- **DOTween** is present; the SME notes are at `docs/reference/DOTWEEN_SME.md`. Read it before
  hand-rolling a coroutine tween.
- **`HudKitController.cs:1591-1596`** — the resource dock builds its chips from paired arrays; that
  is where the gold chip's rect comes from, and therefore the fly-to TARGET.

⚠ **Pooling is project law** (`ARCHITECTURE_PRINCIPLES` §2b.1/§2b.2, the two-VFX-stack scar):
anything spawned repeatedly comes from a POOL, one owner per concern. A burst that `Instantiate`s
per claim is the exact sprawl that rule exists to prevent.

⚠ **The count-up must not lie.** Animate FROM the pre-grant balance TO the measured post-grant
balance, read from the wallet — never from the requested amount. `Enemy.cs`'s kill grant already
makes this distinction explicit (rolled vs credited) and warns on a shortfall; an animation that
counts up to a number that was never banked is a new hollow assertion.

⛔ **A fix that makes the toast falsifiable but still invisible is not a fix.** Acceptance is the
owner SEEING it, not a line in a log.

### The toast ordering question is NOT closed by this

The `+1000` handles the daily chest. **Every other toast caller is still occludable** — enumerate
them (`BankOverflowToastPresenter` is one) and report which are affected. If the owner wants the
general ordering fixed too, that is a follow-up ticket, not a silent widening of this one.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A DEVICE SCREENSHOT showing the acknowledgement legible while the Echo unlock dialogue is
   open**, opened and looked at. This ticket cannot be closed on a marker — the whole defect is that
   the log already said success.
3. ⭐ A regression that **FAILS on today's tree**: raise a toast while a modal is registered open,
   and assert the toast surface is actually visible — resolved rect non-zero, not occluded. Reuse
   `DeNelle.Core.Diagnostics.UiSurfaceProbe` (WO-976) rather than re-deriving the arithmetic; it
   separates `SURFACE_ZERO_SIZE` / `SURFACE_TRANSPARENT` / `SURFACE_OFFSCREEN` / **`SURFACE_BEHIND`**,
   and `SURFACE_BEHIND` is precisely this case. ⚠ Measure AFTER layout settles, and emit a NAMED SKIP
   when unmeasurable — batchmode runs no layout pass.
4. The RESULT enumerates every toast caller found and states which are affected.
5. Owner felt-verifies on device and CLOSES.

## What NOT to touch

- ⛔ `DailyChestController`'s grant path or its `AcknowledgeClaim` call — WO-1213, committed and
  correct. This ticket is about what happens to the toast afterwards.
- ⛔ `EchoUnlockDialogue`'s own behaviour or timing. It is not misbehaving; it opened when it should.
- ⛔ The `PanelManager` single-modal contract itself.
- ⛔ Never convey the acknowledgement by colour alone (owner is red/green colourblind) — words.
## LANDED-WORK AUDIT (2026-08-26)

The shared `RewardFlightLayer` acknowledgement implementation and regression landed in
`b303c4fbf`. Fresh evidence: `Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83800` proves modal precedence (34500 over 34000), pooling,
measured-value counting, chest/celebration producers, HUD lifecycle wiring, and words-not-colour;
`:83814` is `REGRESSION_OK 291/291`. The regression explicitly partial-skips actual rendered
visibility in batchmode. **Post-FIXED APK checklist:** the acknowledgement/device screenshot while the Echo unlock
dialogue is open, in-play visibility proof, and owner felt-close.
