<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-228: Resource Nodes & Gathering Tutorial — Ore, Lumber, Magic, Gems

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (core economy loop, ties to camps + tutorial)  
**Owner:** CLI  
**Depends On:** WO-222/227 (tutorial/companion guide), WO-216 (camps nearby)  
**Blocks:** None (but enables resource economy)

---

## Vision

Pet acts as **auto-harvesting peon** (Warcraft-style). Hero discovers resource nodes; pet automatically gathers them passively:

1. Hero + companion explore nearby area
2. Companion points out first **resource node** (e.g., Iron ore)
3. **Pet auto-harvests nearby nodes** (passive income)
4. Tutorial explains: "Your pet gathers resources while you defend the village"
5. Guide points to 3 more node types around map
6. Pet brings harvested resources to player/base (visual caravan)

**Economy loop:** Pet harvests → resources accumulate → player spends on buildings/upgrades → village grows stronger

---

## Resource Types

### 1. Iron Ore Nodes
- **Model:** `Rock_Large` + `Rock_Pillar` stacked (Medieval_M)
- **Material:** Gray/brown with metallic ore vein texture overlay
- **Visual:** Gray/black rocky outcrop with glowing vein particles
- **Icon/Notation:** ⛏️ ore symbol (visible from ~30m away)
- **Harvest:** Click → mining animation → +20 Iron
- **Refines to:** Stronger building materials via Forge

### 2. Lumber/Wood Nodes
- **Model:** `Tree_Oak` or `Tree_Conifer` (Medieval_M) — standing tree
- **Material:** Natural wood colors
- **Visual:** Tree with glowing wood particles (saw dust effect)
- **Icon/Notation:** 🪵 tree symbol (visible from ~30m away)
- **Harvest:** Click → chopping animation → +15 Lumber
- **Refines to:** Wooden structure upgrades via Lumbermill

### 3. Magic/Arcane Nodes
- **Model:** `Potion_Globe` (Fantasy_M) — tall translucent sphere base
- **Material:** Glowing purple/blue with arcane runes
- **Visual:** Floating crystal aura + swirling energy particles
- **Icon/Notation:** ✨ arcane symbol (visible from ~30m away)
- **Harvest:** Click → channeling animation → +10 Magic
- **Refines to:** Enchantments via Arcane Tower

### 4. Gem/Crystal Nodes
- **Model:** `Candlestick` (Fantasy_M) — tall crystalline geometry
- **Material:** Cyan/diamond shine with faceted surface
- **Visual:** Shimmering crystal formation + light refraction particles
- **Icon/Notation:** 💎 gem symbol (visible from ~30m away)
- **Harvest:** Click → extraction animation → +8 Gems
- **Refines to:** High-value crafting via Jeweler

---

## Infield Notation System

**Visual indicators (visible from distance):**

Each node type has:
1. **3D Model** (ore pile, tree, crystal, gem outcrop)
2. **Particle effect** (glow, shimmer, pulsing light)
3. **UI Icon** (floats above node, shows type)
4. **Quest marker** (yellow/gold ring when tutorial points to it)
5. **Minimap blip** (color-coded: red=ore, brown=wood, purple=magic, cyan=gem)

**Tutorial flow:**
```
Companion: "We need supplies. Look for those glowing rocks over there."
→ Quest marker appears on nearest Iron node
→ Minimap highlights it
→ Player walks over, clicks
→ +20 Iron harvested
→ Companion: "Good. Let's find the others."
→ Marks Wood, Magic, Gem nodes
```

---

## Implementation

### Phase 1: Node System
1. Create `ResourceNode.cs` — base class
   - Type (Iron, Wood, Magic, Gem)
   - Harvest rate (how much per tick, e.g., +1 Iron every 5s)
   - Depletion state (nodes deplete, respawn on timer)
   - Visual representation (3D model + particles)
   - Detection radius (pet finds nodes within ~50m)

2. Create subclasses:
   - `IronOreNode.cs` (5 Iron per harvest, 5s tick)
   - `LumberNode.cs` (4 Wood per harvest, 6s tick)
   - `MagicNode.cs` (3 Magic per harvest, 8s tick)
   - `GemNode.cs` (2 Gems per harvest, 10s tick)

