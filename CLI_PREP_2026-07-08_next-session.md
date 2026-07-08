# ⛔ CLI PREP — 2026-07-08 (the single force-prep for the NEXT CLI session)

**Owner directive:** this ONE document preps the next CLI. Read it, then the canon boot
(SESSION_CANON_LOADER.md → docs/MASTER_CATALOG.md area sections → docs/ARCHITECTURE.md), then
answer PREFLIGHT_GATE.md before touching anything. Non-negotiables in force: §11 sole-committer
by explicit path (never `git add -A`); §12 instrument-first (cite captured data before any fix);
§14 F8 watcher armed whenever the owner plays (re-arm after every fire:
`bash .claude/skills/run-defenders/f8-watch.sh` via Bash run_in_background); one build per wave,
handoffs name the exe timestamp; owner is red/green colorblind (never meaning by color alone).

## WHERE WE ARE (branch `wip/village2-and-f8-tickets`, ALL LOCAL — push HELD for owner word)

- **Overnight program COMPLETE + owner felt-confirmed the P0:** tutorial completes, placement
  works. 8 real-path probes all PASS 4/4 in the fleet on exe 2026-07-07 23:50:04. Ledger:
  `RESUME_2026-07-08_overnight-f8-sweep.md`. Vercel PREVIEW (pre-morning fixes):
  https://defenders-of-the-realm-v2-2dizrqgws.vercel.app — prod untouched.
- **Morning felt session produced F8-31..F8-38** (board tasks; each carries evidence + RCA
  metadata). Committed fix lanes this morning: F8-6 tree pose (e6498b5d), F8-24 stairs sweep
  (a9828b40), F8-15 death forensics (242a352a), F8-22/32 dialogue arb + Sylas headshot (926309ca),
  WO-614 (3a4426b6).

## ⭐ WAVE 2 CLOSED (05:10) — FLEET = ZERO TICKETS
All morning lanes committed through `bb0094cc`; final fleet on exe **2026-07-08 05:10:11** =
**0 tickets, all probes PASS** (compass full PASS buffer=1 pips=1 rect=10x23px). The stairs sweep
EXECUTED (`[StairsSweep] removed 4 stair object(s)`) + navmesh rebaked + committed (13e85e12).
Wave-2 late finds, fixed with data: ArcaneTower fake-null NRE (`?.` misses Unity fake-null),
popup oracle taught the tap-advance/Choose contract, compass ForceProviderPoll dead-nudge on
inactive instances. **WebGL DEPLOYED from bb0094cc:** Vercel PREVIEW
https://defenders-of-the-realm-v2-h0h6hfsf5.vercel.app (READY; supersedes the pre-morning
2dizrqgws preview). Production untouched — promotion is the owner's. Owner felt-pass = exe 05:10:11.
The section below is retained for the process pattern; its steps are DONE except WebGL deploy.

## THE UNFINISHED WAVE — finish THIS first, in order

