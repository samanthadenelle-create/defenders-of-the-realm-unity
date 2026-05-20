# Bug Log — Defenders of the Realm (v2 Unity Port)

**Project:** Defenders of the Realm — Unity 6 LTS / URP port
**Owner:** Samantha Denelle / DeNelle Studios
**Maintained by:** QA. Last updated 2026-05-20.

This is the running bug tracker for the v2 Unity foundation. It is seeded from the
**"Flags raised"** sections of `docs/unity-decisions.md` and from the open items in
the Week-5/6/7 port-notes (`docs/port-notes/`). Every seeded entry is a real,
already-recorded known issue — none are invented.

---

## How to log a new bug

1. Take the next free id: `BUG-0NN` (increment from the highest below).
2. Fill one row in the table: **id · severity · area · summary · status · source**.
3. **Severity** — judge by player/release impact:
   - **Critical** — crash, data loss, blocks the Week-8 acceptance gate, or ships wrong canon/wallet data.
   - **High** — a core feature is broken or unverifiable; a Part 9 gate is at risk.
   - **Medium** — a feature is degraded, cosmetic-but-visible, or works only with a manual workaround.
   - **Low** — minor polish, edge case, or finetune item with no gate impact.
4. **Area** — module/system: Core, BattleATB, Village, Dungeons, Wallet, Onboarding, Build/CI, Data, Audio.
5. **Status** — `Open` → `In Progress` → `Fixed` → `Verified` (QA confirms) → `Closed`. Use `Won't Fix` / `Deferred` with a reason.
6. **Source** — where it was found: a UAT step (e.g. "UAT B5"), a test-plan case (e.g. "TC-DUN-04"), a decisions-log flag, a port-note, or "ad hoc".
7. If found during a test, also mark that test case `FAIL` in `docs/qa/qa-test-plan.md` and reference this BUG id.
8. Add detail (repro steps, build, screenshot path) in the **Detail notes** section below the table, keyed by id.

QA documents; it does not patch. Fixes are made by the build agents/engineers; QA moves the status to `Verified`/`Closed` after re-test.

---

## Bug table

