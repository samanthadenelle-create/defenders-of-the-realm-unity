> ## ▶ LIVE ANCHOR = `CANON_GROUND_TRUTH_2026-08-09.md` — read it FIRST (refreshed here 2026-08-09)
>
> The `Latest (2026-08-09)` section immediately below is current. **Older dated `Latest (...)` sections
> are history — where one disagrees with a newer section or with the anchor, the newer wins.**
> The 08-08 anchor is bannered SUPERSEDED and is **INVERTED** on its two headline sections (the machine
> block is resolved; the dungeon-stair hunt is closed). Do not act on it.
>
> Per CLAUDE.md §15 THIS file is LIVING — edited in place, never snapshotted. The `CANON_GROUND_TRUTH_*`
> anchors are the dated snapshots.
# KEY FACTS — the living fact sheet (update IN PLACE, never snapshot)

> **Rule (owner directive 2026-07-12):** this file is LIVING — when a fact changes, edit the line
> in the same commit as the change and re-stamp its date. Facts here are code-verified, never
> assumed. If a doc contradicts a line here, the doc is stale. Dated anchors
> (`CANON_GROUND_TRUTH_*`) remain the session snapshots; THIS file is the always-current card.

## ⭐ NORTH STAR — the state we are building toward
- **The product:** "Echoes of Elarion" (chapter) in the "Defenders of the Realm" series — "Echoes of
  a Forgotten Civilization" (retired tagline "Hold the last light" noted in canon-strings.json).
  **V1 = ONE controllable Knight ("Grom")** in an overworld with isolated
  real-time BattleArena combat; **the player builds their own city** (player-defined map pivot
  07-11: Build → place/move/rotate functional structures). *(The "build mode IS the demo" framing is
  RETIRED — see the platform line.)*
- **The platform:** **mobile web in Pi Browser**. **Pi Hackathon: WON (owner, 2026-07-17)** — the
  July-31 deadline + "build mode IS the demo" framing is RETIRED/STALE: there is NO upcoming demo, the
  roadmap is OPEN for the next phase (owner sets the new north star). Any doc still leaning on the
  hackathon deadline is STALE. Desktop is the dev proxy, never the verdict.
- **The bar:** the **ten-year-old test** — "wow, this feels good" on a phone, or it isn't done.
  Feel-first; headless proves binding, only the owner's hands prove feel.
- **The player never sees a failure** — errors are captured loudly in the db, invisible on screen.
- **The economy direction:** V1 ships ZERO crypto; soft currency client-owned now, flips
  server-authoritative (auth scaffolding already built) when currency carries real value; SKR is a
  later, separate arc. Monetization = rewarded-ad income paths, never a wall.
- **The architecture:** HP B2B — bounded contexts, presentation never touches objects, the One
  Model (entries + capabilities), data-only content ("data only always"), pooled by default.
  Deep-dives: `docs/NORTH_STAR.md` (vision/GTM) · `docs/COMBAT_PIVOT_NORTHSTAR.md` (combat) ·
  `docs/ARCHITECTURE_NORTH_STAR.md` (does the foundation grow into the dream).
- **The operating dream:** the owner plays and rules; agents build in parallel lanes; every bug is
  a captured line; every system self-reports; the fleet + web bots verify before she ever has to.

## Latest (2026-08-14) — board reconciled, three hollow-assertion bugs fixed, dungeons re-baked
- **Branch `wip/village2-and-f8-tickets`, HEAD `e9c93415`, tree CLEAN, local == origin +5 UNPUSHED** (push
  held for the owner). Save schema still **v38** (`SaveSchema.cs:41`). Gates over the settled tree, read off
  the markers: `Builds/gate-postbake.log` → `COMPILE_GATE_OK` (0 `error CS`) ·
  `Builds/regression-postbake.log` → **`REGRESSION_OK 159/159 suites`**.
- **⚠ THE BOARD WAS LYING ABOUT 13 TICKETS.** `BOARD.html` is derived from the `**Status:**` lines, so it
  was routing agents to rebuild finished systems. **12 WOs had SHIPPED in git while still reading READY or
  "awaiting batch-gate + commit"** — 965/967/968/969/970/971/972/1014/1015/1016/1017/1019, each now citing
  its sha. **WO-1020 is bannered SUPERSEDED by WO-972**: both were minted off the SAME owner capture (F8
  seq 2327) — the UI seat minted a duplicate while the CLI seat shipped the fix. Board moved Ready 511 →
  499, Done 235 → 246, Unlabeled 0. ⚠ **No `.RESULT.md` was fabricated** (RULES 68) — what was verified is
  the shipping commit, not the behaviour, so the RESULT debt stays on the books.
- **⚠ THE DUNGEON RE-BAKE REVERTED AN OWNER RULING, AND THAT IS THE TRANSFERABLE LESSON.** WO-957 changed 13
  `"Extract"` labels to `"Leave"` **in the emitted LAYOUTS** — but layouts are GENERATED from the graphs, and
  the graphs still said `"Extract"`. `DungeonBaker.cs:1703` reads `IsNullOrEmpty(e.label) ? "Leave" : e.label`,
  so the code default is right but **an authored label WINS**. Proven by capture: the first bake stamped
  `label='Extract'` **×15**, `label='Leave'` **×0**. Fixed at the layer that owns it — the 13 labels are now
  `"Leave"` in the three content **GRAPHS**, both dual copies. Re-baked: `label='Leave'` ×13, the only
  remaining `Extract` being the two control fixtures. **This is the "a builder-only row is silently dropped
  by the next regenerate" class applied to an owner pin — fix the SOURCE, never the generated output.**
- **Bake result:** `COMPOSE_ALL_OK 7/7`, zero mate failures, **5 PathComplete**. The 2 PathPartial are exactly
  `dg_descent_probe` + `dg_stair_rig` — the WO-930 control group, deliberately still on the old pair model.
  Layouts are now `version 2` and every one emits `exitRoomId` — but as the **`entry` FALLBACK**, so WHERE the
  one true exit sits is still an owner design pick. Scenes are BINARY (batchmode `SaveScene` ignores
  ForceText) — verified NOT corruption: SerializedFile header + `6000.4.8f1` present, and git reports
  `Bin -> Bin`, i.e. they were already binary before this bake.
- **NEW WOs minted (banner bumped in the same edit each time, next free = 983):**
  **981** = `HeroProgression`'s starter latch is **not persisted — it is INFERRED from hero level** at
  `RestoreFromSave:202` on the assumption a hero past level 1 already got the gift, *which is exactly what
  WO-977 disproves*; so WO-977's retry holds in-session only. §B: the per-level grant at `:259` silently drops
  a point on a null `SkillSystem`, **every level**. **982** = `GraphDungeonComposer` **emits to StreamingAssets
  ONLY**, so every bake silently drifts the dual copy and **Resources — the copy that WINS at runtime — keeps
  the stale one**. ⚠ **This is the ROOT of the 08-08 incident `5f0e23aa` treated as a one-off**; the file was
  fixed, the mechanism was not, and it reproduced across all 7 layouts the next time anyone baked. Nothing
  catches it — `RoomForgeRegression.cs:162` is a hardcoded 3-file list with no `dg_*` in it (audit F24).
- **Three hollow-assertion bugs closed** (from `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`): **WO-977**
  starter skill points (grant first, latch on a MEASURED `AvailablePoints` delta) · **WO-978** four economy
  callers logged the amount *requested* as though it were *credited* — now report measured before/after
  deltas, **plus** a latch-before-grant in `DailyQuestRewardBridge:119` that could mark a daily permanently
  claimed having paid nothing · **WO-979** — ⚠ **its stated premise was REFUTED**: `Bind`'s `hud` parameter was
  never dereferenced anywhere, so wave feedback was never broken; only the trace was. Seam deleted, not "fixed".
- **⚠ 159/159 WAS GREEN ON A TREE WHERE AN OWNER RULING HAD BEEN SILENTLY REVERTED.** The suite count did not
  move before or after the bake that flipped 13 player-facing labels back to "Extract". Not one of 159 suites
  noticed. **The suite is a RATCHET, not a reviewer** — it locks known invariants and cannot read new code.
  This is the audit's own §0 shape ("every gate asserts a thing EXISTS, almost none assert it is CONSUMED")
  demonstrated live, and it is the argument for an external read on state/money/latch changes.
- **WO-980 ruled a DEFECT, not atmosphere** (opened the PNGs): in `docs/proof/2026-08-10-dungeon-headed-AFTER-camera-fix/`
  the hero is clipped at the bottom edge and rendered BEHIND the Talk/Bag buttons, and ~28% of `01_idle` is
  void above the room. **Geometric, so it needs neither the owner's eyes nor a colour call** — but the FIX
  needs a measured headed capture, not a constant picked off a screenshot (`DungeonCameraProfile`:
  `CameraHeight 1.9` / `CameraDistance 3.2` / `LookAtHeight 1.5`). Same capture is WO-973's required first step.
- **The "three tickets, one plume" collapses to TWO** — traced at source: `AmbientAuraPolicy.WithheldAmbientAuraKey`
  is the single literal `"TreeofLifeAura_Aura"` (FireFlies, the Heart tree), which **WO-1002 genuinely closed**.
  `Poi_NodeAura` is a DIFFERENT key → `Magic circle sun loop`, compared by exact key, so it is **never withheld**
  and still plays on every POI beacon. **WO-946 is the live one** and is now bounded: retag, or use the policy's
  existing `ShrinkInsteadOfWithhold` lever. ⚠ **WO-966 deliberately NOT touched** — the overnight report pins
  the dungeon −90 root yaw as untouchable until ruled; two facing systems tuned against each other manufacture
  a third bug.

## Latest (2026-08-10) — the wave-3 settle: 12 lane commits, 143/143 suites, five honest partials
- **Anchor still `CANON_GROUND_TRUTH_2026-08-09.md`** — ⚠ its header is now WRONG on three facts
  (it claims HEAD `19a50616`, "NOT PUSHED", "63 commits ahead"). Read the tree, not that header.
  Branch `wip/village2-and-f8-tickets`. **Save schema v38** (`SaveSchema.cs:41`).
- **The 2026-08-10 morning wave's in-flight lanes are CLOSED OUT.** Three lanes had died mid-write when
  the session expired and the tree did NOT compile: `GearAura.cs` (WO-959, helpers present, call sites
  fine), `EndStateView.cs` (WO-952, four call sites left on the old arity — `error CS7036`), and
  `DungeonExitInteractable.cs` (WO-957/1007/1008, two helper methods referenced but never written —
  `error CS0103`). All three completed by the committer, then gated as one tree.
