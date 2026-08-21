**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 161 — Player Home: a walk-in personal interior (gear display, skinning, founder identity)

**Status: DRAFT — feasibility + design captured; build when prioritized**
**Priority:** Medium — high *feel* + monetization value; the one walk-in interior worth building
**Date:** 2026-05-30
**Lane:** gameplay + world (interior scene) + monetization-adjacent. Reuses DungeonController scene-transition + KayKit dungeon furniture + the existing cosmetic stack.
**Source:** owner — the ONE building interior worth entering is **the player's HOME**: "where your gear is and is yours," supports skinning/novelty, and ties to a **Genesis/Founder** premium identity. Function = **walk to a counter** to interact (not a panel).

---

## Concept — TWO authored interiors (Home + Store), the rest reuse the Store as copies

Owner direction (2026-05-30): build **three bespoke interiors now**, and let the remaining buildings
**reuse a re-skinned copy** of the Store for the time being:

1. **The Player Home** — the unique, personal one. You enter, walk around, your **gear is displayed**,
   you can **skin/customize**, and it carries **founder/Genesis identity** (a Genesis holder / Founders-
   pack owner gets an elevated, "for life" home). The destination that's *yours*.
2. **The Pet Home** — bespoke (promoted out of the copy bucket, owner 2026-05-30). A space where you
   **see all your pets living their best life** — your collected Wardens wander, lounge, play in their
   own home. This is a collection/retention beat (the "visit your pets" moment players love), worth a
   real authored room, not a relabeled shop. Manage roster/skill-tree at a counter inside.
3. **A Store interior** — one authored shop room (counter, merchant, shelves) that doubles as the
   **reusable template** for the remaining functional buildings (Forge, Tower, future Lumbermill), which
   reuse it as a **re-skinned copy** (below) until each earns its own.

So: **three bespoke interiors (Home, Pet Home, Store) + the Store re-skinned for the rest.** Each
building's *function* lives at a **counter inside** its room — the owner's "walk to a counter" model.

### Design intent — the cozy/collector player (owner 2026-05-30): "some people will be happiest there"

Not every player is here for raids and deep-zone risk. **A real segment's whole reason to log in is the
cozy collection space** — visiting their pets, decorating their home, showing off their gear. For that
player, the **Home + Pet Home ARE the core loop**, the way node-defense is the conqueror's. This is an
explicit, valued audience — design these two rooms with that in mind:
- They should reward *just being there* — pets that react to you, a home that reflects your journey
  (gear/trophies earned), gentle life and ambiance, not just a menu behind a door.
- They're the **soft, retention/identity** counterweight to the hard territory loop — together they
  cover both player drives (conquer vs. collect/nest). A game that serves both keeps more people.
- This justifies the bespoke authoring (vs. a re-skinned shop): for these players the room *is* the
  content, so it earns the polish. Cosmetics/skins/founder decor (below) are most meaningful to exactly
  this segment — the monetization and the cozy-player happiness point the same direction.

> This keeps scope sane: we author 2 rooms, not 6. The copies are explicitly a **placeholder stage** —
> flagged so "Forge looks like the Store inside" is a known, intended interim, not a bug.

### Re-skin the Store copy per building type (owner 2026-05-30)

The copies aren't blind duplicates — the **same room shell + counter gets RE-SKINNED** to match the
building, so each reads as its own place without authoring new geometry. Swap props/materials/lighting on
the shared template:

| Building | Store-copy re-skin | Counter function |
|---|---|---|
| **Store** | the base template — merchant shelves, wares, till | open PackStore at the counter |
| **Forge** | re-skinned **blacksmith / armorer** — anvil, forge fire, weapon & armor racks, sparks | craft / upgrade at the forge counter |
| **Tower** | re-skinned arcane study — books, sigils, talent desk | talent tree at the counter |