| ID | Severity | Area | Summary | Status | Source |
|----|----------|------|---------|--------|--------|
| BUG-001 | High | Village | Elarion / Keeper's Keep centerpiece renders as a solid white mass in batchmode review screenshots; immune to ~12 material rebuilds — likely a batchmode shader-variant / lighting quirk, needs the interactive Editor to diagnose. | Open | unity-decisions.md Week-3 Flags |
| BUG-002 | Medium | Village | Exterior wilderness rough: Terrain renders black/unlit, sky is an orange void (no skybox), a few props float off the Terrain. Interior village is fine. | Open | unity-decisions.md Week-3 Flags (Task #14) |
| BUG-003 | High | Core | 16 Core EditMode tests failed (`SaveLoadRoundTripTest` + `ResetCarveOutTest`) — `GameStateService._state` null after the inactive→active serialization sync clobbered reflection-injected `[SerializeField]` fields. Test-harness fix authored (inject after `SetActive`); run goes 43→59/59. | Fixed | core-test-fix.md |
| BUG-004 | High | Audio | Dungeon audio assets missing: `echoes-beneath-elarion.mp3` (dungeon BGM) and `lantern-flicker.mp3` (low-oil SFX) do not exist in the project. Code paths guard the null clip and play silent. Owner must supply the tracks — agent does not substitute music (spec Part 10). | Open | unity-decisions.md Week-1 Flags; week5/week6 port-notes |
| BUG-005 | Medium | Dungeons | `journal-vault` lore-stone paragraph 2 is a non-canon placeholder (no verbatim narrative-bible source). Flagged at runtime via `LoreStone.IsPlaceholderFragment`; must be sourced from the narrative team or the stone cut before ship. | Open | week6-dungeon-systems.md |
| BUG-006 | High | Village | Village NavMesh must be baked before enemies path — only legacy `com.unity.modules.ai` is in the manifest. Until the bake runs, enemies hold position and log a warning. Bake is wired into `VillageSceneBuilder.BuildVillage()`; needs an integration run to verify. | Fixed | unity-decisions.md Week-4 Flags; week4-integration.md |
| BUG-007 | High | Dungeons | "No walk-through walls" (Part 9.2 gate) is unverified — gated on every KayKit dungeon wall mesh carrying a collider, and illusory walls carrying none. Cannot confirm without a Unity build run. | Fixed | week5-dungeon-foundation.md (integrator checklist item 4) |
| BUG-008 | High | Core / Dungeons | `BattleController.ReturnAfterResult` hard-codes the post-battle return to `Village`; `BattleParams` has no return-scene field. A dungeon ATB battle therefore returns the player to the village, not the dungeon — breaks the Part 9.2 dungeon round-trip. | Fixed | week6-dungeon-systems.md |
| BUG-009 | Medium | BattleATB | `BattleController`'s per-enemy breach-roster mapper is still a stub — the ATB enemy roster may not reflect the actual breaching enemies. | Open | week4-integration.md (Known follow-ups) |
| BUG-010 | High | Wallet / Build | Solana Unity SDK package (`com.solana.unity_sdk`) is not yet resolved; the real `SolanaWalletProvider` only compiles once Unity auto-defines `SOLANA_SDK`. Integrator must resolve the package, add the SDK asmdef names to `DeNelle.Wallet.asmdef` references, and verify the `// SDK-VERIFY:` API calls against the live SDK. Module runs over the stub until then. | Open | week7-wallet.md |
| BUG-011 | Medium | Wallet | `WalletEndpoints.SkrMintDevnet` is empty — no devnet SKR mint address is published. The spec deliverable "buy a Hearth Spark pack with 25 devnet SKR" cannot run; the SKR rail fails cleanly with a descriptive error. SOL + USDC rails work. Owner must supply the devnet SKR mint. | Open | week7-wallet.md |
| BUG-012 | Medium | Data / Canon | Canon names **Bryn, Mara, Tovin, Eira, Aelf, Mira** are listed as canon in spec Part 1 but appear nowhere in `narrative-bible.md` or the v1 source — carried in `canon-strings.json` as flagged placeholders. Must be ratified before any Week-6 dungeon text ships. | Open | unity-decisions.md Week-1 Flags |
| BUG-013 | Low | Data | Stray agent-output markup (`</content>` / `</invoke>`) leaked into `packs.json` lines 110–111. Found and stripped; pack data itself unchanged. Logged so QA re-scans all canonical JSON for similar leaks (TC-XC-10). | Fixed | week7-wallet.md; MEMORY.md (agent file-output pitfalls) |
| BUG-014 | Medium | Build / CI | Unity batchmode logs a licensing handshake failure (`LicensingClient has failed validation; ignoring`). Unity proceeds on a cached/offline license — but if the license fully lapses, batchmode builds/CI start failing. | Open | unity-decisions.md Week-1 Flags |
| BUG-015 | Low | Data / Canon | Realm Map region names (`thornwood`, `mirewood`, `hollowfrost`, `emberwastes`, `starfall-reach`) have no canon authority — the React design doc and React code disagree, and region `description` prose was authored fresh. Owner must ratify before any Realm Map UI ships. Realm Map is deferred/v1.1. | Deferred | unity-decisions.md Week-1 Flags |
| BUG-016 | Low | Village | `ForceFieldShimmer` gate stand-in uses emissive intensity 1.4 and washes to white instead of reading violet in review shots. Cosmetic; the real shimmer shader is a Week-4 item. | Open | unity-decisions.md Week-3 Flags |
| BUG-017 | Medium | Village | Week-4 scene wiring outstanding: WaveManager / HeroAbilities / PetDeployer / BuildMenu UIDocument / ForceFieldGate material / enemy + building prefabs / enemy layer mask must be assembled into `Village.unity` for Wave 1 to play. C# compiles; scene assembly is a separate integration pass. | Fixed | unity-decisions.md Week-4 Flags; week4-integration.md |
| BUG-018 | Low | Village | Per-direction fog density gradient (spec §9.5 — denser fog toward the Wound/south) deferred — Unity built-in `RenderSettings` fog is uniform; needs a volumetric / URP fog pass. Ships with uniform exponential-squared fog. | Deferred | unity-decisions.md Week-3 Flags |
| BUG-019 | Medium | Onboarding / Canon | Cold-open intro (`StoryIntroController.ReactOpeningCinematic`) and the tutorial step 1 in `OnboardingFlow.cs` still reference the **retired Lantern motif** and the **retired village name "Avalon"** — directly contradicting DESIGN-DECISIONS #1 (Avalon → Elarion) and #18 (lantern motif dropped, Stone Choir framing). First-run players see five lantern/Avalon lines before they reach the village. The intro is a verbatim port from React `story.ts` that pre-dates the pivot. Story-content rewrite — owner / narrative team. | Open | DESIGN-DECISIONS.md #1, #18; STORYLINE.md |
| BUG-020 | High | Dungeons / Build | Reported 2026-05-20 by Samantha: "entering to healers cottage dungeon loads dungeon stub." Investigation found two likely root causes — see Detail notes. **Primary, on-disk:** `Assets/Scenes/Dungeon_FolksGranary.unity` is currently the **stub** output (contains a root-level `DungeonHeroPlaceholder` capsule; 33 distinct GameObjects vs HC's 93; 793 KB vs HC's 2.8 MB). `GrantPolishBuilder.BuildAll` rebuilds Healer's Cottage via `DungeonSceneBuilder.BuildHealersCottage()` but **never calls `FolksGranaryBuilder.Build()`** — so every grant-polish build leaves Folks Granary as whatever was last on disk (the stub). If the user actually entered the east portal (Folk's Old Granary), the stub is what they saw. **Secondary, runtime:** if the user did enter the west portal (Healer's Cottage), the on-disk HC scene is the full authored dungeon — they may be running a stale build artifact pre-dating the last `DungeonSceneBuilder` rebuild, or a player-build asset-bundle issue is loading older content. | Open | ad hoc (user report 2026-05-20) |

---

## Detail notes

**BUG-001** — Investigated extensively headlessly: FBX importer remaps verified correct (all 404 models map to a valid `hexagons_medieval_URP.mat`); scene `_BaseColor` values verified correct in the serialized `Village.unity`; code hardening shipped (`ForceHexMaterial`, `MakeFlatMaterial` copies a known-good `.mat`, `ApplyColorAll`). Centerpiece stayed white across all changes — strong evidence it is NOT a material-assignment bug. Owner flagged "finetune later"; Week-3 deliverable stands. Needs the interactive Unity Editor to inspect the live renderer. Re-test: TC-VIL-03.

**BUG-003** — Root cause is the test harness (`TestSupport.SpawnService`), not production code. In a real build `Awake` self-heals a null `_state`; EditMode never calls `Awake`. Fix: inject the private fields *after* `go.SetActive(true)`. Production `GameStateService`/`GameState` left unchanged. Status `Fixed` — QA to move to `Verified` after re-running the Core EditMode suite (TC-CORE-01, TC-CORE-02).

**BUG-004** — `public/audio/` contains only `battle/defeat/title/victory/village.mp3`. Both dungeon code paths are fully wired and guard the missing clip (warning + silence, no crash). When the owner supplies the tracks, import `echoes-beneath-elarion.mp3` to `Assets/Audio/dungeons/` and assign to `DungeonController._ambientBgmClip`; assign `lantern-flicker.mp3` to `Lantern._flickerAudio`. No code change needed. Re-test: TC-DUN-07, TC-DUN-15.

**BUG-006** — **Fix verified in source 2026-05-20.** `Assets/Editor/VillageSceneBuilder.cs:347` calls `BakeVillageNavMesh(root)` inside `BuildGameplaySystems` (line 330). Bake runs as part of the village scene builder; needs an Editor build run to confirm the bake takes (the runtime warning suppresses cleanly when the NavMesh exists). Re-test: TC-VIL-08, TC-VIL-09.

**BUG-007** — **Fix verified in source 2026-05-20.** `Assets/Editor/DungeonSceneBuilder.cs:1955-2002` adds `VerifyWallColliders` — a final hardening pass that walks every object under `Walls` / `VerticalConnectors` and force-adds a collider where the FBX import dropped one, while preserving any `[ILLUSORY]`-prefixed hidden passage. `FolksGranaryBuilder.cs` also calls `EnsureCollider` at five wall/structure placements. Cannot prove non-walk-through at runtime without a build; the code path that would close the bug is in place. Re-test: TC-DUN-05 (Healer's Cottage), TC-DUN-13 (Folk's Granary).

**BUG-008** — **Fix verified in source 2026-05-20.** `BattleParams.ReturnScene` field added at `Assets/_Modules/Core/SceneRouter.cs:55` (defaults to `SceneRouter.Village`); `BattleController.ResolveReturnScene` at `Assets/_Modules/BattleATB/BattleController.cs:432-438` reads it and the hard-coded village return is gone. Dungeon caller must set `ReturnScene = SceneRouter.DungeonHealersCottage` (or `DungeonFolksGranary`) on the handoff before the battle. Re-test: TC-DUN-14, TC-CORE-09, UAT B11.

The dungeon side is fully wired to resume (`DungeonRuntimeState` carries the encounter + hero vitals across the scene load). With BUG-008 closed, the village-vs-dungeon return branch is decided by the handoff, not hard-coded.

**BUG-010** — Every SDK-touching line is inside `#if SOLANA_SDK` in `SolanaWalletProvider.cs` and marked `// SDK-VERIFY:`. An API mismatch breaks only that guarded block, never the rest of the module. Integrator path is in `week7-wallet.md` "Devnet test path".

**BUG-013** — Per MEMORY.md, background subagents have leaked `</content>` / `</invoke>` markup into source/data files before. QA should periodically scan all `StreamingAssets/Data/Canonical/*.json` and module source for stray tags (TC-XC-10). This entry is `Fixed` for `packs.json`; keep the scan as a standing check.

**BUG-017** — **Fix verified in source 2026-05-20.** `Assets/Editor/VillageSceneBuilder.cs:330` calls `BuildGameplaySystems(root, gateRoot, heart)`, which assembles WaveManager / HeroAbilities / PetDeployer / BuildMenu into the village; the HUD bridges follow at lines 375 (`WireBuildMenuHudBridge`) and 378 (`WireHeroAbilitiesHudBridge`). The Week-4 row of `unity-decisions.md` already records this as "Resolved 2026-05-19" by the Week-4 scene-integration pass — this row was a bookkeeping lag. Re-test: TC-VIL-10, TC-VIL-11, UAT A8 / A9.

**BUG-019** — Specific stale lines to rewrite (all in `Assets/_Modules/Onboarding/StoryIntroController.cs`'s `ReactOpeningCinematic` table):

- L290 — `"the Lantern of Avalon, which never dimmed,"`
- L292 — `"Avalon. A village. A promise. A home."`
- L299 — `"waiting for the one the Lantern will answer to."`
- L305 — `"…yet the flame brightens at your step."`
- L306 — `"Welcome home, Guardian of the Lantern."`

Plus `OnboardingFlow.cs:180` — the tutorial.steps.1 inline comment quotes `"Welcome home, Guardian. The Heart is the Lantern…"` (the live string is loaded from `en.json` `tutorial.steps.1` — confirm that key's value, not just the comment).

The intro plays on first launch before the player reaches the village, so this contradicts the Stone-Choir framing the moment a new player arrives. Story-content rewrite, not a code change — narrative team / owner should author replacement beats that frame the **Cathedral Spire holding the last note of the Heart-Tree's song** (per `STORYLINE.md` §1) and replace every "Lantern" / "Avalon" mention. After the rewrite, also re-check the `tagline` keys in `canon-strings.json` (the title-screen tagline is the matching surface — UAT step F2).

**BUG-020** — Concrete evidence collected 2026-05-20:

- `Assets/Scenes/Dungeon_HealersCottage.unity`: 2.8 MB, 575 GameObjects, 93 distinct names including `Bryn`, `BrynBody`, `Lantern`, `LoreStone_journal-1..4`, `Checkpoint_checkpoint-entrance/crypt`, `CraftingPedestal`, `Encounter_*` (apprentice-of-the-apothecary / cellar-hollow-one / garden-hollow-one / main-room-hollow-pair / storage-hollow-one), `Room_root-cellar`, `Room_loft-bedroom`, `FollowCameraRig`. Last modified 2026-05-20 04:02. The eight `[PLACEHOLDER]` markers it carries (`rug over hidden trapdoor`, `ladder up to the Loft`, `hearth fireplace`, `stone sarcophagus`, …) are legitimate "no KayKit mesh" markers that `DungeonSceneBuilder` adds, **not** stub markers. **This scene file is the real authored Healer's Cottage.**

- `Assets/Scenes/Dungeon_FolksGranary.unity`: 793 KB, 33 distinct names including a root-level `DungeonHeroPlaceholder` capsule (built only by `DungeonStubBuilder.BuildHeroSpawn`). **This scene file is the stub, not the authored Folks Granary that `Assets/Editor/FolksGranaryBuilder.cs` should produce.**

- `Assets/Editor/GrantPolishBuilder.cs:39` calls `DungeonSceneBuilder.BuildHealersCottage()` — it does **not** call `FolksGranaryBuilder.Build()`. The five steps in `GrantPolishBuilder.BuildAll` (`VillageSceneBuilder.BuildVillage` → `WallRepairSceneSetup.AddWallRepairToVillage` → `IntroFlowSceneBuilder.BuildAll` → `DungeonSceneBuilder.BuildHealersCottage` → `BattleSceneBuilder.BuildBattleScene`) skip Folks Granary entirely. Every grant-polish rebuild therefore leaves the Granary as the stub.

- Village portal map (`Assets/Editor/VillageSceneBuilder.cs:1698-1701`): west portal `(-18, 0, 6)` → `Dungeon_HealersCottage`; east portal `(+18, 0, 6)` → `Dungeon_FolksGranary`. Labels are correct.

Recommended fixes:

1. **Add `FolksGranaryBuilder.Build()` (or whatever the entry point is named) to `GrantPolishBuilder.BuildAll`** as a 6th step after Healer's Cottage. Without it the Granary stays a stub indefinitely.
2. Re-run `Defenders/Build All (Grant-Polish Pass)` to regenerate both dungeon scenes on disk.
3. Rebuild the player so the bundled scene assets pick up the regenerated Folks Granary.
4. If the user clarifies they really did enter the **west** portal and saw a stub, dig further — at that point we know the on-disk scene is the real one, so the runtime issue is asset-bundling / older-build / DungeonController-failed-spawn territory. Ask the user to confirm which portal (west / east) they walked into, and ideally capture a screenshot.

---

## Summary (refreshed 2026-05-20)

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 8 (BUG-001, 003, 004, 006, 007, 008, 010, 020) |
| Medium | 7 (BUG-005, 009, 011, 012, 014, 017, 019) |
| Low | 5 (BUG-002, 013, 015, 016, 018) |
| **Total** | **20** |

By status: Fixed 6 (BUG-003, BUG-006, BUG-007, BUG-008, BUG-013, BUG-017) ·
Deferred 2 (BUG-015, BUG-018) · Open 12.

Source-state audit on 2026-05-20 moved four items from Open → Fixed
(BUG-006 / 007 / 008 / 017): the code paths that close each are in source today
(see the Detail notes for the file:line evidence). They remain `Fixed` rather
than `Verified` until the integrator runs the Unity build/Editor pass that QA
can re-test against (per the workflow at the top of this file).

Of the seven High-severity items, three remain `Open` and still threaten a
Week-8 acceptance gate: **BUG-001** (white centerpiece — predates the 2026-05-20
Cathedral Spire swap; needs re-investigation against the new centerpiece, not
the old tree), **BUG-004** (missing dungeon audio assets — owner must supply),
and **BUG-010** (Solana SDK not yet resolved — wallet runs on the stub). BUG-003
is `Fixed` and ready to move to `Verified` after the first green headless
EditMode run.

_Tend the Heart. Hold the dark._
