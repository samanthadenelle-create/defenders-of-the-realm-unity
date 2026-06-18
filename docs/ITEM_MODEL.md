# ITEM MODEL — the Carriable ontology (data-driven, capability-composed)

**Status:** SPEC / CANON-IN-PROGRESS. Derives from `docs/ARCHITECTURE_PRINCIPLES.md`
§2b (the One Model — recursive collection, capabilities on the entry), §4 (derive
transforms from geometry + name), §2b.1/§2b.2 (POOL by default), §2c (tests are the
permission gate for holistic change). Owner-directed 2026-06-18 ("done, and done right").

**Decision lens:** what is right, not what is easy. This is HOLISTIC/structural work
(§3) — logged + done deliberately + gated by tests, never smuggled into a UX pass.

---

## 1. The principle

Everything in the world is an **Entry** — one universal shape, all the way down. An
Entry's behavior is the **sum of the capabilities it composes**, read off **data**, not
a class hierarchy. The code is a small set of **generic systems that READ capabilities
and act**; the data (the collection) defines *what exists* and *what it can do*.

**Data-driven derivation, two senses (both binding):**
1. **Behavior derived from capabilities** — a system asks *"does this entry retain
   `Carriable` / `Equippable` / `Usable` / `Targetable`?"* and composes behavior from the
   flags. The same inventory reader serves a sword, a potion, and any future carriable;
   combat's targeting reader serves an enemy and the Heart. No `if (kind==Weapon)`.
2. **Look/transform derived from the asset** (§4) — the entry carries `prefabPath`; grip,
   reach, orient, scale fall out of **mesh bounds + name**, never hand-typed Eulers.

**Payoff:** adding a sword / a whole outfit set / a consumable / an enemy = adding an
**entry (or a property) + at most the ONE system that reads a new capability** — never a
new code path per item. 800 weapons are 800 *rows*, not 800 lines. The thin 5-item store
is a thin *collection*, not a broken *system*.

---

## 2. The ontology

```
Entry  (object in a collection — the ONE shape)
  id, displayName, classOf, rarity, tier
  capabilities (composable, opt-in/out — explicit flags on the entry):

  ┌─ Carriable ───────────  in inventory · pick up · carry · stack · sell
  │    ├─ Weapon      (+Equippable)   repo: damageMult, reach, hand, damageType, grip/orient
  │    ├─ Gear/Armor  (+Equippable)   repo: weight, defense, hpBonus, slot, setBonus
  │    └─ Consumable  (+Usable)       repo: effect, magnitude, duration, usableInFight, glyph
  │
  └─ NOT Carriable
       ├─ Enemy      (Targetable, Destructible, Damageable, AI)   repo: hp, ai, contactDamage, moveSpeed
       └─ Structure  (Interactable, Upgradable, Destructible, Targetable)  repo: hp, footprint
```

An **enemy is the same Entry shape as a sword** — it simply does not retain `Carriable`;
it retains `Targetable`+`Destructible`+`AI`. A sword retains `Carriable`+`Equippable`. A
potion retains `Carriable`+`Usable`. **`Carriable` is a capability, not a class.**

### Capability set (extend freely — that is the point)
| Capability | Meaning | Read by |
|---|---|---|
| `Carriable` | in inventory; pickup/carry/stack/sell | inventory service, store, loot |
| `Equippable` | occupies an equip slot; modifies the wearer | equip/loadout, combat stats |
| `Usable` | consumed/triggered for an effect | consumable use, battle loadout |
| `Targetable` | enemies/towers may target it | enemy AI, tower targeting |
| `Destructible` | takes damage / can be destroyed | combat/damage (`IDamageableStructure`) |
| `Interactable` | player can engage (talk/mine/enter) | interaction service + HUD affordance |
| `Upgradable` | has an upgrade path | upgrade panel (already a live flag: `isUpgradable`) |
| `AI` | self-acts (perceive/path/attack) | brain/spawner |

---

## 3. The three Carriable repos (weapon ≠ gear ≠ consumable)

All three share the **catalog ⊥ repo** split + the `Carriable` surface; the repo (stats)
and the equip-vs-use verb diverge. Shared entry fields:

```
id, displayName, kind(Weapon|Gear|Consumable), category, job/classFit, rarity, tier
  ── catalog half (LOOK):   visual = prefabPath + iconPath          ← MISSING today
  ── repo half (BEHAVIOR):  <kind-specific, below>
  economy:   buyCost{wood,food,iron,crystals}, req.level
  narrative: setId, saga, flavor, makersMark
  capabilities: Carriable + (Equippable | Usable)
```

- **Weapon repo:** `damageMult, reach, hand(1h|2h), damageType(melee|ranged), grip/orient`
  (grip + reach **derived** from mesh bounds + name, §4 `WeaponOrientHelper`); future
  `variants[]` axis (e.g. arrow types).
- **Gear/Armor repo:** `weight(light|heavy)` → class-fit (Ranger/Mage=light, Knight/Cleric=heavy),
  `defense` (fractional damage reduction), `hpBonus`, `slot`, `setBonus`.
- **Consumable repo:** `kind, effect, magnitude, duration, usableInFight, glyph`.

---

## 4. What exists today (reconcile onto, never greenfield)

