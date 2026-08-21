**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-229: Resource Harvest Visual Feedback & HUD Display

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (makes harvest loop satisfying, player awareness)  
**Owner:** CLI  
**Depends On:** WO-228 (resource nodes + pet harvesting core system)  
**Blocks:** None (complements WO-228)

---

## Vision

Make resource harvesting **viscerally satisfying** with visual feedback at three layers:
1. **Node-level close-up** (the delicious harvest dopamine)
2. **HUD strategic overview** (always-visible resource awareness)
3. **Optional toast notifications** (big milestone moments)

---

## Layer 1: Node-Level Harvest Feedback

### Floating Resource Text
When pet harvests from a node:
- **Text:** "+5 Iron" (format: "+{amount} {resource_type}")
- **Color-coded by resource type:**
  - Iron → metallic gray text
  - Lumber → earthy brown text
  - Magic → glowing purple/arcane font
  - Gems → sparkling cyan text
- **Animation:** Pop above node, float upward, fade out over 1–1.5s
- **Position:** At node center, rises ~1.5 units

### Particle Burst (Synchronized with text)
**Matching the resource harvested:**
- **Iron:** Metallic spark shower (orange/white particles, falls down)
- **Lumber:** Wood chips burst (brown splinters, float outward)
- **Magic:** Arcane swirl (purple/blue energy vortex, rises up)
- **Gems:** Diamond shimmer (cyan sparkles, rotate + fall)

**Timing:** Fire at exact moment of harvest, last 0.5–1s

### Pet Harvest Animation (Visual confirmation)
**Pet plays quick harvesting action:**
- **Iron:** Pickaxe strike / dig animation
- **Lumber:** Axe chop / saw animation
- **Magic:** Channeling / siphon animation (hands glowing)
- **Gems:** Extraction / pluck animation

**Duration:** 0.3–0.5s (snappy, not slow)

### Node Depletion Visual (Temporary feedback)
When node is harvested:
- **Scale:** Temporarily shrink node by 10% over 0.2s
- **Color:** Dim slightly (reduce brightness by 20%) for 0.3s, then restore
- **Effect:** Shows the node is "exhausted" until recovery

---

## Layer 2: HUD Resource Display (Top-Right Corner)

### Layout (2×2 grid)
```
┌─────────────────────┐
│ [Iron⚔]  245       │
│ [Lumber🪵] 312     │
│ [Magic✨]  87      │
│ [Gems💎]   64      │
└─────────────────────┐
```

### Implementation Details
- **Position:** Top-right corner, 20px margin from edges
- **Background:** Semi-transparent dark panel (40% opacity)
- **Font:** Clear, readable (Arial or similar), 18pt
- **Icons:** 32×32 px icon per resource type
  - Use polyperfect symbols (iron ore, plant, crystal, gem)
  - Color-matched to resource type
- **Spacing:** 8px between icon + text, 12px between rows

### Real-Time Updates
- Wire to `ResourceInventory.OnResourceChanged` event
- Number updates instantly when pet harvests
- No delay (immediate feedback)

### Visual Enhancement: Pulse Animation (Optional)
When resources increase:
- Number briefly scales up by 10% (0.2s)
- Returns to normal size (0.2s)
- Creates satisfying "pop" feedback without distraction

### Fly-In Text (Optional but nice)
When HUD updates:
- Small "+5" text appears next to resource icon
- Flies upward + fades over 0.5s
- Same color as resource type
- Reinforces the harvest globally

---

## Layer 3: Toast Notifications (Optional Milestones)

### Trigger: Big Harvest Moments
Show toast when:
- Pet brings back 10+ resources in single harvest cycle
- Pet completes a full depletion-to-respawn cycle
- Resource total hits round number (100, 200, 500)

### Toast Format
```
┌──────────────────────────────┐
│ Luna harvested 12 Iron!      │
│ (fades after 3s)             │
└──────────────────────────────┘
```

