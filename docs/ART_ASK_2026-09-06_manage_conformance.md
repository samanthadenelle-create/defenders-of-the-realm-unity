# ART ASK — everything the Manage screens still need to match the mockup

**Raised:** 2026-09-06, CLI seat, at the owner's request: *"For anything missing, give me an explicit ask
with all details and sizes and formats so I can get those created now."*
**Target:** `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` + the Manage UI asset sheets.
**Everything below was measured this session** against the working tree — no number is copied from a doc.

---

## 0. READ THIS FIRST — TWO THINGS YOU DO **NOT** NEED TO PAY FOR

**A. The tiered building portraits are ALREADY IN THE REPO. Do not re-create them.**
`Elarion_Building_Portraits_All_Tiers.zip` (26 files) was checked against the tree this session:
**26 identical, 0 different, 0 missing** — byte-for-byte, by MD5. All present at
`Assets/Resources/Portraits/Buildings/`. That covers all tiers of the six ladder families
(`arcane-tower`, `armorer`, `barracks`, `farm`, `forge`, `lumbermill`) plus `heart.png`. **That delivery
is done and landed.**

**B. The 9 troop portraits already exist and are correct.** `Assets/Resources/RpgUi/troop/troop-*.png`
— all nine, 1254×1254 RGBA. **Nothing is owed for screens 4 and 5.**

**C. The ten individual UI pieces you delivered on 2026-09-06 (the Heart, five status medallions, four
tile frames) are NOT missing art — they are UN-IMPORTED art.** `docs/ART_DELIVERY_2026-09-06_manage_assets.md`
records them as identified but *"nothing has been copied, converted or imported"*, and they are still not
in the tree. **That is a CLI import job, not an art job. Do not re-generate them.**

**So the only thing that genuinely needs creating is §1 — and, if you want the mockup's chrome exactly,
§2.**

---

## 1. ⛔ P0 — 21 BUILDING PORTRAITS. This is why the BUILD grid shows empty rings.

**Measured:** of the **26** structures the BUILD grid offers, only **5** resolve a portrait. The other 21
fall through to a neutral hammer icon, which is the blank/plain tile you photographed.

### 1.1 THE FILE CONTRACT — get this exactly right or the file will not be found

| Property | Value | Why |
|---|---|---|
| **Folder** | `Assets/Resources/Portraits/Buildings/` | `ManageArt.BuildingPortraitFolder` (`ManageArt.cs:127`) |
| **Filename** | **the catalog id, VERBATIM, `.png`** | `ManageArt.BuildingPortraitKey` (`:158-161`) returns `folder + ladderId` and its own comment says *"⚠ THIS DELIBERATELY DOES NOT SLUG THE ID"* |
| **Size** | **1024 × 1024** | all 27 existing building portraits measured 1024×1024 |
| **Format** | **PNG, 8-bit, RGBA** | ditto, all 27 |
| **Background** | **fully transparent** (alpha 0 at the corners) | matches the delivered set |
| **Subject** | the whole building, centred, elevated 3/4 view, no text, no UI frame, no baked lock/level/state badge | `WO-2015` portrait contract |
| **Style** | identical to `Portraits/Buildings/barracks.png` — dark Elarion palette, warm focal light, soft vignette edges | so the grid reads as one family |

> ## ⛔ THE FILENAME IS THE TRAP. UNDERSCORES ARE PRESERVED — THEY ARE NOT DASHES.
> The id is used **verbatim**, so it must be `tower_ground_archer.png`.
> **NOT** `tower-ground-archer.png`, **NOT** `building_archertower.png`, **NOT**
> `building-archer-tower.png`, **NOT** `bld_archertower.png`.
> ⚠ **Your three asset sheets use three different naming schemes and NONE of them match the catalog id.**
> The sheets show `building_archertower.png`, `bld_archertower.png` and `building-archer-tower.png` for
> what the game calls `tower_ground_archer`. **Use the left-hand column below and nothing else.**

