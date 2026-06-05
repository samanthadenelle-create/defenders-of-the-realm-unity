# Economy — Assembly-CSharp (no asmdef)

Harvestable resource nodes + inventory.

## Files

- `ResourceNode` (base) + `GemNode`, `IronOreNode`, `LumberNode`, `MagicNode`
- `ResourceInventory` — player resource counts
- `PetHarvester` — NOTE: a `PetHarvester.cs` also exists in `Pets/` —
  check which is live before editing

Design: `docs/RESOURCE_ECONOMY_DESIGN.md`. Correction pass: WO-266.

> Maintenance: update this README when files are added/removed.
