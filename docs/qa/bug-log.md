# Bug Log — Defenders of the Realm (v2 Unity Port)

**Project:** Defenders of the Realm — Unity 6 LTS / URP port
**Owner:** Samantha Denelle / DeNelle Studios
**Maintained by:** QA. Last updated 2026-05-19.

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
| BUG-006 | High | Village | Village NavMesh must be baked before enemies path — only legacy `com.unity.modules.ai` is in the manifest. Until the bake runs, enemies hold position and log a warning. Bake is wired into `VillageSceneBuilder.BuildVillage()`; needs an integration run to verify. | Open | unity-decisions.md Week-4 Flags; week4-integration.md |
| BUG-007 | High | Dungeons | "No walk-through walls" (Part 9.2 gate) is unverified — gated on every KayKit dungeon wall mesh carrying a collider, and illusory walls carrying none. Cannot confirm without a Unity build run. | Open | week5-dungeon-foundation.md (integrator checklist item 4) |
| BUG-008 | High | Core / Dungeons | `BattleController.ReturnAfterResult` hard-codes the post-battle return to `Village`; `BattleParams` has no return-scene field. A dungeon ATB battle therefore returns the player to the village, not the dungeon — breaks the Part 9.2 dungeon round-trip. | Open | week6-dungeon-systems.md |
| BUG-009 | Medium | BattleATB | `BattleController`'s per-enemy breach-roster mapper is still a stub — the ATB enemy roster may not reflect the actual breaching enemies. | Open | week4-integration.md (Known follow-ups) |
| BUG-010 | High | Wallet / Build | Solana Unity SDK package (`com.solana.unity_sdk`) is not yet resolved; the real `SolanaWalletProvider` only compiles once Unity auto-defines `SOLANA_SDK`. Integrator must resolve the package, add the SDK asmdef names to `DeNelle.Wallet.asmdef` references, and verify the `// SDK-VERIFY:` API calls against the live SDK. Module runs over the stub until then. | Open | week7-wallet.md |
| BUG-011 | Medium | Wallet | `WalletEndpoints.SkrMintDevnet` is empty — no devnet SKR mint address is published. The spec deliverable "buy a Hearth Spark pack with 25 devnet SKR" cannot run; the SKR rail fails cleanly with a descriptive error. SOL + USDC rails work. Owner must supply the devnet SKR mint. | Open | week7-wallet.md |
| BUG-012 | Medium | Data / Canon | Canon names **Bryn, Mara, Tovin, Eira, Aelf, Mira** are listed as canon in spec Part 1 but appear nowhere in `narrative-bible.md` or the v1 source — carried in `canon-strings.json` as flagged placeholders. Must be ratified before any Week-6 dungeon text ships. | Open | unity-decisions.md Week-1 Flags |
| BUG-013 | Low | Data | Stray agent-output markup (`</content>` / `</invoke>`) leaked into `packs.json` lines 110–111. Found and stripped; pack data itself unchanged. Logged so QA re-scans all canonical JSON for similar leaks (TC-XC-10). | Fixed | week7-wallet.md; MEMORY.md (agent file-output pitfalls) |
| BUG-014 | Medium | Build / CI | Unity batchmode logs a licensing handshake failure (`LicensingClient has failed validation; ignoring`). Unity proceeds on a cached/offline license — but if the license fully lapses, batchmode builds/CI start failing. | Open | unity-decisions.md Week-1 Flags |
| BUG-015 | Low | Data / Canon | Realm Map region names (`thornwood`, `mirewood`, `hollowfrost`, `emberwastes`, `starfall-reach`) have no canon authority — the React design doc and React code disagree, and region `description` prose was authored fresh. Owner must ratify before any Realm Map UI ships. Realm Map is deferred/v1.1. | Deferred | unity-decisions.md Week-1 Flags |
| BUG-016 | Low | Village | `ForceFieldShimmer` gate stand-in uses emissive intensity 1.4 and washes to white instead of reading violet in review shots. Cosmetic; the real shimmer shader is a Week-4 item. | Open | unity-decisions.md Week-3 Flags |
| BUG-017 | Medium | Village | Week-4 scene wiring outstanding: WaveManager / HeroAbilities / PetDeployer / BuildMenu UIDocument / ForceFieldGate material / enemy + building prefabs / enemy layer mask must be assembled into `Village.unity` for Wave 1 to play. C# compiles; scene assembly is a separate integration pass. | Open | unity-decisions.md Week-4 Flags; week4-integration.md |
| BUG-018 | Low | Village | Per-direction fog density gradient (spec §9.5 — denser fog toward the Wound/south) deferred — Unity built-in `RenderSettings` fog is uniform; needs a volumetric / URP fog pass. Ships with uniform exponential-squared fog. | Deferred | unity-decisions.md Week-3 Flags |