*(Pet Home is NOT in this table — it's a bespoke interior, see above.)*
| **Lumbermill** | re-skinned sawmill/woodshop — log piles, saw, lumber stacks, sawdust | wood production / upgrade at the counter |

Same floorplan, **different dressing + counter** = distinct feel, ~no new authoring (prop swaps from the
KayKit pack). The **blacksmith/armorer skin of the Forge** is the first/priority re-skin (owner named it);
the **Lumbermill** re-skins to a sawmill/woodshop (note: Lumbermill is a not-yet-built building per WO-151
— its interior re-skin lands when the building does).
Later, any building can graduate from "re-skinned Store copy" to a bespoke room — the re-skin is the
interim that already looks intentional, not a placeholder box.

## Feasibility — most machinery already exists (reuse, don't greenfield)

| Need | Exists? | Reuse |
|---|---|---|
| Walk-in interior scene + enter/return transition | **YES** — `DungeonController` + dungeon scenes already do load-interior → walk → return | clone the pattern for a (smaller, cozy) home scene; reuse the door-trigger/portal + return flow |
| Interior dressing assets | **YES** — KayKit Dungeon Remastered furniture (beds, shelves, tables, banners, candles) already imported (Healer's Cottage uses them) | dress the home from this pack |
| Two-scene additive loading | **YES** — Village + OuterWorld split just built (`WorldSceneLoader`) | same plumbing for a Home scene |
| Cosmetics / skinning | **PARTIAL** — `CosmeticApplier` / `CosmeticShopPanel` in the monetization stack (PIPELINE_STATE §5) | home skins = cosmetic entries applied via the existing applier |
| Founder / Genesis tier | **SPEC'd** — `docs/monetization-v2-spec.md` (founder/genesis) + WalletService (Genesis NFT holder check) | gate the elevated home / "founder for life" off the existing entitlement check |
| Gear / loadout data | check `HeroProgression`/inventory | the home *displays* the player's owned gear — reads existing gear/loadout state, doesn't invent it |

So the **scene-transition, interior assets, cosmetic applier, and founder entitlement all exist** — this
WO assembles them into a Home: author one room, wire the door, place a gear display + an interaction
counter, and hook skins/founder gating.

## What to build

1. **Home interior scene** (`PlayerHome.unity`) — one authored cozy room (KayKit furniture), loaded
   additively / via the dungeon-style transition when the player enters the Home building in the village.
   Return-to-village on exit (reuse `DungeonController`/portal return).
2. **Door / entry trigger** on a Home building in the village (build it into the roster — the home is the
   player's, distinct from the 5 functional buildings) → loads `PlayerHome`.
3. **Gear display** — props in the room that show the player's owned/equipped gear (weapon on a rack,
   armor on a stand, trophies). Reads existing gear/loadout state. "Is yours" = persists, reflects you.
4. **Walk-to-a-counter interaction (owner choice).** Interactions happen at **counters/stations inside**
   — walk to the wardrobe to change skins, to a stand to manage gear — NOT a popup at the door. (This is
   the one building where function moves *inside*, per owner.)
5. **Skinning / novelty** — the home (and/or gear/hero) supports cosmetic skins via the existing
   `CosmeticApplier`; the home is where you apply them (wardrobe counter). Room for novelty cosmetics.
6. **Founder / Genesis identity** — a **Genesis holder / Founders-pack owner gets an elevated home**
   (exclusive skin/decor, a "Founder" marker, "for life" entitlement) gated off the existing founder/
   Genesis entitlement check (WalletService / monetization-v2). Non-holders get the standard home.

## Open questions for owner (design calls — non-blocking to scope)
- **Founder mechanic:** is the elevated home tied to a **Genesis NFT holder check**, a **Founders-pack
  purchase**, or **both**? ("I don't know" — flagged; recommend: either entitlement unlocks it, checked
  via the existing WalletService/monetization entitlement, so it's one gate with two sources.)
- **What "for life" means:** purely cosmetic prestige (exclusive home/skin/marker), or does it carry
  ongoing perks? (Recommend cosmetic-prestige first — clean, no balance risk; perks later.)
- **Gear display depth:** static showcase of owned gear, or interactive (equip/manage from the home)?
- **Scope:** one shared home room for everyone (skinned per player), or tiered rooms by progression/founder?

## Constraints (CLAUDE.md §5/§6/§9)
- Reuse `DungeonController` transition + KayKit dungeon furniture + `CosmeticApplier` + founder entitlement
  — **do NOT build a new scene-transition, cosmetic, or entitlement system.**
- Home scene authored via a builder/editor pass (architect lane, single-writer) — not hand-saved.
- Persists "your home" state (skins applied, founder status) via GameState/SaveSchema round-trip.
- No new currency; founder/cosmetic purchases ride the existing monetization stack. No UXML (code-built
  counters/UI). Village→Core only; `?.` cross-module.

## Effort estimate (owner asked "how hard to enter?")
- **Plumbing (enter/return, scene load): LOW** — clone the dungeon transition.
- **Content (author + dress one room): MEDIUM** — one room, existing furniture pack.
- **Gear display + wardrobe counter + founder gating: MEDIUM** — wires to existing cosmetic/entitlement.
- **Net:** a few days of mostly-reuse, NOT a from-scratch system. The expense is room authoring + the
  founder/cosmetic design calls, not engine work.

## Done checklist (CLAUDE.md §10)
- [ ] `PlayerHome` interior scene authored (KayKit furniture); enter from village + return (dungeon pattern reused)
- [ ] Walk-to-counter interactions inside (wardrobe/gear), not a door popup
- [ ] Gear display reflects player's owned gear; persists
- [ ] Skinning via existing `CosmeticApplier`; founder/Genesis elevated home gated off existing entitlement
- [ ] No new transition/cosmetic/entitlement/currency systems; brace balance; Village→Core only
- [ ] Owner design calls (founder mechanic, "for life" meaning, gear depth, scope) resolved before final build
- [ ] `WORK_ORDER_161_player_home_interior.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
