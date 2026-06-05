# BattleATB — `DeNelle.BattleATB`

Active-Time-Battle combat system. Split design: deterministic pure-C# engine
(`Engine/`) + Unity-facing controllers at root. Has its own test assembly.

## Layout

- **Root (Unity layer):** `ATBCombatManager`, `BattleController`, `BattleHud`,
  `BattleVfx`, `ATBBackgroundController`, `AtbCombatantSwapper`, `AtbControlModeStore`
- **`Engine/` (pure C#, no UnityEngine):** `BattleState`, `Combat`, `Turn`,
  `Actions`, `Ai`, `Targeting`, `Rng` (seeded/golden-vector tested),
  `BattleScaling`, `Types`, `Defs`, `CombatantDefSO`
- **`State/`:** `ATBRuntimeState` (carries party/encounter data across scene loads)
- **`Tests/`:** unit tests per engine file + `RngGoldenVectorTest`

Scene: `Assets/Scenes/ATBBattle.unity`. Debugging guide: root `ATB_DEBUGGING_GUIDE.md`.

> Maintenance: update this README when files are added/removed.
