# WORK ORDER 702 — The Founding of Elarion: NPC-guided blank-start tutorial *(renumbered from a colliding fresh 699 mint, 2026-07-13 — 699 is the SEL-1 hero-select chips WO)*

**Status: READY TO IMPLEMENT** (owner directive 2026-07-13, verbatim intent: "start with the
empty structure, tree in the middle, an NPC gives a tutorial — this is your castle, this is what
we want to defend; lay out the structures where you want; remind them placement is strategic
because it can be attacked and their greenery and lumber yards could be damaged").
**Lane:** Onboarding/FTUE. **Type:** NEW beat content on BUILT systems (Tutorial V2 interpreter +
DialogueService + BuildMode signals). **Depends:** BLANK-1 (the start must actually be blank
first) · supersedes WO-695's grace-default pre-placed Forge once landed (the guided beat replaces
the stopgap; keep the grace default only as the skip-path fallback).
**Numbering:** minted from the CLI_LANES banner (next free was 699); mint the Notion row on claim.

## The beat sequence (data-driven — tutorial-steps.json rows + dialogue nodes, not code)

Fresh save, after landing at the hub (tree + well + walls only, per the BLANK-1 ruling):

1. **The Steward greets you at the tree.** One NPC (creative pin #1 — a steward/elder; a named
   character the owner picks) walks up / awaits at the Heart: *"This is your castle — and this
   tree is what we defend. Everything else… is yours to build."* (final copy = owner pass;
   ten-year-old-clear, diegetic).
2. **Guided first placement — LOW-STAKES FIRST (owner ruling 2026-07-13, supersedes the
   Forge-first line from WO-673 ruling #4; BY-HAND placement stands).** The beat opens
   Build → Town with the **Echo Hollow** highlighted: a lesser-value building whose placement
   can't be "wrong," and it pays off immediately — **placing it grants the pet** (the reward
   teaches "building things gives me things" before any stakes talk). Existing
   `TutorialSignals` fire on placement; add a generic `StructurePlaced(itemId)` signal if only
   TowerPlaced exists (small, reusable).
3. **The strategic warning — escalate to the STORE-HOLDERS (owner ruling 2026-07-13).** Next
   the Steward turns to the buildings that hold value: *"Choose your ground with care. Some
   roofs hold your stores — grain, timber, coin. When the horde comes, they'll strike what
   they can reach — an exposed lumber yard burns first, and what burns stops earning. Put
   what you cannot afford to lose where you can afford to defend it."* Guided second placement
   = a store-holding building (Mill/Market/Lumbermill), with the defensible-ground framing
   live on screen. This is WO-672's damage lifecycle + WO-698's threat framing taught at the
   moment it matters — placement = strategy, low-stakes → reward → stakes escalation.
4. **The Founder's Plan ghost (approved ruling 3A).** The Steward offers the ghost layout:
   *"I can sketch where the old town stood — build it my way, or ignore an old man's chalk."*
   One tap = build-it-for-me (spends the seed budget); ignoring it is fully valid. Ghosts fade
   as the player places their own.
4b. **Guided defenses — "a defense or two" (owner, 2026-07-13).** After the town basics stand
   (Sylas's dialog has walked the player through the core buildings), Sylas turns to defense:
   guided placement of one or two defensive pieces (an Archer Tower; optionally a wall segment)
   before the first wave is ever armed. The Defenses tab gets its introduction here, not in a
   menu tooltip.
5. **Close the loop.** First DEFEND prompt after the core kit exists (or the player idles) —
   the wave teaches what the warning promised.

## Fresh-spawn vista (owner ruling 2026-07-13 — the acceptance image)
New game opens on: **the tree, the well, and Sylas standing there** — walls/gates around,
nothing else. No grace forge (killed, WO-707), no vendors, no wisps in frame if avoidable.
Sylas's dialog then constructs the town basics, then a defense or two, then the first wave.

## Sequencing ruling (owner, 2026-07-13 — BINDING on this arc)

**The founding happens in a PEACEFUL town first; the defense comes after.** Beats 1–4 play out
with zero hostile pressure: no waves arm, and no hostile mobs threaten the hub ring while the
founding beats are incomplete (hold `OverworldEncounterSpawner`/`RegionMobSpawner` pressure away
from the walls for the fresh-save founding window — gate on the same beat-incomplete GameState
flag, don't invent a new one). The first DEFEND (beat 5) is the moment hostility enters the
world; the peace→threat turn IS the lesson landing. Skip-path players get the same peace window
until they arm a defense or the idle-DEFEND prompt fires.

## Reconciliation (reuse, never greenfield)
- **Tutorial V2** (`ff.tutorialv2`, tutorial-steps.json + interpreter + telemetry) is the beat
  engine — this WO AUTHORS ROWS, it does not build a tutorial system. If V2's flag is still
  default-OFF, this arc is its flip-ON case (its own fleet pass first, per the standing note).
- **DialogueService/one shared runner** for the Steward lines (no bespoke UI); the Steward is
  an injector-spawned NPC gated to fresh-save + beat-incomplete (one-shot GameState flag,
  additive — the ECHO-1/WO-681 first-meeting pattern).
- **Vendor talk-route census (WO-695's FTUE risk)** is retired by construction: no vendor beats
  fire until the player has placed the buildings that host them.
- Colorblind/verbiage laws apply to all beat UI; skippable (the "Tap to continue ▸" grammar).

## Gates
- [ ] Fleet tutorial probe: fresh save → Steward beat → guided Forge placement → warning line →
      ghost offer → DEFEND arms; probe drives the REAL placement gate (the 2990aaf6 lesson).
- [ ] Skip path: dismissing the Steward leaves a playable state (grace-default Forge or free
      build — never a dead end); beats never re-fire on reload mid-arc or on migrated saves.
- [ ] COMPILE_GATE_OK + DataRegression (steps/dialogue rows parse, dual-copy) + owner felt-pass
      on a true fresh save (PO closes).

## Owner pins
1. **RESOLVED (owner, 2026-07-13): the Steward = SYLAS, the scout** — "use the model for him,
   then unload it." He already exists end-to-end: the Ranger hero body is his model
   (HeroSelectController.cs:707), Resources/Portraits/Sylas.png + HeroPortraits/Sylas exist,
   Tutorial V2 already speaks through him (TutorialStepModel.cs:35, `world.sylas` anchor in
   TutorialHighlightRegistry.cs:124). Spawn his body for the founding beats; DESPAWN (unload)
   when the arc completes — no permanent NPC, no new art.
2. Final copy for the three lines (drafts above are placeholders in her voice).
3. Ghost in V1 scope, or guided-Forge-only first and ghost as fast-follow?
4. Does the migrated-save cohort ever see a one-line "your town survived" variant, or nothing?

*Cross-refs:* WO-673 creative review §3 (rulings 3A/4) · WO-695 (FTUE guard superseded) ·
BLANK-1 (dependency) · WO-672 (the stakes being taught) · WO-698 (strategy thesis) ·
Tutorial V2 (`ff.tutorialv2`) · `docs/TICKET_PIPELINE.md`.
