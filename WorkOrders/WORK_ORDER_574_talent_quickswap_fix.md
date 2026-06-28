# WORK ORDER 574 — Talent panel: quick-swap assignment + passive/active clarity

Status: IMPLEMENTED (edit-only agent; not gated/committed — orchestrator batch-gates per §11)
Date: 2026-06-28
Silo: Combat/AI + UI (file-disjoint from world/scene lanes)
Source: Owner felt-test screenshot — "Garran (Knight) Skills" panel.

## Owner felt-bug (verbatim intent)
- Wisdom 2188 / 13 SP: every node shows **Owned** (all unlocked).
- Right pane: SELECTED TALENT = Spear Thrust (Owned) + QUICK-SWAP (1-4), slot 1 = Throwing Spear.
- "Only the circled one [Spear Thrust] allows the CONFIRM to turn green; after [confirming]
  nothing happens."

## RCA (data-grounded — from the catalog data + the in-code FlowTrace markers)

**Why only ONE node turns CONFIRM green.**
`CanConfirm => CanCommit || SelectedIsAssignable` (HeroSkillTreeVM). With everything owned there
is no learn-plan, so `CanCommit` is false → CONFIRM tracks `SelectedIsAssignable`.
`SelectedAssignAbilityId` (HeroSkillTreeVM.cs ~L622) is non-empty ONLY when the node carries a
top-level `abilityId` that resolves in `AbilityCatalog.FindById`. In `hero-talents.json` the
**knight tree has exactly one such node**: `knight.t1n2` "Spear Thrust" → `abilityId:
"knight.ranged-poke"` (→ Throwing Spear in the `knight-skills` pool). Every other Knight node is a
**passive** (stat / passive / aura / active-emergency) with NO top-level `abilityId` — their
ability references live only inside `effect.ability`, which the assign path does not read. So only
Spear Thrust is "assignable" → only it lights CONFIRM. This is correct given the data; the felt
problem is the panel never EXPLAINS that the rest are passives, so they read as dead.

**Where the assignment died ("nothing happens").** Two stacked failures:
1. *Confirm dead-end.* Spear Thrust was already in slot 1. `ConfirmOrAssign` → `FirstAssignSlot()`
   returns the first EMPTY slot (a different slot) → `AssignableSkillBar.Assign` **rejected the
   duplicate** (AssignableSkillBar.cs old L81-88 "already on the bar") → status set but the bar
   was visually unchanged = "nothing happens."
2. *Dead bar in battle (the deeper root).* Even a SUCCESSFUL assignment lands in
   `AssignableSkillBar` (bottom-middle HUD). `BattleHud9Zone.OnExtraTapped` did **not** cast — it
   only logged `"...not yet wired — 4-slot engine"` (captured in-code). `HeroAbilities` casts the
   4 Q/W/E/R slots from `HeroLoadout`; the assignable bar was render-only. Per commit `aad13778`
   the loadout chooser now writes talent skills to this hot-swap bar — but nothing ever cast from
   it, so every assigned skill was cosmetic.

## Fix

**Make the hot-swap bar genuinely castable (usable in battle).**
- `HeroAbilities`: extracted the cast core into `CastResolved(def, castVariant)`; added
  `TryCastExtra(abilityId)` (resolves def via `AbilityCatalog.FindById`, gates on a NEW per-id
  cooldown store `_extraCooldown` + mana, runs the same anim/face/effect core as Q/W/E/R) and
  `ExtraCooldownRemaining`. Additive — Q/W/E/R behaviour unchanged.
- `BattleHud9Zone.OnExtraTapped`: a filled extra slot now calls `HeroAbilities.TryCastExtra` and
  traces `fired=<bool>`.

**Kill the confirm dead-end + enable tap-to-move.**
- `AssignableSkillBar.Assign`: changed duplicate-REJECT → MOVE (vacates the old slot), added
  `SlotOf`. Re-tapping a different slot relocates a skill; no silent "already on the bar".
- `AssignableSkillBarAccess.SlotOf` exposed for the VM.
- `HeroSkillTreeVM.ConfirmOrAssign`: when the selected skill is already on the bar, report WHERE
  ("already in quick-swap N — tap a slot to move it") instead of silently failing.

**CONFIRM role (consistent, no fake confirm).**
- `CanConfirm => CanCommit || (SelectedIsAssignable && !SelectedAlreadyOnBar)`. CONFIRM lights ONLY
  when it has a real action: commit a learn-plan, or equip an owned active skill not yet on the bar.
  Slot-tap remains the instant assign/move path. A passive or an already-equipped skill leaves
  CONFIRM disabled (no fake green).

**Passive vs active clarity (selection always explains itself).**
- `SelectedNodeStateLine` for an owned node now reads:
  - Active skill: "Owned · Active — tap a slot (1-4) to equip" / "...equipped in quick-swap N (tap a slot to move)".
  - Passive: "Owned · Passive — always active (no slot needed)".

## Files modified
- `Assets/_Modules/Village/Hero/HeroAbilities.cs`
- `Assets/_Modules/Village/Arena/BattleHud9Zone.cs`
- `Assets/_Modules/Village/Hero/AssignableSkillBar.cs`
- `Assets/_Modules/Village/Hero/AssignableSkillBarAccess.cs`
- `Assets/_Modules/Village/Talents/HeroSkillTreeVM.cs`

Brace check: all 5 balanced.

## Owner decisions to confirm
1. **V1 has ONE assignable active Knight skill (Throwing Spear); all other Knight talents are
   passives** — intended, or author more `abilityId` skill nodes in `hero-talents.json`?
2. CONFIRM is intentionally NOT lit for passives / already-equipped skills (no fake confirm).
   Acceptable, or prefer a different CONFIRM affordance?
3. Extra-bar casting uses a per-id cooldown + the shared mana pool, generic cast clip (no per-id
   cooldown RING rendered on the extra tiles yet — polish follow-up if wanted).
