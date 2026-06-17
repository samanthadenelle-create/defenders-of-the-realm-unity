# Pet Organic Movement — Research & Enhancement Plan (2026-06-16)

Distilled from a deep-research pass (101 agents, adversarially verified). Sources: Craig Reynolds
steering (red3d.com — primary), Nature of Code, libgdx gdx-ai, Game AI Pro 3 (lightweight FSM, shipped
in *Drawn to Life*), Unity `Mathf.PerlinNoise` docs.

## What we already have (good base — `PetHeroLeash.cs`)
The leash is already a correct **Reynolds Wander**: a retained heading (`_headingDeg`) bent by a bounded
per-tick jitter (`WanderTurnDegPerSec` 70°/s) — i.e. **temporal coherence** (the thing that reads as
"alive" vs twitchy) — with a **carrot projected ahead** (`LeadDistance` 3.5m), inner-ring (4.5m), soft
**ExploreRadius** (9m) steer-home, hard **ReturnRadius** (13m) beeline, per-pet RNG seed, and stop-and-
sniff beats. So everything below is **additive polish, not a rewrite.**

## Additive enhancements (research-backed)
1. **Perlin-noise heading drift** (Reynolds explicitly endorses coherent noise over pure Random for the
   wander heading — smoother, more animal-like). Replace the uniform-random `_turnIntentDeg` with a
   `Mathf.PerlinNoise`-evolved drift. **Use a signed delta / two noise samples** to avoid the documented
   single-axis→2π left-bias artifact.
2. **Angular-ACCELERATION smoothing** (smoothest, C2-continuous): drive the heading by a clamped change
   in angular *acceleration* rather than setting the turn rate directly — the pet never walks a perfectly
   straight line, never twitches. Clamp so it can't spin. (Medium-confidence source; math is sound.)
3. **Soft-leash BLEND, not switch** (Arrive behavior): instead of switching to steer-home at ExploreRadius,
   **additively weight** a return-to-owner steering up as distance grows past it (desired speed ramps to 0
   approaching owner) — reserve the hard snap for ReturnRadius. Smoother return, no rubber-band.
4. **Weighted idle FSM** (the "more random actions"): a lightweight decoupled state machine — states
   `sniff / sit / look-around / circle-owner / dash-ahead` — chosen by **weighted random** with a
   **per-state cooldown** + **randomized dwell** so nothing repeats back-to-back. Make weights
   **context-sensitive**: raise `dash-ahead` when the player is moving, `sit/sniff` when the player is idle.
   (Game AI Pro 3 pattern: states are decoupled, transitions composable — easy to add/remove behaviors.)
5. **Trig / parametric idle "personality"** as discrete FSM states (occasional, time-driven, allocation-free):
   - **Orbit owner** = `owner + r·(cos t, sin t)` — curious/loyal circling.
   - **Figure-eight / lemniscate** — playful weaving.
   - **Sine bob** (vertical `A·sin(ω t)`) — breathing/hover idle.
   - **Periodic dart** — brief high-speed seek-ahead then return.

## Recommended STARTING params (TUNE IN-ENGINE — no source gives pet numbers; derived from our leash)
- **Perlin drift:** step rate ~0.3–0.6 /s; amplitude mapped to ±`WanderTurnDegPerSec` (±70°/s) range.
- **Idle FSM:** cooldown 3–8 s; dwell 1–4 s randomized; weights e.g. sniff 30 / look 25 / sit 15 /
  circle 15 / dash 15 (context-shifted by player motion).
- **Orbit:** r ≈ `InnerRadius`..`ExploreRadius` (4.5–9 m); angular speed ~30–60 °/s.
- All clamped INSIDE the existing leash radii; the carrot/return logic stays the outer guarantee.

## Honest caveats (from the research)
- The numeric pet defaults above are **engineering judgment**, not sourced — tune in Play.
- Angular-accel smoothing (#2) is single-blog-sourced but mathematically sound (integrating input twice
  = low-pass smoothing); treat as solid principle.
- The trig mood-mappings (#5) are a synthesis of standard parametric math, not a primary-source claim —
  design guidance, use as occasional FSM states (not constant) so they don't read as scripted.
- `Mathf.PerlinNoise` is fine for per-pet drift; if directional isotropy matters at scale, prefer
  `Unity.Mathematics.noise` (simplex).

## Implementation shape (additive, allocation-free)
- Enhance `PetHeroLeash`: swap random jitter → Perlin drift (#1), optional angular-accel (#2), blend the
  soft-leash return (#3).
- Add a small `PetIdleBehaviors` component (or fold into Pet.cs): the weighted FSM (#4) that, on a cooldown,
  picks an idle state — some of which drive the carrot via the trig patterns (#5). Disable wander while an
  idle state owns the carrot; hand back when it ends. Everything stays inside the leash clamp.
