> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: zero catapult `.cs` files exist in the tree.
> The previous Status line read "SPEC - not started. Blocked on nothing, but see sec.5 sequencing." and was wrong.

# WORK ORDER 906 — The Catapult becomes a DEPLOYED offensive siege unit

**Status:** NOT STARTED (reconciled 2026-08-08, see banner). Blocked on nothing, but see §5 sequencing.
**Minted:** 2026-08-04 (CLI), owner ruling
**Lane:** Combat / Raid. Moves content between two systems — read §2 before writing code.
**Adjacent:** WO-853 (structures are targetable — the thing this sieges), WO-820 (raid full-army gate),
WO-870 (tower VFX — deliberately wires nothing for the catapult)

---

## 1. The ruling

> **Owner, 2026-08-04:** *"The catapult will be added for attacks (to siege from distance) but was never
> wired yet."* — and, asked whether it is built or deployed: ***"deploy offensively."***

**The Catapult is a DEPLOYABLE offensive siege unit that the player brings to a raid. It is NOT a placed
defensive structure.**

---

## 2. ⚠ THE CATAPULT IS CURRENTLY AUTHORED AS THE OPPOSITE OF WHAT IT IS

This is the whole difficulty of the WO, and it is not a tag change.

**What it is today** (`Assets/Resources/Data/Canonical/structures-catalog.json`):
- `id: tower_catapult`, `displayName: "Catapult"`
- `repo.behaviorId: "DefenseTower"` — a **defensive placed structure**
- `repo.cost: 100 wood + 80 iron`, `repo.range: 28`, `repo.damage: 24`, `repo.fireRate: 0.8`
- `visualPrefabPath: Structures/Catapult` (the prefab exists)
- also referenced in `build-categories.json` and `scene-configs.json`
- ⚠ **Unreachable in play:** the build menu lists only the **cheapest FOUR of five** tower rows, and the
  Catapult is the one that falls off. No player has ever placed one.

**What the ruling makes it:** a unit in the **deploy** lane — `TroopController` / `TroopDeployer`, the
raid deploy flow — with train time, a production/tier home, deploy placement, and siege targeting.

**These are different systems.** A placed structure is spawned by `StructureFactory` from a catalog row
and lives in the town grid. A deployed unit is trained, stored, carried into a raid and placed by the
deploy UI. Moving between them is not a flag.

**The failure mode to avoid:** half of each. A catapult that is trained like a unit but spawned like a
building, or one that appears in both the build palette and the deploy tray, is worse than either. **Pick
the destination system and move it wholly.**

---

## 3. What already exists (find and reuse — do not greenfield)

Today has repeatedly turned up systems already built and never wired; check each before writing:

- **`TroopController` / `TroopDeployer`** — the deploy lane. `TroopController` already sweeps for
  `IDamageable`.
- **WO-853 (shipped)** closed the disjoint `IDamageable` / `IDamageableStructure` contract, so **walls,
  gates and enemy towers are damageable for the first time.** That is the thing a siege weapon exists to
  break — the seam this WO depends on did not exist before 2026-08-03. Read WO-853 before designing
  targeting.
- **`troops.json` / `BarracksService` / `BarracksProgression`** — train times, costs, the research ladder.
- **The raid deploy flow** (WO-820) — how a player brings an army in and places it.
- **`repo.siegeValue` / `highValueTarget`** — authored in Grok's WO-858 for *collectors*, but the
  vocabulary for "what a raider wants to hit" may already be there. Check it.

---

## 4. The design questions this WO must answer

1. **Where does it live in data?** A row in `troops.json`, or its own siege-unit set? It is not a troop in
   the usual sense — it does not brawl. Say which and why.
2. **Siege targeting.** *"Siege from distance"* implies it prefers **structures over bodies** and
   outranges defences. Define the targeting rule explicitly; `TroopController`'s existing sweep is
   body-first. Its authored range of 28 currently outranges every tower except the Sky Ballista (36) —
   whether that survives the move is a balance decision.
3. **How is it produced?** Barracks? A dedicated siege workshop? This decides whether it needs a new
   building or rides an existing one.
4. **Does the old catalog row stay?** ⚠ **Do NOT delete it until the new home works.** It is referenced
   by `build-categories.json` and `scene-configs.json`; removing it blind will break those. Retire it in
   the same change that lands the replacement, and say so.
5. **Balance.** Its current 100w/80i price and 24 dmg / 0.8 rate were tuned as a *tower*
   (basket/DPS 11.46, comfortably in band). As a deployed consumable those numbers mean something
   completely different. **Re-derive; do not carry them across.**

---

## 5. Sequencing

**No hard blocker** — WO-853 already shipped the damageable structures this needs. But it is worth
landing **after** the current UI wave (WO-865–871) so it does not compete for the same files, and its
balance should be derived against the post-WO-855 economy rather than the pre-cut numbers.

---

## 6. VFX — nothing is owed yet, deliberately

WO-870 wires **no projectile and no impact** for the catapult, correctly: it is unreachable content, so a
tagged effect would be dead data. **When this WO lands, the catapult will need its VFX tagged in
`VfxCasterWindow`** — and per the standing rule the owner tags and the implementer maps verbatim.

Note for whoever gets there: `tower_catapult` authors **no `projectileStyle`**, so it falls back to
`pellet` — a round stone, which is arguably right for a catapult and may need no change at all.

---

## 7. What NOT to do

- **Do NOT "tag it offensive" and call it done.** The ruling changes which SYSTEM owns it.
- **Do NOT leave it in both lanes** — no catapult in the build palette AND the deploy tray.
- **Do NOT delete the catalog row, prefab or data before the replacement works** (§4.4).
- **Do NOT carry the tower-balanced numbers across unexamined** (§4.5).
- **Do NOT surface it in the build menu as an interim step.** That ships the exact thing the ruling says
  it is not, and players will learn it as a tower.
