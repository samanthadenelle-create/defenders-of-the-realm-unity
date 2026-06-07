# WORK ORDER 283 — RESULT

**Status:** DONE ✓ — committed `27e425e`, pushed to `feat/tower-core-loop`.
**Date:** 2026-06-06
**Verified by:** CLI (batchmode import + bake + CompileGate + Windows build).

---

## What landed

- **162 FBX imported** under `Assets/Action/{Shared(15),Knight(99),Ranger(13),Wizard(15),Enemies(20)}/`
  as retargetable Humanoid clips (LFS). Reimport reported **199 Action FBX → Humanoid,
  0 non-Human left behind** (162 new + ~37 legacy flat clips, all Humanoid).
- **`ActionClipImporter.cs`** — now enforces **Optimal** compression (rot/pos/scale
  error 0.5) across the whole `Assets/Action/` library, in `OnPreprocessModel` and both
  batch-fix methods. Existing Humanoid + in-place XZ root + loop-on-idle/walk/run rules
  unchanged. Convention lives in code → every future drop conforms automatically.
- **`HeroAnimatorFactory.cs`** — added **Cleric** spec; widened clip lookup to per-type
  subfolders (type folder first, then `Shared/`, legacy flat fallback). Shared locomotion
  now sources `Shared_Idle / Shared_Walk_Forward / Shared_Run_Forward / Shared_Victory_Pose`.
- **4 hero controllers built**, all clean, no missing-clip warnings:
  | Class | Locomotion | Cast clip wired | Victory |
  |---|---|---|---|
  | Knight | 3 clips | `standing melee attack horizontal` (+UpperBody) | ✓ |
  | Mage | 3 clips | `Wizard_Spell_Cast` (+UpperBody) | ✓ |
  | Ranger | 3 clips | `Ranger_Aim_Idle` (+UpperBody) | ✓ |
  | **Cleric** | 3 clips | `Wizard_Heal` (+UpperBody) | ✓ |

## Knight clips wired this pass (large set, 99 available)

Locomotion (Shared) + **one primary attack** (`standing melee attack horizontal`,
fallback `standing melee combo attack ver. 1`) + WO-218 upper-body layer + Victory.
**Deferred to a follow-up WO:** full sword-and-shield combo trees, blocks, draws/sheaths,
taunts, directional reacts — the remaining ~95 Knight clips are imported and available,
just not yet wired into states/blends.

## Gates

- **CompileGate.Run → `COMPILE_GATE_OK`** ✓
- Brace balance: ActionClipImporter 31/31, HeroAnimatorFactory 46/46 ✓
- **Windows player build-verify → SUCCESS** (`Builds/Windows/DefendersOfTheRealm.exe`) ✓

## Deviation / flag — enemy injured set

WO §4.5 asked to wire the enemy animator to `Shared/` + `Enemies/`. The 20 injured
clips ARE imported as Humanoid Action clips and available. **Wiring was intentionally
NOT done** this pass: the live DTT runtime enemies are **Generic KayKit rigs**
(`EnemyAnimatorSetup` builds Generic shared controllers); driving them with a Humanoid
"injured" controller would cross Generic↔Humanoid (documented landmine — would break
enemy locomotion/attack for no gain). The injured set is intended for the **Humanoid
People-orc family** — that wiring is a separate, play-verified follow-up. Flagged per
the owner's best-practice-pushback standing instruction.

## Smoke test (acceptance §6 visual checks) — PENDING owner/Tricia

Headless build is clean and controllers are valid, but the "no T-pose / no slide,
each hero idles-walks-runs + casts" visual checks require an interactive play session.
Ready for a playtest pass.
