# CANON GROUND TRUTH — 2026-08-01 (post-reboot ship wave: Realm Map + KayKit NPCs + Queues ruling + release train)

> **LIVE ANCHOR (2026-08-01).** Records reality after the post-reboot wave on `wip/village2-and-f8-tickets`.
> **Supersedes `CANON_GROUND_TRUTH_2026-07-26.md`** (bannered). If a doc contradicts a line here, the doc is stale.
>
> **This anchor is a DELTA over the 07-26 anchor**, which itself deltas the deep `2026-07-22` module anchor.
> Read order: this → 07-26 (dungeon/raid wave detail) → 07-22 (§5 module digests, §6 catalog-drift ledger,
> §7 comment-lies registry, §8 landmines — still the deep reference) → `KEY_FACTS.md` → `SESSION_CANON_LOADER.md`.

---

## 1. REPO / GIT / GATES (verified this session)

- **Branch `wip/village2-and-f8-tickets`. HEAD = `ac0a52e3`** (2026-08-01 evening), **local == origin — pushed.**
  Prod untouched (promotion stays the owner's). Working tree clean.
- **Today's commits (all gated before commit):** `e8bd17b0` KayKit stage (12 bodies, `KAYKIT_STAGE_OK 12/12`) ·
  `777dd9ff` WO-818 ph2-3 (repo.npcModel binding + injectors) · `eb5d0710` WO-826 Realm Map + Queues retirement ·
  `1371e70a` oracle registrations + UI-seat reconcile (WO-830/831, banner→832, UI review banked) ·
  `ac0a52e3` DesktopBuild post-build batching re-assert (RCA close).
- **Gates at HEAD:** `COMPILE_GATE_OK` + `REGRESSION_OK` (incl. new `NPC_MODELS_OK` 12 rows + `REALM_MAP_OK`
  5 regions + the retooled obsidian-queue suite) + `UI_CAPTURE_OK 23` (pixels opened, Realm Map verified).
  EditMode last full run 762/764 — the 2 reds are the PRE-EXISTING `WaveDataTest` wave-1-zero-enemies
  data ruling (owner call pending), not this wave.
- **Save schema v35** (unchanged today — no new persisted fields).
- **WO numbering: next free = 832** (`CLI_LANES_WO_NUMBERS.md` banner is the only authority; 782–831 consumed).

## 2. SHIPPED THIS SESSION

- **WO-818 COMPLETE (all 3 phases) — data-driven KayKit NPC bodies.** 12 owner-mapped Humanoid bodies
  tracked at `Assets/Resources/NPCs/KayKit/`; `structures-catalog.json` v6 (dual-copy, hash-identical)
  carries `repo.npcModel` on exactly 12 rows; `KayKitNpcBody` (Village) is the ONE resolver —
  KayKit-first → People chain → capsule, one `FlowTrace.Warn` on an authored-but-broken slug, never a
  blank NPC; both injectors (`BarracksNpcInjector`, `CastleVendorNpcInjector`) consume it.
  A body swap is now a one-word owner JSON retag (creative pick = OWNER-ONLY). Oracle: `CheckNpcModels`
  (parity + slug-file existence + the exactly-12 pin — a 13th row must update the oracle in the same commit).
  Note: KayKit bodies stand statically (no AmbientNPC/Animator on the FBX) — animated idles = follow-up WO.
- **WO-826 SHIPPED — Realm Map parchment UI.** `RealmMapCatalog` (Core/World, typed loader over dual-copy
  `realm-map.json`, no second region list) · `RealmMapVM`/`RealmMapPanel`/`RealmMapPanelBootstrap`
  (Village/Hero, strict MVVM, PanelManager/PanelRouter `PanelId.RealmMap=15`) · HUD **Map** button
  (hidden until `Onboarded`, WO-825 R4 default) · DevPanel "Open Realm Map" · `CaptureRealmMap` ·
  realm-map oracle + 8 EditMode tests. VM derives REAL state from existing `GameState.Regions` +
  `BestWave` (only the discovery WRITER is stubbed → WO-827). Travel CTA disabled until 827.
  mapPoint y = percent-from-TOP (flip is a one-liner in `RealmMapPanel.BuildNode` if felt-mirrored).
  Fog nodes render as rounded squares (spec allowed; disc polish optional). Wayshrine entry NOT built
  (no Wayshrine structure exists) — HUD-only per spec fallback.
- **OWNER RULING (2026-08-01): the bar Queues button is RETIRED.** The right-column **Builders chip**
  (QueueStatus band — directly ABOVE the resources dock) is the ONE Queues entry (same
  `ObsidianQueueGate.RequestToggle`). calm(town) actionBar = **6 faces**: Build · Talk · Bag · Raids ·
  Map · Quests(⇄Upgrade context). This also fixed the latent /5-divisor overflow that anchored the
  trailing face past the zone edge. `ObsidianQueueRegression` 7c now FAILS if the bar button or its
  occupancy row reappears. (Chip face label stays "Builders" — owner may rename to "Queues", one word.)
- **ProjectSettings dirt RCA CLOSED (`ac0a52e3`).** Twice-captured today: the pre-build
  `SetBatchingForPlatform(0,1)` readback proves memory holds dynamic=1, yet the session's exit
  serialization wrote the Standalone entry dynamic=0 — **the reverter runs INSIDE `BuildPlayer`,
  after the set.** Fix = post-build re-assert (mirrors the WebGL exceptionSupport restore in the same
  file). Owner keeps `dynamic=1`; builds should stop dirtying the tree — verify on the next build.
- **Dungeon subsystem verified from a CAPTURED RUN (owner-ordered log test, fresh exe, headless
  `Dungeon_HealersCottage`):** all 7 proving lines green — EnterDungeon (12 rooms/5 lore/2 checkpoints/
  4 encounters/miniboss) · HydrateExits (normal + boss back-door) · DressTraversalLinks · DoorAlign ·
  ApplyThirdPerson mode=FPV · HideHeroBody (20 renderers ShadowsOnly) · felt-fix neutralized
  HeroLocomotion (DungeonHero sole mover). **Plus the 08-01 audit R-A1 arena guard fired live**
  (CharacterController DISABLED on arena stage + movement-step-skipped Once). Known-open (unchanged):
  vitals still seed the 120/60 placeholder (WO-770.10); ~10 placeholder primitives MagentaGuard-hidden
  (WO-770.8 content); one minor find — `EnvTreeFix VERIFY FAILED on 'Skeleton_Mage_Hat'` (tree renderer
  material didn't resolve in build; unticketed).
- **UI-seat work reconciled (sole-committer flow):** **WO-830** Echo harvest affinity + synergy program
  (all 6 echoes → unique harvest affinities Wood/Iron/Food/Gold/Crystals/Repairs; 3 disclosed pair
  synergies; 1 HIDDEN tri-synergy that deliberately under-reports; kills the dead lanes; Aldwin's
  Exploration-identity bug is the felt root) + **WO-831** Echo emergence 2D sprite beat (new sprite +
  dialogue advance at unlock — NO 3D, portrait-spirit canon holds). Plus the frozen
  `docs/qa/UI_REVIEW_2026-08-01.md` (20 panels real-pixel readability review; P0 = opaque body +
  full-screen dim scrim on the old un-reworked panels; Echo Unlock card = the bar to match).
- **Echo canon UPDATE (2026-08-01, supersedes part of the 07-17/07-19 lane model):** all 6 echoes get
  a unique harvest affinity; Defense/Exploration lanes stay hidden/unwired (the old "wire Defense next"
  guidance is retired). Memory `echo-lane-design-rulings` updated by the UI seat.
- **WO-830/831 IMPLEMENTED 2026-08-02 (pending gates):** all 6 roster rows `PreferredLane=Harvest`
  with affinities Aldwin→Food, Elowen→Wood, Corvin→Gold, Bran→Crystals, Doran→Iron, Maren→Crystals
  (Repairs REMOVED per the 08-02 banner; crystals = the doubled affinity, Bran+Maren rates 0.45 each
  so the combined trickle is slowest). Owner ruling honored: the card is a per-Echo RESOURCE PICKER
  (5 chips; affinity = match bonus 0.40, never a lock); echoLanes grammar extended to
  `<resource>:<level>` tokens (read-migrated, NO schema bump); Dump = 5-way split (Gold→AddCoins,
  Crystals→GrantSpendable crystals param); 3 disclosed pair synergies (+0.10 each) + hidden
  tri-synergy (+0.25, applied-only, never displayed); silo capacity reconciled to the rate basis.
  WO-831: EchoUnlockDialogue = emergence sprite beat (Resources/Echoes/Emergence, LFS; Guard
  fallback to portrait — art not yet supplied) → Continue → awakening card. New/updated oracles:
  EchoSpecializationRegression + EchoResourcePickerRegression (registration in DataRegression owed
  by the committer).

## 3. BUILDS / RELEASE TRAIN (this evening)

- **Desktop exe:** rebuilt twice today; final at `Builds\Windows\` 15:17 with the full wave. Batching
  readback healthy; owner felt-test pending.
- **Seeker APK → Firebase App Distribution:** launched (release notes = today's wave). Device
  SM02G4061955851 attached; adb install follows the build. Firebase project
  `defenders-of-the-realm-echos`, app `1:264518851517:android:8e193b012cba6986d050d4`.
- **WebGL → Vercel PREVIEW:** queued after the APK (single Unity gate). Ship build = `BuildOptions.None`
  (never `-DevBuild` to deploy). Preview only — `--prod` promotion is the owner's. Verify the real
  `DEPLOY_URL`, never the `CHAIN_DONE` marker (writes on failure — 07-22 §7 lie #13 stands).
- **Screenshot archive:** `Builds\ui-capture-archive\2026-08-01\` (23 PNGs, the standing pre-ship set).
  Coverage gap list (panels with NO capture method yet) = `docs/qa/UI_REVIEW_2026-08-01.md` §Coverage.

## 3b. VERIFIED INVENTORIES (code-audited this session — cite these, not older counts)

- **Regression gate = 103 checks: 26 inline `Check*` + 77 sibling suites** in `DataRegression.RunAll`
  (46 unguarded + 31 `Guard.Try`-wrapped). The old "16 suites" line in HANDOVER is ~5 waves stale.
  ⚠ Tag `[dungeon-exit]` is emitted by TWO suites (DungeonExitRegression + DungeonExitReachableRegression)
  — log tags are not unique keys.
- **FeatureFlags = 62 flags** (`Core/FeatureFlags.cs`). ⚠ **The XML `<summary>` blocks LIE about the
  default on 12 flags — the trailing `//` comment on the property line is the truth.** Notably ON
  despite docs saying OFF: `mergedworld`, `webtrace`, `overworldencounter`, `regionroam`, `barracks`,
  `outpostcaves`, `mocaploco`, `poicallouts`, `battlehud9zone`; OFF despite docs saying ON:
  `raidwalk`, `gatetraversal`. `ff.strategicplacement` is REMOVED (unconditionally on).
- **Save v35 confirmed** (`SaveSchema.cs:35`, banner now correct); migrator 30→35 registered, v33 is a
  deliberate pass-through, v35 folds legacy build timers into the Builder channel (idempotent).
- **EditMode: 74 files under `Assets/Tests/EditMode` (73 fixtures + SiloCTestFakes)** — but the 2 reds
  (wave-1 zero-enemies) live in **`Assets/Data/Tests/WaveDataTest.cs`** (`DeNelle.Data.Tests` asm), not
  Tests/EditMode; the 762/764 run spans both assemblies.
- **HUD calm(town) roster (byte-identical dual copies):** actionBar = Build/Talk/Bag/Raids/Map/Quests
  (exactly 6); queueStatus = queueStatusChip; actionRail = resourceChips. `workQueueButton` survives
  in the repo ONLY as the retirement assert in ObsidianQueueRegression.
  (Naming note: `ObsidianQueueHud.OpenWorkQueue` keeps its method name — seam name, not player copy.)

## 4. QUEUE / OPEN ITEMS

- **Implementation order (owner/Grok):** WO-822 barracks teach v2 → WO-817 queue visual ph1-2 →
  WO-821 perk research timers → WO-827/828/829 map track → WO-830/831 Echo program (owner sequences).
- **Owner felt-verify list:** Realm Map + Map button · 6-face bar + Builders-chip-as-Queues ·
  KayKit vendor/drillmaster bodies · 819 (sell barracks → baked twin resurfaces + drillmaster reseats) ·
  820 (raid dim + wounded fairness; Phase E soft gate = specced NOT built, owner ruling) · 810 Rumor
  Board · 808 gear · 812/813 barracks loop · WO-825 R1-R4 map rulings (defaults live) ·
  wave-1 zero-enemy data ruling (the 2 EditMode reds).
- **Standing stales (carried, not fixed today):** `MASTER_CATALOG/<area>` files remain 2026-06-12-dated
  (the 07-22 §6/§7 ledgers are the fix list; housekeeping WO still unminted) · CS-1 ring/amulet
  non-persist · 07-22 §8 landmines (Echo lanes write-only — WO-830 now owns the fix · dual-wallet spend
  asymmetry · IronScrap no-drain · audio mixer stub · HeroPortraits absent).

---
*Live anchor 2026-08-01. Next session: read this, then the 07-26/07-22 chain. F8 watch daemon must be
restarted after any reboot (`.claude\skills\run-defenders\f8-watch-start.ps1`). Full boot ritual =
START_HERE.md — owner directive: complete absorption EVERY session, fan out agents, never trim for
efficiency (memory `full-boot-absorption-every-session`).*
