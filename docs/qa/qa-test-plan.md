# QA Test Plan — Defenders of the Realm (v2 Unity Port)

**Project:** Defenders of the Realm — Unity 6 LTS / URP port
**Owner:** Samantha Denelle / DeNelle Studios
**QA scope:** functional verification of the v2 Unity foundation (Weeks 1–8)
**Source of truth:** `docs/v2-unity-port-spec.md` (Part 5 build order, Part 9 acceptance gates), `docs/unity-decisions.md`
**Status:** Living document. Last updated 2026-05-19 (Weeks 1–4 committed/compiling; Weeks 5–7 written, mid-integration; no end-to-end playable build yet).

---

## How to use this plan

- Each test case has an **id**, **area**, **steps**, **expected result**, and a **Checkable** column.
- **Checkable** values:
  - `Editor` — runnable now in the Unity Editor or via batchmode (EditMode/PlayMode tests, scene builders, asset inspection). No playable build required.
  - `Build` — needs the Week-8 end-to-end playable build (APK / Windows EXE). Park until Week 8.
  - `Editor (partial)` — partly verifiable now (code compiles, scene builder runs), full verification needs the build.
- **Result** column: leave blank until executed, then mark `PASS` / `FAIL` / `BLOCKED` / `N/A` with a date and tester initials. A `FAIL` must produce a row in `docs/qa/bug-log.md`.
- Run the full `Editor`-checkable set on every weekly integration commit. Run the full `Build` set once per Week-8 release candidate.
- Test cases trace to spec parts in the **Ref** column so a reviewer can audit coverage.

### Test environment matrix

| Env | Purpose | Quality level |
|-----|---------|---------------|
| Unity Editor (6000.x LTS, Mono) | EditMode/PlayMode tests, scene-builder verification, inspection | Desktop |
| Windows EXE (IL2CPP) | Desktop playthrough, save/load, perf sanity | Desktop |
| Seeker emulator / device (Android, IL2CPP) | Acceptance playthrough, 60 FPS gate, memory ceiling, wallet MWA | Seeker_High |

### Status legend per module (2026-05-19)

| Module | Build state | Notes |
|--------|-------------|-------|
| Core (save/load, scene router) | Compiling; 59/59 EditMode tests pass after harness fix | See bug-log BUG-003 (was 16 failing) |
| BattleATB engine | Compiling; EditMode tests authored | Pure C#; scene wiring partial |
| Village (Weeks 3–4) | Compiling; scene wiring is a separate integration pass | NavMesh bake + UIDocument wiring outstanding |
| Dungeons (Weeks 5–6) | Source written; scene wiring outstanding | ATB return-scene branch missing |
| Wallet (Week 7) | Source written; Solana SDK not yet resolved | SKR devnet mint missing |

---