- **Gates over the settled tree, read off the markers:** `Builds/gate-settle4.log` → `COMPILE_GATE_OK`
  (zero `error CS`) · `Builds/regression-settle3.log` → **`REGRESSION_OK 143/143 suites`** ·
  `Builds/ui-capture-settle.log` → `UI_CAPTURE_OK 62` + `UI_CAPTURE_FIDELITY_OK 44`. ⚠ The
  `UI_GEOMETRY_FAIL x16` in that capture is **WO-941's pre-existing RumorBoard/RealmMap baseline**, not
  new — and **no EndState case is in the capture set**, which is exactly WO-952's missing deliverable.
- **The suite count moved 136 → 143** because seven wave-3 oracles were finally REGISTERED:
  `[barracks-blanktown]` `[echo-hollow-route]` `[harvest-drip]` `[hostile-green]` `[dungeon-cam-958]`
  `[gear-aura-carry]` `[armor-store-window]`. Each lane authored its oracle and left registration to
  the committer on purpose — `DataRegression.cs` is lane-fenced. **Never restate the count; read the marker.**
- **Two reds were found and fixed on the way, both real:** `GuidePointer.cs` (WO-1012) hand-rolled two
  `Image` widgets and tripped the `[ui-obsidian]` HardFailOnNew ratchet — now built through
  `ElarionUiKit.AddImage(rounded:false)`; and `Dungeon/Exit/dungeon_texture` is a `Resources.Load`
  literal with no asset (the KayKit kit is gitignored) — registered as tracked debt in
  `HudUiRegression.MissingResourceBaseline`, since the runtime path already degrades loudly and visibly.
- **SHIPPED + RESULT-filed (7):** WO-950 blank-town drillmaster/teach/phantom-footprint · WO-951 Echo
  Hollow opens the roster · WO-953 harvest "+N" pops through the damage-number pool + gated-faucet
  honesty · WO-956 hostility off the red/green axis · WO-958 dungeon camera in tight rooms · WO-959
  weapon auras only while DRAWN · WO-960 armor-store locked-preview ladder · plus **WO-1012** tutorial/
  FTUE redesign (RESULT filed).
- **⚠ FIVE HONEST PARTIALS — still READY, remaining scope written into each WO body** (a `.RESULT.md`
  forces the board's Done bucket, so none was filed): **WO-952** the geometry fix landed but its capture
  case + `COMPRESSED`-absence oracle do NOT exist · **WO-957/1007/1008** the code landed and the owner's
  "Leave" relabel is now in the data (13 labels, 3 content layouts, both copies byte-identical), **but
  the dungeons have NOT been re-baked** — `_isTrueExit` is a `SerializeField` on BAKED objects, so
  nothing is on screen until a re-bake **in an isolated worktree** · **WO-949** respawn-in-town and the
  3 founding potions landed, the "teach the cost of dying" deliverable did not.
- **WO-85 NEVER STARTED** and now says so: grass + roads already shipped at `cc24da5a`/`bfacf0b3` and
  nothing has touched them since, so the lane is *"why does the shipped terrain not read"*, not "add
  grass" — and **value contrast must carry it** (hue alone is invisible to the owner).
- **Owner directive added to CLAUDE.md §11:** *the pipeline never idles* — the agent pool tops up on
  every lane completion; pin-blocked tickets park with their pins surfaced; one gate, one committer.
- **Owner pins still open:** WO-954 hollow models · WO-947 four cost-basket calls · WO-917 dodge glyph ·
  WO-1013 Arcane Tower/Spire naming · D8 Walls tab · WO-956's deuteranopia risk on the new body tint ·
  WO-960 shelf depth · WO-959's drawn/sheathed mapping.

## Latest (2026-08-09) — the 08-08 ship day: machine unblocked, stairs SOLVED, store re-gated
- **⚡ EVENING-2 WAVE (2026-08-09 ~21:00-23:00, this seat) — the WO-1010 defect-pass close + the Sylas fix.**
  Owner F8s (new product folder `LocalLow\DeNelle\Echoes of Elarion` — **the F8 daemon was watching the
  OLD folder; restarted on the corrected script**, her flags now ping again): (1) *"Sylas is coming
  through as a blink"* — **FIXED**: `HeroBodySwapper.Start()` now probes `Resources/Heroes/<slug>` FIRST
  for non-Knight classes (Ranger.fbx/Mage.fbx were git-TRACKED since `f18b66b4` but unreachable — the
  Blink base load was terminal-on-success); (2) *"This screen is not correct"* — the WO-1010 §7 pass
  closed: D17 sprites live (element/check+rotate authored), D19 seating consumed, always-on touch D-pad
  retired (its reflection seam deleted), ONE skip, P3 hint line, PICK dock 540→410 band-tightening.
  Gates `COMPILE_GATE_OK` + `REGRESSION_OK 133/133` + `UI_CAPTURE_OK 62`/`FIDELITY_OK 44`, PNGs opened
  vs `UI_REVIEW/build_ui_target_wireframe.html` (owner re-pinned it tonight). New WOs off the banner:
  **941** (pre-existing RumorBoard/RealmMap `UI_GEOMETRY_FAIL x16`), **942** (capture-case gaps);
  UI seat minted **WO-1012 tutorial/FTUE redesign** (+ wireframes) — D16's full rework lives there.
  Open: **D8 Walls-tab owner ruling** (conflicts with the 07-13 ruling), tester re-test, felt-verify.
- **Anchor = `CANON_GROUND_TRUTH_2026-08-09.md`** (supersedes 08-08, bannered). Branch
  `wip/village2-and-f8-tickets`, **HEAD `07b756b6` (2026-08-09 23:00), PUSHED 2026-08-10 ~10:12 —
  local == origin** (the 68-commit 08-09 wave; the earlier "HEAD c8320434 / 30 commits" reading was the
  08-08 point-in-time state). ⚠ 2026-08-10: the 5.1 GB stale Grok worktrees under `~\.grok\worktrees`
  were deleted (verified no unique work); the tree carries the 08-10 morning fix wave uncommitted while
  it gates (WO-931 · death-pin rebase · battle-music gate · WO-945).
- **Gates last emitted** (read off the marker files, never off this line): `Builds/gate-ship3.log` 19:36 →
  `COMPILE_GATE_OK` · `Builds/regression-ship3.log` 19:38 → `REGRESSION_OK 130/130 suites` ·
  `Builds/ui-capture-ship.log` 14:30 → `UI_CAPTURE_OK 44`. ⚠ **`Builds/test-results-EditMode.xml` is
  930/930 green but STAMPED 2026-08-04 — five days stale; do not cite it as current evidence.**
- **✅ THE MACHINE BLOCK IS RESOLVED.** Rebooted 2026-08-08 08:07:21; commit charge **45.7 GB of 127.8 GB**,
  11.9 GB physical free, no Unity running. **Windows EXE built 08-08 14:33; Android APK 08-08 20:00
  (572,202,338 bytes); Firebase ran.** ⚠ **The WebGL / web-deploy step NEVER RAN** — `Builds/WebGL` is
  still dated 2026-08-05 and there is **no `Builds/webgl-chain-status.txt`**. That is the open rail.
- **★ THE DUNGEON STAIRS ARE SOLVED — the whole PathPartial hunt is CLOSED.** WO-930 shipped the one-room
  stairwell: `3ab1bfb6` (**first floor-to-floor `PathComplete` in project history**) → `e7163c9c` (skinned,
  0 bad surfaces) → `5f0e23aa` (candle lights + **a caught RED gate: `dg_sunken_vault.json` dual-copy
  drift, Resources held the OLD 17-room layout and Resources WINS at runtime**) → `cb092b7f` (**all 4
  content dungeons PathComplete, 12 descents, 0 mate failures, 14/14 dual-copy parity**;
  `dg_descent_probe`/`dg_stair_rig` left on the old model as controls) → `51a89364` (`RoomPrefabMeta` on
  `StairwellRoom` — the overlap gate had been measuring a **20x10 m room as one 10 m cell**).
  **ROOT CAUSE = STAIR YAW:** `GraphDungeonComposer.SolveMate` hardcoded `yaw = 0f` on vertical sockets, so
  only a Delta of 180 landed the flight in the floor hole. **It was never a property of the stair** — which
  is why four rounds of bucketing the stair's scalars all came back negative. The 08-08 anchor's
  "dump navmesh triangles next" guidance is DEAD; its killed-hypotheses table survives as history.
- **⚠ HEADLESS GATES CANNOT SEE ORIENTATION (transferable).** `70a86c17` **reverts** `bb6dc010`:
  `SkinOptions.PreservePrefabRotation` applied to ALL structures **laid the whole town on its side**
  (13 catalog rows carry a manual -90 that composes to 180), reproducing only on the **dungeon → town
  return path** via `BaseLayoutLoader` — every marker green throughout. The narrow fix is `439e03ee`: a
  per-catalog-row **`RepoProps.preservePrefabRotation`** (default false, **exactly one opt-in:
  `tower_ground_archer`**) with `StructureFactory.OptsFor` as the single reader unifying
  `Create`/`MeasureUprightFootprintMetres`/`GhostPreview`. ⚠ **Still live:** `Resources/Structures` holds
  both a `.fbx` and a same-stem `.prefab`, so `Resources.Load` is **ambiguous**.
- **⚠ SECURITY RE-GATE — `FeatureFlags.RealmStorePurchase` is back to `defaultOn: false` and LOCKED**
  (`576601e3`). `StubWalletProvider` has **NO `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard**, ships in every
  player, fabricates a wallet + a **2000 SKR mock balance** + a base58 signature, and `ApplyPackContents`
  then **grants the pack for ZERO payment** while firing `purchase_completed` with the fake txSig.
  **The submitted store build had a tappable Buy button.** = **WO-931 — IMPLEMENTED 2026-08-10 (option b,
  owner-picked): runtime refusal at BOTH `WalletService.Pay` and `PayFlat` seams** (stub short-circuit +
  `IsRealSigningWallet` belt; loud `FlowTrace.Fail` refusals; regression cases in
  `WalletProviderSelectionRegression` §8; precondition 3 of 3 recorded SATISFIED in the flag's
  DO-NOT-TURN-ON block — **preconditions 1 and 2 remain OPEN, the flag default did NOT move**).
- **Legal + publishing:** `640bfc1c` sets `productName` → **"Echoes of Elarion"** (installs under the store
  listing name). `c8320434` authored `docs/TERMS_OF_USE.md` and hosts it verbatim at `site/terms.html`, live
  at `https://echoes-of-elarion.vercel.app/terms` (verified 200), linked from landing nav + footer;
  governing law **Texas**; ⚠ **no arbitration / class-action / jury-trial waiver — deliberately left for the
  owner's attorney.** Publishing scaffold under `publishing/` + `tools/store_previews_resize.py`.
  ⚠ **TWO OPEN FLAGS:** (a) **`PRIVACY_POLICY.md:87-89` has ONE FALSE SENTENCE on a LIVE page** — it
  describes an Ad button that "grants that time saving immediately without presenting any advertisement",
  and **that button is now ABSENT from the UI entirely** (the core no-ads claim is verified TRUE; only the
  explanatory sentence is stale). **Do NOT edit it — live legal copy is the owner's/attorney's call.**
  (b) **`docs/PUBLISHING_STEPS.md` Rail 1 is OBSOLETE** (bannered): `dapp-store-cli@1.0.0` has **no
  init/create/validate/publish** — the whole surface is `dapp-store --apk-file ... --whats-new ...` — and
  the app must ALREADY exist in the portal with an App NFT. Publisher + app are created **in the web portal
  with a browser wallet**; `publishing/config.yaml` is the verified **paste-source** for that form.
- **⚠ `tools/webbot/` WAS DELETED OUTSIDE GIT.** All four files (`canvas-probe.js`, `introtest.js`,
  `package.json`, `webbot.js`) are **present at HEAD**, **no commit ever deleted them**
  (`git log --diff-filter=D` empty), they are **not gitignored**, and the directory is **absent on disk**.
  This is the Playwright web-build self-test rig. Restorable with `git checkout -- tools/webbot/` —
  **NOT run; it is an open decision for the owner.**
- **Dev tooling out of the shipped player:** `eeb2d389` flips `ff.devresourcetool` **OFF** by default and
  moves DevPanel under Settings (`PanelId.DevPanel` = 17, gated on `PanelRouter.IsRegistered`); `374ccd26`
  ships a **RELEASE desktop player** (verified `DeNelle.DevTools.dll` absent — 206 DLLs, was 207).
  ⚠ **TRAP: the flag flip did NOTHING on this machine** — `FeatureFlags.Get` reads **PlayerPrefs FIRST**
  and this box has `ff.devresourcetool=1` persisted from 08-07. A default change is not a state change on a
  machine that already answered the question.
- **Felt fixes:** `2f10f6ac` — auto-upgrade was handing **every level-2 knight a paid Forge
  `knight_flameblade` for free** (candidates narrowed to owned gear; tri-state ownership survives a
  `VillageInventory.EnsureLoaded` pre-load race). `763d1a60` — nameplates rendered literal
  **`[[missing:market]]` / `[[missing:jeweler]]`** to the player; forge/armorer duplicate; "Lumber Mill"
  renamed across catalog/quests/prefab.
- **⚠ F8 — ONE UNACKNOWLEDGED capture, seq 2248** (2026-08-08 13:17:10, `Main_Castle_Overworld`):
  `Cannot set the parent of the GameObject '[VFX_Harvest_Wood]' while activating or deactivating the parent
  GameObject 'Lumbermill'.` This is the **WO-929** class and WO-929 already names `HarvestAura.cs` — but
  **every proving line in WO-929 is `OutpostEnemy (...)`, a POOLED ENEMY.** This capture proves the same
  illegal `SetParent` fires from a **BUILDING**, so **a fix scoped to the pooled-enemy path is incomplete.**
- **WO board:** `0d75bc06` — an audit found **52 of ~91 WO statuses WRONG**
  (`docs/reference/WO_TRUE_STATUS_2026-08-08.md`); it also surfaced that **WO-884's VFX facade never
  existed**, **WO-898's `crystalsPerBracket` has 0 hits**, and **WO-875/877 were never attempted**.
  WO-930's own file said READY/SHIP-BLOCKING although it shipped (corrected); WO-927 is superseded by its
  own §0. **RESULT-file debt on the live arc: 921/923/924/925/926/927/928/929/930/931/1006/1007/1008/1009 —
  none exist, none fabricated.** ⚠ **Read the next-free WO off the `CLI_LANES_WO_NUMBERS.md` banner — never
  from a doc.** (The banner's own block table had gone stale against its header; the table row was corrected
  2026-08-09.)
- **★ FOUR LONG-STANDING CANON CLAIMS REFUTED AT SOURCE — all CLOSED, stop carrying them** (anchor §9;
  each verified line-by-line by this seat, and each corrected IN PLACE in its own section above):
  **(1) "THE SEAM"** — closed by WO-853; the raid-roadmap prerequisite is **satisfied**, so **WO-774.0 is
  no longer free to defer.** **(2) The "orphan third copy" of the gear catalogs** — `Assets/Data/Canonical`
  **does not exist**, deleted in `c55a5561`; it could not have shadowed the pair anyway because
  `LocalJsonCatalogSource.Read` probes only `Resources.Load<TextAsset>` then `streamingAssetsPath`
  (`LocalJsonCatalogSource.cs:33-52`). *(`CANON_GROUND_TRUTH_2026-07-22.md:193` §5.8 and two design docs
  are stale on this — deliberately not edited.)* **(3) `CatalogBootstrap.RegisterFallback` drift** — all
  three rows are now **field-equal**, including `tower_arcane_spire.visualTexturePath =
  "Structures/ArcaneSpire_Albedo"` (`CatalogBootstrap.cs:307`), so **the pure-white defect is CLOSED**; now
  guarded by `BuildEconomyRegression.cs:1191-1290` gate 12 `[fallback-parity]`. **(4) Dual-copy is
  HEALTHY** — swept 80 files per side, 77 paired, **only `weapons.json` + `armor.json` drift and both are
  the DELIBERATE owner gear ruling**; the 08-08 `dg_sunken_vault.json` drift is FIXED (both sides v1 /
  14 rooms); all dungeon layouts + graphs byte-identical.
- **⚠ NEW GAPS, all OPEN and UNCOVERED** (anchor §10): **three difficulty levers are computed and thrown
  away** (see the Adaptive line above) · **`DataWebRegression` iterates the StreamingAssets root only**
  (`:208` drift, `:356` version), so **a Resources-only file — the copy that WINS at runtime — is never
  drift- or version-checked**; verified Resources-only = `ad-creatives.json`, `ad-placements.json`,
  `widget-params.json`, and **`widget-params.json` has no `version` field at all** · the version check is
  **presence + cross-copy agreement only, never "a change bumps it"** — **24 catalogs had content changed
  with no version bump on their most recent commit** (worst: `enemies.json` +95, `en.json` +265,
  `themes.json` +369, `waves.json`, `abilities.json`) · **`RoomForgeRegression.cs:162`'s dual-copy gate is
  a hardcoded 3-file list containing NO `dg_*` layout** — including `dg_sunken_vault.json`, the exact file
  that drifted, so the next drift ships the same way · **`DungeonBaker` probes ONE path**
  (`placedOrder[0] → placedOrder[last]`, `:432-445`) and is **log-only** — `SaveScene` runs unconditionally
  after a `PathPartial` (`:457-479`, `:490-494`), so a dungeon whose FIRST descent fails is
  indistinguishable from one whose last does, and reachability is gated by the first failure.
- **⚠ WO-930 did NOT delete what its spec said it would — and that is BY DESIGN.** `StairUp`/`StairDown`,
  `IsVertical`, `SEALED_VERTICAL`, the floor holes and ceiling shafts are all **retained as a quarantined,
  gated CONTROL GROUP** (`DungeonMultiLevelRegression.cs:41-63`, explicit **"⚠ DO NOT DELETE"**).
  **`dg_stair_rig` and `dg_descent_probe` are TEST FIXTURES, not stale content or regressions** —
  `[graphs-converted]` asserts they STILL name the retired prefabs so a tidy-up cannot delete the control
  group by accident. Converted layouts, verified: `dg_bonecrypt`, `dg_ember_deep`, `dg_sunken_vault`,
  `dg_stairwell_probe`. The deletion is a future single-commit job (WO-930 §5).
- **⚠ `structures-catalog.json` is `version: 15`** (both copies identical, 29 entries, `_heightCadence`
  present). Any doc saying v6/v7/v8 is a stale point-in-time reading. **Read it off the file, not off a doc.**
- **Still open and CARRIED FORWARD** (the 08-08 anchor dropped these — see the 08-09 anchor §8): the VFX
  **ONESHOT pool saturates 40/40** (different pool, different reclaim path — **NOT closed** by the 08-06
  loop-cap fix) · the **absence** of `SKIPPED - active loops 20/20` across a full wave has **never been
  proven** (owed a fleet run) · **`VFXType` serialises by ORDINAL, appends only** and `Build()` does
  `entries.arraySize = rows.Count` (a builder-only row is silently dropped by the next regenerate) ·
  **WO-910 READY FOR OWNER RULING** (31 dead nodes / 40 player-reachable Ranger+Mage talents; Ranger 1
  usable of 20, Mage 5, both tier-4 capstone rows dead) · **hero select SELF-SKIPS** when the save records
  a class (test a class change with New Game / Play Intro, never Continue) · **`api/` is PREVIEW-only** and
  prod's nonce endpoint has **no CORS** (promotion = owner's call) · still **colour-only and OPEN**: the
  build placement ghost and the hero health bar.

