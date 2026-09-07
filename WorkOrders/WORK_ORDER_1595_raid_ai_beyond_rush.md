# WO-1595 — Raid AI: breach → goal, peel aggro, formation roles

**Status:** READY TO IMPLEMENT — after / parallel with targeting fixes already in flight  
**Minted:** 2026-09-07 — program WO-1592  
**Amended:** 2026-09-07 — owner felt: troops start on walls and **stay** on walls; want breach then push to capture goal + formation  
**Amended:** 2026-09-07 — owner ruled: capture the spire; **if aggro / being attacked, prioritize staying alive**  
**Amended:** 2026-09-07 — owner ruled: troops **deploy and move as a formation** (Front ahead, ranged/DPS behind, healers safe)  
**Priority:** P0 felt — “the AI all just seem to run and attack” + wall-chewing linger  
**Lane:** Troops / garrison AI (file-disjoint from 1593 scene art)  
**Respects:** RAID_BALANCE_AUDIT — do not rebalance HP until staging + targeting hold; defenders must not friendly-fire the spire

---

## 0. Owner voice (binding — 2026-09-07)

> Other issues: the troops I deploy start killing the walls and stay on walls.  
> The idea is **breach the walls and then start moving towards goal (capture the base)**;  
> if getting defensive aggro, kill it;  
> **tanks up front, DPS and ranged behind, healers supporting safely**.

> The idea is **capture the spire** but if aggro or being attacked should **prioritize staying alive**.

> Yes they should **deploy and move as a formation**.

This supersedes the softer “roles, not one brain” framing below where they conflict. Formation + breach→objective is the product sentence. **Survival peel beats the push** while under attack. **Formation is V1, not a later polish.**

---

## 1. Problem (proven + felt)

### 1.1 Felt

Deployed troops **start chewing walls and keep chewing along the ring** instead of punching a hole and driving to the **spire / capture objective**. Raid reads as masonry farming, not an assault.

### 1.2 Proven in code / capture (do not re-guess)

`TroopController.NearestHostile` (`Assets/_Modules/Village/Troops/TroopController.cs` ~846–899) already documents the 2026-09-06 capture:

- `preferStruct=False` (every non-siege role) still picked `Wall_Outer_SS_11` with a **live unit in the sweep** (`accepted[unit=1,struct=17]`).
- Same archer then walked **SS_11 → Watchtower → SS_12 → SS_7 → SS_13 → SS_6 → SS_14** — **outward along the ring**. That is “stay on walls” in data.

Selector rule today (`PrefersUnitOverStructure`): non-siege prefers a unit only if **in attack range** OR a **complete non-detour NavMesh route** to the unit exists; else nearest-wins between unit and structure. After a segment dies, the next-nearest **wall panel** often wins again → ring walk, never a committed push to `RaidSpire`.

Win condition is already **raze the central spire** (`RaidVictoryController` — “THE OBJECTIVE”). Troops do **not** have an explicit “push objective after breach” phase — only nearest-hostile hunt.

Only `role == "siege"` sets `_preferStructures` (WO-933). Day-one Footman/Archer are not siege, so they are in the broken nearest-wins bucket.

---

## 2. Creative design — assault loop + formation

### 2.1 Assault phases (every attacker)

| Phase | When | Behavior |
|---|---|---|
| **Peel / Survive** | Self (or nearby ally for Front/Tank) is under attack / has hostile unit aggro in leash | **Highest priority.** Kill or break the threat so the troop **stays alive**. Ranged may kite / hold range; Front holds the contact. Resume Push only when the immediate threat is dead or out of leash. |
| **Breach** | Safe, and no walkable route toward the spire / through the wall ring | Commit to a **path-blocking** structure (gate / wall on the approach), not “any nearest wall”. Once a hole is open (route to objective or to interior units completes), **leave the ring**. |
| **Push** | Safe, breach open | Move toward **RaidSpire** (capture / raze goal). Do **not** retarget the next outer wall segment for sport. Do **not** clear the whole garrison for its own sake — only peel what threatens the push. |
| **Finish** | Safe, in range of spire | Attack the objective until razed. |

**Priority stack (owner-ruled):** Survive/Peel → Breach (if blocked) → Push/Finish spire.  
“Stay on walls” after a hole exists is an **acceptance FAIL**, not a siege feature.  
Clearing every defender is **not** required for victory — the spire is the goal; fighters only interrupt the push.

### 2.2 Formation roles (owner — binding)

| Role | Who (examples) | Position | Target priority |
|---|---|---|---|
| **Tank / Front** | Footman, Spearman, heavy | **Up front** — contact line, absorb hunters | Peel units in leash → else Push/Breach as above |
| **DPS melee** | (when unlocked) | Just behind / beside tanks | Same peel → push; never lead alone into intact wall |
| **Ranged / DPS** | Archer | **Behind** front; hold max range | Shoot units in range first; structures only if no unit and still in Breach; **never** walk into wall melee to “help” |
| **Breaker** | Siege / wall-prefer | On the breach job only | Prefer path-blocking structures until hole; then Push like everyone else (do not farm the ring) |
| **Healer / Support** | Healer (when unlocked) | **Safely behind** — never lead the charge | Heal lowest ally in radius; peel only if self threatened; no wall chewing |

**V1 ships formation on day-one army (Footman + Archer):** they **deploy into** and **advance as** a formation — Front ahead toward the objective / breach, Ranged held back at standoff. Keep Breaker for siege defs; Tank/Healer slots use the same offset table when those units unlock (hooks reserved so the shape does not rot).

Formation is **not** “same destination, hope roles diverge.” It is:

