# Defensive Catalog v0 — the first testable content

> First concrete catalog to validate the whole engine idea in play. Core principle (owner's
> archer-ground / wizard-wall instinct): **placement determines role.** *Where* you put a tower is
> *what it does* — which makes layout the skill. Each entry is a `CatalogEntry` (visual + repo).

## The starter set (4 entries — small enough to test, deep enough to be fun)

| Tower | Placement (rule) | Range | Targets | Dmg / Rate | Role | Cost |
|---|---|---|---|---|---|---|
| **Archer Tower** | **Ground** (courtyard / wall base) | short–med | single, ground | low / **fast** | anti-infantry · cheap volume · breach response | low |
| **Wizard Tower** | **Wall-walk** (elevated) | **long** | **AoE**, any | med / slow | thin the horde *on the approach*, before it reaches the wall | high |
| **Cannon / Ballista** | **Ground** | med | single, **heavy/armored** | **high** / slow | anti-tank · anti-boss | med |
| **Frost Spire** | **Wall-walk** | med | AoE **slow** + ice | low / med | crowd *slow* (force-multiplier) + elemental synergy | high |

## Placement + spacing (the `PlacementRules` data — no grid, "naturally works here")
- **`must-sit-on`** — Archer/Cannon → **Ground**; Wizard/Frost → **Wall-walk** (the rampart surface we just built).
- **Footprint** ~3 m (one piece); **min-distance** between towers → can't stack → **forces spread for coverage.**
- **Elevation bonus** — placing on the wall-walk grants **+range / line-of-sight over the wall.** That's *why*
  wizards go up top — and it reuses the climbable rampart we already shipped. Height = reach.

## The strategy that emerges (this is the fun)
- **Wall line** (wizards / frost, elevated) = the **FAR layer** — thin the wave on the approach.
- **Ground line** (archers / cannons) = the **CLOSE layer** — finish breaches + handle infantry & heavies.
- **Lanes** — concentrate fire at the **gates** (where the approach funnels). **Range circles must overlap** → no gaps.
- So defense = a **layered, spaced arrangement the player designs.** *Where + spacing = the skill*, exactly like CoC.

## Elemental tie (proves the matrix, concretely)
**Frost Spire** in a snow biome = cheap synergy; in a **fire** biome = costly **counter** but *devastates* fire enemies.
Same tower, value shifts by biome — the affinity matrix in action.

## What this tests
- **Catalog** — defenses as defs. · **Placement conditions** — ground vs wall-walk. · **Mechanics** —
  range/target/dmg/rate as data. · **Layered-defense strategy** — the emergent skill.

## Reuse (mostly exists)
- **Towers exist** — `Tower_Castle_Round/Square`, `Tower_Medieval_*` (polyperfect).
- **Wall-walk exists** — the rampart surface (elevated placement target) we just built + baked.
- **Tower building + Defend-the-Tower shoot logic** likely reusable for targeting/projectiles.