## Latest (2026-08-06) — the VFX night: two P0s, Ranger/Mage unlocked, one height cadence
- **Anchor = `CANON_GROUND_TRUTH_2026-08-06.md`** (supersedes 08-05, bannered). **HEAD `1534dffb`, local is
  43 commits AHEAD of origin — NOT PUSHED.** ⚠ Working tree NOT clean: `ProjectSettings.asset` carries a
  newer APK stamp (`2026.08.05.312459` / code `312459` vs the committed `312348`) and
  `WorkOrders/WORK_ORDER_885`–`894` are untracked. Save still **v36**.
- **Gates last emitted:** `COMPILE_GATE_OK` + **`REGRESSION_OK 120/120 suites`** + `VFX_LOOPFLAG_OK` +
  `VFX_ART_MIRROR_OK` + `PARTICLE_PACK_VFX_BUILD_OK` + `BOSS_FIREBREATH_BUILD_OK`.
  ⚠ **The count moved 117 → 118 → 119 → 120 in eight hours. Read it off the marker, never off a doc.**
- **THE PATTERN (transferable):** six defects, one shape — **a flag authored BY HAND instead of DERIVED
  from the thing it describes.** `IsLoop` · the "self-contained" tracked VFX prefab · `HeroTalentNodeDef.Hidden`
  · `TalentStrategyRegression.HiddenTrees` · the capture harness resolution · `RegisterFallback`.
  **Derive it — and PIN the owner's standing rulings ABOVE the derivation with their reason**, because the
  prefab is the authority on what the art *does*, not on what the game *should do*.
- **⚠ P0 — THE VFX LOOP CAP LEAKED DRY.** `IsLoop` was a sticky checkbox `VfxCasterWindow` force-set true
  for role Projectile/Aura; **53 of 122 picks were wrong.** A loop row never returns its slot (the only
  reclaim frees DESTROYED hosts; pooled objects are never destroyed), **cap 20**. Archer + ballista fire
  `PP_MuzzleFlash` and discard the handle, so after ~20 shots a tower renders **no projectile** and starves
  the Tree of Life aura + every POI marker. **Six F8 sessions on two dates show `SKIPPED - active loops
  20/20`**, naming five victims that were themselves the mis-flagged culprits. Both generators now DERIVE
  the flag from the art (rule: `main.loop` AND a positive rate, emission enabled; authority = the root
  system UNLESS it cannot emit). ⚠ **Not yet proven: the ABSENCE of the message across a full wave — owed a
  fleet run.** ⚠ **A separate, unbundled signature: the ONESHOT pool saturates 40/40** in three captures.
