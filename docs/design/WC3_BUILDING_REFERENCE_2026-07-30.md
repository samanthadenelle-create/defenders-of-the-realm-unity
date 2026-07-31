# WC3 Building Basics — owner-supplied design reference (2026-07-30)

> Owner: "these are ideas we are playing against." Companion to the standing WWCD
> (Clash of Clans) tie-breaker and the WC3-style building-upgrade ruling. When a
> build/queue/economy design is ambiguous, mine THIS list first.

## Ideas mapped to our systems (mining index)

| WC3 idea | Our seam | Status |
|---|---|---|
| Production queue shown N-deep on the building | Builders queue chip (WO-778) + 5-deep rows | **BUILT 2026-07-30** |
| Shift-queue multiple placements | BuildModeController two-step place loop | candidate (multi-drop arm) |
| Rally points on production buildings | Barracks TrainTroopEffect landing spot | WO candidate |
| Buildings under construction take full damage | Destructible + UnderConstructionVisual | verify behavior |
| Repair = 35% cost / 150% time from 1 HP | structure repair pricing (StructureBurn/repair) | WO candidate |
| Cancel refunds: building 75%, research 100%, units 100%, upgrades 75% | BuildTimerService cancel path | WO candidate (no cancel verb yet) |
| Buildings as walls / choke formation | placement grid already supports | design note |
| Don't build hero-trap towns (leave exits) | navmesh + gate-lane clearance already guards lanes | design note |
| Group-select towers to focus a target | tower targeting | far-future |
| Occupied indicator on container buildings | Orc Burrow-style occupancy chip | far-future |

## Verbatim owner-pasted reference

(kept whole so nothing is lost; trimmed only of site nav)

- Queuing buildings via Shift-click multi-placement; repeat per building type.
- Place Town Hall close to the mine (worker travel time = real cost).
- Build in open spots; units can get stuck popping out — set rally toward open side.
- Human peasants can repair-to-accelerate construction (async build-boost idea).
- Buildings under construction take FULL damage (anti-offensive-towering).
- Rally points: on ground, on trees/mine (auto-harvest), on units/heroes (follow),
  on transports/burrows (auto-load); rallied units exit the building on the side
  facing the rally. Dead-hero rally edge cases: unit produced while hero alive
  follows the corpse spot; produced while dead idles at the barracks; reincarnation
  cross counts as alive.
- Group-select same-type buildings to mass-train + mass-set rally.
- Occupied indicators on container buildings (burrows, entangled mines).
- Use building formations as walls; restrict town access.
- Offensive/forward barracks near the enemy; hide key tech buildings off-town.
- Control towers by group-select + focus the hero.
- Avoid hero-trap towns: leave room around the hall + multiple exits.
- Repair: 35% of original cost, 150% of original time (1 HP -> full); upgraded
  buildings repair off summed base+upgrade cost.
- Refunds on cancel: construction 75%, research 100%, structure upgrades 75%,
  units 100%, hero revival 100%.
