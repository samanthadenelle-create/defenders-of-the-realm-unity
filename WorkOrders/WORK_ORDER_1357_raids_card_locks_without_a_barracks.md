# WORK ORDER 1357 â€” Journey RAIDS card locks (with a reason) when there is no barracks

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:37:27, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - ON HER DEVICE. The Journey RAIDS card reads the ONE existing `RaidCapable` predicate instead of `Available = () => true`, and says WHICH barracks problem it is - "Build a Barracks to raid" vs "Rebuild your lost Barracks to raid" vs the flag-off line - discriminated by the v36 `HasEverBuilt` ledger with no new state and no schema bump. A destroyed structure is GONE entirely rather than flagged, so detection was never the gap; saying why was. Her locked-state art is mounted (cropped, de-texted and alpha-restored by the CLI), and the engine's DOUBLE gray-wash on locked illustrated cards is dropped so her darkened padlocked scene is not buried - measured contrast 9.05:1 title / 10.21:1 reason / 15.91:1 badge. Two boundaries flagged not chosen: a barracks under construction still unlocks raids, and a resurfaced baked twin stays capable. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. AWAITING HER FELT-VERIFY on the Journey panel; then Owner Validation closes it.
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

## 2. RCA â€” one rule, two surfaces, one of them ignoring it

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
only** â€” there is no barracks check anywhere on that path. With no barracks the player got a
generic "No troops yet - train troops at the Barracks" toast and a dead end, not a locked door.

This is the duplicated-state class the repo keeps getting burned by (stale WO number block,
retired asmdef table, inlined R2 push). **The fix makes the card read the existing predicate.
It does not add a second barracks check.**

---

## 3. What a destroyed structure looks like at runtime â€” it is GONE, not flagged

Read at source, `Assets/_Modules/Village/Vfx/Destructible.cs:150-193` (`NotifyBroken`):

1. VFX torn down (`TeardownVfx`).
2. Bound vendor NPC destroyed.
3. `PlacementGrid.Free(cell, footprint)`; `BaseLayoutLoader.Forget(placed)`;
   `RemovePersistedLayoutRecord(itemId, cell)` â€” **the persisted `GameState.BaseLayout` record
   is dropped**; `BurnFreeBuild(itemId)`; `StructureSingletonBootstrap.NotifyRemovedDeferred`.
4. `Destroy(gameObject)`.

So **the object does not persist in a destroyed state.** `Building.IsDestroyed`
(`Buildings/Building.cs:132`) is live only for the single frame between hp0 and that
`Destroy`, and `StructureSingleton.IsBuilt` clause 4 already requires `b.IsAlive`
(`BuildMode/StructureSingleton.cs:144`). There is no zombie for an existence check to trip on:
**`IsBuilt("barracks")` already flips false on destruction.** What it cannot do is say *why* â€”
"never had one" and "had one, lost it" are indistinguishable to it.

**Discriminator used: `GameState.HasEverBuilt("barracks")`** (`Core/State/GameState.cs:646`,
the v36 `everBuiltStructureIds` ledger, WO-834). It is **monotonic** â€” selling or losing a
structure never removes the id â€” which is exactly the "have you owned one before" question.
No new state, no schema bump.

### Two boundaries deliberately LEFT ALONE (both documented in-code)

- **Under construction / in the build queue counts as raid-capable.**
  `BuildModeController.Place` spawns the structure and appends its `BaseLayout` record
  (`BuildMode/BuildModeController.cs:2071`) **before** the build timer starts, so a barracks
  mid-build has always read `IsBuilt` and has always been raid-capable. Locking it would
  change the path the owner fenced as working. **Chosen: unchanged.**
  âš  **Flagged for the owner** â€” this is a genuine design call, not an obvious one. Ask:
  *should a barracks still under construction unlock raids, or only a finished one?*
