# WO-1415: nothing in the game ever introduces Heartfire - and it is now the ONE gate on raiding

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the owner's felt-test on build 2026.09.05.356468

## Owner, verbatim (2026-09-05 10:2x)
> "Heartfire is full, i dont understand as a new player what to do with that. No one in game has
> introduced me to heartfire"

## Evidence (measured this session, not inferred)
Mentions of the word "heartfire" in every player-facing content file:

| file | mentions |
|---|---|
| `Assets/Resources/Data/Canonical/guide-content.json` | **0** |
| `Assets/Resources/Data/Canonical/dialogue/dialogues.json` | **0** |
| `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json` | **0** |
| `Assets/Resources/Data/Canonical/canon-strings.json` | 1 (the HUD label) |

The only words a player ever sees are the HUD plate - `[*] [*] [*]  Heartfire` / `"Heartfire is
full"` - and, if they tap a camp with no charge, the refusal toast. No guide entry, no dialogue
line, no tutorial beat, no tooltip. The word is introduced by being printed.

**Severity is higher than a copy gap.** WO-1379 shipped in this same build and made Heartfire the
**ONE gate on whether the player may raid at all** (the per-camp cooldown is retired). So the
mechanic that decides access to the core loop is the one mechanic the game never explains.

Both independent UI reviewers found this without seeing each other
(`docs/qa/UI_REVIEW_2026-09-05/REVIEW_B_independent.md` B10: "Heartfire never explains itself";
REVIEW_MERGED ruling #3 has been open since 07:25 with NO default because only the owner can say
what a charge buys).

## What "Heartfire is full" fails to say
It reports a STATE with no consequence attached. A player reads it and cannot answer: what is it,
what spends it, what do I get, what happens when it is empty, when does it come back.

## Fix shape (one mechanism each, no new systems)
1. **The plate says what it buys, not just that it is full.** When charged:
   `Heartfire 3/3 - each one sends you on a raid`. When spent: `Next Heartfire in 3h 12m`.
   The three marks stay (greyscale-safe); the sentence is what changes.
   ⛔ The exact wording is the OWNER'S (ruling below) - this is the proposed default only.
2. **One guide entry** ("Heartfire") in `guide-content.json`: what it is, that it stacks to three,
   that one rekindles every four hours, that a raid spends one. Two sentences, the shape the
   WO-1389 "Troops" entry already uses.
3. **One introduction at the moment it first matters** - the first time the player opens the Raids
   grid, not at founding (a new player has no raid to spend it on yet). Reuse the existing
   dialogue-screen beat mechanism (WO-1389's `ctx_post_raid` is the working precedent): one panel,
   one sentence, one door to the grid. Non-mandatory, one-shot, latched on `seenTutorials`, no
   schema bump, and the mandatory chain stays at 8.
4. The refusal toast already names the rekindle time (`HeartfireService.BlockedMessage`) - leave it.

## Acceptance
- [ ] `grep -i heartfire` over `guide-content.json` + `dialogues.json` + `tutorial-steps.json`
      returns >= 1 in each, and the HUD line names the consequence.
- [ ] RED-first pin: the plate string must contain a consequence clause, not only a state word;
      a guide entry with the id exists; the beat is reachable headlessly
      (`TutorialStepReachabilityRegression`). Name the mutation for each.
- [ ] Owner felt-test: a new player meets the word before it ever blocks them.

## Owner ruling needed (this is merged-review ruling #3, still open)
**What does one Heartfire charge buy, in your words?** The proposed default above is
"each one sends you on a raid". One sentence replaces every occurrence.
Second, smaller: introduce it at the first Raids-grid open (proposed) or at founding?

## Not in scope
The charge maths (3 max / 4 h regen, canon §4), the HUD flame art (owner's creative call), the
raid door itself (WO-1379, shipped).