1. **On deploy** — slot each troop onto a role-relative offset from the deploy point / march axis (toward spire).  
2. **On move (Breach / Push)** — destinations stay role-layered (Front closer to objective, Ranged/Support behind Front’s line).  
3. **On Peel** — Front may step up to the threat; Ranged/Support hold or kite — they do not collapse into one blob on the wall.

### 2.3 Defender jobs (garrison) — unchanged intent

| Job | Behavior |
|---|---|
| **Hold** | Stand near post (tower / gate / spire ring); chase only inside leash |
| **Patrol** | Short loop; on alert → Hold at nearest post |
| **Hunter** | Intercept **closest deployed player unit** (not the spire) |
| **Tower crew** | Stay on/near tower |

Easy camp: mostly **Hold + Hunter**. Hunters must **not** friendly-fire the spire.

### 2.4 Anti-patterns (ban these)

- Walking the wall ring after a breach exists (the proven archer path).  
- All units sharing one “nearest anything” scorer with no phase / role weight.  
- Archers pathing into wall contact range.  
- Healers or ranged leading the breach.  
- Defenders retargeting friendly structures / spire.  
- Silent AI — every phase / role / target-class change gets a throttled `[Flow:RaidAI]` line.

---

## 3. Implementation shape

1. **Objective pointer** — resolve `RaidSpire.Active` (or equivalent) once per raid; troops know “goal position” for Push.  
2. **Breach gate** — reuse / harden the existing route filter (`RefreshRouteToUnit` / breach probe): while no route toward objective interior, allow structure targets that **block the approach**; when route opens, **forbid** picking another outer wall unless no unit and no objective reachable.  
3. **Role table** — map `TroopDef.Role` (+ optional `raidJob`) → Front / Ranged / Breaker / Support; JSON dual-copy if new field.  
4. **Formation (V1 — owner-required)** — deploy + march as a formation, not a blob:
   - March axis = deploy point → RaidSpire (or breach point while blocked).  
   - Role offsets along that axis (Front forward, Ranged/Support back by authored standoff meters).  
   - Lateral spread so same-role units do not stack on one nav point.  
   - Nav-sample destinations (no teleport). Exact meters tunable; **readable layers on felt test** are the bar.  
   - Likely touch: `TroopDeployer` (initial slots) + `TroopController` move destination bias while not peeling.  
5. **Peel / survive leash** — if self is taking damage or a hostile unit has aggro in leash, that unit **beats structure and beats the spire** (owner: stay alive). Front peels for nearby allies; Ranged peels with shots / kite; Support does not dive. When leash clear → resume Push.  
6. **Headless oracle** — fixtures: (a) after simulated breach, non-siege does **not** select next wall when route to spire/unit is open; (b) archer with live enemy in range does not select wall; (c) siege may still select blocking structure pre-breach.  
7. Instrument `[Flow:RaidAI]` phase + job + target class (keep existing TroopAI / TroopSiege lines; do not strip).

Primary files: `TroopController.cs`, `TroopDef` / `troops.json` (+ Resources dual-copy), garrison spawn job assign if touched. **Do not** hand-edit raid `.unity` scenes.

---

## 4. Owner rulings

**Q1.** Day-one jobs = Front + Ranged (+ Breaker for siege); Tank/Healer hooks reserved — **YES**; and they **deploy + move as a formation** — **YES (owner 2026-09-07)**.  
**Q2.** Defenders on Easy = Hold + Hunter only — **OPEN**.  
**Q3.** Easy “fun cheat”: hunters wait ~1.5s after first deploy so staging stays calm — recommend **YES** — **OPEN**.  
**Q4.** Goal = **RaidSpire** (capture / raze) as the Push target — **YES (owner 2026-09-07)**. Do not require wiping the garrison.  
**Q5.** After breach, **never** farm the wall ring — only retarget walls if the path to the spire is blocked again — **YES (implied by owner assault loop)**.  
**Q6.** If aggro / being attacked → **prioritize staying alive** (peel / survive beats Push) — **YES (owner 2026-09-07)**.

---

## 5. Acceptance

1. Felt: after a hole opens, troops **push toward the spire**, not the next wall panel along the ring. Garrison wipe is not required.  
2. Felt: when a troop is attacked / has aggro, it **peels to stay alive** before resuming the spire push.  
3. Felt: army **deploys and advances as a formation** — Front ahead, ranged/DPS behind, healers (when present) safe in back; not one blob on one wall tile.  
4. Siege/Breaker still prefers **path-blocking** structures pre-breach; post-breach joins Push (unless peeling).  
5. Footmen engage units before walls when both are in range / route-open.  
6. Archers do not pile into the same wall tile as melee.  
7. Garrison hunters do not attack the spire; Hold units stay near posts until leash broken.  
8. `[Flow:RaidAI]` (or existing TroopAI) lines name **phase + job + target class**; regression green; Easy camp still clearable with 10 slots when staging works.

## 6. Not in scope

KayKit meshes (1593), star HUD (1594), HP spreadsheet rebalance, army slot caps, full CoC pathing rewrite / SetDestination migration (unless a minimal change is required to stop ring-walk — call it out before expanding scope).

---

## 7. Paste for CLI

```text
Implement WORK_ORDER_1595_raid_ai_beyond_rush.md under program 1592.
North star: breach → push RaidSpire (capture); if aggro/attacked prioritize staying alive (peel beats push); DEPLOY AND MOVE AS A FORMATION (Front ahead, DPS+ranged behind, healers safe).
Stop wall-ring linger after a hole exists (proven SS_11→…→SS_14 path). Do not wipe-garrison-first. Do not rebalance garrison HP in this ticket.
```
