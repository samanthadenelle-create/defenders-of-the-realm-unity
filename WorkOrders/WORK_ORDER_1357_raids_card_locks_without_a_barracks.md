# WORK ORDER 1357 — Journey RAIDS card locks (with a reason) when there is no barracks

**Status:** DONE
**Silo:** HUD / Raids
**Seat:** CLI (edit-only agent; lead gates + commits)
**Date:** 2026-09-03
**Files:** `Assets/_Modules/Core/HudModel/PostureSignals.cs`,
`Assets/_Modules/Village/Troops/RaidCapabilityHudBridge.cs`,
`Assets/_Modules/HUD/PlayerDeckWorkspace.cs`,
`Assets/Editor/Regression/RaidsDiscoverabilityRegression.cs`

---

## 1. The owner's ruling (verbatim, 2026-09-03)

> "Raid button under journey should fail gracefully, it works great if there is a barracks
> but should show locked if doesnt have one yet or its destroyed"

---

## 2. RCA — one rule, two surfaces, one of them ignoring it

`PostureSignals.RaidCapable` has been the single raid-door predicate since WO-835, published
by `RaidCapabilityHudBridge` (`Assets/_Modules/Village/Troops/RaidCapabilityHudBridge.cs`)
as `FeatureFlags.Raid AND StructureSingleton.IsBuilt("barracks")`, and consumed by
`HudActionBarModel.ComputeMask` for the action bar's Raids face.

The Journey deck card did **not** consult it:

```
Assets/_Modules/HUD/PlayerDeckWorkspace.cs:534   (pre-fix)
    new Card { Title = "Raids", Purpose = "Choose a camp and deploy your army", Concept = "raid",
        ArtKey = "raids", Available = () => true,
        Open = RaidEntryGate.RequestOpen }
```

`Available = () => true`. The card was always offerable. Tapping it fires
`RaidEntryGate.RequestOpen` -> `RaidEntryBridge` -> `RaidSelectionScreen.Open`
(`Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:82`), which gates on **army readiness
only** — there is no barracks check anywhere on that path. With no barracks the player got a
generic "No troops yet - train troops at the Barracks" toast and a dead end, not a locked door.

This is the duplicated-state class the repo keeps getting burned by (stale WO number block,
retired asmdef table, inlined R2 push). **The fix makes the card read the existing predicate.
It does not add a second barracks check.**

---

## 3. What a destroyed structure looks like at runtime — it is GONE, not flagged

Read at source, `Assets/_Modules/Village/Vfx/Destructible.cs:150-193` (`NotifyBroken`):

1. VFX torn down (`TeardownVfx`).
2. Bound vendor NPC destroyed.
3. `PlacementGrid.Free(cell, footprint)`; `BaseLayoutLoader.Forget(placed)`;
   `RemovePersistedLayoutRecord(itemId, cell)` — **the persisted `GameState.BaseLayout` record
   is dropped**; `BurnFreeBuild(itemId)`; `StructureSingletonBootstrap.NotifyRemovedDeferred`.
4. `Destroy(gameObject)`.

So **the object does not persist in a destroyed state.** `Building.IsDestroyed`
(`Buildings/Building.cs:132`) is live only for the single frame between hp0 and that
`Destroy`, and `StructureSingleton.IsBuilt` clause 4 already requires `b.IsAlive`
(`BuildMode/StructureSingleton.cs:144`). There is no zombie for an existence check to trip on:
**`IsBuilt("barracks")` already flips false on destruction.** What it cannot do is say *why* —
"never had one" and "had one, lost it" are indistinguishable to it.

**Discriminator used: `GameState.HasEverBuilt("barracks")`** (`Core/State/GameState.cs:646`,
the v36 `everBuiltStructureIds` ledger, WO-834). It is **monotonic** — selling or losing a
structure never removes the id — which is exactly the "have you owned one before" question.
No new state, no schema bump.

### Two boundaries deliberately LEFT ALONE (both documented in-code)

