# WO-1415: nothing in the game ever introduces Heartfire - and it is now the ONE gate on raiding

**Status:** FIXED 2026-09-05 - landed + gated (COMPILE_GATE_OK 10:58, REGRESSION_OK 383/383 11:01 incl. HeartfireRegression PIN G/PIN H and TutorialStepReachability case 9). ⚠ NOT YET ON THE DEVICE: the 11:02 APK chain died (`apk-build.log:25795` IOException, the APK file was held by another process; `overnight-apk-status.txt` APK_THREW) and the phone still runs 356620 (pre-1415). Rebuilt in tonight's chain; owner felt-test closes. *(was: "building to the device now" - corrected 21:50, that build never existed.)* ⭐ **RULED 2026-09-05: the sentence is "each one sends you on a raid"** (owner: "yes use that sentence"). Merged-review ruling #3 is CLOSED; the copy below is now canon, not a proposal.

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
1. **The plate carries the meaning in PARENTHESES, not a sentence** (owner ruling 2026-09-05: "why
   not display raid in parantheses"). Charged: `Heartfire 3/3 (raids)`. Spent: `Heartfire 0/3
   (raids) - next in 3h 12m`. The three marks stay (greyscale-safe).
   **Why the parenthetical and not the sentence here, measured:** this is a single fitted line on the
   Heart plate - the row WO-1384 had to fight into the plate at 20-26 px - and WO-1407's lane
   measured a 42-char line as the tight case against `HudLayoutBands.HeartMount`. A sentence
   ellipsises; two words do not. The SENTENCE lives where there is room (the guide entry and the
   introduction beat, below). One source string, two renderings: short form on the plate, full
   sentence everywhere else.
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

## Owner ruling - GIVEN 2026-09-05
> "yes use that sentence"

**CANON: a Heartfire charge is what "sends you on a raid".** That clause is the one source for
every place the mechanic is explained - the plate, the guide entry, the introduction beat and the
refusal toast's second line if it needs one. Do not paraphrase it per surface; author it ONCE
(canon-strings) and read it everywhere, the WO-1398 one-source pattern.

Still open, smaller: introduce it at the first Raids-grid open (the ticket's default, implement it)
or at founding? Implement the default; one word re-points it.

## Not in scope
The charge maths (3 max / 4 h regen, canon §4), the HUD flame art (owner's creative call), the
raid door itself (WO-1379, shipped).
