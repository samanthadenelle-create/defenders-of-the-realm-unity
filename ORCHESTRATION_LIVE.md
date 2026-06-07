# ORCHESTRATION_LIVE — Single Source of Truth

> **Read this file FIRST. Do not re-digest the whole repo.**
> This is the living orchestration log. It tells any new session: where the build
> is, what's in flight, what's next, and what each work order's status is — without
> reading 90 design docs and 195 work orders.
>
> Keep it current: when an order closes, a bug appears, or a fix breaks something,
> add a one-line dated comment in the relevant row + the CHANGELOG at the bottom.

**Canonical design spine:** `DESIGN_CORE_LOOP_AND_STRUCTURE.md` — tutorial → outposts → food-gated garrisons → single upgradeable Heart-seat (Town Hall tiers) → boss escalation. Read it before building any world/economy/progression WO.
**Last updated:** 2026-05-31 (session: initial consolidation + core-loop design)
**Build:** GREEN ✅ — committed `00b1662` / `8e4fd35`. Windows exe + 186 MB WebGL build in `Builds/`.
**Highest WO:** 180 (note: `CLAUDE.md` still says "105" — stale, ignore that line).
**Owner north star (2026-05-31):** FUN FIRST, monetization second.

---

## 0. How this project runs (roles + discipline)

| Who | Does |
|---|---|
| **UI (Claude, this session)** | Specs, work orders, triage, routing, sequencing, validates each `.RESULT.md`. **Never edits `.cs` or runs batchmode** (mount corrupts code writes). |
| **CLI (Claude Code, Windows)** | Writes code, build-verifies in batchmode (editor closed), commits **one green check-in at a time**, performs bakes. |
| **Owner (Samantha)** | PM, creative calls, prioritization. |

