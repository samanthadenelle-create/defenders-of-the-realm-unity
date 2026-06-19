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
