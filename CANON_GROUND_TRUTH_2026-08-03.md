# CANON GROUND TRUTH — 2026-08-03 (the solo-night wave + the first live server verification)

> **LIVE ANCHOR (2026-08-03).** Records reality after the 2026-08-02 → 08-03 overnight solo run and
> this morning's boot verification.
> **Supersedes `CANON_GROUND_TRUTH_2026-08-02.md`** (still valid for anything not contradicted here).
>
> **This anchor is a DELTA over the 08-02 anchor**, which deltas 08-01 → 07-26 → the deep 07-22 module anchor.
> Read order: this → 08-02 → 08-01 → 07-26 → 07-22 (§5 module digests, §6 catalog-drift, §7 comment-lies,
> §8 landmines) → `KEY_FACTS.md` → `SESSION_CANON_LOADER.md`.
>
> **Why this anchor exists:** the 08-02 anchor pinned HEAD `e60b19e5`. Seventeen commits landed after it.
> Three boot documents inherited the stale sha and the stale test count — the exact staleness cascade §15
> exists to prevent, repeating one day after the last one was found.

---

## 1. REPO / GIT / GATES (verified from the tree this session)

- **Branch `wip/village2-and-f8-tickets`. HEAD = `56be3ae2`**, **local == origin — pushed.**
- **The 08-02 anchor's HEAD (`e60b19e5`) is 17 commits behind.** The overnight wave ran
  `e60b19e5` → `8e70a3d4` (15 commits, all pushed), then `9d386207` (raid bake) and `56be3ae2`
  (the night report).
- **Working tree is CLEAN.** The in-flight item-identity lane the 08-02 anchor flagged as uncommitted
  landed in `e60f22d6`. The only untracked paths were `Assets/Resources/SolanaUnitySDK/` and
  `Assets/WebGLTemplates/xNFT/` (WO-766 SDK import residue) — **now tracked**, see §6.
- **Gates at HEAD** (from `NIGHT_WRAP_2026-08-02.md` §1 and re-read off the artifacts):

  | Gate | Marker | Was |
  |---|---|---|
  | Compile | `COMPILE_GATE_OK` | — |
  | Data regression | **`REGRESSION_OK 104/104 suites`** | 86 registered that morning |
  | EditMode tests | **`TESTS_OK 912/912`, zero reds** | 884 |
  | UI capture | `UI_CAPTURE_OK 28` | — |

  `Builds/test-results-EditMode.xml` reads `total="912" passed="912" failed="0"`, run 2026-08-03 03:12Z.
  **Any doc still saying 884/884 is stale** — that count was current for less than a day.
- **Save schema v36**, unchanged (`SaveSchema.cs:36`).
- **WO numbering — two-block allocation, unchanged.** `CLI_LANES_WO_NUMBERS.md` banner is the ONLY
  authority: **main line (CLI) next free 853** · **860–899 reserved (UI seat) next free 863**.

## 2. SHIPPED OVERNIGHT (15 commits, `e60b19e5` → `8e70a3d4`)

Frozen detail lives in `NIGHT_WRAP_2026-08-02.md`; this is the canon summary.

- **Enemies now reach you.** Two independent causes of "they mill around": DPS/Ranged enemies targeting
  their own wounded ally, and `_stopTightenedForHero` surviving pooling so a reused body halted 2.5 m out,
  outside the 1.5 m engage ring. Pooled enemies also no longer freeze as statues (`_casting` was never
  cleared on death).
- **Raids are no longer a square room.** Base footprint went from ~2.4% of the authored floor to
  **20 / 49 / 60%**, with a **central spire as the win condition** instead of a corpse count. Raid walls
  had **no colliders at all**, and no raid scene had a hero spawn point — the hero was landing on castle
  courtyard coordinates, i.e. inside the walls on top of the objective. Arenas re-baked, 4/4 walkable
  (`9d386207`).
- **Raid troops animate and aren't magenta** (WO-838's class): nothing under `Troops/` ever assigned an
  AnimatorController, and `MagentaGuard` only swept at scene load, so anything spawned mid-raid was
  permanently invisible to it.