### Phase 2: Pet Auto-Harvest System
3. Create `PetHarvester.cs` — pet peon logic
   - **FindNearbyNodes():** Detect all resource nodes within 50m radius
   - **SelectTarget():** Pick nearest unharvested node
   - **Harvest():** Autonomously gather from node (tick-based, passive)
   - **Carry():** Transport resources back to hero/base (visual)
   - **Deposit():** Add to inventory automatically

4. Pet behavior:
   - Idle when no nodes nearby
   - Seeks nearest node when available
   - Returns to hero when inventory full
   - Repeats indefinitely (true "peon" mode)
   - Optional: Show harvest particle/animation at node

### Phase 3: Infield Notation
5. Node UI layer:
   - Icon above node (floats)
   - Quest marker (yellow ring, pulses) — tutorial only
   - Minimap blip (color-coded)
   - Range check (show icon when <50m away)
   - **Pet indicator:** Visual line/caravan showing resource transport

6. Optional manual harvest:
   - Click node for **immediate boost** (100% harvest vs. gradual)
   - Useful if player needs quick resources in emergency
   - Pet continues auto-harvest if not interrupted

### Phase 4: Placement
7. Spawn 3–5 nodes of each type around village (away from structures, within pet patrol range)
8. Wire to save system (node depletion state persists)
9. Add respawn timer (nodes respawn after 2–4 hours real-time)

### Phase 5: Tutorial Integration
10. Wire to WO-227 companion system
11. Companion dialogue:
   - "Your pet can gather those resources for you. Let's send them over there."
   - Points to nearest Iron node
   - Pet auto-starts harvesting
   - "See? They'll keep gathering while we defend the village."
12. Quest markers guide to first node of each type (pet learns them)

### Phase 6: Inventory System
13. Add resource slots to inventory (Iron, Wood, Magic, Gem)
14. Display in HUD (show counts, accumulating in real-time)
15. Pet deposits harvested resources automatically
16. Enable resource spending (place tower → -50 crystals, etc.)

---

## Acceptance Criteria

- [ ] ResourceNode base class created
- [ ] 4 node types implemented (Ore, Lumber, Magic, Gem)
- [ ] Infield notation working (icon, marker, minimap blip)
- [ ] Click harvests resource correctly
- [ ] Harvest animation + feedback visible
- [ ] 3–5 nodes of each type placed on map
- [ ] Nodes respawn after timer (2–4 hours)
- [ ] Save system tracks depletion state
- [ ] Tutorial companion guides to first node
- [ ] Quest markers work (yellow ring, visible from distance)
- [ ] Inventory system tracks resources
- [ ] WebGL tested: nodes spawn, harvest works, inventory updates
- [ ] Commit: "WO-228: add resource nodes and gathering tutorial (ore, lumber, magic, gems)"

---

## Design Notes

### Multi-Pet Harvesting (3 Pets)
- **Each pet is independent harvester** — assign up to 3 pets to different nodes
- **3 pets = 3x faster resource gathering** (each pet harvests autonomously)
- **Pet 1:** Harvests Iron while you defend
- **Pet 2:** Harvests Wood while you defend
- **Pet 3:** Harvests Magic/Gems while you defend
- Early players get resources faster with 3 pets working in parallel
- Encourages pet collection as core economy feature

### Respawn Timer
- **Real-time:** 2–4 hours (encourages daily play, prevents grinding)
- **Alternative:** Manual respawn after combat (reset when enemies clear nearby camps)

### Depletion
- Each node gives 3–5 harvests before depleting
- **Soft cap:** Players can't out-gather building rate
- Respawn prevents permanent depletion

### Icon Clarity
- Each type must be instantly recognizable (ore ≠ wood ≠ magic ≠ gem)
- Color-coding on minimap reinforces type
- Quest marker (yellow ring) differentiates tutorial nodes from passive nodes

### Harvest Rate Tuning
- **Early game:** Pets harvest fast initially (quick wins)
- **Late game:** Slower rate to maintain challenge
- **Player choice:** Manual harvest gives instant boost vs. waiting for pet

---

## Integration Checkpoints

- [ ] Companion points to nodes in WO-227 dialogue
- [ ] Inventory persists across scene loads
- [ ] Resources can be spent (building costs, refinements)
- [ ] Save system stores node state + respawn timers

---

**Estimate:** 2.5–3.5 hours (node system, placement, UI, inventory, tutorial wiring, testing)
