# WORK ORDER 29 — NPC "Pill" Placeholder in Healer's Cottage

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-26
**Author:** Bug triage — playtest screenshot
**Priority:** Medium — NPC visible but body is a white capsule

---

## Problem

Inside the Healer's Cottage dungeon, a **Villager/Guard NPC** (with the dialogue
"If you're headed to the gates, give them a knock for luck from me.") renders as
a **white capsule "pill"** — the raw `CreatePrimitive(PrimitiveType.Capsule)` shape
with no tint or texture applied.

The speech bubble and proximity dialogue system work correctly. Only the visual
body is wrong.

Screenshot evidence: a white pill shape near the table in the Healer's Cottage,
alongside the NPC's pink aura shadow disc.

---

## Root Cause

The Healer's Cottage dungeon is built by `DungeonSceneBuilder.BuildHealersCottage()`.
The ambient NPCs (Guard/Villager archetype) placed inside the cottage use
`AmbientNPC` (added by reflection). These NPCs get a `CreatePrimitive(Capsule)`
body as a visual stand-in but **no `ApplyTint` call and no `HeroBodySwapper`**
equivalent to replace the capsule at runtime.

Compare with how the hero is handled in `DungeonSceneBuilder`:
```csharp
var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
body.name = "HeroBody";
StripColliders(body);
ApplyTint(body, HexColor("e8d8b8"));   // warm skin tone
// + HeroBodySwapper replaces it at runtime
```

And in `FolksGranaryBuilder`, the `GranaryKeeper` NPC gets:
```csharp
var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
body.name = "GranaryKeeperBody";
StripColliders(body);
ApplyTint(body, HexColor("6b7d8a"));   // slate-blue tint
```

The Healer's Cottage ambient NPCs skip the `ApplyTint` step entirely, leaving the
capsule as the Unity default white/grey.

---

## Fix

### Option A — Tint the NPC capsule (quick fix)

In `DungeonSceneBuilder`, locate where the ambient NPC body capsule is built for the
Healer's Cottage and add an `ApplyTint` call with an appropriate neutral tone:

```csharp
// Villager / Guard archetype — warm terracotta tint (TownsfolkDialogue archetype 2)
ApplyTint(body, HexColor("8a6b5a"));
body.name = "GuardBody";
```

Use different tints per archetype to distinguish NPCs visually:
- Guard (archetype 2): `"8a6b5a"` — earthy brown
- Villager (archetype 1): `"c2a882"` — warm tan
- Elder (archetype 4): `"a09890"` — grey-white

### Option B — HeroBodySwapper for NPCs (preferred, Week 7)

Apply the same `HeroBodySwapper` mechanism used for the hero: the NPC capsule
`body.name = "GuardBody"` (or any archetype name) is replaced at runtime by a
KayKit villager/guard FBX mesh. This matches the WO-35 approach used for the
hero body.

For now, **implement Option A** to unblock the placeholder; note Option B for
Week 7 NPC polish.

---

## Files to Edit

- `Assets/Editor/DungeonSceneBuilder.cs`
  - Locate ambient NPC body capsule creation for Healer's Cottage interior
  - Add `ApplyTint(body, HexColor("8a6b5a"))` and set `body.name = "GuardBody"`

---

## Acceptance Criteria

- [ ] Enter Healer's Cottage dungeon
- [ ] The Villager/Guard NPC visible body is a tinted capsule (warm brown), not white
- [ ] NPC speech bubble still triggers on approach and reads correctly
- [ ] Requires re-running `DungeonSceneBuilder` (Defenders > Dungeons > Build Healer's Cottage) after code change — **owner-gated re-bake**
