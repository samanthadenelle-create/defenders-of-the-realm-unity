# RESULT — WO-1252 builders busy message offers a next step

**Date:** 2026-08-27  **Seat:** CLI
**Status:** IMPLEMENTED — pending PO felt-verify (no Unity run; do not claim COMPILE_GATE_OK)

Owner, on device: *"if you go to place a build and all the builders are busy should say wait or compltee under manage something like that"*. The refusal was fine; the dead end was not.

## Axis split (kept)

- `BuildTimerService.LineFullMessage` is still DEPTH and UNCHANGED:
  `Builders queue is full (n/5). Cancel or finish an item first.`
  The existing WO-1045 oracle still FAILS if that sentence contains `"busy"`.
- Place-time copy is a NEW `BusyCrewMessage()` (CONCURRENCY next-step). Distinct method, distinct sentence.

`freeBuildSlots` stays 2. `queueDepthPerLine` stays 5. TryBuySlot Echo/crystal gate untouched. WO-1253 permanent-builder SKU grant untouched. Builders-chip double-tap stays retired. Route named is Manage (`PanelId.Manage` / Upgrade bar face).

## Exact player-facing sentences (ASCII, explicit newlines)

Core (does-not qualify for TryBuySlot, SKU not offered or already owned):

```
Builders are busy.
Wait, or complete under Manage.
```

Qualifies for TryBuySlot (`CanBuySlot` true — Echo gate passed; crystals not required to NAME the option):

```
Builders are busy.
Wait, or complete under Manage.
Or buy an extra queue slot.
```

Permanent-builder SKU offered (`OffersPermanentBuilder` = on browsable shelf AND not owned):

```
Builders are busy.
Wait, or complete under Manage.
Or get a store builder.
```

Both options live (qualifies-to-buy AND SKU offered):

```
Builders are busy.
Wait, or complete under Manage.
Or buy an extra queue slot.
Or get a store builder.
```

No crystals wording. Store builder is named only when the service says the SKU is offered. Slot is named only when `CanBuySlot` is true.

## Width (measured)

Toast card 500 px; inner 462 px; 14 px/glyph (24 px LegacyRuntime, conservative).

| Line | chars | px | budget |
|---|---|---|---|
| `Builders are busy.` | 19 | 266 | 462 |
| `Wait, or complete under Manage.` | 31 | 434 | 462 |
| `Or buy an extra queue slot.` | 27 | 378 | 462 |
| `Or get a store builder.` | 23 | 322 | 462 |

Wrap is explicit `\n`, never a mid-word ellipsis. Card height grows with line count (`24 + lines*28`, min 72). Ghost reason pill matches 500 px and grows the same way. Toast is NOT a button (`blocksRaycasts = false`); MinTouchPx does not apply. Multiline life 3.6 s.

## Where it surfaces

- `BuildModeController.Place()` — commit-time DEPTH-full refusal toasts `BusyCrewMessage()` (no longer recomposes LineFullMessage).
- Hover + pending place loops — ghost goes red and the floating label quotes the same sentence before the tap.
- Upgrade QueueFull face still quotes `LineFullMessage` (DEPTH, no "busy").

## Regression (RED-first)

Extended `UpgradeQueueFullSurfaceRegression` `[queue-full-surface]`:

1. Method-lookup `BusyCrewMessage` / `ComposeBusyCrewMessage` / `OffersPermanentBuilder` — missing = RED (the pre-this-ticket tree).
2. Composer branches: does-not / qualifies-to-buy / store-only / slot-only. Empty, non-ASCII, no Manage-or-wait, dangling slot, dangling store, or a line over 462 px = RED.
3. `BuildModeController` source must quote `BusyCrewMessage` and must NOT recompose `"Builders queue is full ("`.
4. Live service: 0 Echoes does not mention slot; 5 Echoes does; the two sentences differ.
5. Existing `LineFullMessage` `"busy"` fail is unchanged.

## Files

- `Assets/_Modules/Village/Buildings/BuildTimerService.cs`
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs`
- `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs`
- `Assets/_Modules/Village/BuildMode/GhostPreview.cs`
- `Assets/Editor/Regression/UpgradeQueueFullSurfaceRegression.cs`

Brace-balanced on all five. No NUL bytes. No commit. No Unity. WO Status line not flipped (owner / committer).

## Owner felt-verify

Fill the Builder line (or both crews then queue to the depth cap) and try to place. The toast must be fully readable at device resolution, name wait-or-Manage, and only mention a slot / store builder when those options are actually live.
