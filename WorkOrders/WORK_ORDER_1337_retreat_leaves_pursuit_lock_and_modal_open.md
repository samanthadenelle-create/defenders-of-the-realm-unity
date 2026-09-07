# WORK ORDER 1337 — A retreat leaves the battle-lock held by the pursuit probe, and a panel handle open

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:50:10, build 2026.09.07.358574). PRIOR STATUS: FIXED — the pursuit pulse now dies with the body that stamped it (`Enemy.OnDisable`), and the retreat arm stops telling the gate it has no end-state screen; the modal finding names its panel and heals a proven ghost. Owner felt-verifies and CLOSES.
**Silo:** Combat / Quiescence
**Severity:** P0 — player-facing softlock. Combat input stays suppressed, the HUD cannot return to its town context, and the world interact button is dead under an invisible panel.
**Origin:** Owner felt-test on device, 2026-09-03.
**Provenance:** minted 1337 from the `CLI_LANES_WO_NUMBERS.md` banner (banner bumped in the same edit; 1338 already on disk).

---

## PROOF — captured, not theorised

`logs/f8-inbox/capture-device-20260903-111146-seq4677.md` — device `SM02G4061955851`,
scene `Main_Castle_Overworld`, build `2026.09.03.353593`, stack
`DeNelle.Core.Combat.<Arm>d__13:MoveNext()`:

```
[Flow:Quiescence]   BATTLE_QUIESCENCE_FAIL (retreat) - 2 invariant(s) NOT restored after the battle:
  - battle-lock: still HELD after the battle ended. Combat input stays suppressed and the HUD
    cannot return to its town context. HOLDER(S): PursuitBattleProbe.Probe
    (of 3 registered: PursuitBattleProbe.Probe, BattleArena.<Awake>b__84_0, WaveManager.<OnEnable>b__116_0).
  - modal: a panel handle is STILL OPEN after the reward screen closed. The world interact
    button stays suppressed underneath and the back button targets a panel the player cannot see.
```

### ⭐ THE HOLDER LIST IS THE MOST IMPORTANT FACT IN THIS TICKET

Earlier captures on this same failure (seq 4664, 4675) named **TWO** holders —
`PursuitBattleProbe.Probe, WaveManager.<OnEnable>b__116_0`. This one names **only the probe**.

**The WaveManager unwind that landed under WO-1308 WORKS. Do not re-fix it, and do not read this
as a regression of it.** One owner now releases correctly; one remains, and it arrived through a
different door.

---

## RCA

### Invariant 1 — battle-lock (the real defect)

`PursuitBattleProbe` was fixed once, for the ARENA-WIN path, under WO-1233: the root then was
`PostureSignals.ClearPursuits` having a single caller on scene load while the arena stages in-place.
That fix is intact and it is not the shape here.

**The retreat DOES reach the release seam, and the probe RE-LATCHES afterwards.**

`BattleArena.Resolve` announces the session end synchronously and `BattleSessionEnd.Release` clears
the whole pursuit ring there (t=0). But the arena's **SURVIVORS are not torn down at t=0**: Resolve
captures them into `capturedSurvivors` and hands them to `ReturnHomeWithFade`, which despawns them
only after `HomeFadeOutSeconds = 0.35 s` — with `Destroy(e.gameObject)`, a path that **never reaches
`Die()`**.

`Enemy.OnDisable` already released this body's OTHER battle-lock claim (the
`HeroCombatEngagement` token) on every exit — its own comment states the invariant: *"a despawned/
pooled/destroyed enemy can never wedge `BattleLock.IsInBattle()` true … covers all exits"*. But an
enemy raises the lock through **two** owners, and the second — the pursuit pulse it stamps every
`DriveNav` tick — was revoked in exactly **one** place: `Die()`.

So the arithmetic, on constants that live in this tree:

| step | source | t (unscaled) |
|---|---|---|
| `Release` clears the pursuit ring | `BattleArena.cs:2730` | 0.00 s |
| survivors keep stamping `ReportPursuit` (still alive behind the fade) | `Enemy.cs:1578` | 0.00 → 0.35 s |
| survivors destroyed — no `Die()`, so no `RevokePursuit` | `BattleArena.cs:2919-2922` | 0.35 s |
| last pulse expires on `PostureSignals.PursuitTtl` (1.5 s) | `PostureSignals.cs:37` | **1.85 s** |
| **the gate judges a retreat** at `SettleSeconds` | `BattleQuiescenceGate.cs` | **0.75 s** |

