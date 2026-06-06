# Economy — Assembly-CSharp (no asmdef)

Harvestable resource nodes + inventory (legacy / parallel exploration).

**LIVE IMPLEMENTATION (WO-106 + prior):** the reconciled, production path is:
- `DeNelle.Village.MineNode` (and CrystalMineNode) as the single source of truth for nodes.
- `DeNelle.Pets.PetHarvester` + `MineNodeBridge` (reflection, no asmdef violation) for autonomous pet farming.
- `DeNelle.Village.EconomyService` (the Economy class) as the choke-point faucet: all pet/outpost/node yields now route through `Grant(ResourceCost)` for in-session Wood/Iron sync + OnChanged + persisted Food/Crystals.
- `ClaimableCamp` / `Outpost` (World/Camps) for the clear→claim→build→defend→passive-trickle + scaling.
- `PetHarvestBootstrap` drops starter nodes in Village/Village2 (flag or -spawnPlaceholderMineNodes to enable placeholders for demo).

The files in this folder (ResourceNode family, old PetHarvester, ResourceInventory) are superseded for gameplay; kept for reference or future SO-driven catalog nodes. Do not wire new features here — edit the live Village + Pets paths and **always** go through `DeNelle.Village.EconomyService` (the Economy class).

**Recommended API for all income (pet harvest, outpost ticks, node claims, etc.):**
- `EconomyService.Instance.AddResource(DeNelle.Core.ResourceType, amount)`
- or `EconomyService.Instance.Grant(...)` / `TrySpend(ResourceCost)`

See `HarvestSite`, `OutpostHub`, `MineNode.ClaimAsHarvestSite`, and updated `PetHarvestBootstrap` for examples.

Design: `docs/RESOURCE_ECONOMY_DESIGN.md` (faucets, Food→Population, hybrid pacing, offline caps) and `docs/PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md` (P4 harvest + outpost claims).

WO-106 completed the "using the Economy class" integration + territory multiplier for difficulty.

> Maintenance: update this README when files are added/removed.
