# Economy Foundation Code — Ready for CLI Integration

**Date:** 2026-06-01  
**Status:** Code written, ready for CLI review + push  
**Work Order:** WO-228 (Resource Nodes & Pet Harvesting)

---

## Code Written (in Assets/_Modules/Economy/)

### Core Classes

1. **ResourceNode.cs** (abstract base)
   - Defines resource type, harvest amounts, depletion mechanics
   - Handles respawn timers (2–4 hours real-time)
   - Tracks harvest count → depletion threshold
   - Abstract method `UpdateVisuals()` for subclasses

2. **IronOreNode.cs**
   - Harvest: 5 Iron per tick
   - Tick rate: 5 seconds
   - Depletion: 5 harvests → depleted
   - Visuals: Gray particles + light glow

3. **LumberNode.cs**
   - Harvest: 4 Lumber per tick
   - Tick rate: 6 seconds
   - Depletion: 5 harvests → depleted
   - Visuals: Brown/golden particles + light

4. **MagicNode.cs**
   - Harvest: 3 Magic per tick
   - Tick rate: 8 seconds
   - Depletion: 5 harvests → depleted
   - Visuals: Purple/blue particles + arcane glow

5. **GemNode.cs**
   - Harvest: 2 Gems per tick
   - Tick rate: 10 seconds
   - Depletion: 5 harvests → depleted
   - Visuals: Cyan/diamond particles + bright glow

6. **PetHarvester.cs**
   - Attached to pet (or pet prefab)
   - Finds nearby nodes (50m radius, every 1 second)
   - Auto-selects nearest non-depleted node
   - Calls `TryHarvest()` on current node
   - Deposits resources into inventory automatically
   - Can be manually assigned to specific node via `SetTargetNode()`

7. **ResourceInventory.cs**
   - Tracks 4 resource types (Iron, Lumber, Magic, Gems)
   - Configurable capacity per type (default 999)
   - `AddResource()` — pet deposits harvests here
   - `SpendResource()` — buildings consume resources
   - `OnResourceChanged` event for UI updates

---

## Integration Checklist

- [ ] Folders exist: `Assets/_Modules/Economy/`
- [ ] All 7 .cs files copied to project
- [ ] Add `DeNelle.Economy` namespace to assembly definition (if exists)
- [ ] Create node prefabs (IronOreNode, LumberNode, MagicNode, GemNode)
   - Assign mesh models (polyperfect ore/tree/crystal/gem models)
   - Add particle systems (glow effect)
   - Add lights (glow light)
   - Add sprite renderer for icon
- [ ] Attach `ResourceNode` subclass to each prefab
- [ ] Spawn 3–5 nodes of each type in village scene (via VillageSceneBuilder)
- [ ] Add `ResourceInventory` component to pet prefab
- [ ] Add `PetHarvester` component to pet prefab
- [ ] Wire `PetHarvester.inventory` → pet's `ResourceInventory`
- [ ] Test: Pet finds nodes → harvests → resources accumulate in inventory
- [ ] Create HUD display for resources (Iron, Lumber, Magic, Gems counts)
- [ ] Wire inventory `OnResourceChanged` event to HUD

---

## Next Steps (For CLI)

1. **Review code** for any corrections/adjustments
2. **Add to project** (copy to Assets/_Modules/Economy/)
3. **Create prefabs** (assign models, particles, lights)
4. **Spawn nodes** in village (3–5 of each type)
5. **Wire pet** (attach inventory + harvester components)
6. **Build & test** (run game, verify nodes spawn, pet harvests)
7. **Commit:** "WO-228: add resource nodes and pet harvesting system"

---

## Code Quality Checklist

- [x] Namespace: DeNelle.Economy (isolated module)
- [x] Uses DeNelle.Core interfaces (if needed)
- [x] No System.Reflection usage
- [x] Null checks in critical paths
- [x] Events for UI integration (OnResourceChanged)
- [x] Serializable fields for tuning
- [x] Comments on public methods

---

## Remaining Work (Design/Content)

- **Visuals:** Particle prefabs, model assignments (polyperfect assets)
- **VFX:** Harvest feedback particles, floating "+5 Iron" damage numbers
- **Tutorial:** Wire companion dialogue to node discovery (WO-227)
- **Save/Load:** Serialize node depletion state + respawn timers (save system integration)
- **HUD:** Display resource counts in UI

---

**Ready to push to CLI.** Code is foundation; remaining work is integration + content.
