# WORK ORDER 1081 — A building can be placed in three taps and read in none: the palette never says what anything DOES

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.
**Minted:** 2026-08-24 (UI seat), banner bumped 1081 → 1083 in the same edit (with WO-1082).
**Silo:** Build palette / catalog schema (`Village/BuildMode` + `Core/Catalog` + canonical JSON). No scene work, no economy work.
**Provenance:** owner felt-test on the shipped Seeker APK, 2026-08-24 — *"not a single issue other than
guessing what the crystal mine does"*, then, on being asked: *"i didnt open the card, only placed cause
it was an item."*

---

## 0. ⭐ HOW THIS WAS FOUND, and why it matters more than the ticket

An owner-led felt-test on a shipped device build, in a session where **every automated gate was green**.
There is no log line, no `[Flow:*]` entry, no `Guard` catch and no LayoutOracle finding for *"I placed a
thing without knowing what it was."* The palette rendered correctly. The card built correctly. The
placement committed correctly. Every oracle this repo owns would call that session a pass.

**Only a human playing the game surfaces this class of defect.** Record it beside the WO-1080
capture-provenance finding and the PROD-008 orientation finding: this is the third instrument-blind
class named this month, and it is the one with no instrument at all.

The owner **designed this building** and still could not say what it did at the point of decision. A new
player has no chance.

---

## 1. The premise I was first given was wrong, and the truth is worse

The first brief said *"she could not tell what the Crystal Mine does at the point of decision"*, which
reads as a copy defect on a card she consulted. She then clarified she **never opened the card**.

**She could not have.** Investigated at source:

> ### ⛔ THERE IS NO CARD TO OPEN. The detail view has been unreachable since 2026-06-19.

`BuildStructureInfoPanel` (`Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs:46`) exists and
renders a description at `:290` (`_descLabel.text = card.Description;`). The only route to it is
`BuildPaletteUI.OnCardTapped` (`Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1129-1134`), and
**nothing subscribes to it.** `BuildModeController.EnsurePalette` deliberately does not —
`Assets/_Modules/Village/BuildMode/BuildModeController.cs:3820-3828`:

```
// WO-352 preview (tap card -> Structure Info Preview -> "Place" -> arm) is
// DISABLED 2026-06-19 (owner playtest): its UIToolkit panel adopted a bad/null
// PanelSettings and laid an invisible scrim over the screen ... Revert to IMMEDIATE-ARM
```

`OnCardTapped`, `OnPaletteCardTapped` (`BuildModeController.cs:3858`) and `EnsureInfoPanel` (`:3848`)
have **zero live call sites** repo-wide. There is no long-press, no info button, no second tap: the only
input component on a tile is `CardTapGuard` (`BuildPaletteUI.cs:1101`), a tap-vs-scroll travel
discriminator, not a gesture vocabulary.

### The asymmetry, in gestures

| Action | Gestures | Path |
|---|---|---|
| **Place a building** (touch) | **3** | tap tile (`BuildPaletteUI.cs:1101-1138` → `BuildModeController.cs:3812` `OnEntrySelected += Arm`) → tap world (`BuildModeController.cs:1099-1110`) → tap PLACE (`:206`, commit `:1238-1243`) |
| **Place a building** (desktop) | **2** | hover carries the cell, so PLACE commits directly (`BuildModeController.cs:1112-1124`) |
| **Read what it does** | **not reachable at any gesture count** | no subscriber exists |

⭐ **State the defect in exactly these terms:** placement costs three gestures; reading costs infinity.
The card is not an optional detour off the placement path — **it is off the graph entirely.** The owner
did not skip a step. There was no step to skip.

---

## 2. What the palette TILE shows — the surface she actually acted on

Whole tile built in `BuildPaletteUI.BuildCard(StructureCardVM)` (`BuildPaletteUI.cs:1014-1353`). Complete
field list:

