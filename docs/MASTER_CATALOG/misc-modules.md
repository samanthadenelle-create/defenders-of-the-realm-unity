# Master Catalog — Misc Modules (Dungeons foremost)

Verified **from the actual code** (not comments) **2026-08-02**. Supersedes the 2026-06-12 body and the
2026-07-22 STALE banner wholesale. Scope: `Assets/_Modules/Dungeons/**` (deep), the two-dungeon-path split,
runtime RoomForge types (editor bake stack is cross-referenced, not owned here), `Environment`/`Data`/`UI`
(Assembly-CSharp leftovers), and brief maps of `Pets`/`Cosmetics`/`Wallet`/`Web3`/`Audio`.

---

## 1. Dungeons — asmdef `DeNelle.Dungeons` (ns `DeNelle.Dungeons`), 38 files

**asmdef refs (`DeNelle.Dungeons.asmdef:4-16`): Core, `DeNelle.Village` (⚠ NEW — the old "Dungeons never
references Village, ATB only via SceneRouter" boundary is GONE), Data, Unity.Localization, UniTask,
Unity.Cinemachine, Unity.InputSystem, Unity.Addressables, Unity.ResourceManager, Unity.TextMeshPro,
UnityEngine.UI.** The Village ref carries the real-time `BattleArena` bridge, HeroLocomotion neutralize,
VirtualJoystick, TalkPromptRegistry, VillageInventory/LootTableCatalog (loot), ScreenFader. Still NO
`DeNelle.BattleATB` ref — the legacy ATB path goes through `SceneRouter.GoBattle`.

### 1.1 THE TWO-DUNGEON-PATH SPLIT (load-bearing)

| Path | Scene | Layout source | Controller | Combat | Exit |
|---|---|---|---|---|---|
| **Data-driven** | `Assets/Scenes/Dungeon_HealersCottage.unity` (YAML, 2.9 MB) | `StreamingAssets/Data/Canonical/dungeons/healers-cottage.json` via `DungeonLayoutLoader` | `DungeonController` (scene-placed, `Start()` → `EnterDungeon().Forget()`) | EncounterTrigger → real-time BattleArena (default) / legacy ATB | `ExitToVillage()` → `SceneRouter.Castle` + runtime exits (WO-770.1) |
| **Composed (RoomForge)** | `Assets/Scenes/DungeonCompose/dg_starter_loop.unity` — **BINARY-serialized** RoomForge scene. Quality bake 2026-08-21: 11 rooms / 11 valid mates / 1 intentional entry seal / `PathComplete`; fake stair dead ends removed and the route closes through `loop4`. | Dual-written `StreamingAssets` + runtime-winning `Resources` `dungeon-layouts/<id>.json` (`DungeonComposeLayout`) | **NO DungeonController** — root `DungeonCompose_*`; play-population by the editor baker; `DungeonExitSpawner` injects the way home | three escalating room-owned encounters with deterministic separated seats; side reward/oil room + one grate hazard | `DungeonExitSpawner`-injected `DungeonExitInteractable` → `SceneRouter.Castle` |

**Entry portals** (`Village/World/DungeonWorldPortalSpawner.cs:105-140`, Village module): EAST authored portal
(140,0,20) → **`dg_starter_loop`** (rerouted off HealersCottage); SOUTH portal (20,0,−140) → **`HealersCottage`**
(re-added — the reroute had left the richest dungeon unreachable); WEST **Folk's Granary row REMOVED (WO-776,
2026-07-30)** — contentless stub gated out of play (scene + builder stay in-repo, dev-only).
`Village/Buildings/DungeonPortal.cs:187-226` routes composed ids as full scene names, `"Dungeon_" + id` fallback
for classic ids.

**Editor bake stack cross-ref:** `DungeonBaker` / `DungeonDresser` / RoomForge window / `GraphDungeonComposer` /
`DungeonBaker.PopulateForPlay` live in `DeNelle.Editor.RoomForge` — see the editor-tools master-catalog file
(that catalog owns them). This file owns only the RUNTIME RoomForge types (§1.6).

### 1.2 Root runtime code

