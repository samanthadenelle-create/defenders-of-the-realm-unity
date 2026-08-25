# WORK ORDER 1082 — The build palette leads with the wrong rows, and "collectors" is not the group the owner's own two facts describe

**Status:** READY TO IMPLEMENT — with ONE owner word needed on §3 (which group). The implementation is
specified for both answers; the default is the one her stated facts prove.
**Minted:** 2026-08-24 (UI seat), banner bumped 1081 → 1083 in the same edit (with WO-1081).
**Silo:** Build palette ordering — canonical JSON only. **Zero code changes.** No economy change.
**Provenance:** owner, 2026-08-24 — *"can we order the collectors as last item in array as they are only
items they dont get free and build time is 7 minutes"*.

---

## 1. What she asked for

Move the collectors **last** in the build-palette array, so they are not among the first things a player
meets. Her stated rationale: collectors are the only palette items that do not come free, and their
build time is 7 minutes.

**The ask itself is sound and cheap.** The rationale is where the investigation landed somewhere she may
want to correct — see §3, and treat that as a question, not a contradiction.

---

## 2. Where palette order actually comes from — settled, and it settles the WO-1167 worry

Three layers, and only one of them reaches the player by default:

1. **Query order** — `BuildPaletteVM.AggregateOfType`
   (`Assets/_Modules/Village/BuildMode/BuildPaletteVM.cs:557-569`) concatenates
   `CatalogRegistry.OfType(type)` per declared catalog type. Within a type that is **`entries[]` array
   order in `structures-catalog.json`**.
2. **The sort** — `BuildPaletteVM.Rebuild:334` → `SortForDisplay` (`:443-463`), key at `:466-470`:
   ```csharp
   return e.displayOrder > 0 ? e.displayOrder : int.MaxValue;
   ```
   Ascending; **absent/0 sorts LAST**; tiebreak is the incoming index (`:457` — explicitly stable).
   Only **three** rows author `displayOrder`: `collector_lumbermill` **10**, `workshop` **20**, `forge`
   **30**.
3. **WO-1167 `paletteGroups`** — projected by `BuildPaletteVM.GroupCards` (`:495-549`), authored at
   `Assets/Resources/Data/Canonical/build-categories.json:18-53`. Grouping **adds headers, never
   re-sorts** (`:534-541`).

> ### ⭐ THE WO-1167 CONTRADICTION DISSOLVES, AND HERE IS THE LINE THAT DOES IT
> WO-1167 shipped as **WO-1172 Option B: segmented FILTER CHIPS**, not inline headers — and
> `BuildPaletteUI.cs:826-834` states the rule: ***"All" is the DEFAULT, always (nothing hides behind a
> tap by default)***, with the strip rendering **every card** in flat WO-963 order unless the player taps
> a chip (`:841-861`, `RebuildChips` at `:913-944`).
>
> **So on the screen the player actually sees, grouping does not reorder anything.** "Last in the array"
> DOES reach the player as "last on screen". Her ask is a one-line data move, exactly as she framed it.
> Group membership only matters if she taps a chip, and then it changes which cards show, not their
> order.

### The Town strip as it renders today (default "All"), in order

`collector_lumbermill` (Lumber Mill) → `workshop` → `forge` → then the unauthored tail in array order:
`mine_crystal`, `pet-house`, `market`, `armorer`, `arcane-tower`, `collector_farm`, `collector_forge`,
`lumberyard`, `foundry`, `silo`, `barracks`.

**The very first tile in the Town palette is a collector.**

---

## 3. ⚠ HER TWO STATED FACTS BOTH POINT AT THE STORAGE CONTAINERS, NOT THE `collector_*` ROWS

I tested every reading against source. This is the part that needs one word from her.

### Fact 1 — "they are only items they dont get free"

I was asked to decide between (a) *the only items that cost resources* and (b) *excluded from the free
build slots*. **Both are false as literally stated, and a third reading is exactly true.**

**(a) FALSE — everything costs.** All fourteen Town-buildable rows carry a non-zero basket. Collectors
are in fact the **cheapest** three: `collector_farm` 320 (w240/i80), `collector_lumbermill` 360
(w160/f80/i120), `collector_forge` 480 (w240/i240), against `lumberyard` 1120, `silo` 1200, `foundry`
1440, `barracks` 920. Source says so too — `Assets/_Modules/Core/Catalog/BuildTimerConfig.cs:179-183`:
*"NOTHING in the game is free. All … structures-catalog entries have a non-zero basket."*