Already separate collections, each its own repo — the ontology is the *unnamed* shape of
what we have:
- `weapons.json` (16) + `armor.json` (5) → **`GearCatalog`** (Weapon, Gear)
- `consumables.json` (4) → **`ConsumableCatalog`** (Consumable)
- `enemies.json` (9) → **`EnemyFactory`** (Enemy)
- `buildings.json` / `structures-catalog.json` → **`BuildingCatalog`** — **`isUpgradable`
  is already a capability flag**: the proof the model works.

**The two honest gaps that make this real vs aspirational:**
1. **No asset link.** `WeaponDef`/`ArmorDef`/`ConsumableDef` carry an **emoji `icon`** and
   **no `prefabPath`** → equipping changes a *number*, not a visible model. The catalog⊥repo
   law requires `visual` = the prefab.
2. **Capability is implied, not named.** "Which catalog the entry sits in" stands in for the
   capability. Lift `Carriable`/`Equippable`/`Usable`/`Targetable`/… to **explicit flags** so
   systems read the flag, not the file.
   **OWNER-RATIFIED 2026-06-18:** capabilities are EXPLICIT flags on the entry — agreed, this
   is the model. A system reads the flag, never the catalog-of-origin. Notated as canon.

**Asset-catalog gap:** `MASTER_ASSET_REFERENCE` covers walls/props/nature/dungeon — **not**
the gear bundle (Blink ~800 weapons / ~290 armor, KayKit, Tripo). The owned gear is
**uncatalogued**; that is why the store is thin. See `docs/MODEL_CATALOG.md` + the
`ModelCatalogGenerator` pattern.

---

## 5. The work (sequenced, bounded payments — §3 leverage, §2c gated)

Build the **leaf first**; each slice ships with the tests that prove it preserved behavior.

- **WO-Item-1 — Schema move (additive, foundation).** Add `prefabPath` + `iconPath` and
  explicit capability flags (`Carriable`/`Equippable`/`Usable`) to `WeaponDef`/`ArmorDef`/
  `ConsumableDef` (default the flags so existing rows are unchanged). `version`-stamp
  `weapons.json`/`armor.json` (today: none, a drift hazard). **No behavior change.** Ships
  with the INVARIANTS wired into BOTH EditMode tests AND the headless **regression suite**
  (`RegressionSuite.cs` → `DeNelle.Editor.DataRegression.RunAll` → `REGRESSION_OK`, the
  permission gate per HANDOVER §4). **OWNER-RATIFIED 2026-06-18: the model invariants live in
  the regression test, not just the doc** — so every change/regen is gated by data, not faith.

  **Invariants the regression asserts (§2c permission gate):**
  - every `Weapon` entry retains `Carriable`+`Equippable`; every `Gear` retains
    `Carriable`+`Equippable`; every `Consumable` retains `Carriable`+`Usable`.
  - NO entry retains both `Carriable` and `AI` (an item is never an enemy).
  - every `Carriable` entry resolves a non-null `prefabPath` (the asset link exists).
  - every `Targetable`/`Destructible` entry resolves a model; capability ⇒ its reader exists.
  - Resources↔StreamingAssets gear copies in sync; `version` present + monotonic.
- **WO-Item-2 — Gear/weapon generator (the leverage).** A sibling of `ModelCatalogGenerator`
  that scans the owned gear packs → stubs `id/name/category/job/prefabPath` per asset
  (auto-fillable); leaves `damageMult/defense/req/price/lore` for **rarity-templated + human
  authoring**. Emits to `weapons.json`/`armor.json`. Add the gear packs to the asset catalog.
  **Why this is the high-leverage move (owner, 2026-06-18 — "that generator can be used so
  dynamically"):** it is **re-runnable and asset-driven** — drop a new pack, regenerate, the
  catalog (and therefore the store/inventory/equip) grows with **zero hand-typing of rows**.
  The dynamism IS the point: the generator + the regression gate are a pair — **every regen
  runs the invariants (WO-Item-1) and must pass `REGRESSION_OK`**, so dynamic growth can never
  silently break the model. Idempotent; never overwrites a `manual=true` authored field (§4).
- **WO-Item-3 — Equip pooling (§2b.1).** Equipped-gear visuals come from a **pool, one
  owner** (the attach system) — never `Instantiate` per equip (the VFX double-up scar).
- **WO-Item-4 — Capability readers (incremental).** Migrate inventory/equip/use/target call
  sites to read the explicit capability flag instead of catalog-of-origin. One reader per
  capability (§1 bounded context).

**Guardrails on all of it:** POOL by default; ONE owner per concern; tests are the
permission gate (no "done" without green); reconcile additively onto the existing catalogs;
keep the Resources/StreamingAssets copies in sync (the `CanonicalJson` law).

---

## 6. Non-goals / flags for the owner

- **Per-slot vs full-body armor** — today armor is modeled as full-body outfit (`slot` is
  effectively one). Decide if Gear gets head/chest/legs slots before the generator runs (it
  changes the entry granularity).
- **Enemy/Structure stay as-is for now** — they already have catalogs; naming their
  capabilities as explicit flags is a later, separate slice (don't boil the ocean).
- **`Assets/Data/Canonical/{weapons,armor}.json` is a dead 3rd copy** (drift hazard,
  data-catalogs §5 FLAG 1) — fold into the source-of-truth or delete as part of WO-Item-1.
</content>
