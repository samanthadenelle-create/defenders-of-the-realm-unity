# WO-1389: introduce the reason to train and to upgrade - the free three open the door; the game must show what is behind it

**Status:** IN PROGRESS 2026-09-05 05:37 - landed + gated (COMPILE_GATE_OK, REGRESSION_OK 378/378 incl. [post-raid-beat]); in the APK building now, then the device/Firebase. Q1-Q3 still open (HUD voice shipped). Design ruled by the owner 23:20-23:27 (why AND how, as a post-first-raid FTUE beat); sequenced AFTER WO-1387 (time-only training) lands; Q1 voice still open - implement with the HUD voice and swap on her word

## Owner, verbatim (2026-09-04 23:20-23:23)
> "the idea was lets start them with a free army to get them into raids" -> "then introduce the reason to
> upgrade" -> "but should 3 be it? dont you want a full army?"

## What the build already has (read at source tonight)
- **The door:** `StarterArmyGrant` gives 3 free Footmen at the first Barracks; the WO-823 soft gate opens the
  raid door on 3 deployable slots (save v41). The deploy screen reads `Army: 3 / 10 slots`.
- **The gap:** the army cap is 10 (`ArmyStorage`), so the first raid goes out at 3/10. Seven empty slots are
  the first reason to train - and with WO-1387 training costs only time (gold hurries it).
- **The ladder:** four camps gated by victories (`scene-configs.json` `unlockVictories` 0 / 3 / 10 / 20, WO-1375),
  each with more defenders and a boss; `RaidSelectionVM` already locks cards with "win N more raids".
- **The reason to upgrade, authored but unspoken:** `troop-upgrades.json` - Footman L2 = +16% strength, L3
  unlocks Sweeping Cut, L5 Warden's Roar, L7 Champion's Combo; Archer L3 Snare Arrow, L5 Suppressing Volley,
  L7 Thunderbolt; reach and strength curves to L7 for all nine troops. `TroopUpgradeSeconds` is the only price.
- **What is missing:** nothing on any screen says "your army is 3 of 10", "this camp's garrison is 9 - you
  have 3", "L3 unlocks Sweeping Cut", or "the next camp needs 3 wins". The scout report lists walls / garrison
  / boss but never compares them to YOUR army. The guide has no troop-upgrade line.

## Answer to "should 3 be it?" - no; 3 is the door, 10 is the army
Keep the grant at 3 (the door must open on turn one). The design job is to make the other 7 slots and the
upgrade ladder VISIBLE and WANTED, in this order of pressure:
1. **After the first raid** (win or loss): the victory/settle screen says, in words, `Army 3 / 10 - train
   7 more to fill your ranks` with a TRAIN door to Manage - Troops. (RaidVictoryController / EndStateVM row.)
2. **The scout report compares:** `Garrison: 9 defenders - you field 3` and, when the camp's tier exceeds the
   army's average level, `Their walls are Stone - Footman L3 breaks Stone` (the reach/strength curve made
   into one sentence per camp; the copy is the owner's). Greyscale-safe, words not colour.
3. **The Troops card shows the NEXT unlock:** under UPGRADE TO L2: `L3 unlocks Sweeping Cut` (from
   `BarracksProgression.NextAbility`) so the button has a destination, not just a number.
4. **The raid grid's locked card names the wins needed** (already: "win N more raids") AND what those wins
   buy: `Camp II: stone walls, 12 defenders` - the scout line, shown before the player can enter.
5. **Guide entry** ("Troops") gets two sentences: the free three, and that levels unlock abilities.
6. **Journey card** (the Raids card WO-1357 locks) shows `Army 3 / 10` as its subtitle until full.


## THE VEHICLE (owner, 23:25): a FTUE beat AFTER THE FIRST RAID
The six pressure points above are delivered as ONE post-first-raid tutorial beat, not as scattered UI:
- Fires once, on the first `RaidDeployController.ReconcileRaidEnd` (win OR loss - a loss is the stronger
  teaching moment), after the settle screen is dismissed and the player is back in town (the WO-823
  `everCompletedRaid` flip is the trigger; latch on the existing `seenTutorials` map, no schema bump).
- Form: a DIALOGUE SCREEN WITH IMAGES - no world actor (standing canon: the wolf/Aldwin body exists only for
  the opening FTUE; every later beat is a dialogue screen). Echo-guide voice if Q1 = Echo.
- WHY then HOW (owner 23:27: "introduce to player why to upgrade and how"). The beat is two-part:
  WHY = three dialogue panels, each one sentence + one image + one door: (1) "Your first raid is done.
  Army 3 / 10 - train to fill the ranks" -> door: Manage - Troops; (2) "Levels change what a troop CAN DO -
  Footman L3 learns Sweeping Cut" -> door: the Footman card; (3) "Camp II opens at 3 wins - stone walls,
  12 defenders" -> door: Journey - Raids.
  HOW = on taking door (2) the beat continues as a GUIDED TAP on the real screen, the way the opening
  FTUE coaches placement: `TutorialHudOverlay` coach-mark on the Footman rail row ("Pick a troop"), then on
  `UPGRADE TO L2` ("Upgrade costs time - tap to start"), then on the TRAINING NOW band once the job lands
  ("It is upgrading. Gold hurries it, if you cannot wait" pointing at OPEN QUEUE / HIRE REINFORCEMENTS).
  Same for TRAIN 1 FOOTMAN if the army is under 10 ("Train to fill your ranks - it only takes time").
  Each coach-mark completes on the real tap (signal), never on a timer. Skippable at every step; never
  blocks the HUD; completing any step latches the beat.
- It slots into `tutorial-steps.json` as a NON-mandatory beat (the mandatory chain is pinned at 8 by
  `CheckTutorialSteps` - do not grow it); `TutorialStepReachabilityRegression` must reach it headlessly.
- Points 2 and 4 above (scout compare, locked-card scout line) stay as ambient UI so the reason persists
  after the beat is dismissed.

## Not in scope
Troop balance numbers; the reward ladder (WO-1373/1374); the escalation thresholds (WO-1375).

## Owner questions (one word each)
- Q1 Copy voice: the Echo guide's voice ("Elowen: ...") or the plain HUD voice for the compare lines?
- Q2 Should the 7 empty slots also prompt from the HUD (a Journey subtitle) or only after the first raid?
- Q3 Does filling the army to 10 unlock anything beyond the door (a name, a banner)? If not, say "no".