| Field | Rendered | Where |
|---|---|---|
| Name | **YES** | `:1150-1154` |
| Icon / art | **YES** (fallback = one gilt initial letter, `:1229-1235`) | `:1190-1226` |
| Cost | **YES**, *except* a freebie renders **nothing at all** (`:1293`, WO-1010 D20) | `:1287-1300` |
| "Built" chip (singleton already placed) | YES, replaces cost | `:1252-1265` |
| Lock-reason words (visible-locked rows) | YES | `:1315-1335` |
| Targeting tag — **towers only** | YES | `:1312`, `:1337-1352` |
| **What it does** | **NO** | `card.Description` is never read in this file |
| Build time | **NO** | no duration read in `BuildCard` |
| Tier / level | **NO** | `StructureCardVM.TierBadge` (`:152`) computed, never read |
| Footprint | **NO** | `StructureCardVM.cs:88-90` — "the palette never reads it" |

**A tile is: icon + name + cost.** For a first-placement freebie it is **icon + name**, and nothing else.
That is an invitation to place something unknown, and it is exactly what the owner did.

---

## 3. What the Crystal Mine actually does — proven from code, not comments

⛔ It is **not** a no-op. It is real, wired, and regression-covered.

- **Behaviour:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:124-138` — `OnWaveCleared(int waveId)`
  calls `economy.AddCrystals(yield)`. **Event-driven, per cleared wave. Not a timer, not a passive tick,
  not a collect-tap.**
- **The "when":** subscribed in `OnEnable` (`:112-115`) to `WaveManager.OnWaveCleared`
  (`Assets/_Modules/Village/Waves/WaveManager.cs:314`, invoked at `:2886`), resolved via
  `FindObjectsByType<WaveManager>()` (`CrystalMine.cs:316-320`).
- **Yield:** `Assets/Resources/Data/Canonical/buildings.json:21-34`, `"crystalsPerWave": [1, 2, 4]` —
  **L1 = 1 crystal per cleared wave, L2 = 2, L3 = 4.** Read at `CrystalMine.cs:164-220`, indexed
  `level - 1` and clamped (`:145-150`).
- **Dispatch proven:** `Assets/_Modules/Village/Catalog/StructureFactory.cs:1182-1184`
  `case "CrystalMine": root.AddComponent<CrystalMine>();`, fed by the row's `repo.behaviorId`.
- **Coverage:** `Assets/Editor/Regression/CrystalProductionRegression.cs` drives a real `CrystalMine` on a
  real `PlacedStructure` through `OnWaveCleared` and asserts a non-zero L1 delta.
- **Row:** `Assets/Resources/Data/Canonical/structures-catalog.json:426-478` — `role: crystal_producer`,
  `type: Resource`, `behaviorId: CrystalMine`, `maxLevel: 3`, cost **wood 320 / iron 200 / crystals 0**
  (a WO-947-clean *regular* basket — wood + iron, no crystals). **Not locked**: the Town `lockedIds` is
  `["jeweler","mill","lumbermill"]` (`build-categories.json:13-17`); the row was unlocked earlier the
  same day in `936da0c3b` under PROD-015. Both canonical copies are byte-identical (md5
  `e58dfae029370d4998037d0adb2cfa58`, 80920 bytes).

> ### ⭐ THE COMPREHENSION GAP IS DESIGNED IN, AND THE NAME IS HALF OF IT
> A building called a **Mine** pays out **only when a defence wave is cleared**. Every convention the
> word "mine" carries — dig, passive, over time, tap to collect — is wrong for this row. This is not a
> building whose purpose a player can infer from its name and its icon. It is precisely the row that
> needed a sentence, and it is the row with no surface to put one on.

---

## 4. ⛔ AND THE ONE SENTENCE THAT DOES EXIST IS FALSE

`StructureCardVM.Description` is **not data**. It is a hardcoded switch on `type` —
`Assets/_Modules/Village/BuildMode/StructureCardVM.cs:238-249`:

```csharp
case CatalogType.Tower:    return "A defensive tower — auto-fires on enemies in range.";
case CatalogType.Wall:     return "A wall segment — blocks and slows the enemy advance.";
case CatalogType.Gate:     return "A gate — a controlled opening in your defenses.";
case CatalogType.Resource: return "A resource structure — gathers materials over time.";
default:                   return "A village structure.";
```

`mine_crystal` is `type: Resource`, so the one sentence the game holds about the Crystal Mine is
**"A resource structure — gathers materials over time."** It does not gather. It does not work over
time. It does not say which material. **Had she opened the card, it would have told her something
untrue** — so this is not merely an absent explanation, it is a wrong one, and it is the same wrong
sentence for **all fourteen** Town-buildable rows.

It also breaks a rule already ruled on twice: **WO-1161, owner verbatim — *"if we add a building we do
not want to have to manually code it"***. A per-type sentence in C# is a role list in code by another
name.

**Rank the two defects:** ① the flow (§1 — no reachable surface; 3 gestures to place, none to read) is
what the owner felt and is **P0**. ② the copy (§4 — hardcoded, type-level, false for this row) is **P0
too**, because fixing ① alone would ship the false sentence to a surface where players finally read it.

### 4b. There is NO description field in the schema — and one row already tried

`Assets/_Modules/Core/Catalog/CatalogEntry.cs:29-108`, complete public field list: `id`, `displayName`,
`type`, `kind`, `role`, `displayOrder`, `visualPrefabPath`, `visualTexturePath`, `repo`, `composite`,
`orientation`. `RepoProps` (`Assets/_Modules/Core/Catalog/RepoProps.cs`) carries no description-like
field either.

⚠ **`structures-catalog.json:162` authors `"description"` on `tower_siege_tower`** — *"Wall-mounted spear
thrower — fires spears at flying creatures; ignores ground. The strategic counter to the sky dragon."*
It is **silently discarded**: the loader runs `MissingMemberHandling.Ignore`
(`Assets/_Modules/Village/Catalog/CatalogBootstrap.cs:279`, deserialize at `:281`). An author already
reached for this field, wrote a good sentence, and the pipeline ate it without a word.

**So this ticket is about adding the field, not about writing better text into an existing one.**

### 4c. And the Crystal Mine's only other "what it does" signal says "Other"

`role: crystal_producer` is named by **no** `paletteGroups` group (`build-categories.json:18-53` —
Producers is `wood_producer` / `food_producer` / `iron_producer` only). Per rule (1) at
`build-categories.json:3`, an unlisted role falls into the trailing bucket, and that bucket's label is
the hardcoded word **`"Other"`** (`BuildPaletteVM.cs:506`, `:547`). The one categorical hint the palette
offers files the Crystal Mine under "Other".

⚠ The catalog's own `_rolePassNote2026_08_24` (`structures-catalog.json:3`) still asserts `mine_crystal`
is *"lockedIds-filtered and never render[s]"*. **That is false at HEAD** as of `936da0c3b`, and must be
corrected in the same edit (§15 canon rule: a state change with no canon update is an incomplete change).

---

## 5. Do other buildings share the gap? YES — all of them

The gap is **total**, not `mine_crystal`-specific:

- **Every** tile in **every** verb renders no description (§2) — Town, Defense, Walls, Collector, Support.
- **Every** structure in the game resolves to one of five hardcoded per-type sentences (§4). All
  **fourteen** Town-buildable rows — `mine_crystal`, `pet-house`, `workshop`, `market`, `forge`,
  `armorer`, `arcane-tower`, `collector_farm`, `collector_lumbermill`, `collector_forge`, `lumberyard`,
  `foundry`, `silo`, `barracks` — resolve to **"A resource structure — gathers materials over time."**
  or, for the three `Collector` rows, **"A village structure."**
- The Crystal Mine is the one the owner *noticed*, because it is the one whose name actively misleads.

**Scoped deliberately (ARCHITECTURE law — no structural refactor smuggled into player-facing work):**
this ticket adds the field, authors the fourteen Town sentences, and renders one line on the tile. It
does **not** rebuild the info panel, does **not** change the placement flow's gesture count, and does
**not** restyle the palette. See §11 for what is deferred and why.

---

## 6. The fix

### 6.1 Schema — one authored field (data, never a switch)

Add to `Assets/_Modules/Core/Catalog/CatalogEntry.cs`:

```csharp
/// <summary>
/// WO-1081 — the ONE player-facing sentence saying what this building DOES. Authored data,
/// never a C# switch (WO-1161, owner: "if we add a building we do not want to have to manually
/// code it"). Absent/blank falls back to the per-type sentence so no card can ever render empty.
/// </summary>
public string description;
```

`tower_siege_tower`'s existing key (`structures-catalog.json:162`) starts being read for free.

### 6.2 `StructureCardVM.DescriptionFor` — authored first, type sentence as the floor

`Assets/_Modules/Village/BuildMode/StructureCardVM.cs:238-249`: return `e.description` when non-blank;
otherwise the existing `switch` **unchanged** as the fallback. Emit
`FlowTrace.Once("Build", "desc-unauthored-" + e.id, ...)` on every fallback, so an unauthored row is a
logged line and never a silent generic. ⛔ Do **not** delete the switch — a blank card is worse than a
generic one, and §12 forbids removing the net.

### 6.3 The tile renders one line — the player-felt half

In `BuildPaletteUI.BuildCard` (`BuildPaletteUI.cs:1014-1353`), add **one** effect line under the name,
above the art band. Rules:
- Reads `card.Description`. **≤ 48 characters** rendered; longer text is ellipsised — never wrapped to
  three lines, and never allowed to push the cost label off the tile.
- Renders on **every** tile in every verb, including a freebie tile (which today shows name + icon only).
- Locked / visible-locked tiles keep their lock words as-is; the effect line renders **in addition**, so a
  player can tell what a locked building would do before earning it.
- ⛔ **The cost label must stay fully visible at every tested aspect.** That is an acceptance test, not a
  stylistic preference — it is the PROD-013 failure shape (a label pushed off-screen by a new element).

### 6.4 Author the sentences (data; both copies, byte-equal)

`Assets/Resources/Data/Canonical/structures-catalog.json` **and**
`Assets/StreamingAssets/Data/Canonical/structures-catalog.json`.

⚠ **Where player-facing structure copy lives, checked at source:** `canon-strings.json` holds proper
nouns and HUD words — it carries `"crystalMine": "Crystal Mine"` at `:107`, the **name**, and nothing
about behaviour. Structure *behaviour* copy is already authored **on the catalog row**
(`tower_siege_tower.description`). **Follow the pattern that exists: author `description` on the catalog
row.** Do not invent a parallel string table.

**The required string — the one this ticket exists for:**

```json
"description": "Yields Crystals each time a wave is cleared."
```

43 characters. It names the resource and it names the trigger — the two things the word "Mine" gets
wrong. Level scaling (1/2/4) is deliberately **not** on the tile; it belongs to the upgrade surface.

The other thirteen Town rows, authored in the same pass — leaving them on a sentence proven false is not
a smaller change than fixing them:

| id | displayName | `description` |
|---|---|---|
| `mine_crystal` | Crystal Mine | `Yields Crystals each time a wave is cleared.` |
| `collector_farm` | Farm | `Harvests Food for your town over time.` |
| `collector_lumbermill` | Lumber Mill | `Harvests Wood for your town over time.` |
| `collector_forge` | Iron Mine | `Harvests Iron for your town over time.` |
| `lumberyard` | Lumberyard | `Raises how much Wood your town can hold.` |
| `foundry` | Foundry | `Raises how much Iron your town can hold.` |
| `silo` | Silo | `Raises how much Food your town can hold.` |
| `market` | Store | `Sells consumables and materials.` |
| `forge` | Weaponsmith | `Sells weapons.` |
| `armorer` | Armorer | `Sells armour, and opens the Iron harvest.` |
| `workshop` | Crafting Station | `Craft gear and potions here.` |
| `barracks` | Barracks | `Trains and houses your troops.` |
| `pet-house` | Echo Hollow | `Home and wardrobe for your Echoes.` |
| `arcane-tower` | Cathedral of Magic | `Spells and magic research.` |

⚠ **Two of these sit on a known, open display-name defect and must NOT be "fixed" here.** `forge` is
displayed *"Weaponsmith"* and `armorer` is displayed *"Armorer"*, and `structures-catalog.json:798-818`
records that the crossed names await an owner ruling. The sentences above describe what each row
actually **sells** per `vendors.json`, which is the authority (`_artNote2026_08_17`, owner verbatim:
*"which sells weapons, that is the weaponsmith use the JSON data"*). Rename nothing.

`armorer`'s second clause is proven at `structures-catalog.json:872` (`_productionNote`: the Armorer is
the iron faucet's `satisfiedByStructureIds` pair).

### 6.5 Put the Crystal Mine in a group whose header means something

`build-categories.json` (both copies), Town row, `paletteGroups[0].roles`: add `"crystal_producer"` to
**Producers**. One line, display-only, no id change, no re-gate, no re-sort. Correct the stale
`_rolePassNote2026_08_24` sentence in `structures-catalog.json:3` in the same edit.

### 6.6 An oracle, because a comment rots and this is the kind of rule that rots

Extend an existing suite (or add `[structure-descriptions]` to `BuildEconomyRegression`):

1. **Every** row in `structures-catalog.json` whose `type` is `Resource` or `Collector` authors a
   non-blank `description`. A new building without one **fails** — that is what keeps §5 from re-opening.
2. No authored `description` exceeds 48 characters.
3. `StructureCardVM.DescriptionFor(entry)` returns the authored string verbatim for `mine_crystal`, and
   returns the substring `"gathers materials over time"` for **no** row that authors a description.
4. Source lint: `StructureCardVM.cs` contains no `role` or `id` string literal — the switch stays keyed
   on `type` only (WO-1161).
5. The two canonical copies stay byte-identical (the file's own standing rule).

---

## 7. Acceptance criteria

⚠ **The owner is red/green colourblind. No criterion below depends on hue** — each is judged on presence
of text, position, size, character count, or a greyscale-safe capture.

**A — the behavioural core (this is what closes the ticket):**

1. Standing at the Town build palette — **without opening any other screen, without any second gesture
   on the tile, and without any doc** — a player can read from the Crystal Mine tile alone: **what it
   produces** (the word "Crystals") and **what it costs** (320 Wood, 200 Iron). Judged by opening the
   capture PNG and reading the tile.
2. The same holds for **all fourteen** Town-buildable tiles: each renders a non-empty effect line naming
   its own function, and **no two of the fourteen render the identical sentence.**

**B — the copy is true:**

3. `StructureCardVM.DescriptionFor` returns exactly `Yields Crystals each time a wave is cleared.` for
   `mine_crystal`. Asserted headlessly.
4. The string `"gathers materials over time"` reaches **no rendered tile** for any row that authors a
   description. Asserted headlessly.
5. Every `Resource`/`Collector` row authors a `description`; the suite goes red if one does not.

**C — nothing regressed on the surface she actually uses:**

6. The **cost label stays fully visible and unclipped** on every Town tile at **≥ 2 landscape aspects**
   in `RunCaptureHeadless`, judged by **opening the PNGs** (memory:
   `headless-screenshot-verify-ui-before-build`). ⛔ This is the single most likely way this change ships
   a new defect.
7. Placement still costs the same **three** gestures (§1). ⛔ **This ticket must not add a gesture to the
   placement path.** `BuildPaletteVMTests` and the palette regressions stay green.
8. `BuildCarouselTutorialOrderRegression`, `BuildPaletteGroupsRegression`, `BuildEconomyRegression` and
   `BuildCardArtRegression` all stay green — no id renamed, no order changed, no cost changed.
9. Both canonical copies byte-identical; `REGRESSION_OK <n>/<n> suites` on a **fresh** log. **Judge the
   marker, never the exit code** (memory: `gates-report-success-without-proving-it`).

**D — the group hint stops saying nothing:**

10. The Crystal Mine card renders under **Producers**, not **Other**. Judged by the `palette-sections:`
    FlowTrace line showing `Producers=4` and **no** `Other` bucket, plus the chip captions in the capture
    PNG.

---

## 8. Files to edit

| File | Change |
|---|---|
| `Assets/_Modules/Core/Catalog/CatalogEntry.cs` | add `public string description;` |
| `Assets/_Modules/Village/BuildMode/StructureCardVM.cs` | `DescriptionFor` reads authored first, type switch as fallback, `FlowTrace.Once` on fallback |
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` | render one effect line in `BuildCard` (≤48 chars; must not displace cost) |
| `Assets/Resources/Data/Canonical/structures-catalog.json` | 14 `description` strings; correct the stale `_rolePassNote2026_08_24` |
| `Assets/StreamingAssets/Data/Canonical/structures-catalog.json` | byte-identical mirror |
| `Assets/Resources/Data/Canonical/build-categories.json` | add `"crystal_producer"` to Producers |
| `Assets/StreamingAssets/Data/Canonical/build-categories.json` | byte-identical mirror |
| `Assets/Editor/Regression/BuildEconomyRegression.cs` *(or a new suite)* | the five §6.6 gates |
| `Assets/_Modules/Village/Catalog/Generated/CatalogFallbackData.g.cs` | ⛔ **regenerate** via `DeNelle.Editor.CatalogFallbackGenerator.Generate` — it embeds the catalog verbatim and `BuildEconomyRegression` gate 12 goes red on staleness |

