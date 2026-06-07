# Backlog Triage — 2026-06-04

Grant firefight. Goal = a PLAYABLE demo a judge can run end-to-end: intro → hero
select → village → fight a wave → (loop). Everything below is ranked by ROI toward
that single outcome.

**Closed today (done/moot):** DEF-247, DEF-256, DEF-254, DEF-245. See
"Closed this pass" at the bottom.

**Legend:** `CODE-only` = isolatable agent, parallelizable (no Unity project lock).
`SCENE/BAKE` = touches Village2/OuterWorld scene or needs a navmesh/light bake →
**serial, single project lock** (one at a time, gated by CLI). `BLOCKER` = the demo
is unplayable / a whole lane is stuck until this lands.

---

## TOP 8 HIGH-ROI PULLS (across all lanes, ranked)

| # | Issue | Title | Lane | Code/Scene | Blocker? | Why / effort |
|---|---|---|---|---|---|---|
| 1 | **DEF-253** | BLOCKER: Can't get past intro/song screen — no way to advance to hero select | UI/HUD | CODE-only | **BLOCKER** | The single most important ticket. Game is literally unplayable — judge is stuck on the song screen. Add a tap/skip/auto-advance to hero select. Small, surgical fix; unblocks the entire demo. ~1-3h. |
| 2 | **DEF-246** | CRITICAL: WebGL catalog loading — replace File.ReadAllText with UnityWebRequest async fallback | Monetization/Backend | CODE-only | **BLOCKER** | "Catalog unavailable" on itch.io = towers/buildings/enemies empty on the live web build the judge actually runs. Per MEMORY the CanonicalJson loader pattern already exists (Resources.Load first) — finish converting remaining catalogs. ~3-5h. |
| 3 | **DEF-257** | Moat water plane covers entire exterior — hero/NPCs standing in water | World/Environment | SCENE/BAKE | near-blocker | "How do I turn this blue off" — first thing the owner saw. Whole-exterior water is the worst visual read in the demo. Root cause known (Village2Generator.CreateMoat one giant quad). Fix generator + regen + bake. High visible polish, moderate effort. ~2-4h. |
| 4 | **DEF-237** | REFIX: Tower model still oversized/ornate — spec is simple wooden watchtower | World/Environment | SCENE/BAKE | no | Towers are THE tower-defense fantasy on screen. Wrong model undercuts the genre read. Swap to SM_Tower_Medieval_Wood per spec, regen. Code-adjacent (prefab path) but lands via the baker → serial. ~2h. |
| 5 | **DEF-238** | REFIX: Moat/bridge — planks buried, no water, no channel | World/Environment | SCENE/BAKE | no | Pairs with DEF-257 — do them in one moat/bridge pass. Bridge is the gate-approach the enemies/hero cross; buried planks read as broken. ~2-3h combined with #3. |
| 6 | **DEF-166** | Kill → Claim → Build → Defend loop (node claiming + outpost) | Combat/AI | CODE-only | no | This IS the core loop the grant pitch sells (fight→harvest→upgrade→push). Even a thin vertical slice makes the demo a *game* not a sandbox. Larger (~1-2d) but highest narrative-of-the-pitch ROI; scope to one claimable node. |
| 7 | **DEF-171** | Build Mode click-to-place + green/red grid preview | UI/HUD→gameplay | CODE-only | no | Tower-defense without placing towers isn't a demo. Click-to-place w/ valid/invalid preview is the interactive hook a judge will try first. ~half day. Pairs with DEF-244 roster. |
| 8 | **DEF-255** | Rampart walkway too narrow for tower placement | World/Environment | SCENE/BAKE | no | Directly blocks wall-mounted tower placement (DEF-244/DEF-171 payoff). Widen walkway / add placement pads in generator, regen. ~2h. Sequence right after the moat pass while the project is open. |

**Sequencing note for the lead:** #1, #2, #6, #7 are CODE-only → hand to parallel
agents now. #3, #4, #5, #8 are all World/Environment SCENE/BAKE → batch them into
**one serial Village2 regen+bake pass** (moat + bridge + tower model + rampart width)
to avoid repeated project locks and repeated bakes. See DEF-236 process note first.

---

## Lane: World/Environment (mostly SCENE/BAKE — serial, one Unity lock)

> These contend for the Village2Generator + bake. Batch into a single regen pass.