#### DungeonController.cs (1726 lines) — the Healer's Cottage orchestrator. LIVE.
- `Start()` → `EnterDungeon().Forget()` (`DungeonController.cs:213-216`). **`EnterDungeon()` flow (`:260-380`)**:
  disable hero input → **tag the Keeper `Player`** (F8 2026-07-30 — nothing else tags it in non-village scenes;
  untagged, BattleArena warp captured "<no Player>" and staged a PHANTOM fight, `:273-279`) →
  `DungeonLayoutLoader.LoadAsync` (hard-stop + re-enable input on null layout, `:282-292`) → lore + crafting data
  loads → `resuming = HasPendingEncounter` (the ATB round-trip return, `:315`) → `StartRun`/vitals seed →
  `PlaceHero` → `ConfigureCamera/Lantern/Bryn` → `DressEntranceNpc` → `HydrateLoreStones/Checkpoints/Encounters`
  → `ConfigureCrafting` → `HydrateChests` → **`HydrateExits`** → `ConfigureDungeonHud` → **`DressTraversalLinks`**
  → `SweepPlaceholderCubes` → `StartAmbientAudio` → `SubscribeRealtimeSettle` → (resume) `ResolvePendingEncounter`
  → `Ready = true`, input back (`:344-379`).
- **WO-775 `SeedHeroVitalsFromLiveHero` (`:395-444`)**: seeds run HP/mana from live `HeroHealth`/`HeroAbilities`
  (TryGetComponent + `HeroHealth.Instance` fallback); the 120/60 literals survive only as FlowTrace.Warn-guarded
  last-ditch fallbacks.
- **`HydrateExits` (WO-770.1, `:536-571`)** — fixes the roach-motel: runtime-injected NORMAL exit at the entry-room
  centre (always open) + BOSS BACK-DOOR at the `workshop` room, spawned hidden, revealed by `Update` on
  `BossDefeated` (`:507-511`); both route `ExitToVillage`. `SeatExitOnFloor` raycast seats the arch (`:578-583`).
- **`DressTraversalLinks` (WO-711, `:1357-1540`)** — runtime-authored door/stair ports, never a scene edit. Doors:
  one `DungeonPortLink` pair per `kind=="doorway"` wall (deduped; illusory walls skipped; `leadsTo:"exit"` warned),
  **anchored at each room's own built `wall_doorway` mesh** rather than the JSON midpoint (the two authorings
  drift ~4 u — the "Open Door prompt off the door mouth" fix; `CollectDoorMeshes`/`NearestDoorMeshXZ`
  `:1548-1607`). Stairs: a hardcoded 3-pair table for `healers-cottage` only (room→floor-Y map ground 0 /
  upper 6 / under −6, `:1370-1377`, `:1513-1530`); other dungeon ids get a Warn, no stair ports.
- **`SettleEncounter` (WO-770.3b, `:1161-1186`)** — the ONE settlement authority for BOTH battle paths: victory =
  `DungeonLootGrant.GrantEncounter` + `ResumeAfterEncounter(true)` (credits the boss when boss → unlocks the
  back-door), hero resumes in place; defeat = cancel the arena's pending return-warp
  (`BattleArena.Existing?.CancelPendingReturnWarp`, F8 seq512 defeat-freeze fix `:1170-1176`) + no loot/credit +
  `ExitToVillage`. ATB path reaches it via `ResolvePendingEncounter` (`:1128-1145`, outcome read off
  `SceneRouter.PendingBattle.LastOutcome` — WO-770.3: missing carrier = LOSS); real-time path via
  `OnRealtimeBattleEnded`.
- **Real-time settle bridge (`:1196-1288`)** — subscribes `BattleArena.OnBattleStaged/OnBattleEnded` (arena is a
  DDOL lazy singleton). On stage: `_arenaOwnsHero = true`, input off, **R-A1 (audit 2026-08-01): DISABLE the
  hero's CharacterController** (`SetHeroCharacterController(false)`, `:1230-1265` — input-off alone left
  `DungeonHero.Update` calling `Move(gravity)` every frame → TWO collision bodies fighting one transform),
  un-neutralize HeroLocomotion (the arena drives it), force `_cameraRig.SetCombatFraming(true)` (OTS combat).
  On end: restore CC **before** input, restore framing, settle only if WE have a pending encounter.
- **`AbandonRealtimeBattle` (patch 6, F8 2026-07-30, `:1303-1336`)** — teardown/exit/death-EVAC while a fight is
  live: `arena.ResolveAbandoned` (no outcome — else it resolves a PHANTOM WIN over a destroyed stage), restore
  CC + framing, `ClearPendingEncounter` + `ResolveEncounter` so re-entry can never resume a dead fight. Gated
  strictly on `_arenaOwnsHero`; called first in `OnDestroy` (`:218-226`) and `ExitToVillage` (`:450-455`).
