# WO / Linear Backlog ROI Triage — feat/tower-core-loop

_Generated 2026-06-03 · read-only analyst pass · team "Defenders of the Realm" (153 issues pulled across all states)_

Scope: classify every non-terminal issue, score VALUE items by grant-demo ROI, organize into pickable silos.
Source of truth for completion = git log on `feat/tower-core-loop`. Landmine handled: review-clone-only `ClaimableNode`/node symbols.

---

## 1. Summary Counts

Across the 153 issues, **128 are already terminal** (Done/Canceled/Duplicate) and were not re-triaged except to confirm the input "known-done" list. The triage operated on the **25 non-terminal** issues (Backlog / Todo / In Progress), plus terminal-state corrections.

| Bucket | Count | Notes |
|---|---|---|
| **DONE** (was non-terminal, now verified shipped) | 4 | DEF-153, DEF-100, DEF-94, DEF-109(substantially) — git evidence below |
| **DUPLICATE** | 1 | DEF-130 (already marked Duplicate of DEF-129) |
| **NOISE** (review-clone / stale premise) | 6 | DEF-137, 138, 139, 140, 142 (ClaimableNode — not in branch); DEF-90 (already Done, GROUP1/2 clone) |
| **VALUE** (real, applicable, not done) | 15 | scored + siloed below |
| Linear onboarding template noise | 4 | DEF-1/2/3/4 — ignore (not project work) |

Already-Done verified against git (no action, cited for the record): WO-162 (`7906cb2`,`729ceeb`), WO-243 partial (`7906cb2`), WO-247 partial / Sawmill+Armorer+gate-seam (`1165a01`), WO-250/DEF-100 (`be9ce31`), WO-253/DEF-153 (`e2519e0`), WO-249/DEF-148, WO-215 (`dd6e31d`,`d36e9cb`), WO-235 (`6bdd353`/`5adc6d5`), WO-196 -NoBrotli (`2736ad1`), plus the 13 Done DEF tickets (117/150/135/136/141/146/143/151/134/144/145/148/101).

---

## 2. The 5 Silos (ROI-ranked)

ROI = (grant-demo impact × player-facing visibility × reach) ÷ (effort × risk), 1–100.
Boost: clean code-only, high visibility, low risk, no bake. Penalize: needs bake/playtest, in-editor visual confirm, art imports, multi-week effort. Perf items capped low (owner: deferred to post-stability).

### Silo A — Clean solo-code (CLI pulls now). No bake / no visual / in-branch verified.

| WO / DEF | Title | ROI | Justification (impact vs effort/risk) | Lane |
|---|---|---|---|---|
| DEF-147 | Hero hover exploit — float above enemies | **88** | HeroLocomotion is pure-transform (no gravity); add ground-snap → kills a demo-breaking exploit. Code-only, tiny, high visibility. | Combat/AI |
| DEF-149 | NPC dialogue box overlaps/obscures HUD | **82** | Pure Canvas sortOrder fix; HUD readability is on-camera every session. Low risk, no bake. | UI/HUD |
| DEF-133 | Wave-clear XP too high ("crazy EXP") | **74** | One-number balance tune in WaveXpBridge; fixes an obviously-wrong demo feel. Trivial effort. | Combat/AI |
| DEF-132 | Hero faces wrong direction when moving (esp. left) | **71** | Body-yaw fix in locomotion; visible every step the player takes. Code-only, low risk. | Combat/AI |
| DEF-95 | Pets traveling in reverse | **58** | Pet facing/path fix; visible but pet is secondary. Code-only. | Combat/AI |
| DEF-92 | Track TowerPersistenceService CC-bonus PR coverage | **30** | Test/coverage chore; zero player visibility. Low effort but low demo value. | Monetization/Backend |

### Silo B — Bake-gated (VillageSceneBuilder edit + full bake + owner playtest).