- **Under construction / in the build queue counts as raid-capable.**
  `BuildModeController.Place` spawns the structure and appends its `BaseLayout` record
  (`BuildMode/BuildModeController.cs:2071`) **before** the build timer starts, so a barracks
  mid-build has always read `IsBuilt` and has always been raid-capable. Locking it would
  change the path the owner fenced as working. **Chosen: unchanged.**
  ⚠ **Flagged for the owner** — this is a genuine design call, not an obvious one. Ask:
  *should a barracks still under construction unlock raids, or only a finished one?*
- **A resurfaced WO-819 baked twin counts as raid-capable.** After a destruction the stand-in
  `CastleBarracks` re-activates, so `IsBuilt` clause 2 can hold while the build palette
  correctly reads BUILDABLE (`StructureSingleton.IsPlayerBuilt`, WO-843). Kept capable on
  purpose: a barracks is *visibly standing*, and locking raids in front of it is the more
  confusing outcome. **Do not "fix" this to `IsPlayerBuilt`** — on a pre-handover Default-Town
  save the founding barracks has no placement record yet, so `IsPlayerBuilt` would lock raids
  for a player who plainly has one.

**Net: the capable/not-capable boundary is bit-identical to before this ticket.** The method
gained an `out` parameter, never a clause. "It works great if there is a barracks" is untouched.

---

## 4. The change

### `Core/HudModel/PostureSignals.cs`
- New `PostureSignals.RaidLockReason { None, FlagOff, NoBarracks, BarracksLost }`.
- New `RaidLock` property + `RaidLockCopy(reason)` — **the one owner of the lock wording**,
  so card, any future surface, and the oracle read identical strings.
- `SetRaidCapable(bool capable, RaidLockReason reason = None)` — one method with an optional
  parameter (not an overload, so `HudActionBarRegression`'s `GetMethod("SetRaidCapable")`
  stays unambiguous). It now fires `RaidCapableChanged` on a **reason-only** change:
  `NoBarracks -> BarracksLost` never moves the bool, and an early-return on the bool alone
  would strand the wrong sentence on screen.

### `Village/Troops/RaidCapabilityHudBridge.cs`
- `ComputeCapable` emits the reason alongside the existing refuse line; the poll's edge test
  is now `(capable, reason)`.
- `FlowTrace.Step("Raid", ...)` now names the gate decision **and its reason**
  (`lock=NoBarracks` / `lock=BarracksLost`), so "why is this locked / why is it not" answers
  itself from a capture. `FlowTrace.Step("Navigation", "deck card 'Raids' LOCKED - <copy>")`
  fires on the render side.

### `HUD/PlayerDeckWorkspace.cs`
- `Card` gains `Func<string> LockReason`.
- `BuildCard` renders it in place of the generic "Complete its requirement first" purpose line
  (`Guard.Try`-wrapped; null falls back to the generic line).
- The Raids card: `Available = () => PostureSignals.RaidCapable`,
  `LockReason = () => PostureSignals.RaidLockCopy(PostureSignals.RaidLock)`.

### Locked presentation
The deck already had the right treatment and it is reused, not reinvented — the card stays
**visible**, grays, and gets the existing worded `[ LOCKED ]` badge on a dark plate
(`PlayerDeckWorkspace.cs:216-227`, authored for exactly this reason under WO-1311: the owner
is red/green colourblind, so hue and dimming may never be the only carrier). Hiding is **not**
the pattern for this deck, and WO-1008 already settled that a self-hiding raid door reads as
broken. This ticket adds the missing half: **the reason**.

| Situation | Badge | Line under the title |
|---|---|---|
| Barracks stands (any troop count) | none — card live | "Choose a camp and deploy your army" |
| Never built a Barracks | `[ LOCKED ]` | **"Build a Barracks to raid"** |
| Barracks destroyed / lost | `[ LOCKED ]` | **"Rebuild your lost Barracks to raid"** |
| `FeatureFlags.Raid` off | `[ LOCKED ]` | **"Raids are turned off in this build"** |

ASCII-only. Untouched geometry: touch target is the full grid cell (half the body band, well
over 112 px in landscape); `MeasureArtFit`, the `OpaqueMargins` table and the label styling
from today's earlier edits were read first and are **not** modified.

---

## 5. Oracle — `RaidsDiscoverabilityRegression` (extended, not a new suite)

New `CheckJourneyRaidsLock` ("J1"), registered in `Run`. **Both directions are pinned**, the
offerable one first, precisely because a fix that just locked the card always would pass a
one-sided "it locks" test while deleting a working feature:

