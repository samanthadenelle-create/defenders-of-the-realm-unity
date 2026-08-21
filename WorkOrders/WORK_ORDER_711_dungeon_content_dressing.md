**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 711 — HealersCottage content dressing: pills become PEOPLE (torch-teacher first)

**Status: SPEC — owner walking the dungeon live 2026-07-13 evening, annotating placeholders.**
**Lane:** Dungeons/content. **Depends:** the dungeon being mapped in (2026-07-13, portals ->
dungeons). Art fidelity = the WO-584c lane; THIS WO is the interaction/teaching layer that can
ship on placeholder-or-better bodies.

## Owner annotations (F8-captured, live walk)
1. **The entrance pill = an NPC who TEACHES THE TORCH NEED** (F8 verbatim: "this pill was
   going to be a npc teaching the need for a torch"). The lantern mechanic's consumable
   already exists (`AtbInventory.Torches` — "dungeon lantern-mechanic consumable", rides the
   existing inventory object, no schema change). Beat: the NPC at the threshold warns the
   dark eats the unlit ("No one walks the cottage dark. Take a torch - and mind it burning."),
   hands/sells the first torch, the dungeon's dark rooms make the lesson true. Same dialogue
   rail as everything (DialogueService rows; ASCII; word-carries-meaning).
2. **Interaction = TEACHING TORCH USE IN THE DARK** (owner, live): the Talk teaches the
   actual use-action; the first dark room makes it real.
3. **DOORS: "anywhere with a door, use Door action — nav-link PORT from one side to the
   other"** (owner, live): every dungeon door = an interact that teleports the hero across
   (the RegionGate/HeroLinkCrossing idiom — the game's trusted crossing primitive). No
   squeeze-through physics.
4. **STAIRS THE SAME** ("same with steps going up"): interact -> port to the top/bottom
   landing. Simple.
5. **Scope law for this walk (owner, verbatim): "WE CAN COOK LATER BUT FOR NOW SIMPLE"** —
   every slice above ships in its simplest honest form; polish/cooking is a later pass.
*(append further owner annotations as F8s arrive — one row per pill/feature.)*

## Shape
- Replace each annotated pill with: a body (People-pack/KayKit as available — placeholder
  acceptable per the owner's milestone acceptance), a `CastleNpcInteractable`-style Talk, and
  its teaching row. Data + injector-idiom only; no scene hand-edits (runtime dress like every
  other injector).
- The torch-teacher gates nothing (teaches, offers; never a wall) — the DARK does the gating.

## Gates
- [ ] Fleet dungeon probe: enter -> torch-teacher present + Talk routes -> torch acquired ->
      run completable; COMPILE_GATE_OK + baseline; owner felt-pass on the teaching beat.

*Cross-refs:* owner F8 seq-1322 · WO-584/584c (art) · AtbInventory.Torches (NestedTypes.cs) ·
the dungeon map-in (portals -> dungeons, 2026-07-13).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