- **A resurfaced WO-819 baked twin counts as raid-capable.** After a destruction the stand-in
  `CastleBarracks` re-activates, so `IsBuilt` clause 2 can hold while the build palette
  correctly reads BUILDABLE (`StructureSingleton.IsPlayerBuilt`, WO-843). Kept capable on
  purpose: a barracks is *visibly standing*, and locking raids in front of it is the more
  confusing outcome. **Do not "fix" this to `IsPlayerBuilt`** â€” on a pre-handover Default-Town
  save the founding barracks has no placement record yet, so `IsPlayerBuilt` would lock raids
  for a player who plainly has one.

**Net: the capable/not-capable boundary is bit-identical to before this ticket.** The method
gained an `out` parameter, never a clause. "It works great if there is a barracks" is untouched.

---

## 4. The change

### `Core/HudModel/PostureSignals.cs`
- New `PostureSignals.RaidLockReason { None, FlagOff, NoBarracks, BarracksLost }`.
- New `RaidLock` property + `RaidLockCopy(reason)` â€” **the one owner of the lock wording**,
  so card, any future surface, and the oracle read identical strings.
- `SetRaidCapable(bool capable, RaidLockReason reason = None)` â€” one method with an optional
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
The deck already had the right treatment and it is reused, not reinvented â€” the card stays
**visible**, grays, and gets the existing worded `[ LOCKED ]` badge on a dark plate
(`PlayerDeckWorkspace.cs:216-227`, authored for exactly this reason under WO-1311: the owner
is red/green colourblind, so hue and dimming may never be the only carrier). Hiding is **not**
the pattern for this deck, and WO-1008 already settled that a self-hiding raid door reads as
broken. This ticket adds the missing half: **the reason**.

| Situation | Badge | Line under the title |
|---|---|---|
| Barracks stands (any troop count) | none â€” card live | "Choose a camp and deploy your army" |
| Never built a Barracks | `[ LOCKED ]` | **"Build a Barracks to raid"** |
| Barracks destroyed / lost | `[ LOCKED ]` | **"Rebuild your lost Barracks to raid"** |
| `FeatureFlags.Raid` off | `[ LOCKED ]` | **"Raids are turned off in this build"** |

ASCII-only. Untouched geometry: touch target is the full grid cell (half the body band, well
over 112 px in landscape); `MeasureArtFit`, the `OpaqueMargins` table and the label styling
from today's earlier edits were read first and are **not** modified.

---

## 5. Oracle â€” `RaidsDiscoverabilityRegression` (extended, not a new suite)

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
visibility bridge, which WO-1286 never touched** â€” it has been dead code guarding nothing
since commit `486cd7b17`, and it is the guard that keeps the troop clause out of the very
predicate this ticket extends. Its preconditions were verified to hold at HEAD before
re-registering (`RaidsFaceLabel` x3 in `HudKitController`; every `ArmyReadiness` /
`DeployableSlots` occurrence in the bridge is inside a comment, and the lint strips comments).

