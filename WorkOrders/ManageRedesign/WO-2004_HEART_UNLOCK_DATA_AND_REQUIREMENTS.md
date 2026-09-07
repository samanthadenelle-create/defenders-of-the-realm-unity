# WO-2004 — Define Data-Driven Heart Unlock Bundles and Upgrade Requirements

**Status:** READY TO IMPLEMENT - state unproven 2026-09-06

**Priority:** P0  
**Depends on:** WO-2003

## Objective

Make Heart upgrades actually unlock the broader game instead of functioning as a cosmetic level number.

## Data model

For each Heart level transition, author one progression record containing:

- target Heart level
- costs
- duration
- prerequisite conditions
- newly unlocked building IDs
- newly unlocked building upgrade caps
- newly unlocked troop IDs
- newly unlocked troop upgrade caps
- newly unlocked research schools/perks
- newly unlocked defenses
- newly unlocked systems
- reach/radius value if applicable
- optional reward/message metadata

## Do not hard-code unlocks in UI

The model computes the unlock result from progression data.

UI may show:
- "Unlocks at Heart Level 4"
- a preview list supplied by model

UI may not know which Heart level unlocks Outrider, Stone Gate, etc.

## Unlock preview

Heart selection/detail model should support a short preview:

`NEXT LEVEL UNLOCKS`
- Stone Gate
- Outrider
- Armorer Level 3
- +X build reach

Preview contents come from the progression model.

## Gate normalization

Every system currently using a village/town tier gate must be audited.

For each gate:
- map to Heart Level if it represents realm progression
- leave unchanged only if it is truly a different mechanic
- document exceptions

## Acceptance criteria

- no duplicated Heart-level unlock tables across services
- one authoritative progression table
- every player-facing tier lock resolves to a real Heart upgrade path
- unlocking a Heart level invalidates/rebuilds dependent Manage state
- no restart required to see newly unlocked tiles/actions


---

## Provenance and reconciliation (added by the CLI seat, 2026-09-06)

Authored outside this repo and delivered by the owner as `Elarion_Manage_Redesign_Detailed_WorkOrders.zip`
on 2026-09-06. Filed verbatim; **the body above is the author's and has not been edited.** Everything the CLI
seat adds appears under a heading like this one.

**Numbering:** this set uses a **2000-block**, a THIRD namespace alongside the CLI main line and the UI seat's
reserved block. It is declared on `CLI_LANES_WO_NUMBERS.md` so it cannot collide (CLAUDE.md section 2 - the banner is the
sole authority). Do not renumber these into the main line.