---

## 9. ⛔ What NOT to touch

- ⛔ **Never rename a structure id.** They are live save keys (`everBuiltStructureIds`, `BaseLayout`,
  and quest `targetId`s at `quests.json:146 / :361 / :1125`). **Display strings only.**
- ⛔ **Do not turn the Crystal Mine into a mine node the player works.** The owner has recorded a
  `blue_mine` KayKit asset for a future evolution of this building. That is a later design change; this
  ticket is about understanding the building that exists **today**.
- ⛔ **Do not rebalance** the Crystal Mine's yield, cost, tier, `maxLevel` or upgrade curve. The
  `crystalsPerWave: [1,2,4]` curve and the 320-wood/200-iron basket were owner-ruled the same day
  (`936da0c3b`); `_crystalCostNote` at `structures-catalog.json:1000` already flags the Spire price for
  re-judgement when the faucet lands. Not here.
- ⛔ **Do not re-enable `BuildStructureInfoPanel`** in this ticket. Its `PanelSettings` resolution is the
  same UIToolkit-render class as WO-465 and it laid an invisible screen-blocking scrim. See §11.
- ⛔ **Do not change the placement gesture count**, do not re-add an `OnCardTapped` subscriber, do not
  introduce a long-press. A tile tap must keep arming immediately.
