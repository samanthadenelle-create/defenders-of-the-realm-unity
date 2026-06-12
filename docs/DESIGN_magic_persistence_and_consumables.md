# Design — Persistent Magic + Consumable Value

Status: **DESIGN / FOR OWNER REVIEW** — research-grounded, read from code (not comments).
Author lane: architect + design (read-only). No code written.
Owner goal: *"Tie in magic to be persistent so that consumables actually have value."*

Canon: village = **Elarion**. Scope law: focused TD-RPG, simple-polished over deep-rough
(see memory `scope-discipline-not-an-mmo`). This design adds **one** persistent number and
**one** spend/refill link — it deliberately does not build a spellbook MMO.

---

## A) Current state — how it actually works today

### Magic / spells (the hero ability kit)
- Spells live in `abilities.json` as a fixed Q/W/E/R kit per class (mage/knight/ranger),
  loaded by `AbilityCatalog`. Each ability already carries a **`manaCost`** field
  (`abilities.json`: Q=0, W=3, E=4, R=7) — the data is there.
- `HeroAbilities.cs` (`Assets/_Modules/Village/Hero/HeroAbilities.cs`) owns a mana pool:
  `_maxMana = 10`, `_manaRegenPerSecond = 0.9`. `TryCast` gates on
  `cooldown <= 0 && mana >= manaCost`, then `_mana -= def.ManaCost`. Mana **regenerates
  every Update** back to max.
- **This mana pool is 100% in-memory.** It is created fresh each scene (`Awake: _mana = _maxMana`),
  never read from or written to `GameState`, and **regenerates for free** in seconds.
- Spells themselves are **not unlocked or leveled** — every hero always has its full
  Q/W/E/R from frame one. The only progression that touches abilities is
  `HeroTalentModifiers` (cooldown/damage multipliers from `hero-talents.json`) and
  `HeroProgression` (level damage scalar). Neither persists a *spell roster*.
- There is a separate **`Magic` currency** on `GameState` (`int Magic`, save v15) — but
  this is the **building-upgrade tech-tree gating currency** (DEF-121/WO-230), NOT hero
  spell mana. Name collision worth noting: "Magic" already means something else.

**Gap:** "magic" has no persistent progression and no persistent cost. The mana pool is
a free, self-refilling, per-session number. Nothing the player does to magic carries
across a session, and casting costs nothing the player has to *manage* between fights.

### Consumables (potions / food / tent)
- `consumables.json` defines 4 items: `minor-heal-potion` (heal 40), `greater-heal-potion`
  (heal 90), `traveler-rations` (food, heal 25), `scout-tent-kit` (rest). **No mana potion
  exists in the catalog.**
- `ConsumableUseService.TryUse` (`Items/ConsumableUseService.cs`) implements **heal** and
  **rest** (rest = a heal). The **`Mana` effect branch is an explicit no-op** — it logs
  *"mana restore DEFERRED (no mana pool wired)"*. That comment is **stale**: a mana pool
  DOES exist on `HeroAbilities` — it just isn't reachable/persistent, so the consumable
  has nothing meaningful to refill.
- The whole drop→craft→consumable loop (`ItemDropSystem`) **ships dark** —
  `ItemDropSystem.Enabled` defaults `false`; `ConsumableUseService.TryUse` returns false
  when off. So in the live build, crafted consumables effectively don't exist yet.
- **Two parallel consumable stores exist:**
  1. `VillageInventory` (the larder) — string-keyed counts; **persists** via
     `GameState.GearInventory` (save v20, Neon-synced). This is where crafted potions and
     shop-bought potions land.
  2. `AtbInventory` (`NestedTypes.cs`) — a *typed* combat inventory `{Potions, ManaCrystals,
     Cleanses, Torches}` that **already persists** in `SaveSchema` (`inventory.potions`,
     `inventory.manaCrystals`, …). **`ManaCrystals` already exists as a persisted field** but
     is unused by the real-time village magic loop.
- **`ShopPanel` sells `minor-mana-potion`** (hardcoded in `_potionIds`, line 52) — but that
  id is **not in consumables.json**, so buying it deposits a dead string that `TryUse` can't
  resolve. The mana potion is half-wired: sold, never usable.

**Gap (the core one):** potions are bought infinitely for a few Wood, persist forever, and
heal — there is no scarcity beyond cost, and **mana potions point at a mana pool that refills
itself for free.** A consumable that restores a free-regenerating resource has zero value.

---

## B) Persistent magic design

The smallest change that makes magic persistent AND gives consumables a job: **make the
hero mana pool a persisted, resource-gated number that does NOT trivially self-refill.**

### B1. Persist the mana pool (the keystone change)
Add the hero's mana to the save. Reuse the **already-persisted** `AtbInventory.ManaCrystals`
slot's sibling pattern — but mana is a hero-state scalar, so add it as a first-class field.