**Supersedes:** `WORK_ORDER_1427` (why-can't-I) and `WORK_ORDER_1428` (the Manage card grows to the mockup).
Both were minted earlier the same day from the owner's playtest and her mockup; this program subsumes them and
goes further by replacing the rail model rather than enriching the card.

**Measured facts this set is consistent with** (from `docs/manage-flow-map/MAP.md`, run `Builds/flowmap1`):
43 rail rows across four areas, about two visible at a time; Buildings 6 + Defense 11 = 17, which is the number
the canon cites; the scroll auditor reporting `geometry=5 touch=5` on deliberately scrolled frames, which WO-2016
is right to call a fix rather than a waiver.

---

## Implementation notes and gate audit (Opus lane, 2026-09-06) — edit-only, ungated

**Status: EDIT-ONLY, NOT GATED.** This lane holds no Unity lock and ran no compile gate, no regression
and no commit. Everything below is measured at source this session or is explicitly named as unproven.

### Files changed

| File | Change |
|---|---|
| `Assets/Resources/Data/Canonical/heart-progression.json` | **NEW** — the one authoritative Heart ladder |
| `Assets/StreamingAssets/Data/Canonical/heart-progression.json` | **NEW** — byte-identical mirror |
| `Assets/_Modules/Core/State/HeartProgressionCatalog.cs` | **NEW** — its only reader |
| `Assets/_Modules/Village/Buildings/Progression/VillageTierService.cs` | `MaxTier` const → catalog-backed property; `NextCost()` reads the catalog |
| `Assets/_Modules/Village/Buildings/Progression/HeartProgression.cs` | `UnlocksAt` derives troops / population cap / echo slots transitively |
| `Assets/_Modules/Village/Population/PopulationService.cs` | `CapAtVillageTier(int)` — read-only projection of the existing private ladder |
| `Assets/Editor/Regression/HeartSurfaceRegression.cs` | new case `[ladder-*]` — an empty or re-hardcoded ladder cannot ship |

`.meta` files for the two new `.json` and the new `.cs` are left for Unity to generate on import.

### What moved out of code, and what deliberately did not

The **ladder** moved. `VillageTierService` carried `public const int MaxTier = 3;` and
`return 250 * next;` — the ceiling and the cost curve for the gate that opens nearly all content,
where the owner cannot tune them. Both now live in `heart-progression.json` with **identical values**
(3; 250 / 500 / 750). This is a de-hardcoding, **not a re-balance**.

⛔ **There is deliberately NO hand-written fallback ladder.** A missing or malformed catalog fails
loudly and loudly *empty* (`FlowTrace.Fail` + `Debug.LogError`, then `MaxLevel == 0`), following
`BuildingTierCatalog`'s precedent and owner ruling 2026-08-24 / WO-1170 (*"We need to not have
anything pulled other than from json"*, pinned by `JsonMirrorLiteralRegression`). A silent fallback to
3 and 250 would be indistinguishable from a working catalog.

**The unlock lists did NOT move into data, and that is the point.** WO-2004's own acceptance line —
*"no duplicated Heart-level unlock tables; one authoritative progression table"* — is satisfied by
**derivation**, not by authoring a second table. The chain, all measured at source:

```
heart-progression.json      Heart Level ceiling + crystal cost      (NEW, this lane)
building-tiers.json         requiresVillageTier on a ladder rung ─┐  -> that rung opens
                            perks ride their own row's field  ────┤  -> that perk opens
troops.json                 unlockBarracksTier == that rung  ─────┤  -> that troop becomes reachable
PopulationService.CapByTier cap[level]                        ────┤  -> the cap rises
population-milestones.json  all.villageLevel == level         ────┘  -> an echo slot's Heart condition
```

Authoring a troop list into `heart-progression.json` would have created exactly the duplicated table
the WO forbids. `UnlocksAt` composes the hops instead, so **"Outrider" now appears in the preview with
no second table** — worded as a path (`Outrider (via Barracks Level 4)`), because reaching the Heart
Level makes the Barracks rung *upgradable*; it does not hand the player the troop.

### ⛔ Where the delivered spec disagrees with the tree — read before acting on the spec

1. **`RESULT` for WO-2003 names paths that do not exist.** It lists
   `Assets/_Modules/Core/Manage/HeartProgression.cs`, `.../HeartPanel.cs` and
   `.../HeartPanelBootstrap.cs`. Measured: `Assets/_Modules/Core/Manage/` contains none of the three.
   The real files landed in commit `a6bbc523d` at
   `Assets/_Modules/Village/Buildings/Progression/HeartProgression.cs` and
   `Assets/_Modules/Village/UI/Manage/HeartPanel.cs` / `HeartPanelBootstrap.cs`.
   RESULT files are frozen (CLAUDE.md §15) so this lane did not edit it — flagged for the lead.

2. **The spec's example preview is not this game's content.** It shows
   `NEXT LEVEL UNLOCKS: Stone Gate / Outrider / Armorer Level 3 / +X build reach` under
   *"Unlocks at Heart Level 4"*. Measured:
   - **Heart Level 4 does not exist** — the ceiling is 3, and `ProgressionReachabilityRegression`
     exists precisely because `lumber-ancient-sawmill` once demanded tier 4 and was unreachable forever.
   - **Stone Gate is not Heart-gated at all.** It is earned from `wall_wood` reaching L2
     (`RewardedProgression.TryUnlockStoneGate`) and is then *hidden by an art gate*
     (`BuildInventoryModel.cs:313-316`, `HiddenUntilFinishedArtId = "gate_stone"`) — the unlock is
     earned and has no effect. A different mechanic; documented exception, left unchanged.
   - **`+X build reach` describes a system that does not exist.** Grepped
     `Assets/_Modules/Village/BuildMode` for build-reach / influence-radius / buildable-radius:
     **zero hits.** Canon §6 permits a reach unlock and owner ruling 12 requires the value be
     data-driven, but there is no radius to make data-driven yet. **This is a new feature for the
     owner, not a missing number**, and authoring one would be a promise nothing can keep.
   - **Outrider and Armorer Level 3 are real** and both now appear in the preview.

3. **The spec's 12-field progression record cannot be honoured as written.** Only *target level* and
   *costs* exist. Deliberately absent, each recorded in the file's own `_authoringNotes`:
   **duration** (no Heart job kind, no queue channel, no timer — `TryUpgrade` is instant: spend →
   tier+1 → Save → Recompute; WO-2003 recorded this and it is still unruled), **prerequisite
   conditions** beyond the crystal cost (none exist in the spend path), **defense unlocks**
   (`structures-catalog.json` authors no tier gate — grepped for requires/unlock/minTier/gateLevel:
   zero hits), **reach**, and **reward/message metadata** (no reward grant is wired to a Heart level).
   Authoring inert fields would also trip `AuthoredFieldReaderRegression`'s Case C ratchet, which
   fails a new authored string field with no production reader.

### Owner rulings reconciled

- **Ruling 23 (one of each storage type; capacity grows by LEVEL, not COUNT).** **No conflict with
  this WO, and nothing here touches it.** Measured: the three container rows (`lumberyard`, `foundry`,
  `silo`) carry **no `requiresVillageTier`** — they are not on the Heart axis at all, so no authored
  requirement in this WO's blast radius contradicts the ruling. The singleton flag landed in `a6bbc523d`
  (WO-2005). This lane added no capacity, count or level logic.
- **Ruling 24 (Cathedral priced as a small multi-resource basket, superseding ruling 22's stone).**
  **No conflict, and deliberately untouched.** The `arcane-tower` rows *are* Heart-gated
  (`requiresVillageTier` 0/1/2/3) and are still authored in crystals (T2 = 2,560). Re-pricing them is
  WO-2005 / WO-2007's job and is blocked on ruling 24's own open item — `BuildingUpgradeService.TierCost`
  picks **one** lane by tier index and cannot express a basket. This lane changed no cost row.
  ⚠ **The preview shows what opens, never what it costs**, so it cannot repeat the authored-vs-charged
  lie. A note in `UnlocksAt` records that any future cost line must read `BuildingTierChargeLane`, not
  the authored key.
- **Storage climbs to six levels; `RepoProps.MaxStructureLevel` is the single ceiling.** No level
  ceiling was re-hardcoded. `RepoProps.MaxStructureLevel` (6, per-structure ladder) and the Heart
  ceiling (3, `heart-progression.json`) are **two different axes**; both the new catalog and
  `VillageTierService.MaxTier` say so in-code so a future seat cannot conflate them.
- **Ruling 21 (the barracks BUILDING tier gates troops).** The troop derivation reads the building
  tier and never `GameState.BarracksLevel` — that is stated as a ⛔ in `AppendTroopsForBuildingRung`.

### Gate normalization audit — every tier-shaped gate in the tree

| Gate | Where | Disposition |
|---|---|---|
| `requiresVillageTier` (27 rows) | `building-tiers.json`, read by `BuildingUpgradeService:53-59` and `BuildingPerkService:194-199` | **IS the Heart gate.** Already normalized to "Heart Level" in copy by WO-2003. |
| `all.villageLevel` | `population-milestones.json` (echo slots 4, 5) | **IS a Heart gate**, compound (also needs quests/outposts). Now surfaced in the preview with an honest "also needs other milestones" suffix. |
| `CapByTier` = `{5,8,12,16}` | `PopulationService.cs:56`, **code-side, not JSON** | **IS a Heart gate.** Now surfaced via `CapAtVillageTier`. ⚠ **Recorded gap:** it is a balance ladder living in code. Moving it to JSON is a balance-data change and was deliberately not smuggled into this progression change. |
| `unlockBarracksTier` (9 troops) | `troops.json`, `TroopUnlock` | **DOCUMENTED EXCEPTION** — a genuinely different mechanic (building tier, ruling 21), but **transitively Heart-gated** because the barracks rungs carry `requiresVillageTier`. Left unchanged; now traversed. |
| Stone Gate | `RewardedProgression.TryUnlockStoneGate`, on `wall_wood` L2 | **DOCUMENTED EXCEPTION** — a structure-level reward, not a realm tier. Currently inert behind an art gate. |
| `unlockVictories` (0/3/10/20) | `scene-configs.json` | **DOCUMENTED EXCEPTION** — region/scene progression by victories. Not realm progression. |
| `requiresQuestId`, `requiresFlag`, `requiresFeature`, `requiresWallet`, `unlockMethod`, `unlockSpell`, `unlockAbility` | quests, gear/jeweler recipes, daily-quests, packs, cosmetics, abilities, hero-talents | **NOT tier gates.** Out of scope. |
| `RepoProps.MaxStructureLevel = 6` | `Core/Catalog/RepoProps.cs:`**`111`** | **DIFFERENT AXIS.** Per-structure ladder ceiling, not a realm tier. Never conflate. ⚠ **CLAUDE.md §8 cites `RepoProps.cs:69` for this constant. Measured at source 2026-09-06: it is line 111.** A stale line number in the read-first canon — flagged for the lead, not edited by this lane. |

**Finding worth the owner's eyes:** barracks tiers **4, 5 and 6 all carry `requiresVillageTier: 3`**.
The barracks ladder runs to 6 while the Heart ceiling is 3, so **the final Heart Level opens the path
to four troops at once** (Outrider, Siege Catapult, Battlemage, Echo Legionnaire) and nothing is
Heart-gated above it. That is authored, not a bug, and `ProgressionReachabilityRegression` passes on
it — but it means the last third of the troop roster hangs off a single Heart rung. A balance call.

### Proven at source before hand-off (things easy to assume and expensive to get wrong)

- **`PopulationService` declares `namespace DeNelle.Village.Population`** (`:39`), so the
  fully-qualified `DeNelle.Village.Population.PopulationService.CapAtVillageTier(...)` calls resolve.
  `Guard` and `FlowTrace` both declare `namespace DeNelle.Core.Diagnostics` (`Guard.cs:4`,
  `FlowTrace.cs:4`).
- **The three new `HeartUnlockKind` values cannot crash the view.** `HeartPanel` consumes the enum in
  exactly ONE place — `HeartPanel.cs:350`, `unlocks[i].Kind == HeartUnlockKind.Research` — a bare
  equality producing a bool. There is no `switch` with a throwing default and no icon array indexed
  by `(int)Kind`, so `Troop` / `PopulationCap` / `EchoSlot` render as ordinary non-research rows.
- **Adding sources to `UnlocksAt` would have BLUNTED the suite it feeds, and that was corrected.**
  Case 3 `[heart-level-opens-something]` tested `unlocks.Count == 0`. Because the population cap now
  rises at every level 1–3 unconditionally and each source is independently `Guard.Try`'d, a total
  failure of the building-tier derivation would have left a non-empty list and the case would have
  gone **green on the very defect its own failure text names**. It now counts
  `Kind == BuildingLevel` entries. Widening what an oracle observes without narrowing what it asserts
  is how a suite quietly stops asserting — the same species as the 394-green run these seam oracles
  exist for.

### Not proven by this lane

- **Nothing was compiled, gated or run.** Brace balance and NUL-freedom were checked on all five `.cs`
  files (all balanced, zero NULs); both JSON copies parse and are byte-identical (10 lines, CRLF,
  matching the working-tree convention under `core.autocrlf=true`). **That is not a compile.**
- **The new `[ladder-*]` regression case has not been executed.** It is written against measured
  behaviour and should be green, but that is a claim, not a result.
- **Acceptance criterion "no restart required to see newly unlocked tiles/actions" is UNPROVEN.**
  Three of the four links are proven at source: `HeartProgression.TryRaise` →
  `VillageTierService.TryUpgrade` does `Save()` + `ModifierService.Recompute()`;
  `HeartPanel.OnRaiseTapped` (`HeartPanel.cs:410-419`) calls `Render()` immediately after `TryRaise`,
  so **the Heart panel itself refreshes**; and `PopulationBootstrap.PollVillageTier` (`:159-163`)
  polls `VillageTierService.Current` every frame, so the population cap self-heals with no event.
  **What is NOT proven is the BUILD / ARMY grids behind the Heart panel** — whether `ManageScreenVM`
  re-projects its tiles when the player closes the Heart after a raise. No subscription from
  `ManageScreenVM` to a tier-changed signal was found, and `VillageTierService` raises no event; the
  likely path is a rebuild on panel re-open, which **this lane did not verify**. Closing it needs a
  runtime capture (§12), which an edit-only lane cannot take. **Recorded as open, not ticked** — if
  it is a real gap it is one subscription, not a refactor.
- **The preview list GREW and nobody has looked at it rendered.** Heart Level 3 now yields six
  building rungs + four troops (Outrider, Siege Catapult, Battlemage, Echo Legionnaire, all hanging
  off that one rung) + the perks on those rows + a population-cap line + an echo-slot line. If
  `HeartPanel` lays out a fixed row count or an unscrolled column, that is a **presentation** question
  for WO-2017 and an owner call — flagged so the lead sees it before opening a `UI_CAPTURE` PNG, not
  after.
