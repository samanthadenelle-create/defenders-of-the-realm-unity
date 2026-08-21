# WORK ORDER 999 — RESULT

**Status:** IMPLEMENTED (HUD feel + data v6 retune) — owner felt-close still open
**Date:** 2026-08-15  
**Design:** `docs/design/MOBILE_CLASS_RESOURCE_ECONOMY.md`

## Creative lock (Warcraft / StarCraft + mobile)

- **Physical Q basics stay free; Mage Fireball spends Mana** — the 2026-08-21 felt retune closes rapid spell spam.
- **W/E/R spend the pool** — secondary gate like WC mana on spells.
- **Three identities:** Mana / Vigor / Focus.
- **Ranger Focus rebuilds by free basics** (Quick Shot + melee on-hit).
- **Universal skills free** — shared escape/heal/dash.

## Shipped numbers (abilities.json v5, dual-copy identical)

| Class | Pool | Regen | On-hit | Kit Q/W/E/R |
|-------|-----:|------:|-------:|-------------|
| Mage | 24 Mana | 0.6/s | 0 | 3 / 5 / 7 / **12** |
| Knight | 12 Vigor | 2.0/s | 0 | 0 / 3 / 4 / **7** |
| Ranger | 15 Focus | 0.8/s | **+1.5** | 0 / 4 / 5 / **9** |

## Code

- `HeroAbilities.CastResolved` — free non-support casts restore `OnHitRestore` (ranger Focus).
- `AbilitySlotRecord` + producer — `ManaCost` + `Affordable`.
- `HudKitController.OnAbilities` — cost digit on face; dim + non-interactable when unaffordable.
- `HeroVitals` — `ResourceDisplayName` on nameplate (`Grom Lv 1 · Mana`).

## Deferred (by design)

- Barracks structural Vigor (R7 later).
- Owner felt-close on wave combat.

## Verify

- Play mage: sustained Fireball permits eight rapid casts and refuses the ninth; W/E drain bar; R needs half the pool; regen remains visible but no longer erases spend.
- Play ranger: Quick Shot fills Focus; Storm empties it.
- Cost digits on every costed face, including Mage Q; unaffordable faces dim and disable.
