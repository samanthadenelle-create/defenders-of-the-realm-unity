# World Collection Model — owner directive (2026-06-10, captured overnight)

**Status:** DIRECTIVE / VISION captured. NOT yet implemented. Needs an awake,
gated design pass before any code (this is a world-data-model decision, load-bearing).
**Law:** `docs/ARCHITECTURE_PRINCIPLES.md` — HP B2B lens: nested collections,
bounded context, capability-as-property. Right not easy.

---

## 1. The directive (owner's words, captured)

> "we have castle. could become castles" → "collection of castles in realm" → "each
> realm has its city states" → "and those all have buildings" → (earlier) buildings as
> a collection where `store.upgradable` / `store.interaction` are capabilities; "buildings
> that have upgrades are noted and handled as such."

## 2. The model — nested collections (B2B catalog, all the way down)

The whole world is **nested collections**, each level a catalog of entries, capability
carried as **properties on the entry** — not bespoke per-instance code.

```
Realm                      (collection of city-states)
  └─ City-State            (today: the Castle hub → "Castles" as a collection across the realm)
       └─ Building          (collection; each entry = a "SKU")
            • capabilities:  Upgradable    (store.upgradable),
                             Interaction  (store.interaction),
                             Destructible (isDestructible),
                             Targetable   (isTargetable),
                             ActionId, Label, … (flags/props on the entry)
```

### 2a. Capabilities are COMPOSABLE — retained or not, per entry (owner directive)

Each capability is an **independent, opt-in/opt-out property** an entry **retains or
does not retain**. A building entry composes whatever set is true for it — there is no
fixed "building behaves like X" class; behavior is the SUM of the capabilities it holds.

Known capability set so far (extend freely — that's the point):

| Capability | Meaning | Consumed by |
|---|---|---|
| `Interactable` (`store.interaction`) | player can engage it (talk/mine/enter) | interaction service + HUD affordance |
| `Upgradable` (`store.upgradable`) | has an upgrade path; "noted + handled as such" | upgrade entry points + HUD note |
| `Destructible` (`isDestructible`) | can take damage / be destroyed | combat/damage system (`IDamageableStructure`) |
| `Targetable` (`isTargetable`) | enemies / towers may target it | enemy AI + tower targeting |

**Why composable matters (the win):** a decorative wall = `Destructible` + `Targetable`,
NOT `Interactable`/`Upgradable`. A vendor stall = `Interactable`, NOT `Destructible`. The
Heart = `Destructible` + `Targetable`, NOT `Upgradable`. Today these are tangled into
per-type code + scattered `IDamageableStructure` implementations + AI tag checks; the
model makes each a **flag on the entry**, so systems ask "does this entry retain
`Targetable`?" instead of hard-coding per type. Add a capability = add a property +
the one system that reads it (§1 bounded context).

**Existing code this maps onto (reconcile, not replace):** `IDamageableStructure`
(Destructible), the `HeroTarget`/enemy-target tags + tower targeting (Targetable),
`BuildingInteractable` (Interactable), the upgrade panels (Upgradable). The collection
makes these capability-driven instead of type/tag/interface-scattered.

B2B parallel: **Region → Catalog → Category → SKU**, with capability flags on the leaf.
- A **realm** = a region/market (a collection of city-states).
- A **city-state / castle** = a catalog (a collection of buildings). The current single
  Castle hub (MainCastle_Hall) becomes ONE entry in a **collection of castles/city-states**.
- A **building** = a SKU entry; `Upgradable`/`Interaction`/`ActionId` are **properties**
  read uniformly by the HUD/interaction/upgrade layers. Nothing hard-codes "which
  buildings upgrade" — the collection is the single source of truth.

## 3. Why this is right (architecture fit)

- **One law, every level (§1 bounded context):** the same collection+capability shape
  repeats Realm→City-State→Building. Add a realm / castle / building = add an ENTRY,
  not new code paths.
- **Presentation stays isolated (§2):** the HUD/interaction affordance + upgrade entry
  points READ capability flags off the collection; they don't own which buildings do what.
- **Dovetails WO-391:** an interactable exposes its capability; the buildings collection
  is the authoritative "which buildings have Interaction/Upgrade." The interaction
  service + the buildings collection are two views of ONE model.
- **Scales the world pillar (memory: roaming-troops / bigger-world):** a realm of
  city-states / castles you can visit + raid is naturally a collection of collections —
  this model is what makes "enlarged finite world of mini-bases" data-driven, not
  hand-built per base.

## 4. Architect's flags for the AM (do NOT decide at 1am)

As your dev lead, I want your awake judgment on these before any code — committing the
codebase to a sweeping world model overnight would be easy-but-wrong:

1. **Scope/sequencing.** The killer near-term slice is the **Buildings collection**
   (capabilities: Upgradable/Interaction) — it unblocks the HUD interaction work + "note
   buildings that upgrade." The Realm→City-State→Castles tiers are the *bigger* world
   vision. Recommend: **build the Buildings-collection leaf first** (immediate value,
   bounded), and **spec the Realm/City-State/Castle tiers as the world-data-model WO**
   to grow into — don't boil the ocean in one WO.
2. **Reconcile, don't replace (memory).** There is existing `Building` / `BuildingType`
   / `BuildingInteractable` / `CrystalMine` / upgrade-panel / `VillageSceneBuilder`
   (Buildings[] static array — already a proto-collection!) + the catalog work
   (StructureFactory / placement recipes / village-factory architecture). The collection
   model must be **layered additively onto these**, not greenfield. The recipe/catalog
   "town = data" architecture (memory: village-factory-architecture) is likely the
   existing seam to extend.
3. **Persistence (§7 lane).** A realm of city-states with per-building upgrade state is
   save-state per player (backend persistence pivot) — the collection is also the save
   schema. Worth modeling once, with the backend lens.
4. **Naming/canon.** "Castle → Castles", "City-State", "Realm" need canon entries
   (DESIGN-DECISIONS) so they're consistent in code + strings.

## 5. Proposed WOs (for owner to number from the master backlog)

- **WO-A — Buildings collection (leaf, near-term):** catalog of building entries +
  capability props (Upgradable/Interaction/ActionId); HUD interaction + upgrade entry
  points read capabilities; reconcile with existing Building/interactable/upgrade code.
- **WO-B — World collection model (tiers, vision):** Realm → City-State/Castles →
  Buildings nested collections; data-driven world; ties to the bigger-world pillar +
  save schema. Spec first, build incrementally.

## 6. Status

Captured only. No code. Held for an awake, gated design decision on scope + sequencing
(§4). Recommend starting with WO-A (Buildings collection) as the bounded, high-value
first payment; grow into WO-B.
