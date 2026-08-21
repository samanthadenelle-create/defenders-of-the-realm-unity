# WORK_ORDER_499 — BIOME ARENA BACKDROPS + tactical cover (THE WOW / engagement)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Why it matters (owner):** "we have the quests and functions, but THIS is the wow — this is what pulls
them in and KEEPS them, the engagement." The immersive battle is the retention driver. Make it amazing.
Extends WO-495 (themed arena). Mobile-first, skip-safe, near-zero perf.

## The system
1. **Biome BACKDROPS** — owner-provided Grok art (in `Assets/Resources/Arena/Backdrops/`, ~250-380KB each).
   Load each as a **skybox or large background plane behind the 3D treeline, with simple lighting** (unlit /
   emissive so it reads as a distant painted scene). ONE texture per biome = huge immersion, ~free perf.
   Biomes (assign the cryptic filenames -> biome keys by viewing): **forest** (`c1S70`=outerworld_backdrop,
   runed trees) · **crystal-cavern** (`LugGn`=cavern_backdrop) · **statue-ruins** · **VOLCANIC** (lava — the
   hard-family backdrop) · **runed dungeon-hall** · **castle courtyard**. (Raw files: aU9vc, Bh1tD, MxSKY,
   PNBkH, 9SBll, KTj1N — map to biomes, rename `<biome>_backdrop`.)
   - **SELECTION CRITERION (owner 2026-06-23):** a backdrop must have a DISTANT/OPEN foreground so the arena
     floor sits IN FRONT of it. **DROP the firefly-pond image** — its big foreground pond eats the playable
     kite space (the water would be inside the arena, not behind it). Pick backdrops whose foreground reads
     as "horizon," not a large near object.
2. **Per-biome PARTICLES (subtle, perf-cheap, for life):** floating leaves/pollen (forest), glowing motes
   (cave/mire), embers (dungeon/volcanic), dust motes (castle), mist drift. + optional subtle parallax.
3. **CYCLE the backdrop by enemy-family / progress (the danger gradient):** forest = early/easy, VOLCANIC =
   harder families, CAVE = wizard-heavy, CASTLE = tanky fights. Random within a tier, or tied to ThreatLevel
   (WO-467 danger gradient / WO-479 seed budget). The backdrop SIGNALS the fight's difficulty.
4. **TACTICAL COVER matched to the image (owner's big idea):** design the arena's INVISIBLE barriers to line
   up with the backdrop's natural features — columns, rocks, trees. The player kites/hides behind them to
   **break LINE-OF-SIGHT and INTERRUPT enemy casts** (the mage's telegraphed cast, WO-491/494). Turns a
   pretty backdrop into a TACTICAL playground without tight chokepoints (Grok's open-space positioning, WO-494).
   - Needs: a per-biome cover layout (a few colliders at the image's column/rock positions) + LoS checks
     (the cast already has a LoS mask, WO-449) so standing behind cover breaks the cast lock.

## Build (reuse-not-greenfield)
- **Backdrop:** in `BattleArena.BuildArena` (after `DressArenaEdge`, BattleArena.cs:219) add `BuildBackdrop(theme)`
  — a large unlit textured backdrop (cyclorama box / curved plane) behind the treeline, `Resources.Load` the
  biome texture, skip-safe (LogWarning + skip -> keeps today's sky). Parent to `_arenaRoot` (auto teardown).
- **Particles:** a per-biome `ParticleSystem` (code-built or a tiny prefab) parented to `_arenaRoot`; cheap,
  short lifetimes, pooled (WO-496 #11 "effects clear out fast").
- **Cycle:** pick the biome key from `BackdropContext` + ThreatLevel/family (a small theme-by-threat table).
- **Cover:** per-biome cover colliders (matched to the chosen backdrop) — phase 2 of this WO; the backdrop +
  particles are phase 1 (the visible wow); the LoS cover is the tactical layer on top.
- Mobile: max ~2k textures + crunch compression on import; unlit backdrop = no extra lights.

## Phasing
- **P1 (the visible wow):** backdrop plane per biome + subtle particles. Owner felt-verifies the immersion.
- **P2 (tactical):** image-matched invisible cover + LoS-break-interrupts-cast. The depth.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
