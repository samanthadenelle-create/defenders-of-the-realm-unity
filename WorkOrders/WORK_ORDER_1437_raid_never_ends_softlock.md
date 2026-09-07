# WO-1437: P0 SOFTLOCK - the raid never terminates. Win condition met, clock frozen, respawn re-enters the raid.

**Status:** FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `5bc5025f5` (see RESULT). Was P0: the player could not leave a won raid by any path except RETREAT.
**Silo:** raid session lifecycle (`RaidDeployController`, `HeroDeathEndState`/`EndStateView`, the raid
clock). Disjoint from WO-1436 (HUD posture/teardown) and WO-1435 (rail geometry).
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358161**, verbatim:
> *"ssays i won but still in raid"*

**Evidence:** `logs/debug/raid-stuck-2026-09-06.log` (9 MB) +
`logs/debug/raid-ai-and-pets-2026-09-06.log` (18 MB), plus two device screenshots taken minutes apart.

---

## 1. THE CHAIN, QUOTED

```
13:02:47.284  [Flow:UI] PanelManager: 'EndState' opened and verified visible (IsOpen=true).
13:02:47.284  DeNelle.Village.UI.HeroDeathEndState:OnHeroDeath()
13:02:53.285  [Flow:EndState] HeroDeath primary fired: action=respawn
13:02:53.291  [Flow:DeathTrace] SCREEN CLOSED: EndState 'YOU HAVE FALLEN' by EndStateView.OnDestroy
13:02:55.487  [Flow:Reward] KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker ...
13:02:58.476  [Flow:Reward] KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker ...
13:03:00.564  [Flow:Reward] KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker ...
```

**The hero DIED, the player chose RESPAWN, and respawn returned them INTO the raid.** `(raid active)` is
still true a full 7 seconds later and kills are still scoring. **No `ReconcileRaidEnd`, no return home.**

## 2. THE CLOCK IS FROZEN - measured across two captures

Two device screenshots taken **several minutes apart** both read **`1:58`** remaining. Over the same
interval `Razed` climbed **79% -> 85%** and `SPIRE DOWN` was achieved.

**The world is simulating. The raid clock is not.** Since the raid's own scoring reads
`elapsed=45s/180s ... underTime=True`, a stopped clock means the timeout path that would otherwise end
the session can never fire. **The timer is the last remaining exit and it is dead.**

## 3. THE WIN CONDITION IS VISIBLY MET AND NOTHING ACTS ON IT

On screen: **`SPIRE DOWN`**, **`Razed 85%`**, `Troops 8/10`, `3/3`. The owner read that as a win, and it
is one. **Nothing terminates the session.** The only interactive exit left is the `RETREAT` button, which
settles as a partial - so a player who WON is offered only the losing exit.

## 4. WHAT TO PROVE BEFORE FIXING - the three exits, separately

Each is a distinct path and at least two are broken. **Instrument each, then fix the dead step (section 12).**
1. **Win/objective exit** - what watches `SPIRE DOWN` / razed-threshold, and why did it not fire?
2. **Timer exit** - what advances the raid clock, and what stopped it? Note WO-1436 records ~4 s of
   `Time.timeScale=0.00` at raid entry from the deploy modal; determine whether a hold was acquired and
   never released. **A world-hold leak is the lead hypothesis - prove or kill it, do not assume.**
3. **Death exit** - `HeroDeath primary fired: action=respawn` must not resolve to "stand up inside the
   raid". Decide with the owner whether respawn in a raid is even a legal action, or whether hero death
   ends the raid as a settled partial (the earlier 12:59:47 run DID settle on death:
   `hero death settle: partial loot for 32% razed`). **Two death paths behaved differently in one session
   and that inconsistency is itself the finding.**

## 5. ACCEPTANCE

- [ ] A won raid returns the player home and settles the win, proven by a headless raid run asserting the
      **scene changes away from `RaidBase_*`** after the objective is met.
