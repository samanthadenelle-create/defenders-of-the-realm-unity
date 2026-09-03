# WORK ORDER 1327 - The fireball bounces off every layer with zero energy loss, so it returns to the caster and never dies

**Status:** READY TO IMPLEMENT
**Silo / Lane:** VFX / combat feel
**Type:** EXISTING (shipped, misconfigured)
**Minted:** 2026-09-02 (CLI) from a WO-1305 side-finding, corroborated by TWO owner captures.
**Severity:** P2 - a core spell behaves wrongly every cast, and the owner has reported it twice.

## The owner reported this twice, in her own words

- seq 4644 (2026-09-02 06:58): *"the fire spell is wrong. casts at me and stays at me. CAne we look..."*
- seq 4152 (2026-08-31): *"fireball when i cast spins at me after casting, staff is almost cor..."*

Both were read as animation/orientation complaints at the time. They are not - or not only.

## The settings, read at source (not inferred)

`Fireballs` particle system, CollisionModule:

| field | value | consequence |
|---|---|---|
| collision type | world | collides with scene geometry |
| `quality` | `0` (High) | per-collider accuracy, so it really does hit buildings |
| `collidesWith` | **all 32 layers** | nothing is excluded - not the player, not town props |
| `bounce` | **1.0** | PERFECTLY ELASTIC. No energy is lost on impact. |
| `dampen` | **0** | and none is lost to damping either |
| `minKillSpeed` | **0** | the particle is NEVER killed by a collision |

Those five together are not a near-miss; they are the exact recipe for a projectile that ricochets
around the town forever and comes back at the person who cast it. Inside walls - which is where the
owner plays - a bounce-1.0 particle is in a box.

⚠ **This is a mechanical reading of captured settings, not a proven root.** It is consistent with both
owner reports and no other candidate has been examined. Confirm with a play capture inside the walls
before declaring it closed (CLAUDE.md sec.12).

## Also found in the same prefab - a MOBILE PERF defect

The child `Point Light`'s `Light` component reads `m_Enabled: 0`, which looks like an off switch and
is not: it is a **PROTOTYPE**. Two systems drive it through `LightsModule` with `ratio: 1` -
`Fireballs` (`maxLights: 20`) and the sub-emitter `Explosion ` (`maxLights: 5`).

**That is roughly 25 concurrent real-time point lights per cast** (intensity 5, range 5, shadows off).
On the Seeker and on any phone, that is a frame-rate event on every fireball.

⛔ The cheap dial is `maxLights` and `ratio`. **Do NOT delete the child** - it is the prototype the
modules instantiate from, and removing it breaks the effect rather than tuning it.

## The fix

- Correct the collision so a fireball dies on impact instead of ricocheting: the levers are
  `bounce`, `dampen`, `minKillSpeed`, and `collidesWith` (a fireball almost certainly should not
  collide with the player's own layer at all).
- Bring the concurrent light count down to something a phone can carry.
- ⛔ **Both of these are FEEL and PRESENTATION values, so per the 2026-09-02 standing rule in
  `KEY_FACTS.md` they should be TUNABLE where the rail can reach them** (`docs/PROD022_TUNABLE_FLAGS.md`).
  Particle-module values baked in a prefab are NOT reachable by the tunables rail - say so plainly if
  that is the case rather than pretending otherwise, and put whatever IS code-side on the rail.
- The owner is red/green colourblind and owns every creative VFX call: change BEHAVIOUR (does it
  bounce, how many lights) freely; do not restyle the effect, recolour it, or swap the prefab.

## Acceptance

- [ ] A play capture inside the walls shows the fireball terminating on impact rather than returning.
- [ ] The concurrent light count per cast is stated as a NUMBER, before and after.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] ⛔ Owner felt-verifies and CLOSES - this is a feel defect and no headless gate can see it.

## What NOT to touch

- Do not restyle, recolour or replace the fireball VFX. The owner picks all VFX.
- Do not delete the `Point Light` child (it is the prototype).
- Do not widen into WO-1305 part B (the Synty duplicate addresses), which is separately fenced.