- ⛔ **Do not change `displayOrder`, catalog array order, `lockedIds` or `catalogTypes`.** Ordering is
  WO-1082's ticket; the two must not collide inside the same file.
- ⛔ **Do not delete the per-type `switch`** in `DescriptionFor`, and do not strip any `FlowTrace` (§12 —
  instrumentation is permanent).
- ⛔ **Do not state a cost basket you have not read.** WO-947: regular structures are wood + iron;
  magical are crystal-based. The Crystal Mine is **wood 320 + iron 200 + crystals 0** — regular, verified
  at `structures-catalog.json:437-441`.

---

## 10. Instrumentation (§12 — the calls are permanent)

- `FlowTrace.Once("Build", "desc-unauthored-<id>", ...)` on every fallback to the type sentence.
- Extend the existing `card-order:` / `palette-sections:` lines with a `desc-authored=<n>/<n>` count, so
  one capture read answers "did the sentences ship".
- The new label build sits inside the existing `Guard.TryEach("BuildPalette", "build card", ...)`
  (`BuildPaletteUI.cs:866-867`), so a bad string logs and skips one card instead of blanking the shelf.

---

## 11. Separate follow-ups — named here, deliberately NOT folded in

Holistic/structural; the architecture law forbids smuggling them into player-facing work. Mint
separately if the owner wants them:

1. **The detail surface is dead code.** `BuildStructureInfoPanel` + `OnCardTapped` + `EnsureInfoPanel`
   have been unreachable since 2026-06-19 and still carry live-looking wiring, a next-tier preview and a
   stats table. Either fix the `PanelSettings` resolution and give it a deliberate gesture, or delete it.
   Leaving unreachable UI that *looks* wired is how a future seat "fixes" a panel no player can open.
2. **`MissingMemberHandling.Ignore` eats authored data silently.** `tower_siege_tower.description` sat
   unread in a shipping canonical file. A parity oracle failing on any JSON key with no
   `CatalogEntry`/`RepoProps` field would have caught it the day it was written — the same class as
   WO-1173's schema-parity gate.
3. **A comprehension oracle does not exist and may not be buildable.** §0 is the real finding. Worth an
   owner conversation about whether a scripted first-session walkthrough — a human, once per milestone,
   against a written expectation — becomes a standing ritual, in the Sunday-housekeeping shape.

---

## 12. ⛔ VERIFICATION PROVENANCE — what I opened, and what the implementer must confirm first

This ticket was written under a wrap-up deadline. **Accuracy about coverage matters more than the
appearance of completeness**, so the citations are split honestly below. Nothing here changes the
finding; it changes how much of the ticket you may take on faith.