## 1. Core — save/load, scene routing, state

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-CORE-01 | Save round-trip (EditMode) | Run the Core EditMode suite (`SaveLoadRoundTripTest`). | All save/load round-trip tests pass; a saved `GameState` reloads with identical field values. | Editor | Wk1 | |
| TC-CORE-02 | Service construction | Run `ResetCarveOutTest` + `SaveLoadRoundTripTest` (the 16 service tests). | `GameStateService._state` is a live non-null `GameState` SO; no `NullReferenceException` logged. | Editor | bug-log BUG-003 | |
| TC-CORE-03 | Schema validation | Run `SaveSchemaValidateTest`. | Save JSON shape matches the C# `SaveSchema` record; `SaveSchema.Version` is set. | Editor | Part 4 | |
| TC-CORE-04 | Save migration | Run `SaveMigratorTest`. | Each registered migration step transforms an old-version save to current without data loss. | Editor | Wk1 | |
| TC-CORE-05 | New Game → quit → relaunch | In a build: New Game, accrue some resources/wave progress, quit app, relaunch. | Save resumes with same hero HP, pet bond, resources, wave number. | Build | Part 9.6 | |
| TC-CORE-06 | Save survives app kill mid-playthrough | In a build: enter village, kill the app process (not a clean quit), relaunch. | Last checkpoint/autosave restores; no corrupt-save crash. | Build | Part 9.6 | |
| TC-CORE-07 | Scene router transitions | From each scene, trigger `SceneRouter` loads (Title→Village, Village→ATBBattle, Village→Dungeon, Dungeon→ATBBattle). | Target scene loads with fade; no missing-scene exception. | Build | Wk1 | |
| TC-CORE-08 | Build Settings scene list | Inspect `EditorBuildSettings`. | Title, Village, Dungeon_HealersCottage, ATBBattle are all present and enabled. | Editor | wk6 note item 8 | |
| TC-CORE-09 | ATB return-scene routing | From a dungeon encounter, win an ATB battle. | Control returns to `Dungeon_HealersCottage`, not Village (when `PendingBattle.Wave == 0`). | Build | bug-log BUG-008 | |
| TC-CORE-10 | Canon-string loader | Confirm `canon-strings.json` + `locale/en.json` load via the typed loader at scene init. | Loaders hydrate without parse error; canon keys resolve. | Editor | Part 4 | |
| TC-CORE-11 | Save version bump | Bump `SaveSchema.Version`, load a save written at the prior version. | Migrator runs; loaded state is valid at the new version. | Editor | Part 9.6 | |

## 2. BattleATB — combat engine

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-ATB-01 | RNG determinism | Run `RngGoldenVectorTest`. | Same seed produces the same sequence; matches the golden vector (TS-parity / anti-cheat property). | Editor | Wk2, anti-cheat-spec | |
| TC-ATB-02 | Targeting | Run `TargetingTest`. | Target selection picks the spec'd combatant for each targeting mode. | Editor | Wk2 | |
| TC-ATB-03 | Actions resolution | Run `ActionsTest`. | Attack/ability actions apply correct damage, splash, and status per the TS port. | Editor | Wk2 | |
| TC-ATB-04 | Combat math | Run `CombatTest`. | Damage formula, defense halving, status ticks, heal clamp, kill flag all correct. | Editor | Wk2 | |
| TC-ATB-05 | Turn / ATB bar fill | Run `TurnTest`. | ATB bars fill, ready detection fires, turn order resolves deterministically. | Editor | Wk2 | |
| TC-ATB-06 | AI choice | Run `AiTest`. | AI returns the spec'd action for tank/healer/damage roles given a battle state. | Editor | Wk2 | |
| TC-ATB-07 | Battle state lifecycle | Run `BattleStateTest`. | Battle state initializes, mutates, and reports outcome (win/lose) correctly. | Editor | Wk2 | |
| TC-ATB-08 | Battle scaling | Run `BattleScalingTest`. | Enemy stat scaling by wave/tier matches the scaling table. | Editor | Wk2 | |
| TC-ATB-09 | Placeholder battle scene plays | Open `ATBBattle.unity`, enter Play, submit "Attack". | Engine resolves the turn, the log scrolls, the battle reaches a win or lose. | Editor (partial) | Wk2 deliverable | |
| TC-ATB-10 | Battle outcome → caller | Win/lose a battle launched from the village. | Outcome applies damage/result back into GameState; control returns to caller. | Build | Wk4 | |
| TC-ATB-11 | Breach-roster mapper | Trigger a breach with N enemies, inspect the ATB roster. | The ATB enemy roster reflects the actual breaching enemies (not a stub set). | Build | bug-log BUG-009 | |
| TC-ATB-12 | Cross-engine parity | Run a seeded battle (seed=42) in Unity; compare outcome to the React engine. | Identical outcome — same seed, same result. | Editor | Wk2, Part 9 | |

