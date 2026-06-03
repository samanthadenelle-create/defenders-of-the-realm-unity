# Item Drops + Consumable Crafting — Design Spec (for owner + creative review)

Status: **SPEC + DARK SCAFFOLD** — needs owner/creative sign-off before full build.
Lane: isolated parallel lane (gear/crafting + item drops + consumables). Ships dark.
Canon: village = **Elarion**. Source-of-truth combat is the village wave loop.

---

## 1. Where this fits (what already exists)

The branch already has a near-complete **village crafting station**:

- `CraftingRecipeCatalog` loads `crafting-recipes.json` (ingredients + recipes).
- `VillageInventory` — singleton larder, PlayerPrefs-persisted, `Add / TryConsume /
  TryCraft / CanCraft`. Seeds 5 of every ingredient on first run.
- `VillageCraftingPanel` — the Workshop UI (recipes list + craft button + larder).
- Existing content: **one recipe (torch)** with 3 ingredients.

There is also a **gear v1** system (separate, hero-side):
- `GearCatalog` (weapons.json/armor.json) + `GearLoadout` (auto-equip best eligible).
- Gear is data-driven and graceful; **no equip UI, no loot drops yet** (auto-equip only).

**The gap this lane fills:** nothing currently *produces* materials. The larder only
seeds itself; enemies/bosses drop nothing; there are no consumables. We add the
**SOURCES** (drops) and the **consumable layer**, composing the existing larder +
crafting — not replacing them.

---

## 2. Item taxonomy

### 2a. Drop sources → loot tables
| Source | Trigger (existing event) | Table id (data) |
|---|---|---|
| Common enemy | `Enemy.Died` | enemy def id, else `defaults.enemy` |
| Boss (dragon) | `DragonBoss.Died` | `defaults.boss` |
| Dungeon (future) | TBD dungeon kill hook | `defaults.dungeon` |

A loot table = a list of weighted drop lines (`materialId`, `chance`, `min/maxCount`).
Each line is rolled independently on a kill; hits deposit into the village larder.

### 2b. Crafting materials (drops)
Placeholder roster (creative to finalise names/art):
- `monster-hide`, `wild-herb`, `tattered-cloth`, `ember-resin` (shared w/ torch),
  `rare-essence` (boss-only).

### 2c. Consumable types
| Kind | Effect (v1) | In-fight? | Example |
|---|---|---|---|
| **Potion** | instant heal (mana later) | yes | Minor / Greater Healing Draught |
| **Food** | heal now; **buff later** | yes | Traveler's Rations |
| **Tent Kit** | **rest between fights** → heal party to full | no (rest-only) | Scout's Tent Kit |

A consumable's `id` is its larder key **and** the matching recipe's output id, so the
existing `VillageInventory.TryCraft` path works unchanged.

---

## 3. Example recipes (drops → consumables)

These would be ADDED to `crafting-recipes.json` (content, not yet added — see §6):

| Recipe (output id) | Ingredients | Result |
|---|---|---|
| `minor-heal-potion` | 1 wild-herb | heal 40 |
| `greater-heal-potion` | 3 wild-herb + 1 rare-essence | heal 90 |
| `traveler-rations` | 1 wild-herb + 1 monster-hide | heal 25 (food) |
| `scout-tent-kit` | 2 tattered-cloth + 1 monster-hide | rest → full heal |

(Recipe outputs already key off the recipe id in the existing catalog, so this drops
straight into the current Workshop panel with zero panel changes.)

---

## 4. Loop summary

`kill enemy/boss → roll loot table → materials into larder → craft consumable at
Workshop → carry into next fight → use potion/food mid-fight, pitch tent between
fights → push further`.

This is exactly the owner's intent (fight → harvest → upgrade → push) and stays on
the "focused TD-RPG, not an MMO" scope line — a simple flat material→consumable sink,
no deep shop/economy.

---

## 5. CONTENT DECISIONS NEEDING OWNER / CREATIVE SIGN-OFF

1. **Material roster + names** — the 5 placeholder materials are programmer names.
   Creative owns the real list + lore-flavored display names + glyph/icon art.
2. **Drop rates per enemy** — placeholder chances (0.20–0.35). Owner tunes for the
   intended grind feel (how many kills ≈ one potion).
3. **Per-enemy tables vs one shared table** — scaffold uses one default enemy table +
   one boss table. Do we want per-archetype tables (e.g. casters drop essence)?
4. **Consumable roster + magnitudes** — heal amounts, food buff design, whether
   potions also restore mana (no hero mana pool exists yet — see deferred).
5. **Tent Kit semantics** — "portable rest between fights": is "between fights" a
   real state (wave-clear / camp) or a free anytime-out-of-combat heal? Needs the
   between-fight state machine decision.
6. **Where consumables are used** — a hotbar? auto-use at low HP? the use-service is
   wired but has no UI binding yet (deliberately — UI is owner/creative-gated).
7. **Does this tie to the gear lane?** — drops currently feed *consumables* only.
   Do rare drops also feed *gear crafting/enhancement* (the gear system's deferred
   "loot drops layer on later")? That's a cross-lane decision.

---

## 6. What is DEFERRED (not in this scaffold)

- **Recipe content** — the 4 example consumable recipes are NOT yet added to
  `crafting-recipes.json` (that file is shared content; adding them is a one-line-per
  recipe edit but should land with the finalised roster, not placeholders).
- **Gear-system finishing** (equip UI, manual swap, loot→gear) — requires editing the
  existing gear files; kept OUT of this isolated lane. Follow-up work order.
- **Mana / timed-buff systems** — no hero mana pool or buff manager exists; potion
  mana + food buffs are recognised + logged TODO, not applied.
- **Between-fight rest state machine** — tent kit applies a heal in v1 and logs the
  deferred rest layer.
- **Drop VFX / pickup motes / world-drop pickups** — v1 deposits straight to the
  larder (no physical pickup). Physical drop motes are a later polish pass.
- **Save integration beyond PlayerPrefs** — larder already persists via
  `VillageInventory`'s PlayerPrefs; backend sync is a separate lane.
- **Resources/Data/Canonical dual-copy** — for WebGL, `loot-tables.json` +
  `consumables.json` may need a Resources copy (per the dual-copy convention) before
  a WebGL build. Editor/Windows reads StreamingAssets fine. CLI step, not done here.

---

## 7. How to turn it ON (it ships dark)

Mirror of `CampSystem.Enabled`:
- Code: `ItemDropSystem.Enabled = true;` before a gameplay scene loads, then
  `ItemDropSystem.StartNow();` (or just enter a wave scene).
- Define: add scripting define `DOTR_ITEM_DROPS`.
- Use a consumable: `ConsumableUseService.TryUse("minor-heal-potion", inFight: true);`

When OFF (default): no watcher spawns, no events are subscribed, nothing rolls,
`ConsumableUseService.TryUse` returns false immediately. Zero footprint in the build.
