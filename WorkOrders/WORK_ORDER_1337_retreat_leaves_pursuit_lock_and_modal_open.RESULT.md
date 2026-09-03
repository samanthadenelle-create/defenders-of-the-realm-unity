# WO-1337 RESULT — A retreat leaves the battle-lock held by the pursuit probe, and a panel handle open

**Status:** FIXED (edit-only; NOT gated, NOT committed, NOT built — the lead gates and commits)
**Date:** 2026-09-03
**Silo:** Combat / Quiescence
**Closure:** the owner felt-verifies and CLOSES (headless cannot judge feel).

---

## Which invariant, and the proving line for each

### 1. battle-lock — FIXED AT SOURCE

**Proving line** (`logs/f8-inbox/capture-device-20260903-111146-seq4677.md`, device
`SM02G4061955851`, build `2026.09.03.353593`, `Main_Castle_Overworld`):

```
- battle-lock: still HELD after the battle ended. … HOLDER(S): PursuitBattleProbe.Probe
  (of 3 registered: PursuitBattleProbe.Probe, BattleArena.<Awake>b__84_0, WaveManager.<OnEnable>b__116_0).
```

Read against seq 4664/4675, which named `[PursuitBattleProbe.Probe, WaveManager…b__116_0]`: **the
wave holder is GONE. WO-1308's unwind works and was not touched.** The probe therefore does reach
`BattleSessionEnd.Release` — and **re-latches after it**, which is the second of the two shapes the
ticket asked to be told apart.

**Root cause (2 sentences).** An enemy raises the battle-lock through TWO owners — the
`HeroCombatEngagement` token and the pursuit pulse it stamps every `DriveNav` tick — and
`Enemy.OnDisable` released only the first, while `PostureSignals.RevokePursuit` had exactly one
caller, `Die()`. The arena's retreat teardown removes survivors with `Destroy(e.gameObject)` **0.35 s
after** the session end (`HomeFadeOutSeconds`, under the masked-return fade) and never reaches
`Die()`, so their last pulse stayed live to `0.35 + PursuitTtl(1.5) = 1.85 s` while the gate judges a
retreat at `SettleSeconds = 0.75 s` — deterministic, on constants that live in the tree, and exactly
what **WO-1233's own header already recorded**: *"the RETREAT case fails deterministically"*.

### 2. modal — a DIFFERENT owner; NOT the same cause

**Proving line** (same capture):

```
- modal: a panel handle is STILL OPEN after the reward screen closed. The world interact
  button stays suppressed underneath and the back button targets a panel the player cannot see.
```

**They do not share a cause.** They share one *precondition*: a retreat announces the session end
**synchronously** and does its teardown **asynchronously behind the fade**. The battle-lock half is a
pulse outliving its body; the modal half is the arm site denying that a retreat has an end-state
screen at all.

A retreat **does** present one — `_pendingLossBanner` → `BattleArenaHud.ShowResult` →
`EndStateView.Show` → `PanelManager.NotifyOpened("EndState")` — deferred to arrival at the end of
`ReturnHomeWithFade` (the t=397 "two death screens" fix, which must not be moved back). The arm site
passed `null` for the reward-screen probe on `!won`, so the gate started settling immediately and
judged the modal invariant straight through that screen's own open. The gate's own header names the
rule this broke: *"an open reward screen is correct behaviour, and failing on it is the fastest way to
teach everyone to ignore this gate."*

⚠ **What the capture CANNOT prove, and was therefore not "fixed":** which panel was open. The modal
finding named none — the identical attribution gap WO-1233 closed for the battle-lock. §12 forbids
editing on a theory about the answer, so this ticket did the two things provable without one: closed
the structural false-positive above, and made the finding **name the panel and say whether it is
visible or an invisible ghost**.

**The `STUCK WORLD HOLD: 'pause-menu' outstanding for 8444s` line and the photographed settings
panel:** that is a `WorldHold` reason — a different subsystem from the `PanelManager` arbiter — and
nothing in seq 4677 links them. **UNPROVEN, deliberately not fixed.** The named-holder message
settles it in one read on the next capture; if it comes back `HOLDER: 'Settings' …`, it is the
recurring leak and belongs to whoever opened it.

---

## What changed

### `Assets/_Modules/Village/Enemies/Enemy.cs` — THE FIX

`OnDisable` now also calls `PostureSignals.RevokePursuit(GetInstanceID())`, alongside the
`HeroCombatEngagement` release that was already there. Deliberately **in the body, not in the arena's
despawn loop**: the pulse is keyed by this instance id, so this body is its only honest owner, and
`OnDisable` covers all three removal paths at once (Destroy, pool release, scene unload) instead of
adding an Nth per-caller release. `Die()`'s revoke is **kept** — death revokes immediately so town
chrome returns as the last threat dies, rather than waiting out the corpse's death hold. Idempotent
(`RevokePursuit` no-ops on an absent key).