- **⚠ P0 — THE TRACKED VFX PREFABS WERE NOT SELF-CONTAINED.** `CopyAsset` duplicates the **prefab only** —
  never its materials/textures/shaders/meshes/animations. **27 of 28 prefabs, 183 references, 73 distinct
  assets** pointed into gitignored art (magenta/untextured/invisible on any machine without the packs).
  **Now 0**, verified twice; **~23.85 MB mirrored, deduped** to `Assets/Resources/VFX/_Shared/`; enforced by
  a regression that fails on ANY dependency in a gitignored root (`VFX_ART_MIRROR_OK`). ⚠ Two pack
  MonoBehaviours could not be mirrored and were **stripped — `Casting_Fire` no longer spawns a projectile.**
  ⚠ **Lana Studio is NOT gitignored** (only its URP upgrade subfolder is).
- **⚠ RANGER + MAGE UNLOCKED, TREES EMPTY.** `ff.knightonly` defaults **OFF**; roster Knight/Ranger/Mage via
  `DeNelle.Core.State.PlayableHeroes` (**Cleric deliberately out — no authored kit**;
  `ff.knightonly`=1 restores the solo-Knight pivot). Emptying `TalentStrategyRegression.HiddenTrees` — which
  had hardcoded `{ranger,mage}` so guard G3 had **NEVER** audited 40 player-reachable nodes — surfaced **31
  real dead nodes: Ranger ONE usable talent of 20, Mage five, both tier-4 capstone rows dead.** Knight (32)
  and shared (9) green. `hero-talents.json` **UNTOUCHED, md5 unchanged**; the 31 are a dated ratcheted
  baseline (a baseline id that stops reporting dead ALSO fails). **WO-910 = READY FOR OWNER RULING.**
- **⚠ LATENT INVISIBLE-HERO P0, FIXED.** Ranger/Mage have **no FBX**; both fell through to a Blink base body
  and **`Assets/Blink` is gitignored** — on a fresh clone the terminal fallback **returned without
  instantiating anything** after `Start` had destroyed the placeholder. Both bail-outs now build a tracked
  **KayKit** body. ⚠ **Hero select SELF-SKIPS when the save already records a class**
  (`HeroSelectController.OnEnable` → `SceneRouter.GoCastle()`), so testing a class change needs **New Game /
  Play Intro**, never Continue.
- **⚠ ONE HEIGHT CADENCE** (owner ruling, recorded in the data as `_heightCadence`, catalog **v6 → v7 → v8** — 7 = the archer `0ac59581`, 8 = the cadence `d42e2817`, verified at HEAD):
  **1.25** landmark · **1.2** towers (4.8 m, 2.778 m across = 49.9% of a house) · **1.0** building base ·
  **0.75** siege · **0.35** decoration. **WALLS DELIBERATELY EXCLUDED** — the fit is uniform, so narrowing a
  wall **opens PATHABLE GAPS in saved wall runs** and shrinks the navmesh obstacle with them; needs a
  measured audit + a migration decision. **`collector_farm` at 1.4 is a COMPENSATION, not an outlier**
  (windmill blades inflate the Y bounds). **`repo.visualHeight` is DEAD for runtime placement** — deprecated by
  WO-764, authored zero times, no longer read by `StructureFactory.EffectiveVisualHeight` (one legacy
  EDITOR reader survives in `RaidBaseGenerator.cs`). *(This SUPERSEDES the "tower override 7m" line further down this file.)*
- **ACCESSIBILITY — the low-health tell is no longer a colour.** Severity drives **pulse rate 0.85 → 3.2 Hz**,
  **guttering depth** (trough to a tenth of authored density) and simulation speed; **below a quarter health
  the RECIPE SWAPS** to a candle gutter — a shape change, not a hue change. The vignette stays as a
  **redundant** cue; colour-ONLY was the bug. Mutual exclusion is **structural** (one handle field). Still
  colour-only and OPEN: **the build placement ghost** and **the hero health bar**.
- **⚠ THE UI CAPTURE HARNESS WAS GEOMETRY-BLIND** until `7e05e6d3` — only `canvas.scaleFactor` was rewritten,
  never `Screen.*`, so **the resolution in a PNG filename was a LABEL, NOT A LAYOUT** and two panels shipped
  broken behind a green marker. **2670x1200 had never been rendered in this repo.** Several 08-05 UI commits
  are **not** geometry-verified. ✖ **`ClampMinTouch` was CHECKED AND RULED OUT** at three sites tonight
  (bands resolved 117 / 116.7-130.6 / exactly 112.0 px) — check the arithmetic before naming it.
- **⚠ VFX is a CONNECTION problem, not an art problem:** **26 of 79 enum values are wired to real art with
  ZERO gameplay callers**; six whole tracked Lana categories sit at **0% usage**; a GUID sweep of **8,795
  prefabs and 156 scenes found ZERO VFX scripts attached anywhere** (which is what makes `EliteVFXController`
  dead three separate ways — **its 0.7 boss death shake has never fired in the shipped game**).
  ⚠ **`VFXType` serialises by ORDINAL, not name — appends only.** ⚠ `Build()` does
  `entries.arraySize = rows.Count`, so **a row written only by a builder is silently dropped by the next
  regenerate** and the effect falls back to something that still looks like it works.
- **Session ledger (known dictionary):** `docs/reference/SESSION_INDEX_2026-08-06.md` — every defect with
  its proving line, every **refuted** belief with the evidence that killed it, the owner rulings, the open
  items. Earlier half of the same day: `docs/reference/DEFECT_INDEX_2026-08-05.md` (frozen).

## Latest (2026-08-03) — the solo-night wave + the FIRST live server verification
- **Anchor = `CANON_GROUND_TRUTH_2026-08-03.md`** (supersedes 08-02, bannered). **HEAD `56be3ae2`, pushed,
  local==origin, working tree CLEAN.** Gates: `COMPILE_GATE_OK` + **`REGRESSION_OK 104/104 suites`** +
  **`TESTS_OK 912/912` zero reds** + `UI_CAPTURE_OK 28`. Save still **v36**.
- **⚠ The 08-02 anchor pinned `e60b19e5` and 17 commits landed after it** — three boot docs inherited the
  stale sha AND the stale 884/884 count. Read the count off the marker, never off a doc.
- **SERVER, PROBED LIVE (not reported) — this corrects 08-02:** **`auth_nonces` EXISTS**; prod
  `GET /api/auth/nonce` returns **HTTP 200** with a real nonce. ⚠ but the table's only rows were minted by
  the probe — no real client has ever used it. **`api/` is deployed to PREVIEW only** and the game
  hardcodes the prod domain, so the overnight server work is unreachable; **and prod's nonce endpoint has
  NO CORS + `OPTIONS` 400**, so a browser blocks the WebGL wallet rail regardless of the client. Prod is
  proven to be running OLD `api/` code (prose error shape + missing `bugreports`/`authrejects` views).
  `player_data` = **2 test rows, newest 2026-05-31**; `bug_reports` = **0**; `analytics_events` = 80,749
  (web tracing flows fine). **Promoting `api/` to prod is the highest-value action on the board — owner's call.**
- ~~**THE SEAM (verified from code):** nothing can damage a wall, gate or enemy tower...~~
  **⚠ REFUTED + CLOSED 2026-08-09 — do NOT carry this forward.** WO-853 closed the seam from both ends and
  it no longer exists at HEAD. **Dual implementation** (`... : MonoBehaviour, IDamageable,
  IDamageableStructure`) on `Village/Walls/WallSegment.cs:53`, `Village/Gates/Gate.cs:67`,
  `Village/Buildings/DefenseTower.cs:57`, `Village/World/Camps/RaidSpire.cs:61`; **mask widening on BOTH
  troop entry points** so a factory-supplied Enemy-only mask cannot strip it — `TroopController.cs:189`
  (`SetEnemyMask`), `:201-202` (`WithStructureLayer`), `:394` (`Awake`) — with walls staying on the
  **Structure** layer deliberately (that layer is the tower LoS blocker mask; relayering them onto Enemy
  would make towers shoot through walls again); **collider buffer 48 → 128** (`:104`) so wall panels cannot
  crowd enemy colliders out of `OverlapSphereNonAlloc`'s arbitrary-order truncation. Covered by
  `TowerWallLosRegression`, `StructureTargetableRegression:440`, `DefenseTargetableRegression:136`,
  `RaidArenaShapeRegression:363`. **⚠ Consequence: the raid-roadmap prerequisite is SATISFIED, so the
  WO-774.0 drop-and-watch-vs-led ruling is NO LONGER FREE TO DEFER** — it was parked because the seam
  blocked both roadmaps, and that reason is gone. *(verified line-by-line 2026-08-09)*
