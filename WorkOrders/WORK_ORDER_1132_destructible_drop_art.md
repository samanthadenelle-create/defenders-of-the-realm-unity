**Status:** IMPLEMENTED 2026-08-21 - crate became a chest (Hostile/IDamageable removed after auditing every caller); drop identity is 100% silhouette, 16 distinct across 24 live ids, hue demoted to a luma-separated kind cue. Owner felt-verify owed.

# WORK ORDER 1132 — The chest: destructibles become OPENABLE, not attackable

**Minted:** 2026-08-21 (CLI, banner bumped 1132 -> 1133 in the SAME edit)
**Lane:** Dungeons / Combat-adjacent. **Class:** DESIGN CHANGE + art.
**Supersedes the fix half of WO-1047.** Narrowed from WO-1009 (which stays CLOSED).

## OWNER RULING 2026-08-21 (verbal, this session)

> "can we make it a chest?" / "open chest" / "not attackable item"
> "can only open outside of combat"
> "prevents player from trying to run in collect and go"

## Why this is the right fix, not just a nicer one

Today a container is a **static hostile**: `BreakableContainer` declares
`Faction => CombatFaction.Hostile` (`:66`) and sits on the Enemy layer so the hero's
melee OverlapSphere can damage it. That is deliberate - it is how smashing works. But it
also makes every crate a valid TARGET for the hostile reticle, which is the entire
defect logged as **WO-1047** ("a dungeon prop is registering as a HOSTILE target").

Two concerns were sharing one flag: *may the hero damage this?* and *is this a thing to
lock onto?* This ruling removes the first concern entirely, so the second stops being
ambiguous. **The bug does not get filtered out - it stops existing.** That is why this
supersedes 1047's fix rather than competing with it.

The out-of-combat gate is a real design rule, in the owner's words: it "prevents player
from trying to run in collect and go". Loot becomes a reward for CLEARING a room, not a
sprint past live enemies.

## Deliverables

1. **A chest that OPENS.** The verb is *Open*, not *Attack*. Reuse the existing dungeon
   interactable pattern (the "Leave Dungeon" pad and `DungeonExitInteractable` are the
   shape to follow - a prompt the hero walks up to, not a combat action).
2. **Not attackable, at all.** Remove `IDamageable` / `IDamageableStructure` and the
   `CombatFaction.Hostile` declaration from the container, and take it off the Enemy
   layer. ⛔ Verify every caller first: those interfaces are the hero-melee and
   contact-damage seams, and something else may reach the type through them. Removing an
   interface is not a rename - find the callers before you cut.
3. **Out-of-combat gate.** Openable only when no combat is active. Find the EXISTING
   combat-state authority and use it - do not invent a second one. `PanelManager` /
   `PanelRouter` already refuse gameplay panels during battle (`battleAllowed` is the
   data exception), and the HUD has a hostile(activebattle) state; one of those is the
   signal. If enemies are live, the prompt must say so in WORDS ("Not while enemies are
   near"), never just fail to respond - a dead tap reads as a bug.
4. **Real chest art**, closed and open states, replacing
   `GameObject.CreatePrimitive(PrimitiveType.Cube)` at `BreakableContainer.cs:137`.
5. **The drop keeps working.** On open it still rolls its table and spawns the pickup.
   `IngredientPickup` currently spawns a primitive SPHERE (`:137`, scale 0.4) - give it
   real art too. ⛔ KEEP two things exactly as they are: the collider is deliberately
   destroyed (pickup is a distance check so the mote cannot block the hero or the
   NavMesh), and the material is built URP-safe on purpose (the "pink floor" lesson).

## The tint question - answer it before authoring art

`IngredientPickup.CreateRuntime` already takes a `tint` and writes `_BaseColor`/`_Color`,
but the owner saw a WHITE pellet. Either the tint resolved to white for that ingredient
or the property did not take. Right now tint is the ONLY thing telling one ingredient
from another, so all-white motes make moonbloom and shadowcap identical.
⚠ Colour alone is NOT an acceptable answer regardless - the owner is red/green
colourblind. Identity must be carried by shape, glyph or label.

## Migration care

Existing composed dungeons already place these containers. Changing the component's
shape must not orphan authored placements or require a re-bake of every scene - and if
it does require one, SAY SO rather than silently invalidating baked content (`WO-1043`
is the re-bake ticket and is already unblocked).

## Pin it

A regression asserting: no chest/container declares `CombatFaction.Hostile`; the chest is
not on the Enemy layer; opening is refused while combat is active AND the refusal has a
canon sentence; the drop still spawns on open. WO-1047's `[hostile-admit]` instrumentation
STAYS (never strip instrumentation) - after this ships it should record no non-Enemy
admissions at all, which is the proof the class is gone.

## Related

- **WO-1047**: its identification work is done and its fix is superseded here. Close it
  against this ticket once the chest ships and a dungeon run shows a clean admit log.
- The five orphan ingredients (`ing_moonbloom`, `ing_shadowcap`, `ing_quickfoot`,
  `ing_spring_water`, `ing_cloth_scrap`) have NO rows in `loot-tables.json` - they drop
  from nothing. Separate data gap; art does not surface them.