⚠ **It cannot suppress a real chase.** Pursuit is PULSE-based: a live chaser re-stamps on its next
`DriveNav` tick, so this drops only the pulse of a body that is gone, and a disabled enemy is not
chasing anyone.

### `Assets/_Modules/Village/Arena/BattleArena.cs` — the false-positive hole

The retreat arm's `: null` became `: () => _pendingLossBanner != null || EndStateView.IsShowing`.
**Pending-OR-showing, not just `IsShowing`**: at arm time the banner has not opened yet, so a bare
`IsShowing` reads false, the wait loop exits on its first poll, and the settle judges through the
banner's open — the false finding intact. `_pendingLossBanner` is nulled in the same statement block
that invokes it with **no yield between**, so no poll can observe the handover gap.

This does **not** weaken the gate: a defeat banner that never closes is still reported, because
`Arm`'s `ModalWaitCapSeconds` (60 s) breaks the wait and marks the verdict `cappedOut`.

### `Assets/_Modules/Core/UI/PanelManager.cs` — attribution

Two additive read-only accessors (the handle's `IsOpen` probe was `internal`, and the gate lives in
Core): `OpenPanelSelfReportsOpen` (`bool?`) and `DescribeOpen()`. No behaviour change; the handle
itself is still not exposed. The probe read is `Guard.Try`-wrapped — it is the caller's code.

### `Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs` — name it, then heal it

- The modal finding's original sentence is preserved **verbatim** and `HOLDER: …` is **appended** —
  the same strengthening WO-1233 applied to the battle-lock finding. It fires on exactly the
  condition it always did.
- **Modal self-heal**, ordered after the lock heal and before the blunt `timeScale` write (so
  attributable recovery keeps first refusal):
  - probe says **NOT open** → a proven **ghost** (WO-465 invisible-scrim class). Nothing is on
    screen, so there is nothing for the player to dismiss and no way out; this **is** the softlock.
    Healed via `PanelManager.CloseAll`, i.e. through the panel's **own `Close` action** — never by
    zeroing the arbiter's record — mirroring how the lock heal re-drives the owners' own unwinds.
    Reports before and after, and says loudly if `CloseAll` failed to clear it.
  - probe says **VISIBLE** (or no probe) → a real panel is on screen and hers to dismiss, so it is
    **reported by name and left alone**. Force-closing here would yank a live screen (the pause menu
    over a just-ended fight) out from under her. That is now enough, because the finding names it.
- Detection logic, thresholds and the `BattleLock` probe list are **untouched**; no `timeScale` was
  written; no release call was added.

### `Assets/Editor/Regression/BattleQuiescenceRegression.cs` — the oracle (extended, no second suite)

Four cases, registered in `Run` and cleaned up in the existing `finally` (which now also calls
`PanelManager.CloseAll`, because the arbiter is a global too):

1. **`RetreatSurvivorPulseDoesNotOutliveTheBody`** — behavioural, both directions, over the real Core
   statics. Reproduces the retreat timeline: `Release` clears the ring, a survivor still alive behind
   the fade re-stamps, and **(a)** that un-revoked pulse must still HOLD the lock (the captured
   defect — asserting it is what stops the test passing for the wrong reason, and it fails loudly if
   the reproduction ever stops reproducing), while **(b)** the body revoking its own pulse releases
   the lock immediately. Nothing force-clears `BattleLock`.