## 3. Village — build, waves, enemies, pets, gate, hero

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-VIL-01 | Village scene builds | Run `Defenders > Week 3 > Build Village Scene`. | Builder completes; console summary shows 5/5 building prefabs, 4 force-field gates, NavMesh bake count > 0; no `MISSING`/`not found`. | Editor | wk4-integration | |
| TC-VIL-02 | Wall ring layout | Inspect `WallLayout.cs` output / built scene. | Perimeter wall sections + cardinal gate gaps generate per the Avalon layout spec (rectangle with south bow-out). | Editor | Wk3 | |
| TC-VIL-03 | Heart renders & glows | Open `Village.unity`; inspect the Elarion centerpiece. | Heart mesh renders with the violet crystal emissive (NOT a white mass). | Editor | bug-log BUG-001 | |
| TC-VIL-04 | Heart threat states | Drive `HeartController.SetState` through serene→vigilant→warning→danger→critical. | Emissive color steps through each threat tier. | Editor (partial) | Wk3 | |
| TC-VIL-05 | Hero walks the village | Enter Play in `Village.unity`; move with WASD / tap-to-move. | Blaise (Mage) moves smoothly via CharacterController; camera follows. | Build | Wk3 | |
| TC-VIL-06 | NavMesh baked | Window > AI > Navigation after a village build. | Blue NavMesh covers village interior + 4 approach lanes; walls/buildings carved as obstacles. | Editor | bug-log BUG-006 | |
| TC-VIL-07 | Build menu opens & places | Open the build menu, place a building on a valid ground tile, pay crystals. | Building places at the tap target; crystal cost deducts; invalid tiles rejected. | Build | Wk4 | |
| TC-VIL-08 | Five buildings buildable | Build each of Crystal Mine, Pet House, Arcane Tower, Workshop, Farm. | Each places with HP from `buildings.json`; footprint collider present. | Build | Wk4 | |
| TC-VIL-09 | Wave 1 fires | Enter Play; let the ~45s prepare countdown elapse. | 8 Hollow Walkers spawn at the north gate and path toward the Heart on the NavMesh. | Build | Wk4 deliverable | |
| TC-VIL-10 | Waves 2–3 | Survive Wave 1, continue. | Waves 2 and 3 spawn per the authored wave table. | Build | wk4-waves | |
| TC-VIL-11 | Enemy contact damage | Let an enemy reach a wall / building / gate. | Structure takes contact damage via `IDamageableStructure`; HP/damage value updates. | Build | wk4-integration | |
| TC-VIL-12 | Enemy death | Reduce an enemy to 0 HP with hero/pet/tower damage. | Enemy dies and despawns; no `NavMeshAgent` warning spam. | Build | Wk4 | |
| TC-VIL-13 | Hero abilities Q/W/E/R | Cast bolt (Q), frost nova (W), beacon (E), meteor (R). | Each ability fires, consumes mana, hits enemies in the enemy mask; E heals the Heart. | Build | Wk4 | |
| TC-VIL-14 | Starter pets deploy | Enter Play; observe the Heart ring. | Aether Sprite, Flame Pup, Ice Wolf deploy in a ring and hunt the nearest enemy. | Build | Wk4 | |
| TC-VIL-15 | Pet bond / AI mode | Set a pet bond rank; switch AI mode (aggressive/defensive/balanced). | Pet behavior reflects bond rank and selected AI mode. | Build | Wk4 | |
| TC-VIL-16 | Gate force-field damage | Attack a gate down past 25% HP. | Force-field shimmer collapses, blocker collider toggles off, enemies pour through; gate still attackable to 0 HP. | Build | Wk4 | |
| TC-VIL-17 | Breach → ATB hand-off | Let an enemy cross the inner wall ring. | `SceneRouter.GoBattle` fires; ATBBattle loads with the breaching enemies as the roster. | Build | Wk4 deliverable | |
| TC-VIL-18 | Return from ATB to village | Resolve the breach battle. | Control returns to Village; battle damage applied; wave continues. | Build | Wk4 | |
| TC-VIL-19 | Resource bar HUD | Observe the HUD during play. | Crystals / food / coins and Heart HP bar read live from GameState. | Build | Wk3 | |
| TC-VIL-20 | Exterior terrain renders | Inspect the exterior wilderness. | Terrain is lit (not black); a skybox is present; no strays floating off the terrain. | Editor (partial) | bug-log BUG-002 | |
| TC-VIL-21 | Gate yaw / seam | Inspect each cardinal gate against its wall opening. | Gate fills the wall opening flush (no 90° offset). | Editor | wk3 flag (resolved — regression check) | |

