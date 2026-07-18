# Default dungeon room prefabs

| Prefab | Archetype | Sockets | Notes |
|--------|-----------|---------|-------|
| `Entrance` | hub | S,N | Dungeon mouth — approach south, continue north |
| `EntryHall` | hub | S,N | Alias-friendly hub (spine sample name) |
| `Straight` | combat | S,N | Corridor cell — north/south |
| `TurnLeft` | combat | S,W | Left bend (enter S, leave W) |
| `TurnRight` | combat | S,E | Right bend (enter S, leave E) |
| `TJunction` | combat | S,E,W | T-junction (S/E/W) |
| `Intersection` | combat | N,E,S,W | 4-way cross |
| `DeadEnd` | lore | S | Cul-de-sac (single south socket) |
| `ChokePoint` | combat | S,N | Narrow pass N/S — ambush / squeeze |
| `CombatChamber` | combat | S,N | 2x2 fight room |
| `LoreShrine` | lore | S | Shrine / lore stone (dead-end lore) |
| `RewardVault` | reward | S | Treasure end room (accent floor tint) |
| `SecretAlcove` | secret | S | Secret alcove — socket flagged isSecret |
| `StairDown` | hub | S+StairDown | Horizontal entry + stair down socket |
| `StairUp` | hub | S+StairUp | Horizontal entry + stair up socket |
| `SideBranch` | combat | W,E | East-west spur corridor |
| `BossKeep` | boss | S | Boss arena (enter south only, accent floor) |

Rebuild: `Defenders/Dungeon/Build Default Room Prefabs`
