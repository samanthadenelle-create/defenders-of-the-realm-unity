# CANON GROUND TRUTH — 2026-08-02 (marathon day 2: Echo program · action bar · raid truth · tester wallet program)

> **LIVE ANCHOR (2026-08-02).** Records reality after the day-2 marathon on `wip/village2-and-f8-tickets`.
> **Supersedes `CANON_GROUND_TRUTH_2026-08-01.md`** (still valid for anything not contradicted here).
>
> **This anchor is a DELTA over the 08-01 anchor**, which deltas 07-26, which deltas the deep 07-22 module anchor.
> Read order: this → 08-01 → 07-26 → 07-22 (§5 module digests, §6 catalog-drift, §7 comment-lies, §8 landmines)
> → `KEY_FACTS.md` → `SESSION_CANON_LOADER.md`.

---

## 1. REPO / GIT / GATES (verified this session)

- **Branch `wip/village2-and-f8-tickets`. HEAD = `731840e7`**, **local == origin — pushed.** Tree clean.
- **Today's commits (the day landed as three documented lanes, all gated BEFORE commit):**
  - `a7e4acb2` **fix(f8)** felt-fix wave — WO-842 wallet unify · WO-843 rebuild cards · WO-844 potions ·
    WO-797 dungeon rooms + exit beacon + `dg_starter_loop` re-bake (38 files)
  - `6f22a5fe` **feat(wo)** banked-spec wave — WO-830/831 Echo program · WO-835 action bar · WO-839 raid
    deploy · WO-840 armorer · WO-841 countdown (57 files)
  - `731840e7` **feat(tester)** wallet-first tester program — WO-766 Solana SDK live · WO-845 login errors ·
    WO-846 bug attribution · WO-847 wallet-first Android login (36 files)
  - Earlier in the day: `1812f3f8` MASTER_CATALOG 19-section SME rewrite (WO-836, 14-agent fleet) ·
    `b09cba68` canon refresh · plus the F8 triage commits.
- **Gates at HEAD:** `COMPILE_GATE_OK` + `REGRESSION_OK` (**six new oracles registered today**:
  raid-deploy-ui, wallet-provider, hud-actionbar, echo-picker, dungeon-room-ownership, realm-map) +
  **EditMode 849/849 — ALL GREEN** + `UI_CAPTURE_OK 26` (PNGs opened, not just counted).
- **The two long-standing `WaveDataTest` reds are GONE — they were STALE TESTS, not an open ruling.**
  The owner ruled smart-composition on 07-30 (`_smartComposition:1` → WaveManager generates rosters;
  `waves.json` `enemies[]` batches are inert). Both tests were rewritten to assert the batches are
  **EMPTY** — a re-add now FAILS. Any doc still calling this "an open owner ruling" is stale.
- **Save schema v36** (was v35). `v35→v36` adds **`everBuiltStructureIds`** (monotonic; seeded for
  established towns from BaseLayout ∪ FreeBuildsUsed ∪ the template snapshot). Echo lane tokens moved to
  a `<resource>:<level>` grammar and are **read-migrated — NO further bump.**
- **WO numbering: next free = 848** (`CLI_LANES_WO_NUMBERS.md` banner is the ONLY authority; 782–847 consumed).
  Four two-seat numbering collisions struck today; all resolved by first-on-disk-and-referenced-wins.
  **PROPOSED (owner to ratify): the UI seat mints only from a reserved 850–899 block.**

## 2. SHIPPED THIS SESSION

- **WO-830 + 831 — the Echo harvest program.** All six Echoes carry a unique harvest **affinity**
  (Wood/Iron/Food/Gold/**Crystals**/Repairs — owner ruling: Maren harvests **Crystals**, not Repairs).
  **Affinity is a MATCH BONUS, never a lock:** the player picks each Echo's harvest resource from a
  picker; matching the affinity doubles the yield. Token grammar `<resource>:<level>`. Three **disclosed**
  pair synergies + one **hidden tri-synergy** (applied, never displayed). Dump credits all five
  resources incl. Gold/Crystals. Emergence sprite beat at unlock with a never-blocking fallback
  (the 6 emergence PNGs are still owed by the owner — the beat degrades gracefully without them).
  Files: `Assets/_Modules/Village/Harvest/Echo*` (9 files) + `echoes-balance.json` (dual-copy).