| WO / DEF | Title | ROI | Justification | Lane |
|---|---|---|---|---|
| DEF-114 | Gate bottom edges align to ground (z-fight/float) | **55** | Visible seam at every gate; small builder tweak but needs a bake + playtest. | World/Env |
| DEF-126 | World-map lip — village↔OuterWorld terrain seam | **48** | Smooths the exit transition (high-traffic path); bake-order + terrain edit, owner-in-loop. | World/Env |
| DEF-106 | Double wall ring (BuildWallRing + BuildWallPerimeter both active) | **40** | Cleanup of overlapping geo; cosmetic, needs bake. Partially addressed by prior gate work — verify before doing. | World/Env |
| DEF-96 | Upside-down tree asset reappeared | **38** | Regen re-adds deleted props (known pattern); fix the builder so bake doesn't re-introduce it. Bake-gated. | World/Env |
| DEF-109 | Village pass (wall/gate/tower/veins/moat) — WO-177/158/167/168/157/176/179 | **35** | Mostly SHIPPED already (gate exit DEF-127, purple mat DEF-103, rampart lifts); residual = tower mesh polish + moat channel. Re-scope before pulling — large bake. | World/Env |

### Silo C — Visual / anim (needs in-editor Play-mode confirmation).

| WO / DEF | Title | ROI | Justification | Lane |
|---|---|---|---|---|
| DEF-99 | Castle doors not opening / purple door | **44** | DoorController exists; approved creative = VFX-mask + collider toggle + URP mat. Needs Play-mode confirm at 4 gates. | UI/HUD / World |

### Silo D — Assets (owner imports art/audio first).

| WO / DEF | Title | ROI | Justification | Lane |
|---|---|---|---|---|
| DEF-91 | Replace KayKit placeholder NPCs with purchased character pack | **36** | Real visual upgrade to townsfolk, but BLOCKED on owner importing Assets/Models/People. High effort downstream. | World/Env |

### Silo E — Big features (multi-step planned work).

| WO / DEF | Title | ROI | Justification | Lane |
|---|---|---|---|---|
| DEF-152 | Gate-crossing intel (Sylas line / AlertIntelSystem) | **52** | Real grant-demo polish (no blind exits); min version = a few Sylas lines (code), full version needs WO-241 AlertIntelSystem. Phase the min slice → could promote to Silo A. | UI/HUD |
| DEF-119 | Opening cutscene + story companion system (WO-227) | **34** | Strong onboarding/narrative value but multi-step; partially seeded by tutorial work. | Combat/AI |
| DEF-121 | Resource economy (Wood/Food/Iron/Crystals + Magic tech axis) WO-230 | **30** | Core loop depth; EconomyService exists (DEF-78) so this is an extension, multi-step. | Monetization/Backend |
| DEF-37 / DEF-38 | Spire Defense / "The Spire Awakens" tower-defense mode (WO-77/78) | **28** | Big standalone mode; DTT already BUILT (PatriciaLight) — verify overlap before greenfielding. Large. | Combat/AI |

_Deferred / low-priority backlog (kept for record, not siloed for "do-next"):_ DEF-112 (camera-over-walls + store polish, large/mixed), DEF-113 (overworld random encounters), DEF-115 (ATB party icons polish, Done-adjacent — see note), DEF-89 (defer BattlePassManager, explicitly deferred), DEF-84 (cherry-pick chore — likely obsolete on this branch), DEF-29/12 (Glimmer/Wisdom earn-source design — pre-grant economy, owner-deferred per scope-discipline), DEF-68/69 (campaign/monetization, post-grant), DEF-61/62/63 (world expansion, post-stability), DEF-18 (dungeon NPC placeholder).

---

## 3. Top 10 ROI — Do-Next Shortlist (cross-silo)