- **New `GameState` field + `SaveSchema.PersistedState` field** (append-only, save v21):
  - `public double Mana;` (current mana) — clamp `>= 0`, `RequireFinite`.
  - `public int ManaMax = 10;` (so spell-school upgrades can raise the ceiling later).
  - Default on a fresh/old save: `Mana = ManaMax = 10` (mirrors today's `_maxMana`).
- `HeroAbilities.Awake` reads `Mana`/`ManaMax` from `GameStateService.Instance.State`
  instead of resetting to `_maxMana`. On every spend (`_mana -= manaCost`) and on a refill,
  write back through a small `GameStateService.SetMana(double)` mutator (Village→Core is
  legal; Core must NOT reference Village — write the scalar directly, per memory
  `core-cannot-reference-village`). Debounce the save (don't `Save()` every frame —
  save on scene exit / on spend, like `VillageInventory.SyncToState`).

### B2. Make mana a *managed* resource, not a free regen
Today mana regen (`0.9/s`) refills the whole pool in ~11s — so it never runs out in practice
and a potion is pointless. Two tunable options (owner picks the feel):

- **Option A — slow trickle (recommended, least disruptive):** drop passive regen to a token
  rate (e.g. `0.15/s`, or zero out of combat) so heavy casting drains the pool over a fight
  and mana potions become the fast refill. Keeps the existing field; one inspector default.
- **Option B — charges model:** convert the big spells (W/E/R) to integer **charges** that do
  not regen at all; only consumables / rest restore them. Cleaner "scarcity" but a bigger
  change to `HeroAbilities`. **Recommend deferring** B to a later pass; A delivers the value
  link now.

Either way, **mana now persists between sessions** — you log back in with the mana you had,
not a free-topped pool. That is the "magic is persistent" half.

### B3. The Crystals → spells → jewelry → armor arc (progression spine)
The memory model is *"Crystals = special arc: unlock spells now → jewelry → armor later."*
Wire the **first rung** only (scope discipline):

- **Crystals unlock spell tiers / raise ManaMax.** `GameState.Resources.Crystals` already
  persists (save v18, single source of truth). Add a tiny persisted set of **unlocked spell
  upgrades** — reuse the existing `OwnedItemIds` list (already persisted) with ids like
  `spell-w-unlocked`, `manamax-2`, so **no new collection/schema field is needed** for the
  unlock ledger. A vendor/Yarn node spends Crystals to add the id; `HeroAbilities`/`AbilityCatalog`
  checks `OwnedItemIds.Contains(...)` to gate a slot or bump `ManaMax`.
- This makes Crystals the persistent magic-progression currency (learned-spell ledger +
  bigger mana pool), with jewelry/armor as the **deferred** later rungs of the same arc — do
  NOT build them now.
- **Do not reuse the `GameState.Magic` field** for hero mana — it is already the
  building-tech currency. Keep mana separate (`Mana`/`ManaMax`) to avoid the name collision.

**Net persistent-magic surface:** `Mana`, `ManaMax` (new, save v21) + spell-unlock ids folded
into the existing `OwnedItemIds`. Spells are unlocked by spending Crystals, leveled via the
existing talent trees, and the mana you cast with survives the session.

---

## C) Consumable VALUE design

Consumables matter when they (1) are scarce, and (2) restore/grant something the player
genuinely runs out of. B makes mana the thing you run out of; C makes consumables the refill.

### C1. The mana potion becomes real (the central link)
- **Add `minor-mana-potion` (and `greater-mana-potion`) to `consumables.json`** with
  `effect: "mana"`, magnitude e.g. 4 / 9. (`ShopPanel` already sells `minor-mana-potion` —
  this finally makes that purchasable id resolve.)
- **Un-stub the Mana branch** in `ConsumableUseService.ApplyEffect`: route `ConsumableEffect.Mana`
  to a mana-restore call. Because mana now lives on `GameState`, the service restores it by
  `GameStateService` (`State.Mana = min(ManaMax, Mana + magnitude); Save()`), and `HeroAbilities`
  reads the live value — no Village↔Core circular ref, no scene-coupling. Delete the stale
  *"no mana pool wired"* log.
- Now: **persistent mana that real spells cost (B2) + a consumable that refills it = the
  potion has value.** A frost nova / meteor you can't afford to spam is a potion you want to carry.

### C2. Scarcity (so potions aren't free flavor)
- **Cost gate:** keep buying potions through `EconomyService.TrySpend` (already wired in
  `ShopPanel.TryBuyPotion`) but price mana potions in **Crystals** (the magic-arc currency),
  not trivial Wood — e.g. `crystals: 1` for minor. Heal potions stay cheap Wood; mana potions
  cost the scarce magic currency, tying the refill to the same arc as the spells.
- **Craft gate:** the drop→craft path already exists (`consumable-recipes.json` +
  `ItemDropSystem`). Add a mana-potion recipe (e.g. `rare-essence` + `wild-herb`) so the
  *earned* path is gather→craft, the *bought* path is Crystals. Both deposit the same id into
  the same persisted larder (`VillageInventory`).
- **Carry limit (optional, owner call):** a soft cap (e.g. 5 mana potions carried) turns
  "buy 99 and never think about it" into a real loadout choice. Cheapest version: cap at
  buy/craft time in `ShopPanel`/crafting (read `VillageInventory.Get(id)`), no schema change.
  **Recommend shipping without a hard cap first**, add only if testing shows potion-spam.

### C3. Meaningful effects beyond heal/mana (deferred, named for completeness)
- **Buff** (`effect: "buff"`) and **revive** are still stubbed/absent. Keep them DEFERRED;
  they are not needed to make the magic↔consumable link land. The mana potion alone closes
  the owner's loop.

---

## D) The unifying loop (one paragraph)

You **gather** resources and crystals and **kill** enemies for drop materials; you spend
crystals to **unlock/upgrade spells** and **buy or craft** mana potions; in the field your
spells now draw from a **persistent mana pool that no longer refills for free**, so a hard
fight **drains** it; **mana potions refill that persistent pool**, so you carry them, spend
them, and ration them — and deeper/bigger spells (W/E/R) cost more mana, which makes you want
*more* potions and *more* crystal income. Consumables have value **because** magic is
persistent and resource-gated: the potion restores a number you actually run out of and
carry between sessions, instead of one the engine hands back for free every ten seconds.

---

## E) Implementation slices (priority order, bounded)

Each slice is independently shippable; the value link lands by **Slice 3**.

**Slice 1 — Persist mana (save v21).** *(Core lane — schema)*
- Add `Mana` (double) + `ManaMax` (int, default 10) to `GameState.cs` and
  `SaveSchema.PersistedState` (append-only at END; default-on-read like `Magic`/`partyMemberIds`,
  no migrator step needed). Add `Validate` clamps (`NonNegInt`/`RequireFinite`).
- Bump `SaveSchema.CurrentVersion` to 21; one line in the version comment.
- Add `GameStateService.SetMana(double)` (+ debounced save).

**Slice 2 — `HeroAbilities` reads/writes persisted mana.** *(Village lane)*
- `Awake`: seed `_mana`/`_maxMana` from `State.Mana`/`State.ManaMax` (fallback 10).
- On spend and on scene-exit, write back via `SetMana`. Keep the API identical so the HUD
  mana bar is unchanged.
- Tune passive regen down (B2 Option A — inspector default `_manaRegenPerSecond ≈ 0.15`,
  or 0 out of combat). **Owner approves the rate before this slice merges** (feel call).

**Slice 3 — Mana potion gains value.** *(Village lane — the payoff)*
- Add `minor-mana-potion` / `greater-mana-potion` rows to **both** copies of
  `consumables.json` (Resources + StreamingAssets — dual-copy law, data-catalogs §1).
- Un-stub `ConsumableUseService` Mana branch → restore `State.Mana` (capped at `ManaMax`),
  `Save()`. Remove the stale log.
- Price mana potions in Crystals in `ShopPanel.TryBuyPotion` (already a one-line cost in the
  `_potionIds` loop — it currently sets `crystals:1` for mana ids; verify it resolves now that
  the id exists in the catalog).
- **After this slice the owner's goal is met:** persistent, costed magic + a potion that refills it.

**Slice 4 — Crystal→spell unlock ledger (the arc's first rung).** *(Village + a Yarn vendor node)*
- Add a vendor/Yarn command that spends `Resources.Crystals` to append a `spell-*` / `manamax-*`
  id to the existing persisted `OwnedItemIds` (no new schema field).
- `HeroAbilities`/`AbilityCatalog` gate a slot or raise `ManaMax` on those ids.
- Keep jewelry/armor rungs **out** (deferred — memory model's "later").

**Slice 5 — Earned craft path + optional carry cap.** *(only if ItemDropSystem is turned on)*
- Add a mana-potion recipe to `consumable-recipes.json` (both copies).
- Optional soft carry cap at buy/craft time (no schema change). Ship only if spam shows in test.

**What NOT to touch:** the `GameState.Magic` building-tech currency (different system); the
`AtbInventory` ATB combat path (separate battle mode — leave `ManaCrystals` as-is unless the
owner wants the ATB potion unified later); the dark `ItemDropSystem` flag (Slice 5 is gated on
the owner choosing to enable drops).

---

## Biggest open question for the owner

**How scarce should mana be — and is the real-time village magic loop the right place to spend
the consumable, or the ATB battle?** The whole "consumables have value" payoff hinges on mana
actually running out. That is a **feel decision only the owner can make in the editor**: how far
to cut passive regen (Slice 2, Option A vs the bigger charges model B), and whether mana potions
restore the **real-time `HeroAbilities` pool** (this design's path) or the **already-persisted
`AtbInventory.ManaCrystals`** used in ATB battles — or both. Two persisted mana-ish stores already
exist (`HeroAbilities` in-memory pool vs `AtbInventory.ManaCrystals`); we should pick ONE as the
canonical "mana the player manages" before wiring the potion, or risk a third unreconciled store
(cf. the three-persistence-stores flag in economy-meta). Recommendation: make the **real-time
`HeroAbilities` pool** canonical and persisted (it's where spells are actually cast in the
shipping village loop), and treat `AtbInventory.ManaCrystals` as the ATB-only variant for now.