- [ ] The raid clock advances, proven by a regression that MEASURES elapsed across ticks - a frozen clock
      must FAIL. It must fail against today's build; state the RED proof in-file.
- [ ] Hero death in a raid resolves to exactly ONE documented outcome, per the owner's ruling.
- [ ] **A new seam oracle: every raid session reaches a terminal state.** No exit path may depend on the
      player finding RETREAT. This is the general form and it is what would have caught it.
- [ ] `REGRESSION_OK n/n`.

## 6. WHY NO ORACLE CAUGHT THIS
Same species as WO-1430 and WO-1436: the parts each work - scoring scores, EndState shows, retreat
settles - and **nothing asserts that the session as a whole can END.** Suites ask "does this system do its
job"; none asked "can the player get out".

---
---

# IMPLEMENTATION - 2026-09-06 (edit-only lane; NOT gated, NOT committed)

**Status of this section:** code written and brace-clean; `COMPILE_GATE_OK` / `REGRESSION_OK` are the
CLI lead's to run (this lane never held the Unity lock). **One owner decision is flagged in §I5.**

## I1. RCA - the whole chain, from the captured lines only

Everything below is quoted from `logs/debug/raid-stuck-2026-09-06.log` and
`logs/debug/raid-ai-and-pets-2026-09-06.log`. Nothing here is inferred from reading source.

```
13:02:42.115  OBJECTIVE COMPLETE - RaidSpire RAZED. The raid is won.
13:02:42.116  VICTORY - raid 'raider_camp_small' won (SPIRE RAZED). Running claim -> next-companion -> return.
13:02:42.118  CLAIM - 'raider_camp_small' flipped ENEMY -> PLAYER-owned (newClaim=True).
13:02:42.133  stars settled: 3 (cleared=True destruction=77 % elapsed=62s/180s underTime=True ...)
13:02:42.149  raid-end reconcile - deployed 10, survivors 9, wounded 1 (stars 3, recovery 300s).
13:02:42.214  RETURN - victory screen shown ...; tap or auto-dismiss routes to the castle.
13:02:47.276  'Victory!' destroyed WITHOUT firing its primary action - EndStateView.Show
              - REPLACED by a new end-state 'YOU HAVE FALLEN'. That action is now abandoned.
13:02:47.276  hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=False)
13:02:47.284  'Victory!' destroyed WITHOUT firing its primary action - CloseFromArbiter
13:02:49.468  HERO MOVED ... by HeroHealth.Respawn reason=in-place respawn at spawn anchor
13:02:53.285  HeroDeath primary fired: action=respawn
13:02:55.487  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker
13:02:58.476  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker
13:03:00.564  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker
13:03:56 -> 13:04:16  [Flow:HeroOwner] ... timeScale=1.00  (every line, ~20 samples)
```

**The win exit FIRED CORRECTLY and in full.** Objective -> victory -> claim -> cooldown -> companion ->
stars -> loot -> army reconcile -> victory screen, all inside 100 ms. The session's *settlement* was never
the defect. **The defect is that the only route HOME from that settled session was owned by a UI view.**

`RaidVictoryController.ShowVictoryScreen` hands both the primary action (`ReturnHome`) **and** the
`AutoDismissSeconds` softlock guard (`_autoReturnSeconds = 12f`) to one `EndStateVM`. Five seconds into
that twelve-second guard the hero died - the raid world keeps simulating behind the victory screen, and
`timeScale=1.00` is **measured**, not assumed - the death end-state replaced the victory end-state, and
**both escape routes died with the GameObject.** The view even logged it, twice, and nothing was
listening.

## I2. THE 12:59 vs 13:02 INCONSISTENCY - one token, and it is the faction flag