## 4. Dungeons — Healer's Cottage (hero, lantern, lore, encounters, checkpoints)

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-DUN-01 | Dungeon scene builds | Run the dungeon scene builder. | `Dungeon_HealersCottage.unity` builds the 12-room layout; no missing-mesh errors. | Editor | Wk5 | |
| TC-DUN-02 | Hero spawns at entrance | Enter Play; observe spawn. | Hero spawns at the SW Garden Approach entry room per the layout JSON `spawn` block. | Build | Wk5 deliverable | |
| TC-DUN-03 | Hero walks all rooms | Walk the hero through every room and corridor. | Smooth WASD + tap-to-move; camera follows at the isometric tilt. | Build | Wk5 | |
| TC-DUN-04 | No walk-through walls | Walk the hero into every solid wall. | CharacterController slides along walls; hero never clips through. | Build | bug-log BUG-007, Part 9.2 | |
| TC-DUN-05 | Illusory wall passable | Walk into the illusory wall to the secret room. | Hero passes through (illusory walls carry no collider). | Build | wk5 checklist 4 | |
| TC-DUN-06 | Lantern PointLight | Observe the hero's lantern light over time. | A PointLight follows the hero; intensity falls as oil drops; refills at oil stones. | Build | Wk6, Part 9.2 | |
| TC-DUN-07 | Lantern low-oil audio | Let lantern oil drop low. | `lantern-flicker.mp3` plays IF imported; if absent, lantern stays silent (no crash). | Build | bug-log BUG-004 | |
| TC-DUN-08 | Bryn encounter | Approach Bryn at the cottage entrance. | Bryn's world-space speech bubble shows the canon cottage-entry line from `lore-fragments.json#bryn-cottage-entry`. | Build | Wk6 deliverable | |
| TC-DUN-09 | Lore-stone read | Tap each of the 5 lore-stones. | The lore modal opens with the verbatim journal text for that stone id. | Build | Wk6 | |
| TC-DUN-10 | journal-vault placeholder flagged | Read the `journal-vault` lore-stone. | The placeholder paragraph is flagged at runtime (`IsPlaceholderFragment`); it is NOT silently shipped as canon. | Editor (partial) | bug-log BUG-005 | |
| TC-DUN-11 | Checkpoint heal + save | Walk the hero into a checkpoint shrine. | Hero heals to full (`HealHeroToFull`); progress saves; toast requested. | Build | Wk6 | |
| TC-DUN-12 | Scripted ATB encounter | Enter a scripted encounter zone. | An ATB battle launches via `SceneRouter.GoBattle` with `Wave == 0` (dungeon marker). | Build | Wk6 deliverable | |
| TC-DUN-13 | Mini-boss encounter | Trigger the Workshop mini-boss. | `ConfigureBoss` drives the encounter; a boss victory marks the boss defeated. | Build | Wk6 | |
| TC-DUN-14 | ATB round-trip preserves vitals | Win a dungeon ATB battle. | Hero HP/mana carry across via `DungeonRuntimeState`; hero resumes at `EncounterResumePosition`. | Build | bug-log BUG-008 | |
| TC-DUN-15 | Dungeon ambient BGM | Enter the dungeon. | `echoes-beneath-elarion.mp3` loops at volume 0.25 IF imported; if absent, silent + one warning (no crash). | Build | bug-log BUG-004 | |
| TC-DUN-16 | Random encounter table | Walk repeatedly through random-encounter zones with a fixed seed. | Encounters fire per the seeded `RandomEncounterTable`; deterministic for a given seed. | Editor (partial) | Wk6 | |