1. **DEF-257** [Urgent] — Moat water plane covers entire exterior — `SCENE/BAKE` — **near-blocker** — worst visual read in demo; fix CreateMoat() ring channel. *(Top-8 #3)*
2. **DEF-237** [Urgent] — REFIX tower model → simple wooden watchtower (SM_Tower_Medieval_Wood) — `SCENE/BAKE` — genre-defining prop, spec ignored twice. *(Top-8 #4)*
3. **DEF-238** [Urgent] — REFIX moat/bridge planks buried, no channel — `SCENE/BAKE` — pair with DEF-257 in one pass. *(Top-8 #5)*
4. **DEF-255** [High] — Rampart walkway too narrow for tower placement — `SCENE/BAKE` — gates the tower-placement payoff. *(Top-8 #8)*
5. **DEF-242** [Urgent] — P0 full village rebuild, modular Medieval Village 4 zones — `SCENE/BAKE` — **large.** Superseded in spirit by the Village2 factory that shipped today. ROI now LOW for the demo (Village2 is already standing on wood walls). **Recommend: down-scope to "polish what Village2 already generates" and de-prioritize the full teardown** — a full rebuild mid-firefight is high-risk for low marginal judge-visible gain. Flag for owner.
6. **DEF-236** [Urgent] — PROCESS: root-cause analysis before any gate/wall ticket closes — `CODE-only` (analysis/doc) — **do this once, first**, as the gating doc for the World batch above; prevents the 5th regression. Cheap, high leverage on the rest of the lane.
7. **DEF-210** [High] — Source crafting-station art (Campfire/Mortar/Kettle) from existing packs — `CODE-only` (asset audit) — only matters if pet/consumables (DEF-207/249) ship; LOW demo ROI now. Defer.

---

## Lane: Combat/AI (mostly CODE-only — parallelizable)

1. **DEF-166** [High] — Kill→Claim→Build→Defend loop / node claiming + outpost — `CODE-only` — the pitch's core loop; scope to one node. *(Top-8 #6)*
2. **DEF-244** [High] — Tower roster: 6 towers + upgrade paths + Tree of Life abilities — `CODE-only` (data + behaviors) — the meat of "tower defense"; pairs with DEF-171. Scope to 2-3 towers for the demo. HIGH ROI, medium effort.
3. **DEF-161** [High] — WO-164 Zone Foundation (depth read + ThreatLevel + zone records) — `CODE-only` — **keystone**: unblocks DEF-169/170/166 zone work. Do early if pursuing the loop lane. Medium.
4. **DEF-250** [Medium] — 6 enemy archetypes (visual/behavioral variety) — `CODE-only` — makes waves feel like a real fight, not clones. Good demo polish; scope to 3 types. Medium.
5. **DEF-222** [High] — Tutorial + companion onboarding (guided placement, 4-gate narrative) — `CODE-only` — high value for a judge who needs guidance, but depends on dialogue + build-mode. Medium-large. After DEF-171/DEF-253.
6. **DEF-200** [High] — WO-231 Party assembly redesign + early resource loop fix — `CODE-only` — touches the front of the loop; medium. After core loop pieces.
7. **DEF-209** [High] — Defensive storyflow (tie unlocks/towers/companion into an arc) — `CODE-only` — design glue; LOW immediate demo ROI (systems exist, narrative connective tissue). Defer past playable.
8. **DEF-251** [High] — Integrate Yarn Spinner for all dialogue — `CODE-only` — **infra investment**, not demo-visible. High effort, risky mid-firefight (new dependency). **Recommend defer** until after playable. Flag.
9. **DEF-207** [High] — Pet acquisition "Echoes" system — `CODE-only` (design+impl) — out of demo scope (MEMORY: not-an-MMO, hold the line). Defer.
10. **DEF-169** [Medium] — WO-155 Region enemy spawning tables — `CODE-only` — depends on DEF-161. Defer with zone lane.
11. **DEF-170** [Medium] — WO-160 Wandering tribes (roaming groups) — `CODE-only` — depends on DEF-161. Defer.
12. **DEF-248** [Medium] — Zone/Outpost Manager architecture (additive scenes, return portals) — `CODE-only` — large architecture; overlaps DEF-166/161. Scope-risk. Defer the full build.
13. **DEF-249** [Medium] — Consumables & camping system — `CODE-only` — out of demo scope. Defer (not-an-MMO).
14. **DEF-119** [Medium] — WO-227 Opening cutscene + story companion — `CODE-only` — overlaps DEF-222/252; likely dedupe. Defer / merge.

---

## Lane: UI/HUD

1. **DEF-253** [Urgent] — BLOCKER intro/song screen can't advance — `CODE-only` — **#1 overall.** *(Top-8 #1)*
2. **DEF-171** [High] — Build mode click-to-place + green/red grid preview — `CODE-only` — interactive hook. *(Top-8 #7)*
3. **DEF-252** [High] — Intro cinematic/sequence missing (music plays, no visual) — `CODE-only` — first impression for the judge. Medium. Do after DEF-253 (same screen); may share work. Possibly partial overlap with DEF-119.
4. **DEF-181** [Medium] — Hero cards: Archer/Knight/Mage/Cleric on select screen — `CODE-only` — polish on the select screen the judge hits immediately. Low-medium effort, decent visible ROI.
5. **DEF-179** [High] — WO-222 Tutorial redesign (hero combat first, free tower placement) — `CODE-only` — depends on DEF-171/build mode; design-heavy. Medium. After build mode.
6. **DEF-182** [Medium] — Resource gathering tutorial + harvest visual feedback — `CODE-only` — supports the harvest half of the loop; medium. After core loop visible.
7. **DEF-194** [Medium] — Camera systems (dual cam + defend-tower + over-walls) — `CODE-only` — MEMORY notes the close 3rd-person cam is already owner-validated; much of this may be **stale/partially done**. Verify before pulling — likely down-scope. Flag.

---

## Lane: VFX/Audio (CODE-only, no gameplay deps — safe parallel)

1. **DEF-183** [Medium] — Audio full pass "sound everything" — `CODE-only` — cheap, broadly felt polish (AudioService exists). Good filler for an idle agent; scope to combat + UI SFX. Medium.
2. **DEF-184** [Low] — Music themes (ATB + overworld) + player jukebox — `CODE-only` — nice-to-have; jukebox is out of demo scope. Do the 1-2 theme hookups only, defer jukebox. Low.

---

## Lane: Monetization/Backend

1. **DEF-246** [Urgent] — WebGL catalog loading (UnityWebRequest/Resources fallback) — `CODE-only` — **#2 overall**; despite the lane name this is a live-build BLOCKER, not monetization. *(Top-8 #2)*

---

## Lane: Legacy/Misc (pipeline — mostly defer)

1. **DEF-162** [High] — WO-201 Catalog data model + defensive catalog — `CODE-only` — engine foundation; unblocks DEF-192 (factory/persistence). MEMORY suggests catalog/factory work largely landed — **verify status, likely close-candidate or down-scope.** Flag.
2. **DEF-186** [Medium] — WO-237 Building upgrade panel (Lumbermill) — `CODE-only` — supports upgrade half of loop; medium. After core loop.
3. **DEF-192** [Medium] — WO-148/149 Catalog factory + catalog-driven persistence — `CODE-only` — MEMORY: this architecture is described as largely built. **Verify — close-candidate.** Flag.
4. **DEF-196** [Medium] — WO-211 WebGL build full optimization — `CODE-only` — do AFTER DEF-246 (correctness before perf); MEMORY says perf pass is owner-deferred. Defer.
5. **DEF-201** [Low] — WO-273/274/275 zone arch + project/silo restructuring — `CODE-only` — MEMORY: silo restructure explicitly SKIPPED by owner+CLI (cosmetic, landmine risk). **Recommend Cancel/Won't-do.** Flag.

---

## CLOSE CANDIDATES (verify first — NOT closed this pass)

These look done/moot/superseded from MEMORY + today's commits, but need a quick
verify before closing (flagged, not executed):

- **DEF-192** (WO-148/149 catalog factory + persistence) — MEMORY "catalog→factory→persistence architecture" describes StructureFactory.Create one-path + recipe persistence as built. Verify the acceptance criteria, likely Done.
- **DEF-162** (WO-201 catalog data model) — MEMORY "catalog thesis validated live" says Part A compiled green and registry work landed. Verify; likely Done or mostly-done.
- **DEF-194** (camera systems) — MEMORY "open-world camera = close 3D third-person" is owner-validated as shipped; the high-cam premise of this ticket is stale. Verify which sub-items remain; likely down-scope or close.
- **DEF-201** (silo/project restructuring WO-275) — MEMORY "village geometry hand-author session" records owner+CLI explicitly SKIPPED the silo restructure as cosmetic. Recommend **Cancel**, not Done.
- **DEF-242** (P0 full village rebuild) — Superseded by the Village2 factory that shipped today (Village2 is the new live village). Recommend down-scope to "polish Village2 output"; the full modular teardown is likely moot. Owner call.
- **DEF-119 / DEF-252 / DEF-222** — overlapping intro/cutscene/companion-onboarding tickets. Likely a dedupe (keep DEF-253 + one onboarding ticket). Verify and merge before pulling all three.

---

## Closed this pass (2026-06-04)

| Issue | Reason | Citing |
|---|---|---|
| **DEF-247** | EventSystem + AudioListener added to Village2 | commit 78af7e1 (system manifest: Main Camera + AudioListener + EventSystem) |
| **DEF-256** | WaveManager + 4 spawn points wired 12m outside gates; navmesh baked 4/4 gates | f617ccf, 78af7e1 |
| **DEF-254** | Village2 wood rebuild — wall seating fixed, gate touches wall, water plane removed | 9802db5, f87eba8, 78af7e1 |
| **DEF-245** | Castle-wall experiment REVERTED to wood — off-axis segments no longer exist | f87eba8 ("keep wood walls") |
