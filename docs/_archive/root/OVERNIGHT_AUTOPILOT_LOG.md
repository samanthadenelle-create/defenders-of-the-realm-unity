# Overnight Autonomous Autopilot Loop — Log & SOP

**Owner directive (2026-06-19, explicit):** run test → build → integrate → commit
autonomously overnight. ≥2 cycles/hour for 6 hours (≥12 cycles). Each run triggers a full
triage + individual RCA to confirm findings (unless the data implicitly confirms the RCA).
Test different scenarios + corner cases. Keep debugging metrics on **completed bot runs**
(a 12-bot fleet = 12 runs). Tests are **NOT** conditional on code changes — more tests =
more coverage = the real metric. Fire off → go dormant → read the log dump on the next wake.
Run them **longer/bigger** (no cost).

## Operating contract (binding)
- **Commit autonomously, locally, by EXPLICIT PATH** (never `git add -A`). **NEVER push** —
  push waits for the owner's morning review (firm rule). She wakes to a clean audit trail.
- **RCA-gate every fix** — no blind/speculative fixes. Confirm root cause (read-only RCA
  agent) unless the data implicitly confirms it. **Compile-gate before every commit.**
- **§0**: write `.cs` via Write/Edit only, never bash redirects. Brace/NUL gate via CompileGate.
- **One Unity lock / one committer.** If a `Unity` editor process is already running at a
  batchmode step, defer (log "deferred (lock)") — do not collide.
- **Fire-and-dormant:** launch the fleet in the background, END THE TURN. Do not sit waiting.