```
12:59:45.549  hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=True)
12:59:47.750  hero death settle: partial loot for 32% razed.          <- settled, went home
13:02:42.118  CLAIM - flipped ENEMY -> PLAYER-owned
13:02:47.276  hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=False)
13:02:49.468  HERO MOVED ... by HeroHealth.Respawn reason=in-place respawn   <- stood up inside it
```

Same build, same scene, same death, five minutes apart. `HeroHealth.HandleDeath`'s raid evac branch read
`if (SceneOwnership.IsEnemyOwned)` **and nothing else**. That is a **faction** flag, and the victory path
**deliberately flips it** - `RaidClaimService` turns the razed camp player-owned at the win. So winning a
raid silently disarmed its own death exit. `HeroDeathEndState` read the same flag for its copy.

**The repo already had the right answer and neither reader used it.** `RaidScoring.RaidInProgress` is
documented in-code as *"THE ONE 'am I inside a raid' ANSWER"* (WO-1227) - the scorer's own lifetime with
`HubScenes.IsRaid` as fallback - and it does not move when ownership flips. This fix **removes a second
answer to one question rather than adding one**; no new session type was introduced.

## I3. TWO WO PREMISES THE DATA KILLED - report, not work around

**§2 "THE CLOCK IS FROZEN / a world-hold leaked from the deploy modal" is FALSE.** Every `[Flow:HeroOwner]`
line from 13:03:56 to 13:04:16 reads `timeScale=1.00`. `RaidScoring.Update` advances `_elapsed` every frame
and stops on exactly one condition: `if (_finalized) return;`. The raid settled at `elapsed=62.3s` of
`180s`, and **180 - 62.3 = 117.7s = the `1:58` the owner photographed twice.** The clock stopped because
the **session ended and the player was never removed from the scene** - a symptom of the softlock, not a
second bug, and not a `timeScale` hold. The lead hypothesis is **killed, not assumed away.**

*Consequence for acceptance §5 line 2:* **a "clock advances" oracle is GREEN against today's build and
cannot honestly be shown RED.** Manufacturing a RED there would be fabricated evidence (CLAUDE.md §11B).
The clock oracle is still written and still MEASURES real `Update` ticks (a genuinely frozen clock fails
it) but is declared in-file as a **forward guard**; the RED proof for this ticket is carried by the four
pins that do fail against HEAD - see §I6.

**§1 "respawn re-enters the raid because of `action=respawn`" is FALSE.** `HeroHealth.Respawn` moved the
hero at **13:02:49.468**, **four seconds BEFORE** the screen's primary fired at 13:02:53.285. The
coroutine is the mover; the end-state is narration. **Editing `EndStateView` would have fixed nothing.**

## I4. THE THREE EXITS, ANSWERED SEPARATELY

| Exit | Verdict from the capture | Fix |
|---|---|---|
| **Win / objective** | **NOT broken in settlement** - it fired completely at 13:02:42. **Broken in egress:** the route home was owned solely by a destroyable view; `_autoReturnSeconds` died with it. | Session-owned `StrandingWatchdog` in `RaidDeployController` (§I5.3) |
| **Timer** | **NOT broken.** `OnTimeExpired -> DoRetreat` is wired and the clock ran to 62.3s at `timeScale=1.00`. It is *inert after settle* by design (`Update` early-returns on `_finalized`) - correct only while something else guarantees the exit, which is what was missing. | Watchdog's last-resort arm covers a never-settled raid |
| **Death** | **BROKEN, and inconsistently** - see §I2. Gated on a flag the victory path flips. | Both readers now consult `RaidScoring.RaidInProgress` |

## I5. WHAT CHANGED

1. **`Assets/_Modules/Village/Troops/RaidScoring.cs`** - added `public const bool RaidDeathEndsRaid = true;`
   beside the existing `RaidInProgress`. **This is the owner's ruling, in exactly one place**, read by both
   the behaviour (`HeroHealth`) and the copy (`HeroDeathEndState`) so they can never disagree again.