**(b) FALSE — `freeBuildSlots` cannot exclude anything.** `BuildTimerConfig.cs:196` (`= 2`) is read at
exactly one site, `Assets/_Modules/Village/Buildings/BuildTimerService.cs:201`
(`int free = Mathf.Max(1, Config.freeBuildSlots);`). It is a **per-channel concurrency count** that takes
no id, no entry and no type. There is no per-structure free/instant flag anywhere. Pinned at 2 by
`BuildEconomyRegression.cs:1368-1369`.

**Worse for (a) and (b): collectors are the rows that DO come free.**
`BuildModeController.FreeBuildAvailable` (`:2951-2987`) Lane 3 (`:2986`) gives **every non-tower id its
first placement free**, and all three collectors are `singleton: true` — so in normal play a collector
**never costs anything at all**. `collector_lumbermill` is additionally in the protected `FoundingKit`
(`:2890-2904`).

> ### ⭐ (c) THE READING THAT IS EXACTLY TRUE — and it is the FIRST-BUILD GRACE, not cost
> `BuildModeController.cs:2077-2083`, quoting the owner's own carve-out verbatim:
> ```
> // OWNER CARVE-OUT 2026-08-06: "other than the pallets". The pallets are the STORAGE
> // CONTAINERS -- lumberyard / foundry / silo ...
> bool isPallet = TownBankCapacity.IsStorageContainer(_armed.repo);
> ```
> `GraceReasonFor` (`:2140`): `if (isPallet) return BuildGraceReason.None;  // carve-out wins in every state`.
>
> **The storage containers are the ONLY town rows that do not get the free 15-second first build**
> (`BuildTimerConfig.firstBuildSeconds = 15f`, `:192`; applied at `BuildTimerService.cs:488-495`). And
> WO-837 is binding canon that `lumberyard` was **removed from `FoundingKit`** for exactly this reason
> (`BuildModeController.cs:2895-2900`): *"stockpiles … are CAPACITY-CAP PROGRESSION buildings, never
> founding freebies."*
>
> **"They are the only items they don't get free" is a precise, true statement about the pallets.**

### Fact 2 — "build time is 7 minutes"

**Not the collectors. It is the storage containers, and the number is 7.68 minutes.**

Duration is derived, never authored — there is no `repo.buildSeconds` or `repo.tier`
(`BuildTimerConfig.cs:296-300`). Basket weight at `:287-288`
(`wood + 1.5*iron + 1.0*food + 2.0*crystals`) → `TierForCost` (`:304-314`) against bands at `:94`
(`{600,1200,2400,4200,7000,11000,17000}`) → `DurationSecondsForTier` (`:274-280`) = `45 * 3.2^tier`.

| id | basket | tier | seconds | minutes |
|---|---|---|---|---|
| `collector_farm` | 360 | 0 | 45.0 | **0.75** |
| `collector_lumbermill` | 420 | 0 | 45.0 | **0.75** |
| `collector_forge` | 600 | 1 | 144.0 | **2.40** |
| `mine_crystal` / `forge` / `armorer` / `arcane-tower` / `barracks` | 620–1080 | 1 | 144.0 | 2.40 |
| **`lumberyard`** | 1280 | 2 | **460.8** | **7.68** |
| **`silo`** | 1320 | 2 | **460.8** | **7.68** |
| **`foundry`** | 1680 | 2 | **460.8** | **7.68** |

And because the collectors DO get the grace, a first Farm / Lumber Mill / Iron Mine actually builds in
**15 seconds**. Since all three are singletons, that is the only build of them a player will ever see.

`BuildTimerConfig.cs:81` even prints the number in the source comment: *"T2 7.7m"*.