2. **`DespawnRevokesPursuitAtSource`** — source-lint on the real owner: the revoke is reachable from
   `Enemy.OnDisable` (bounded 3000-char window, so a match in another member proves nothing), it
   still exists in **two** places (dropping `Die()`'s delays peaceful chrome by the whole death hold;
   dropping `OnDisable`'s restores this defect), and `PursuitBattleProbe`'s predicate is intact.
3. **`RetreatClosesEveryPanelHandle`** — behavioural. A handle opens honestly and then stops being
   open without calling `NotifyClosed` (the real shape of a ghost, and it keeps the case from
   emitting `NotifyOpened`'s own invisible-scrim `LogError` as setup noise). Asserts the finding
   fires, keeps its original sentence, **names** the panel, says GHOST, and that `CloseAll` clears it
   **through the panel's own Close action** (a cleared record over a panel that still thinks it is
   open is a new bug, not a heal). Then a **VISIBLE** panel must still FAIL the invariant and be
   reported as visible — the ghost discrimination decides the HEAL, never the FINDING.
4. **`RetreatWaitsOutItsOwnDefeatBanner`** — source-lint on the two wiring halves: the gate heals a
   ghost and **only** a ghost, and the retreat arm no longer hands the gate a bare `null`.

---

## Oracle mutation — proven RED

Unity was **not** run (out of scope for this seat, per the work order). The suite's `ReadCode` and
the six new lint rules were replicated byte-identically in Python and run against real mutants
(`HEAD` extracted with `git show`):

| Subject | Verdict | rules that fail |
|---|---|---|
| **`HEAD` (the shipped defect)** | **RED (5)** | enemy-ondisable-revokes, enemy-revokes-twice, gate-heals-ghost, gate-spares-visible, retreat-waits-banner |
| working tree (fixed) | **GREEN** | — |
| mutant: `PursuitBattleProbe` weakened to `false` | **RED (1)** | probe-intact |
| mutant: revoke only in `Die()` (the defect, re-introduced) | **RED (2)** | enemy-ondisable-revokes, enemy-revokes-twice |
| mutant: revoke only in `OnDisable` (`Die()`'s dropped) | **RED (1)** | enemy-revokes-twice |
| mutant: gate closes ANY panel (blunt heal) | **RED (1)** | gate-spares-visible |
| mutant: retreat arm back to a bare `null` | **RED (1)** | retreat-waits-banner |

Every rule is discriminating: `HEAD` fails five, and the sixth is proven by the weakened-probe
mutant. ⚠ **One rule was caught mid-authoring by this exercise and tightened:** `probe-intact`
originally matched the bare member name `PostureSignals.PursuitActive` and read **GREEN** against the
weakened probe, because `ReadCode` strips comments but **keeps string-literal contents** and that
file logs its own member name in an install message (the suite header's claim that literals are
blanked is wrong). It now matches the assignment `bool active = PostureSignals.PursuitActive;`.

The behavioural halves are self-mutating by construction — case 1 branch (a) *asserts* the
un-revoked pulse still holds the lock, and case 3 *asserts* a visible panel still fails the invariant
— so a force-clear or a blunt heal fails them.

⚠ **For the lead:** the behavioural halves have not been **executed**. They need one
`DeNelle.Editor.BattleQuiescenceRegression.RunAll` on the gate run (marker
`BATTLE_QUIESCENCE_SUITE_OK`).

---

## Brace / NUL check (per file)

| File | Result |
|---|---|
| `Assets/_Modules/Village/Enemies/Enemy.cs` | BALANCED, clean |
| `Assets/_Modules/Village/Arena/BattleArena.cs` | BALANCED, clean |
| `Assets/_Modules/Core/UI/PanelManager.cs` | BALANCED, clean |
| `Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs` | BALANCED, clean |
| `Assets/Editor/Regression/BattleQuiescenceRegression.cs` | BALANCED, clean |

Edited lines were re-read after each edit (a brace check does not catch a missing semicolon).

## Acceptance criteria

1. A retreat releases every battle-lock holder — the pursuit pulse dies with the body that stamped
   it, 1.5 s before the TTL would have. ✔ (needs the owner's felt-test to close)
2. A retreat during a GENUINE chase still holds the lock — pulse-based re-stamping is untouched, and
   `LiveChaseIsNotSuppressed` (WO-1233) plus the intact-probe rule assert both directions. ✔
3. A retreat's own defeat banner is no longer reported as a leak, and a banner that never closes
   still is (60 s cap → `cappedOut`). ✔
4. A stuck GHOST handle now heals through its own door; a visible panel is named, not yanked. ✔
5. `BATTLE_QUIESCENCE_FAIL (retreat)` no longer fires — **needs a captured device run**; PO closes.
6. Nothing was silenced: every existing `FlowTrace` line is intact and four new ones were added. ✔

## Deliberately NOT touched / left open

- ⛔ `WaveManager`'s WO-1308 unwind, its lock probe and its `OnDisable` standdown — **proven working
  by this very capture** (its holder is absent from the list). Not re-fixed, not re-litigated.
- ⛔ `BattleQuiescenceGate`'s detection logic, thresholds and both findings' original sentences —
  strengthened by appending only.
- ⛔ `BattleLock` — no probe registered, unregistered, or forced. `PursuitBattleProbe`'s predicate is
  byte-identical.
- ⛔ The `timeScale` lane (`WorldHold`, `HitStopManager`, `CombatFeedbackManager`,
  `HeroHitReaction`, `WaveCelebrationManager`) — a separate owner; **no clock was written**.
- ⛔ `Assets/HeroContent` + hero importer metas (live decimation lane), the store lane (`PackStore`,
  `NightMarket*`, `packs.json`, `canon-strings.json`, `hud-areas.json`), `PetHeroLeash`, and the
  payload/Addressables files.
- **OPEN, and left open on purpose:** which panel held the arbiter in seq 4677, and whether it is the
  same handle as the photographed settings panel / the 8444 s `'pause-menu'` `WorldHold`. Two
  different subsystems, no capture linking them, and §12 forbids a fix on the theory. The next
  capture names it.
- The masked-return ORDERING (teardown behind the fade while the session end is announced
  synchronously) is the shared precondition of both findings and was **not** restructured: it is the
  t=397 double-death-screen fix and the WO-969 stranding fix, and nothing in this capture proves it
  wrong. Both defects were closable at their own owners without moving it.

## Also updated

- `docs/MASTER_CATALOG/core.md` — two rows next to the battle-lock sources: that `Enemy.OnDisable`
  releases **both** of an enemy's lock claims, and the new `PanelManager` attribution accessors
  (CLAUDE.md §15: canon moves with the change).
