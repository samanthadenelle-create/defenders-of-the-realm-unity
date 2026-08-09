# WORK ORDER 933 — Siege Catapult troop (CoC scarcity + WC Demolisher)

**Status:** IMPLEMENTED (2026-08-09)  
**Lane:** Village / Troops / Raids  
**Product lens:** Clash of Clans scarcity + Warcraft siege (structure-first, escort tax)

## Intent

Add a single **offensive siege machine** so the player can peel raid towers from **standoff range**.  
Risk/reward: **heavy cost, fragile, slow, max 1 owned**. Outrider remains the **rush** answer; catapult is the **snipe** answer (both T4).

## Locked rulings (preferred guidance)

| Axis | Ruling |
|---|---|
| Cap | `maxOwned: 1` — roster + in-flight train; **wounded still counts** |
| Targeting | Prefer Hostile **structures** (`IDamageable` + `IDamageableStructure`); else nearest unit |
| Damage bias | `structureDamageMult: 2.0`, `unitDamageMult: 0.55` |
| Range | 26 (beats T1 ~14; contested vs enemy catapult tower ~28) |
| Unlock | Barracks **T4** with Outrider |
| Art | Machine path `Structures/Catapult` (not Supercyan humanoid) |
| Name | `troop-catapult` / **Siege Catapult** (never collide with defensive `tower_catapult`) |

## Stats (authored)

| Field | Value |
|---|---|
| slots | 4 |
| maxHp | 50 |
| attackDamage | 48 (×2 structure / ×0.55 unit) |
| attackCooldown | 2.5 |
| attackRange | 26 |
| moveSpeed | 2.0 |
| huntScanRadius | 30 |
| cost W/I/F | 320 / 280 / 80 |
| buildSeconds | 600 |
| unlockBarracksTier | 4 |

## Files

### Code
- `TroopDef.cs` — `maxOwned`, `structureDamageMult`, `unitDamageMult`; model path docs
- `TroopController.cs` — structure-prefer hunt + damage mult (role `siege`)
- `ArmyStorage.cs` — `CountOfDef`; optional `maxOwnedOf` on `CanTrain`
- `BarracksService.cs` — enforce maxOwned on `EnqueueTraining` + in-flight count
- `TroopFactory.cs` — full Resources path, siege collider/agent, skip gear/animator
- `TroopRosterRegression.cs` — 7 → 8 + siege asserts
- `TroopTrainingPanel.cs` — comment only

### Data (dual-copy both trees)
- `troops.json` — 8th row
- `troop-upgrades.json` — flat reach curve (anti map-artillery)
- `barracks.json` — L4 unlocks `troop-outrider` + `troop-catapult`
- `building-tiers.json` — T4 effect names Siege Catapult

## Acceptance

- [x] Catalog loads 8 troops; dual-copy hash match
- [x] T4 unlock + announce copy includes Siege Catapult
- [x] Train refuses second catapult while one owned/wounded/in-flight
- [x] Siege hunt prefers structures; unit fallback exists
- [x] Regression `TroopRosterRegression` green under DataRegression
- [ ] PO felt: one escorted catapult peels a tower line; naked dies; second train blocked

## Not in scope
- Projectile rock VFX (instant damage V1 OK)
- Scrap-to-replace while wounded
- Walk-in overworld siege pathing polish