- **Sole-mover felt-fix (2026-07-26, `:597-666`)** — `EnsureSingleDungeonMover`, polled from `Update` (skipped
  while `_arenaOwnsHero`): the village HeroBodySwapper injects a HeroLocomotion/NavMeshAgent onto the Keeper;
  with no navmesh baked its off-mesh fall-snap slid the hero and stomped DungeonHero. Neutralize WITHOUT
  disabling the component (type-resolvers + §7 enemy targeting exclude disabled): agent off,
  `HeroLocomotion.GroundSnapEnabled=false`, `SetScriptedMove(Vector2.zero)`. **Both statics restored on teardown**
  (`RestoreInjectedHeroMover`, `:656-666`) — before every OnDestroy early-return so the ATB round-trip never
  freezes the battle-scene hero (`:228-231`).
- `OnDestroy` (`:218-253`): abandon-arena → restore mover → unsubscribe → **BUG-008 guard**: pending encounter =
  ATB teardown, leave the run intact; else bank the per-run inventory to the larder
  (`DungeonLootGrant.DepositDungeonInventory`, WO-749) + `EndRun`.
- `ExitToVillage` (`:450-477`): abandon + restore + EndRun + deposit&clear inventory + stop BGM →
  `SceneRouter.LoadSceneWithFade(SceneRouter.Castle)` (owner 2026-07-13: home = merged overworld hub; the
  abandoned legacy Village scene predates this).
- Misc: `HydrateChests` (WO-749, `:902-951`) attaches `DungeonChestInteract` to `Chest_{id}` visuals or runtime
  markers; `SweepPlaceholderCubes` (`:1629-1666`) hides near-white `[PLACEHOLDER]` primitive cubes only;
  `ConfigureCrafting` (`:823-878`) pairs scene pickups with placements and **runtime-authors surplus placements
  as tinted scatter motes** (WO-749 — more placements than scene pickups is normal); ambient BGM 0.25 volume,
  silent-with-warn when the clip is missing (`:1676-1700`). Header still says "the town is Avalon" (`:18`) —
  canon violation in a comment only.

#### DungeonHero.cs — the Keeper's dungeon walk. LIVE. **CharacterController IS the sole mover** (`:52-53`).
- Input priority per frame (`ResolveDesiredDirection`, `:250-290`): WASD/arrows (camera-relative) → shared
  village `VirtualJoystick.Move` (`:340-357`) → **kit HUD D-pad via loose reflection**
  (`DeNelle.HUD.Kit.HudMoveInput.Move`, cached PropertyInfo — Dungeons must not reference HUD; F8 2026-07-30
  "dungeon doesnt allow movement": only HeroLocomotion read the D-pad and the dungeon zeroes HeroLocomotion,
  `:269-330`) → tap-to-move, **gated OFF while `FeatureFlags.DungeonFpv`** (a tap is a LOOK in FPV, `:283-289`).
- Phantom-pointer guard (`IsRealPointer`, `:448-459`): rejects (0,0) synthesized taps that walked the Keeper into
  the bottom-left corner forever. Gravity ground-stick (`:488-500`); Tripo FBX −90° yaw offset in `FaceHeading`
  (`:509-513`); safe `Teleport` disables the CC across the move (`:182-196`).
- **R-A1 guard (`:200-219`)**: when the CC is disabled (arena owns the hero) the whole movement step is skipped
  and velocities/animator zeroed — no gravity accumulation slam on re-enable.
- Animator `Speed` float, `_hasSpeedParam` cached (WO-163).

#### DungeonCameraRig.cs — the dungeon follow rig. LIVE. **FIRST-PERSON is the shipped DEFAULT.**
- Three modes resolved at `Bind` off FeatureFlags, not serialized fields (`ResolveMode`, `:290-297`):
  **FPV (`ff.dungeonfpv`, default ON — `Core/FeatureFlags.cs:650`)** wins > legacy top-down iso
  (`ff.dungeoniso`, default OFF, `:657`) > over-the-shoulder (the flag-less fallback). The "OverShoulder
  (DEFAULT)" wording in the mode list is historical — header `:11-17` says so itself.