- **WO-835 — the action bar shows only APPLICABLE faces.** New **`HudActionBarModel`** (Core) is the
  single enum-ordered authority; the View renders **from the array** and re-packs centered at constant
  width — **holes are impossible by construction** (that was the whole class of bug). Raids stays hidden
  until raid-capable; capable-but-not-full keeps the dim state.
- **WO-839 raid deploy screen** (FrameCore footer/subHeader zones, scout-report well, F8 note flow —
  and the **release softlock closed**: the harness freeze entry is now dev-guarded).
- **WO-840 armorer** — the armor building was seating the **weapons** vendor via `AnchorRoles`
  (`"Forge"`→`"Blacksmith"` at anchor `forge`, per WO-444 law). Party-shop cleanup B1–B5.
- **WO-841 upgrade countdown** now live-ticks through the VM off the queue publish seam; every WO-832 §4
  truncation fixed with **fixed-pixel line bands** (see §4 lesson).
- **WO-842/843/844 (felt fixes from F8 captures):** GameState is the single Wood/Iron authority (the
  captured "985k can't afford 800" was a stale-VM tier guard **plus** a dual-wallet seam — both fixed);
  destroyed/sold singleton buildings are **rebuildable again** (`IsPlayerBuilt` split from twin-counting
  `IsBuilt` — this had silently broken the WO-819 sell loop); Bag potions **apply their real effect**
  through an injected seam (they previously removed the item and lied).
- **WO-797 — dungeon rooms OWN their enemies.** Wake-from-room-footprint + confine-above-retaliation;
  a runtime `DungeonRoomBinder` covers already-baked scenes; encounter blocks in all 4 layout copies;
  the exit carries a **discoverability beacon** (pulsing light + EXIT label + wider prompt). This closes
  the owner's two starter-loop F8s: "enemies all gathered at the gate" and "no way to exit".
  `dg_starter_loop` was **re-baked in an isolated worktree** with the WO-796 real hero body
  (matesOk 10/10, PathComplete).
- **WO-766 — REAL Solana wallet connect is in the project** (see §3).
- **WO-845/846/847 — the tester program** (see §3).
- **WO-836 — MASTER_CATALOG full SME refresh:** all 19 `docs/MASTER_CATALOG/*.md` sections rewritten
  from the actual code by a 14-agent fleet (`1812f3f8`).

## 3. THE TESTER PROGRAM — identity model (OWNER-RULED, binding)

> **Firebase account = ACCESS. Solana wallet = DATA IDENTITY.**

- A tester needs a **Firebase account to receive the APK** (Firebase App Distribution) and to log in on
  desktop/web. On **Android/Seeker the login surface is wallet-first: "Connect Wallet" (the one gold CTA)
  or "Play as Guest" — there is NO email form** (owner ruling 2026-08-02, WO-847). Desktop/web keep the
  WO-787/845 email layout. The seam is `LoginSurfacePlatform` (Core) + `LoginWalletBridge`; the connect
  handler registers **before** the SKR skin gate so it is skin-independent.
- **The wallet address is the save key** (`playerId` for `/api/game/save`) and the **bug-report
  attribution key**. `BindWallet` rebinds; client-held state re-POSTs under the new key, so carry-over
  is automatic. **Cross-device requires the same wallet on each device.**
- **Solana SDK is really integrated:** magicblock `Solana.Unity-SDK` v1.2.9 by git URL.
  **`com.cysharp.unitask` was REMOVED from the manifest** — the SDK vendors UniTask under the same
  assembly name (`UniTask`), so the standalone package was a GUID conflict; existing refs resolve to the
  vendored copy. `DeNelle.Wallet.asmdef` references `com.solana.unity_sdk`; **`versionDefines` arm
  `SOLANA_SDK` on EVERY platform where the package is present** (compile-everywhere, runtime-select —
  it is NOT Android-only, contrary to an earlier agent report). MWA androidlib at
  `Assets/Plugins/Android/MobileWalletAdapter.androidlib` carries the `solana-wallet` `<queries>` scheme.
- **Mainnet is SAFE for testers: purchases are OFF, there is no transfer path, and the wallet is used for
  MESSAGE SIGNING ONLY.**
- **Bug attribution loop:** Settings → bug report → `BugReportVM` (playerId = BoundWallet, caps 120/500,
  log tail) → `POST /api/bug-report` → `INSERT INTO bug_reports` → readable via
  `api/admin/db.js?view=bugreports` (cursor paging, screenshot-presence flag) → local
  `bugreport-watch` daemon trio pings the CLI to triage.
