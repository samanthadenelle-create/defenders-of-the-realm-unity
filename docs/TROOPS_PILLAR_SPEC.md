# Troops Pillar — Spec (owner direction 2026-06-14)

**Status: SPEC / PRIORITIZE-NEXT.** The design has leaned on this repeatedly — raids are weak solo,
defense wants a garrison, the arena wants units. Troops aren't polish; they're the **player's
deployable army**, the missing agency layer. Companion to [`RAID_PILLAR_VISION.md`].

## The core idea: ONE finite army, THREE uses
Build a finite army once (at the barracks); use it everywhere. NOT three systems — one.

| Mode | The army does | Already exists |
|---|---|---|
| **Defend** | Troops garrison your base, fight the waves alongside towers | wave loop, towers |
| **Raid** | Deploy a selected army at an enemy base | raid generator, scene-ownership, enemy turrets/variety |
| **Arena** | The same troops fight (PvE / SKR-wager) | arena WO-388/389, SKR-wager stub |

This is the CoC "train an army → raid" loop fused with Warcraft unit control — the North Star, bounded.

## Barracks + FINITE troops (the stakes)
- **Barracks** = a buildable/upgradeable structure (existing build system + catalog `type:"troop"`).
  Trains troops for resources (Wood/Iron/Food) over a build timer.
- **Finite army cap** (CoC "army camp / housing space"): you can only field N troop-slots; bigger
  troops cost more slots. Upgrading the barracks/camp raises the cap.
- **Finite = the whole point.** You can **lose your army in a hard raid → must rebuild.** That is the
  resource sink, the tension, and the thing an **SKR wager** is staked against. Infinite troops would
  gut all three. (Owner: "finite troops.")

## Troop types + the AIR ruleset (solve the air problem)
A small, readable roster (not an RTS tech tree — scope discipline):
- **Melee** (frontline brutes) — soak walls/turrets.
- **Ranged** (archers/casters) — hit from range; double as **anti-air**.
- **Air** (a flyer — the premium raid unit).
- **Siege** (optional later — catapults to crack walls, mirroring enemy catapults).

**Air ruleset (the real "air problem" solve):** air **flies over walls** (so it bypasses the CoC wall
system — by design), BUT is **hard-countered by anti-air** (dedicated AA towers + ranged troops). So
air is a premium option with a clear answer, not an unanswered "ignores everything." This applies BOTH
ways: enemy air (the dragon) is countered by YOUR AA towers/ranged; your air is countered by enemy AA.
Fixes the original "dragon ignored the towers and went for the tree."

## Deployment loop
1. Build troops at the barracks (resource + time + slot cost).
2. **Select an army** (within the cap) for a target.
3. Deploy → **Defend** (auto-garrison on a wave), **Raid** (the generated enemy base), or **Arena**.
4. Survivors return; **the fallen are gone** (finite) → rebuild.

## Rewards loop (ties to raids)
Raid clear → **time-based 1/2/3 stars** → escalating **resource rewards** (the troop-rebuild fuel),
**plus a CHANCE drop of a new Echo (pet)** on a strong clear (3-star) — raids become a pet-acquisition
source feeding the Echo Hollow. Stars → resources → more/better troops → harder raids. Self-reinforcing.

## Integration (reuse, don't greenfield)
- **Catalog:** troops as `type:"troop"` entries (build-system catalog) — same factory as structures.
- **Build/economy:** existing resource gather + build-timer + barracks upgrade tiers.
- **Combat AI:** troops reuse the enemy/hero combat seams (`IDamageableStructure`, the brain/locomotion)
  — a troop is "a friendly Enemy" mechanically (faction-flipped), mirroring the EnemyOwned-tower pattern
  we just shipped (`DefenseTower.TowerAllegiance`). Strong precedent: one combat core, faction decides sides.
- **Raid generator / scene-ownership:** the deploy target IS the streamed enemy base (load-on-demand,
  destroy-on-leave). Troop death in a raid = the finite loss.
- **Arena / SKR:** same army into the wager fights.
- **Pets/Echoes:** the chance-drop reward + Echoes-as-troops question (below).

## Open design questions (for creative feedback)
1. **Echoes vs troops:** are Echoes (pets) a SEPARATE class, or are some troops *unlocked* as Echoes
   (the chance-drop)? Owner leaned toward Echoes mattering — do Echoes fight as elite troops?
2. **Control depth:** full unit-by-unit control (Warcraft) vs deploy-and-they-fight (CoC)? Recommend
   **CoC-lean** (deploy + simple commands) for scope, with hero+companions as the "controlled" core.
3. **Persistence:** does the standing army persist across sessions (save), and how does the finite
   loss feel fair (rebuild cost/time tuned so a wipe stings but isn't punishing)?
4. **Air balance:** AA strength vs air HP — air should be scary but answerable, never dominant.
5. **Slot economy:** army-cap curve + per-troop slot costs (the core balance dial).

## Sequencing
Per the project scope line, this is the **roaming-troops / post-grant** pillar — the pre-store-listing
critical path is the tight hero+companion loop the tonight-fixes stabilized. So: **spec now (this doc),
build after the current loop is bot-verified.** It is the deliberate next-big-pillar, not scope-drift —
prioritized here so the raid + arena work we're doing now is built *toward* it (e.g. the raid generator
already names turrets for faction-arming, which the troop faction model reuses).

**Build order when greenlit:** barracks structure + troop catalog → finite-cap economy → troop combat
(faction-flipped enemy brain) → deploy-to-defend → deploy-to-raid (the generated base) → air + AA →
arena/SKR hook.