## 5. Wallet — Solana devnet, pack store

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-WAL-01 | Module compiles without SDK | Build the project with the Solana SDK package absent. | Wallet module compiles and runs over `StubWalletProvider`; no broken assembly. | Editor | wk7 note | |
| TC-WAL-02 | SDK resolves, define flips | Resolve `com.solana.unity_sdk`; check scripting defines. | Unity auto-defines `SOLANA_SDK`; `SolanaWalletProvider` SDK block compiles. | Editor | bug-log BUG-010 | |
| TC-WAL-03 | Network defaults to Devnet | Inspect `WalletService.DefaultNetwork`. | Value is `WalletNetwork.Devnet`; no Mainnet path active. | Editor | Part 10 | |
| TC-WAL-04 | Mainnet hard-block | Force `Network = Mainnet`, attempt a payment. | `SolanaWalletProvider.SendPayment` returns a failure (defensive block). | Editor | Part 10 | |
| TC-WAL-05 | No secrets in repo | Search the repo + git history for keys/seed phrases. | `wallets.json` holds only 2 public base58 addresses; zero private keys / seed phrases anywhere. | Editor | Part 10 | |
| TC-WAL-06 | Wallet connect (stub) | Open the wallet connect dialog with the stub provider. | Stub "connects", reports a mock balance for SOL/USDC/SKR. | Build | Wk7 | |
| TC-WAL-07 | Wallet connect (real, devnet) | Connect Phantom (desktop) or Seeker (MWA) on devnet. | Wallet connects; `GetBalance` returns real devnet SOL/USDC. | Build | Part 9.4 | |
| TC-WAL-08 | Pack store renders 5 packs | Open the pack store. | Hearth Spark, Lanternlight, Folk's Thanks, Patron of Elarion, Founder's Vow render from `packs.json` with per-currency amounts. | Build | Wk7 | |
| TC-WAL-09 | Pack purchase (SOL rail, devnet) | Buy a pack on the SOL rail on devnet. | Transfer tx lands at the devnet recipient `3Eeww…`, confirms, pack contents apply to GameState. | Build | Part 9.4 | |
| TC-WAL-10 | Pack purchase (SKR rail) | Buy the Hearth Spark pack with 25 devnet SKR. | Succeeds once `WalletEndpoints.SkrMintDevnet` is set; fails cleanly with a descriptive error until then. | Build | bug-log BUG-011 | |
| TC-WAL-11 | Rewards Distributor transparency | View the title/settings transparency label. | The public Rewards Distributor address `2JRmE…` displays; it is NOT used as a payment destination. | Build | Wk7 | |
| TC-WAL-12 | Covenant compliance | Inspect the pack store contents. | No loot boxes, no gacha, no randomized purchases, no energy systems, no combat-stat sales — convenience power only. | Editor | Part 10 | |

## 6. Onboarding — title, bumper, story intro

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-ONB-01 | Studio bumper plays | Launch the app fresh. | DeNelle Studios bumper plays (~3s) then fades to the Title scene. | Build | Wk1 | |
| TC-ONB-02 | Title screen content | Observe the Title scene. | Heart-Wing banner, tagline "By lantern. By oath. By Heart.", Connect Wallet + Start buttons render. | Build | Wk1 | |
| TC-ONB-03 | Story intro cold open | First launch (no save). | The 3-line cold open from the narrative bible auto-plays (~5s). | Build | Wk1 | |
| TC-ONB-04 | Start button → Village | Press Start on the Title scene. | Village scene loads. | Build | Wk1 | |