- OTS/FPV both use `CinemachineThirdPersonFollow` behind a **heading-corrected pivot** (child of the hero,
  constant local yaw `_headingYawOffset=90` undoing the Tripo −90 model offset — following the raw transform
  would seat the camera at the Keeper's SIDE, `:33-41`, `EnsurePivot :384-396`). Bind hard-overwrites the OTS
  offsets to the 2026-07-26 felt-tuned values at runtime so already-baked scenes get them (`:258-265`).
- **FPV**: ~0 camera distance at eyeline (`:136-140`), independent yaw+pitch look layer in `LateUpdate`
  (right-half-of-screen touch-drag / mouse delta, pitch clamp ±70, decoupled from FaceHeading, `:435-487`);
  hero renderers set **ShadowsOnly** with prior modes stashed + restored on mode change/`OnDestroy`
  (`:528-570`); AvoidObstacles + head-bob OFF (motion sickness). Left screen half reserved for the joystick.
- **`SetCombatFraming(bool)` (`:498-519`)**: forces OTS for an arena fight, restores the resolved mode after —
  called by DungeonController on `OnBattleStaged/Ended`. Iso path keeps the height-capped legacy chase
  (`ApplyIso`, `:575-600`). `Start()` auto-binds to any `DungeonHero` if no controller called `Bind` (Granary
  path, `:237-241`).

#### EncounterTrigger.cs — scripted + boss (+dormant random) encounter zones. LIVE.
- `ConfigureScripted/ConfigureBoss/ConfigureRandom` (`:106-180`); boss def adapted into the scripted shape so one
  Tick path drives both (`:147-158`); `_hasFired` restores from `HasFiredScriptedEncounter` on resume.
- Fire path: `RegisterScriptedEncounter` + `BeginEncounterHandoff(id, isBoss, heroPos)` (`:226-232`) →
  `LaunchBattle`: **`FeatureFlags.DungeonRealtimeBattle` (`ff.dungeonrealtime`, default ON —
  `Core/FeatureFlags.cs:273`; WO-591 "retire ATB": the dungeon is a SKIN of BattleArena)** →
  `BattleArena.Instance.BeginEncounter(EncounterParams)` — stages additively at a far offset, warps the hero
  in/out IN-scene, no scene round-trip (`:326-356`). Refusal or throw → **`RollbackHandoff()`** (`:355`, `:364`,
  `:408+`): clears handoff + combat lock + re-arms `_hasFired` so the run is never wedged combat-locked with no
  battle. Flag OFF → legacy `SceneRouter.GoBattle(BattleParams)` verbatim, same rollback guard (`:369-400`).
- Random path intact but **dormant in v1** (`DungeonLayout.disableRandomEncounters` set in healers-cottage.json);
  `RandomEncounterTable.cs` (pure C#) unchanged, unreachable.

#### Other root files
| File | Verdict |
|---|---|
| `DungeonLayout.cs` | Pure data model + `DungeonLayoutLoader.LoadAsync` (StreamingAssets, Android-async). Unchanged shape: rooms/walls(doorway/illusory)/spawn/loreStones/checkpoints/scriptedEncounters/miniBoss/chests/encounterPool/bryn/lanternPosts/oilStones. |
| `Checkpoint.cs` | Heal/save shrine; **`ToastRequested` UnityEvent added (WO-770.7)** (`:83`) — was firing into the void; DungeonController now subscribes `DungeonToastView.Show` (`DungeonController.cs:1046-1049`). Heal still wired-but-redundant in v1. |
| `LoreStone.cs` | Readable stone; **`ReadRequested : UnityEvent<LoreReadRequest>` (`:67`)** raised by tap via MobileInteractButton; DungeonController subscribes `LoreReadingModal.Show` (WO-770.4 — closed the triple gap: input + subscriber + view). `LoreReadRequest {LoreStoneId,Title,Body[]}` (`:38-45`). |
| `LoreFragments.cs` | Fragment set + loader (lore-fragments.json). Unchanged. |
| `Lantern.cs` | Oil lantern (radius/oil/refill/tincture/cloak). Unchanged API; read by the HUD via `LanternReadoutAdapter` → `DungeonHudVM`. |
| `DungeonPortLink.cs` | **NEW (WO-711)** — one side of a door/stair port: proximity prompt (shared `MobileInteractButton` + desktop F) → 0.15 s ScreenFader fade → `DungeonHero.Teleport` to the mate point → face onward (`:2-22`, `:41-55`). Runtime-authored only by `DressTraversalLinks`. |
| `DungeonExitInteractable.cs` | **NEW (ship-blocker DG-01)** — walk-in + Interact RETURN exit; `static Spawn(pos, onExit, label)`; `SetHero()` pushes the rig so the prompt never depends on a HeroLocomotion type-lookup. Contains **`DungeonExitSpawner`** (internal static, `[RuntimeInitializeOnLoadMethod]` + sceneLoaded, `:40-96`): injects an exit into every `DungeonCompose_*` scene at load — covers the already-baked binary dg_starter_loop with no re-bake; idempotent; exit routes `SceneRouter.Castle`. |
| `TorchWardenDress.cs` | **NEW (WO-711)** — `TorchWardenDresser.Dress`: hides Bryn's capsule pill, spawns a People-pack body (`NPCs/NPC_Peasant_Mevina`, Tob fallback), attaches a TalkPromptRegistry Talk (dialogues.json `dun_torch_warden` — teaches the torch/dark mechanic), first completion grants 1 torch into `AtbInventory.Torches` behind a SeenTutorials key (`:2-26`). Gates nothing; every step Guard.Try'd. |
| `DungeonStubEncounter.cs` / `DungeonStubReturn.cs` | Folk's Granary stub-scene pads (controller-free encounter + exit). Still compiled/LIVE in that scene, but the **Granary portal row is REMOVED (WO-776)** — the stub scene is out of normal play, dev-only. |

### 1.3 State/ — DungeonRuntimeState.cs (ScriptableObject; instance `State/HealersCottageRuntimeState.asset`)
Run lifecycle + encounter handoff + hero vitals across the ATB round-trip. Full public surface verified
(`:138-497`): queries `DungeonId/CurrentRoomId/HeroPosition/DungeonRunSeed/InCombat/BossDefeated/RunActive/
PendingEncounterId/HasPendingEncounter/PendingEncounterIsBoss/EncounterResumePosition/HeroHp|MaxHp|Mana|MaxMana/
HasHeroVitals/HasReadLore/HasReachedCheckpoint/HasFiredScriptedEncounter/HasOpenedChest`; mutators
`StartRun/EndRun/SetHeroPosition/SetCurrentRoom/MarkSecretRoomFound/TickEncounterClock/Register*Encounter/
BeginEncounterHandoff/ResumeAfterEncounter(bool victory)/ClearPendingEncounter/SetHeroVitals/HealHeroToFull/
ResolveEncounter/MarkBossDefeated/ReachCheckpoint/ReadLoreStone/OpenChest`. `StartRun` deliberately preserves the
pending handoff + vitals across a reload; `ResumeAfterEncounter(true)` credits the boss internally.

### 1.4 Crafting/ (7 files)
| File | Verdict |
|---|---|
| `CraftingData.cs` | Data model + `CraftingDataLoader` (crafting-recipes.json) + **`CraftingDataLoader.Cached`** static the shop provider reads. |
| `CraftingPedestal.cs` | Interact craft station → `CraftingPanelRequest`; **`ToastRequested` now subscribed** (WO-770.7 D13 — crafts confirm via DungeonToastView, `DungeonController.cs:870-871`). |
| `DungeonInventory.cs` | Per-run ingredient inventory SO (PlayerPrefs-backed); cleared on fresh run; **banked to the persistent larder on every exit path** (WO-749). Instance `State/HealersCottageInventory.asset`. |
| `IngredientPickup.cs` | Collectible mote; + **`CreateRuntime(...)`** — runtime-authored tinted scatter motes for data placements beyond the scene bake (WO-749). |
| `DungeonLootGrant.cs` | **NEW (WO-749)** — THE dungeon→larder bridge: `GrantChest(rewardKey)` (rewardKey→loot-table map, fallback `chest-rare`), `GrantEncounter(bool boss)`, `DepositDungeonInventory` (per-run scatter → `VillageInventory`), `CanonicalLarderId` alias seam (currently identity). Reuses `LootTableCatalog.Roll` + `VillageInventory.Add` (`:2-67`). Before it, chests/scatter/ATB-victory granted NOTHING. |
| `DungeonChestInteract.cs` | **NEW (WO-749)** — proximity auto-open chest (radius 2.4), once per run via `DungeonRuntimeState.OpenChest`, resolves rewardKey → `DungeonLootGrant`. Attached at runtime by `HydrateChests`. |
| `CraftableShopProvider.cs` | **NEW** — crafting-as-shoppable: implements Core `ICraftableCatalog` over `CraftingDataLoader.Cached`, installed into `CraftableCatalogRegistry` by a RuntimeInitializeOnLoad hook so `Village.Hero.ShopCatalog` lists craftables with no Dungeons dependency (`:2-21`). Recipe with 0 ingredient lines = not craftable. |

### 1.5 UI/ (6 code files + 2 UXML/USS pairs)
| File | Verdict |
|---|---|
| `CraftingPanelController.cs` | **STILL UXML** (`[RequireComponent(UIDocument)]`, binds CraftingPanel.uxml element names, `:25-37`) — now a "dumb skin" over `DungeonCraftVM` (MVVM Silo F). ⚠ UXML-in-builds risk remains OPEN here (§8 landmine) — unlike Settings, this pair was never converted. |
| `DungeonHudController.cs` | **STILL UXML** (DungeonHud.uxml, `:31-40`); oil meter View over `DungeonHudVM`; `SetLantern` push seam preserved. Same open UXML risk. |
| `DungeonCraftVM.cs` | **NEW (MVVM Silo F)** — pure VM projecting a `CraftingPanelRequest` into the promoted Core `CraftRecipeVM` (same struct as the village WorkshopCraftVM); re-projects on inventory change; forwards Craft/Close (`:2-15`). |
| `DungeonHudVM.cs` | **NEW (MVVM Silo G)** — pure VM over the narrow `ILanternReadout` seam (`OilFraction/IsLowOil/EstimatedSecondsRemaining`); owns the low/critical band + label logic the View used to inline (`:2-40`). |
| `LoreReadingModal.cs` | **NEW (WO-770.4)** — CODE-BUILT Obsidian-kit uGUI reading modal (`ElarionUiKit.BuildModalCanvas`, sort 31000, PanelManager-registered); `static Show(LoreReadRequest)` is the LoreStone subscriber (`:2-12`). |
| `DungeonToastView.cs` | **NEW (WO-770.7)** — CODE-BUILT one-at-a-time obsidian toast (`ElarionUiKit.ToastCard`); `Show(string)` shaped for `UnityEvent<string>.AddListener` — the checkpoint/craft/Bryn feedback sink (`:2-14`). |

### 1.6 RoomForge/ (runtime types only — editor stack in the editor-tools catalog)
| File | Verdict |
|---|---|
| `DungeonComposeLayout.cs` | JSON source-of-truth for RoomForge→DungeonBaker: room-prefab placements on a 6 u cell grid + named socket connections + `ComposeRules` (spineOnly, minShrines, maxMateDistance 1.25, sealUnmated, pacing 0.6/0.2/0.2) (`:15-64`). Path `StreamingAssets/Data/Canonical/dungeon-layouts/<id>.json`. Parallel to (NOT replacing) the legacy wall-run `DungeonLayout`. |
| `DungeonBakerChecks.cs` | **The PURE shared mate/seal/verify/overlap logic + compose loop** — deliberately in the runtime assembly so the editor baker AND the headless `RoomForgeRegression` oracle call the exact same code with no assembly cycle (`:2-17`). WO-745 contract: any mate failure ABORTS the bake; post-mate drift re-verify + AABB overlap check; emits the `[Flow:DungeonBake]` band. Types: `MateFailReason` enum (MissingInstance/MissingSocket/TypeMismatch/Distance/Alignment/Drift/Overlap), `MateResult`, `ConnectionOutcome`, `ComposeOutcome` (`:26-70`). |
| `RoomPrefabMeta.cs` | Author metadata on a forged room prefab root: roomId, archetype (combat/lore/reward/hub/secret/boss), themePalette, footprintCells @ cellSize 6 (`:12-34`). |
| `RoomSocket.cs` | Authorable doorway/arch/stair socket: id, `RoomSocketType`, facing hint, isSecret, bake-written `matedTo`, halfWidth; `Outward = transform.forward`; editor gizmos (`:12-60`). |
| `RoomSocketType.cs` | enum Door/Arch/StairUp/StairDown. |

### 1.7 Wanderer/
`Bryn.cs` — tiered-dialogue wandering NPC; **`SetToastSink(Action<string>)` added (WO-770.7 D14)** so her
greeting also surfaces via DungeonToastView while the world-space bubble path stays HUD-free (`:184`);
`WandererBubble.cs` / `WandererDialogue.cs` unchanged.

### 1.8 Dungeon data JSON (referenced, not in-module)
`Data/Canonical/dungeons/healers-cottage.json` (12 rooms, `disableRandomEncounters` set) ·
`Data/Canonical/dungeon-layouts/*.json` (compose format, DGN-1) · `crafting-recipes.json` (now with the WO-749
ingredient floor-scatter placements beyond the scene bake) · `lore-fragments.json` ·
`loot-tables.json` `dungeon-*` tables (WO-749). No `folks-granary.json` — the Granary remains layout-less.

---

## 2. Environment / Data / UI — Assembly-CSharp (no asmdef), re-verified 2026-08-02

- `Environment/NightTorchLightSystem.cs` — unchanged: self-bootstrap DDOL, **hard-gated to scene "Village2"**
  (`TargetScene`, `:50`, `:105-125`); warm night light ramp, mobile-cheap.
- `Environment/TorchFireController.cs` — unchanged: scene-placed per-torch flame VFX + combat-intensify
  (OverlapSphere/Update, fine ≤8 torches).
- `Data/MasterAssetCatalog.cs` — unchanged SO catalog; the 2026-06-12 "no consumer found" flag was NOT re-audited
  this pass — still treat as possibly dormant.
- `UI/` — **`GameOverUI.cs` is GONE** (folder now holds only README.md). The old "duplicate death-screen path"
  flag is RESOLVED by deletion; the live death path is the Village-side screens.

---

## 3. Brief module maps (headers + key seams verified; not exhaustive)

### Audio — asmdef `DeNelle.Audio` (11 files) — **the mixer stub truth**
- Files: AudioBootstrap, AudioService, MusicDirector, MusicTrack, MusicSelectionPanel(+Bootstrap), JukeboxVM,
  ProceduralSfx, SfxClipLibrary, SfxId, WebGLAudioUnlock.
- **Mixer truth (verified 2026-08-02):** `Assets\Audio\Resources\Audio\GameAudioMixer.mixer` EXISTS (resolves at
  Resources "Audio/GameAudioMixer") but has **`m_ExposedParameters: []`** — the MasterVol/MusicVol/SfxVol/UiVol/
  VoiceVol params AudioService's header claims (`AudioService.cs:20-27`) are NOT exposed. Every
  `_mixer.SetFloat` silently fails. **What actually works:** `SetVolume` ALSO drives the music sources directly
  (`_director?.ApplyVolumeScale`, `:820-826`) and `SetMuted` ALWAYS snaps every source's mute flag
  (`:854-863`) — so Master/Music sliders + mute are audible via per-source scaling; **the SFX volume slider is
  effectively inert** (mixer-param-only path). Settings' AudioMixerBridge hits the same wall (see
  devtools-settings-onboarding.md P1).
- `AudioService` — DDOL singleton (`Instance`, `:63`), per-scene BGM crossfade via sceneLoaded, SfxId → authored
  clip (`SfxClipLibrary` Resources) with **synth fallback** (DEF-183/WO-243, `:627-660`), `PrewarmCombatSfx`,
  `ApplyPersistedSettings` seeds from GameState 0..100 (`:906-918`, called reflectively by SettingsBootstrap).
- `MusicSelectionPanel` — jukebox; the J hotkey is gated behind `FeatureFlags.DevHotkeys` (default OFF)
  (`:78-81`); touch entry elsewhere.

### Pets — asmdef `DeNelle.Pets` (15 files)
Starter-warden pillar: `PetDeployer` (spawns the 3 starter pets on a radius-11 ring around the Heart; takes the
Heart position as a plain Vector3 — **no Village reference, integrator-wired**, `:2-14`), `Pet` + combat/AI,
`PetCatalog` (pets.json), ~~`PetProgression`~~ (DELETED 2026-08-16, WO-993 - pet levelling descoped; `Pet.SetProgressionMultipliers` went with it and `HeroProgression` is now the only `IXpEarner`), `PetAcquisitionService`, harvest/mining bridges (`PetHarvester`,
`MineNodeBridge`), presentation (`PetAnimatorController/PetClipPlayer/PetEmoteController/PetIdleRoutines/
PetBillboard/PetAttackVfxBridge/PetHeroLeash`). LIVE (village loop).

### Cosmetics — asmdef `DeNelle.Cosmetics` (4 files)
`GlimmerCurrencyService` (soft currency) · `BattlePassManager` (WO-73 reconciled: PlayerPrefs persistence,
premium pass 2 400 Glimmer, LevelUpVFX via reflection — Cosmetics→Village not allowed; **no-op until a
`BattlePassData` SO asset is assigned in the Inspector** (`:21-22`) — verify an asset exists before calling this
LIVE) · `CosmeticCatalog` · `CosmeticApplier`.

### Wallet — asmdef `DeNelle.Wallet` (11 files + Tests/)
`WalletService` over `IWalletProvider`; **`SolanaWalletProvider` compiles with or without the SDK — all real SDK
code is behind `#if SOLANA_SDK`, a define the project must set once the Solana Unity SDK package resolves;
without it every method fails cleanly and WalletService falls back to `StubWalletProvider`** (`:10-21`).
DEVNET-only by spec; no private keys (player wallet signs, `:23-32`). Store side: `PackCatalog`, `PackStore`
(+`PackStoreVM`, `PackStoreBootstrap`, `WalletSkinBootstrap`) — the ~70%-built store (§8: do NOT greenfield).
`WalletConnectDialog`, `WalletRegistry`, `WalletEndpoints`, `CryptoPaymentManager`. Real Seeker/Mobile-Wallet-
Adapter connect remains the WO-766 gap.

### Web3 — asmdef `DeNelle.Web3` (7 files)
`JupiterSwapService` (WO-43): REAL = Jupiter /quote fetch + fee math + panel + wallet lookup; **STUB = swap
signing/submission — `WalletBridgeStub` logs a fake signature in-editor and HARD-FAILS in a release build**
(`:9-19`); registers as `CoreServices.Jupiter` (no asmdef ref needed by callers). Devnet-vs-mainnet reconcile
flag still open (`:21-27`). Plus `JupiterSwapPanelController`, `SwapVM`, `SwapFeeConfig`, `JupiterSwapBootstrap`.

---

## FLAGS (risk ledger)

### Architecture drift (P1)
- **Dungeons now references DeNelle.Village** (asmdef `:6`) — the old clean "Dungeons→Core only, ATB via
  SceneRouter" law is dead; the real-time BattleArena bridge, mover-neutralize, loot larder and TalkPromptRegistry
  all ride it. Any future Village→Dungeons reference would now CYCLE — `CraftableShopProvider` exists precisely
  to avoid that; keep it that way.
- **DGN-1: `dg_starter_loop.unity` is BINARY-serialized** (verified header bytes) at
  `Assets/Scenes/DungeonCompose/` — do not hand-open/re-save from a shared tree (NUL-corruption memory);
  re-bake only in an isolated worktree. Runtime code never edits it (`DungeonExitSpawner` injects at load).

### Open build risks
- **Dungeon UXML pair still unconverted:** `CraftingPanelController` + `DungeonHudController` bind
  CraftingPanel.uxml / DungeonHud.uxml — the §8 "UXML renders empty in player builds" class is CLOSED in
  Settings but **OPEN here**. The code-built LoreReadingModal/DungeonToastView show the target idiom.
- SFX volume slider inert (mixer has zero exposed params; no per-source SFX fallback) — see Audio §3.

### Dormant / gated
- Random encounters: `RandomEncounterTable` + EncounterTrigger random path — dormant v1
  (`disableRandomEncounters`); clock still ticks so v1.1 is a data flip.
- Legacy ATB dungeon combat: intact behind `ff.dungeonrealtime=0` (reversible retire, WO-591); default is
  real-time BattleArena in-scene.
- Camera A/Bs: OTS (`ff.dungeonfpv=0`) and legacy iso (`ff.dungeoniso=1`, loses to FPV) — one PlayerPref away.
- Folk's Granary: portal row removed (WO-776) — stub scene + `DungeonStubEncounter/Return` + builder stay in-repo,
  dev-only, no layout JSON.
- `Checkpoint` heal still redundant in v1 (post-fight full restore); functions as save/respawn marker.
- `BattlePassManager` no-ops without an authored `BattlePassData` asset; Web3 swap signing is a stub that
  hard-fails in release; `SolanaWalletProvider` is inert until `SOLANA_SDK` is defined (stub provider serves).

### Comment-vs-code
- `DungeonController.cs:18` still calls the town "Avalon" (canon: Elarion) — comment only.
- `DungeonCameraRig` mode list still labels OverShoulder "(DEFAULT)" — the live default is FPV (the header's
  2026-07-30 note corrects itself; trust `ResolveMode` + `FeatureFlags.DungeonFpv`).
- `AudioService.cs:20-27` claims five exposed mixer params — the asset exposes none (comment lies; verified).
- Old catalog claims now WRONG: "dungeons route to ATB via SceneRouter only" (real-time arena is default),
  "RoomForge omitted" (fixed above), "GameOverUI possible duplicate" (file deleted).