- **Gear/economy:** a level-1 Mage is no longer unarmed; shield upgrades finally do something; the defense
  cap is one constant at **0.90** instead of fourteen literals that had drifted into two numbers.
- **The tutorial's Hollow step can be completed** — it armed in an enemy-owned scene where building
  silently refuses, and asked for a "Lumberyard", which is a *stockpile* and harvests nothing.
- **Identity/backend:** login can't hard-softlock, stub wallets can't collide across devices, bug reports
  carry a stack trace, and the cloud-save server half was rewritten (see §3).
- **The check-in gate had never been running** — not "ran the wrong suite": `tools/regression/checkin_gate.ps1`
  did not *parse* under PowerShell 5.1, so no stage of it had ever executed (`6aac7351`).
- **Adaptive difficulty landed pure, deterministic, and INERT** — see §5.
- Also: `DeNelle.Core.Difficulty` renamed **`DeNelle.Core.Adaptive`** (the namespace shadowed the persisted
  `Difficulty` enum; the enum is in saves, so renaming *it* was not an option); hub foliage; a 28-term
  glossary; Cathedral mage-tier perks now reach the hero; item identity.

## 3. THE SERVER — FIRST LIVE VERIFICATION (2026-08-03, this session)

### 3.0 The stack, stated plainly (owner clarification 2026-08-03 — stop re-confusing these)

> **Firebase = ACCESS + DELIVERY. Vercel = the API host. Neon = where the data lands.**

| Layer | What it is | What it is NOT |
|---|---|---|
| **Firebase** | App Distribution (how a tester RECEIVES the APK) + Firebase Auth (login/access) | **not** a backend — there is **no `firebase.json` and no `functions/` in this repo**; no Firebase-hosted API exists |
| **Vercel** | hosts the `api/` serverless functions; also where the **demo WebGL links** are deployed | not how the game reaches testers |
| **Neon** | Postgres — `player_data`, `bug_reports`, `auth_nonces`, `analytics_events` | never reached directly by the client; always through `api/` |

**The consequence that matters:** the Firebase-distributed APK **hardcodes the Vercel domain** in nine
shipping client files — `GameStateService.cs:1083` (`/api/game/save`+`/load`), `BugReportVM.cs:44`
(`/api/bug-report`), `WebTrace.cs:76` (`/api/trace`), plus `EventTracker`, `PromoCodeService`,
`ReferralService`, `PiSignInController`. So promoting `api/` is **not** about getting a build to testers —
they already have it via Firebase. It is about **the APK already on their phones silently dropping their
bug reports and their progress** against old server code. That is why `bug_reports` has 0 rows and
`player_data` is two May fixtures.

⚠ One `vercel deploy` ships **both** the demo WebGL and `api/` — which is where "Vercel is just for demo
links" comes from, and why the API half gets forgotten.

> **Everything in this section is probed, not reported.** The 08-02 anchor's "`auth_nonces` does not
> exist" line is **now WRONG** and is corrected here.

- **`auth_nonces` EXISTS and works.** `GET /api/auth/nonce?wallet=<base58>` on **production** returns
  **HTTP 200** with a real nonce + 300 s TTL, repeatably. The owner's `schema.sql` run fixed it.
  ⚠ **Caveat: the table held 2 rows and both were minted by this probe** — it exists but has never been
  exercised by a real client.
- **Production is running OLD `api/` code.** Proven two ways from the live surface: a malformed wallet
  returns the prose `{"error":"Missing or malformed wallet"}` instead of the structured
  `{ok:false, code:"AUTH_WALLET_MALFORMED", ref}` the 08-02 rewrite emits; and `/api/admin/db` answers
  `Unknown view. Use: overview | players | metrics | traces` — the `bugreports` and `authrejects` views
  (WO-846) are absent. **`api/` is deployed to PREVIEW only and the game hardcodes the production domain.**