- **Overnight (15 commits):** enemies actually reach you (own-wounded targeting + `_stopTightenedForHero`
  surviving pooling); pooled-enemy statues fixed; **raids rescaled 2.4% → 20/49/60% of floor with a spire
  objective** (raid walls had NO colliders; no raid scene had a hero spawn point); raid troops animate +
  aren't magenta; unarmed level-1 Mage fixed; defense cap unified at **0.90**; tutorial Hollow step
  completable; **the check-in gate had never run at all** (didn't parse under PS 5.1); `DeNelle.Core.Difficulty`
  → **`DeNelle.Core.Adaptive`** (it shadowed the persisted enum).
- ~~**Adaptive difficulty is INERT** — `WaveManager` records none of the six fields, so every read returns 1.0.~~
  **⚠ HALF-REFUTED 2026-08-09.** All six `EncounterSample` fields **ARE** measured and recorded: six-arg
  ctor + `DynamicDifficulty.RecordEncounter` at `Village/Waves/WaveManager.cs:2471-2484`, armed by
  `BeginEncounterTelemetry` at `:2341`, consumed at `:1761-1762` and `:1876-1877` via `e.ApplyDifficulty`.
  **The REAL defect is narrower and worse-shaped (NEW, HIGH, UNCOVERED): three of the five multipliers are
  computed and have ZERO gameplay consumers** — `EnemyCountMultiplier`, `BossHpMultiplier`,
  `BossDamageMultiplier` (`Core/Difficulty/DynamicDifficulty.cs:119,122,125`), no reader anywhere outside
  `Core/Difficulty` (the only external hits are `DynamicDifficultyRegression.cs:276-292` and
  `Assets/Tests/EditMode/DynamicDifficultyTests.cs`, and **both call `DifficultyMath.*`, never the live
  `DynamicDifficulty.*` properties**). **So every boss wave ignores the softer boss curve the math file
  exists to produce, and the count signal is dead.** `DynamicDifficultyRegression` proves the math/oracle
  only — no `WaveManager` reference, no consumption assertion — so the levers can be correct and unwired
  with the suite green.
  ⚠ **Namespace vs. path — both are right, do not "fix" either:** the folder is
  `Assets/_Modules/Core/Difficulty/`, but all six files in it declare `namespace DeNelle.Core.Adaptive`.
  The 08-03 rename moved the **namespace** (it shadowed the persisted enum) and left the folder alone.
  *(verified 2026-08-09)*
- **Canon health:** `docs/MASTER_CATALOG.md` (the INDEX) was NOT refreshed by WO-836 — only the 19 area
  files were; the index still says Blaise/party-of-4/v30/next-WO-412. **Use it as a filename list only.**
  The area files are code-true as of `b77a178e`, not HEAD. `docs/reference/REGRESSION_COVERAGE_MATRIX.md`
  is two Sundays stale (still says "16 suites") — use its proposed assertions, never its counts.
  RESULT-file debt = **33**, not 31.

## Latest (2026-08-01) — post-reboot ship wave: Realm Map + KayKit NPCs + Queues ruling + release train
- **Anchor = `CANON_GROUND_TRUTH_2026-08-01.md`** (supersedes 07-26, bannered). **HEAD `ac0a52e3`, pushed,
  local==origin.** Gates: `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK 23` (pixels eyeballed).
- **WO-818 ALL PHASES SHIPPED:** 12 KayKit NPC bodies tracked at `Assets/Resources/NPCs/KayKit/`
  (`KAYKIT_STAGE_OK 12/12`, Humanoid); `structures-catalog.json` **v6** dual-copy carries `repo.npcModel`
  on exactly 12 owner rows; `KayKitNpcBody` resolver = KayKit-first → People chain → capsule (one Warn,
  never blank); `NPC_MODELS` oracle pins the 12 verbatim. Body swap = one-word owner JSON retag.
- **WO-826 Realm Map SHIPPED:** parchment panel (Elarion gilt home + 5 fog regions from dual-copy
  `realm-map.json`), strict MVVM, HUD **Map** button (hidden until Onboarded, WO-825 R4), DevPanel entry,
  `REALM_MAP` oracle + 8 EditMode tests. Travel stubbed → WO-827. 825 program IN FLIGHT (827/828/829 next).
- **OWNER RULING: bar Queues button RETIRED** — the right-column **Builders chip** (QueueStatus band,
  above the resources dock) is the ONE Queues entry; calm(town) bar = **6 faces**
  (Build/Talk/Bag/Raids/Map/Quests⇄Upgrade). `ObsidianQueueRegression` 7c enforces the retirement.
- **ProjectSettings dynamic-batching RCA CLOSED** (`ac0a52e3`): reverter proven (twice-captured) to run
  INSIDE `BuildPlayer` after the pre-build set; `DesktopBuild` now re-asserts static=0/dynamic=1
  post-build (the WebGL exceptionSupport-restore pattern). Owner keeps dynamic=1.
- **Dungeon verified from a captured run** (owner-ordered log test): all 7 proving lines + the R-A1
  arena CharacterController guard green. Open: vitals 120/60 placeholder (770.10), placeholder props
  (770.8), `EnvTreeFix VERIFY FAILED 'Skeleton_Mage_Hat'` (minor, unticketed).
- **Release train:** fresh desktop exe (15:17) · Seeker APK built + Firebase App Distribution (testers
  group) + adb install · WebGL→Vercel PREVIEW queued (promotion = owner). Screenshot archive:
  `Builds\ui-capture-archive\2026-08-01\` (23 PNGs).
- **UI seat reconciled:** WO-830 (Echo harvest affinity+synergy: 6 unique affinities
  Wood/Iron/Food/Gold/Crystals/Repairs, 3 disclosed pairs, 1 HIDDEN tri-synergy) + WO-831 (2D emergence
  sprite beat) minted; `docs/qa/UI_REVIEW_2026-08-01.md` (20-panel real-pixel review) banked.
  **WO next-free: see the banner** (`CLI_LANES_WO_NUMBERS.md` is the SOLE authority — never copy the
  number into docs, point at it. As of 2026-08-02 two DISJOINT blocks are in use: the CLI mints the
  main line, the UI seat mints only from a reserved 860–899 block. Five collisions happened on
  2026-08-02 alone, every one caused by a mint that did not bump the banner in the SAME edit.)
- **Verified inventories (cite these):** FeatureFlags = 62 (⚠ XML summaries LIE on 12 defaults —
  trailing `//` comment is truth); save **v36** (`SaveSchema.cs:36`, WO-834 `everBuiltStructureIds`).
- **⚠ CORRECTED 2026-08-02 — the WaveDataTest line that used to sit here was FALSE and dangerous.**
  It read "EditMode reds live in `Assets/Data/Tests/WaveDataTest.cs` (wave-1 ruling open)". There are
  **NO EditMode reds** — the full suite is **884/884 green**. Those two tests were **STALE TESTS**,
  not an open question: the owner ruled smart-composition on 2026-07-30 (`_smartComposition:1`, so
  `waves.json` `enemies[]` batches are inert), and both tests were rewritten to assert the batches
  are EMPTY — a re-add now FAILS the gate. Leaving the old line here invited a session to re-open a
  ruling the owner had already closed, which is the exact failure §15 exists to prevent.
- **Queue ahead:** 827/828/829 (travel/minimap/biomes) · 821 timed research · 837 stockpile caps ·
  838 magenta troops (Phase-A probe first) · 848 restore Android stripping · 851 every-4th-wave boss
  waves · 861 remaining phases. **830/831/835/839/840/841/850/852/860 all SHIPPED 2026-08-02.**

## Latest (2026-07-30) — 12-agent SME fan-out + check-in sweep + WO-783 fix wave
- **Operating model (owner directive):** CLI = **GATEKEEPER**. Dedicated agents write requirements, write
  the tests, and produce **read-only implementation proposals**; the CLI verifies every proposal **against the
  tree**, runs the tests, gates, commits by explicit path, and **screenshot-verifies before anything reaches
  the owner**. Agent output is a proposal, never truth — two claims were REFUTED on verification this session.
  Memory: `cli-gatekeeper-agent-role-model`. *(2026-07-30)*
- **Check-in sweep:** 9 commits, tree clean. Found + fixed **5 folders with tracked contents but an UNTRACKED
  folder `.meta`** (Core/Enemies, Core/Jobs, Village/Troops/Data, both dungeon-graphs) — a GUID-regeneration
  hazard on the second machine. `Assets/UnityTechnologies/` (191 MB Particle Pack) gitignored per big-pack
  policy + logged in `tools/art/REQUIRED_PACKS.md`. *(2026-07-30)*
- **WO-783 fix wave — IMPLEMENTED, all three gates green** (`COMPILE_GATE_OK` + `REGRESSION_OK` +
  `UI_CAPTURE_OK`, pixels opened not just markers):
  - **Raid VICTORY now settles the army.** `ReconcileAfterRaid` had ONE caller (retreat) and `AddVeterancy`
    had **ZERO repo-wide** — *winning a raid was free*. One latched `RaidDeployController.ReconcileRaidEnd(stars)`
    now serves both exits; 3-star clears pay veterancy.
  - **Healer's Cottage is REACHABLE again.** It lost its `AuthoredPortal` row when the east portal was
    rerouted to `dg_starter_loop`, so the richest dungeon in the game (lore/mini-boss/chests/crafting) was
    dev-overlay-only. Third row added, SOUTH `(20,0,-140)` yaw 352 — the only one of the three seats whose
    ground is provably flat (the WO-468 cave corridor pins Y=0). ⚠ navmesh seating still needs a runtime line.
  - **`[ui-obsidian]` ratchet ARMED** (`HardFailOnNew=true`, 0 NEW) + a **namespace-qualified regex blind spot**
    closed that had been hiding `OutpostHub.cs` as a false "resolved".
  - **waves.json authored schedule is DEAD and now says so** — see the standing-truth line below.
  - Echoes-button safe-area inset 16 -> 54 ref px (~7 dp -> ~24 dp on the Seeker).
- **FPV camera: owner RE-AFFIRMED default-ON** (2026-07-30). `ff.dungeonfpv` stays `defaultOn:true`; the
  `PAIN_POINTS` §4 "keep only if felt-tested" gate is CLOSED. Two `DungeonCameraRig` headers that called FPV
  "a STUB with no independent look" and named over-the-shoulder the default were corrected.
  - **⚠ SUPERSEDED 2026-08-07 (WO-920).** `ff.dungeonfpv` is now **`defaultOn:false`** — the shipped explore
    camera is a **LOCKED over-the-shoulder** rig; FPV survives fully wired as an opt-in A/B (`ff.dungeonfpv=1`).
    The 07-26 default-ON was a workaround chosen *instead of* raising the ceiling; **WO-919 removed that premise**
    (composed rooms are now 4 m walls + a ceiling slab, relit dark), so the trade was reversed.
  - **⚠ AND THE SCOPE OF THAT FLAG WAS ALWAYS NARROWER THAN THIS LINE IMPLIES.** `ff.dungeonfpv` only reaches
    the **two hand-built** dungeon scenes that carry a `DungeonCameraRig` (`Dungeon_HealersCottage`,
    `Dungeon_FolksGranary`). The **composed `dg_*` dungeons and `KayKitChallengeOutpost` bake no camera and no
    rig at all** — their camera is the runtime `GameplayCamera (ensured)` + `DeNelle.Village.SmartMobileCamera`
    (`HeroControlEnsurer` L283-295), so the flag never applied to them. Their locked seat is
    `SmartMobileCamera.ApplyDungeonProfileIfNeeded`. Seat + clear colour for **both** pipelines now come from the
    one authority, `DeNelle.Core.World.DungeonCameraProfile`; the scene test is `HubScenes.IsDungeon`.
- **STANDING TRUTH — `waves.json` `enemies[]` batches are INERT.** `_smartComposition:1` in both live hubs
  means `WaveManager` GENERATES every wave's roster; only `countdownSeconds`, `boss`, `apexBoss` survive.
  **19 waves / 55 batches / 148 authored enemies are discarded every session.** Not a code regression (WO-362
  supersession was deliberate) — the *data* was authored 2026-07-11, ~4 weeks AFTER the batches went inert.
  A once-per-session `FlowTrace.Warn` now names it. **OPEN owner ruling (WO-783 D1):** which authority wins.
- **New WOs:** **783** (this wave) · **784** Echo lanes — canon's "3 of 4 stub" is wrong, **all four** are
  write-only, even Harvest bypasses the Core contract · **785** VFX survivability — **117 of 121** owner-tagged
  VFX rows point into gitignored packs with **no runtime fallback**. **782 RESERVED** (night-wrap capsule
  standee). **WO next-free = 786** *(superseded → banner, 832)*. *(2026-07-30)*

## Latest (2026-07-26) — dungeon+raid felt-test wave + Sunday housekeeping
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-26.md`** (delta over 07-22, which stays the deep module
  reference). Branch `wip/village2-and-f8-tickets`, HEAD `7dec0e07`, **local==origin — this wave IS pushed**
  (change from 07-22 push-HELD). Prod untouched. Save still **v34** (no new persisted fields this wave).
- **Dungeons = functional end-to-end loop** (enter → explore → read lore → fight with REAL win/loss → settle
  → leave → Village). Shipped: WO-770.1 (exit + boss back-door), .2 (correct-dungeon return), .3 (real
  victory/defeat carrier via `SceneRouter.PendingBattle.LastOutcome` — a lost fight ends the run), .3b
  (real-time `BattleArena.OnBattleEnded` → shared `SettleEncounter`; fixes the never-released combat lock),
  .4 (readable lore stones + code-built modal), .7 (toast layer + live Bryn dialogue), .9 (stale-read
  `OnEnable` clear). Plus DungeonHero sole-mover + taller camera + Bryn pill-hide. *(2026-07-26)*
- **Raid loop LOCKED to Teleport/Deploy** (COC model, owner 2026-07-26); walk-to retired as the raid loop
  (its `EnemyOutpost`s may return as a light overworld patrol side-activity). When raid work starts, set
  `ff.overworldencounter=0` + `ff.raidwalk` OFF first. WO-771 v2 is the build plan; **nothing built yet.**
  Reuse `RaidBaseGenerator` + `EnemyFactory→Enemy→TargetManager` combat; `IDamageableStructure` must move
  Village→Core; tower-fire is greenfield. V1 = PvE generated bases (skip the deterministic sim). *(2026-07-26)*
- **Firmed WO set (`docs/qa/`):** 770 (dungeon), 771 (raid v2), 772 (shared enemy system — classes/families/
  armor/weapons + `EnemyResolver`, fixes generic-skeleton bug), 773 (common Obsidian job queue). Validation:
  `docs/qa/dungeon-raid-validation-2026-07-26.md`. **772 is BLOCKED on owner ratifying `docs/enemy-codex.md`**
  (review-and-approve gate) — it blocks 770.11 + 771.13. *(2026-07-26)*
- **Non-dungeon felt fixes shipped:** enemies-out-of-castle + battle-mode BattleLock (`e05f92f7`), towers no
  longer shoot through walls (Structure layer + LoS, `2cb3c40d`), MagentaGuard catches Android compile-failed
  shaders (`386a932f`), loading overlay + standard bar (`4edf8dcc`/`7dec0e07`), gate-traversal teleport off —
  walk through the arch (`8c35332f`), collector buildings get vendor NPCs (`804a02a2`, Lever 1 in progress),
  Alchemy recipe scroll-fix (`8ca95735`). *(2026-07-26)*
- **WO next-free = 774** *(superseded → banner, 832)* (761–773 consumed; 770–773 are decimal-sub-order specs in `docs/qa/`). Ticket table:
  `docs/qa/SUNDAY_STATUS_2026-07-26.md`. §6/§7 catalog-drift housekeeping WO + CS-1 ring/amulet non-persist
  ticket still open. *(2026-07-26)*

## Latest (2026-07-22) — SME fan-out + canon refresh + branch hygiene
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-22.md`** (supersedes 07-19). A 17-agent read-only SME fan-out
  (code-verified) confirmed: **code healthy, gates green** (HEAD `148ab637`, local==origin, `REGRESSION_OK`
  16 suites/0 reds, save v34) — **the real debt is DOCUMENTATION DRIFT.** The `MASTER_CATALOG/<area>` sections
  are weeks stale; the 07-22 anchor carries the **§6 catalog-drift ledger** + **§7 comment-vs-code lies
  registry**. Key corrections: home hub = `Main_Castle_Overworld` (MergedWorld ON, one navmesh) not
  MainCastle_Hall; `ff.atbdungeon` doesn't exist (real gate `ff.dungeonrealtime`, dungeons route into
  BattleArena); 23 build scenes; ~70 catalogs; audio 5-group mixer never built (AudioSource-direct fallback only);
  HeroPortraits folder absent; deploy chain writes `CHAIN_DONE` on failure.