### Mutation (RED proof)
âš  **Designed, not executed** â€” this was an edit-only pass; no Unity gate was run (lead owns
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

Brace / NUL check per touched file â€” `python -c "... count('{')==count('}') ... chr(0) ..."`:

| File | Result |
|---|---|
| `Assets/_Modules/Core/HudModel/PostureSignals.cs` | BALANCED clean |
| `Assets/_Modules/Village/Troops/RaidCapabilityHudBridge.cs` | BALANCED clean |
| `Assets/_Modules/HUD/PlayerDeckWorkspace.cs` | BALANCED clean |
| `Assets/Editor/Regression/RaidsDiscoverabilityRegression.cs` | BALANCED clean |

No Unity gate, no build, no commit â€” the lead owns all three.
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

---

# FOLLOW-UP - 2026-09-03 (later the same day): the owner's LOCKED face for the RAIDS card

**Status:** FIXED 2026-09-03 - ON HER DEVICE. The Journey RAIDS card reads the ONE existing `RaidCapable` predicate instead of `Available = () => true`, and says WHICH barracks problem it is - "Build a Barracks to raid" vs "Rebuild your lost Barracks to raid" vs the flag-off line - discriminated by the v36 `HasEverBuilt` ledger with no new state and no schema bump. A destroyed structure is GONE entirely rather than flagged, so detection was never the gap; saying why was. Her locked-state art is mounted (cropped, de-texted and alpha-restored by the CLI), and the engine's DOUBLE gray-wash on locked illustrated cards is dropped so her darkened padlocked scene is not buried - measured contrast 9.05:1 title / 10.21:1 reason / 15.91:1 badge. Two boundaries flagged not chosen: a barracks under construction still unlocks raids, and a resurfaced baked twin stays capable. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. AWAITING HER FELT-VERIFY on the Journey panel; then Owner Validation closes it.
**Silo:** HUD / Raids
**Files:** `Assets/_Modules/HUD/PlayerDeckWorkspace.cs`,
`Assets/Editor/Regression/HudLabelFitRegression.cs`,
`Assets/Resources/UI/ElarionMedieval/cards/raids-locked.png` (+ new `.meta`)

*No new WO number was minted: this is the same card, the same ruling, the same day. The
lock predicate, the reason strings and `PostureSignals` are UNTOUCHED - WO-1357 shipped
and is on the owner's device.*

## F1. What landed

The owner supplied art for the LOCKED state only:
`Assets/Resources/UI/ElarionMedieval/cards/raids-locked.png`, 1416x742 RGBA, aspect 1.91,
alpha bbox (3,7)-(1410,736). A dark war camp on the LEFT, a stone-and-steel padlock
medallion at centre-left, and the right ~45% left as a deliberately EMPTY plate.

Before this follow-up the Journey panel rendered `[ LOCKED ] / RAIDS / "Rebuild your lost
Barracks to raid"` over a plate with no illustration, while QUESTS beside it showed its
artwork. Now the locked card carries her face and QUESTS' treatment.

## F2. Why the plate is empty - and why it must stay that way

The reason the card is shut is DYNAMIC (never had a Barracks / lost one / flag off), so it
can only be live TMP text. Two earlier exports carried a generic line baked into exactly
that plate; the owner was offered the choice and chose the wordless re-generate so the live
copy wins. That is settled.

It is also the WO-1341 defect class: the Hero deck printed every label twice because the
words were painted into the PNGs. So the guard is not a comment - see F8, case 9b, which
OPENS the file and measures the plate.

## F3. Where it is mounted

| What | Where |
|---|---|
| `Card.LockedArtKey` field (new) | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:27-37` |
| Locked-face selection in `BuildCard` | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:123-142` |
| Gray-wash dropped for an authored locked face (surface) | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:157-165` |
| Gray-wash dropped for it again (`colors.disabledColor`) | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:187-192` |
| `LockedArtKey = "raids-locked"` on the Journey RAIDS card | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:610-620` |
| Importer settings for the new PNG | `Assets/Resources/UI/ElarionMedieval/cards/raids-locked.png.meta` |

The art key is chosen in ONE place:

    bool authoredLockFace = !available && !string.IsNullOrEmpty(spec.LockedArtKey);
    string artKey = authoredLockFace ? spec.LockedArtKey : spec.ArtKey;

Everything downstream (`Resources.Load`, `ResolveArtFit`, the fit cache) reads `artKey`, so
`"raids"` and `"raids-locked"` are separate cache entries and cannot cross-contaminate.
If the locked sprite fails to load, a `FlowTrace.Warn` names it and the card falls back to
`ArtKey` - never silently, because an inviting camp behind a `[ LOCKED ]` badge is a lie.

Precedent followed: `quests.png` is mounted through the same illustrated-card branch, and
`cards/troops-locked` already establishes the `-locked` art-key naming
(`ManageScreenPanel.cs:696`).

**The unlocked path is untouched.** With `available == true`, `authoredLockFace` is false,
`artKey == spec.ArtKey`, the surface colour is `Color.white` as before and `disabledColor`
takes its original literal. The diff is reachable only while the card is locked.

## F4. Two things this follow-up deliberately changed, and why

1. **`.meta` authored, not left to Unity.** The delivery arrived without one. A default
   import is not a Sprite and has no tight mesh, so `Resources.Load<Sprite>` returns null at
   runtime and the card would have rendered empty again. The meta is a byte-for-byte clone of
   `quests.png.meta` (`textureType: 8`, `spriteMode: 1`, `spriteMeshType: 1` Tight,
   `alphaIsTransparency: 1`, `isReadable: 0`) with a fresh guid
   `c10b81658d1240fd88d270d462ff1970`. Verified: `diff` against `quests.png.meta` with the
   guid line removed is EMPTY.
2. **The runtime gray tint is dropped for an authored locked face, in both places.** A locked
   illustrated card was washed twice - once on `IllustratedCardSurface`
   (`.48,.48,.50,.82`) and once by the Selectable, which multiplies `targetGraphic` (the art)
   by `colors.disabledColor` (`.46,.46,.48,.82`) because `button.interactable` is false. That
   wash is the stand-in for "no locked art exists". Her face IS the darkened, padlocked scene,
   and washing it again only costs the live text contrast on a near-black plate. The owner is
   red/green colourblind, so locked-ness never rested on hue anyway: it reads from the padlock
   (shape), the `[ LOCKED ]` badge and the remedy line (words). Both washes still apply to
   every card that has no `LockedArtKey`.

## F5. The fit machinery - measured route, NO table row

`PlayerDeckWorkspace.MeasureArtFit` derives the packaging margin from the sprite's
alpha-built tight mesh, with `OpaqueMargins` as the fallback for exports whose margin is
opaque. Measured off the delivered file, Case-7 style (alpha <= 8 is transparent, mean
channel >= 170 is pale packaging):

    alpha margin  L3 T7 R6 B6      ink margin  L3 T7 R6 B6      (1416x742)
    transparentMargin = TRUE  ->  opaqueMargin = FALSE  ->  no row wanted

So the alpha route owns this card and **no `OpaqueMargins` row was added**, which is what
that table's own comment demands ("a row is a claim that the PNG's border pixels are
packaging"). `HudLabelFitRegression` Case 7 already walks every PNG in the card directory
that the deck source mentions, so `raids-locked` is now inside its sweep for free and will
FAIL if a future re-export loses its alpha.

Either mesh outcome is correct here: a 3px margin resolved gives `Corrected = true` and a
~0.6% zoom under a `RectMask2D`; a mesh that snaps back to the full rect gives `IdentityFit`
and renders 1:1 with a 3px transparent edge. Neither crops artwork.

## F6. Contrast finding - the live text READS on this plate

Measured over the illustrated card's text plate (x 0.49..0.96, y 0.20..0.86 - the band the
title and reason occupy), WCAG 2.x relative luminance:

    mean plate luminance   0.0051  (near-black, as expected)
    light-pixel fraction   0.0017  (0.17% - the padlock rim clipping the plate's left edge)

    ElarionUi.Gold         (title, 36px)         9.05 : 1
    ElarionUi.ParchmentDim (lock reason)        10.21 : 1
    ElarionUi.Parchment    ([ LOCKED ] badge)   15.91 : 1

All three clear WCAG AA (4.5:1) with a wide margin - comfortably, because the plate is dark
and every deck text tone is light. **No colour change is needed and none was guessed at.**
This holds only because the double gray wash was dropped (F4.2); with it, the art under the
text was being lifted toward mid-gray and the ratios would have collapsed.

## F7. Lock medallion does NOT overlap the live text

Column-luminance profile of the delivered face: the illustration and medallion occupy roughly
x 0.00..0.50, and inside the title/purpose bands the only pixels above the glyph threshold sit
in x **0.489..0.540** - the medallion's outer rim just kissing the plate's left edge, 0.17% and
0.11% of those bands respectively. Both live labels are CENTRED in the 0.49..0.96 plate
(centre 0.725), and "RAIDS" at 36px plus a one-line reason at FontMicro come nowhere near
x 0.54. No overlap at the current card size (2-column grid, half-cell each).

## F8. The oracle - `HudLabelFitRegression` Case 9 `[raids-locked-face]`

Extended the existing suite (`DataRegression.cs` is fenced; `HudLabelFitRegression` already
owns the deck-card precedent as Cases 6 and 7). Registered at
`Assets/Editor/Regression/HudLabelFitRegression.cs:176`; body appended near the end of the
same file.

**9a - the wiring (source lint, 5 assertions).** `LockedArtKey = "raids-locked"` is declared;
`ArtKey = "raids"` still is (the unlocked path may not be collateral damage); the locked face
is gated on `!available`; and NEITHER wash may come back
(`(available || authoredLockFace) ? Color.white`, `colors.disabledColor = authoredLockFace`).

**9b - the plate is EMPTY (opens the PNG).** Decodes the file with `Texture2D.LoadImage`, so
`isReadable: 0` is irrelevant, and measures the fraction of plate pixels light enough to be a
glyph. This is the honest half: Case 6 can only ban art keys somebody already knew were bad,
whereas this fails on a re-delivery with words on it that nobody has listed yet.

**9c - the live text still reads.** Computes WCAG contrast of `Gold`, `ParchmentDim` and
`Parchment` against the measured plate luminance, floor 4.5:1. This is the check the source
cannot make at all.

### The ink ceiling is CALIBRATED, not guessed

Ran the measurement against the delivered face and against the same face with a parchment-toned
line baked back into the plate at several sizes:

| plate content | light-pixel fraction |
|---|---|
| **delivered (clean)** | **0.0017** |
| baked line, 28px | 0.0077 |
| baked line, 40px | 0.0139 |
| baked line, 72px | 0.0278 |
| baked title + subtitle | 0.0255 |

`PlateInkCeiling = 0.006` sits 3.5x above the clean face and below the smallest line anyone
would author on a 1416px-wide card. *(A first draft used 0.02 and a 28px bake slipped under it
at 0.0077 - the loose ceiling was caught by running the mutation, not by reading the number.)*

### PROVEN RED - the mutations

Ran a harness replicating Case 9's exact predicates and thresholds against the live tree and
against mutated copies:

    9a live tree          GREEN
    9a mutant: LockedArtKey = "raids-locked" -> null
                          RED  -> "the Journey RAIDS card declares no LockedArtKey"
    9a mutant: (available || authoredLockFace) ? Color.white -> available ? Color.white
                          RED  -> "the authored locked face is being gray-washed again"
    9b live tree          GREEN  ink 0.0017 <= 0.0060
    9b mutant: bake "Unlock to access raid battles" into the plate at 28px
                          RED  -> ink 0.0077 > 0.0060
    9c live tree          GREEN  9.05 / 10.21 / 15.91 vs floor 4.50

The 9a "no LockedArtKey" mutation is the headline one: it is exactly the state of the tree
before this follow-up, and the case names the card from the source.

## F9. Gate evidence (edit-only)

| File | Brace / NUL check |
|---|---|
| `Assets/_Modules/HUD/PlayerDeckWorkspace.cs` | BALANCED clean |
| `Assets/Editor/Regression/HudLabelFitRegression.cs` | BALANCED clean |

All added lines are ASCII-only (verified against the diff). No Unity gate, no build, no
commit - the lead owns all three. No `.unity` scene touched. No `DataRegression.cs` edit.
`PostureSignals`, `RaidCapabilityHudBridge`, the lock predicate and the reason strings are
untouched. The owner's PNG was not recoloured, restyled or re-edited.

**For the lead:** `raids-locked.png` and `raids-locked.png.meta` are UNTRACKED - stage both
by explicit path with the two `.cs` files, or the build ships a null sprite.

## F10. For the owner (PO felt-verify + close)

1. On a save with no barracks: open Journey. RAIDS now shows the dark war camp and the stone
   padlock on the left, with `[ LOCKED ]`, `RAIDS` and `Build a Barracks to raid` as live text
   on the right - same left-art / right-text shape as QUESTS beside it.
2. Build a barracks: RAIDS returns to its normal face and opens as before.
3. The card is no longer dimmed by the engine on top of the art. If it now reads as *less*
   locked than you want, say so and the badge/plate gets more weight - the fix will not be a
   darker tint.