### 1.2 THE 21 FILES

| # | **Filename (exact)** | Shown as | Filter | What it is |
|---|---|---|---|---|
| 1 | `tower_ground_archer.png` | Archer Tower | DEFENSE | timber-and-stone tower, arrow slits, archer platform |
| 2 | `tower_ballista.png` | Ballista | DEFENSE | mounted bolt-thrower on a raised wooden emplacement |
| 3 | `tower_siege_tower.png` | Sky Ballista (Anti-Air) | DEFENSE | upward-angled ballista, steep elevation — must read **anti-air** |
| 4 | `tower_catapult.png` | Catapult | DEFENSE | counterweight siege catapult, timber frame |
| 5 | `tower_arcane_spire.png` | Arcane Spire | DEFENSE | slender crystal-topped magical spire, blue glow |
| 6 | `wall_wood.png` | Wooden Palisade | DEFENSE | sharpened-log palisade section |
| 7 | `wall_stone.png` | Stone Wall | DEFENSE | dressed-stone curtain wall section, crenellations |
| 8 | `gate_stone.png` | Stone Gate | DEFENSE | stone gatehouse with an arched timber gate |
| 9 | `healing_caravan.png` | Healing Caravan | DEFENSE | covered wagon, healer's banner, lantern |
| 10 | `collector_lumbermill.png` | Lumber Mill | ECONOMY | working sawmill, log pile, water wheel |
| 11 | `collector_farm.png` | Quarry | ECONOMY | ⚠ **a QUARRY, not a farm** — cut stone face, carts. The id is legacy; the building is a quarry |
| 12 | `collector_forge.png` | Iron Mine | ECONOMY | ⚠ **an IRON MINE, not a forge** — timbered mine mouth, ore carts. Id is legacy |
| 13 | `mine_crystal.png` | Crystal Mine | ECONOMY | mine mouth with glowing blue crystal formations |
| 14 | `mill.png` | Mill | ECONOMY | windmill or grain mill |
| 15 | `market.png` | Store | ECONOMY | market stall / trading post, awnings, goods |
| 16 | `pet-house.png` | Echo Hollow | ECONOMY | ⚠ **hyphen here, it is the real id** — a small shrine-like hollow where Echoes dwell, soft blue spirit light |
| 17 | `lumberyard.png` | Lumberyard | STORAGE | stacked timber store, open-sided shed |
| 18 | `foundry.png` | Foundry | STORAGE | iron store / ingot racks (it is the **iron container**) |
| 19 | `silo.png` | Stoneyard | STORAGE | ⚠ **stone store**, despite the id — cut-stone yard, blocks stacked |
| 20 | `workshop.png` | Crafting Station | CRAFT | open workshop, benches, tools |
| 21 | `jeweler.png` | Jeweler | CRAFT | jeweller's shop, gem display, fine goldwork |

⚠ **Rows 11, 12 and 19 are id/name mismatches that already exist in the data.** Draw the **"Shown as"**
column — that is what the player reads on the tile. Do not draw the id.

**Tiers:** none of these 21 has an upgrade ladder authored today, so **one file each — no `-2` / `-3` /
`-4` variants are needed.** (The six ladder families that do have tiers are already fully covered.)

---

## 2. P1 — UI CHROME, only if you want the mockup's chrome exactly

**Status: none of it is in the repo.** `Assets/Resources/UI/ElarionMedieval/` holds **37** generic
files — `tab-selected`, `button-normal-empty`, `card-frame-empty` and so on. **Not one asset from your
Manage sheets is present under the sheet's names.**

⚠ **The sheets were delivered as CONTACT SHEETS, not as individual files** (except the ten in §0C).
A contact sheet cannot be sliced to shippable quality. So each icon below needs to come out as **its own
transparent PNG**.

**Common contract for everything in §2:** PNG, 8-bit **RGBA**, **transparent background**, no baked text,
square unless stated.