> **Linear sync = CLI owns it (owner reassigned 2026-05-31).** UI does NOT update Linear anymore — avoids double-driving. UI tracks state in PIPELINE.md / ORCHESTRATION_LIVE.md instead. (UI's last Linear edits before handoff: DEF-108→Done, DEF-109→In Progress.)

**Iron rules (from CLAUDE.md):**
- `VillageSceneBuilder.cs` is **SINGLE-WRITER** — only one thread touches village/castle/gate/terrain at a time. One pass → one rebake.
- One commit per work order. **Only move forward on a green build.** Validate each story before releasing the next.
- Bakes only via CLI in batchmode, **editor must be closed**. UI never fires batchmode.
- Never hand-edit `.unity` scene files (corruption history). Rebuild via `Defenders > Week 3 > Build Village Scene`.
- Brace-balance gate every `.cs` before "done".

**UI → CLI handoff format** (every order CLI receives):
```
ORDER: WO-### <name>
WHY: <one sentence — the player-facing or build reason>
FILES: <files CLI may touch> | DO NOT TOUCH: <files to leave alone>
ACCEPTANCE: <bullet exit criteria, line by line>
BAKE: <yes/no — if yes, the batchmode command + "editor closed">
GATE: brace check + build green + commit "feat: implement WO-### — <title>"
```

---

## 1. Lanes (parallel threads — but one check-in at a time)

| Lane | Owns (files) | Parallel-safe? | Current focus |
|---|---|---|---|
| **A — Village Scene** | `VillageSceneBuilder.cs` + scene | ❌ SERIAL bottleneck | Playable-village critical path (P0/P1) |
| **B — Combat / ATB** | Enemy, EnemyBrain, ATB, BattleController | ✅ | Dragon fix → lose-condition → ATB party |
| **C — Core data / economy** | Zone, wallet, build-mode systems | ✅ | WO-164 → WO-131 → WO-108 keystone |
| **D — World / content** | `OuterWorldBuilder`, resource nodes | ✅ | Worker auto-collect → nodes/settlements/tribes |
| **E — Polish / anim / UI** | HUD, camera, animator, audio | ✅ | Hero anim, console spam, camera, health bars |

A is the gate to a clean playtest. B/C/D/E run alongside A on separate files.

---

## 2. ORDERED QUEUE

Status legend: 🔴 open/next · 🟡 in CLI · 🟢 done (RESULT filed) · ⏸ parked · ✂️ superseded/void

### LANE A — Village Scene (SERIAL — do top to bottom, then ONE rebake)
| WO | Status | Note |
|---|---|---|
| 173 exterior terrain missing (black void) | 🔴 **NEXT — P0** | Whole world renders black. First order out. |
| 177 wall lean / walk-through / south gate | 🔴 P1 | Geometry orientation wrong. |
| 158 gates impassable (hero can't exit) | 🔴 P1 | Only 3 of 4 gates exist; drawbridges must be walkable. |
| 167 gatehouse pillar clips ceiling | 🔴 P1 | All 4 gates. |
| 168 navmesh seals gate openings | 🔴 P1 | Enemies/hero can't path through. |
| 157 strip magenta crystal veins | 🔴 P2 | Remove deleted vein generator from rebake. |
| 137 village rebake after castle | 🔴 (rebake) | **One bake** closes the whole Lane A pass. CLI batchmode, editor closed. |
| 166 base gates+anim+pet+stairs | 🟢 done | RESULT filed. |

### LANE B — Combat / ATB (parallel)
| WO | Status | Note |
|---|---|---|
| 125 dragon unhittable + no-lose on heart fall | 🔴 P1 | Boss height/reachability via Resources prefab. |
| 132 hero damage + real village lose-condition | 🔴 P1 | GameOverUI + Heart death event. |
| 169 ATB party-of-4 + real models (not purple pills) | 🔴 P1 | Phased; biggest "feels like a game" win. |
| 170 2D retro battle VFX | 🔴 P2 | After 169. |
| 171 ATB + overworld themes | 🔴 quick win | Audio only, low risk. |
| 135 P1 bug fixes (CrystalMine/VFX/WaveManager leak) | 🔴 P1 | Independent files. |

### LANE C — Core data / economy (parallel)
| WO | Status | Note |
|---|---|---|
| 164 zone foundation (ThreatLevel + records) | 🔴 **do first in C** | Read by Lanes B & D — unblocks them. |
| 131 economy wallet unification | 🔴 P1 | Single crystal source of truth (3 sources today). |
| 108 player build mode (VillageSceneBuilder power → player) | 🔴 ⭐ keystone | The core-vision feature. After 164/131. |

### LANE D — World / content (parallel)
| WO | Status | Note |
|---|---|---|
| 117 worker dispatch & auto-collect | 🔴 ⭐ owner #1 demoable | Wood → cap → bank. 2-day demo. |
| 153 world crystal mine | 🔴 | Renewable node. |
| 159 node settlements (claim/harvest/defend) | 🔴 | Depends on 164. |
| 160 wandering tribes | 🔴 | Radius-triggered raids. |
| 155 region enemy spawning | 🔴 | Data-driven tables + depth scaling. |

### LANE E — Polish / anim / UI (parallel)
| WO | Status | Note |
|---|---|---|
| 174 hero walks backwards + no walk anim | 🔴 P1 | Locomotion. |
| 163 3,351 console errors (AmbientNPC spam + AudioMixer) | 🔴 P1 | 99% of log noise; perf cost. |
| 156 camera pivot over high walls | 🔴 P1 | Hero off-screen; 3 competing controllers. |
| 178 HUD health-bar styling | 🔴 P2 | Flat green → themed. |
| 175 store visual polish | 🔴 P2 | Generic dark box. |
| 176 tower visual polish | 🔴 P2 | Swap to polyperfect mesh. |
| 179 moat water material | 🔴 P2 | Gated on VFX pass. |

### ⏸ Parked / design-led (not for batch execution)
152 city UI redesign · 161 player home interior · 162 music jukebox · 154 rare timed crystals · 111 resource pillar (design) · 121 analytics (spec-review)

### ✂️ Superseded / void
138 animated object factory (void) · 43 jupiter swap (superseded) · 136 castle = DONE (3 files were one job); remaining work split to WO-181.

---

## 3. Owner decisions (RESOLVED 2026-05-31)
1. **Two combat engines → ATB is a SEPARATE PvE mode.** Real-time village defense stays the core loop; ATB evolves independently. WO-169/170 scoped self-contained (Batch C).
2. **Canon → ELARION / Stone Choir is canon; purge Avalon** from live specs, keep history. → WO-182.
3. **WO-136 → DONE** (castle rebuilt). Remaining: stairs to upper level + unlockable secondary siege defenses. → new WO-181 (Lane A, after rebake).

4. **Wallet authority → EconomyService is the single ledger.** GameState + ResourceBalance become thin reads into it. Unblocks the 164 → 131 → 108 chain (Batch D). No HELD items remain.

---

## CHANGELOG (newest first — append one line per event)
- 2026-05-31 — Owner decision: wallet authority = EconomyService → WO-131 scoped; 164→131→108 unblocked (Batch D). All owner gates resolved.
- 2026-05-31 — Owner decisions resolved (ATB separate / Elarion canon / WO-136 done). Wrote WO-181 (rampart stairs + siege) and WO-182 (Avalon purge). Moved ATB into Batch C in PIPELINE.md.
- 2026-05-31 — Created this file. Consolidated state from full-repo digest. Lane A = first batch. Linear (112 issues) confirmed as lagging mirror; WORK_ORDER .md files are source of truth.