### Read at source by me, this session — treat as verified

- `build-categories.json` Town row in full: `lockedIds` is `["jeweler","mill","lumbermill"]`
  (`mine_crystal` is **NOT** locked), and `paletteGroups` names no `crystal_producer`.
- `structures-catalog.json:426-478` — the whole `mine_crystal` row: role, type, `behaviorId`,
  `maxLevel: 3`, cost wood 320 / iron 200 / crystals 0, and **no `displayOrder`**.
- `StructureCardVM.DescriptionFor` at `:238-249` — the hardcoded per-`type` switch, quoted verbatim in §4.
- `CatalogEntry.cs` public field list — **no description field**, confirmed field by field.
- `structures-catalog.json:162` — `tower_siege_tower` authors a `"description"`.
- `BuildPaletteVM` — `SortForDisplay`, `OrderKey` (`displayOrder > 0 ? displayOrder : int.MaxValue`),
  `Rebuild`, and the hardcoded `"Other"` label at `:506` / `:547`.
- `BuildPaletteUI.cs:820-950` — the WO-1172 Option B chip block and **"All" is the default**.
- `BuildModeController.cs:3820-3828` (the WO-352 disable comment, quoted verbatim), `:3848-3862`
  (`EnsureInfoPanel` / `OnPaletteCardTapped`), `FoundingKit` `:2860-2904`, `isPallet` `:2077-2083`,
  `GraceReasonFor` `:2140`.
