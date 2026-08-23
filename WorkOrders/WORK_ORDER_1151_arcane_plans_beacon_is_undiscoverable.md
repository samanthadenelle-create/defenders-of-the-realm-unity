**Status:** READY TO IMPLEMENT

# WORK ORDER 1151 — The Arcane Tower plans beacon is perfect, and nobody knows what it is

**Minted:** 2026-08-22 (CLI, banner bumped 1151 -> 1152 in the SAME edit)
**Lane:** Onboarding / world legibility. **Class:** DISCOVERABILITY, NOT A DEFECT.
**Evidence:** `docs/ui-evidence/wo1151/01_seeker_plans_beacon.png` — Seeker, 2670x1200,
2026-08-22 23:24.

## ⭐ THE VFX IS APPROVED. DO NOT TOUCH IT.

Owner, on the capture: *"its absolutely perfect but the user has no idea till told or stumbles on
it, that this is the new arcane tower plans"*.

⛔ This ticket adds **NOTHING** to the beacon's appearance. Do not retune the column, the ground
ring, its colour, its scale or its budget. The one thing wrong with it is that it is unexplained.

## THE GAP

A large light column and ground ring marks where the Arcane Tower plans dropped. A player reads it
as scenery — or never looks up. There is no name, no prompt, and nothing tells them it is theirs to
collect. The feature works and is invisible in the only sense that matters.

## ⭐ OWNER RULING 2026-08-22 — THE ECHO SPEAKS

Owner, verbatim: *"i think the echo should pop on screen and say see that light, looks like
something dropped"*.

So: on first sight of the beacon, the Echo delivers a one-line nudge in the player's own voice.
Not a tooltip, not a quest-log entry — a companion noticing something.

## ⛔ THE TRAP, AND IT IS THE WHOLE IMPLEMENTATION RISK

**"Pop on screen" MUST mean a DIALOGUE SCREEN, never a spawned Echo body.**

Owner, confirming: *"not 3d"* / *"just a window vidual dialog"*. A 2D window with the Echo's
portrait and the line. **No world actor, no 3D model, nothing placed in the scene.**

- `EchoWorldPresence` is the **single appearance owner** for the Echo (WO-1108 Lane B). Its
  lifecycle is fixed: it escorts the player to the gate, vanishes, and returns **once** after the
  battle. One owner, one lifecycle, no second spawner. `PetDeployer.DespawnEcho` is the first and
  only despawn path in the game.
- Canon already settled the general case: the guide **body** exists only for the opening FTUE;
  **every later beat is a dialogue screen with no world actor**.

Spawning an Echo to say this line would be a second spawner AND a second appearance owner — two
rules at once. Use the dialogue seam (`Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs`)
and the Echo's portrait.

## SCOPE

1. Fire the beat when the beacon first becomes visible to the player — not when the plans object is
   created, or a player who never enters that area gets a line about a light they cannot see.
2. **Once, ever.** Persist it. A companion who repeats "see that light" every load is worse than
   silence. Mirror the existing one-shot patterns rather than inventing a flag.
3. The line is the owner's, in the Echo's voice. Copy lives in `canon-strings.json` — BOTH
   canonical copies, byte-identical, **ASCII only** (TMP renders emoji and smart quotes as tofu).
4. Suppress it while a modal, battle or wave is up; a nudge that lands over a fight is noise.
   `BattleLock` is the existing authority — read it, do not add a second gate.

## ⛔ CONSTRAINTS
- Do NOT modify the beacon VFX, its budget, or add a VFX loop slot. It is owner-approved as-is.
- Do NOT spawn an Echo actor (see the trap above).
- Owner is **RED/GREEN COLOURBLIND** — if any marker is added it carries a word or glyph, never a
  hue.
- Code-built uGUI only; UXML does not work in player builds.
- ⚠ Do not touch the collection logic. `CastleDefensePlansPickup.TryCollect` (`:80`, `:84`) and its
  `PlansCollected` event (`:46`) already work; this ticket only makes the beacon legible.

## ACCEPTANCE
- [ ] A fresh Seeker capture shows the Echo's line on first sight of the beacon
- [ ] The beat does NOT fire a second time after a reload
- [ ] No Echo body is spawned — `EchoWorldPresence` remains the sole appearance owner
- [ ] The beacon itself is pixel-identical to the approved capture
- [ ] Verified by DEVICE SCREENSHOT, not by reading code