1. capable -> door open, `RaidLock == None`, `RaidLockCopy(None)` empty.
2. `NoBarracks` -> not capable, copy non-empty, names the Barracks.
3. `BarracksLost` -> reason-only flip **raises the change event**, copy non-empty, names the
   Barracks, and is **different text** from the NoBarracks copy.
4. All three lines ASCII.
5. Source: the card gates on `PostureSignals.RaidCapable`, supplies a `LockReason`, `BuildCard`
   renders `spec.LockReason`, the `[ LOCKED ]` badge survives, and `PlayerDeckWorkspace`
   contains **no** `StructureSingleton` / `IsBuilt` of its own (the anti-drift assertion).

Live globals are snapshotted and restored in a `finally` so no later suite inherits a shut door.

**Also re-registered: `CheckVisibilityPredicateSource` (D5).** WO-1286 dropped three checks
from `Run` when it retired the bottom-bar Raids face; D1/D2/`CheckReasonsSpeakInWords` were
genuinely inverted by that change and stay unregistered on purpose, but **D5 lints the
visibility bridge, which WO-1286 never touched** — it has been dead code guarding nothing
since commit `486cd7b17`, and it is the guard that keeps the troop clause out of the very
predicate this ticket extends. Its preconditions were verified to hold at HEAD before
re-registering (`RaidsFaceLabel` x3 in `HudKitController`; every `ArmyReadiness` /
`DeployableSlots` occurrence in the bridge is inside a comment, and the lint strips comments).

### Mutation (RED proof)
⚠ **Designed, not executed** — this was an edit-only pass; no Unity gate was run (lead owns
the gate). The mutations to run:

| # | Mutation | Expected failure |
|---|---|---|
| M1 | Revert the card to `Available = () => true` | J1 source: "no longer gates on PostureSignals.RaidCapable" |
| M2 | Make `RaidLockCopy` return the same string for `NoBarracks` and `BarracksLost` | J1: "print the SAME line ... different situations with different remedies" |
| M3 | Restore the `if (RaidCapable == capable) return;` early-return in `SetRaidCapable` | J1: "NoBarracks -> BarracksLost raised no change event" |
| M4 | Drop `LockReason` from the card (keep the gate) | J1: "supplies no LockReason" |
| M5 | Lock the card unconditionally (`Available = () => false`) | J1 direction 1: "the working path the owner fenced off ... is broken" |

M5 is the one that matters: it is the fix that would pass a one-sided test.

---

## 6. Gate evidence (edit-only pass)

Brace / NUL check per touched file — `python -c "... count('{')==count('}') ... chr(0) ..."`:

| File | Result |
|---|---|
| `Assets/_Modules/Core/HudModel/PostureSignals.cs` | BALANCED clean |
| `Assets/_Modules/Village/Troops/RaidCapabilityHudBridge.cs` | BALANCED clean |
| `Assets/_Modules/HUD/PlayerDeckWorkspace.cs` | BALANCED clean |
| `Assets/Editor/Regression/RaidsDiscoverabilityRegression.cs` | BALANCED clean |

No Unity gate, no build, no commit — the lead owns all three.
No `.unity` scene touched. No `DataRegression.cs` edit (existing suite extended).
Fenced files (`HeartAuraController`, `EliteVFXController`, `HeldVfxHook`,
`NightStoreAuraSelectionRegression`, `HeroContent`, `tutorial-steps.json`, board tooling,
ship scripts, `PackStore.cs`, `VfxManualPicks.json`, `WorldHold.cs`, scenes) untouched.

---

## 7. For the owner (PO felt-verify + close)

1. With a barracks standing: open Journey -> RAIDS is live and behaves exactly as before.
2. On a fresh save with no barracks: RAIDS is visible, grayed, badged `[ LOCKED ]`, and reads
   **"Build a Barracks to raid"**.
3. After losing a barracks: same, but it reads **"Rebuild your lost Barracks to raid"**.
4. **One question for you:** should a barracks that is still *under construction* unlock raids?
   Today it does (unchanged), because the structure and its record exist the moment it is
   placed. Say the word and it becomes finished-only.