- **Branch hygiene:** 2 stale agent worktrees + local branches removed (dungeon work verified already-merged);
  2 stale remotes purged — `feat/tower-core-loop` (`cea673e4`) + `samantha-village-progress-2025-05-23`
  (`40a570a6`). Remotes now `master` + `wip` only.
- **Real bug surfaced (CS-1):** equipped ring/amulet (`equippedRingId`/`AmuletId`) declared + migrator-seeded
  (v26) but no GameState field / no Snapshot-Apply → **reset on reload.** Needs a ticket.
- **Still queued:** §6 catalog-drift + §7 comment-lie fixes as a housekeeping WO; CS-1 ticket. Push HELD.

## Latest (2026-07-20 overnight autonomous loop) — see `OVERNIGHT_RESULT_2026-07-20.md`
- **Regression baseline = REGRESSION_OK, ALL 16 SME P1 suites GREEN, ZERO reds** (2026-07-20). Added
  WAVES_SCHEMA (EW-3) + PACK_COSMETIC_INTEGRITY (ECON-1) + flipped DUNGEON_DRESSING green (real
  DungeonDresser prop pass). **All 5 audit P1s cleared + guarded.** Full green set: WAVE_SCALING /
  ENEMY_REWARDS / WALL_MITIGATION / UPGRADE_AUTHORITY / PACK_GRANT / SFX_RESOLVE / DUNGEON_EXIT /
  FOUNDING_REACH / FTUE_HONESTY / ECHO_CARD_COPY / SHADER_PIN / MODAL_REGISTRATION / CRYSTAL_PRODUCTION /
  WAVES_SCHEMA / PACK_COSMETIC_INTEGRITY / DUNGEON_DRESSING. *(pushed to wip, origin 1d7512b0)*
- **Composed dungeons now DRESSED:** `DungeonDresser.DressRoom` (Assets/Editor/RoomForge) seats ~8 real
  KayKit props (corner torches + floor barrels/crates) per composed room, wired into `DungeonBaker`
  pre-NavMesh (colliders stripped, doorway clearance). Broader dungeon VFX/lighting/battle dressing = next
  pillar follow, NOT built yet.
- **NEW TOOL — headless UI screenshot capture:** `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`
  (edit-mode synchronous render; the old Play-mode path never worked under `-batchmode -quit`). Writes
  `Builds\ui-capture\*.png` + `UI_CAPTURE_OK`. **Run it before shipping any UI change** (owner rule: never
  be first to see a broken panel). It already caught 2 real Echo-card bugs pre-build tonight.
- **Newly data-driven (SSOT):** `buildings.json` crystalsPerWave (v2, CrystalMine yield), `enemies.json`
  xp/coinReward (v4), `walls.json` heartDamageMultiplier. All dual-copy identical + version-bumped.
- **Echo card = 6 NAMED SOULS** (Aldwin/Elowen/Corvin/Bran/Doran/Maren) in `EchoRosterCatalog` — each the
  awakened essence of a soul the Heart guards; founding header "An Echo Awakens" (not "Leveled Up to 1").
  Founding card layout screenshot-verified (full copy fits, 3-across buttons, one dismiss).
- **15 top-band modals** now register with PanelManager (back-button + battle-lock arbiter).
- **Builds:** Seeker APK -> Windows -> WebGL launched detached ~06:28; WebGL DEPLOY pending owner `vercel` CLI.

## Persistence / save
- Save schema **v38** — `SaveSchema.cs:41` → `public const int CurrentVersion = 38;` *(re-verified at source 2026-08-10; the const moved lines, read it off the file)*. **v38 = WO-934 army loadout bank** — `ArmyStorage.loadouts` (3 named composition presets) + `activeLoadout` index; additive on nested Army JSON, `MigrateToV38` EnsureLoadouts for empty slots. History: v29 heroLevel/heroXp/heroLifetimeXp; v30 strategicPlacementMigrated WO-673; v31 echoLanes; v32 freeBuildsUsed; v33 echoLanes `lane:level` token WO-738 — deliberate pass-through; v34 persists Tribes/Wards/Arena + pet active-slot; **v35** `obsidianQueue` — WO-773 multi-channel Builder/Train/Research queue, `MigrateToV35` folds legacy buildJobs/pendingBuilds/buildingCooldowns into the Builder channel, idempotent; **v36** WO-834 `everBuiltStructureIds` (the blank-town baked standdown); **v37** WO-911 M2 **the per-job PAID BASKET** — `paidWood/paidFood/paidIron/paidCrystals/paidMagic` on `BuildJobData`, the precondition for the owner's Q1 ruling that **cancel refunds 100% of what was paid, flat**; ⚠ **a pre-v37 job refunds ZERO and says so.** Every bump carries a `SaveMigrator` step so the CORE_SAVE version-triple oracle stays green.
- **Persisted:** BaseLayout, Zones, PartyMemberIds, ArenaDefense, PetName, Settlements. **NOT persisted (truthful red oracles):** Tribes, Wards, Arena W-L record, pet active-slot map, broken-tower state. *(2026-07-12)*
- Local save = PlayerPrefs `dotr-save`, signed (LB-3 HMAC, tamper-rejected); server save/load nonce-auth is built but `BackendAuthConfig.Enforced` = **OFF**. *(2026-07-12)*

## Data catalogs
- **Dual-copy rule: `Resources/Data/Canonical` WINS at runtime** over StreamingAssets. `DATAWEB` oracle enforces content sync. *(2026-07-12)*
- **Gear ruling:** the SMALL curated set is deliberate ("only a few prefabs — nothing decent to use yet") → **Resources is truth for weapons/armor**; sync Resources → StreamingAssets. The 433-weapon StreamingAssets copy is the stale side. *(owner 2026-07-12)*
- Drifted pairs found (sync pending): weapons, armor, daily-quests, skin, stake-rewards, tower-perks. *(2026-07-12)*
- The "six StreamingAssets-only WebGL-broken catalogs" are **already mirrored** (that risk-ledger line is stale). *(2026-07-12)*
- **Echo model (WO-738, owner Path-B ruling):** 6 collectible spirits (identity in the `EchoRosterCatalog` CODE TABLE — no ScriptableObjects, WebGL-safety ruling), balance in `echoes-balance.json` (dual-copy). Each echo has element + level (max 8) + one assigned functional lane (Harvest/Crafting/Defense/Exploration). `EchoBonusCalculator` is the single math source (economy + UI + `EchoSpecializationRegression` oracle all read it). Echoes NEVER fight: Defense = passive offline city-raid bonus, Exploration = dungeons-only — both STUBBED (write to Core `EchoLaneBonuses`, hosts read when they land); **Harvest + Crafting are the felt-now lanes.** Picker reachable via roster-card tap (the wisp-injector path is dead). *(2026-07-17)*

