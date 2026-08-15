# BOARD NOW — prioritized working stack

**Generated:** 2026-08-15 (board audit from repo)  
**Source of truth:** `WorkOrders/*.md` → view = `BOARD.html` (`python tools/board_build.py`)  
**Notion:** RETIRED (2026-08-08). Do not use the old Notion DB.

---

## Snapshot (2026-08-15)

| Bucket | Count | Notes |
|--------|------:|-------|
| **Ready** | 482 | Most are >14 days old by file mtime — do not pull without validity check |
| Spec | 53 | Needs design / owner pin |
| Blocked | 16 | Explicitly blocked |
| Unlabeled | **0** | Hygiene pass 2026-08-15: PARTIAL→READY PARTIAL, LANDED→DONE, HOLD→BLOCKED |
| **Done** | 267 | ~143 Done rows still lack a `*.RESULT.md` |
| Closed | 108 | Superseded / cancelled |
| **Open total** | **~551** | vs **~375** Done+Closed (Ready inflated by PARTIAL keywords, not new work) |

### Binding rules (owner)

1. **Recency wins (this session):** the **last ~50–100 work orders by WO number** are weighted as **valid** when they **contradict** older tickets. Do not re-open or re-implement an older Ready item that fights a newer WO — treat the newer one as authority (Done *or* open).  
   - **As of 2026-08-15:** last **100** ≈ **WO-915 → 1020** (includes UI seat 1000+). Last **50** ≈ **WO-965 → 1020**.  
   - **How to apply:** if acceptance criteria or design conflict, **follow the higher WO number** (or the Done RESULT in that window). Older open tickets become **CLOSED — SUPERSEDED by WO-NNN** or **READY — PARTIAL: only remainder that does not fight NNN** after a one-line check — not “do both.”  
   - **Does not mean** every old ticket is dead — only that **on contradiction, recent wins**. Orthogonal older work can still be valid after the 2-week check.
2. **Age gate:** any ticket **older than 2 weeks** is **VERIFY FIRST**, not “still Ready = still do it,” **unless** it is inside the last-100 window (those stay high trust).

Most of the Ready pile outside 915+ is backlog / partially shipped / superseded by 915–1020 work.

### Hard supersessions (recent wins — do not implement the loser)

| Recent authority | Beats (older / conflicting) | Topic |
|------------------|----------------------------|--------|
| **1012 / 971 / 1014 DONE** | **702**, **133**, multi-guide FTUE paths | ONE guide, ONE tutorial |
| **993 READY** | **128**, pet aura / physical pet stack (WO-58 era) | Echoes = faucet/helpers, not companions |
| **933 DONE** | **906** deployable-siege framing | Catapult = troop (scarcity), not the old deploy unit ticket as written |
| **934 DONE** | **724** “barracks live” as greenfield | Army loadouts + train path largely exist — only *remainder* gaps |
| **968 DONE + 980 READY** | **920** stationary-cam as current truth | Loco/camera healed; framing is **980** |
| **935 READY** | **715**, **195**, master VFX forks | Paid pack program + unified cast is the VFX/anim spine |
| **990 READY + 991 SPEC** | any “build the town healer tower” path | Healer row retired; caravan is the field |
| **965 DONE** | old “F8 gets buried” re-litigation | Inbox is a queue |

---

## Priority order (what to work next)

**Weight:** open tickets in **WO-965+** first, then **915–964**, then older only if they survive verify + do not contradict the table above.

### P0 — Player-breaking / gates lying

| WO | Title | Status (2026-08-15) |
|----|-------|---------------------|
| **995** | Dungeon boot self-evict | **IMPLEMENTED** — grace + clear-hold arm; PO 10× boot verify |
| **980** | Dungeon camera framing | **OPEN** — needs owner framing ruling |
| **966** | Hero facing wrong while running | **IMPLEMENTED** — Mage/Ranger shoulder align; PO felt Mage run-N |
| **988** | Headed capture false OK | **DONE** (SelfTest 5/5) |
| **994** | Shield seat after WO-970 | **PARTIAL** — code B+C; **owner re-dial shield_A** |

### P1 — Ship risk / data truth

| WO | Title | Status |
|----|-------|--------|
| **996** | `armor.json` dual copies | **IMPLEMENTED** — SA library +15 ladders; subset regression |
| **984** | Gate wrapper log-text | **DONE** |
| **974** | Addressables content seam | **DONE** — EnsureBuilt + abort on fail |
| **975** | Gear / Blink | **PARTIAL** — no full Blink; **armor = stats+2D**; **weapons = placed 3D items** (shippable mesh path) |
| **910** | Ranger/Mage talents | **B partial** — stats LIVE; unlockAbility/summon/onEvent remain |
| **986** | Thin footprints | **IMPLEMENTED** CoC XZ |
| **989** | Ballista id | **IMPLEMENTED** `tower_ballista` + alias |
| **991** | Healing caravan | **RULED** follow-hero slow crawl, glass HP, siege support unit |
| **994** | Shield | **RULED** good until **dungeon→town port only** — fix seam, not re-dial |
| **976** | hasSurface false green | **DONE** |
| **985** | KeeperRelative yaw fragment | **IMPLEMENTED** |
| **987** | Portal touch + confirm | **IMPLEMENTED** — Obsidian confirm |
| **990** | Retire tower_healer | **DONE** (verified absent) |