> ### ⛔ THE RESOLUTION, AND IT NEEDS ONE OWNER WORD
> **Both of her stated facts — "the only ones that don't come free" and "7 minutes" — are true of the
> STORAGE CONTAINERS (Lumberyard / Foundry / Silo) and of nothing else in the palette.** They are false
> of the three `collector_*` rows on both counts.
>
> ⚠ **Flagging the phrasing rather than assuming:** the owner said "collectors"; her two facts say
> "pallets". Most likely she is using "collectors" for the resource-buildings family as a whole (the
> palette itself groups Producers and Storage adjacently, and `BuildTimerConfig.cs:81` calls the
> containers "pallets"), which reads naturally. **She may correct this and it changes only which line
> moves.** §4 specifies the move for both answers; nothing else differs.

---

## 4. The fix — a data move, no code

⭐ **The trap, and it is the whole implementation note:** authoring a HIGH `displayOrder` to push a row
last **does the opposite** — an authored order always sorts *before* an unauthored one, because
unauthored = `int.MaxValue` (`BuildPaletteVM.cs:466-470`). Setting `displayOrder: 900` on the Lumberyard
would move it to the **front**. The only correct lever is **array position among the unauthored rows**.

### 4a. DEFAULT (her facts) — move the STORAGE CONTAINERS last

In `entries[]` the current tail runs `collector_farm`(20) · `collector_lumbermill`(21) ·
`collector_forge`(22) · `lumberyard`(23) · `foundry`(24) · `silo`(25) · `barracks`(26) ·
`repair_default`(27).

**One move:** relocate `barracks` to sit **before** `lumberyard`, leaving the three containers as the
last Town-buildable rows in the array. None of them authors `displayOrder`, so the stable sort carries
them straight to the end of the strip. Apply byte-identically to both canonical copies.

### 4b. ALTERNATIVE (if she means the three `collector_*` rows)

Relocate `collector_farm`, `collector_lumbermill` and `collector_forge` to the **end** of `entries[]`
(after `barracks`), **and** remove `"displayOrder": 10` from `collector_lumbermill`
(`structures-catalog.json:1128`) — without that removal it stays the first card no matter where the row
sits in the array.

> ### ⛔ 4b BREAKS A SHIPPED OWNER RULING AND A GREEN GATE. Do not run it without her word.
> `Assets/Editor/Regression/BuildCarouselTutorialOrderRegression.cs:265-266` asserts, in its own words,
> *"the tutorial's FIRST placement must be the carousel's first card"* — the Lumbermill must hold the
> lowest authored `displayOrder` in the file — and `:250-261` requires the strictly-ascending chain
> `collector_lumbermill → workshop → forge`.
>
> That gate exists because of **WO-963, owner ask 2026-08-10, verbatim: *"Can we order the carousel in
> order of how the tutorial presents them?"*** `tutorial-steps.json:40` still teaches
> `build.card.collector_lumbermill` first. So 4b puts the row the tutorial points at last on the shelf,
> and either the tutorial changes with it or that ruling is being reversed. **That is an owner decision,
> not an implementation detail** — surface it, do not resolve it in code.
>
> 4a has **no** such conflict: no container authors a `displayOrder`, and no gate asserts their position.

### 4c. Consumers of palette position — checked, and there are none that break

- **Tutorial: id-keyed, not index-keyed.** `BuildPaletteUI.cs:1051` registers
  `"build.card." + e.id`; `TutorialFlow.cs:1057` composes `"build.card." + wantId`;
  `TutorialHighlightRegistry.cs:126-134` lists the ids. **Reordering cannot break a highlight.**
- `BuildPaletteGroupsRegression.cs:240` asserts within-section order positionally (`s2[0].Cards[0/1]`) —
  it drives synthetic sections, unaffected by catalog array order.
- `Assets/Tests/EditMode/BuildPaletteVMTests.cs:46,93,94` and `CastlePlansUnlockRegression.cs:141` index
  `vm.Cards[i]` — **re-verify these after the move**; a fixture asserting `Cards[0]` is the one thing
  here that a reorder can flip.
- `UICaptureLaunch.cs:3007` asserts an object **name** (`"BuildPaletteRestoreTab"`), not an index.
- No quest, save-data or gameplay consumer reads a palette slot number.
- ⛔ **`CatalogFallbackData.g.cs` embeds this file verbatim** — regenerate via
  `DeNelle.Editor.CatalogFallbackGenerator.Generate` or `BuildEconomyRegression` gate 12 goes red on
  staleness.

---

## 5. Acceptance criteria