- **⚠ NEW, not in the night report: production's nonce endpoint has NO CORS headers and `OPTIONS`
  preflight returns 400.** A browser therefore blocks the WebGL build's nonce fetch before the function
  runs. That is exactly the bug the 08-02 rewrite fixed — and the fix is on preview. **The wallet rail is
  still unreachable from the web client, even though curl gets a clean 200.**
- **Table state (prod, probed):** `player_data` **2 rows**, both test fixtures (`test-wallet-0001`,
  `Test123`), `schema_version` **10**, newest **2026-05-31** — no player's progress has ever reached Neon.
  `bug_reports` **0 rows**. `analytics_events` **80,749 rows**, latest 2026-08-03 02:27 — web tracing is
  flowing fine.
- **THE SINGLE HIGHEST-VALUE ACTION ON THE BOARD: promote `api/` to production** (`vercel deploy --prod`).
  Owner's call; nothing downstream of the tester programme moves until it happens. Verified safe: the new
  `load.js` still accepts `?playerId=`, which is what the shipped client sends, so no build in the wild breaks.
- **Do NOT flip `BACKEND_AUTH_ENFORCED`** — the wallet signing path has never been proven end-to-end on a
  device, and per the caveat above the nonce table has never seen a real client.

## 4. THE ONE CROSS-CUTTING SEAM (verified from code this session)

> **Nothing in this game can damage a wall, gate, or enemy tower.**

`WallSegment.cs:28` and `Gate.cs:45` implement **`IDamageableStructure` only**.
`TroopController.NearestHostile()` (`TroopController.cs:449-459`) sweeps for **`IDamageable`** via
`GetComponentInParent<IDamageable>()`. **The two interfaces are disjoint**, so a deployed troop
structurally cannot see a wall as a target — which is why "Razed %" counts bodies, not buildings.

This is the prerequisite hiding under **both** long-range roadmaps: a raid can never be about bases under
either posture, and a player-authored base another player attacks has nothing to attack. **~2–3 days.**
It also means **the WO-774.0 drop-and-watch-vs-led-raid ruling is free to defer** — the seam is required
either way.

## 5. OPEN — owner decisions / filed not fixed

Full list with `file:line` in `OUTSTANDING_FOR_GROK_2026-08-02.md` (⚠ that file predates the 15 overnight
commits; `NIGHT_WRAP_2026-08-02.md` §6 is the newer filed-not-fixed list).

**Owner decisions:**
- **Promote `api/` to prod** (§3) — the one unblock.
- **Should shields drop at all?** Both exact-rarity weapon loops can award an off-hand as a main hand —
  the same shape as the unarmed-Mage bug. The one-line fix removes shields from the drop pool: a design call.
- **WO-774.0** raid spectator model — deferrable per §4.
- **`lumberyard` in `FoundingKit`** — a stockpile is currently a founding freebie, in tension with the
  WO-837 ruling. Removing it can only *add* softlock risk, so it was left.
- **Author `category` on ten legacy `weapons.json` rows** (`knight_starter`, `ranger_starter`,
  `cleric_starter`, all four `aegis_*`) — the armed-hero oracle then upgrades to a hard per-class
  weapon-kind assertion with no code change.
- **Killable enemy raid towers** — patch written, needs a felt-test not a compile.
- **Melee has no on-hit visual** for non-perfect swings now that Perfect Hit is a real timed input.
- Owner ratification of the **860–899 UI-seat block** (operational already); the **6 Echo emergence PNGs**.

**Known-open, filed not fixed:**
- **Adaptive difficulty is INERT.** The math is correct and oracle-proven (at-target lands on 1.000
  exactly, both rails reachable), but `WaveManager` records none of the six fields it needs, so every read
  returns 1.0. `Enemy.SetBaseStats`/`ApplyDifficulty` and the spawn hooks are in; the measurements are not.
- **A cancelled build has always burned the charge** — `CancelPlacing` refunds `TowerData.cost` crystals,
  which is 0 for DevTower. Pre-existing, but now costs ~70 wood + 40 iron.