- **⚠ OPEN SERVER GAP:** the **`auth_nonces` table does not exist** in Neon. It is the prerequisite for
  flipping wallet-auth to **Enforced**. `bug_reports` exists (0 rows at probe); `analytics_events` had
  79,428 rows. Until the table exists, wallet-auth stays permissive.

## 4. LESSONS FORGED TODAY (propagate — §12 discipline)

- **TMP vertical culling:** text bands must be sized in **fixed pixels ≥ the font's line height**, never
  as a fraction of a parent — fractional bands silently cull glyphs (this was the Rumor Board and the
  upgrade-panel truncation, same root).
- **Linear colorspace alpha:** `0.92` over parchment reads as **khaki**, not "slightly transparent".
  **Only `1.0` is opaque.** Do not use near-1 alpha as a design value.
- **A "plausible-but-wrong" oracle is worse than none:** the RAID-DEPLOY-UI suite first-matched its
  detector string inside an explanatory *comment* and passed a broken screen. Detectors must search
  **from the anchor index**, not from 0.
- **Read the failure's own evidence:** the Echo roster test was "fixed" once from a guessed prefix and
  failed again; the NUnit XML literally printed `But was: "Food - Lv 1 - +55% (best)"`. The XML is data —
  read it before theorising.
- **`LogAssert.Expect` is mandatory** when a test exercises a path that `FlowTrace.Fail`s — otherwise the
  runner fails the test for the very log line that proves the branch worked.
- **The gate's exit code lies; the marker does not.** (Re-proven again today — see `KEY_FACTS.md`.)

## 5. RAID BATTLEFIELD — verified anatomy (design decision PENDING with the owner/Grok)

Deep dive captured in **`docs/design/RAID_BATTLEFIELD_ANATOMY_2026-08-02.md`** and the consult brief
**`docs/design/GROK_CONSULT_RAID_BATTLEFIELD_2026-08-02.md`**. Verified facts:

- Raid bases occupy **~2.4% of the authored floor** — the owner's "just a square room with 1 enemy" is
  accurate, not a perception error.
- **Guard-ring bug:** garrisons spawn at `baseRadius * 0.5`, which places them **outside** the walls.
- **Towers fire only at the hero/companions and are indestructible; troops cannot damage structures.**
  "Razed %" therefore counts **bodies**, not buildings.
- **The hero is present in the raid**, which contradicts the drop-and-watch canon the owner stated
  ("Raids = drop troops and watch; Arena = I lead"). `HeroControlEnsurer.IsVillageScene` includes
  `HubScenes.IsRaid`. A spectator path is specced as **WO-774.0 (~2 days)** — **not yet built.**
- Troops rendering **magenta** = URP material breakage (laptop-absolute `.fbm` bindings + Supercyan
  materials never URP-fixed + `MagentaGuard` never sweeps runtime spawns) → **WO-838**, Phase-A probe first.
- `IronBastion` is an unregistered, spawnerless **mockup**.

**Nothing here has been "fixed" — the model decision is the owner's** (drop-and-watch vs. led raid).

## 6. OPEN — owner decisions / not yet built

- **WO-774.0** raid spectator model (awaiting the owner's/Grok's ruling on §5).
- **WO-838** magenta troops: Phase-A probe, then implementation.
- **WO-837** stockpiles cap capacity — **ruling captured, implementation not started.**
  Lumberyard / Foundry / Quarry(silo) are **capacity-cap stockpiles, NOT founding freebies**; they are
  OUT of the FoundingKit array and their `storageCapacity` becomes the live wallet cap.
- **WO-821** research costs TIME as well as resources (Warcraft-style research queue) — queued, not built.
- **Axe seat orientation** — owner's call: fix the Offset Forge path, or mint a `WeaponOrientHelper` WO.
- **850–899 UI-seat block** ratification (see §1).
- **WO-825 R1–R4**; **WO-823 Phase E**; silo-vs-Quarry naming; the Builders chip label ("Builders" vs
  "Queues"); the **6 Echo emergence PNGs** (owner-owed art).
- **`auth_nonces` table creation + the Enforced flip** (see §3).

## 7. HOUSEKEEPING DEBT

- Stale worktrees to remove: `$env:TEMP\eoa-bake` and `D:\EoA\.claude\worktrees\agent-*` (from WO-826).
- `PerformanceTestRunInfo.json` — decide whether it belongs in `.gitignore`.