- **Position:** Bottom-right corner (doesn't block HUD)
- **Duration:** 3 seconds
- **Animation:** Slide in + fade out (smooth)
- **Stacking:** If multiple toasts, stack vertically with 8px spacing

---

## Implementation Plan

### Phase 1: Floating Text + Particles
1. Create `FloatingResourceText.cs` prefab
   - Spawns at node position
   - Moves up + fades
   - Destroys itself after animation

2. Create particle prefabs (one per resource type)
   - IronSpark.prefab
   - LumberChips.prefab
   - MagicSwirl.prefab
   - GemShimmer.prefab

3. Wire to `PetHarvester.OnResourceHarvested()` event
   - Spawn floating text
   - Spawn particle burst
   - Play pet harvest animation

### Phase 2: HUD Display
4. Create `ResourceHUD.cs` UI component
   - Displays Iron, Lumber, Magic, Gems in 2×2 grid
   - Updates via `ResourceInventory.OnResourceChanged` event
   - Optional: Pulse animation on value change

5. Create resource icons (from polyperfect catalog)
   - Iron symbol (ore/rock icon)
   - Lumber symbol (plant/tree icon)
   - Magic symbol (crystal/arcane icon)
   - Gems symbol (diamond/sparkle icon)

6. Place HUD prefab in scene (top-right corner)
   - Configure anchor (top-right)
   - Set margins (20px from edges)

### Phase 3: Optional Toast Layer
7. Create `HarvestToast.cs` prefab
   - Shows brief notification
   - Fades after 3s
   - Stacks multiple toasts if needed

8. Wire to `ResourceInventory` milestone events
   - Trigger on big harvests
   - Trigger on milestones (100, 200, etc.)

---

## Acceptance Criteria

- [ ] Floating "+X Resource" text pops above node on harvest
- [ ] Text color-coded by resource type (gray/brown/purple/cyan)
- [ ] Text floats upward + fades smoothly
- [ ] Particle burst fires (Iron sparks, Lumber chips, Magic swirl, Gem shimmer)
- [ ] Pet plays harvest animation (pickaxe/axe/channel/pluck)
- [ ] Node visually dims + shrinks temporarily (depletion feedback)
- [ ] HUD displays 2×2 grid (Iron, Lumber, Magic, Gems) in top-right
- [ ] HUD updates in real-time (no delay)
- [ ] HUD has semi-transparent background + readable fonts
- [ ] Optional: Pulse animation on HUD value increase
- [ ] Optional: Fly-in "+X" text next to HUD icon
- [ ] Optional: Toast notifications for big harvests
- [ ] WebGL tested: all feedback visible, no performance hit
- [ ] Commit: "WO-229: add resource harvest visual feedback and HUD display"

---

## Testing Checklist

1. **Node-level feedback:**
   - Harvest from node → floating text appears ✓
   - Text is correct color (gray/brown/purple/cyan) ✓
   - Particles match resource type ✓
   - Pet animation plays ✓
   - Node dims + shrinks temporarily ✓

2. **HUD display:**
   - HUD visible in top-right corner ✓
   - Shows correct resource amounts ✓
   - Updates when pet harvests ✓
   - Icons are clear + recognizable ✓
   - Font is readable ✓

3. **Optional layers:**
   - Pulse animation smooth (if enabled) ✓
   - Toast appears for big harvests (if enabled) ✓
   - Toast fades after 3s ✓

4. **Performance:**
   - No frame rate drops with floating text + particles ✓
   - Multiple harvests simultaneously don't stutter ✓

---

## Design Notes

### Why This Combo Works
- **Node-level feedback:** Satisfying close-up dopamine (like loot drops in Diablo)
- **HUD counter:** Strategic awareness (player always knows resource status)
- **Particles:** Visual clarity (Iron ≠ Lumber ≠ Magic ≠ Gems at a glance)
- **Pet animation:** Character agency (feels like the pet is actually working)
- **Toast (optional):** Celebration moments (big harvests feel special)

### Color Consistency
- Iron: Gray (#888888)
- Lumber: Brown (#8B6914)
- Magic: Purple (#9D4EDD)
- Gems: Cyan (#00F5FF)

Use these same colors everywhere (particles, text, HUD, icons).

### Performance Tip
- Reuse particle prefabs (don't instantiate new ones per harvest)
- Pool floating text objects (create 10–20 at startup)
- Update HUD text only when value changes (not every frame)

---

**Estimate:** 2–3 hours (particles, floating text, HUD layout, animations, testing)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
