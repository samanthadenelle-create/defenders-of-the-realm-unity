# SUNDAY STATUS — Work-Order / Ticket Reconciliation — 2026-07-26

**Purpose:** the Sunday housekeeping ledger — every WO/ticket in flight this session, with its ACTUAL
status (shipped this session vs. remaining), reconciled against `git log` (HEAD `7dec0e07`, local==origin),
the firmed spec files in `docs/qa/`, `CLI_LANES_WO_NUMBERS.md`, and `NOTION_SOURCE_OF_TRUTH.md`.

**Status legend:** DONE (shipped + pushed this branch) · IN-PROGRESS (partially landed / owner cited "in
progress") · BACKLOG (specced, not started) · BLOCKED (waiting on owner ratification / asset staging).

---

## 1. Dungeon functional loop — WO-770 (11 sub-orders)

| Sub-order | Title | Status | Evidence |
|---|---|---|---|
| 770.1 | Always-open exit + boss-gated back-door | **DONE** | `8ccacd9d` — `DungeonExit`, `ExitToVillage()` now called |
| 770.2 | Return to the CORRECT dungeon after a fight | **DONE** | `4f4545c8` — `EncounterTrigger.ReturnScene` no longer hardcoded |
| 770.3 | Real victory/defeat carrier | **DONE** | `e51628e0` — `SceneRouter.PendingBattle.LastOutcome`; lost fight ends run |
| 770.3b | Real-time encounter settlement + defeat parity | **DONE** | `53e1b9e4` — `BattleArena.OnBattleEnded` → shared `SettleEncounter` |
| 770.4 | Readable lore stones | **DONE** | `15fb8ca1` — `LoreStone.Read()` wired + code-built lore modal |
| 770.7 | Player-feedback toast layer + live Bryn dialogue | **DONE** | `101fa983` — checkpoint/craft toasts + Bryn FirstMeet/Idle (D13/D14) |
| 770.9 | Housekeeping — stale-read + builder overlap | **DONE (stale-read)** | `fd45e2f0` — `DungeonRuntimeState.OnEnable` clears run identity (D11). Builder-overlap dedupe (D15) folds into 770.6. |
| 770.5 | Consolidate two Village→Dungeon entry systems | **BACKLOG** | OWNER-GATED (village re-bake); canonical-verify seam first |
| 770.6 | Folk's Granary = first-torch tutorial dungeon | **BACKLOG** | CODE + OWNER-GATED; reuses Lantern+crafting; Granary→Cottage prologue |
| 770.8 | Content: KayKit geometry, ambient BGM, asmdef | **BACKLOG** | CONTENT/OWNER (KayKit zip in Downloads; asmdef item is code-only) |
| 770.10 | Hero integration: real vitals + walk anim | **BACKLOG** | CODE + CONTENT; vitals code-only, walk-anim on WO-772/771.13 rig |
| 770.11 | Dungeon enemy-placement system | **BACKLOG** | Depends on WO-772 + WO-771.13 |

**Dungeon movement/camera/Bryn felt-test fixes (fold into 770 wave):**

| Item | Status | Evidence |
|---|---|---|
| DungeonHero sole mover + taller camera + exit interaction | **DONE** | `82e1f3a4` |
| Bryn pill-hide covers a skinned baked body | **DONE** | `f42e6f7e` |

---

## 2. Raid system — WO-771 (Teleport/Deploy, v2, 15 sub-orders)

**Loop LOCKED to Teleport/Deploy** (owner 2026-07-26); walk-to retired as the raid loop. **Nothing built yet
— entire WO-771 is BACKLOG (SPEC firmed + validation-signed-off).** V1 spine below; V2 sub-orders drop to V2.

| Sub-order | Title | V1/V2 | Status |
|---|---|---|---|
| 771.0 | Move `IDamageableStructure` into Core + extend | V1 (prereq) | **BACKLOG** |
| 771.1 | Troop schema + canonical `troops.json` | V1 | **BACKLOG** |
| 771.1b | Consolidated save-schema migration (owns all new fields) | V1 | **BACKLOG** |
| 771.4 | Deploy UI + `RaidDeployLog` capture | V1 | **BACKLOG** |
| 771.9 | Barracks & troop upgrade progression | V1 | **BACKLOG** |
| 771.6 | Scoring / stars / loot / economy payout | V1 | **BACKLOG** |
| 771.10 | Defensive tower combat (greenfield) | V1 | **BACKLOG** |
| 771.11 | Live raid HUD | V1 | **BACKLOG** |
| 771.5 | Raid scene / runtime state / troop actors / playback | V1 | **BACKLOG** |
| 771.13 | Shared troop/enemy/hero Animator + KayKit builder | V1 (art) | **BACKLOG** (asset-staging) |
| 771.2 | Base snapshot model + capture | V2 | **BACKLOG** |
| 771.3 | Deterministic sim + breach-aware pathing | V2 | **BACKLOG** |
| 771.7 | Deterministic replay + async matchmaking + anti-cheat | V2 | **BACKLOG** |
| 771.12 | Defender-side economy: loot loss / shields / revenge | V2 | **BACKLOG** |
| 771.14 | Balancing / tuning pass | V1+V2 | **BACKLOG** (gated on monetization/wallet specs) |

> **Save-schema reconciliation note:** WO-771.1b is written against the read-only tree's schema (v10→v11).
> Live `wip` schema is **v34** — CLI reconciles the raid fields onto a v34→v35 migration when 771.1b is built.

---

## 3. Shared enemy system — WO-772

| Item | Status | Note |
|---|---|---|
| WO-772 enemy classes/families/armor/weapons + `EnemyResolver` | **BLOCKED** | Owner-ratification gate: operationalizes `docs/enemy-codex.md`, a review-and-approve codex. Owner ratifies the roster (or subset) first. Also fixes the generic-skeleton spawn bug. Prereq for 770.11 + 771.13. |

---

## 4. Common Obsidian job queue — WO-773

| Item | Status | Note |
|---|---|---|
| WO-773 `ObsidianQueueService` (unified timed-work queue) | **BACKLOG** | Supersedes ad-hoc `BuildingCooldowns`/`PendingBuilds`. If CLI's `BuildTimerService`/WO-762 lands first, this becomes "generalize `BuildTimerService` into the slotted queue". Consumed by 771.8/771.9 + village build/tower/wall. |

---

## 5. Non-dungeon felt-test fixes shipped this session

| Item | Status | Evidence |
|---|---|---|
| Enemies stay out of castle (tutorial) + battle-mode BattleLock | **DONE** | `e05f92f7` (ZoneManager 52/52) |
| Towers no longer shoot through walls (Structure layer + LoS) | **DONE** | `2cb3c40d` |
| MagentaGuard catches Android compile-failed shaders | **DONE** | `386a932f` |
| Loading overlay (founding→hub) | **DONE** | `4edf8dcc` |
| Loading overlay uses a standard loading bar | **DONE** | `7dec0e07` |
| Gate-traversal teleport disabled (walk through arch) | **DONE** | `8c35332f` |
| Collector buildings get vendor NPCs (Lever 1 in progress) | **IN-PROGRESS** | `804a02a2` (owner cites Lever 1 in progress) |
| Alchemy recipe list scroll/overlap fix | **DONE** | `8ca95735` |

---

## 6. Counts + flags

- **DONE:** 7 dungeon sub-orders (770.1/.2/.3/.3b/.4/.7/.9) + 2 dungeon felt fixes + 7 non-dungeon felt fixes = **16 items shipped + pushed.**
- **IN-PROGRESS:** 1 (collector vendor NPCs — Lever 1).
- **BACKLOG:** 5 dungeon sub-orders (770.5/.6/.8/.10/.11) + all 15 raid sub-orders (WO-771) + WO-773 = **21 specced-not-started.**
- **BLOCKED:** 1 (WO-772 — owner enemy-codex ratification gate; blocks 770.11 + 771.13).

**Flags for the owner:**
1. **WO-772 is the gating dependency** for dungeon enemy placement (770.11) AND raid art (771.13) — route
   `docs/enemy-codex.md` for review-and-approve to unblock.
2. **WO numbering drift:** `CLI_LANES_WO_NUMBERS.md` banner said next-free 761; 761–773 are consumed →
   **next-free = 774** (corrected in the banner this session).
3. **Save-schema mismatch in WO-771.1b/773 specs** (written against v10/v11; live is v34) — reconcile onto
   v34 at implementation. Documented, not a defect.
4. **Nothing claimed-done that isn't:** all "shipped" items above trace to a specific commit on `wip`
   (local==origin). The WO-770/771/772/773 *specs* are DONE/firmed as documents; the *implementations* are as
   tabled (770 partial, 771/772/773 not started).
5. **Full DataRegression re-baseline not re-run this pass** — last certified `REGRESSION_OK` is 07-22;
   re-run before the next build ship.