- **Placement still spawns `Towers/DevTower`** regardless of which tower row is picked.
- **`Place()` charges after `loader.Spawn`**, so a declined charge leaves a structure standing. Now loud.
- **A fourth copy of the shader predicate** lives in `HeroBodySwapper`, without the `isSupported` branch,
  on the runtime hero-body path that ships to Android.
- **Three more spawners never apply role tactics** — the dungeon group path is worst (every dungeon mob
  runs with no tactics at all).
- **`GearLoadout.EquipArmorById` enforces nothing** — no class, weight or level gate.
- **Guests can't migrate to a wallet** — connecting after playing as guest orphans the guest row.
- **WO-848** restore Android managed stripping Medium · **WO-851** spec on disk, not implemented ·
  **WO-861** in flight · **WO-862** minted, not implemented · **WO-837** stockpile caps, ruling captured,
  not started · **WO-838** magenta troops largely addressed overnight — re-scope before implementing.

## 6. HOUSEKEEPING RESOLVED THIS SESSION

- **WO-837 and WO-838 each had TWO different spec files on disk** — a real collision, not doc drift:
  - `837` = stockpiles-cap-capacity **and** wallet-first-identity-drop-email
  - `838` = bug-report-email-and-admin-view (⚠ superseded by WO-846) **and** raidbase-material-survivability

  Resolved by first-on-disk-and-referenced-wins; see the banner for the renumbering.
- **WO-848 and WO-849 exist only as banner rows** — no spec file at all, and 849 is marked SHIPPED. A WO
  with a commit and no artifact.
- **`Assets/Resources/SolanaUnitySDK/` + `Assets/WebGLTemplates/xNFT/` are now TRACKED.** The SDK resolves
  its adapter UI through a *serialized prefab reference*, so deleting risks a null at wallet-connect;
  untracked content under `Resources/` that ships is the magenta-ground bug class. 85 KB, and it kills a
  two-machine drift vector. (We ship `webGLTemplate: PROJECT:Pi`, so xNFT is unused but harmless.)
- **RESULT-file debt is 33, not the 31 canon claimed.**

## 7. CANON HEALTH — what is stale RIGHT NOW

- **The 2026-08-02 Sunday sweep ran steps 1, 2, 6 and 8 but SKIPPED 4, 5 and 7.** Consequences:
  - **`docs/reference/REGRESSION_COVERAGE_MATRIX.md` is two Sundays overdue** and still headlines
    *"0 of 73 covered / 16 suites green"* against a tree that runs **104 suites**. Use its *proposed
    assertions*; **never quote its counts**.
  - **`docs/reference/MASTER_BACKLOG_2026-07-19.md`** still lists shipped rows as open.
  - No `docs/qa/SUNDAY_STATUS_2026-08-02.md` was produced.
- **`docs/MASTER_CATALOG.md` — the INDEX itself was NOT refreshed by WO-836.** Only the 19
  `docs/MASTER_CATALOG/<area>.md` section files were rewritten. The index body still claims Blaise +
  party-of-4, OuterWorld streaming, 64 Yarn nodes, `SaveSchema v30`, "next free WO = 412". **Treat
  `MASTER_CATALOG.md` §1–§3 as a filename index only** — the section files are the truth.
- **The 19 section files are code-true as of `b77a178e` (08-02 morning), NOT current HEAD.** ~20 commits
  landed after that fleet ran. Known contradictions: `economy-meta.md` says WO-830 is "spec only, NOT in
  code" (it shipped); `docs-wo-state.md` says save is v35 and next-free WO is 836 (v36; 853/863);
  `resources-art.md` says the KayKit bodies have no Animator wiring (WO-833 shipped it);
  `village-npcs.md` documents the `"Forge"`→`"Blacksmith"` anchor mapping as correct (that mapping *was*
  the WO-840 bug).
- Stale worktrees still to remove: `$env:TEMP\eoa-bake`, `D:\EoA\.claude\worktrees\agent-*`.
  `PerformanceTestRunInfo.json` — still undecided on `.gitignore`.
