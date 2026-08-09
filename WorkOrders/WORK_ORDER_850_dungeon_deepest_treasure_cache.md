# WORK ORDER 850 — Deepest-room treasure cache (torch recipe + crafting supply)

**Status:** DONE (reconciled 2026-08-09 from the tree - commit `0bb46258` landed the deepest-room treasure cache with the torch recipe unlock and the fixed crafting supply, with a follow-up balance pass in `64b1f48b`. NOT felt-verified; no `.RESULT.md`)

**Status:** IN FLIGHT (2026-08-02, CLI + 2 lane agents). **Lane:** Dungeons / Crafting.
**Origin:** owner request during live felt-testing, 2026-08-02 — *"can we add treasure at deepest, simple crafting supply"*.

---

## 0. Owner rulings (captured verbatim, these ARE the spec)

| # | Ruling | Source |
|---|---|---|
| R1 | Treasure sits at the **deepest** room of the dungeon | *"add treasure at deepest"* |
| R2 | Loot = **fixed** basic crafting materials — **no RNG** | AskUserQuestion: "Fixed mats + a crafting recipe unlock" |
| R3 | Plus a **crafting recipe unlock, FIRST CLEAR ONLY** | same |
| R4 | Interaction = **prompt, then a confirm/reward panel** (not walk-in auto-claim) | AskUserQuestion: "Prompt then confirm" |
| R5 | The first recipe is the **TORCH**, and the cache pays **the crafting items for it** | *"first one should be recipe for a torch and crafting items for it"* |
| R6 | Supply is **simple materials AND potion ingredients** | *"simple or for potions"* → taking both |

**R5 note:** the torch recipe ALREADY EXISTS — `crafting-recipes.json` → `recipes[0]`,
id `torch`, ingredients `dry-reed x1 + oil-soaked-cloth x1 + ember-resin x1`. This WO
unlocks it; it does **not** author a new recipe, and it must **not** retro-gate the
existing Healer's-Cottage pedestal (`pedestal.recipeId = "torch"`) behind the new unlock.

---

## 1. Why "deepest" had to be COMPUTED

No layout in the tree carries a depth or boss marker. `dg_starter_loop` rooms are all
`hub`/`combat`; archetype `boss` exists in `rooms-catalog.json` but **no shipped layout
uses it**. So depth is derived:

> **BFS hop distance from the graph's `entry` room over the layout's `connections[]`,
> treated as UNDIRECTED. Ties break on the LOWEST ordinal `instanceId`.**

Determinism is the point — a random or enumeration-order pick would be un-regressable, and
the AutoPilot fleet could never replay it. Note that **furthest-by-hops and
furthest-by-Euclidean-distance DISAGREE** on `dg_starter_loop` (`turn3` vs `turn2`), which
is exactly why the rule is written down instead of assumed. If no room is deeper than the
entry, the cache is **not injected at all** — seating the reward on the entry is worse than
no reward.

## 2. Architecture

| Concern | Decision |
|---|---|
| Injection | `DungeonTreasureSpawner`, a `RuntimeInitializeOnLoadMethod` scene hook mirroring `DungeonExitSpawner` — idempotent, composed-dungeon only |
| Prompt | the shared `MobileInteractButton.Request/Release` (the project's ONE interact path; there is no `IInteractable`) |
| Panel | `ElarionUiKit.BuildObsidianModal` only — hand-rolled uGUI is a hard gate failure (`UiObsidianConformanceRegression.HardFailOnNew`) |
| Exits | **ONE.** The shared Close is retired; "Take" is the single CTA — the same owner F8 (seq 628) that killed Continue-vs-Close on the Echo emergence beat |
| Granting | `DungeonLootGrant.GrantFixed` — stays the ONE granting seam; deposits exactly what it is given, never rolls a table |
| Grant timing | fires **only** on Take, after panel teardown. A dismissed panel must never silently eat the reward (WO-844 potion lesson) |
| First-clear one-shot | `GameState.SeenTutorials` via `GameStateService.MarkTutorialSeen` — **NO schema bump** (still v36); copies the `TorchWardenDress.GrantTorchOnce` idiom |
| Per-run guard | a component-local `_opened` flag, so one run pays once |

**Why not `DungeonRuntimeState.OpenChest`:** it is a runtime-only ScriptableObject that does
not survive save/load, so it cannot carry "first clear ever".

**Why not the existing `DungeonChestInteract`:** that is the WO-749 rich-dungeon chest — it
**auto-opens on proximity with no prompt and no panel**, and rolls a loot table. R2/R4
explicitly ask for the opposite.

## 3. Known risk being verified in-lane

The torch ingredients (`dry-reed`, `oil-soaked-cloth`, `ember-resin`) are defined in
`crafting-recipes.json`'s own `ingredients` block, whereas the larder uses `materials.json`
ids (`ing_*`, `HealthHerb`). `DungeonLootGrant.CanonicalLarderId` exists *because* those
namespaces differ. **If the torch ingredients are not larder-valid, the cache would hand out
items the larder cannot hold.** The lane agent must surface this rather than substitute
look-alikes — `ing_cloth_scrap` is NOT `oil-soaked-cloth`. Routing that gap is the CLI's
call, not the agent's.

## 4. Acceptance

- [ ] Cache injects into `dg_starter_loop` at a room that is deeper than `entry`, deterministically
- [ ] Approaching raises the shared Interact prompt ("Open the cache"); it releases on leave and while a panel is open
- [ ] Take grants the fixed bundle exactly once per run; nothing is credited if the panel is dismissed
- [ ] First clear unlocks recipe `torch` and persists across save/load; second clear grants mats but not the recipe again
- [ ] Panel: single "Take" CTA, ASCII-only, `Name xN` as TEXT (colourblind-safe), fixed-pixel bands
- [ ] `DungeonTreasureRegression` green: bundle ids resolve in the catalogs, deepest-room math pinned incl. the tie-break and the no-connections→null case
- [ ] EditMode tests green for the pure BFS
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + EditMode + `UI_CAPTURE_OK` with the panel PNG **opened**

## 5. NOT in scope

- Authoring new recipes, or gating any existing recipe behind `RecipeUnlocks`
- Changing the Healer's-Cottage pedestal or the WO-749 chest
- Chest ART (no chest prefab exists under `Resources/`; the cache is built from primitives
  like the exit beacon — KayKit `chest.fbx` is edit-time art only). A real chest model is a
  follow-up if the owner wants it.
