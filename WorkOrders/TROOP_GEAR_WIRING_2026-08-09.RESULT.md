# Troop gear wiring — RESULT (2026-08-09)

**Status:** CODE + DATA DONE. Compile/regression gate **blocked** by open Unity editor (project lock).  
**Scope:** Specialist resolution — unique troop bodies + weapons/offhand attach so roles do not share bare skins.

## What shipped

### Code
| File | Change |
|---|---|
| `Assets/_Modules/Village/Troops/TroopGearApplier.cs` | **NEW** — attaches `weapon`/`offhand` Resources prefabs to Humanoid RightHand / LeftHand (bow → LeftHand); strips colliders; primitive fallback if mesh missing; `FlowTrace` on attach/miss. |
| `Assets/_Modules/Village/Troops/TroopDef.cs` | `weapon` + `offhand` optional `[JsonProperty]` fields. |
| `Assets/_Modules/Village/Troops/TroopFactory.cs` | Calls `TroopGearApplier.Apply` after animator bind (bones resolve). |
| `Assets/Editor/SupercyanResourceWire.cs` | Bodies + gear maps; menu **Defenders/Troops/Wire Supercyan Bodies And Gear**; batch `RunBatch`. |

### Data (dual-copy, byte-identical)
`Assets/StreamingAssets/Data/Canonical/troops.json`  
`Assets/Resources/Data/Canonical/troops.json`

| Troop | Model | Weapon | Offhand |
|---|---|---|---|
| Footman | `SC_Footman` | `TroopGear/Sword` | — |
| Archer | `SC_Archer` | `TroopGear/Bow` | — |
| Spearman | `SC_Footman` | `TroopGear/Spear` | — |
| Shieldguard | `Knight` | `TroopGear/Sword` | `TroopGear/Shield` |
| Outrider | `SC_Barbarian` | `TroopGear/AxeRight` | — |
| Battlemage | `SC_Mage` | `TroopGear/Staff` | — |
| Echo Legionnaire | `Knight` | `TroopGear/Sword` | `TroopGear/Shield` |

### Resources art (path load)
- **Bodies:** `Resources/Heroes/SC_Footman`, `SC_Archer`, `SC_Barbarian`, `SC_Mage` (+ existing `Knight` FBX/package).
- **Gear:** `Resources/TroopGear/{Sword,Spear,Bow,Shield,Staff,AxeRight,Mace}` — mirrored from Supercyan `Base/High Quality` (file copy; Unity will assign meta on next editor import).

## Gates (blocked this session)

```
Unity process Id=26988 held project lock.
run-unity-method.ps1 refused CompileGate + wire batch.
```

**Owner / next CLI when editor is closed:**

```powershell
# Optional re-wire (strips gear colliders/rigidbodies cleanly):
powershell -ExecutionPolicy Bypass -File ./run-unity-method.ps1 -Method DeNelle.Editor.SupercyanResourceWire.RunBatch -LogName supercyan-wire.log

powershell -ExecutionPolicy Bypass -File ./run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log
# expect COMPILE_GATE_OK

powershell -ExecutionPolicy Bypass -File ./run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log
# expect REGRESSION_OK <n>/<n> suites  (TroopRosterRegression still green)
```

Brace balance: OK on all touched `.cs` (pre-gate).

## PO felt-check (code alone cannot close)
1. Barracks train tray — 7 portraits still resolve.
2. Deploy Footman / Archer / Spearman / Outrider / Battlemage — distinct silhouettes + held gear.
3. Shieldguard + Legionnaire show sword + shield.
4. No T-pose / slide regression (animator still binds before gear).
5. If a hand is empty but JSON has weapon → check Player.log for `[Flow:TroopGear]` Warn (missing Resources path or non-humanoid avatar).

## Not in this specialist pass (still open elsewhere)
- Raids: PO Phase 0 play; `rewardMultiplier` honesty; IronBastion keep/drop; props art.
- Dungeon: baker still PathPartial/ports until stair connector wiring (WO-923) lands end-to-end.
