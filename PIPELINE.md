# PIPELINE — CLI Work Queue (pull from here)

> **CLI: this is your queue. Execute the ACTIVE QUEUE top-to-bottom.**
> UI writes orders here. CLI pulls, builds, verifies, commits, bakes.
> Full strategy/plan lives in `ORCHESTRATION_LIVE.md` — this file is just the live dispatch.

**Last dispatch:** 2026-05-31 by UI
**Build baseline:** GREEN ✅ `00b1662` / `8e4fd35`

---

## CLI PROTOCOL
1. Work the **ACTIVE QUEUE** in order. Batch A is serial (single-writer on `VillageSceneBuilder.cs`). Batch B may run concurrently (separate files).
2. **One commit per WO. Green build before the next.** Message: `feat: implement WO-### — <title>`.
3. **No bake** until told (Batch A ends with one bake; Batch B has none).
4. After each WO: set its **STATUS** below to `🟢 done`, write `WORK_ORDER_###_*.RESULT.md`, and append a line to the LOG.
5. If a fix breaks something or two WOs collide on a file, set STATUS `⚠️ blocked`, note it in LOG, and continue with the next safe WO.
6. Editor must be **CLOSED** for any bake.

Status tags: 🔴 todo · 🟡 doing · 🟢 done · ⚠️ blocked

---

## ACTIVE QUEUE

### ✅ UI APPLIED THE CODE FIXES DIRECTLY (2026-05-31, lane locked to UI) — CLI: VERIFY ON WINDOWS + BAKE
Owner locked the lane to UI to fix what CLI missed twice. UI applied these directly via Edit tools (Windows path), brace-balanced:
- **`Fortify.cs` `BuildMoat`** — wooden `Drawbridge_Medieval` → **`Bridge_Medieval_Stone`** (no ropes), centered over the moat; moat **widened** (outer 48/39→54/47, ~12m); crystal water → **flat quads + translucent teal water material**.
- **`Fortify.cs` `Ramp` lambda** — stairs rotation **+90° model-fwd correction** + scale to full slope length (reaches parapet). (WO-183 root cause: LookRotation aligned wrong axis + too short.)
- **`SmartMobileCamera.cs`** — made it the **sole** camera driver (disables sibling `VillageCamera`); framing → (0,18,-22), lookAt 2.5, hero-find fallback. (The WO-156 Deoccluder did nothing — `HeroCinemachineRig` is commented out/unattached.)
- **`TownsfolkDialogue.cs`** — "Force-fields…" bark → "The wards hold steady…".
- **GATE CLEAR — both fixes:** (1) `WireGateForceFields` removed (no barrier); (2) **`Walls.cs` L583-585: S/E/W mid-wall towers offset to −10** (flank the gate, matching North) — they were parked IN the S/E/W openings = the "stone blocking the door." Gates now physically clear.
- **`Fortify.cs` `BuildRamparts` (WO-181)** — overhanging machicolated rampart deck: wide walkway offset OUTWARD over the moat, crenellated parapet, corbel brackets, corner wraps; **all 4 stairs retargeted to land flush on the deck** (deckTopY 5.4, navmesh continuous) — stairs no longer dead-end into the wall.
- **`CityManifest.cs`** — footprint colliders on manifest buildings (no more walk-through); buildings −20% (`NormalizeProp` 7→5.6); wardens right-sized to hero (blacksmith no longer giant).

**⚠️ CRITICAL — MOUNT IS STALE/DESYNCED (§0).** Both agents flagged the Linux mount copies are out of sync (truncated, false brace counts). The **Windows files are authoritative and balanced**. CLI MUST:
1. Open the project on Windows, **let it COMPILE** (the real gate — if the agents' URP material API calls or edits don't compile, fix + report).
2. **Validate `CityManifest.json` parses on Windows** (mount left NUL artifacts).
3. **Bake** `BuildVillage` (editor closed) + combined navmesh. **UI cannot bake** (no batchmode).
4. Verify in playtest — **PER-GATE / PER-ITEM, "mostly" does NOT pass:**
   - [ ] **ALL 4 GATE OPENINGS (N/E/S/W) CLEAR & PASSABLE** — no stone/portcullis/wall/bridge/arch in ANY opening; hero walks straight through each of the 4 (test individually). *(WO-188 A1)*
   - [ ] **ZERO inner bridges** — no bridge/stone-arch inside the village or blocking a gate; the only bridges are the 4 stone ones OUTSIDE over the moat. **Remove the inner bridge.** *(WO-188 A2)*
   - [ ] stone bridges over wide moat (no drawbridge/ropes/portcullis); flat blue water (no crystal); stairs climb to parapet; hero stays framed; tree plaza clear; blacksmith at forge.