2. **`Assets/_Modules/Village/Hero/HeroHealth.cs`** - the death evac branch now reads
   `enemyOwnedScene || (raidInProgress && (raidSettled || RaidDeathEndsRaid))`. `IsEnemyOwned` is **kept as
   an OR, not replaced** - Village2 and enemy-owned dungeons are not raids and must keep evacuating exactly
   as today. The trace names **which signal chose the branch**, so a future divergence is readable off a
   capture instead of re-derived.
3. **`Assets/_Modules/Village/Troops/RaidDeployController.cs`** - `StrandingWatchdog` + `ForceExitHome`.
   Unscaled, armed inside `BindScoringRoutine` (i.e. **before `BuildHud`**, honouring WO-1110 §1's
   load-bearing order - an exit hatch must never depend on presentation succeeding). Two arms: a settled
   raid still in its own scene after 30 s, and a raid that never settled at all after `clock + 45 s`.
   Fires `FlowTrace.Fail` loudly - the net is a seatbelt, never the fix. Safe by construction:
   `SettlePartialLoot` early-returns on `Finalized`, `ReconcileRaidEnd` latches on `_reconciled`,
   `ReturnHome` latches on `_returning`, so a normal exit makes it a logged no-op and it **cannot
   double-pay a raid** (both latches read at source, not from comments).
   *This is not a novel design:* `BattleArena.StrandingWatchdog` (WO-969) already solved this exact shape
   for arenas, and its comment records the identical failure - *"EndStateView.CloseFromArbiter -> Destroy
   -> the 45s watchdog had to rescue her"*. Raids simply never got the same net.
4. **`Assets/_Modules/Village/UI/EndState/HeroDeathEndState.cs`** - the copy signal now tracks
   `HeroHealth`'s branch. **This file makes no routing decision** - presentation stays out of the
   lifecycle, and Case D pins that it never gains one.
5. **`Assets/Editor/Regression/RaidTerminalStateRegression.cs`** (new) + registered in
   `DataRegression.RunAll` between the fences (so the `REGRESSION_OK n/n` count moves with it).

### ⚠ OWNER DECISION REQUIRED - one line, both answers already built

**Is "Rise again" a legal action inside a LIVE (unsettled) raid?** WO §4.3 correctly says this is not an
engineering call. It is `RaidScoring.RaidDeathEndsRaid`, and flipping it is the entire change.

- `true` **(set, and recommended)** - hero death is the raid's third exit: settles partial loot, reconciles
  the army, routes home. Identical to Retreat.
- `false` - hero death respawns in place and the raid continues; the player leaves by objective, clock or
  Retreat.