## Backend / web
- **`api/` lives IN THIS REPO and is git-TRACKED** (not gitignored, not a separate React repo). Deploys ride any `vercel deploy` run from the repo root. *(2026-07-12)*
- WebTrace: `?trace=1` → `POST /api/trace` → Neon **`analytics_events`** (`event_name='web_trace'`; no separate web_traces table). **CLI read path = the `[sig]` echo in Vercel runtime logs** (`DATABASE_URL` is sensitive/unpullable). *(proven 2026-07-12)*
- **No TTL cron exists** for trace rows (security H1 — fix pending). Open POSTs (trace/track/bug-report) have **no rate limit**. *(audit 2026-07-12)*
- db-viewer: `tools/db-viewer/index.html` + `api/admin/db.js`, key = `ADMIN_DASH_KEY` (Vercel env, set + redeploy to activate). *(2026-07-12)*

## Web triage — the read path (LIVE as of 2026-07-15; it never was before)
- **`ADMIN_DASH_KEY` is now SET** (Vercel env, preview+production; value in gitignored `.admin-dash-key`).
  It had NEVER been set, so `tools/db-viewer` + the `/triage-web-issue` skill were **dark since written**
  — 70,053 `analytics_events` rows accumulated unread. Endpoint verified live. *(2026-07-15)*
- ⚠ **`vercel logs` CANNOT give you the `[sig]` lines** — proven: even `--json` returns exactly ONE
  message per request (the summary `[web_trace] sess=… lines=N signal=N`); the per-line
  `[sig]` echoes from `api/trace.js:67` are never surfaced. The canon read-path "the `[sig]` echo in
  Vercel runtime logs" gets you `signal=18` but **not the 18 lines**. **Real read path = the admin
  endpoint** → `api/admin/db.js?view=traces` (sessions) → `&session=<id>&order=asc&limit=50` (the
  scene-load HEAD, where TERRAINDIAG / MagentaGuard / catalog resolution live). Header
  `x-admin-key`; base rotates per deploy → `Builds\admin-preview-url.txt`. *(2026-07-15)*
- **`order=asc` + `offset` + `total_batches`/`has_more` added** to the traces view. It was
  `DESC LIMIT 20` with no offset, so long sessions (one ran 2840 batches / 153k lines) were readable
  only from the TAIL = gameplay spam; the diagnostic head was structurally unreachable. *(2026-07-15)*
- **WEB F8 WATCHER LIVE:** `.claude/skills/run-defenders/websig-watch-start.ps1` polls the trace DB and
  emits into the SAME `logs/f8-inbox` with the same PING seq contract, so `f8-check-inbox.ps1` covers
  desktop AND web. Start it alongside `f8-watch-start.ps1`. Proven against the known-bad 07-15 session:
  29 signal hits, fires on the MAGENTA line. *(2026-07-15)*
- **Sessions are attributable from 2026-07-15:** `WebTrace._buildId` = `<version>@<host>` (was
  `Application.version` = **"1.0" for every build**, so a magenta preview and healthy prod were
  indistinguishable). Needs a WebGL rebuild to reach players. *(2026-07-15)*

## Ground / terrain (RCA 2026-07-15 — the magenta ground)
- **The visible ground of `Main_Castle_Overworld` is the `ExteriorTerrain` Terrain**, NOT the courtyard
  tiles (those are dropped to Y=-0.5 + hidden by GroundZFightFixer). It binds its material BY GUID at
  `Main_Castle_Overworld.unity:16016` -> `0eb083914b7ffae4eaf721e2353fea0b` =
  `Assets/Generated/Terrain/ExteriorTerrainMaterial.mat`. *(2026-07-15)*
- **That .mat was NEVER IN GIT** (`git log --all -- <it>` = empty) — `Assets/Generated/` was ignored, so it
  only ever existed on the machine that ran the terrain bake (laptop, baked 05-31). Its siblings
  (ExteriorTerrainData + the 5 .terrainlayers) WERE tracked, committed before the ignore rule → the ground
  was **walkable but MAGENTA** on every other machine. **FIXED:** original recovered from the laptop share +
  `Assets/Generated/Terrain/` is now TRACKED (the whole bake folder). *(2026-07-15)*
- **MagentaGuard could not save it:** `MagentaGuard.cs` gated terrain recovery on `tm != null &&
  IsBrokenShader(...)` — a NULL material short-circuited it, so the fix never fired (the FloorDiag line
  reads `mat='<NULL>' ... broken=False`). Now treats a null materialTemplate AS the break. *(2026-07-15)*
- **The web build is NOT blind — read the live trace, don't hunt a desktop log** (a CLI got this wrong
  2026-07-15 and wrote a bogus WO; the answer was already in START_HERE §3). `FlowTrace.cs:28` defaults
  `Enabled = isEditor || isDebugBuild` **on purpose** (PII: hot-path lines carry wallet ids / save-blob
  lengths / roster), but `WebTrace.cs:162` sets `FlowTrace.Enabled = true` when web tracing activates, and
  BOTH its gates are already open: `FeatureFlags.cs:117` `WebTrace => Get("webtrace", defaultOn: true)` and
  `WebTrace.cs:63` `TraceEndpoint = https://defenders-of-the-realm-v2.vercel.app/api/trace`. So a live web
  session streams `[Flow:*]` to Neon `analytics_events`; **CLI read path = the `[sig]` echo in Vercel runtime
  logs**. ⚠ `WebTrace.cs:11-15`'s header still says "DORMANT BY DEFAULT / default OFF" — **the comment LIES**
  vs `defaultOn: true` (classic: verify from code). *(2026-07-15)*
- Minor, unfixed: `FloorDeepDiag.cs:32` is hard-scoped to `TargetScene = "MainCastle_Hall"`, so it never runs
  in the live merged world `Main_Castle_Overworld`. MagentaGuard's own FloorDiag dump is what actually fires. *(2026-07-15)*
- ⚠ **`/Assets/Resources/Structures/` is gitignored** (`.gitignore:121`) — only **4** models are tracked
  (ArcaneSpire_1/2/3, WizardTower_1); the other ~37 arrive ONLY by manual LAN copy from the laptop.
  **This is DELIBERATE and stays** (owner ruling 2026-07-15): there are exactly **two machines** (this
  desktop + `Kayden-Laptop`, share `\\<ip>\EoA`, user `Kayden-Laptop`) — no CI, no fresh clones, so the
  big-art-out-of-git policy holds and LFS is not worth it.
  **The real risk is TWO-MACHINE DRIFT, and it is not theoretical — it caused BOTH 07-15 bugs:** the
  terrain material existed only on the laptop (magenta ground), and the 22:00 exe was cut 45 min BEFORE
  the 22:45 art copy, shipping 4 of 41 models as placeholders while the build reported SUCCESS.
  **Mitigation = make drift LOUD, not tracked:** a pre-build oracle that fails when
  `structures-catalog.json` `visualPrefabPath` keys do not resolve on disk. Proposed 07-15, owner's call. *(2026-07-15)*

## Builds
- **APK VERSION STAMPING (2026-08-05):** every Android build now stamps a **monotonic
  `AndroidBundleVersionCode`** (minutes since 2026-01-01 UTC — stateless, no counter file to drift
  across the two machines) plus a readable `bundleVersion` (`2026.08.05.312200`) via
  `AndroidBuild.ApplyVersionStamp`. **Before this both were frozen at `1.0` / `1` forever**, so
  (a) Firebase App Distribution folded EVERY tester build into one release — the upload literally
  replies *"re-uploaded already existing release 1.0 (1)"* and testers cannot tell builds apart —
  and (b) `Application.version`, which feeds `WebTrace._buildId` and the bug-report `app_version`
  column, was the constant `"1.0"` that made a magenta preview and a healthy prod indistinguishable
  in the trace DB (the 2026-07-15 incident). Android also refuses an install whose versionCode goes
  backwards, so the frozen code was a latent update failure.
- **Firebase = APK DISTRIBUTION ONLY (owner ruling 2026-08-05):** *"only for storing the APK not
  changing from Neon."* Neon `/api/game/save` remains the save backend; **no Firestore migration**,
  and **no re-adding email/Google/phone auth** (Android ships wallet-first per WO-837/847). The
  Firebase console's "Add Firebase to your Android app" wizard shows the **Android Studio** path —
  its `com.google.gms.google-services` Gradle plugin + `firebase-bom` snippets must **NEVER** be
  applied here: this is Unity (`mainTemplate.gradle` is `com.android.library`, Groovy, template
  tokens), the Firebase **Unity** SDK pre-generates
  `Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` instead, and the
  dependency block between `// Android Resolver Dependencies Start/End` is EDM4U-generated and
  overwritten on every resolve. Installed SDK = **13.14.0** (`firebase-app-unity`/`firebase-auth-unity`);
  any added Firebase package MUST match that version.
