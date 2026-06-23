# WORK_ORDER_294 — Forgemasters' Saga: 4 deep crafter Yarn + 3 reconciliation scenes

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 290 (QuestService), 291 (vendor Yarn base), 293 (tiers)
**Design source:** `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` §1–3, `LORE_ELARION_WEAPONSMITHING.md`

## Context
The four production crafters (Borin/Forge, Halvard/Armory, Pell/Lumbermill, Wren/Windmill) carry the deep
"Aegis and the Sin" saga across four acts, culminating in legendary crafting. Reconciliation scenes are
the emotional core (Act II shared meal at Brom's hearth).

## Goal
Author the deep saga content: act-gated dialogue for the four crafters + three two-NPC reconciliation scenes.

## Files to edit / create
- Extend `NPC_Borin.yarn`, `NPC_Halvard.yarn`, `NPC_Pell.yarn`, `NPC_Wren.yarn` with Act I–IV branches
  (gated on `QuestService` stages + village level + region clears).
- 3 reconciliation scene nodes (Borin↔Halvard truth; Pell↔forge over Heartwood; Wren's hearth gathering),
  staged at Brom's inn; low/no combat.
- Component objectives (Threefold Fold, Oathweld, Heartwood Bough, Last Pressing) = Keystones via QuestService
  (feeds WO-292 + WO-293/295).
- Optional one-line Sylas companion interjection at each Act hook (reuse companion system, WO-238/227).

## Acceptance criteria
- [ ] Each crafter's Talk deepens per Act; the three reconciliation scenes play and advance the saga.
- [ ] Components register as Keystones in QuestService; Act IV unlocks the legendary recipe (WO-293/295).
- [ ] Tone is bittersweet/hopeful (per design guardrail); no blue button (WO-110); no placeholder text.
- [ ] Quest state persists; brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't duplicate vendor base Talk from WO-291 — extend it.