### Board cleanup (recency supersessions, 2026-08-15)

Closed as superseded: **702, 133** ←1012/971 · **715, 195** ←935 · **128** ←993 · **906** ←933.

### P2 — Owner-ruled cleanup (implement when not in P0 fire)

| WO | Title | Notes |
|----|-------|-------|
| **993** | Echoes are faucet; retire physical pet stack | Includes FTUE lead replacement for leash |
| **990** | Retire `tower_healer` row; keep HealerTower behaviour | |
| **989** | `tower_wall_wizard` → Ballista identity | |
| **992** | Six unwired dead classes | Per-class; research first for torch/aura |
| **936** | Catalog gating / progression truth | PARTIAL — Finding A only |
| **954** | Hollow models data-driven | Needs one owner pin on models |

### P3 — Programs / polish (sequence, don’t thrash)

| WO | Title | Notes |
|----|-------|-------|
| **935** | Paid anim + VFX connection program | SME brief exists; **unified `cast(spellId, target?)`** is the north star |
| **901** / **900** | Collector loop umbrella / full tell | Already partial-shipped; finish remainder only |
| **1005** / **1009** | Dungeon UI cohesion / interactables | After P0 dungeon cam + exit |
| **1006** | Manage as launcher | Owner ruled option A |
| **946** / **941** / **942** | VFX subtle / UI geometry / capture gaps | Polish after feel is stable |

### Spec / owner pin (do not implement blind)

| WO | Title |
|----|-------|
| **991** | Healing caravan mobile + heal field |
| **986** | FootprintCells squares thin structures |
| **980** §3 | Camera framing option (owner) |
| **910** | Ranger/Mage talent consumers (design) |

### Recently DONE (do not re-open)

Examples (mtime ≤14d): **933** catapult troop, **934** army loadouts, **968** dungeon loco/camera heal (framing = **980**), **1012** FTUE redesign, **965** F8 queue, **960–964**, many 970s. Prefer RESULT files + Status DONE.

**Status drift to fix when touching files:** WO-**909** still says LANDED (should be DONE keyword); WO-**900/901** still READY though largely shipped.

---

## >14 day Ready — validity protocol

**Do not implement a Ready ticket with file age >14 days until one of:**

1. **Still valid** — acceptance criteria still fail at HEAD (one proof: code path, capture, or play).  
2. **Partial** — rename status to `READY — PARTIAL: <one remaining line>`.  
3. **Done** — criteria met → `DONE` + `*.RESULT.md` (even short).  
4. **Closed** — superseded/removed system → `CLOSED — SUPERSEDED by WO-NNN` (or reason).

**Largest stale Ready clusters (spot-check first, don’t bulk-close):**

| Cluster | WO range | Likely fate |
|---------|----------|-------------|
| Legacy pre-400 backlog | ~251 Ready | Many obsolete vs hub/dungeon/raid reality — **verify or CLOSE** |
| CoC program 723–731 | 724–731 | Much of Path A / raid may already exist — **verify against HEAD before re-implement** |
| Founding FTUE 702–710 | 702+ | **1012 DONE** may supersede large chunks of 702 — verify remainder only |
| Hovl/VFX 715 | 715 | Overlaps **935** / SpellVfx — fold, don’t duplicate |
| Builder queue 762 | 762 | Obsidian multi-channel queue largely shipped (v37) — verify remainder |
| Board hygiene | **918**, **937**, **940** | Meta: execute as a dedicated sweep, not mid-feature |

**Hygiene WOs that clean the board itself:** **918** (close shipped), **937** (status keywords + parser), **940** (created-date filter). Prefer running these *after* P0 feel tickets, or as a dedicated half-day.

---

## What NOT to pull

- Anything **Ready** only because nobody closed it after a later WO shipped it.  
- **Defend-the-Tower / PatriciaLight** era (already CLOSED/SUPERSEDED where marked).  
- Full **935** pack rewiring before a thin **unified Cast** slice is designed.  
- **991 / 986** without owner design/call.  
- Bulk status edits without the validity protocol above.

---

## Live F8 (as of last inbox)

- **2026-08-14** `Dungeon_HealersCottage` — camera doesn’t work right; harvest shows `timeScale=0` freeze risk + DungeonCam OTS. Maps to **980** (+ confirm not **995** spawn/exit if scene loads wrong).

---

## How to refresh

```text
python tools/board_build.py
# open BOARD.html
# this file = human priority; regenerate when the working stack shifts
```

Next mint: see `CLI_LANES_WO_NUMBERS.md` banner (main line was **997** as of 2026-08-14 recon).