---

## Detail notes

**BUG-001** — Investigated extensively headlessly: FBX importer remaps verified correct (all 404 models map to a valid `hexagons_medieval_URP.mat`); scene `_BaseColor` values verified correct in the serialized `Village.unity`; code hardening shipped (`ForceHexMaterial`, `MakeFlatMaterial` copies a known-good `.mat`, `ApplyColorAll`). Centerpiece stayed white across all changes — strong evidence it is NOT a material-assignment bug. Owner flagged "finetune later"; Week-3 deliverable stands. Needs the interactive Unity Editor to inspect the live renderer. Re-test: TC-VIL-03.

**BUG-003** — Root cause is the test harness (`TestSupport.SpawnService`), not production code. In a real build `Awake` self-heals a null `_state`; EditMode never calls `Awake`. Fix: inject the private fields *after* `go.SetActive(true)`. Production `GameStateService`/`GameState` left unchanged. Status `Fixed` — QA to move to `Verified` after re-running the Core EditMode suite (TC-CORE-01, TC-CORE-02).

**BUG-004** — `public/audio/` contains only `battle/defeat/title/victory/village.mp3`. Both dungeon code paths are fully wired and guard the missing clip (warning + silence, no crash). When the owner supplies the tracks, import `echoes-beneath-elarion.mp3` to `Assets/Audio/dungeons/` and assign to `DungeonController._ambientBgmClip`; assign `lantern-flicker.mp3` to `Lantern._flickerAudio`. No code change needed. Re-test: TC-DUN-07, TC-DUN-15.

**BUG-008** — The dungeon side is fully wired to resume (`DungeonRuntimeState` carries the encounter + hero vitals across the scene load). The remaining piece is the village-vs-dungeon return branch in `BattleController` (or a `ReturnScene` field on `BattleParams`) — a Core/BattleATB change. Blocks UAT step B11 and TC-DUN-14 / TC-CORE-09.

**BUG-010** — Every SDK-touching line is inside `#if SOLANA_SDK` in `SolanaWalletProvider.cs` and marked `// SDK-VERIFY:`. An API mismatch breaks only that guarded block, never the rest of the module. Integrator path is in `week7-wallet.md` "Devnet test path".

**BUG-013** — Per MEMORY.md, background subagents have leaked `</content>` / `</invoke>` markup into source/data files before. QA should periodically scan all `StreamingAssets/Data/Canonical/*.json` and module source for stray tags (TC-XC-10). This entry is `Fixed` for `packs.json`; keep the scan as a standing check.

---

## Summary (seeded set, 2026-05-19)

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 7 (BUG-001, 003, 004, 006, 007, 008, 010) |
| Medium | 6 (BUG-005, 009, 011, 012, 014, 017) |
| Low | 5 (BUG-002, 013, 015, 016, 018) |
| **Total** | **18** |

By status: Fixed 2 (BUG-003, BUG-013) · Deferred 2 (BUG-015, BUG-018) · Open 14.

No Critical bugs are recorded yet — but seven High-severity items each threaten a
Week-8 acceptance gate and must be closed or formally deferred before the UAT in
`docs/qa/uat-script.md` can pass. The standout High items: BUG-001 (white
centerpiece), BUG-007 (unverified dungeon wall collision), BUG-008 (dungeon ATB
round-trip returns to the wrong scene), and BUG-010 (Solana SDK not yet resolved).

_Tend the Heart. Hold the dark._
