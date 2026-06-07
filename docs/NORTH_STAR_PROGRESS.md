# North Star — Progress & Trajectory (tracker)

_Assessed 2026-06-03 against `docs/NORTH_STAR.md`. Updated as rungs move._

## Ladder position: **Rung 2 → 3 (mid-climb)**
| Rung | Status |
|---|---|
| 1. Defend the Tower (PatriciaLight) | **standing** (iterating; now on the shared factory) |
| 2. Defend the Town (village TD) | **WHERE WE LIVE** — being hardened for the grant demo |
| 3. Defend + Explore (world beyond walls) | **bones in** (OuterWorld + ZoneManager + regions; orc roam/camps registered) |
| 4. Place your base (where you build) | not started |
| 5. Structure your settlement (CoC build mode = the CREATE verb) | **the heart — not started** |
| 6. Build how you want | the dream |

## Core-loop spine — pillar status
| Pillar | North Star target | Now |
|---|---|---|
| **BUILD (player base)** | CoC build mode (the CREATE verb) | 🔴 BuildMenu + plot/grid exist; full build-mode is **the** gap. Village still builder-generated = the documented drift. |
| **Walls + tiers** | wood→stone→reinforced sink | 🟡 WallSegment built; tiers still a gap. DEF-121 adds a Magic-gated upgrade tier (partial). |
| **Towers** | place + upgrade | 🟢 built; 3-tier model swap (DEF-208) pending. |
| **HARVEST (nodes/mines)** | generalize + auto-harvest | 🟡 CrystalMine passive; **DEF-121 (in flight)** makes Wood/Food/Iron/Crystals harvestable + pet auto-harvest → real movement. |
| **DEFEND (waves/roaming)** | threat to base + mines | 🟢 **big jump this session** — DEF-224 fixed enemy aggro + wall collision (combat actually works now); orc family adds roam/camp threat. |
| **Economy + OFFLINE** | currency + idle accrual | 🟡 GameState + save exist; DEF-121 economy lands; **offline accrual still a gap**. |

## What this session moved (net)
- **DEFEND works now** (DEF-224) — combat was broken (enemies ignored hero / walked through walls); it's the spine and it's fixed.
- **One reusable factory** (hero/companion/enemy/pet→ PatriciaLight) — this is the "AI structure that makes one person a studio" + the **printing-press for evergreen content** the doc bets on. Lightweight foundation hardened.
- **Onboarding (DEF-222)** — teaches the loop = the population/retention engine.
- **Front door + polish (DEF-211, cards, HUD, lighting, moat)** — *"the clean polished build IS the pitch"* (Pi/Solana grant). Directly on the GTM thesis.
- **Economy (DEF-121)** — reconnecting HARVEST→UPGRADE.
- **Codex by family (orc/hollow)** — the enemy variety + the air/ground & role counter axes the evergreen-meta engine runs on.

## Trajectory — honest
- **Short term (grant): ON TRACK.** Hardening a clean, playable *Defend-the-Town* slice + working combat + onboarding + front door is exactly the doc's "polished build is the pitch." Right focus for the one-grant-shot.
- **Mid term: reconnecting the loop.** Harvest/economy moving (DEF-121); offline accrual + wall tiers are the next loop gaps.
- **Long term: the CREATE verb is still untouched.** Rungs 5–6 (player build mode) — the *heart* of the vision — haven't started. We are polishing the builder-generated village, which the North Star explicitly calls "the inverse of the vision." That's the right *sequencing* (demo first) but it's the big outstanding gap, and the end-game (async Challenge Arena, smart targeting, maneuver AI) sits beyond it.

## The one vision-step that would most move the needle (post-grant)
**Build mode** (Rung 5): "let the player do what `VillageSceneBuilder` does." The primitives exist (BuildMenu, plot/grid, polyperfect palette, StructureFactory). Turning the builder's power over to the player is the single highest-leverage move from "polished demo" toward the actual differentiated game — and it unlocks the Arena end-game (you author both base + attack).

**Bottom line: mid-ladder, not bottom. The hard systems are built; the gap is the player-build layer + reconnecting the loop — a re-centering, not a rebuild.** Current grant-focused work is North-Star-aligned; the heart (CREATE) is the deliberate next climb once the demo lands.
