<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 244 — Node Visibility & Discovery

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 244
**Date:** 2026-06-02
**Triggered by:** Owner playtest — "I was playing and never saw a single node."

---

## The problem

Nodes exist in the world but have zero visual presence. The player walks past them without knowing they're there. The entire Kill → Claim → Build loop is invisible.

A node must be discoverable from a distance, readable at a glance, and unmistakably meaningful when you find it.

---

## Three layers of visibility

```
Layer 1 — WORLD PRESENCE     You can see the camp from far away
Layer 2 — COMPASS / HUD      You know roughly where nodes are even off-screen
Layer 3 — COMPANION HINT     Sylas tells you when you're close to one you haven't found yet
```

---

## Layer 1 — World presence (the camp itself)

### What a node looks like before clearing

Every node camp must have at minimum:
- **A campfire** — existing fire VFX from `VFXManager`. Place one at the camp centre. Visible from ~60m in daylight, ~100m at night.
- **A smoke column** — tall rising smoke particle (use existing `Sfx_Campfire` or create `VFX_CampSmoke`). Height 6–8m. Visible from ~80m. This is the primary long-distance signal.
- **2–3 crude props** — arrange existing KayKit or polyperfect assets: a tent/tarp, a barrel, a crate. These say "someone lives here" instantly.
- **Patrolling enemies** — already handled by `RegionMobSpawner`. Visible enemies are the strongest signal. Make sure their patrol radius keeps them visible near the camp, not hidden in trees.
- **A resource icon floating above the camp** — world-space UI (code-built, no UXML). Small icon + resource type text. Visible from 40m. Fades in on approach.

```
     🔥 smoke rising
     [Iron ⚙]        ← floating world-space label, fades in at 40m
     
  [tent][barrel][crate]
  
  * enemy patrols *
```

### Resource icon — floating world-space label

```csharp
// NodeWorldLabel.cs — attach to ClaimableNode
// Code-built world-space Canvas (renderMode = WorldSpace)
// Text: nodeName + resource icon (e.g. "Iron Camp  ⚙")
// Scale: 0.02f (world units). Billboard (faces camera each frame).
// Fade: invisible beyond 60m. Fades in 40m–25m. Full opacity < 25m.
// State changes:
//   Uncleared → white text, no background
//   Cleared (unclaimed) → gold text, pulsing
//   Claimed → teal text + small flag icon
```

### State visuals — smoke tells the story

| Node state | Smoke | Fire | Props |
|---|---|---|---|
| Enemy camp (uncleared) | Grey/black smoke column | Orange campfire | Enemy tents |
| Cleared (unclaimed) | Smoke dies out, embers only | Embers fading | Props remain |
| Claimed | No smoke | Warm amber hearth | Outpost Hall + workers |

The transition from enemy smoke → dying embers → warm hearth fire is the visual arc of claiming a node. No UI needed — the world tells the story.

---

## Layer 2 — Compass / HUD indicators

### Node compass markers (edge-of-screen arrows)

When a node is within 150m but NOT in the camera frustum, show a small directional arrow on the screen edge pointing toward it.

```csharp
// NodeCompassMarker.cs — one per ClaimableNode, managed by NodeCompassController
// Screen-edge arrow: 24×24px, pointing toward the node's world position
// Colour by state:
//   Uncleared (enemy camp)  → red/orange  ◄
//   Cleared (unclaimed)     → gold        ◄ (pulsing)
//   Claimed (owned)         → teal        ◄
// Shows: icon + distance label ("Iron 48m")
// Max 3 shown at once — prioritise nearest unclaimed nodes
// Range: 150m. Beyond 150m = no marker (too far to matter right now)
```

### Minimap dots (if minimap exists)

If/when a minimap is added:
- Enemy camp = red dot
- Cleared node = gold dot
- Owned outpost = teal dot

### HUD "nearest node" strip (fallback if no minimap)

Small strip at top-right: "Nearest node: Iron Camp · 72m ▶"
Updates every 3 seconds. Only shows when player is in OuterWorld and has no claimed nodes yet (tutorial assist — fades out once player claims their first node).

---

## Layer 3 — Sylas (companion discovery hints)

Sylas has been out on the Outer Paths for years. He knows where the camps are. Use him.

### First world trip — explicit guidance

When the player exits the village gate for the first time, Sylas says:

> *"There's a camp to the [direction]. You can see the smoke from here if you know what to look for. Clear it — it's a resource node. Iron, if I'm reading the terrain right."*

He then walks toward it (follows the player but nudges the direction via pathfinding). This is the tutorial for node discovery — Sylas makes the smoke meaningful.

### Proximity hints (within 60m, node undiscovered)

If the player is within 60m of an uncleared node they haven't visited before, Sylas says one of:

> *"Camp ahead. Hollow Ones, by the look of the smoke."*

> *"You smell that fire? That's not ours."*

> *"There's a camp in that direction — I can see their patrol from here."*

> *"That smoke's been there a while. Settled camp, not a scouting party."*

Fires once per undiscovered node. Never repeats for the same node.

### Post-clear prompt

After killing the last enemy at a camp:

> *"It's clear. Plant your flag before something else moves in."*

This is the moment that teaches "Press E / tap to claim."

---

## Implementation — what CLI needs to build

### New files

```
Assets/_Modules/Village/World/NodeWorldLabel.cs     World-space floating label above each node
Assets/_Modules/Village/World/NodeCompassController.cs  Screen-edge arrows for off-screen nodes
Assets/_Modules/Village/World/NodeCompassMarker.cs  Per-node arrow instance
Assets/_Modules/Village/Companions/SylasNodeHints.cs   Proximity hint + first-trip guidance
```

### VFX / props

- Place `VFXManager.Play(VFXType.Campfire, campPos)` at each node centre on scene load
- Place `VFX_CampSmoke` particle (create or source from polyperfect) at +0.5m above campfire
- Arrange 2–3 prop prefabs (`KayKit/tent`, `KayKit/barrel`, `KayKit/crate`) around the fire at randomised angles

**Props placement:** in `ClaimableNode.Awake()` — scatter 2–3 props within 4m of node centre at random Y=0, random rotation. Use `Resources.Load<GameObject>` from a `NodePropSet` SO (list of eligible prop prefabs).

### Smoke state transitions

Subscribe to `ClaimableNode.OnCleared` and `OnClaimed`:
- `OnCleared` → stop smoke emitter, swap campfire to embers VFX
- `OnClaimed` → swap embers to small warm hearth fire (friendly, lower, amber)

---

## Acceptance criteria

- [ ] Every node has visible campfire + smoke column from 60m+
- [ ] 2–3 props arranged around each camp
- [ ] Floating world-space label appears on approach (40m), shows resource type
- [ ] Label colour changes on clear (gold) and claim (teal)
- [ ] Screen-edge compass arrows show for nodes within 150m not in view
- [ ] Compass arrow shows distance label and state colour
- [ ] Sylas says a directional hint on first world trip
- [ ] Sylas says proximity hint within 60m of undiscovered node (once per node)
- [ ] Sylas says "plant your flag" after last enemy dies at a camp
- [ ] Smoke fades to embers on clear, warm hearth on claim
- [ ] No UXML / UIDocument

## What NOT to touch
- `Village.unity` — do not hand-edit
- `WaveManager` wave logic
- `RegionMobSpawner` enemy count/aggro logic — only subscribe to `Enemy.Died` for the hint trigger