- A repo-wide grep for `BuildStructureInfoPanel`: the only hits outside its own file are
  `BuildModeController` (the disabled block), `SiblingPanelSettings`, and two comments in
  `StructureCardVM`. **The "no live subscriber" claim rests on this grep**, which is strong evidence
  but is not the same as tracing every delegate assignment.
- `CrystalMine.cs` — `OnWaveCleared` at `:124`, `economy.AddCrystals(yield)` at `:136`, the
  `crystalsPerWave` parse at `:180`, and the `OnWaveCleared.AddListener` subscribe at `:308`.
  `StructureFactory.cs:1182-1183` — `case "CrystalMine": root.AddComponent<CrystalMine>();`
  ⭐ **The core behavioural claim of this ticket is verified in code, not from comments.**
- `buildings.json:16` — the `crystalsPerWave` curve note (⚠ this one is a **comment**; see below).
- The full Town cost table in §5 / WO-1082 §3 — computed by parsing the JSON directly.

### ⚠ NOT opened by me — OPEN ITEMS, confirm before implementing

Phrased as questions on purpose. Do not treat any of these as established:

1. **The per-field line numbers in the §2 tile table** (`:1150-1154` name, `:1190-1226` art,
   `:1287-1300` cost, `:1252-1265` Built chip, `:1315-1335` lock words, `:1337-1352` targeting,
   `:1101` `CardTapGuard`) come from greps and structural reading, **not from reading `BuildCard` end
   to end.** ⛔ **First implementation step: read `BuildPaletteUI.BuildCard` in full and confirm the
   table.** The *conclusion* — a tile shows icon + name + cost and no description — is safe, because
   `card.Description` has **zero** reads in that file. The line numbers may drift.