## 7. Cross-cutting — performance, audio, canon, data layer

| ID | Area | Steps | Expected result | Checkable | Ref | Result |
|----|------|-------|-----------------|-----------|-----|--------|
| TC-XC-01 | 60 FPS — village wave | Record the Profiler during Wave 1 on Seeker_High. | 60 FPS held; frame-time spikes ≤ 33 ms. | Build | Part 9.3 | |
| TC-XC-02 | 60 FPS — dungeon walk | Record the Profiler while walking the dungeon on Seeker_High. | 60 FPS held; frame-time spikes ≤ 33 ms. | Build | Part 9.3 | |
| TC-XC-03 | Memory ceiling | Monitor memory across the 5-minute playthrough. | Total memory stays ≤ 400 MB. | Build | Part 9, Wk8 | |
| TC-XC-04 | No crashes / softlocks | Run the full 5-minute acceptance playthrough. | Zero crashes; zero softlocks. | Build | Part 9.1 | |
| TC-XC-05 | Audio mix levels | Listen across all scenes. | Music/SFX/UI/Voice play at the `audio-mix-spec.md` levels; music crossfades at scene transitions. | Build | Part 9.5 | |
| TC-XC-06 | Canon names on screen | Inspect every UI surface for canon strings. | Avalon, Elarion, Blaise, Alduin the Mournful, the Heart-Wing, the tagline, DeNelle Studios all appear correctly. | Build | Part 9.7 | |
| TC-XC-07 | No rogue inline canon strings | Search the codebase for hard-coded canon strings. | Zero inline uses — all flow through `canon-strings.json` / `locale/en.json`. | Editor | Part 9.7 | |
| TC-XC-08 | Unsourced canon names | Check Bryn, Mara, Tovin, Eira, Aelf, Mira against a canon source. | Each name has a ratified canon source before any text using it ships. | Editor | bug-log BUG-012 | |
| TC-XC-09 | Data layer present & loaded | Confirm every Part 4 JSON file exists and has a C# loader. | Each JSON exists in the repo and is consumed by at least one loader; schema tests pass. | Editor | Part 9.9 | |
| TC-XC-10 | Data files free of stray markup | Scan all `StreamingAssets/Data/Canonical/*.json` for leaked agent markup. | No `</content>`, `</invoke>`, or similar tags in any data file. | Editor | bug-log BUG-013 | |
| TC-XC-11 | Build outputs produced | Run the Week-8 build. | An Android `.apk` and a Windows `.exe` are produced and launch. | Build | Wk8 | |
| TC-XC-12 | Decisions log current | Review `docs/unity-decisions.md`. | Every architectural call from Weeks 1–8 has a complete row (date, decision, alternative, reason, reversibility). | Editor | Part 9.8 | |
| TC-XC-13 | Contractor-ready clone | On a clean machine, clone the repo and open in Unity 6 LTS. | Project opens, compiles, builds the APK with only `docs/README.md` as onboarding. | Build | Part 9.10 | |
| TC-XC-14 | EditMode suite green | Run the full EditMode test suite. | All tests pass (Core 59/59 + BattleATB suite + schema tests). | Editor | Part 9 | |

---

## Coverage summary

| Module | Test cases | Checkable now (Editor / partial) | Needs build |
|--------|-----------|----------------------------------|-------------|
| Core | 11 | 8 | 3 |
| BattleATB | 12 | 10 | 2 |
| Village | 21 | 5 | 16 |
| Dungeons | 16 | 4 | 12 |
| Wallet | 12 | 6 | 6 |
| Onboarding | 4 | 0 | 4 |
| Cross-cutting | 14 | 6 | 8 |
| **Total** | **90** | **39** | **51** |

39 test cases are exercisable today against the Editor / batchmode; the remaining 51 are gated on the Week-8 end-to-end playable build (see `docs/qa/uat-script.md`).

_Tend the Heart. Hold the dark._
