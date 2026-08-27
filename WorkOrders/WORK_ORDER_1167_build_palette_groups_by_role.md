# WORK ORDER 1167 — The build palette groups itself by ROLE, so a new building needs data and not code

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).
Headless-verified (`COMPILE_GATE_OK` + `REGRESSION_OK 272/272` incl. the new `[palette-groups]`
oracle); rendered as WO-1172 Option A (inline dividers); owner felt-verify closes it.

**Minted:** 2026-08-24 (CLI), banner bumped 1167 → 1168 in the same edit.
**Provenance:** owner, 2026-08-24 — *"What about the buildings, is that done grouping by collector
producer storage defense? For build menu and UI"*. Answer at the time: **no**. This is that work.

---

## 1. What the palette does today, read from the data

`build-categories.json` maps five build verbs to catalog types. **Town is one flat list of 16 tiles**
— `catalogTypes: ["Resource","Collector"]`, no internal order, no headers:

| Verb | label | catalogTypes | locked ids |
|---|---|---|---|
| Town | Build Town | Resource + Collector | jeweler, mine_crystal, mill, lumbermill, collector_forge |
| Defense | Build Defenses | Tower + Gate | 4 |
| Walls | Castle Structures | Wall | wall_stone |
| Collector | Build Collectors | Collector | — |
| Support | Build Support | Support | healing_caravan |

⛔ **`type` CANNOT express the grouping the owner asked for, and this is the whole reason the work
exists.** `type` answers *what system owns this row*; the owner's question — producer / storage /
trade / civic — is about **what the building DOES FOR THE PLAYER**, and the two do not line up:

- **Farm and Lumber Mill are `Collector`; Silo, Lumberyard and Foundry are `Resource`** — so the
  producers and their stores are split down the middle by a field that has nothing to do with either.
- `market`, `forge`, `armorer`, `jeweler`, `workshop`, `barracks`, `pet-house` and `arcane-tower`
  are **all `Resource`**, i.e. one bucket holds the shop, three crafters, the troop building and two
  civic doors.

**Grouping by `type` would produce exactly the wrong picture.** The axis the owner wants already
exists and is already populated: **`role`** (WO-1161).

## 2. The rule this must not break

> **Owner, WO-1161:** *"if we add a building we do not want to have to manually code it"* ·
> *"we want building.newtype"*

So the grouping is **authored data, not a switch statement**. `StructureRole` is deliberately an
**open string vocabulary and NOT a C# enum** for this reason — an enum would freeze it and force a
recompile per building. Any implementation that hardcodes a role list in C# has re-broken the rule
this project already ruled on twice.

## 3. The design — one new authored block, zero new code paths

Add a `paletteGroups` array to the **Town** row of `build-categories.json`. Display-only; ordered;
roles listed by name:

```jsonc
"paletteGroups": [
  { "label": "Producers", "roles": ["wood_producer","food_producer","iron_producer"] },
  { "label": "Storage",   "roles": ["wood_store","food_store","iron_store"] },
  { "label": "Trade",     "roles": ["marketplace","weaponsmith","armorer","jeweler","crafting_station"] },
  { "label": "Civic",     "roles": ["barracks","echo_home","arcane"] }
]
```

**Rules the implementation must honour:**
1. **A role naming no group falls into a trailing "Other" bucket** — never dropped. A building that
   vanishes from the palette because someone forgot to author a group is worse than an ugly header.
2. **A group whose members are all locked/filtered renders NOTHING** — no empty header.
3. **Order within a group = existing palette order.** This change adds headers; it does not re-sort.
4. **Display only.** `buildType`, `catalogTypes` and `lockedIds` are untouched, nothing re-maps, and
   no catalog id changes — ⛔ ids are frozen save keys (`everBuiltStructureIds`, BaseLayout).
5. Keep the **Resources + StreamingAssets copies byte-equal** (the file's own standing rule).

## 4. ⚠ SIX TOWN ROWS HAVE NO ROLE, and they must be filled first

Read from the catalog today — these would all land in "Other":

| id | displayName | note |
|---|---|---|
| `barracks` | Barracks | the troop building; WO-1163 makes it the pure gold sink |
| `pet-house` | Echo Hollow | WO-1166 — home + wardrobe |
| `arcane-tower` | Cathedral of Magic | |
| `mill` | Mill | LOCKED in Town today |
| `lumbermill` | Lumber Mill | LOCKED — ⚠ **display-name collides with `collector_lumbermill`** |
| `mine_crystal` | Crystal Mine | LOCKED |

⚠ **`lumbermill` and `collector_lumbermill` both display "Lumber Mill".** One is locked, so no
player sees both — but this is the same crossed-naming cluster WO-1161 opened, and grouping will put
them side by side the moment `lumbermill` unlocks. **Resolve the name, or keep it locked, before
this ships.**

## 5. Acceptance

- [ ] Town palette renders headers in authored order; Defense / Walls / Collector / Support unchanged
- [ ] A building given a brand-new role string appears under "Other" **with no code change** — the
      test that proves the owner's rule is intact
- [ ] Locked ids stay filtered; no empty group renders
- [ ] Both JSON copies byte-equal; `structures-catalog.json` unchanged except the six added roles
- [ ] Oracle: assert every Town-eligible catalog row resolves to exactly one group or Other, and that
      the C# holds **no hardcoded role list** — the rule, pinned
- [ ] `REGRESSION_OK` + `UI_CAPTURE_OK` with the palette PNG actually opened (a compile-green palette
      proves nothing about a layout — see the "Price unavailable" truncation found on device)

## 6. Not in scope

Re-sorting tiles, re-pricing, the Smithy merge (WO-1163), Echo Hollow's verb (WO-1166), and the
`forge`/`armorer`/`workshop` display-name ruling (WO-1161). **This ticket adds headers to a list.**