5. Commit by path.

**Residual flags for CLI:** (a) `DrawbridgeController.cs` (separate file) may still spawn drawbridge behavior — check/remove if wired. (b) `WireGateForceFields` in Fortify.cs is the gate force-field *visual* (separate from the bark) — confirm with owner whether that magical-barrier visual stays or goes (owner disliked "force-fields" conceptually).

---

### 🤝 CLI FREEZE-2 ORDER — (superseded above for moat/bridges/water/stairs/camera/bark; remaining: 181 ramparts, 192 ground, footprint colliders, building/unit scale)
**Status:** WO-189b city already baked (`3049b7f`). UI has **rebuilt `CityManifest.json`** to the district
template (19 buildings, ~38 props, sacred center cleared, 4 quadrants, blacksmith at forge, bridges at gates).
CLI now does the `.cs` builder fixes (which UI can't touch — the village partials are dirty/desynced on UI's
mount) + ONE bake. All fixes are specced in their WOs.

**STEP 0 — Validate the manifest JSON on Windows.** `Assets/Editor/CityManifest.json` was written through a
mount that left NUL/padding artifacts. **Confirm it parses as clean JSON on Windows** (strip any trailing
garbage + re-save) BEFORE baking — else `JsonUtility.FromJson` fails and the city won't populate.