`0.75 < 1.85`, every time — which is why **WO-1233's own header already recorded that "the RETREAT
case fails deterministically"** while the win case (which waits out the reward screen) did not.

This is the WO-1308 shape one door over: **a release seam that existed and an owner that never
reached for it on the path where it mattered.**

### Invariant 2 — the modal (a DIFFERENT owner; NOT the same cause)

The two findings **co-occur** but do not share a cause. They share one *precondition*: a retreat
announces the session end **synchronously** while doing its actual teardown **asynchronously behind
the masked-return fade**.

A retreat presents an end-state screen: `Resolve` sets `_pendingLossBanner` to
`hud.ShowResult(false, …)` (`BattleArena.cs:2679`), `BattleArenaHud.ShowResult` routes a defeat
through `EndStateView.Show` (`BattleArenaHud.cs:96`), and that calls `PanelManager.NotifyOpened` on
a registered handle named `"EndState"` (`EndStateView.cs:200-202`). It just opens it **LATE**, on
arrival, at the end of `ReturnHomeWithFade` — which is the t=397 "two death screens" fix and must
not be moved back.

And the arm site told the gate the opposite: `won ? (…EndStateView.IsShowing) : null`. The gate's
own header states the rule that `null` broke — *"an open reward screen is correct behaviour, and
failing on it is the fastest way to teach everyone to ignore this gate."*

**The capture cannot say WHICH panel was open**, because the modal finding named none — the same
attribution gap WO-1233 closed for the battle-lock, and §12 forbids editing on a theory about the
answer. So the ticket does the two things that are provable without one: it closes the structural
false-positive hole above, and it makes the finding NAME the panel and say whether it is visible.

**On the known `STUCK WORLD HOLD: 'pause-menu' outstanding for 8444s` and the settings panel the
owner photographed:** that is a `WorldHold` reason, a different subsystem from the `PanelManager`
arbiter, and nothing in seq 4677 ties them together. **Unproven — deliberately not fixed here.**
The named-holder message is what will decide it in one read on the next capture.

---

## Required

1. The pursuit pulse must die with the body that stamped it, on **every** removal path — not only
   on `Die()`. One owner, one lifecycle (WO-1108); no Nth per-caller release.
2. **SELF-HEAL, not merely log** (the WO-1308 standard). A softlock that reports itself is still a
   softlock.
3. ⛔ Never narrow `BattleQuiescenceGate`'s detection or its message text; ⛔ never force
   `BattleLock` false; ⛔ never weaken `PursuitBattleProbe`'s predicate (that would trade a stuck
   lock for combat input that dies during a real chase — F8-46 Option A).
4. Extend `BattleQuiescenceRegression` (no second suite) with an oracle pinning *a retreat releases
   every battle-lock holder AND closes every panel handle*. Prove it RED first.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. `BATTLE_QUIESCENCE_SUITE_OK` with the four new WO-1337 cases.
3. ⭐ A device capture over a session including at least one **retreat** showing **zero**
   `BATTLE_QUIESCENCE_FAIL` lines. The absence of the message is the acceptance.
4. Owner felt-verifies (retreat from an overworld encounter, then interact with the town) and CLOSES.

## What NOT to touch

- ⛔ `WaveManager`'s WO-1308 unwind or its lock-probe predicate — proven working by this capture.
- ⛔ `BattleQuiescenceGate`'s detection logic or the two findings' original sentences.
- ⛔ `Assets/HeroContent` + hero importer metas (live decimation lane).
- ⛔ The store lane (`PackStore`, `NightMarket*`, `packs.json`, `canon-strings.json`,
  `hud-areas.json`), `PetHeroLeash`, payload/Addressables files.
- ⛔ The `timeScale` lane (`WorldHold`, `HitStopManager`, `CombatFeedbackManager`,
  `HeroHitReaction`) — a separate owner, and no clock was written.
