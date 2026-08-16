# WORK ORDER 997 — RESULT

**Status:** DONE (implementation) — residual feel + owner rulings → **WO-999**  
**Verified at source:** 2026-08-15 (CLI SME pass)

## What shipped (code + data)

### Data (`abilities.json` v4 — Resources ≡ StreamingAssets byte-identical)
| Class | Resource | Pool | Regen | On-hit | Kit costs Q/W/E/R |
|-------|----------|-----:|------:|-------:|-------------------|
| Mage | Mana | 20 | 1.0/s | 0 | 0 / **5** / **7** / **10** |
| Knight | Vigor | 10 | 1.5/s | 0 | 0 / **3** / **4** / **6** |
| Ranger | Focus | 12 | 0.6/s | **+1** | 0 / **4** / **5** / **8** |

Non-ultimate pool skills (mage/knight/ranger-skills) also carry non-zero costs (oracle Case 3).  
`universal.*` stays **0** by design (shared free utilities).

### Plumbing (`HeroAbilities`)
- `ApplyClassResource` seeds base pool/regen/displayName/onHit from catalog
- Single cost reader still `ManaCostOf` (Cathedral mult folds for mage only)
- `RestoreMana` + melee on-hit Focus (`PlayerAttackController` when `OnHitRestore > 0`)
- Town full restore unchanged (`SafeZoneRecovery`)

### Bar legibility (§3b)
- `HeroVitalsModel` carries `ManaExact` / `MaxManaExact` floats
- Producer epsilon-gates floats (not ints only)
- `HudKitController` lerps fill + spend flash on downward jump

### Oracle
- `ClassResourceRegression` registered as `[class-resource]` in `DataRegression`
- 4 cases: resource blocks · costs fit pool · costed non-ultimate · dual-copy identical

## Explicitly NOT in this RESULT (→ WO-999)
1. Ability-face **cost pips** + unaffordable darken (spec §3.4 UI)
2. Bar / plate **label** reading class `ResourceDisplayName` (still “MP” chrome)
3. **Ranger Quick Shot** Focus restore (cast basic, not melee path only)
4. Owner ruling: costed Q tier vs free basic forever
5. Structural knight/ranger building keys (Barracks vigor — deferred)
6. Owner felt-verify that the economy *feels* right in play

## Correction to the pre-997 brief
The brief that said “every cost is 0 against a 10 pool / only ultimates matter” described **pre-v4** data.  
At HEAD that sentence is **false** — W/E mid-skills cost real pool against the new ceilings.
