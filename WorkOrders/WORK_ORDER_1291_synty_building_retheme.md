# WO-1291 — Building re-theme: catalog + hand-placed storefronts onto Synty

**Status:** IN PROGRESS (2026-09-01: 30 of 33 addresses swapped + gated; 3 unmapped, scene storefronts + runtime visual proof outstanding)
**Minted:** 2026-09-01 (CLI, banner bumped 1289 -> 1293 in the same edit)
**Branch:** `feat/synty-art-retheme`   **Lane:** 3 of 4 (Synty art re-theme)
**Owner ruling 2026-09-01:** **FULL re-theme, everything Synty** (catalog + storefronts + props).
Lane 3 covers catalog + storefronts; lane 4 (WO-1292) covers environment/props.

---

## CURRENT STATE (verified 2026-09-01)

`Assets/Resources/Data/Canonical/structures-catalog.json` — **28 entries, 27 carrying art**, every
`visualPrefabPath` under `Structures/*`, served through **Addressables / the R2 CDN**:

```
tower_ground_archer  Structures/Tower_Wooden_Watchtower     wall_wood    Structures/Wall_Medieval_Wood
tower_ballista       Structures/Ballista_L1                 wall_stone   Structures/Wall_Medieval_Stone
tower_siege_tower    Structures/Ballista                    gate_stone   Structures/Gate_Medieval_Medium
tower_catapult       Structures/Catapult                    mine_crystal Structures/CrystalMine
healing_caravan      Structures/HealingCaravan              deco_torch   Structures/Torche_Wall
pet-house            Structures/PetHouse2                   workshop     Structures/ShopAndCrafting
```

Hand-placed storefronts baked into `Main_Castle_Overworld.unity`: `Blacksmith_Weapons_Storefront`,
`Forge_Armor_Storefront`, `Windmill_Food_Storefront`, `Lumbermill_Wood_Storefront`,
`Jeweler_Gems_Storefront`, `Marketplace_Monetization`, `ArcaneTower_MagicUpgrades`, `CastleBarracks`.

## THE REPLACEMENT ART

`Assets/Synty/PolygonFantasyKingdom/Prefabs/` (URP-native — see WO-1290 for the shader proof):

- **`Buildings/Presets/` — 26 `*_Optimized` prefabs**: Blacksmith, Church A/B, Tavern, Stables,
  Windmill, Tower, Hut x2, Shelter x2, Outhouse, Houses 01-10, Archway x2. Near-1:1 onto our storefronts.
- **`Buildings/House/` — 241 modular pieces** for anything a preset does not cover.
- **`SiegeEngines/` — 15 prefabs**: real art for `tower_ballista` / `tower_catapult` / `tower_siege_tower`,
  which currently point at polyperfect stand-ins.
- **`Castle/` — 348 prefabs** (WO-1290) for `wall_*` / `gate_stone`, so the catalog wall entries finally
  match the perimeter.
- **`Props/Banners/` — 43** for faction/ownership dressing.

## THE WORK

1. **Author a mapping table** — one tracked file, catalog id -> Synty prefab. Do NOT scatter path
   literals across builders (the duplicated-state failure class that produced the stale WO block,
   the hardcoded repo root, and the retired asmdef table — CLAUDE.md §2/§0/§5).
2. **Copy the referenced prefabs into the tracked Addressable/Resources path.** `Assets/Synty/` is
   gitignored (461MB, same policy as polyperfect) — only what the game references gets committed.
3. **Re-point the 27 `visualPrefabPath` values.** **Never rename a catalog `id`** — they are live save
   keys (memory `structure-role-enum-and-format-normalization`).
4. **Normalize by Y-height, not raw scale** — `repo.visualHeight` fit-to-height (DEF-208 / WO-751), and
   respect the `_heightCadence` note in the catalog. **Walls are deliberately excluded from narrowing**
   (`MASTER_CATALOG.md:86-87` — narrowing opens pathable gaps in saved wall runs).
5. **Re-theme the hand-placed storefronts** in the hub scene via the builder, never by hand-editing
   `.unity` (CLAUDE.md §3).
6. **Colliders + layer**: BoxCollider, `Structure` layer, so `IDamageableStructure` / tower LoS / nav
   carving keep working.

## SHIP GATE — THIS LANE CANNOT SHIP WITHOUT AN R2 PUSH (CLAUDE.md §16)

Every `Structures/*` swap rebuilds the Addressable content, and **bundle names are content-hashed —
every content build needs ITS OWN push. A push from a previous build can never cover it.** A missing
push fails SILENTLY: the APK installs, launches, plays, and shows placeholder buildings with no error
on screen. This has already happened three times (2026-08-18 / -19 / -20).

- [ ] Run **`tools\r2-ship.ps1`** — the ONE sanctioned path. Do not re-inline the push or the verify.
- [ ] Judge by **`R2_PUSH_OK` + `R2_PARITY_OK` on a FRESH log**, never the exit code.
- [ ] Never `adb install` a hand-built APK — that bypasses the whole gate.

## ACCEPTANCE CRITERIA

- [ ] All 27 catalog entries resolve to a Synty prefab; zero null loads (assert, do not eyeball).
- [ ] No catalog `id` changed. Save-compat proven by loading a pre-change save.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` + **`R2_PARITY_OK`** on FRESH logs.
- [ ] `RunCaptureHeadless` screenshots of the town from the standard angles, opened and looked at.
- [ ] Build-mode placement still seats every structure on the ground at the right height.

## DO NOT TOUCH

- `Assets/Generated/Terrain/**` (WO-1289). Castle perimeter geometry (WO-1290).
- Catalog `id` strings, `RepoProps.MaxStructureLevel` (the SINGLE level ceiling — never re-hardcode one).
- The cost baskets — regular structures are wood+iron only, magical are crystal-based (WO-947).
