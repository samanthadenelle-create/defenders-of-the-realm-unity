<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 727 — Recipe AI Settlements (Tiered BaseLayout Camps)

**Status:** READY TO IMPLEMENT  
**Priority:** P1  
**Silo:** World / Data  
**Depends on:** WO-726 (loop works on *any* camp)  
**Blocks:** WO-728, WO-729, WO-730  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** L  

---

## Goal

AI camps are **data recipes** (`BaseLayout` + garrison recipe + threat), not one-off scenes — **same shape future PvP will use**.

---

## Built already

- `BaseLayout` / `PlacedStructureData`
- `OutpostFoundationGenerator.Realize` (or equivalent realize path)
- `ArenaMode.UsePlayerCastle` (player layout as defender)
- `ArenaCatalog` seeded opponents
- `garrison-recipes.json` + `GarrisonController`
- `ProceduralSiegeArenaBuilder` + `ArenaNavMeshBaker` (flat plate)

---

## Gaps to close

1. Author **3–5 AI camps** as JSON recipes (easy → hard): walls, towers, core/heart, garrison.
2. **Realize onto flat siege plate** (not multi-level castle mesh) for reliable NavMesh.
3. Expose difficulty (`levelRange` / threat) in raid picker UI.
4. Honor structure **level** in Realize if still gapped (ARENA_SOLUTION WO-388b).
5. Document in RESULT: *“PvP opponent = same JSON from another player’s BaseLayout.”*

---

## Tasks

1. Pick storage site (StreamingAssets + Resources dual-copy if WebGL-loaded).
2. Author recipes; register in picker / `ArenaCatalog` or successor.
3. Verify troops path + attack structures/garrison (no mass fall-through).
4. Scale loot by threat on clear (hook existing loot tables).
5. DataRegression for recipe load non-empty.

---

## Acceptance

- [ ] Picker shows ≥3 distinct AI camps; layouts visibly differ.
- [ ] Troops path and engage on plate.
- [ ] Clear awards scaled loot by threat.
- [ ] Recipe load WebGL-safe (`CanonicalJson` / Resources mirror if needed).
- [ ] CompileGate + DataRegression green.

---

## Not in scope

- Fetch real player bases (WO-730).
- Hand-edit `SiegeArena.unity` (builder-owned only).
- Defend & watch (WO-729).

---

## Key files

- `Assets/_Modules/Village/Arena/ArenaCatalog.cs`
- `Assets/_Modules/Village/Arena/ArenaMode.cs`
- `Assets/Editor/ProceduralSiegeArenaBuilder.cs`
- `Assets/_Modules/Village/Arena/ArenaNavMeshBaker.cs`
- Garrison recipes under StreamingAssets/Resources
- BaseLayout realize path (`OutpostFoundationGenerator` or current equivalent)

---

## RESULT

`WorkOrders/WORK_ORDER_727_recipe_ai_settlements.RESULT.md`