| Group | Count | Size | Files |
|---|---|---|---|
| **Status badges** | 5 | 512×512 | ⛔ **ALREADY DELIVERED — see §0C. Do not re-create.** `status-available`, `status-locked`, `status-inprogress`, `status-max`, `status-queue` |
| **Tile frames** | 4 | 512×512, 9-slice safe | ⛔ **ALREADY DELIVERED — see §0C.** `frame-tile`, `frame-selected`, `frame-locked`, `frame-max` |
| **Tab icons** | 4 | 256×256 | `tab-build`, `tab-army`, `tab-research`, `tab-queue` |
| **Filter icons** | 5 | 256×256 | `filter-all`, `filter-economy`, `filter-defense`, `filter-craft`, `filter-storage` — ⛔ **five, not six. There is no CIVIC chip** (`BuildFilter.cs:59`) |
| **Resource icons** | 5 | 256×256 | `res-wood`, `res-stone`, `res-iron`, `res-crystal`, `res-gold` |
| **Stat icons** | 4 | 256×256 | `stat-health`, `stat-attack`, `stat-range`, `stat-speed` — screen 5 draws these |
| **Research school icons** | 4 | 256×256 | `research-arcane`, `research-defense`, `research-weapons`, `research-army` |
| **Chrome** | 3 | 256×256 | `icon-back` (a `<-` **arrow**, not a word), `icon-close`, `icon-time` (hourglass) |
| **Progress bar** | 2 | 512×64 | `progress-track`, `progress-fill` |

### ⚠ Two measured constraints your sheet art must satisfy

1. **The delivered frames are not interchangeable as they stand.** Measured in the delivery doc: centre
   alpha is **opaque (A≈253)** on `frame-tile` and `frame-selected` but **fully transparent (A=0)** on
   `frame-locked` and `frame-max`. Two carry a slate plate, two are hollow rings — so the portrait behind
   a locked/max frame shows through where it does not behind a normal one. **Please make all four
   consistent** (recommend all hollow, so one portrait sits behind every state).
2. **`frame-selected`'s glow bleeds outside the frame rect**, so it cannot 9-slice to the same border as
   `frame-tile`. Either keep the glow inside the rect, or accept that the selected state occupies a
   slightly larger footprint.

---

## 3. ⛔ NOT AN ART ASK — three things that would waste your money

1. **The empty gold rings on the current BUILD tiles are NOT a missing frame.** They are the neutral
   hammer fallback firing because the portrait did not resolve (`ManageScreenPanel.cs:3421-3425`).
   **§1 fixes them. New frame art would not.**
2. **The Catapult reading *"A defensive tower … auto-fires on enemies in range"* is a COPY defect, not
   an art defect** — every `CatalogType.Tower` shares one unauthored-description fallback. Owned by
   **WO-1565**.
3. **Colour is never the fix.** The owner is red/green colourblind; state must read in greyscale. Every
   badge above must be distinguishable by **shape and silhouette alone**.

---

## 4. WHAT HAPPENS WHEN THE FILES ARRIVE

1. Drop the 21 into `Assets/Resources/Portraits/Buildings/` using the exact names in §1.2.
2. Unity import settings must match the existing set (read off `barracks.png.meta`):
   `textureType: 8` (Sprite), `spriteMode: 1` (Single), `alphaIsTransparency: 1`,
   `maxTextureSize: 1024`.
3. `ManagePortraitCoverageRegression` is the proof — it enumerates the catalog against the filesystem and
   **fails by name** on any id with no portrait. Green there means the grid is fully dressed.
4. Re-capture and compare against the mockup per `WorkOrders/ManageRedesign/CAPTURE_LOOP_GOAL.md`.

**No frame in the repo currently postdates the code** (newest capture 18:39, last commit 18:51, edits
20:47), so the first comparison after these land must use a **fresh** capture.
