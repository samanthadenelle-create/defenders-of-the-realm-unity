# WORK ORDER 724 — Barracks Live: Train → ArmyStorage

**Status:** READY TO IMPLEMENT (after WO-723)  
**Priority:** P0  
**Silo:** Buildings / UI / State  
**Depends on:** WO-723 — **DONE** (read RESULT only; Path A locked)  
**Blocks:** WO-726  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Related (roster ladder):** WO-732–737 — prefer green or in-flight before felt-pass  
**Effort:** M  
**Parallel-safe with:** WO-725  
**Queue:** **CoC implement START** — do not wait on re-opening 723  

---

## Goal

Player can unlock/use Barracks, train troops into the **persisted** army (`ArmyStorage`), and read cap / wounded state.

---

## Built already (reuse — do not rewrite)

| Piece | Path |
|-------|------|
| Army roster | `Assets/_Modules/Core/State/ArmyStorage.cs`, `PlayerTroop.cs` |
| Catalog / spawn | `Assets/_Modules/Village/Troops/TroopCatalog.cs`, `TroopFactory.cs`, `TroopDeployer.cs` |
| Train UI | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` |
| NPC entry | `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs` (gates on `FeatureFlags.Barracks`) |
| Dialogue open | `TroopDialogueCommands` / structure id `barracks` |

---

## Gaps to close

1. **Surface Barracks in the player settlement** (not only hidden baked `CastleBarracks`):
   - Flip path for `ff.barracks` + unhide **and/or**
   - Ensure `barracks` is a placeable Town-tab building with interact → training UI.
2. **Unlock rule** (pin one in RESULT):
   - **A (simple):** Village Tier ≥ N / Heart + founding complete.
   - **B (CoC):** Barracks is buildable; first place unlocks train.
3. **Economy seam:** train costs via existing `EconomyService` callbacks into `ArmyStorage`.
4. **UI:** training panel usable; army cap + wounded recovery readable.
5. **Flag:** keep default OFF until smoke green; testers use `PlayerPrefs "ff.barracks"=1`. Production default ON only at WO-731.

---

## Tasks

1. Confirm SaveSchema still round-trips `GameState.Army`.
2. Implement unlock + surface per pin (A or B).
3. Wire interact → `TroopTrainingPanel.Open` (no UXML; kit chrome only).
4. Verify train ×1 / ×5, cap, wounded cannot deploy (or show recovering).
5. Regression: `ff.barracks` OFF fully hides structure/NPC/dialogue.

---

## Acceptance

- [ ] Fresh save can reach Train UI without dev cheats (after unlock rule).
- [ ] Train ×1 / ×5 adds `PlayerTroop`s; save/load preserves roster.
- [ ] Cap enforced; wounded troops blocked or clearly recovering.
- [ ] `ff.barracks` OFF still fully hides feature.
- [ ] CompileGate + brace/NUL clean; DataRegression green if catalogs touched.
- [ ] Optional fleet: train one troop.

---

## Not in scope

- Deploy HUD (WO-726).
- ArenaMode combat / PvP.
- Hand-edit `.unity` scenes.

---

## Key files

- `Assets/_Modules/Core/FeatureFlags.cs` (`Barracks`)
- `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs`
- `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs`
- `Assets/_Modules/Core/State/ArmyStorage.cs`
- Build palette / catalog row for `barracks` if placeable

---

## RESULT

`WorkOrders/WORK_ORDER_724_barracks_live_train_army.RESULT.md`