2. **Does a freebie tile really suppress the cost entirely** (`:1293`, WO-1010 D20)? If it does not,
   §2's "icon + name only" softens to "icon + name + cost". Does not change the fix.
3. **`crystalsPerWave: [1, 2, 4]`** — I read the *note* in `buildings.json` and the *parse site* in
   `CrystalMine.cs`, but did not open the data row itself. **Confirm the literal array** before quoting
   1/2/4 anywhere player-facing. (§12 of CLAUDE.md: comments lie.)
4. **`CatalogBootstrap.cs:279` `MissingMemberHandling.Ignore`** — asserted, not opened. **Verify that
   `tower_siege_tower.description` is genuinely discarded** rather than read somewhere I did not find.
   If it is already read, §6.1 gets simpler, not harder.
5. **`BuildStructureInfoPanel.cs:46`** — only `:290` (`_descLabel.text = card.Description;`) was
   confirmed by grep. The `:46` class-declaration line is unverified.
6. **`WaveManager.cs:314` / `:2886`** (the `OnWaveCleared` declaration and invoke sites) — inferred from
   `CrystalMine`'s subscribe, not opened. The subscription itself IS verified.
7. **`CrystalProductionRegression.cs`** — I asserted it drives `OnWaveCleared` and asserts a non-zero L1
   delta. **I did not open it.** Confirm it exists and covers what §3 claims before relying on it as
   existing coverage.
8. **The md5 / byte-equality of the two `structures-catalog.json` copies** — asserted, not computed.
   Verify with a hash before and after your edit; the byte-equality *requirement* is real regardless
   (`build-categories.json:4`).
9. **`structures-catalog.json:798-818`** (the crossed `forge` / `armorer` display-name defect) — I read
   the `_artNote2026_08_17` and `_productionNote` via grep at `:851` and `:872`, not that line range.
   The **ruling** it records is verified; the line numbers are not. ⛔ Either way: **do not rename
   either row.**
10. **Character counts** in the §6.4 table were counted by eye for the Crystal Mine string only.
    **Machine-count all fourteen against the 48-char cap** — the oracle in §6.6(2) will catch it, but do
    not ship a table you have not measured.

### Deliberately not investigated at all

- Whether the effect line **fits** the existing tile geometry at the shipped `CardWidthPx`. §6.3 sets a
  48-char cap as a starting constraint, **not a measured fit.** The first capture will tell you; if it
  does not fit, the constraint moves, not the ticket.
- Whether any **non-Town** verb (Defense / Walls / Support) needs different copy. Out of scope here;
  §6.6(1) only requires `Resource`/`Collector` rows to author a string.
