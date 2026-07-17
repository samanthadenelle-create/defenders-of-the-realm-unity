# WO-734 RESULT — Barracks Tier Copy Announces Unit Unlocks

**Status:** DONE (copy-only, data). Not gated/built/committed (per instruction).
**Date:** 2026-07-16

---

## What renders the tier effect text (SME finding)

- **Field shown = the tier's `effect` string** in `building-tiers.json` (building id `"barracks"`).
- Flow: `BuildingUpgradeVM.BuildCity()` sets `_effectById[id] = t.Effect ?? "";`
  - `D:\eoa\Assets\_Modules\Village\Buildings\Progression\BuildingUpgradeVM.cs:469`
- Exposed to the View via `EffectFor(id)`:
  - `BuildingUpgradeVM.cs:179-180`
- The perk-grid View reads it purely through `EffectFor(id)` (mvvm binding seam — the View never re-pulls the catalog). Each barracks tier is one tile ("Tier N - <Name>") whose effect sub-line is now the unlock announcement + stat mults.
- The tier `name` still renders as the tile title ("Tier 2 - Drill Yard", VM line 467); I left names unchanged and put the unlock in `effect` (the panel's descriptive line), which is the field the WO/panel surfaces.

## Copy edits (before -> after, barracks `effect` field)

| Tier | Name | Before | After |
|------|------|--------|-------|
| 1 | Muster the Barracks | `Opens recruitment drills` | `Footman and Archer ready. Opens recruitment drills` |
| 2 | Drill Yard | `Troop health +8%` | `Unlocks Spearman. Troop health +8%` |
| 3 | War College | `Troop damage +12%, health +10%` | `Unlocks Shieldguard. Troop damage +12%, health +10%` |
| 4 | Standing Army | `Troop damage +18%, health +18%` | `Unlocks Outrider. Troop damage +18%, health +18%` |
| 5 | Warhost | `Troop damage +26%, health +26%` | `Unlocks Battlemage. Troop damage +26%, health +26%` |
| 6 | Legion of Elarion | `Troop damage +38%, health +38%` | `Unlocks Echo Legionnaire. Troop damage +38%, health +38%` |

- Player-facing display names only (Spearman/Shieldguard/Outrider/Battlemage/Echo Legionnaire) — no raw `troop-*` ids. Matches program roster table exactly.
- **Stat mult `modifiers` numbers UNCHANGED** (all `troopDamageMult`/`troopHealthMult`/costs untouched). T1-T3 gold-research perks untouched.
- No `unlocksTroopIds` structured field added (copy-only path suffices; unlock MATH stays WO-733 via `TroopDef.UnlockBarracksTier`).
- No dialogue line added (optional deliverable; scoped out to avoid touching dialogues.json this pass).

## Dual-copy verification (md5 match)

Both files edited identically (Windows-path Edit tool, never mount/bash):
- `Assets/Resources/Data/Canonical/building-tiers.json`
- `Assets/StreamingAssets/Data/Canonical/building-tiers.json`

```
0a6bc89a8381ebc34112151d24595863 *StreamingAssets/Data/Canonical/building-tiers.json
0a6bc89a8381ebc34112151d24595863 *Resources/Data/Canonical/building-tiers.json
```
**MD5 MATCH.** Both parse as valid JSON.

## ASCII / gate

- All added copy is pure ASCII (used ". " separators, no em dash).
- Non-ASCII scan: 6 bytes total per file, both **pre-existing** em dashes (U+2014) — one in the arcane-tower "Healing Fountain" perk, one in the barracks `_comment` header. Neither is in a barracks tier `effect` string; none introduced by this WO.
- **No `.cs` files touched** (data-only) -> no brace/NUL code gate applicable. `BuildingUpgradeVM.cs` was READ only.

## Not touched (other WOs / agents)

- troops.json / TroopDef / TroopTrainingPanel (WO-732/733/735)
- `ff.barracks` flag (untouched — not flipped)
- unlock math, tier pricing, perk icons
- repair/camera/aura/store/dungeon