⚠ **The owner is red/green colourblind — no criterion depends on hue.** Judged on order, position, text
and captured frames.

1. On the default **All** view of the Town palette, the three storage containers (Lumberyard, Foundry,
   Silo) are the **last three** Town-buildable tiles in the strip. Judged from the `card-order:`
   FlowTrace line (`BuildPaletteVM.cs:406-408`) and by opening the capture PNG.
   *(4b instead: the three `collector_*` rows are the last three, and the Lumber Mill is no longer the
   first card.)*
2. **The first tile is no longer the row that moved.** Stated as a positive so it is judgeable: under
   4a the strip still opens on `collector_lumbermill`; under 4b it opens on `workshop`.
3. **No cost, no build time, no tier, no id and no lock state changes.** Diff the two canonical copies
   and confirm the only changes are line positions — `git diff --stat` shows `structures-catalog.json`
   with equal insertions and deletions, and every `"cost"`, `"buildCost"` and `"id"` value unchanged.
   ⛔ She asked for an **ORDER** change, not an economy change.
4. Both canonical copies **byte-identical** (md5 match), and the file's `version` bumped per the file's
   own rule.
5. `BuildCarouselTutorialOrderRegression`, `BuildPaletteGroupsRegression`, `BuildEconomyRegression`,
   `BuildPaletteVMTests` and `CastlePlansUnlockRegression` **all green**, and
   `CatalogFallbackData.g.cs` regenerated. `REGRESSION_OK <n>/<n> suites` on a **fresh** log — judge the
   marker, never the exit code.
6. The tutorial's founding beat still highlights the Lumber Mill card wherever it now sits (id-keyed,
   §4c) — verified by a headless tutorial run reaching `build.card.collector_lumbermill`.

---

## 6. Files to edit

| File | Change |
|---|---|
| `Assets/Resources/Data/Canonical/structures-catalog.json` | relocate rows per §4a (default) or §4b; bump `version` |
| `Assets/StreamingAssets/Data/Canonical/structures-catalog.json` | byte-identical mirror |
| `Assets/_Modules/Village/Catalog/Generated/CatalogFallbackData.g.cs` | ⛔ regenerate (`CatalogFallbackGenerator.Generate`) |
| `Assets/Tests/EditMode/BuildPaletteVMTests.cs` *(only if a `Cards[i]` fixture flips)* | re-anchor by id, never by index |

---

## 7. ⛔ What NOT to touch

- ⛔ **Never rename a structure id** — live save keys. Rows move; ids do not change.
- ⛔ **No rebalancing.** Do not change any `cost`, `buildCost`, `upgradeCost`, `tierCostThresholds`,
  `firstBuildSeconds`, `freeBuildSlots` or the pallet grace carve-out. If the 7.68-minute container wait
  is itself the complaint, that is a **separate economy ticket** and an owner ruling — this one only
  moves rows.
- ⛔ **Do not "fix" the ordering with a high `displayOrder`.** It sorts the row to the FRONT (§4).
- ⛔ **Do not run §4b without the owner's word** — it fails a shipped gate and reverses WO-963.
- ⛔ **Do not touch `paletteGroups`, `lockedIds` or `catalogTypes`.** Group membership is not the lever
  here (§2), and `paletteGroups` is WO-1081's edit — the two tickets must not collide in one file.
- ⛔ **Do not add or remove any `description` string** — that is WO-1081. If both land in the same
  window, sequence them: WO-1081 first (it adds keys), WO-1082 second (it moves rows).
- ⛔ **Do not strip any `FlowTrace`** (§12). The `card-order:` line at `BuildPaletteVM.cs:406-408` is the
  proving line for criterion 1 and must survive.

---

## 8. One line for the owner, so she can correct me in one word

> Your two reasons — *"the only items they don't get free"* and *"build time is 7 minutes"* — are both
> exactly true of the **Lumberyard, Foundry and Silo** (the storage containers): they are the only rows
> carved out of the free 15-second first build, by your own 2026-08-06 ruling *"other than the pallets"*,
> and they are the only rows that take **7.68 minutes**. The three rows literally named `collector_*` —
> Farm, Lumber Mill, Iron Mine — build in **15 seconds** and are **free** on first placement. **Which
> three do you want last?** Everything else about the change is identical.