**STEP 1 — `.cs` builder fixes (Windows, brace-gated, no bake yet):**
- **WO-183 stairs** — `Fortify.cs` `Ramp` lambda (~L211/230): add the model-forward rotation fix
  (`* STAIR_MODEL_FWD_FIX`, the Euler that maps the prefab's authored forward → +Z) AND scale the stair to the
  slope length (~10.3m) so it climbs flush against the wall and reaches the parapet. WO-166 only moved
  endpoints — fix the ROTATION + SCALE. Verify all 4 ramps.
- **WO-179 moat** — widen `BuildMoat` (`Fortify.cs`) so the moat reads real + adds defender distance; swap the
  blue-crystal water for the stylized water material (mobile: depth-texture OFF, foam from UV mask, no
  refraction); **navmesh excludes the water, includes the bridges only** (enemies funnel onto bridges).
- **WO-188 bridges** — `Bridge_Medieval_Stone` at all 4 gates spanning the (wider) moat, flush bank-to-bank;
  **remove the misplaced wooden drawbridge**; bridge length matches moat width.
- **WO-181 ramparts** — wide, full-perimeter walkable + siege-defense slots.
- **WO-189 follow-ons** (`VillageSceneBuilder.CityManifest.cs`): footprint colliders on manifest buildings
  (so enemies path around them); **building size −20%** (`NormalizeProp` target 7f→~5.6f); **unit/warden scale**
  (NPCs oversized vs hero — right-size); warden held-prop → hand-bone bind.
- **Bark fix** — remove the "Force-fields are humming steady on all four gates. Sleep easy, Keeper." line. **It's hardcoded in TWO files — strip BOTH or it survives:** `Assets/_Modules/Village/NPCs/TownsfolkDialogue.cs` AND `Assets/Editor/VillageSceneBuilder.Fortify.cs`. Reword to wards/wall theme (e.g. "The wards hold steady on all four gates.") or cut.
- **192 ground** (if not already in) — invisible Y=0 walkable plane + `TerrainBaseDepth`→0, `ExteriorTerrainBuilder` re-run WITH this bake.

**STEP 2 — ONE bake** (`BuildVillage` → reads new manifest; editor closed) + combined navmesh bake.

**STEP 3 — Verify:** `[CityManifest] placed 19 buildings…` in the log; district city (Commerce NE, etc.);
**tree plaza clear**; ~19 buildings not 29; bridges span the wider moat; stairs climb the wall to the parapet;
hero/enemies **cannot** walk through buildings or the water; blacksmith at the forge anvil. Screenshot.

**STEP 4 — Commit by path; owner playtests; UI releases the lane.**

#### 🧊 VILLAGE FREEZE CHECKLIST — do ALL of these in ONE bake (lock once, then release)
Everything below touches the single-writer lane. Land them together so the lane only freezes once:
- [ ] **189b city** — compile + bake the manifest (above). Plus: **footprint colliders** for manifest buildings (agent stripped colliders → enemies won't path around them; add like `BuildBuildings` does), and **warden held-prop → hand-bone bind** (props currently parented near hand as TODO).
- [ ] **188** stone entrances (`Bridge_Medieval_Stone`) replace drawbridges
- [ ] **192** ground z-fight (invisible Y=0 walkable plane + `TerrainBaseDepth`→0; re-run `ExteriorTerrainBuilder` WITH this bake — don't freeze twice)
- [ ] **179** water material (blue crystal shards → real water)
- [ ] **183** stairs orientation + full height
- [ ] **181** wide full-perimeter ramparts + siege slots
- [ ] **168** navmesh continuity across openings
- [ ] **Verify/fold:** 157 veins, 126 materials, 150 deleted-skip, 176 tower (manifest watchtowers), DEF-101 (gate-overlap + spawn points), DEF-106 (double wall ring)
- [ ] One bake → screenshot → owner playtest → UI releases the lane.

**NOT in the freeze (parallel, separate files):** camera 156, wave timer 186, pet/hero anim 184/187/174, console 163, ATB 169/170, WebGL/asset-cull/deploy, economy/build-mode/backend, OuterWorld/region content (no Village bake).

---

### 🧊 VILLAGE FREEZE 2 — city-bake findings + polish (one bake)
From the 2026-05-31 city-bake playtest. Split by who fixes it; all land in ONE re-bake.

**A. MANIFEST DATA — rebuild to the CANONICAL TEMPLATE (`DESIGN_VILLAGE_DISTRICTS.md`):**
Owner concept art is the definitive layout. Rebuild `CityManifest.json` to match:
- [ ] **4 quadrant districts:** NW Blacksmith/Craft (Blacksmith, Armorer, Lumbermill) · **NE COMMERCE** (Commerce Hall, Market, Pet Shop, Jeweler — the upgrade-gated corner) · SW Tavern/Social · SE Residential (Healer's Hut, NPC House, Hero's Home, houses). Reorganize from inner/mid/outer rings → clean quadrants.
- [ ] **SACRED TREE — clear EVERYTHING around it.** Remove ALL props/buildings/Heart-set pillars inside the circular plaza around the Tree (0,0,1); keep it fully open + a clean stone-ring plaza.
- [ ] **Density → ~HALF (not just −30%)** — "NOT crowded": ~3–4 buildings per quadrant, generous grass spacing. Cut from 29 toward ~14.
- [ ] **Reorganize EXISTING roster only** (don't source new meshes): Forge/Lumbermill/Barracks/Arcane→NW, Market/CrystalMine→NE Commerce, Tavern→SW, Houses(+Hero's-Home placeholder)/PetHouse→SE, Heart-only center, Farm/Granary→south apron. **Armorer/Jeweler/PetShop/Healer's-Hut/Hero's-Home/Commerce-Hall = FUTURE vision (upgrade-unlocked), NOT this pass.**
- [ ] **Blacksmith → forge** — pin the Blacksmith warden AT the forge anvil (currently on the road/wandering). All wardens stationary at their building.
- [ ] **SCALE PASS (two parts):** (1) **buildings −20%** (`NormalizeProp` target 7m→~5.6m in `CityManifest.cs`); (2) **unit/character scale** — NPCs/wardens oversized vs hero (blacksmith dwarf huge); right-size all units to the hero's scale.

**B. BUILDER CODE (CLI, `.cs`):**
- [ ] **183 stairs — ROOT-CAUSED** (see WO-183): `Fortify.cs` Ramp lambda — add model-fwd rotation fix (`* STAIR_MODEL_FWD_FIX`) + scale to slope length (~10.3m) so it climbs flush + reaches parapet. WO-166 only moved endpoints, never the rotation — that's why it regressed 3×.
- [ ] **Footprint colliders** on manifest buildings — confirmed "walk through buildings, no collisions"; add footprints like `BuildBuildings` does so hero/enemies are blocked + navmesh routes around.
- [ ] **Warden held-prop → hand-bone bind** (props parented near hand as TODO).
- [ ] **179 moat (WO-179):** widen the moat (`Fortify.cs` BuildMoat) so bridges span a real gap + add defender distance; stylized water shader (mobile, depth-texture-off, no refraction — replaces blue crystal); **navmesh excludes water, bridges are the ONLY crossings** (enemies funnel onto the bridge chokepoints).
- [ ] **188 bridges:** `Bridge_Medieval_Stone` at ALL 4 gates spanning the (now wider) moat, flush bank-to-bank; **remove the misplaced wooden drawbridge.** Bridge length matches moat width.
- [ ] **181 wide ramparts + siege slots.**

**C. DIALOGUE/CONTENT:**
- [ ] **Remove the "Force-fields are humming on four gates" bark** — no force-fields in a medieval realm; makes no sense. Rewrite to wards/wall theme or cut.

- [ ] one bake → owner playtest → release lane

### 🧊 FREEZE-3 — post-freeze-2-bake playtest findings (2026-06-01)
- [ ] **MOAT HAS NO VISIBLE WATER (not what was specced).** The moat CODE survived (wide 54/47 band, flat water
      quads, stone bridges — all in committed `Fortify.cs`), but **the water isn't visible:** there's no dug
      CHANNEL, so the water plane at y=−0.4 sits UNDER the flat grass, and/or the code-built URP-transparent
      `MoatWater` material isn't rendering. FIX: **dig the moat channel** (lower the ground in the 42–54 ring so
      the water sits in a visible depression below grade) AND verify the water material actually renders
      (translucent blue). The wall should overlook a visible water-filled moat, per WO-179 + the WO-181 sketches.
- [ ] **WORLD VOID AGAIN — bake-pairing bug (P0).** Terrain is correctly in `OuterWorld.unity` (WO-173 Option A
      ✓), but the freeze-2 bake rebaked **Village only** → OuterWorld not re-baked/loaded → world comes up void.
      **FIX: the village bake MUST also bake OuterWorld** (re-run `ExteriorTerrainBuilder.BuildExterior`) AND
      confirm `WorldSceneLoader` loads OuterWorld additively at play. This is WO-173 acceptance ("terrain survives
      a Village rebake") — make the two-scene bake a PAIRED step in the bake checklist so it can't regress again.
- [ ] **Stairs: STOP at the top level + leave a LANDING with room to get up (owner 2026-06-01).** The top step
      must terminate exactly at the deck walkable surface (deckTopY) — not overshoot, not stop short — AND there
      must be a **flat landing with standing clearance** at the top, **set back from the parapet/merlons**, so the
      player can step OFF the stairs ONTO the walkway (don't run the stairs right up against the crenellations
      with no room). Every stair instance. (WO-181 hard rule + this landing-clearance refinement.)
- [ ] (carryover) tree center: a building (the church/blue) may still read as on the tree island — verify the
      manifest 14m clear actually baked; if a builder-placed structure sits there, move it.

### 🔥 PRIORITY 0 — WORLD RESTORE (do FIRST, standalone fix + bake + SHIP A BUILD)
**This is THE blocker — owner: "biggest P0, blocks 80% of game." Decoupled from Batch A so CLI fixes
the world, bakes it ALONE, and ships a build the owner can walk in — before any other village work.**

| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| P0 | **173 world restore** | 🟢 **DONE** (commit 8f4c6f3 — `EnsureTerrainMaterial` was missing; terrain renders; village rebaked green. Linear DEF-108 = Done. Owner verify on fresh build.) | World is a floating slab in black void; no terrain/maps beyond the village. **Root cause (already diagnosed in WO-173): scene-split orphaned the terrain** — built into Village.unity, stripped by castle rebakes; OuterWorld.unity has regions/nodes but no ground. **DECISION LOCKED — Option A:** retarget `ExteriorTerrainBuilder.BuildExterior` to build terrain into **OuterWorld.unity** so terrain+regions+nodes load together via WorldSceneLoader (and future Village rebakes can't wipe it). Also confirm skybox/lighting/fog survive the additive load. | Village sits in a **visible landscape** (terrain+biomes), not void; 4 regions + mine nodes on the ground; WorldSceneLoader load confirmed in log; terrain survives a Village rebake; **bake + produce a fresh build for owner to verify.** |

### BATCH A — Playable Village (SERIAL — top to bottom, ONE bake at end)
Goal: clear the P1 geometry so the village is playable and exitable. (P0 world restore runs FIRST, above.)

| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| A1 | ~~173 terrain~~ | ↪ moved to PRIORITY 0 | Pulled out as standalone world-restore + early bake. | — |
| A2 | 177 wall lean / walk-through / south gate | 🔴 | P1 — walls lean, hero clips through. | Segments upright + collidable; south gate oriented right; 4 sides consistent. |
| A3 | **188 solid STONE entrances replace gates** | 🔴 | **DESIGN CHANGE (owner) — SUPERSEDES the gate/drawbridge cluster.** Gates broken — **EAST, SOUTH, WEST all blocked**; drawbridge half upside-down. Owner: remove gates + drawbridges, put a **solid STONE entrance** (`Bridge_Medieval_Stone`, not wood) over the moat at each crossing — always passable, no moving parts. | 4 stone entrances; no portcullis/drawbridge/ropes; hero+enemies cross; flush; navmesh continuous. |
| A3b | **192 ground z-fight fix** | 🔴 | Diamond holes in interior grass = village floor + world terrain coplanar at Y=0. Floor already removed (owner) — **finish safely:** invisible Y=0 walkable collider plane (so navmesh still bakes a floor), drop `TerrainBaseDepth`→0, re-bake. Spec: WO-192. | No z-flicker on fresh build; interior navmesh walkable; doesn't re-break WO-173. |
| A4 | ~~158 gates impassable~~ | ↪ folded into 188 | Re-scoped: "all sides passable" now satisfied by solid bridges. | — |
| A5 | ~~167 gatehouse pillar clip~~ | ↪ 188 | Likely moot (gatehouse arch may go). Keep clip fix only if a decorative arch stays. | — |
| A6 | 168 navmesh across openings | 🔴 | Still needed — continuity across each bridge (now trivial, no portcullis). | NavMesh continuous across all 4 bridges. |
| A7 | 183 stairs orientation + height | 🔴 | Stairs **inconsistent**: some float/disconnected, some rotated wrong, **and at least one too SHORT — doesn't reach the wall top** (playtest r1+r2). Turn all to ascend against the wall AND run full height to the parapet. | Every stair instance: correct rotation, flush ground→**full parapet height**, hero walks all the way up; navmesh continuous. |
| A8 | 179 fix water | 🔴 **(eyesore — elevate)** | **Owner playtest r4 2026-05-31: "water looks really bad."** Moat renders as bright-blue **crystalline spiky shards**, not water — high-visibility, reads as broken. Do it in this scene pass. | Moat reads as actual water (flat/translucent material + gentle surface, NOT crystal shards); sits in the channel below grade; no floating blue chunks. |
| A9 | 157 strip magenta crystal veins | 🔴 | P2 — deleted veins respawn as magenta on bake. | Vein generator removed from rebake; no magenta. |
| A10 | **189b builder hook** | 🟢 **CODE DONE (UI)** — awaiting CLI compile+bake | UI landed it additively: new partial `Assets/Editor/VillageSceneBuilder.CityManifest.cs` (braces 64/64) reads `Assets/Editor/CityManifest.json` and instantiates ~29 buildings/~100 props/6 wardens/4 bridges under `CityManifestRoot`; one-line hook in `VillageSceneBuilder.cs:375` after `BuildBuildings`, before navmesh bake; skips the 5 existing buildings; warn-and-skip on missing prefabs. | Builder consumes manifest on bake; city populates; nothing hand-placed. |

> **189a (city authoring) runs in PARALLEL — see Lane G below.** Split out so the village redesign
> stops waiting behind the bug fixes: the layout/roster/bindings are DATA (no VillageSceneBuilder geometry),
> authored now; only 189b (the builder hook + bake) is serial and lands at the Batch A rebake.
| A11 | 181 wide ramparts + stairs + siege slots | 🔴 | **Wide full-perimeter walkable rampart** (defenses go up top, ground stays clear) + stairs (WO-183) + unlockable siege-defense slots. | Walk entire perimeter loop; navmesh continuous; siege slots up top; ground uncluttered. |
| A12 | 137 village rebake | 🔴 | Closes Batch A — land A1–A11. | `Defenders > Week 3 > Build Village Scene` (batchmode `DeNelle.Editor.VillageSceneBuilder.BuildVillage`), **editor closed**. No errors; bake-twice match for WO-189; screenshot for UI. |

### BATCH B — Parallel-safe (separate files; run alongside A; commits still serialize)
Goal: stop the game feeling broken; deliver owner's #1 demoable. Skip any WO that collides with an in-flight A step — mark ⚠️ blocked and report.

| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| B1 | 163 console error triage | 🔴 | P1 — ~3,351 errors/boot (AmbientNPC spam) drown real errors + cost perf. | Guard animator params; fix AudioMixer exposed-param; clean boot log. |
| B2 | 174 hero walks backwards + no walk anim | 🔴 | P1 — core locomotion looks broken. | Hero faces/moves travel dir; walk anim on move, idle on stop. |
| B3 | 156 camera pivot over high walls | 🔴 | P1 — hero off-screen; 3 controllers fight. **Playtest 2026-05-31: confirmed STILL broken — camera angle breaks when hero is behind a wall (no pivot/fade, hero hidden).** | One authoritative controller; hero stays on-screen behind walls; pivots above parapet; wall-fade on occlusion. |
| B4 | 135 P1 bug cluster | 🔴 | P1 — CrystalMine auto-upgrade, VFXManager drift, WaveManager dict leak. | Each sub-bug fixed per WO; no new warnings. |
| B5 | 117 worker dispatch & auto-collect (Ph1) | 🔴 | ⭐ owner #1 demoable — idle/harvest hook of core loop. | ResourceType enum + ResourceNode + Worker → auto-collect wood to cap → bank. Data only, no bake. |

---

### BATCH C — Unblocked by owner decisions (2026-05-31) — queue after A/B
| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| C1 | 169 ATB party-of-4 + real models | 🔴 | **Decided: ATB = separate PvE mode.** Build self-contained: party of 4, real enemy meshes (not purple pills), dynamic HUD. No coupling to village loop. | Per WO-169 phases; enemies render as models; party HUD dynamic. |
| C2 | 170 2D retro battle VFX | 🔴 | After C1. Spell/hit VFX for the ATB mode. | Per WO-170. |
| C3 | 181 rampart stairs + upper siege defenses | 🔴 (Lane A) | Castle's remaining work: stairs to upper level + unlockable siege-defense slots. **Serial — after Batch A rebake, folds into next bake.** | Per WO-181. |
| C4 | 182 Avalon→Elarion purge | 🔴 (docs) | **Decided: Elarion canon.** Edit live specs only, leave history. No build. Unblocks WO-116. | Per WO-182. |

### BATCH D — Economy + build-mode keystone (parallel, Lane C — own files)
Run in strict order; each gates the next.
| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| D1 | 164 zone foundation (ThreatLevel + records) | 🔴 | Do first — read by Lanes B & D, unblocks region/world work. | Per WO-164; ThreatLevel + zone records in place. |
| D2 | 131 wallet unification | 🔴 | **Decided: EconomyService = single ledger.** GameState + ResourceBalance become thin reads that call into EconomyService; one source of truth for all crystal/resource totals. | One authority (EconomyService); GameState/ResourceBalance defer to it; no divergent totals; save/load consistent. |
| D3 | 108 player build mode | 🔴 ⭐ keystone | The core-vision feature — hand the player VillageSceneBuilder's placement power. After D1+D2. | Per WO-108; player can place walls/towers/mines on their plot; spends via EconomyService. |

### BATCH E — Playtest findings (2026-05-31) — parallel-safe code, fold in where lane fits
(183 stairs + 179 water + 188 bridges moved into Batch A — scene work.)
| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| E1 | 184 pet T-pose | 🔴 (Lane B/E) | Pet walks in **T-pose** — animator not bound/driven. Code only, no bake. | Idle + walk cycle play; no T-pose; no animator warnings. |
| E2 | 187 pet clips through walls | 🔴 (Lane B) | Pet passes **through walls** — not on navmesh / ignores colliders. Separate from T-pose. | Pet respects wall collision, routes around; keeps up w/ failsafe. |
| E3 | 185 hero→pet-select flow | 🔴 (Lane UI/FTUE) | Hero select **drops straight into village** — pet-select screen missing. Code-built UI. | Title→hero→PET SELECT→village w/ chosen pet. |
| E4 | 186 wave countdown timer | 🔴 (Lane UI) | **No visible wave timer** (only START WAVE button + hourglass). Player can't read pacing. | Visible countdown; manual early-start still works. |

**Also (known):** brown void beyond village (incl. east/south) = WO-173 (A1). Camera-behind-wall = WO-156 (B3).

## DESIGN ITEM — whole-city planning (not a code WO yet)
Owner playtest note: "needs whole city planning." The village currently reads as ad-hoc placement.
Elevate **WO-152** (city information architecture / layout) from PARKED → needs a deliberate layout
plan before more structures land. Per core-loop design, the player builds from scratch (WO-108), but
the **starting/tutorial city + layout language** need an intentional pass. UI to draft a city-plan
doc when owner is ready. (Design, not dispatch — do not hand to CLI yet.)

### LANE G — Village redesign authoring (PARALLEL — no VillageSceneBuilder geometry)
The village redesign has waited too long behind the serial bug lane. Decoupled: author the city as DATA now.
| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| G1 | **189a author CityManifest** | 🟢 **DONE** | `CityManifest.draft.json` + `.README.md` authored: 29 buildings, 100 props, 6 Wardens (blacksmith→forge anvil+hammer), 4 stone bridges, 4 cobble roads. Grounded in real constants (inner wall 28/21, gates N/E/S/W, Heart at 0,0,1). 0 building violations, all prefab paths verified on disk. Ready for 189b. | ✓ Data file ready for the builder to consume. |

### BATCH F — WebGL / Web deploy
| # | WO | Status | Order + reason | Acceptance |
|---|---|---|---|---|
| F1 | 190 fresh WebGL build | 🔴 | Existing `Builds/WebGL/` is stale (pre WO-173 world fix). Rebuild on current green tree via `build-webgl.ps1`, editor closed. | Build succeeds green; index.html + Brotli `.br` present; log total + `.data.br` size. |
| F2 | (owner) itch.io upload | 🔵 owner task | Zip `Builds/WebGL/` contents + upload to itch. **Guide: `DEPLOY_WEBGL_ITCH_GUIDE.md`.** | Public/restricted playable web link. |
| F3 | **191 WebGL size optimization** | 🔴 | **Ship ONLY used assets** (owner). Root cause: `Resources/` force-ships everything; 92 MB orphan FBX + uncrunched textures + 100% audio. Phase 0 unused-asset audit → Phase 1 quick wins (~70–95 MB total) → Phase 2 Addressables (initial download ~15–25 MB). Spec: WO-191. | Per WO-191 phases; before/after sizes logged. |

## HELD — none blocking. (116 NPC dialogue auto-unblocks when WO-182 purge lands.)

---

## LOG (newest first — CLI + UI append one line per event)
- 2026-05-31 — **FREEZE 2 ready.** UI rebuilt `CityManifest.json` to district template (29→19 buildings, 100→38 props, sacred center cleared, 4 quadrants, blacksmith@forge, bridges@gates). UI could NOT touch the village `.cs` partials (dirty/desynced on mount) → **CLI does the `.cs` fixes + bake** per the clean freeze-2 order above (stairs WO-183, moat WO-179, bridges WO-188, ramparts WO-181, colliders/scale WO-189, bark, 192). STEP 0: validate the JSON on Windows (mount left NUL artifacts). Camera WO-156 + WO-191 + WO-120 are CLI's parallel lanes.
- 2026-05-31 — **Backend reconciled (WO-120 addendum).** Client is offline-first; client calls = the contract. CRITICAL: two economies — `EconomyService` (run, ephemeral) vs `GameState.ResourceBalance` (persisted = DB source of truth). **WO-80 superseded.** Minimal schema defined (JSONB save blob + auth_nonces + events + bug_reports MUST-HAVE; leaderboard/profile/promo/entitlements LATER). Hard gate: save/load has NO signature auth (anyone can overwrite any save) — wallet-nonce auth required. Fixes: 405→400, Avalon→Elarion in spec, v10→v11 drift. Owner DECIDED (industry standard): **authenticate saves NOW (mandatory wallet-nonce sig); soft currency client-owned at launch + server sanity-checks; flip economy to server-authoritative when crypto/IAP value enters** (on-chain entitlements verified server-side).
- 2026-05-31 — **WO-189 city LANDED (code).** Village lane locked to UI (CLI handoff @ `202d026` clean). City authored as data (`CityManifest.draft.json`, WO-189a) → UI implemented the builder hook (WO-189b): new additive partial `VillageSceneBuilder.CityManifest.cs` (64/64 braces) + 1-line hook at `VillageSceneBuilder.cs:375` + `Assets/Editor/CityManifest.json`. Reuses existing helpers, skips the 5 dupes, warn-and-skip on missing prefabs. **Handed CLI the bake** to compile-verify + populate (UI doesn't fire batchmode). Fold the rest of the village pass (188/192/179/183/181) into the same bake = one freeze.
- 2026-05-31 — **Playtest r4 + architect.** Water eyesore → WO-179 elevated (blue crystal shards → real water). Ground diamond-holes = village floor/world terrain coplanar → architect: floor already dropped, finish safely (invisible walkable plane + TerrainBaseDepth→0) → **WO-192**. Owner: drawbridges → **STONE entrances** (`Bridge_Medieval_Stone`) → WO-188 updated.
- 2026-05-31 — **Asset cull approved.** CLI reference audit → owner-approved REMOVE NOW (green-gated, ~140 MB raw): `pet-aether-twilight.fbx` (91 MB, 0 refs), `Resources/Enemy/` (28 MB, stale test, code uses `Enemies/`), `CC5Hero/` + `Editor/CC5ExtractTex.cs` (21 MB). VERIFY: `Textures/Cathedral.png` (23 refs). DEDUPE: `.fbm` → keep `Textures/<name>` copy + gitignore `.fbm`. KEEP+compress: 3 pets + 3 heroes. Codified in WO-191 Phase 0.
- 2026-05-31 — **WebGL deploy + optimization planned.** WO-190 (fresh build on green tree), `DEPLOY_WEBGL_ITCH_GUIDE.md` (itch upload = fastest link), WO-191 (size optimization). Architect found: 181 MB `.data` = all of `Resources/` (537 MB raw force-shipped); **92 MB orphaned `pet-aether-twilight.fbx` with zero references**; textures uncrunched; audio 100%/stereo. Owner principle: ship ONLY used assets → Phase 0 unused-asset audit. Targets: Phase 1 quick wins ~70–95 MB; Phase 2 Addressables ~15–25 MB initial download.
- 2026-05-31 — **CLI SHIPPED a batch** (branch `feat/tower-core-loop`, green through `475b04a`). DONE: WO-173 world/terrain (8f4c6f3), WO-181 VillageSceneBuilder refactor 4657→657 + ramparts, WO-166 stairs, WO-157 veins, WO-177 walls/south-gate, WO-167 pillars, WO-164 zone scaffolding. Linear synced: **DEF-108→Done, DEF-109→In Progress.** ⚠️ COORDINATION GAP: CLI built 4 **drawbridge** gates (north only has 3) BEFORE owner's WO-188 decision to **replace gates with solid bridges** — CLI's next gate step should be WO-188, not more drawbridges. Hero may render as violet capsule (Wizard.fbx not found) — note for animation lane. New build to playtest: `Builds\Windows\DefendersOfTheRealm.exe`.
- 2026-05-31 — **WORLD RESTORE elevated** (owner: "biggest P0, blocks 80%, still no world/maps"). Pulled WO-173 out of Batch A → standalone **PRIORITY 0** (fix + bake ALONE + ship a build to verify). Decision LOCKED: Option A (terrain → OuterWorld.unity). NOTE: build is unchanged since `00b1662` — nothing fixed until CLI runs; this is the first order to hand CLI.
- 2026-05-31 — **Playtest round 3 + city design** (owner). ROOT-CAUSED the empty-city regression (rebake wipes non-persisted content) → **WO-189 persistent City Manifest** + `DESIGN_ELARION_CITY.md` (grounded in real catalog prefabs). Owner calls: archer tower → KayKit `building_watchtower` (drop ornate `Tower_Medieval_Big`); **wide full-perimeter ramparts** so defenses go up top, ground stays clear → WO-181 updated; Wardens must bind to buildings w/ held props (blacksmith → forge+anvil+hammer). New finds: hero facing 90° off (N→W) pinned to WO-174; pet T-pose still (WO-184).
- 2026-05-31 — **Playtest round 2** (owner). DESIGN CHANGE: remove gates+drawbridges → **WO-188 solid bridges** (supersedes WO-158/167 drawbridge work; folded into Batch A). New: WO-186 wave timer, WO-187 pet through walls. Moved WO-179 water + WO-183 stairs into Batch A (scene pass). Confirmed still broken: WO-156 camera (breaks behind walls), WO-183 stairs (another rotated wrong + too short), WO-158 (SOUTH gate also blocked), WO-179 (moat = blue crystal chunks).
- 2026-05-31 — **Playtest round 1** (owner). New: WO-183 stairs orientation (Lane A), WO-184 pet T-pose, WO-185 hero→pet-select flow → Batch E. Confirmed: WO-173 (no world beyond village, incl. east), WO-158 (EAST gate blocked/portcullis down). Design flag: whole-city planning → elevate WO-152.
- 2026-05-31 — Owner decision: wallet authority = EconomyService → WO-131 scoped; economy/build-mode chain (164→131→108) unblocked into Batch D. No HELD items remain.
- 2026-05-31 — Owner decisions: (1) ATB = separate PvE mode → WO-169/170 unblocked into Batch C; (2) Elarion canon, purge Avalon → new WO-182; (3) WO-136 castle done, remaining work = stairs + upper siege defenses → new WO-181 (Lane A, after rebake).
- 2026-05-31 — UI dispatched Batch A (playable village) + Batch B (parallel-safe). Awaiting CLI pickup.
