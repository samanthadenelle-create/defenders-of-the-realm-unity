# WORK ORDER 956 — An enemy reads GREEN: hostility must never sit on the red/green axis

**Status:** READY TO IMPLEMENT (RCA-first: pin WHICH green source, then fix at that seam)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 956 → 957 in the same edit)
**Silo:** Village/Enemies visuals + VFX tinting — coordinate with WO-954 (models) and WO-955 (pool)
**Origin:** owner F8 seq 2269 2026-08-10 (*"see here and the one is green"*), clarified same session:
*"it was a enemy that was showing as green."* Owner is red/green colourblind — an enemy wrapped in
green reads as friendly/safe. Standing law: never meaning by colour alone; hostile things must not
wear the "safe" hue.
**Predecessor:** the 08-06 session's open item #5 — *"Cast_Heal is a green glow — a second
colourblind pass"* — was never ticketed. This WO is that ticket, widened to enemy-side green cues.

## 1. RCA first (§12 — one instrumented look before any edit)

Candidate sources, in likelihood order (pin with a capture/screenshot; the flagged frame from seq
2269 was near the arena — check the break PNGs around 11:11:54, and reproduce vs the dungeon healer):
1. **The heal cast glow (`Cast_Heal`) on an ENEMY healer** — `hollow-acolyte` (model Skeleton_Healer)
   heals its allies; if the player-side heal VFX is reused untinted, the enemy glows green.
2. The `EnemyCaster`/aura VFX family tint.
3. A material/placeholder fallback tint on the enemy body.

## 2. The rule to implement (once pinned)

- Enemy-side effects NEVER present on the green axis: tint the enemy heal/support effects to the
  hostile palette (e.g. sickly violet/amber — final hue = owner look pass) OR carry a shape cue
  (jagged ring vs soft bloom). Faction drives presentation — One Model: the effect reads the
  caster's faction, never a hardcoded per-effect colour (check how EnemyVfxSet / the VFX catalog
  tint path works before adding a mechanism).
- Word+shape survives on any HUD echo of the same event.
- Keep the PLAYER-side heal green if the owner likes it — the defect is hostile-green, not green.

## 3. What NOT to touch

VFXType ordinals · the WO-955 pool guard · enemy stats/AI · the accessibility low-HP recipe.