| # | WO / DEF | ROI | Silo | One-liner |
|---|---|---|---|---|
| 1 | DEF-147 | 88 | A | Add gravity/ground-snap to pure-transform hero → kill hover exploit |
| 2 | DEF-149 | 82 | A | Canvas sortOrder so dialogue stops covering HUD |
| 3 | DEF-133 | 74 | A | Tune WaveXpBridge — "crazy EXP" balance |
| 4 | DEF-132 | 71 | A | Fix hero body-yaw facing when moving left |
| 5 | DEF-95 | 58 | A | Fix pets traveling in reverse |
| 6 | DEF-114 | 55 | B | Align gate bottoms to ground (bake) |
| 7 | DEF-152 | 52 | E→A | Sylas gate-intel lines — ship the code-only minimum slice |
| 8 | DEF-126 | 48 | B | Smooth village↔OuterWorld terrain seam (bake) |
| 9 | DEF-99 | 44 | C | Castle door VFX-mask + collider toggle + URP mat |
| 10 | DEF-106 | 40 | B | Remove double wall-ring overlap (bake) |

Clean pickup-now path for a solo CLI session with no owner-in-loop: **DEF-147 → 149 → 133 → 132 → 95** (all Silo A, code-only, high visibility), then batch one bake round for **DEF-114 + 126 + 106 + 96** together.

---

## 4. DONE / DUPLICATE / NOISE — with Linear notation status

### DONE (moved/confirmed; comment posted citing commit)
| DEF | Evidence | Linear action |
|---|---|---|
| DEF-153 | `e2519e0` tutorial centered + lifted above HUD | comment + state→Done |
| DEF-100 | `be9ce31` portal interior glow self-bootstraps | comment + state→Done |
| DEF-94 | `be9ce31` runtime magenta/portal-color fix | comment + state→Done |
| DEF-109 | substantially shipped (DEF-127 gate exit, DEF-103 purple mat, rampart lifts); residual tower/moat polish | comment (recommend re-scope; left In Progress) |

### DUPLICATE
| DEF | Canonical | Linear action |
|---|---|---|
| DEF-130 | DEF-129 (web build navigability / on-screen controls) | already in Duplicate state — confirmed, no change |

### NOISE — review-clone-only / stale (comment with reason; NOT auto-canceled, recommend owner close)
| DEF | Reason | Linear action |
|---|---|---|
| DEF-137 | `ClaimableNode` not in feat/tower-core-loop; branch uses Camps system (`CampPromptUI` already has `Input.touchCount` mobile path) | comment, recommend close |
| DEF-138 | `ClaimableNode.ShowBuildPanel()` — file doesn't exist here ("spec in WO-239, not yet implemented") | comment, recommend close |
| DEF-139 | `ClaimableNode.DestroyNode()` — review-clone symbol, N/A here | comment, recommend close |
| DEF-140 | `RegionMobSpawner.FindObjectsOfType<ClaimableNode>` — RegionMobSpawner exists but has NO such call; ClaimableNode absent | comment, recommend close |
| DEF-142 | `ClaimableNode` repop timer — review-clone symbol, N/A here | comment, recommend close |
| DEF-90 | GROUP 1/2 clone-scoped tickets — already Done; no action | none |

_Perf-optimization note: DEF-139/140 are also perf items (FindObjectsOfType caching). Even if a future node system lands, these are owner-deferred to post-stability → keep low._

---

## 5. Methodology / verification notes

- Completion judged by `git log --oneline` on this branch, not by ticket state.
- Landmine confirmed by grep: `ClaimableNode` appears 0× as a type in `Assets/_Modules`; only match is a method `FindNearestClaimableNode()` returning `MineNode` in `Settlement.cs`. `RegionMobSpawner.cs` exists (4 refs) but contains no `FindObjectsOfType<ClaimableNode>` / `ShowBuildPanel` / `DestroyNode`. This branch's node-equivalent is the **Camps** system (`Assets/_Modules/Village/World/Camps/`).
- DEF-147 applicability confirmed: `HeroLocomotion.cs` header — "pure transform (no Rigidbody, no NavMeshAgent)" → no gravity → hover exploit is real and code-fixable.
- DEF-137 staleness confirmed: `CampPromptUI.cs` already polls `Input.touchCount > 0` alongside `KeyCode.E` → mobile path exists in the real system.
