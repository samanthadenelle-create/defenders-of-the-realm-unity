# WO-1179 - Roaming troops that attack the town, escalating in size and smarts

**Status:** SPEC - needs one design pass with the owner before it is handed to a dev lane.
**Silo:** Combat/AI. **Origin:** owner, 2026-08-24, verbatim:
> *"I still want to add roaming troops that attack the town, incrementally getting harder, smarter
> attacks one gate, maybe two gates same time, all 4 eventually"*

## Why this is its own ticket and not WO-513

⚠ **WO-513 is the nearest thing in the tree and it is NOT this.** 513 makes an orc *family* coordinate
once it has already arrived - surround, flank, express roles instead of three identical solo rushes.
It is about **how a group fights when it gets there**. This ticket is about **what arrives, from
where, how often, and against how many gates at once**. Handing 513 to a dev lane expecting roaming
troops would deliver a real feature that is not the one she asked for.

⭐ They compose well: 513 is the melee behaviour of a pack, this is the campaign that sends packs.
Build 513 first and this inherits it.

## The escalation ladder (the shape of the ask)

1. One gate, small band.
2. One gate, larger / better composition.
3. **Two gates simultaneously** - the first point at which the player cannot simply stand in one
   place, and therefore the first real difficulty step.
4. All four gates.

⚠ **The step that matters is 2 -> 3, not the numbers.** Everything before it is a bigger version of
the same fight; the two-gate attack is the moment the player must choose what to leave undefended.
Tune that transition, not the roster sizes.

## Seams that already exist - reuse, do not greenfield

- `SpawnPoint` tags are already placed **12m outside each gate** (CLAUDE.md §7) - the four attack
  origins exist.
- `WaveManager` already **generates rosters** (`waves.json` `_smartComposition:1`; the authored
  `enemies[]` batches are INERT and a re-add now FAILS a regression). ⛔ **Do not author batches** -
  extend the generator.
- Enemy AI finds the hero **by component** (`FindFirstObjectByType<HeroLocomotion>()`), not by tag.
- Gates are `IDamageableStructure` implementors already.

## Open design questions - the owner's, and they change the build

1. **Is this the wave loop escalating, or a SECOND system running alongside it?** A raid that arrives
   while the player is mid-wave is a different feature from a wave that gets harder.
2. **Can it arrive while the player is away / offline?** ⚠ This collides directly with the 48-hour
   shield product she proposed the same day - if roaming troops can hit an offline town, the shield
   has something to protect and a reason to exist; if they cannot, the shield protects nothing.
3. **What does losing a gate cost?** Escalation without a consequence is difficulty without stakes.

## Acceptance (provisional - do not implement until the questions above are answered)

- [ ] Attacks originate from the existing `SpawnPoint` markers, not a new spawner
- [ ] Composition comes from the `WaveManager` generator, not authored `enemies[]` batches
- [ ] The 2-gate step is reachable in a headless run and PROVEN by a captured trace, not by reading
      the tuning table
