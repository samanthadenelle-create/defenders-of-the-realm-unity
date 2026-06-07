# CLI DISPATCH — 2026-06-03
**Up to 7 concurrent agents at CLI discretion.**

---

## Housekeeping complete: WO renumber

19 duplicate WO numbers (231–253 range) renumbered to 258–276.
CLAUDE.md updated: highest WO = **276**. No more collisions.

---

## STABILITY (fix what's broken)

### Urgent
- **WO-248** Hero Hover Exploit Fix → DEF-147 `CODE parallel`
- **WO-133** OnboardingFlow Wiring (FTUE) → unblocks DEF-153 `CODE parallel`
- **WO-253** Tutorial Speech-Bubble Overlay → DEF-153 `HUD parallel` (depends 133)

### High
- **WO-174** Hero backwards + walk anim `Combat parallel`
- **WO-232** Rendering & URP Sweep → DEF-6,94,96,99,103,106,114 `mixed`
- **WO-233** Collision & NavMesh Sweep → DEF-11,19,25,101 `mixed` (depends 232)
- **WO-234** Animation Sweep → DEF-5,95 `parallel`
- **WO-150** Village Roster Reconcile (magenta ghosts) `VSB serial`
- **WO-157** Strip Crystal Veins (deleted content) `VSB serial`
- **WO-247** Village Cleanup (upside-down tree + seam) → DEF-96,126 `VSB serial`

### Medium
- **WO-163** Console Error Triage `mixed parallel`
- **WO-166** Playtest Regressions (gates, walk, pet) `VSB serial`
- **WO-167** Gatehouse pillar clips ceiling `VSB serial`
- **WO-168** NavMesh unseal gate openings `VSB serial`
- **WO-177** Wall orientation wrong `VSB serial`
- **WO-206** Moat water material `VSB serial`
- **WO-212** Gate Z-Fighting `VSB serial` (depends 196)
- **WO-249** Oversized NPC Scale → DEF-148 `CODE parallel`
- **WO-250** Portal Interior Glow VFX → DEF-100 `CODE parallel`
- **WO-251** Landing Page UI Fixes → DEF-134,144,145 `HUD parallel`
- **WO-252** NPC Dialogue Z-Order → DEF-149 `HUD parallel`
- **WO-135** P1 Bug-Triage Audit `mixed`

---

## PIPELINE (unblock future work)

- **WO-207** Split VillageSceneBuilder into partials `VSB serial` — **DO FIRST**, unblocks all VSB work
- **WO-196** Rebuild WebGL without Brotli `Build parallel` — unblocks 212, 213, 214
- **WO-211** WebGL Build Optimization `Build parallel`
- **WO-164** Zone Foundation (depth + ThreatLevel) `Core parallel` — unblocks 155, 160, 205, 239
- **WO-201** Catalog Data Model `Core parallel` — unblocks 148, 149
- **WO-140** Hero Animator Factory `Editor parallel` — unblocks 217, 218, 234
- **WO-202** CC5 Character Pipeline `Character parallel` — unblocks hero cards (223–226)
- **WO-136** Complete Castle Structure `VSB serial`
- **WO-274** Project Restructuring `mixed` — unblocks 275
- **WO-275** Silo Architecture `mixed` (depends 274)
- **WO-173** Exterior Terrain (black void fix) `World parallel` — unblocks 245, 247

---

## VALUE STREAM (player-facing features)

### Core Loop
- **WO-239** Kill → Claim → Build → Defend (Node System) `gameplay parallel`
- **WO-205** Node Settlements (claim → harvest → defend) `gameplay parallel`
- **WO-242** Mobile-First HUD & Interaction → DEF-129,137 `HUD parallel`
- **WO-169** ATB Refinement (party-of-4) `BattleATB parallel`
- **WO-215** Build Mode Click-to-Place `gameplay parallel`
- **WO-237** Building Upgrade Panel `gameplay parallel`
- **WO-235** Hero Death + Heartwood Destroyed Screen → DEF-102 `HUD parallel`

### World & Exploration
- **WO-245** World Terrain + Nature + POIs → DEF-61,62,63 `WORLD parallel`
- **WO-155** Region Enemy Spawning `Combat/AI parallel`
- **WO-160** Wandering Tribes `Combat/AI parallel`
- **WO-216** Enemy Camps System `Combat/AI parallel`
- **WO-165** Dungeon World Portals `gameplay parallel`
- **WO-244** Node Visibility & Discovery `gameplay parallel`
- **WO-153** World Crystal Mine `economy parallel`
- **WO-154** Rare Crystal Spawns `economy parallel`

### Combat & Battle
- **WO-276** FF-Style ATB System `BattleATB parallel`
- **WO-258** ATB Critical Bug Fixes `BattleATB parallel`
- **WO-170** 2D Battle Animations & VFX `BattleATB parallel`
- **WO-217** Animation Polish `animation parallel`
- **WO-218** Animation Layering `animation parallel`
- **WO-219** Visual Feedback (Hit-Stop, Shake) `VFX parallel`
- **WO-259** In-World Combat Core `Combat parallel` (depends 232)

### Story & Onboarding
- **WO-222** Tutorial Redesign `gameplay parallel`
- **WO-227** Opening Cutscene & Story Companion `narrative parallel`
- **WO-228** Resource Gathering Tutorial `gameplay parallel`
- **WO-238** Sylas First Meeting `narrative parallel`
- **WO-231** Party Assembly + Early Resource Loop `gameplay parallel`

### Heroes & Characters
- **WO-223** Archer Hero Card `HUD parallel`
- **WO-224** Knight Hero Card `HUD parallel`
- **WO-225** Mage Hero Card `HUD parallel`
- **WO-226** Cleric Hero (4th class) `HUD parallel`
- **WO-246** Replace KayKit NPCs → DEF-91 `VSB serial`

### UI & Polish
- **WO-175** Store Visual Polish `UI parallel`
- **WO-178** HUD Health-Bar Styling `UI parallel`
- **WO-176** Tower Visual Polish `Art/VSB serial`
- **WO-236** Cosmetic Store UI `UI parallel` (depends 232)
- **WO-229** Resource Harvest Feedback `HUD parallel`
- **WO-240** Heartwood: Living Tree Asset `VSB serial`

### Economy & Monetization
- **WO-172** Build/Upgrade Timers + Ad Speedup `economy parallel`
- **WO-180** Production Building Roster `VSB serial`
- **WO-273** Zone Architecture Redesign `architecture parallel`

### Audio
- **WO-243** Audio Full Pass → DEF-36 `audio parallel`
- **WO-171** Music: ATB + Overworld Themes `audio parallel`
- **WO-162** Player Music Selection `audio parallel`

### Camera & Misc
- **WO-156** Village Camera (over walls) `camera parallel`
- **WO-214** Dual-Camera System `camera parallel` (depends 196)
- **WO-221** Defend Tower Camera Closer `camera parallel`
- **WO-241** AlertIntelSystem `gameplay parallel`
- **WO-148** Catalog Structure Factory `code parallel` (depends 137)
- **WO-149** Catalog-Driven Persistence `code parallel`
- **WO-158** Gates impassable `VSB serial`

---

## Lane constraints for concurrent agents

`VillageSceneBuilder.cs` = **serial bottleneck** — max 1 agent at a time.
Everything else is parallel per CLAUDE.md §9. CLI allocates up to 7 agents across non-conflicting lanes at its discretion.

---

*UI agent — 2026-06-03*
