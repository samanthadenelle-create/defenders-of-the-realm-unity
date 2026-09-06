# ART DELIVERY 2026-09-06 — Manage UI asset drop (`assets.zip`)

**Delivered by:** the owner, as `assets.zip`, 2026-09-06.
**Extracted to (NOT in the repo):** `%LOCALAPPDATA%\Temp\claude\D--eoa\a2f63a40-…\scratchpad\assets\`
**Status:** identification + mapping only. **Nothing has been copied, converted or imported.** No code or
catalog was edited.

⚠ **Everything below was measured this session.** Dimensions and alpha come from `System.Drawing` reads of the
delivered files; catalog ids come from opening
`Assets/Resources/Data/Canonical/structures-catalog.json` and `…/troops.json`; existing-art paths come from
enumerating `Assets/Resources/`. Anything not measured is marked as such.

---

## 0. The headline

14 files. **Three unique contact sheets** (one of the four is a byte-for-byte duplicate) plus **ten individual
1254×1254 assets**.

Of the ten individuals, **one is a building (the Heart of Elarion) and nine are UI chrome** — five state
medallions and four tile frames. **The count of confident catalog-id matches is ZERO**, and that is the honest
number: nine of the ten pieces have no catalog axis to match against, and the tenth (the Heart) has no entry in
`structures-catalog.json`. See §2.

This delivery is **UI chrome, not the tiered portrait set** that `docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md`
asked for. None of that document's 62 files are here. See §5.

---

## 1. The ten individual assets

All ten are **1254×1254 PNG**, corner alpha `0` (transparent background **confirmed**, not assumed —
`GetPixel(0,0).A = 0` on all ten).

| # | Source filename | What it depicts | Proposed canonical name | Catalog id | Existing art it replaces |
|---|---|---|---|---|---|
| 1 | `…09_54_34 AM (1).png` | **Heart of Elarion** — gothic gold-and-blue reliquary, faceted glowing blue crystal heart in a tracery arch, banners, stepped stone plinth | `building-heart.png` | **NO CONFIDENT MATCH** — see §2 | **NEW.** No heart portrait exists anywhere under `Assets/Resources/Portraits/` |
| 2 | `…09_54_35 AM (2).png` | Gold **padlock** on a cracked dark stone medallion, gold ring + 4 cardinal points | `status-locked.png` | n/a (UI chrome) | **NEW** |
| 3 | `…09_54_35 AM (3).png` | Green gem **up-arrow** on a dark green medallion | `status-available.png` | n/a (UI chrome) | **NEW** |
| 4 | `…09_54_35 AM (4).png` | Gold **hourglass**, blue glowing sand, on a blue-lit medallion | `status-inprogress.png` | n/a (UI chrome) | **NEW** |
| 5 | `…09_54_35 AM (5).png` | Gold **crown + laurel wreath** on a dark medallion | `status-max.png` | n/a (UI chrome) | **NEW** |
| 6 | `…09_54_36 AM (6).png` | Gold **list/queue lines with a red dot** on a dark medallion | `status-queue.png` | n/a (UI chrome) | **NEW** |
| 7 | `…09_54_36 AM (7).png` | Square **gold frame, opaque dark slate fill**, mitred corner studs | `frame-tile.png` | n/a (UI chrome) | **NEW** |
| 8 | `…09_54_36 AM (8).png` | Square **glowing gold frame, opaque dark fill**, corner sparkles, glow bleeding outside the frame | `frame-selected.png` | n/a (UI chrome) | **NEW** |
| 9 | `…09_54_36 AM (9).png` | Square **grey stone frame with a gold inner line, hollow centre** | `frame-locked.png` | n/a (UI chrome) | **NEW** |
| 10 | `…09_54_37 AM (10).png` | Square **gold-and-blue frame with blue glow and star cabochons, hollow centre** | `frame-max.png` | n/a (UI chrome) | **NEW** |

**Confidence notes, stated rather than buried:**

- **#1–#6 are high confidence.** Each is an enlargement of a labelled tile in the contact sheet's
  `08_STATUS_ICONS` row, and the five states map **1:1 onto the five states canon requires** —
  `00_MANAGE_REDESIGN_CANON.md` §7 lists Available / Locked / In progress / Queue blocked / Max upgrade track
  as the required model-owned concepts. That is a strong corroboration, not a coincidence.
- **#7–#10 are moderate confidence.** They are identified by colour and by position in the sheet's
  `09_OTHER_UI_ELEMENTS` row (`frame-tile`, `frame-selected`, `frame-locked`, `frame-max`), not by any label
  baked into the file. If the intent were `frame-locked` = the blue one, only the two names swap.
- **Measured set inconsistency:** centre alpha is **opaque (`A≈253`) on #7 and #8** but **fully transparent
  (`A=0`) on #9 and #10**. So two of the four frames carry a slate plate and two are hollow rings. If these are
  meant to be interchangeable states of one tile, they are not currently interchangeable — the tile art behind a
  locked/max frame would show through where it does not behind a normal/selected frame. Worth a decision before
  import.
- **#8's glow bleeds outside the frame rect.** It will not 9-slice to the same border rect as #7. Either give it
  its own slice or accept a larger footprint for the selected state.

---

## 2. Why the Heart is "NO CONFIDENT MATCH"

`Assets/Resources/Data/Canonical/structures-catalog.json` holds **28 entries** (read this session). Their ids:

```
tower_ground_archer  tower_ballista  tower_siege_tower  tower_catapult
wall_wood  wall_stone  gate_stone  mine_crystal  healing_caravan  deco_torch
pet-house  workshop  market  mill  lumbermill  forge  armorer  jeweler
arcane-tower  tower_arcane_spire  collector_farm  collector_lumbermill
collector_forge  barracks  lumberyard  foundry  silo  repair_default
```

**None of them is the Heart.** The Heart is authored separately in
`Assets/Resources/Data/Canonical/heart.json` (maxHp / ringRadius / three damage phases) and owned at runtime by
`HeartController` — it is not a buildable structure entry, so there is no structure id to assert. Asserting one
would be exactly the kind of invented id that later becomes code.

This matters more than usual right now: `00_MANAGE_REDESIGN_CANON.md` §6 makes **Heart Level the visible realm
progression spine** and says *"The Heart itself must be upgradeable through a real model/service path."* If the
Heart is going to appear as a Manage tile, **someone has to decide what its id is** — that is a design decision
for the owner, not something to infer from art.

Also note: the Heart tile is labelled `building_heart.png` on **Sheet C only**. It does **not** appear on Sheet A,
whose convention this document recommends. The proposed name `building-heart.png` is therefore an
**extrapolation** of Sheet A's prefix onto a tile Sheet A does not contain — flagged so nobody reads it as
delivered canon.

---

## 3. The four 1536×1024 sheet files — three unique, one exact duplicate

MD5, measured this session:

| File | MD5 | Verdict |
|---|---|---|
| `ChatGPT Image Sep 6, 2026, 09_49_43 AM.png` | `8CBB9264CE20FA07D793C8ED5616C91C` | **Sheet A** |
| `ChatGPT Image Sep 6, 2026, 09_52_23 AM (1).png` | `8CBB9264CE20FA07D793C8ED5616C91C` | **byte-identical duplicate of Sheet A** — discard |
| `ChatGPT Image Sep 6, 2026, 09_52_23 AM (2).png` | `82639E23DC487C1CD9EB3C01B02695B8` | **Sheet B** — distinct design |
| `ChatGPT Image Sep 6, 2026, 09_52_23 AM (3).png` | `D4DDE3014D563A32047E1A4EC40FAACA` | **Sheet C** — distinct design |

They are not variants of one layout; they are **three different pitches with three mutually incompatible naming
schemes**:

| | Sheet A (`09_49_43`) | Sheet B (`09_52_23 (2)`) | Sheet C (`09_52_23 (3)`) |
|---|---|---|---|
| Building naming | `building-lumber-mill.png` | `bld-lumbermill.png` | `building_lumbermill.png` |
| Troop naming | `troop-footman.png` | `troop-footman.png` | `troop_footman.png` |
| Resource naming | `res-wood.png` | `icon-wood.png` | `icon_wood.png` |
| Status naming | `status-locked.png` | `badge-locked.png` | `icon_locked.png` |
| Heart tile | absent | absent (`bld-heart.png` present) | `building_heart.png` |
| Research schools | 4 (cathedral/armorer/forge/barracks) | 4 (`res-arcane/defense/weapons/army`) | 4 (`research_arcane/shields/weapons/tactics`) |

**Recommendation: Sheet A is the convention.** Two independent reasons, both checkable:

1. `Assets/Resources/RpgUi/troop/` already contains exactly `troop-archer.png`, `troop-battlemage.png`,
   `troop-catapult.png`, `troop-echo-legionnaire.png`, `troop-field-cleric.png`, `troop-footman.png`,
   `troop-outrider.png`, `troop-shieldguard.png`, `troop-spearman.png` — **Sheet A's scheme, already on disk**,
   and matching all nine ids in `troops.json` one-for-one.
2. The ten delivered individual assets follow Sheet A's `status-*` / `frame-*` vocabulary, not B's `badge-*`
   or C's `icon_*`.

**Record the other two schemes so nobody imports under `bld-` or `building_`.** Mixing them is how a loader
silently falls back forever.

⚠ **The sheets are a SPEC, not cuttable source.** They are 1536×1024 composites, so each tile is roughly 60–90 px
— far below any usable size — and several labels are AI-garbled (`building-sky-ball-lista.png`,
`building-stone-gaz-pe.png`, `R6_STUER ICONS`, `badge-loar-brg.png`, `treearch_arcane.png`). Read them for intent
and inventory; never trace a filename off them verbatim.

---

## 4. IMPORT NOTES

**Source size vs. target size — these are 4–8× too big.**

| Set | Measured dimensions | Measured size |
|---|---|---|
| **Delivered individuals** | **1254×1254** | **0.85–2.0 MB each** |
| `Assets/Resources/Portraits/Buildings/` (all **26** files) | **uniformly 1024×1024** | 0.6–0.7 MB |
| `Assets/Resources/Portraits/` (root, mixed legacy) | `784×1168` for the tower ladders; `1254×1254`, `1272×1237`, `1451×1084`, `568×861`, `512×512` elsewhere | 0.3–2.8 MB |

*(The `Portraits/Buildings/` count is **26**, not the 25 quoted in
`docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md` — measured by enumerating the folder. Minor, but the
request doc's number is off by one.)*

- **The tier-aware folder is uniformly 1024×1024** — that is the established target for a building portrait, and
  the delivered Heart should be resampled to it rather than imported at 1254².
- **The nine chrome pieces are far larger than any UI icon needs.** `ART_REQUEST` §0 records the actual display
  sizes: **112 px** for the rail row and **~150 px** for the card portrait. A state medallion is smaller still.
  256×256 would be generous for `status-*`; the frames want to be authored/sliced at the size they are drawn at,
  not downsampled from 1254². Import at 1254² and every one of these costs ~6 MB of VRAM uncompressed for a
  112-px badge.
- **The root folder is a mess of resolutions and formats** (PNG and JPG mixed — `Healing_Caravan.jpg`,
  `storage_food.jpg`, `farm.jpg`, `barracks.jpg` …). Author new work as PNG.

**⚠ TWO PORTRAIT CONVENTIONS, ONLY ONE IS TIER-AWARE** (per `docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md`
§0, re-confirmed against disk this session):

| Folder | Tier-aware? | Reached by |
|---|---|---|
| `Assets/Resources/Portraits/Buildings/<slug>-<level>.png` | **YES** | `ManageScreenPanel.LoadManageBuildingSprite` |
| `Assets/Resources/Portraits/<slug>.png` (root) | **NO** — the loader never appends a level | `BuildPaletteUI.ResolveEntryArtPublic` |

The measured consequence is unchanged: `archer-tower-2.png` and `archer-tower-3.png` sit in the root folder right
now and **no Manage code path can reach them**. **Any new building portrait — including the Heart — belongs in
`Portraits/Buildings/` under `<id>-<level>.png`.**

**Where the chrome would go, if imported.** None of it exists today; all nine are additive:

- `Assets/Resources/RpgUi/frame/` — 17 files, all `frame_*` (`frame_core`, `frame_quest`, `frame_talent` …).
  **No tile/selected/locked/max frame exists.**
- `Assets/Resources/RpgUi/badge/` — **exactly one file**, `badge_level.png`. No state badges at all.
- `Assets/Resources/RpgUi/icons/` — 11 files, none of them a status state.
- `Assets/Resources/RpgUi/slot/`, `…/panel/`, `Assets/Resources/HudIcons/` — checked; the nearest neighbours are
  `slot_*`/`rarity_*`, `panel_*` and `hud_*`. **Nothing overlaps.**

Note the existing folders use **underscores** (`frame_core`, `badge_level`, `slot_item`) while the sheets use
**hyphens** (`frame-tile`, `status-locked`). Pick one at import time; do not straddle.

---

## 5. GAPS — what the redesign needs that is NOT in this delivery

Cross-checked against `WorkOrders/ManageRedesign/00_MANAGE_REDESIGN_CANON.md` and
`docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md`.

**A. The entire tiered portrait set. Zero of the 62 requested files arrived.**
No `<id>-<level>.png` at any tier for any structure or troop. Specifically still missing:
- the 12 cheap re-cuts (existing tower tier art into the `Portraits/Buildings/` convention),
- the three **six-tier** storage ladders (`lumberyard`, `foundry`, `silo` — 18 files; `RepoProps.MaxStructureLevel = 6`),
- 18 troop tier variants (all nine `troops.json` units are flat today),
- `tower_siege_tower`, `wall_wood`, `mine_crystal`, `healing_caravan` tiers (11 files),
- the two duplicate research icons and the gate portrait (3 files).

**B. Three of the eight status icons on Sheet A are missing.**
Delivered: locked, available, in-progress, max, queue. **Not delivered:** `status-warning`, `status-error`,
`status-check`. Canon §8 makes tile state mandatory and lists **"upgrade unaffordable"** as a required
indicator — there is no delivered icon for it, and `status-warning` is the obvious candidate. This is the one gap
that blocks a canon requirement rather than merely looking plainer.

**C. All four research school icons.**
Canon §5 names exactly four schools — Cathedral of Magic (Magic), Armorer (Defense), Forge/Weaponsmith (Weapons),
Barracks (Army) — and Sheet A's `06_RESEARCH_SCHOOLS` block draws exactly those four. **None was delivered as an
individual asset.**

**D. Navigation and filter chrome.**
Canon §2 fixes the tabs at BUILD / ARMY / RESEARCH plus a global QUEUE, and §3 fixes six BUILD filters
(ALL / ECONOMY / DEFENSE / CRAFT / STORAGE / CIVIC). Sheet A draws all of them (`tab-*`, `filter-*`). **None
delivered.**

**E. Buttons, resource icons, bars, level badges.** Sheet A's `03_UI_BUTTONS`, `07_RESOURCE_ICONS`,
`09_OTHER_UI_ELEMENTS` (progress bar bg/fill, `badge-level`, `badge-queue`, `badge-upgrade`) — none delivered.
*(Partly mitigated: `RpgUi/currency/` already has all eight currency icons, so cost chips render today.)*

**F. Every building except the Heart.** Sheet A draws 24 building tiles; one arrived.

**G. The Heart has no tier variants**, despite canon §6 making Heart Level the progression spine. One flat
portrait cannot show a spine climbing. And per §2, the Heart has no id yet — that decision gates the art.

---

## Appendix — Sheet A's building labels mapped to catalog ids

⚠ **These are labels read off Sheet A, NOT delivered files.** Recorded because it is the reconciliation WO-2005
needs; the only building actually delivered is the Heart. Ids read from
`Assets/Resources/Data/Canonical/structures-catalog.json` this session.

| Sheet A label | Catalog id | Catalog display name |
|---|---|---|
| `building-lumber-mill.png` | **NO CONFIDENT MATCH** | **two entries both display "Lumber Mill"**: `lumbermill` (`sawmill`) and `collector_lumbermill` (`wood_producer`). Sheet A also lists a separate `building-lumberyard`, so the sheet distinguishes the store from the producer — but not these two. Owner call. |
| `building-quarry.png` | `collector_farm` | Quarry (`stone_producer`) |
| `building-iron-mine.png` | `collector_forge` | Iron Mine (`iron_producer`) |
| `building-crystal-mine.png` | `mine_crystal` | Crystal Mine |
| `building-barracks.png` | `barracks` | Barracks |
| `building-echo-hollow.png` | `pet-house` | Echo Hollow |
| `building-cathedral.png` | `arcane-tower` | Cathedral of Magic |
| `building-crafting-station.png` | `workshop` | Crafting Station |
| `building-weaponsmith.png` | `forge` | Weaponsmith |
| `building-armorer.png` | `armorer` | Armorer |
| `building-jeweler.png` | `jeweler` | Jeweler |
| `building-archer-tower.png` | `tower_ground_archer` | Archer Tower |
| `building-ballista.png` | `tower_ballista` | Ballista |
| `building-arcane-spire.png` | `tower_arcane_spire` | Arcane Spire |
| `building-catapult.png` | `tower_catapult` | Catapult |
| `building-sky-ballista.png` | `tower_siege_tower` | Sky Ballista (Anti-Air) |
| `building-wooden-palisade.png` | `wall_wood` | Wooden Palisade |
| `building-stone-wall.png` | `wall_stone` | Stone Wall |
| `building-stone-gate.png` | `gate_stone` | Stone Gate |
| `building-lumberyard.png` | `lumberyard` | Lumberyard (`wood_store`) |
| `building-stoneyard.png` | `silo` | Stoneyard (`stone_store`) |
| `building-foundry.png` | `foundry` | Foundry (`iron_store`) |
| `building-healing-caravan.png` | `healing_caravan` | Healing Caravan |
| `building-store.png` | `market` | Store (`marketplace`) |

**Catalog entries with NO tile on any sheet:** `mill` (Mill / `gristmill`), `deco_torch` (Wall Torch),
`repair_default`. The first is a real building with no art anywhere in this delivery; the last two are plausibly
not Manage tiles at all.

**Troops need no mapping work.** All nine `troops.json` ids (`troop-footman`, `troop-archer`, `troop-spearman`,
`troop-field-cleric`, `troop-shieldguard`, `troop-outrider`, `troop-catapult`, `troop-battlemage`,
`troop-echo-legionnaire`) already have matching flat cards on disk under `Assets/Resources/RpgUi/troop/`, under
Sheet A's exact naming. The gap there is **tiers**, not identity.