1. **Collect in-flight agent lanes** (if not already in tree — check `git status`):
   (a) UiKit lane: TargetNameplate/PartyNameplate prefab GUID repair + sprite-null subtree guard
   (F8-31; ALSO cover the castBar white-on-white noted on F8-37's ticket) + portrait circle-mask
   (F8-32 kit side). The two prefabs show modified in the tree — verify against the agent report
   before committing. (b) Victory-panel row layout + BR ability icons (F8-35 + F8-33).
   (c) F8-34 gray T-pose enemy RCA (read-only — route its named root to a fix).
2. **Brace+NUL sweep** every modified .cs (python one-liner, §1).
3. **Gate the combined tree:** `powershell -ExecutionPolicy Bypass -File C:\eoa\run-unity-method.ps1
   -Method DeNelle.Editor.CompileGate.Run -LogName gate.log` (check `Get-Process Unity` yourself
   first — the WebGL clone at C:\defenders-webgl-build does NOT lock C:\eoa but DOES block the
   script's name-based check). Then DataRegression.RunAll.
4. **Stairs batch (F8-24, code already committed):** run
   `DeNelle.Editor.CastleWallStairsSeatFix.RemoveAllInMergedScene` (expect census line
   `[StairsSweep] removed 4 stair object(s) ...`) then
   `DeNelle.Editor.WorldMergeBuilder.BakeMergedWorldNavmesh` (editor CLOSED). Commit the scene +
   navmesh by explicit path (binary asset — never git-restore, re-bake on corruption).
5. **Commit remaining lanes by explicit path**, then **ONE Windows build**
   (`build-windows.ps1`) + **fleet** (`run-autopilot-fleet.ps1 -Count 4 -TimeoutMin 9`) — the probe
   suite must stay green (AssertTutorialFirstTower / DialogueChain / OrientModalReleases /
   WaveVendorRules / CompassMarks / ScatterRecords / HeroHasAlbedo / TutorialArms). Hand off with
   the exe timestamp.
6. **WebGL + Vercel preview** after the fleet is green: reset the warm worktree to the LOCAL
   branch (`git -C C:\defenders-webgl-build checkout --detach; reset --hard
   wip/village2-and-f8-tickets`), robocopy the 6 gitignored art packs (list in
   build-webgl-isolated.ps1 §3), run the WebGL batchmode (fork-aware: wrapper exits early, watch
   the Unity PID), mirror `Builds/WebGL` into C:\eoa\Builds\WebGL, `npx -y vercel deploy --yes`
   from C:\eoa (= preview; NEVER --prod — promotion is the owner's).

## OPEN TICKETS AFTER THE WAVE (board = source of truth; key ones)

- **F8-37 arena pole** — giant untextured cylinder in BattleArena (evidence flag_05 + flag_02);
  RCA not yet run. BattleArena builds at ArenaCentre (5000,0,5000), BattleArena.cs:328/450.
- **F8-38 root-while-casting** — enemies walk while channeling; ruling: cast = rooted commitment
  window (cast anim, not walk blend).
- **F8-34 gray T-pose enemy** — RCA agent may have landed; read its output, fix the named step.
- **F8-15 death flow** — forensics are IN (242a352a): owner's next death produces
  [Flow:DeathTrace] lines naming every popup opener + hero mover. Fix what the lines name.
  Known already: ALL death popups funnel through EndStateView.Show BYPASSING PanelManager;
  GameOverScreen freezes timeScale.
- **F8-39 towers vanish on death, ALL return on next placement** — the saved layout survives (they
  come back) but the death/respawn path skips the visual rebuild while a placement commit runs a
  full refresh. Owner rule: "either they exist or do not" — one source of truth. Data-RCA via the
  new [Flow:DeathTrace] respawn capture + BaseLayoutLoader traces; likely fix = run the saved-layout
  rebuild on respawn (or prove the death path tears down live towers it shouldn't).
- **F8-40 max-tier tower identity (owner directive)** — fully-upgraded towers get an idle AURA vfx
  + recolored/more-damaging projectiles at max tier, and the Ballista's max tier also gains RANGE.
  Per-tier damage/range/projectileStyle fields already exist in structures-catalog.json — extend
  with a tier-scoped projectile tint/vfx key; aura = presentation layer, pooled, from the
  URP-fixed spell pack. Max-tier read must NOT be color-only (colorblind rule): shape/trail carries.
- **F8-41 waves must ATTACK the city (owner directive)** — evidence already captured: every enemy
  in the session logs '[Flow:EnemyAggro] ProbeForStructure null -> no structure target (Heart-march
  / roam only)'. Ruling: wave enemies path into the city and attack DEFENSES en route (all defense
  types already implement IDamageableStructure). RCA whether the probe is bugged (mask/radius) or
  Heart-only by design, then make waves target the nearest lane defense.
- **F8-42 repair costs (owner directive)** — damaged structures persist HP + offer a Repair
  interaction costed from catalog data ('data only always'); destroyed = full rebuild cost. Ties to
  WO-432 tech-tree + WO-612 timers.
- **Dialogue advance = 'Tap to continue ▸' passive hint** (owner 2026-07-08) — replaced the
  Continue chip in DialogueView (uncommitted at prep-time if the wave hasn't closed; check
  git status). One-action-one-button arbitration unchanged (_tapHint drives the hint).
- **F8-23/26 design pin** — wave-countdown-as-Battle posture: owner ruling still open.
- **WO-613B** outpost chunk rebuild — spec READY, owner go pending.
- **11 NPC portraits are card-framed art** — PO call after the kit circle-mask lands.
- Pre-existers (unchanged, fleet-named): WO-602 home-return unwired; CavePortal seam unreachable
  (closest 442.9m > 16m, PathPartial — bake gap); WO-453 rep spawn-gate.

## WO-614 SKILL TREE — RULED, READY TO IMPLEMENT (the big next lane)

`WorkOrders/WORK_ORDER_614_skill_tree_solo_rework.md`. Owner rulings (2026-07-08, all stamped):
1. New actives = NEW signature skills cut from the PREMIUM mocap clips (not just orphan re-wiring).
2. Bottom-RIGHT rail = SIGNATURE MOVES: one new signature per tree tier; Q locked basic;
   T1/T2/T3 → W/E/R. **TIER 1 = a RANGED attack** firing the **Thunderbolt or Arcane Blast**
   animation (cut both, wire the better-feeling, show both if close).
2a. Feel-first: the metric is the battle moment reading well.
3. **"data only always"** — 100% data-only, no code hooks, standing default for tree work.
Open detail: where the TIER 4 signature lives (capstone-as-cast placement).
Facts from the audit: 31 wired nodes = 25 KEEP / 3 CONVERT / 3 REPLACE; 6 nodes carry `ally:true`;
4 built bar abilities are ORPHANED (mending-salve, snare-arrow, suppressing-volley, shield-bash);
active density goes T1-T4 1/0/0/0 → 2 per tier. Files: hero-talents.json, abilities.json,
weaponskill-animations.json (+ dual copies where applicable).

## SESSION LESSONS ALREADY CANONIZED (memories exist — do not relearn)

- Fleet `-nographics` = NO shaders: `Material.HasProperty` reads false for everything — audits
  must read the serialized sheet (fixed in HeroBodySwapper 7e663981; pattern applies anywhere).
- `SkinOptions.LocalRotation` ASSIGNS, never composes — callers author the FULL pose including
  the FBX native up-axis correction; SeatFlat's narrowest-axis heuristic is for squat props only.
- run-unity-method.ps1 requires -LogName; Unity forks so wrappers exit early (fork-aware wait);
  background bash watchers get reaped — use the Monitor tool for long waits.
- PowerShell 5.1 mangles multi-line commit messages — write to a file, `git commit -F`.
- The editor-side fix that never reaches the SHIPPED scene is a standing failure class (F8-24):
  always verify WHICH scene/asset the player actually loads.
