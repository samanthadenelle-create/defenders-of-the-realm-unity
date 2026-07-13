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
2. *(append further owner annotations from this walk as F8s arrive — one row per pill.)*

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