**Recommending `true` because it is not a new opinion - it is the ruling already shipped.**
`HeroHealth.HandleDeath` carries the owner ruling of 2026-07-30 verbatim (*"Hero death is the THIRD raid
exit... All three exits are honest now"*), and WO-1110 §3 made death pay exactly what retreat pays. The
12:59:47 capture **is that ruling working**; 13:02:53 is it being bypassed. `false` would retire a shipped
ruling, which is why it needs the owner's word rather than a silent pick.

**Not subject to the toggle:** a **settled** raid always routes home. Loot paid, camp claimed, clock
stopped - there is no session left to respawn into. That case is the softlock itself, not a matter of taste.

**Two behaviour changes deliberately NOT made** (owner's call, flagged rather than taken): making the hero
invulnerable after the win, and pausing the garrison during the victory screen. Either would have prevented
this specific death, neither is required by the fix, and both change how a raid feels.

## I6. ACCEPTANCE - measured

- [x] **Clock oracle MEASURES elapsed across real `Update` ticks** - Case A drives the live component and
      fails on a frozen clock. **Declared in-file as GREEN before and after**, with the proof that §2's
      premise was killed. Non-flaky: a zero editor `deltaTime` is recorded as a note, never a verdict.
- [x] **Seam oracle: every raid session reaches a terminal state** (Cases C + E) - no exit's only owner may
      be a destroyable view, and the clock exit must survive. This is the general form and the one that
      would have caught it.
- [x] **Hero death resolves to exactly ONE documented outcome** - one constant, both readers, traced.
- [x] **RED proof, measured not asserted.** Each pin's predicate was run against
      `git show HEAD:<path>` (commit `f986f3cff`) with this suite's own comment-stripping and
      brace-matched extraction: **7 pins RED**, Case A GREEN, exactly as documented. Recorded verbatim in
      the suite header.
- [ ] **A headless raid run asserting the scene changes away from `RaidBase_*`** - **NOT DONE, and it
      cannot be done today.** `AutoPilotDriver`'s raid loop deliberately stops OUTSIDE the raid scene (*"a
      re-introduced raid teleport trips AutoPilotProbes' scene-load Fail"*), so no harness plays a raid to
      its objective. Cases C/E pin the mechanism that performs the scene change; the live end-to-end
      assertion needs a PlayMode raid harness that does not exist. **Separate lane - the one acceptance
      item this WO leaves genuinely open.**
- [ ] `COMPILE_GATE_OK` / `REGRESSION_OK n/n` - **CLI lead's to run.** This lane is edit-only.

## I6b. RIGHT NOW, BEFORE ANY BUILD LANDS - **RETREAT is loot-safe for her**

She can press **RETREAT** and **lose nothing.** Read at source, not assumed:
`DoRetreat` is `SettlePartialLoot("retreat")` -> `ReconcileRaidEnd(0)` -> `TroopRally.Clear()` ->
`Save()` -> `GoCastle()`. `SettlePartialLoot` **early-returns** on `RaidScoring.Finalized`, which latched
at 13:02:42, and `ReconcileRaidEnd` is latched on `_reconciled`, which ran in the same breath. **Nothing in
`DoRetreat` downgrades a win.** Her 3 stars, the 1800w/1100i/2200g, the camp claim and Sylas were all
banked at 13:02:42.13-.21 and are already persisted. The button is mislabelled for her situation, not
destructive.

## I6c. FOLLOW-UPS - deliberately NOT done, so the P0 ships

1. **Cosmetic, introduced by this fix, in this exact scenario.** A post-win death now takes
   `leavingTheRaid=true`, so the fallen screen reads *"The raid is lost. You retreat to the castle"* for
   ~1.8 s over a raid she just **won**, before the evac lands. Not a softlock; wrong words. Correct shape:
   `HeroDeathEndState` **stands down entirely when `raidSettled`** (the victory screen owns that moment;
   `HeroHealth` still evacs). One `if` - held back only because the coordinator asked for the smallest fix.
2. **Double-`GoCastle` risk, unproven either way.** After a settled raid, the victory auto-dismiss and
   `HeroHealth`'s evac could both call `SceneRouter.GoCastle`. `RaidVictoryController.ReturnHome` latches on
   `_returning`, but that latch does not cover `HeroHealth`'s call. **I did not verify whether
   `SceneRouter.GoCastle` is itself idempotent** - check that before doing follow-up 1, since standing the
   death screen down changes which caller wins the race.
3. **PlayMode raid harness** (acceptance 1) - see §I6.

## I7. BRACE / NUL GATE (CLAUDE.md §1)

| File | `{` | `}` | NUL |
|---|---|---|---|
| `Assets/_Modules/Village/Troops/RaidScoring.cs` | 90 | 90 | 0 |
| `Assets/_Modules/Village/Troops/RaidDeployController.cs` | 129 | 129 | 0 |
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | 164 | 164 | 0 |
| `Assets/_Modules/Village/UI/EndState/HeroDeathEndState.cs` | 17 | 17 | 0 |
| `Assets/Editor/Regression/RaidTerminalStateRegression.cs` | 43 | 43 | 0 |
| `Assets/Editor/Regression/DataRegression.cs` | 1075 | 1075 | 0 |