- **PROD (current) = the 2026-08-05 build** — deployment `dpl_9vGadbKyPrQ55HR3PaUT53i9CNUh`,
  `https://defenders-of-the-realm-v2-ly1ih48m3.vercel.app`, `target: production`, deployed
  **2026-08-05T23:37Z**, commit `8fdb29a5`, serving `defenders-of-the-realm-v2.vercel.app`.
  *(verified 2026-08-10 against the LIVE Vercel deployment record, not against a doc)*
  - ⚠ **This line used to read *"PROD (current) = the 07-16 six-fix build `q2v5vj86g`, promoted
    2026-07-16"* — STALE BY THREE PRODUCTION DEPLOYMENTS.** The record shows `target: production`
    deploys on **2026-08-03T22:50Z**, **2026-08-04T19:33Z** and **2026-08-05T23:37Z**. A seat trusting
    the old line would have believed prod was three weeks of code behind where it actually is — which
    is exactly the error that kept the "prod runs OLD `api/`" claim alive below (see `docs/HANDOVER.md`
    and the 08-09 anchor's correction notes). **Read the deployment record, never this line, when the
    answer has to be current.**
  - **Rollback target = `Builds/PROD_ROLLBACK.txt`** — ⚠ this doc referenced that file **for weeks
    while it did not exist on disk**; it was finally written 2026-08-10. A referenced rollback target
    that was never written is the same as having no way back, and nothing in the pipeline was checking.
    **STANDING RULE: overwrite `Builds/PROD_ROLLBACK.txt` with the OUTGOING prod deployment id BEFORE
    every promotion.** Recorded after the fact, it points at the thing you are trying to escape.
- **2026-08-01 release train:** fresh desktop exe · Seeker APK v-wave installed on-device + Firebase App
  Distribution (testers) · WebGL→Vercel preview refreshed. Screenshot archive
  `Builds\ui-capture-archive\2026-08-01\`.
- **Web-build self-test = `tools/webbot/`** (Playwright): `webbot.js` drives the DEPLOYED build for
  screenshots + live browser-console `[Flow:*]` capture + a drag-pan engage check; `introtest.js`
  clicks Play Intro. CAVEAT: synthetic clicks do NOT reliably fire Unity uGUI buttons in WebGL — the
  bot verifies boot/render/console + asset serving, but button-driven flows (Play Intro, into Build
  mode) need owner felt-test or the codec/HTTP determinants. Pass the SSO bypass param in the URL. *(2026-07-16)*
- **Intro video plays on web** (owner Q 2026-07-16): determinants all green — `StreamingAssets/Video/
  Defenders.mp4` serves 200 (`video/mp4`, 4MB), codec = **H.264 avc1 + AAC mp4a** (browser-compatible;
  Unity copies StreamingAssets raw so codec IS the web risk), `IntroSequencePlayer` uses VideoPlayer
  **URL source** (not VideoClip) with the WebGL `audioOutputMode=Direct` fix. *(2026-07-16)*
- **Ship WebGL = `BuildOptions.None`** (Development is opt-in `-DevBuild` — NEVER deploy a DevBuild: Development players paint the full-screen error overlay). *(verified WebGLBuild.cs:124 / DesktopBuild.cs:178, 2026-07-12)*
  - ✅ **CLOSED 2026-08-08 (`374ccd26`):** the long-standing "desktop release still ships Development"
    open item is DONE — the desktop player now builds RELEASE, verified by `DeNelle.DevTools.dll` being
    **absent** (206 DLLs, was 207). ⚠ Paired trap from the same commit: `ff.devresourcetool`'s default
    flip to OFF **changes nothing on a machine that already has `ff.devresourcetool=1` in PlayerPrefs** —
    `FeatureFlags.Get` reads PlayerPrefs FIRST. *(2026-08-08)*
- Deploy chain: **prefer `overnight-webgl-deploy.ps1`.** Promotion + push stay the owner's call.
  *(corrected 2026-08-10 — this line used to read "`webgl-vercel-overnight.ps1` detached; markers +
  `DEPLOY_URL` in `Builds/webgl-chain-status.txt`. Preview only", which mixed two scripts into one
  procedure that no single script implements.)*
  - **The two scripts disagree and the markers belong to the OLDER one.** `overnight-webgl-deploy.ps1`
    writes **`Builds\overnight-chain-status.txt`**; the markers canon quotes (`CHAIN_START`,
    `WEBGL_BUILD_OK`, `DEPLOY_URL`, `CHAIN_DONE`) are `webgl-vercel-overnight.ps1`'s. Grepping for a
    marker in the file the other script writes finds nothing and reads as "the chain never ran".
  - `webgl-vercel-overnight.ps1` calls bare **`vercel deploy --yes` with NO token and NO scope**, so it
    fails unless the CLI is already interactively authed — which a detached/overnight run is not.
  - ⚠ **TRAP — never `cd Builds\WebGL` and deploy from there.** That folder carries its own
    `.vercel/project.json` pointing at a **DIFFERENT Vercel project** (`defenders-webgl`,
    `prj_ox8fqdHbD7lkrKEyxy0dtQAjphGc`) than the repo root
    (`defenders-of-the-realm-v2`, `prj_qUmuwr8BN492oZH8yRuvPZMN3e0J`). Deploy from the **repo root** so
    the link resolves to the real project. *(The stray file only exists when `Builds/WebGL/` has been
    built + linked; `Builds/WebGL/` is absent from disk right now, so the trap is dormant, not gone.)*
- Fleet baseline: DataRegression = **REGRESSION_OK, 0 reds** — all 5 long-standing reds fixed 2026-07-19 (R1 arena ground texture, R2 dual-wallet Grant->GameState, R3 pet active-slot persist, R4 core-save Tribes/Wards/Arena persist, R5 orc-raider SSOT enemies.json Hp 130). *(2026-07-19)*; re-certified 2026-08-01 with UI_CAPTURE_OK 23 (103 checks).

## UI / MVVM (WO-744 — DONE 2026-07-18)
- **Strict MVVM across the whole game:** every panel View binds an `IPanelViewModel` and reads NO
  game state at runtime; all state/logic lives in the VM (`CreateDefault` is the sole resolution
  site). All 36 audit panel Views migrated (silos B/C/D/E/F/G) + the landmines: BattleHudUgui behind
  `ff.battlehudvm` (default OFF; ATB feel-sim untouched), DialogueView with the WO-702 truce relocated.
  Spec: `docs/UI_MVVM_MIGRATION_PLAN.md`. *(2026-07-18)*
- **The ratchet is ARMED:** `UiMvvmConformanceRegression` runs in `DataRegression` as `[ui-mvvm]`
  with `HardFailOnNew=true` + an EMPTY baseline — any NEW View that reads game state (EconomyService/
  GameStateService/Find*Type/gameplay catalogs) HARD-FAILS the gate. Non-panel offenders (flow
  controllers, spawners, benign EventSystem/sibling finds, HUD wiring) are allowlisted with reasons. *(2026-07-18)*
- Shared VM seams: `Core.UI.Mvvm.WalletVM` (DTO) + `LiveWalletSource`, `GearIconCatalog` (icon leak),
  promoted `Core.UI.Mvvm.CraftRecipeVM`, `ArenaPaletteVM`, `StructureCardVM`/`PlacedTowerListVM`. *(2026-07-18)*

## Room Forge (WO-740–745 — DONE 2026-07-18)
- Socketed-room dungeon pipeline merged to mainline: 17 default room prefabs + shared KayKit
  materials; JSON compose layouts (`Assets/**/dungeon-layouts/`, dual-copy + `version`); the demo
  bakes clean (`matesOk=2 matesFail=0`, NavMesh `PathComplete`); `RoomForgeRegression` (`[room-forge]`,
  10 cases) + `[Flow:DungeonBake]` + baker hard-gate/re-verify fixes. Editor menus under
  `Defenders/Dungeon/*`. KayKit atlas stays machine-local (big-art-out-of-git). *(2026-07-18)*

## UI / input
- ASCII-only TMP strings (non-ASCII glyphs = tofu □ on device); never meaning by color alone (owner red/green colorblind). HUDUI oracle locks the tofu class. *(2026-07-12)*
- Build-mode touch: uGUI verb bar + PLACE + kit d-pad (publishes `HudMoveInput` → merged with arrow-key read). GhostPreview moves its CHILD visual — probe via `GhostPreview.CurrentPosition`, never the host transform. *(2026-07-12)*
- **Right ActionBar = Attack + Q/W/E/R named skills:** Sword Wielding / Sword Heroic / Shield Charge / Warden's Grace / Radiant Strike. **Mobile HUD shows NO key-letters** (WO-750 SPEC). *(2026-07-19)*
- ⚠ **CORRECTED 2026-08-06 — this line was two rulings stale.** It read "default **4m**, tower override
  **7m**, siege override **3m**". WO-764 replaced the per-class metre overrides with a **`heightMul`
  multiplier** on a 4 m base (tower 1.25 = 5.0 m), and the 2026-08-05 owner cadence ruling moved it again.
  **Live values:** base **4 m** × `heightMul` — **1.25** landmark · **1.2** towers (4.8 m) · **1.0**
  building base · **0.75** siege · **0.35** decoration; recorded in `structures-catalog.json` as
  `_heightCadence`. Walls stay at 1.0 **deliberately**. `repo.visualHeight` is dead. The Y-height audit tool
  (`StructureHeightAudit`) is still the way to print `measuredY` per prefab. *(WO-751 → WO-764 → 2026-08-05)*
- **Destroyed items = NO rebuild + full-cost + VFX cleanup** via a new `Destructible` component (WO-753 in progress). *(2026-07-19)*
- **Headless UI-screenshot pass must run before builds** (felt-test-wave standing rule). *(2026-07-19)*

## Echo canon
- **Echo = the essence of a person the tree of Elarion guards** — 6 named people: Aldwin, Elowen, Corvin, Bran, Doran, Maren. Feeds the WO-752 founding-card overhaul + post-tutorial interjection (SPEC + creative sign-off, awaiting copy). Balance/lane model unchanged (see the Echo model line under Data catalogs). *(2026-07-19)*

## Process
- Boot: **START_HERE.md** routes everything; SAMANTHA.md = the confirmation gate; PREFLIGHT_GATE A/B/C.
- Phone/async triage: `/triage-web-issue` skill — pull the web-trace from the db (`api/admin/db.js`, `X-Admin-Key`=`ADMIN_DASH_KEY`), RCA from the proving line, write the WO left READY for the Windows machine. *(2026-07-12)*
- WO numbering: mint from the `CLI_LANES_WO_NUMBERS.md` banner (**832** as of 2026-08-01; NEVER copy the number — the banner is the only authority; historical: 761–773 consumed — 762 builder-queue, 763 Wisdom, 764 hub-Y-height, 765 capture-Default-Town, 766 Seeker wallet, 767 texture caps, 768 thin-client, 769 Firebase auth, 770 dungeon, 771 raid, 772 enemy, 773 Obsidian queue; earlier: 739-753 consumed — 750 Right-ActionBar naming SPEC, 751 Y-height normalization DONE, 752 Echo founding-card SPEC, 753 Destructible IN PROGRESS; Grok-03 here→there = **716–722** + **715** VFX; see `docs/UI/Grok-03-here-to-there-WO-program.md`), bump in the same edit. ⚠ UI-seat mints in the old 674–685 space collide — translation table in the banner; owner syncing the UI seat 07-13. Collisions resolved 2026-07-13: 677–681 duplicate specs renumbered to 688–692, 682/683/685 dupes to 695/693/694; a fresh 07-13 mint colliding with the 684 board renumbered to **696** (repair-before-upgrade context). *(2026-07-13)*
- Outstanding board: `WorkOrders/WORK_ORDER_684_outstanding_items_board.md` (exact asks + steps).
- ✅ Apex dragon model = **SWAP LANDED 2026-07-24 (WO-760)** — the licensed Asset-Store dragon (product 71047 "Dragon Animated", WDallgraphics; source `Assets/Dragon/`, now git-tracked, not gitignored) ships as `Resources/Enemies/Boss_Dragon.prefab`, built by `DragonAnimatorSetup` + force-tracked `Assets/Generated/Animators/SyndrathDragon.controller`. Old CC-BY-NC 3DHaupt `Dragon.fbx`/2 controllers/materials + the orphan `Prefabs/Village/Generated/Boss_Dragon.prefab` git-rm'd; unlicensed `RedDragon 1.2` stray deleted; `EnemyFactory` dragon keys repointed to `Boss_Dragon`. ⚠ **The earlier "RESOLVED 2026-07-23" claim was PREMATURE** — that commit only repointed comments; the CC-BY-NC model still SHIPPED (Resources includes unused assets) until the 07-24 builder-run + git-rm. Commercial-ship blocker now ACTUALLY cleared; boss "Syndrath the Devourer" retained; fly-in->land->burn-towers->retarget-Tree behavior built (WO-760, felt-verify pending).