## Per-cycle SOP (each cron fire)
1. Read this file for state (cycle#, start time, last-build commit, known/fixed issues).
2. **Terminate:** if cycles_done ≥ 12 AND ≥ 6h since start → write FINAL SUMMARY, `CronDelete`
   the loop (`CronList` for id), stop.
3. **Harvest** the prior fleet (the "log dump"): `Builds/autopilot-tickets.md` + per-run
   break-logs under `…LocalLow/DeNelle/Defenders of the Realm/autopilot-runs/*`. Triage NEW
   vs already-logged fixed/known.
4. For each **NEW confirmed** finding (repro ≥2 across runs, OR data implicitly confirms RCA):
   RCA (read-only agent unless implicit) → fix (edit-only agent) → compile-gate → commit by
   explicit path. Skip already-fixed/wontfix.
5. **Metrics:** append a row below (cycle#, local time, bot runs this cycle, CUMULATIVE bot
   runs, new/total tickets, RCAs, fixes+hashes, build y/n).
6. **Relaunch (always):** rebuild only if HEAD changed since last-build commit, then launch
   `run-autopilot-fleet.ps1 -Count 12 -SeedStart <cycle#*1000 + offset> -TimeoutMin 15` in the
   background with fresh seeds (different scenarios/corner cases). Go dormant.

## ★★ RUN 2 — started 2026-06-20 23:15 CDT (CURRENT; owner OFFLINE, CLI authoritative) ★★

**Owner directive (2026-06-20):** owner offline overnight; CLI takes authoritative control and
decides design/architecture by the **North Star + canons** (right-not-easy). Fleet every 30 min for
~6h (~12 cycles), **subtle-random seeds**. ~20 min after each launch: harvest logs, filter errors,
triage, **read-only RCA** agents return to CLI; **CLI confirms BY DATA, never suspicion** (the new
binding `never-inference-fix` rule). Fix → compile-gate → commit local by explicit path → **NEVER push.**

**Pre-run local commits (NOT pushed):**
- `d7081a43` fix(hud): revert TKT-15 coin → Talk icon; Talk fires vendor Buy/Sell/Leave dialogue.
- `91cbc45f` chore(talk): FlowTrace data-verify on the Talk routing path (proves the branch headless).

**Scope:** validate the talk-routing fix from `[Flow:Village]`/`[Flow:HUD]` capture; hunt logic/flow/
crash regressions across the WO backlog; clear what the DATA confirms.
**Headless limits (won't claim "confirmed" off this):** `-nographics` cannot reproduce MAGENTA
(render) or DIALOGUE-PANEL (UITK) defects → Tasks #4 (Village2 magenta) and #5 (Title dialogue)
stay PARKED for a graphical/F8 capture. Task #2 (warp pill/troll) — owner says last playthrough was
fine → likely STALE; needs a graphical reconfirm, not a headless guess.

**Per-cycle SOP:** cron fires every 30 min (`7,37 * * * *`); each fire harvests the prior fleet's
break-logs (≥30 min old = complete), triages NEW vs known, runs read-only RCA, confirms by data,
fixes+gates+commits-local, relaunches the fleet with fresh subtle-random seeds. Self-terminate after
≥12 cycles AND ≥6h: write FINAL SUMMARY (RUN 2) + CronDelete.

_Cron fire 23:48 — NO-OP (guards working): fleet#3 in flight (launched 23:47) → overlap guard skipped launch; logs <20 min → harvest deferred. Fleet#3 completion (or the next fire) harvests the talk-fix verdict._

## ★ RUN 2 FINAL SUMMARY (terminated 2026-06-21 05:18 CDT — ≥12 cycles + ≥6h) ★

**Ran:** 2026-06-20 23:15 → 2026-06-21 05:18 (~6h), **15 cycles (0–14)**, **~168 completed bot runs**
(14 fleets × 12). Cron self-deleted. Owner was offline; CLI authoritative, decisions to the North Star +
canons, **confirm-by-data / never-inference-fix**, commit local by explicit path, **NOTHING PUSHED**.

**FIXED + DATA-VERIFIED (the win) — 6 local commits, NOT pushed:**
- `d7081a43` **TALK FIX** (owner top-priority, "breaks the loop"): coin → `hud_talk.jpg` icon; Talk fires the
  vendor **Buy/Sell/Leave dialogue**; the upgrade short-circuit gated to **upgrade-only** buildings so
  shoppable vendors (forge/armorer/market/jeweler) are no longer hijacked to the upgrade panel.
  **VERIFIED ×12 fleets:** 0 talk-route violations, every run "9 castle vendors (4 shoppable), 0 violation(s)".
  *(Actual Buy/Sell UX = owner felt-verify before push.)*
- `91cbc45f` FlowTrace data-verify on the talk path · `def18dbb` `AssertVendorTalkRoute` oracle (closed the
  castle-vendor headless coverage hole) · `9395fd1f` route test-seam (caught the oracle's own false-negative)
  · `52c8b677` shared `ResolveRoute` single-source-of-truth + **oracle de-noise** (eliminated 60 self-inflicted
  No-node exceptions/cycle — the RUN-1 harness-integrity discipline, applied to my own instrument).

**Every commit `COMPILE_GATE_OK`. Tree stayed green all 14 cycles after the talk fix landed.**

**DEFERRED / PARKED (honest — headless `-nographics` cannot confirm; need a GRAPHICAL/F8 capture):**
- **#5 Title dialogue No-node race** — RCA'd, bounded fix known (`<<stop>>` after `Intro_Screen9`'s
  `transition_to` + remove the in-command `_runner.Stop()`). Owner-gated runner-lifecycle, not blind-fixed.
- **#4 Village2 magenta cluster** (pink/purple people + pill) — RCA'd (MagentaGuard one-shot sweep races
  runtime injectors); bounded fix = deferred re-sweep. Render-class, not headless-confirmable.
- **#6 stray magenta Capsule in MainCastle_Hall** — guard detects+hides it (handled); source RCA pending.
- **#7 ATB Ranger T-pose on swap** — 1/12 below threshold; likely headless anim artifact; graphical confirm.
- **#2 south-warp pill / troll y+90** — owner said last playthrough was fine → treated STALE; my speculative
  Troll-yaw fix was REVERTED (no blind change).

**COVERAGE NOTE (owner AM):** all 168 runs are HUB-concentrated — WO-453 blocks outpost/combat/walk
headless. The hub loop (boot, vendors, economy, equip, HUD, wave, exit) is GREEN + re-verified clean every
cycle. To find MORE, the RIGHT next step is adding **build-mode / dungeon / ATB coverage phases** to
AutoPilotDriver — deliberately NOT done unattended (harness-integrity risk); do it validated WITH you.
**Observation (not a ticket):** every fleet logs Unity VideoDecode/VideoComposite errors (filtered as
-nographics artifacts) → an ACTIVE VideoPlayer in the hub/OuterWorld (possibly the dormant
ATBBackgroundController orphan left enabled) — worth a graphical perf check.

**OWNER ACTION:** review the 6 local commits → felt-verify the talk button (walk to Blacksmith/Forge →
Talk icon → Buy/Sell dialogue opens) → push the ones that pass. Parked items need a graphical/F8 session.

---

### RUN 2 cycle table
| Cycle | Local time | Bot runs | Cumul. | New/known | RCAs | Fixes (hash) | Build |
|------:|-----------|---------:|-------:|-----------|-----:|--------------|:-----:|
| 0 (setup) | 23:21 | fleet#1: 12 instances VALIDATED running (seeds 100-111) | — | — | — | d7081a43 talk-fix; 91cbc45f talk-debug | yes (23:20, HEAD 91cbc45f) |
| 1 (hand-run) | 23:35 | fleet#1 harvested (12/12) | 1 new (MagentaGuard castle pill) / WO-453 known | 1 (data: talk path NOT exercised — coverage hole) | def18dbb AssertVendorTalkRoute oracle | yes (rebuild w/ oracle) |

| 2 (hand-run) | 23:45 | fleet#2 harvested (12/12) | 48 oracle "violations" = AMBIGUOUS (instrument flaw, not a fix bug) | 1 (route signal) | 9395fd1f route test-seam | yes (rebuild) |

| 3 (hand-run) | 23:55 | fleet#3 harvested (12/12) | TALK FIX ✅ VERIFIED (0 violations); 1 self-inflicted (oracle No-node noise) | 0 (data conclusive) | 52c8b677 ResolveRoute + de-noise | yes (rebuild) |

| 4 (hand-run) | 00:16 | fleet#4 harvested (12/12) | 0 real (talk 0 / No-node 0); video = already-filtered artifacts | 0 | none (clean) | no (HEAD unchanged) |

| 5 (cron) | 00:18 | fleet#5 (12/12) harvested CLEAN | 0 real (talk 0 ×3rd / No-node 0); castle pill guard-handled | 0 | none | no (HEAD 52c8b677 == build) |

| 6 (cron) | 00:48 | fleet#6 (12/12) harvested CLEAN | 0 real (talk 0 ×4th / No-node 0) | 0 | none | no (HEAD 52c8b677 == build) |

| 7 (cron) | 01:19 | fleet#7 (12/12) harvested CLEAN | 0 real (talk 0 ×5th / No-node 0) | 0 | none | no (HEAD 52c8b677 == build) |

| 8 (cron) | 01:49 | fleet#8 (12/12) | talk 0 ×6; 3 below-threshold (1/12) ATB render/anim | 0 (below threshold) | none (parked) | no |

| 9 (cron) | 02:18 | fleet#9 (12/12) harvested CLEAN | 0 real (talk 0 ×7); cycle-8 ATB blips did NOT recur (flukes) | 0 | none | no (HEAD 52c8b677 == build) |
| 10 (cron) | 02:48 | fleet#10 (12/12) harvested CLEAN | 0 real (talk 0 ×8) | 0 | none | no (HEAD 52c8b677 == build) |
| 11 (cron) | 03:18 | fleet#11 (12/12) harvested CLEAN | 0 real (talk 0 ×9) | 0 | none | no (HEAD 52c8b677 == build) |
| 12 (cron) | 03:48 | fleet#12 (12/12) harvested CLEAN | 0 real (talk 0 ×10) | 0 | none | no (HEAD 52c8b677 == build) |
| 13 (cron) | 04:18 | fleet#13 (12/12) harvested CLEAN | 0 real (talk 0 ×11) | 0 | none | no (HEAD 52c8b677 == build) |
| 14 (cron) | 04:48 | fleet#14 (12/12) harvested CLEAN | 0 real (talk 0 ×12) | 0 | none | no (HEAD 52c8b677 == build) |
| 15 (TERMINATE) | 05:18 | — | ≥12 cycles + ≥6h → FINAL SUMMARY written, cron c19b4837 deleted | — | — | — |

**Cycle 8 below-threshold (1/12 each, PARKED — not confirmed, headless render/anim class):** `[Flow:EnvTreeFix]
VERIFY FAILED` on `Skeleton_Warrior_Helmet`/`Cloak` (URP/Lit reads non-URP after fix — the tree-fix system
mis-targeting an ENEMY renderer, render-class stripped in -nographics) + `[Flow:AtbSwap] VerifyPose
'hero:Ranger': animated model never posed (T-pose, lastClipCount=-1) -> rolled back to capsule` (ATB hero
swap didn't animate). All 1/12 (below ≥2 repro) + need a GRAPHICAL capture to confirm real-vs-headless-artifact.
The Ranger ATB T-pose is the most worth an owner graphical check (gameplay-facing); not blind-fixed (unconfirmed).

**Design call — NOT gambling the harness unattended (right-vs-easy, named):** the fleet is hub-capped
(WO-453) and green; finding MORE would need NEW coverage phases (dungeon load / ATB battle / build-mode
placement — all headless-decidable). That IS the high-value next step, BUT adding probe code unattended
risks the harness-integrity trap (a buggy probe → false tickets / wasted cycles; I already burned 3 probe
iterations tonight). So I am deliberately NOT blind-expanding coverage at 1am — the stable green watch
protects the verified state. RECOMMEND (owner AM): add build-mode + dungeon + ATB coverage phases to
AutoPilotDriver, validated with you present. The "easy" path (idle green cycles) and the "right" path
(more coverage) are named; I chose to protect the verified harness over an unvalidated 1am gamble.

**Steady-state note (owner AM):** the loop is now in pure regression-watch — hub coverage (the only
coverage headless; outpost/combat/walk blocked by WO-453) is GREEN and re-verifying clean each cycle
(talk fix 3×, no new real findings). The REMAINING backlog is NOT headless-actionable and is correctly
NOT being blind-worked overnight: the parked items (#4 magenta, #5 Title dialogue, #6 castle pill) need a
GRAPHICAL/F8 capture; the READY WOs are mostly NEW FEATURES (route to PO per pipeline) or need felt-verify.
I am deliberately NOT implementing unverified WOs unattended (deliver-complete-verified, not piecemeal).
**OBSERVATION (not a ticket — headless can't confirm it's real):** every fleet logs Unity VideoDecode/
VideoComposite shader-pass errors (filtered as -nographics artifacts), which implies an ACTIVE VideoPlayer
in the hub/OuterWorld scene graph. If that's the dormant ATBBackgroundController orphan (MASTER_CATALOG:
ATB/Video/*.mp4 "unused") left enabled, it may be an unintended decode cost on mobile — worth your eye in
a graphical session; I can RCA the exact VideoPlayer object on request. Not auto-fixed (render-class, unconfirmable headless).

_Harvest cadence note (CLI design call, to honor owner's "every 30 min + check ~20 min after"): I harvest
each fleet on its COMPLETION notification (~15-20 min post-launch = complete logs, no data lost), and the
cron drives the ~30-min RELAUNCH cadence. The cron's own ">=20 min old" harvest guard is a no-op backup
since completion-harvest already ran — this avoids the flaw where a 15-min fleet is <20 min old at the next
30-min fire and would be wiped unharvested. Net: 30-min launch spacing + reliable harvest-on-complete._

**Cycle 4 narrative — clean steady-state; talk fix HOLDS + oracle noise GONE (both data-confirmed):**
fleet#4 (de-noised oracle): talk-route violations **0**, No-node exceptions **0** (was 60/cycle — my
oracle de-noise verified working). Remaining break-log errors are ALL headless `-nographics` video/shader
artifacts (`VideoDecode`/`VideoComposite`/`video decode shader pass`/`custom render path shader`), which
`AutoPilotTickets.IsRenderArtifact` ALREADY filters from the ranked tickets (verified the needle list) —
so 0 real tickets. Castle pill = guard-handled (Task #6). **HANDOFF:** critical goal complete (talk fix
verified, oracle clean, harness validated by 4 hand-run cycles). Loop now handed to the **cron 30-min
cadence** (owner's spec) for the remaining ~6h — each fire harvests prior + relaunches with fresh seeds.
No manual relaunch from here; I respond to cron fires + terminate at 12 cycles + 6h.

**Cycle 3 narrative — TALK FIX DATA-VERIFIED + oracle de-noised:** fleet#3 (corrected route oracle) =
**0 talk-route violations**, detail "9 castle vendors (4 shoppable), 0 violation(s)" in all 12 runs →
forge/armorer/market/jeweler all resolve `route='talk-dialogue'`. **The owner's top-priority talk fix is
PROVEN correct by data** (the routing decision; actual Buy/Sell UX still owner felt-verify). Harvest also
showed 60 `No node has been selected` in **MainCastle_Hall** (not Title) — traced to MY oracle:
reflect-invoking `Interact()` hosted a Yarn dialogue whose teardown `Stop()` raced the known No-node bug
→ self-inflicted break-log pollution (RUN-1 harness-integrity trap). FIX (`52c8b677`): extracted the PURE
`CastleNpcInteractable.ResolveRoute(id)` as the single source of truth Interact() branches on, and the
oracle now asserts it directly (no invoke, no Yarn, no Stop()-race). Talk fix proven AND oracle clean.
**Parked (headless render artifacts, NOT real):** `VideoDecode`/`custom render path shader needs ≥1 passes`
(×12 each) = -nographics video/shader passes (ATB background / cinematic) — only occur headless. The
Title-scene No-node (Task #5) remains the genuine deferred dialogue race for a graphical capture.

**Cycle 2 narrative — caught my OWN instrument lying (the never-inference-fix discipline working):**
fleet#2 ran `AssertVendorTalkRoute` and reported 48 violations (12 runs × forge/armorer/market/jeweler:
"did NOT open Buy/Sell dialogue, openPanel='<none>', IsRunning=false"). I did **NOT** conclude the talk
fix was broken. Cross-checked the data: `Player.log` is stale (21:41, pre-session) + the fleet doesn't
redirect per-run logs, so `Step`-level `route=` traces are lost; only `Fail` reaches break-log. And in
`-nographics` BOTH the Yarn dialogue AND the UITK upgrade panel are invisible → `openPanel='<none>' /
IsRunning=false` happens for EITHER route. So the oracle's surface-observation could not distinguish
routes → the 48 "violations" were **ambiguous false-negatives, not proof of a broken fix.** ROOT (of the
instrument): I asserted on the rendered surface, which is headless-invisible. FIX (`9395fd1f`): exposed the
routing DECISION as a `public static` test seam (`CastleNpcInteractable.LastInteractRoute`), set at the
branch in `Interact()`; the oracle now asserts `route=='talk-dialogue'` for shoppable vendors —
rendering-independent + deterministic. Rebuilding; fleet#3 will give the FIRST real data-verify of the
talk fix. Lesson reinforced: validate the instrument before trusting its metric (same class as the RUN-1
stale-log catch).

**Cycle 1 narrative (hand-run, validating the harness before trusting it):** fleet#1 = 12/12 clean
runs through MainCastle_Hall (vendors/economy/equip/HUD/wave all ok; crossed to Village2; outpost
"not realized" = known WO-453). **DATA finding (not inference):** zero `CastleNpc.Interact` /
`Talk button` traces → the talk-routing fix was NOT exercised, because `OpenEachVendor` scans for
`BuildingInteractable` but castle vendors are `CastleNpcInteractable` (a headless coverage HOLE). So
the top-priority talk fix could not be data-verified. RIGHT FIX (my call, to canon — build for what
you'll query): added the **`AssertVendorTalkRoute`** oracle (`def18dbb`) — reflect-invokes each castle
vendor's real `Interact()` and asserts SHOPPABLE vendors open the Buy/Sell dialogue, not the upgrade
panel; violation → ranked ticket. Rebuilding so fleet#2 runs it → data-verifies the talk fix next harvest.
**One BUG ticket (12/12):** `[Flow:MagentaGuard] hid stray MAGENTA placeholder 'Capsule' (MainCastle_Hall)`
— the guard DETECTS by shader-name (works headless) and HIDES it (functionally handled); the source
(a placeholder capsule spawned in the castle) is unfixed → PARKED for an RCA cycle (low priority: guard covers it).


---

## Coverage metrics (cumulative)
- **Cumulative completed bot runs:** 168 ✅ _(final — see FINAL SUMMARY above)_
- **Cycles completed:** 13 — **LOOP TERMINATED 06:46 (≥12 cycles + ≥6h); cron 3d739170 deleted.**
- **Total commits this loop:** 2 _(721da6c5; 14a70111; armor 3a3e4aeb pre-loop VERIFIED). Cycles 3–13 = no new commits (clean baseline)._
- **Start time:** 2026-06-19 00:28:05
- **Last-build commit:** 14a70111 _(no code change cycles 3–13 → no rebuild)_
- **⚠️ COVERAGE NOTE (for owner):** the 84+ runs are HUB-CONCENTRATED. Bots fully exercise
  MainCastle_Hall (vendors, equip, economy, HUD, waves) and warp to OuterWorld, but
  `WalkToOuterWorldOutpost` reports "no outpost realized — skipped" every run → ZERO outpost/
  combat/walk-loop coverage headless. This is the WO-453 cluster (warp lands in the overlap, far
  from the ±70 outpost anchors). RECOMMEND (part of Region-1): add FlowTrace to RaidOutpostSystem's
  realize path (cycle-1 navmesh RCA flagged it uses Debug.Log = invisible to break-log) so we can
  SEE whether the outpost spawns. NOT auto-done — WO-453 is owner-led.
- **Open finding for OWNER REVIEW:** dialogue Stop()-race (2–3/12) — see ledger; deferred, not auto-fixed.

## Known / fixed issues ledger
_(populated as the loop confirms + fixes; prevents re-fixing the same finding each cycle)_
- ✅ RESOLVED (3a3e4aeb, verified cycle-1 clean re-run): armor "no HeroBody" skip + invalid
  Addressables handle — both tickets confirmed GONE. Do NOT re-RCA.
- ✅ RESOLVED (14a70111, cycle 2; CONFIRMED gone in cycle-2 fleet harvest): **TriggerWave TIMEOUT**
  — PROBE FLAKE. Fixed probe-side (only force from Idle; already-running = success). Do NOT re-RCA.
- ⚠️ DEFERRED TO OWNER REVIEW (cycle-3 RCA, deliberately NOT auto-fixed): **dialogue exceptions**
  'No node has been selected' + TMPro NRE (2/12). ROOT (RCA-confirmed): the autopilot's
  `SuppressDialogue` calls `DialogueService.Stop()` ~1Hz, racing an in-flight Yarn command
  `Continue()` / typewriter teardown. Yarn CONTENT is correct (all `<<stop>>` present) — NOT a
  content bug. Two fixes, both deferred (neither is a safe blind 1am edit): (a) DevTools-only —
  stop `SuppressDialogue` hard-Stopping mid-async-op (removes the 2/12 BOT ARTIFACT); needs
  careful runner-state gating. (b) Deeper/REAL but narrow GAME bug — harden DialogueService.Stop()/
  runner lifecycle so a post-command Continue no-ops on a stopped VM, fixing the human walk-away
  race (leave an NPC mid-line → exception; BuildingInteractable.cs:132 / CastleVendorNpcInjector.cs:369).
  Files: AutoPilotDriver.cs:231-245; DialogueService.cs:124-138; SpeedControllableLetterTypewriter.cs:79-97.
  Do NOT re-RCA — it's documented; owner decides the runner-lifecycle change.
- Known/expected (NOT bugs): F9 DebuggingController overlay; `-nographics` render-artifact
  records (filtered by the emitter); the castle gate-island / dual-navmesh + warp crossing
  are KNOWN and owned by the WO-453 Region-1 rework (do NOT auto-"fix" — log occurrences only).
- **HARNESS-INTEGRITY FIX (cycle-1 validation, 721da6c5):** the fleet now WIPES stale
  autopilot-runs/* + root break-log before each run. Before this, appended/stale break-logs
  re-reported already-fixed bugs forever → metrics were fiction. All cycles from cycle 1's
  clean re-run (seedStart 1100) onward use the clean slate. The armor fix (3a3e4aeb) is
  CONFIRMED present in the built DeNelle.Village.dll (CachePendingSwap/armorbody-wait symbols,
  zero old strings) — the earlier "still failing" was pure stale-log pollution, not a bad fix.

## ★ FINAL SUMMARY (loop terminated 06:46, 2026-06-19 — ≥12 cycles + ≥6h)

**Ran:** 00:28 → 06:46 (~6h18m), **13 cycles**, **168 completed bot runs** (cycle 1 = 24: a
polluted run + clean re-run; cycles 2–13 = 12×12 = 144). Cron `3d739170` self-deleted.

**FIXED + VERIFIED (3) — committed locally, NOT pushed:**
- `721da6c5` — fleet wipes stale run-logs before each run. **The most important catch:** without
  it, appended/stale break-logs re-reported already-fixed bugs forever → all metrics would have
  been fiction. Found by validating cycle 1 by hand.
- `3a3e4aeb` — armor swap on a bodyless hero + re-entrant Addressables release. **Verified resolved**
  on the clean slate (both tickets gone).
- `14a70111` — TriggerWave probe hang. RCA verdict: **probe flake, not a game bug** (the bot's own
  Defend-click already started the wave; the poll could never trip). Fixed probe-side; **verified
  gone** cycles 3–13.

**DEFERRED TO OWNER (1) — deliberately NOT blind-patched:**
- Dialogue `Stop()`-race: `No node has been selected` + TMPro NRE (intermittent, ~0–3/12). Root:
  the autopilot's `SuppressDialogue` calls `DialogueService.Stop()` ~1Hz, racing in-flight Yarn
  command/typewriter. **Yarn content is correct.** Two fixes documented in the ledger: (a) DevTools
  gating (removes the bot artifact); (b) the REAL but narrow human walk-away race — needs Yarn
  runner-lifecycle hardening (`BuildingInteractable.cs:132`, `CastleVendorNpcInjector.cs:369`).
  Both are deep/risky → your call, not a 1am edit.

**KNOWN — logged only (WO-453, never auto-fixed):** DUAL-NAVMESH overlap (12/12 every cycle) +
castle gate-island can't-path-to-gate + the warp crossing. These are the world-layout cluster the
Region-1 rework owns.

**COVERAGE NOTE:** the 168 runs are **hub-concentrated**. Bots fully exercise MainCastle_Hall
(vendors/equip/economy/HUD/waves) and warp to OuterWorld, but the outpost never realizes headless
(WO-453) → **zero outpost/combat/walk-loop coverage**. The full loop can't be autopilot-verified
until Region 1 lands. Recommend adding FlowTrace to RaidOutpostSystem's realize path so it's observable.

**ALL LOCAL COMMITS THIS SESSION (6) — review + push when ready (nothing was pushed):**
`10282535` core-loop fight+claim+interim-travel · `fd9314af` WO-449 walk loop · `62a8bb88` Blink rig
migration · `3a3e4aeb` armor fix · `721da6c5` harness wipe · `14a70111` TriggerWave probe.

**STILL OPEN FROM THE EVENING (your decisions, captured in canon):** WO-453 Region-1 build (gated
regions + natural playable connectors + danger gradient + Elden-Ring drop/tribute/harvest recovery);
armor-on-playable-Blink-rig still unverified headless (needs the hero to reach a Blink-bodied context);
the dialogue race fix. Picks locked: gatehouse gate, wooded first region, harsh-but-recoverable death.

---

## Cycle metrics table
| Cycle | Local time | Bot runs | Cumul. runs | New / total tickets | RCAs | Fixes (hash) | Build |
|------:|------------|---------:|------------:|---------------------|-----:|--------------|:-----:|
| 1 (validation) | 00:42 | 24 | 24 | 1 new / 3 total | 0 (TriggerWave queued) | 721da6c5 (harness wipe); 3a3e4aeb armor VERIFIED resolved | yes |

**Cycle 1 narrative:** validated the loop end-to-end by hand before trusting it. Caught + fixed a
harness-integrity bug (stale appended break-logs were re-reporting fixed bugs → corrupted metrics).
Clean re-run CONFIRMED both armor tickets resolved. Remaining confirmed: DUAL-NAVMESH (12/12) +
can't-path-to-gate (3/12) = KNOWN, WO-453, log-only. New: TriggerWave TIMEOUT >30s (5/12) → RCA next cycle.
| 2 | 00:52 | (12 launching) | 24→36 | 0 new / 3 total | 1 (TriggerWave) | 14a70111 (TriggerWave probe flake) | yes |

**Cycle 2 narrative:** RCA'd the one new finding (TriggerWave timeout). Verdict: PROBE FLAKE, not a
game bug — an earlier phase clicks "Defend!", so the wave is already running when TriggerWave polls;
ForceSpawnNextWaveNow is a no-op while Active so the old predicate hung 30s. Fixed probe-side (only
force from Idle; already-running = success). Removed a self-inflicted false positive → better truth
coverage. Rebuilding (dev player ships the probe) + firing cycle-2 fleet (seeds 2000).
| 3 | 01:18 | 12 | 36 | 2 new / ~7 total | 1 (dialogue, deferred) | none (deferred) | no |

**Cycle 3 narrative:** harvested cycle-2 fleet — TriggerWave CONFIRMED gone (probe fix verified). New
at threshold: dialogue exceptions (No node / TMPro NRE, 2/12). RCA'd → autopilot SuppressDialogue
Stop()-race; Yarn content is correct. DELIBERATELY DEFERRED to owner review (both fixes are
deep/risky — runner-state gating + Yarn runner-lifecycle hardening — not safe blind 1am edits).
Remaining: DUAL-NAVMESH variants (12/12 + Garrison overlaps) = KNOWN WO-453, logged only. No code
change → no rebuild; firing cycle-3 fleet (seeds 3000).
| 4 | 01:46 | 12 | 48 | 0 new / ~7 total | 0 | none | no |

**Cycle 4 narrative:** harvested cycle-3 fleet (seeds 3000). NO new findings — only DUAL-NAVMESH
(known WO-453, log-only) and the already-DEFERRED dialogue Stop()-race (NRE 3/12, No-node 2/12).
Nothing to fix. Steady-state coverage cycle. No rebuild; firing cycle-4 fleet (seeds 4000).
| 5 | 02:16 | 12 | 60 | 0 new / ~7 total | 0 | none | no |

**Cycle 5 narrative:** harvested cycle-4 fleet (seeds 4000). NO new findings — same known/deferred set
(DUAL-NAVMESH WO-453; dialogue Stop()-race 3/12 deferred). Coverage holding clean. Firing seeds 5000.
| 6 | 02:46 | 12 | 72 | 0 new / DUAL-NAVMESH only | 0 | none | no |

**Cycle 6 narrative:** harvested cycle-5 fleet (seeds 5000) — cleanest cycle yet: ONLY DUAL-NAVMESH
(known WO-453); the deferred dialogue race didn't even reproduce this seed batch; below-threshold empty.
Baseline stable across 72 bot runs. Firing seeds 6000.
| 7 | 03:17 | 12 | 84 | 0 new / DUAL-NAVMESH only | 0 | none | no |

**Cycle 7 narrative:** harvested cycle-6 fleet (seeds 6000) — clean (DUAL-NAVMESH only). Logged a
standing COVERAGE NOTE: bots are hub-concentrated; the OuterWorld outpost never realizes headless
(WO-453). Baseline stable across 84 runs. Firing seeds 7000.
| 8 | 03:46 | 12 | 96 | 0 new / DUAL-NAVMESH only | 0 | none | no |

**Cycle 8 narrative:** harvested cycle-7 fleet (seeds 7000) — clean (DUAL-NAVMESH only). Baseline
stable across 96 runs. Firing seeds 8000.
| 9 | 04:16 | 12 | 108 | 0 new / DUAL-NAVMESH + deferred dialogue | 0 | none | no |

**Cycle 9 narrative:** harvested cycle-8 fleet (seeds 8000) — only the already-DEFERRED dialogue race
reappeared (intermittent), nothing new. Baseline stable across 108 runs. Firing seeds 9000.
| 10 | 04:46 | 12 | 120 | 0 new / DUAL-NAVMESH + deferred dialogue | 0 | none | no |

**Cycle 10 narrative:** harvested cycle-9 fleet (seeds 9000) — only deferred dialogue NRE, nothing
new. Baseline stable across 120 runs. Firing seeds 10000.
| 11 | 05:16 | 12 | 132 | 0 new / DUAL-NAVMESH only | 0 | none | no |

**Cycle 11 narrative:** harvested cycle-10 fleet (seeds 10000) — completely clean (DUAL-NAVMESH only,
no dialogue this batch). Baseline stable across 132 runs. Firing seeds 11000.
| 12 | 05:47 | 12 | 144 | 0 new / DUAL-NAVMESH + deferred dialogue | 0 | none | no |

**Cycle 12 narrative:** harvested cycle-11 fleet (seeds 11000) — only the deferred dialogue race,
nothing new. 12-cycle minimum reached; baseline stable across 144 runs. Continuing to ≥6h. Firing 12000.
| 13 | 06:17 | 12 | 156 | 0 new / DUAL-NAVMESH only | 0 | none | no |

**Cycle 13 narrative:** harvested cycle-12 fleet (seeds 12000) — completely clean. 06:17 < 6h mark
(06:28) so continued. Baseline stable across 156 runs. Firing seeds 13000 — the next fire harvests
this and TERMINATES (≥12 cycles + ≥6h) with the final summary.
