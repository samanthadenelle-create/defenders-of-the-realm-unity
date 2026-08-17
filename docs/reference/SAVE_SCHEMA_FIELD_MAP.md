# SAVE SCHEMA FIELD MAP — the known dictionary of persisted state

**Status:** LIVE REGISTRY. Named as a KNOWN DICTIONARY by `SUNDAY_HOUSEKEEPING.md` §2; created 2026-08-16.
**Scope:** every field that survives an app close — the versioned save envelope, the PlayerPrefs
side-band, and what crosses the wire to Neon.
**Rule of this document:** every row carries a `file:line`. Nothing here is copied from another doc.

---

## 0. WHY THIS FILE EXISTS (read once, then never restate a version number anywhere)

The schema version is **38**, at `Assets/_Modules/Core/State/SaveSchema.cs:41`:

```csharp
public const int CurrentVersion = 38;  // v38 — WO-934 army loadout bank...
```

On the night this registry was written, six load-bearing docs claimed the schema was v20, v30, v34,
v36 and v37 between them, and one file managed three different answers inside its own body. Even
`SaveSchema.cs`'s **own header comment** said `CurrentVersion = 36` while the const 25 lines below
read 38 — that lie is now corrected in place and annotated at `SaveSchema.cs:11-14`.

**A copied version number rots. A cited field map does not.** Never write the number into a second
file; cite `SaveSchema.cs:41` instead. Same discipline as CLAUDE.md §2 (WO numbers) and §0 (repo root).

**The version triple that must stay aligned** (asserted by `Assets/Editor/Regression/CoreSaveRegression.cs`):
1. `SaveSchema.CurrentVersion` — `SaveSchema.cs:41`
2. the migrator's **top registered step** — `SaveMigrator.cs:79` (`{ 38, MigrateToV38 }`)
3. `GameState.SchemaVersion` initializer — `GameState.cs:34` (`= SaveSchema.CurrentVersion`, derived, never a literal)

---

## 1. THE STORAGE LAYERS (there are four, and only one is versioned)

| Layer | Where | Versioned? | Migrated? | Validated? | Signed? |
|---|---|---|---|---|---|
| **The save envelope** | PlayerPrefs key `dotr-save` (`SaveSchema.cs:47`), via `LocalSaveProvider.cs:36` | YES (`SaveSchema.cs:41`) | YES (`SaveMigrator.cs:87`) | YES (`SaveSchema.cs:691`) | YES — HMAC-SHA256 embedded at the front (`SaveSchema.cs:153`) |
| **PlayerPrefs side-band** | ~150 loose keys — see §6 | NO | NO | NO | NO |
| **Neon `player_data.game_state`** | JSONB, `api/schema.sql:59-65` | column exists but is **always written as 10** — see §8 | NO | server bounds-guards only (`api/game/save.js:327`) | signature is on the transport, not the row |
| **Runtime-only SOs** | `prepTimerLocked`, `paused`, `activeRegionRun` etc. | n/a — deliberately not persisted (`GameStateService.cs:932-934`) | — | — | — |

Envelope shape (`SaveSchema.SaveFile`, `SaveSchema.cs:208-220`):

| JSON key | C# | type | meaning | citation |
|---|---|---|---|---|
| `format` | `Format` | int | file format, **not** schema version. Always 1 | `SaveSchema.cs:211`, const at `:43` |
| `storeVersion` | `StoreVersion` | int | = `CurrentVersion` at write; the migrate-from input on load | `SaveSchema.cs:213`; read at `GameStateService.cs:335` |
| `exportedAt` | `ExportedAt` | string (ISO-8601) | write timestamp | `SaveSchema.cs:215`; set `GameStateService.cs:386` |
| `wallet` | `Wallet` | string | wallet the save is tagged to | `SaveSchema.cs:217`; set `GameStateService.cs:387` |
| `state` | `State` | `PersistedState` | the payload — §3 | `SaveSchema.cs:219` |

Stored value layout is `<64-hex-HMAC>\n<json>` in **one atomic write** (`SaveSchema.cs:146,153`);
a legacy unsigned save is detected by the absent prefix, loaded once, and re-signed
(`GameStateService.cs:303-308,358-362`). Threat-model honesty: the key is client-embedded
obfuscation, not authority (`SaveSchema.cs:90-95`).

---

## 2. THE VERSION LADDER — every bump, what it added, its migrator step

`Steps` registry: `SaveMigrator.cs:37-80`. Applied ascending, cumulatively, for every
`fromVersion < step.Key` (`SaveMigrator.cs:90-96`). A version with **no** `Steps` entry is
*additive-default-on-read*: the wire field is nullable and the absent case falls through to
`GameState`'s own initializer in `ApplyPersisted`.

| v | What it added | Migrator step | Citation |
|---|---|---|---|
| 1 | baseline | — | — |
| 2 | `resources` = STARTER {250,80,15}, `ownedItemIds` = [] | `MigrateToV2` | `SaveMigrator.cs:127-132`; starter at `NestedTypes.cs:55` |
| 3 | `heroClass` defaulted to Mage | `MigrateToV3` | `SaveMigrator.cs:138-142` |
| 4 | `wood`=15, `buildingCooldowns`={}, `tutorialStep`=Done | `MigrateToV4` | `SaveMigrator.cs:148-154` |
| 5 | `towerAbilities` = [0]×TowerSlots | `MigrateToV5` | `SaveMigrator.cs:160-168` |
| 6 | ATB+dungeon block: `inventory`,`atbLossStreak`,`breachStyle`,`buildingDamage`,`dungeons`,`quests` | `MigrateToV6` | `SaveMigrator.cs:176-186` |
| 7 | starter dungeon merged into `dungeons.discovered` | `MigrateToV7` | `SaveMigrator.cs:194-201`; id at `SaveSchema.cs:53` |
| 8 | gate id rename `gate-0` → `gate-2` in `buildingDamage` | `MigrateToV8` (try/catch, now LOUD) | `SaveMigrator.cs:209-230` |
| 9 | `pendingBuilds`=[]; folds the legacy `realm-defenders-settings` prefs blob in, then deletes it | `MigrateToV9` | `SaveMigrator.cs:241-285`; legacy key `SaveSchema.cs:49` |
| 10 | `regions` = empty RegionProgress | `MigrateToV10` | `SaveMigrator.cs:291-295` |
| 11 | `aetherCrystals` | none (default-on-read) | `SaveSchema.cs:289`; noted `SaveMigrator.cs:49-50` |
| 12 | `lastHarvestClaimMs` (WO-115) | none | `SaveSchema.cs:298` |
| 13 | `buildJobs`, `adSkipsUsedToday`, `adSkipDayKey` (WO-172) | none | `SaveSchema.cs:306,309,312` |
| 14 | `baseLayout` (WO-108) | `MigrateToV14` | `SaveMigrator.cs:303-307` |
| 15 | `magic` (DEF-121/WO-230) | none | `SaveSchema.cs:330`; noted `SaveMigrator.cs:52-54` |
| 16 | `partyMemberIds` (WO-301) | none | `SaveSchema.cs:340`; noted `SaveMigrator.cs:55-56` |
| 17 | `zones` (WO-164) | `MigrateToV17` (seeds `DefaultZoneGraph`) | `SaveMigrator.cs:316-322` |
| 18 | crystal unification — `aetherCrystals` folded into `resources.crystals`, then zeroed | `MigrateToV18` | `SaveMigrator.cs:331-341` |
| 19 | `arenaDefense` (WO-389) | none | `SaveSchema.cs:364`; noted `SaveMigrator.cs:59-61` |
| 20 | `gearInventory` | none | `SaveSchema.cs:267` |
| 21 | `settlements` (WO-159) | `MigrateToV21` | `SaveMigrator.cs:349-354` |
| 22 | `army` (WO-453) | `MigrateToV22` | `SaveMigrator.cs:362-367` |
| 23 | `buildingTiers` (WO-430) | `MigrateToV23` | `SaveMigrator.cs:371-376` |
| 24 | `villageTier` + `ownedBuildingPerks` (WO-432) | `MigrateToV24` | `SaveMigrator.cs:380-385` |
| 25 | `echoCount`=1, `siloResources`, `wavesCompleted` | `MigrateToV25` | `SaveMigrator.cs:391-397` |
| 26 | `equippedRingId`, `equippedAmuletId` (WO-543) | `MigrateToV26` — **seeds a field nothing reads, see §5** | `SaveMigrator.cs:402-407` |
| 27 | `PlacedStructureData.worldY` + `.wallMounted` | `MigrateToV27` — documented no-op | `SaveMigrator.cs:415-420` |
| 28 | `populationXp/Quests/Outposts`, `populationEchoSlots`=1 (WO-587) | `MigrateToV28` | `SaveMigrator.cs:426-433` |
| 29 | `heroLevel`=1, `heroXp`, `heroLifetimeXp` (F8-47) | `MigrateToV29` | `SaveMigrator.cs:440-446` |
| 30 | `strategicPlacementMigrated`=false (WO-673) | `MigrateToV30` | `SaveMigrator.cs:454-458` |
| 31 | `echoLanes`="wood" (WO-681/658) | `MigrateToV31` | `SaveMigrator.cs:464-468` |
| 32 | `freeBuildsUsed` — first-build freebies replace the founding resource seed | `MigrateToV32` | `SaveMigrator.cs:475-479`; budget zeroed `NestedTypes.cs:78-79` |
| 33 | `echoLanes` grammar bare-lane → `lane:level` (WO-738). SAME wire field | `MigrateToV33` — pass-through, exists only to keep the triple aligned | `SaveMigrator.cs:488-491` |
| 34 | `tribes`, `wards`, `arena`, `petActiveSlots` (REDS #3/#4) | `MigrateToV34` | `SaveMigrator.cs:502-509` |
| 35 | `obsidianQueue` (WO-773) — **folds** legacy `buildJobs`/`pendingBuilds`/future `buildingCooldowns` into the Builder channel, then clears them | `MigrateToV35` | `SaveMigrator.cs:526-598` |
| 36 | `everBuiltStructureIds` (WO-834) — seeded `BaseLayout ∪ FreeBuildsUsed ∪ frozen template-if-established` | `MigrateToV36` | `SaveMigrator.cs:627-653`; frozen template list `:621-625` |
| 37 | the PAID BASKET on `BuildJobData`: `paidWood/paidFood/paidIron/paidCrystals/paidMagic` (WO-911) | `MigrateToV37` — deliberate no-op on data; **traces** how many in-flight jobs will refund ZERO | `SaveMigrator.cs:685-708`; fields `BuildJobData.cs:160-172` |
| **38** | `ArmyStorage.loadouts` (3 named presets) + `activeLoadout` (WO-934) | `MigrateToV38` → `ArmyStorage.EnsureLoadouts()` | `SaveMigrator.cs:678-683`; ensure at `ArmyStorage.cs:110-125` |

**Import gate:** a save NEWER than this build is rejected outright, as is a non-finite version
(`SaveMigrator.cs:105-120`). Equal version skips the chain entirely (`:116-118`).

### ⚠ Ladder defects found while verifying

- **`MigrateToV38` is declared BEFORE `MigrateToV37`** in the file (`:678` vs `:685`), and the long
  XML `<summary>` block written *for v37* (`:655-673`) is therefore attached to **`MigrateToV38`**.
  Functionally harmless (the registry keys, not source order, drive execution) but the v37 rationale
  now documents the wrong method. Doc-drift of exactly the kind §0 exists to stop.
- **`SaveMigrator.cs`'s own file header says "v1 → v36"** at `:2`, `:11`, and `:30` — two versions
  stale, the same failure the `SaveSchema.cs` header had.
- **`GameState.cs:33`** comment reads `= SaveSchema.CurrentVersion (36)` — the code is right, the
  parenthetical is stale.
- **`GameState.cs:8,28` / `GameStateService.cs:372,441`** say "~60 fields ... through schema v34".
  Actual count is 82 `[JsonProperty]` members on `PersistedState`.

---

## 3. THE FIELD MAP — `SaveSchema.PersistedState`

Declared `SaveSchema.cs:233-643`. Every field is nullable-tolerant (mirrors Zod `.partial()`,
`SaveSchema.cs:226-231`); Newtonsoft drops unknown keys.

**Read the columns as:** *written by* = the line in `GameStateService.Snapshot()` that emits it;
*read by* = the line in `ApplyPersisted()` that restores it, plus the runtime consumer.
`Snapshot` is `GameStateService.cs:442-529`; `ApplyPersisted` is `:536-620`. Both are abbreviated
below as `Snap:<n>` / `Apply:<n>`.

| JSON key | C# member | Type | v | Default when absent | Migrator step | Written / Read | Citation |
|---|---|---|---|---|---|---|---|
| `pets` | `Pets` | `List<PetData>` | 1 | `[]` (SO init) | — | Snap:447 / Apply:539 | `SaveSchema.cs:235` |
| `starterPetId` | `StarterPetId` | string | 1 | null | — | Snap:448 / Apply:540 | `SaveSchema.cs:236` |
| `petName` | `PetName` | string | — (additive, no bump) | null | — | Snap:449 / Apply:541; consumed `ElaraWaveThreeJoin.cs:409` | `SaveSchema.cs:239` |
| `onboarded` | `Onboarded` | bool? | 1 | false | — | Snap:451 / Apply:542 | `SaveSchema.cs:240` |
| `bestWave` | `BestWave` | double? | 1 | 0 | — | Snap:452 / Apply:543; server-wins merge `GameStateService.cs:1463` | `SaveSchema.cs:241` |
| `resources` | `Resources` | `ResourceBalance?` | 2 | `Starter{250,80,15}` | v2 | Snap:453 / Apply:544 | `SaveSchema.cs:242`; struct `NestedTypes.cs:41-59` |
| `ownedItemIds` | `OwnedItemIds` | `List<string>` | 2 | `[]` | v2 | Snap:454 / Apply:545; read `PackStoreVM.cs:56` | `SaveSchema.cs:243` |
| `petBonds` | `PetBonds` | `List<double>` | 1 | `[0,0,0]` | — | Snap:455 / Apply:546; read `BattleController.cs:383` | `SaveSchema.cs:244` |
| `voidshards` | `Voidshards` | double? | 1 | 5 | — | Snap:456 / Apply:547 — **no gameplay reader, see §5** | `SaveSchema.cs:245` |
| `towers` | `Towers` | `List<double>` | 1 | `[0]×9` | — | Snap:458 / Apply:549 | `SaveSchema.cs:246` |
| `towerAbilities` | `TowerAbilities` | `List<double>` | 5 | `[0]×9` | v5 | Snap:459 / Apply:550 — **no gameplay reader, see §5** | `SaveSchema.cs:247` |
| `wallLevel` | `WallLevel` | double? | 1 | 0 | — | Snap:460 / Apply:551; read `WallTierData.cs:267` | `SaveSchema.cs:248` |
| `stone` | `Stone` | double? | 1 | 20 | — | Snap:461 / Apply:552 — **dev-tool only, see §5** | `SaveSchema.cs:249` |
| `iron` | `Iron` | double? | 1 | 5 (SO) / 0 on New Game | — | Snap:462 / Apply:553 | `SaveSchema.cs:250`; reset `GameStateService.cs:981` |
| `wood` | `Wood` | double? | 1 | 15 (SO) / 0 on New Game | v4 seeds 15 | Snap:463 / Apply:554 | `SaveSchema.cs:251`; reset `:982` |
| `buildingCooldowns` | `BuildingCooldowns` | `Dictionary<string,double>` | 4 | `{}` | v4; **drained by v35** | Snap:464 / Apply:555 — **legacy, see §5** | `SaveSchema.cs:252` |
| `pendingBuilds` | `PendingBuilds` | `List<PendingTowerBuild>` | 9 | `[]` | v9; **drained by v35** | Snap:465 / Apply:556 — **legacy, see §5** | `SaveSchema.cs:253` |
| `tutorialStep` | `TutorialStep` | `TutorialStep?` | 1 | `Step1` (fresh) / `Done` (migrated) | v4 | Snap:466 / Apply:557 | `SaveSchema.cs:254`; converter `TutorialStepConverter.cs` |
| `joystickSensitivity` | `JoystickSensitivity` | double? | 1 | 1.0 | — | Snap:467 / Apply:558 | `SaveSchema.cs:255` |
| `movementStyle` | `MovementStyle` | `MovementStyle?` | 1 | `Auto` | — | Snap:468 / Apply:559 | `SaveSchema.cs:256` |
| `muted` | `Muted` | bool? | 1 | false | v9 | Snap:469 / Apply:560 | `SaveSchema.cs:257` |
| `musicVolume` | `MusicVolume` | double? | 1 | 70 | v9 | Snap:470 / Apply:561 | `SaveSchema.cs:258` |
| `sfxVolume` | `SfxVolume` | double? | 1 | 80 | v9 | Snap:471 / Apply:562 | `SaveSchema.cs:259` |
| `difficulty` | `Difficulty` | `Difficulty?` | 1 | `Normal` | v9 | Snap:472 / Apply:563 | `SaveSchema.cs:260` |
| `voiceOvers` | `VoiceOvers` | bool? | 1 | false | v9 | Snap:473 / Apply:564 — stored, not yet wired (`GameState.cs:121`) | `SaveSchema.cs:261` |
| `ownedPets` | `OwnedPets` | `List<PetSpecies>` | 1 | `[]` | — | Snap:474 / Apply:565 | `SaveSchema.cs:262` |
| `seenTutorials` | `SeenTutorials` | `Dictionary<string,bool>` | 1 | `{}` | — | Snap:475 / Apply:566. **Also backs `ProgressionUnlocks` (`unlock.*`) and `RecipeUnlocks` (`recipe_unlocked:*`)** | `SaveSchema.cs:263`; `ProgressionUnlocks.cs:32-45`, `RecipeUnlocks.cs:41,58-65` |
| `boundWallet` | `BoundWallet` | string | 1 | null → guest id minted | — | Snap:476 / Apply:567; minted `GameStateService.cs:1236` | `SaveSchema.cs:264` |
| `heroClass` | `HeroClass` | `HeroClass?` | 1 | `None` | v3 → Mage | Snap:477 / Apply:568 (**unconditional**, null clears) | `SaveSchema.cs:265` |
| `inventory` | `Inventory` | `AtbInventory?` | 6 | `Empty{0,0,0}` | v6 | Snap:478 / Apply:569 | `SaveSchema.cs:266`; struct `NestedTypes.cs:105-120` |
| `gearInventory` | `GearInventory` | `Dictionary<string,int>` | 20 | `{}` (+3 heal potions on New Game) | — | Snap:479 / Apply:570; founding grant `GameStateService.cs:999-1002` | `SaveSchema.cs:267` |
| `atbLossStreak` | `AtbLossStreak` | double? | 6 | 0 | v6 | Snap:480 / Apply:571 — **no reader, see §5** | `SaveSchema.cs:268` |
| `breachStyle` | `BreachStyle` | `BreachStyle?` | 6 | `Ask` | v6 | Snap:481 / Apply:572. **Survives New Game by design** | `SaveSchema.cs:269`; carve-out `GameStateService.cs:1067` |
| `buildingDamage` | `BuildingDamage` | `Dictionary<string,double>` | 6 | `{}` | v6, keys renamed by v8 | Snap:482 / Apply:573 | `SaveSchema.cs:270` |
| `dungeons` | `Dungeons` | `DungeonProgress` | 6 | starter dungeon discovered | v6 + v7 | Snap:483 / Apply:574 | `SaveSchema.cs:271`; type `NestedTypes.cs:185-226` |
| `activeDungeonRun` | `ActiveDungeonRun` | `ActiveDungeonRun` | 6 | null | v6 | Snap:484 / Apply:575 (**unconditional**) | `SaveSchema.cs:272`; type `NestedTypes.cs:160-171` |
| `quests` | `Quests` | `QuestProgress` | 6 | empty | v6 | Snap:485 / Apply:576; read `QuestService.cs:233,306` | `SaveSchema.cs:273`; type `NestedTypes.cs:251-276` |
| `regions` | `Regions` | `RegionProgress` | 10 | empty | v10 | Snap:486 / Apply:577 | `SaveSchema.cs:274`; type `NestedTypes.cs:280-296` |
| `myInviteCode` | `MyInviteCode` | string | 1 | null | — | Snap:487 / Apply:578; read `LeaderboardService.cs:163` | `SaveSchema.cs:275` |
| `contacts` | `Contacts` | `List<ChatContact>` | 1 | `[]` | — | Snap:488 / Apply:579 — **no reader, see §5** | `SaveSchema.cs:276` |
| `blockedCodes` | `BlockedCodes` | `List<string>` | 1 | `[]` | — | Snap:489 / Apply:580 — **no reader, see §5** | `SaveSchema.cs:277` |
| `inbox` | `Inbox` | `List<ChatMessage>` | 1 | `[]` | — | Snap:490 / Apply:581 — **no reader, see §5** | `SaveSchema.cs:278` |
| `lastInboxSyncAt` | `LastInboxSyncAt` | double? | 1 | 0 | — | Snap:491 / Apply:582 — **no reader, see §5** | `SaveSchema.cs:279` |
| `aetherCrystals` | `AetherCrystals` | double? | 11 | 0 | v18 folds+zeroes it | Snap:457 / Apply:548. **DEPRECATED — serializes as 0** | `SaveSchema.cs:289`; deprecation `GameState.cs:54-58` |
| `lastHarvestClaimMs` | `LastHarvestClaimMs` | double? | 12 | 0 | — | Snap:492 / Apply:583; the offline-accrual + silo clock | `SaveSchema.cs:298` |
| `buildJobs` | `BuildJobs` | `List<BuildJobData>` | 13 | `[]` | v35 folds + clears | Snap:493 / Apply:584 — **wire back-compat only, see §5** | `SaveSchema.cs:306`; note `GameState.cs:474-476` |
| `adSkipsUsedToday` | `AdSkipsUsedToday` | double? | 13 | 0 | — | Snap:494 / Apply:585; read `BuildTimerService.cs:1360,1372` | `SaveSchema.cs:309` |
| `adSkipDayKey` | `AdSkipDayKey` | string | 13 | null | — | Snap:495 / Apply:586; rolled `BuildTimerService.cs:1396,1406` | `SaveSchema.cs:312` |
| `baseLayout` | `BaseLayout` | `List<PlacedStructureData>` | 14 | `[]` | v14 | Snap:496 / Apply:587 | `SaveSchema.cs:321`; record `PlacedStructureData.cs:37-74` |
| `magic` | `Magic` | double? | 15 | 0 | — | Snap:497 / Apply:588 | `SaveSchema.cs:330` |
| `partyMemberIds` | `PartyMemberIds` | `List<string>` | 16 | `[]` | — | Snap:498 / Apply:589; joined at `GameStateService.cs:652` | `SaveSchema.cs:340` |
| `zones` | `Zones` | `List<ZoneState>` | 17 | `DefaultZoneGraph()` | v17 + `EnsureZoneGraph` at `:619` | Snap:499 / Apply:590 | `SaveSchema.cs:351`; record `RegionZone.cs:87-113` |
| `arenaDefense` | `ArenaDefense` | `List<PlacedDefenderData>` | 19 | `[]` | — | Snap:500 / Apply:591 | `SaveSchema.cs:364`; record `PlacedDefenderData.cs:40-52` |
| `settlements` | `Settlements` | `List<SettlementState>` | 21 | `[]` | v21; also null-defaulted `SaveSchema.cs:852` | Snap:501 / Apply:592 | `SaveSchema.cs:378`; record `WorldContent.cs:160-200` |
| `army` | `Army` | `ArmyStorage` | 22 | fresh cap-10 | v22 + v38 | Snap:502 / Apply:593 (**never null**) | `SaveSchema.cs:391`; type `ArmyStorage.cs:40` |
| `buildingTiers` | `BuildingTiers` | `Dictionary<string,int>` | 23 | `{}` | v23 | Snap:503 / Apply:594; compiled by `ModifierService` | `SaveSchema.cs:404` |
| `villageTier` | `VillageTier` | int (non-nullable!) | 24 | 0 | v24 | Snap:504 / Apply:595 (**unconditional**) | `SaveSchema.cs:408` |
| `ownedBuildingPerks` | `OwnedBuildingPerks` | `List<string>` | 24 | `[]` | v24 | Snap:505 / Apply:596 | `SaveSchema.cs:412` |
| `echoCount` | `EchoCount` | double? | 25 | 1 | v25 | Snap:506 / Apply:597 | `SaveSchema.cs:421` |
| `siloResources` | `SiloResources` | double? | 25 | 0 | v25 | Snap:507 / Apply:598 | `SaveSchema.cs:427` |
| `wavesCompleted` | `WavesCompleted` | double? | 25 | 0 | v25 | Snap:508 / Apply:599 | `SaveSchema.cs:433` |
| **`equippedRingId`** | `EquippedRingId` | string | 26 | `""` | v26 seeds it | **NEVER written, NEVER read — see §5.1** | `SaveSchema.cs:441`; `SaveMigrator.cs:404` |
| **`equippedAmuletId`** | `EquippedAmuletId` | string | 26 | `""` | v26 seeds it | **NEVER written, NEVER read — see §5.1** | `SaveSchema.cs:447`; `SaveMigrator.cs:405` |
| `populationXp` | `PopulationXP` | double? | 28 | 0 | v28 | Snap:509 / Apply:600 | `SaveSchema.cs:454` |
| `populationQuests` | `PopulationQuests` | double? | 28 | 0 | v28 | Snap:510 / Apply:601 | `SaveSchema.cs:457` |
| `populationOutposts` | `PopulationOutposts` | double? | 28 | 0 | v28 | Snap:511 / Apply:602 | `SaveSchema.cs:460` |
| `populationEchoSlots` | `PopulationEchoSlots` | double? | 28 | 1 | v28 | Snap:512 / Apply:603 | `SaveSchema.cs:466` |
| `heroLevel` | `HeroLevel` | double? | 29 | 1 | v29 | Snap:513 / Apply:604; mirror of `HeroProgression._level` | `SaveSchema.cs:476` |
| `heroXp` | `HeroXp` | double? | 29 | 0 | v29 | Snap:514 / Apply:605 | `SaveSchema.cs:482` |
| `heroLifetimeXp` | `HeroLifetimeXp` | double? | 29 | 0 | v29 | Snap:515 / Apply:606 | `SaveSchema.cs:488` |
| `strategicPlacementMigrated` | `StrategicPlacementMigrated` | bool? | 30 | false (migrated) / **true** (New Game) | v30 | Snap:516 / Apply:607 | `SaveSchema.cs:501`; New-Game `GameStateService.cs:1046` |
| `echoLanes` | `EchoLanes` | string (CSV) | 31, grammar v33 | `"wood"` (SO) / `"harvest:1"` (New Game) | v31 seeds `"wood"`; v33 pass-through | Snap:517 / Apply:608; parsed `EchoAssignments.cs` | `SaveSchema.cs:527`; New-Game `GameStateService.cs:1047` |
| `freeBuildsUsed` | `FreeBuildsUsed` | `List<string>` | 32 | `[]` | v32 | Snap:518 / Apply:609; burned in `BuildModeController.Place` | `SaveSchema.cs:540` |
| `tribes` | `Tribes` | `List<TribeState>` | 34 | `[]` | v34 | Snap:519 / Apply:610 | `SaveSchema.cs:552`; record `WorldContent.cs:93-133` |
| `wards` | `Wards` | `List<WardStoneState>` | 34 | `[]` | v34 | Snap:520 / Apply:611 | `SaveSchema.cs:563`; record `WardContent.cs:111-133` |
| `arena` | `Arena` | `ArenaProgress?` | 34 | `Empty{0,0,0,0}` | v34 | Snap:521 / Apply:612. **Shadowed by a PlayerPrefs mirror — §7.2** | `SaveSchema.cs:575`; struct `ArenaProgress.cs:29-47` |
| `petActiveSlots` | `PetActiveSlots` | `List<string>` | 34 | `[]` | v34 | Snap:522 / Apply:613; consumed by `PetAcquisitionService` | `SaveSchema.cs:588` |
| `obsidianQueue` | `ObsidianQueue` | `ObsidianQueueState` | 35 | `Empty()` 3-channel | v35 (folds legacy) | Snap:523 / Apply:614 (**never null**) | `SaveSchema.cs:600`; type `ObsidianQueueState.cs:64` |
| `barracksLevel` | `BarracksLevel` | int? | — (rides v35) | 1 (clamped `<1 → 1`) | — | Snap:524 / Apply:615 | `SaveSchema.cs:610` |
| `troopLevels` | `TroopLevels` | `Dictionary<string,int>` | — (rides v35) | `{}` | — | Snap:525 / Apply:616 | `SaveSchema.cs:618` |
| `gearLevels` | `GearLevels` | `Dictionary<string,int>` | — (WO-808, rides v35) | `{}` | — | Snap:526 / Apply:617 | `SaveSchema.cs:626` |
| `everBuiltStructureIds` | `EverBuiltStructureIds` | `List<string>` | 36 | `[]` | v36 (3-leg union) | Snap:527 / Apply:618; gate input to `StructureSingleton.MayBakedTwinSurface` | `SaveSchema.cs:642`; helpers `GameState.cs:538,550` |

### 3.1 Fields on `GameState` that are NOT on the wire

| `GameState` field | Why it never serializes | Citation |
|---|---|---|
| `SchemaVersion` | travels as the envelope's `storeVersion`, not inside `state`; stamped post-load | `GameState.cs:34`; stamped `GameStateService.cs:353,1069` |
| `PartySize` | derived property, not a field | `GameState.cs:308` |

### 3.2 Fields validated / clamped on load

`SaveSchema.Validate` (`:691-867`) rejects NaN/±Infinity and clamps. `NonNegInt` = `max(0, floor(n))`
(`:654`); `FiniteInt` = `floor(n)` (`:665`); `RequireFinite` throws on NaN/Inf (`:672`).
Clamped: `resources.{crystals,food,coins}` `:702-704`; `bestWave` `:707`; `voidshards` `:708`;
`stone/iron/wood` `:709-711`; `wallLevel` `:712`; `atbLossStreak` `:713`; `aetherCrystals` `:714`;
`magic` `:715`; `petBonds/towers/towerAbilities` `:718-720`; `pets[].level/.xp` `:729-730`;
`inventory.*` `:738-741`; `pendingBuilds[].*` `:751-753`; `activeDungeonRun.*` `:761-771`;
`inbox[].sentAt/.readAt` `:782-784`; `lastInboxSyncAt` `:788`; `lastHarvestClaimMs` `:790`;
`echoCount/siloResources/wavesCompleted` `:793-798`; `population*` `:801-808`; `heroLevel/Xp/LifetimeXp`
`:811-816`; `buildJobs[].startMs/.durationMs` `:824-825`; `adSkipsUsedToday` `:830`;
`obsidianQueue.<ch>.boughtSlots` + every job `:841-843`; the **paid basket** `:889-893`.
`zones`/`settlements` are null-defaulted to `[]` at `:848-853`.
Volumes and joystick are **finite-checked only, never clamped** (`:855-859`).
**Not validated at all:** `arena` (ints, no NaN risk — stated `SaveSchema.cs:572`), and every
string/dictionary/list-of-record field.

---

## 4. NESTED PERSISTED TYPES — full field maps

### 4.1 `ResourceBalance` (struct) — `NestedTypes.cs:41-59`
`crystals` / `food` / `coins`, all `int`. `Starter` = {250,80,15} (`:55`), `Zero` (`:58`).
Note `wood`/`iron`/`stone`/`magic`/`voidshards` are **not** in this struct — they are flat
`GameState` scalars (`NestedTypes.cs:69`).

### 4.2 `PetData` (class) — `NestedTypes.cs:24-37`
`id`, `ownerId` (always `"local-player"`, `:28`), `species`, `nickname` (omitted when null, `:31`),
`level`, `xp`, `unlockedSkillIds`, `equippedActiveIds`.
⚠ Do not confuse with `Assets/Data/PetData.cs` — a different type, an SO balance layer (§5.3).

### 4.3 `AtbInventory` (struct) — `NestedTypes.cs:105-120`
`potions`, `manaCrystals`, `cleanses`, plus `torches` — added **without a schema bump**,
`NullValueHandling.Ignore` (`:115-116`).

### 4.4 `PendingTowerBuild` (struct) — `NestedTypes.cs:95-101` — `slot`, `ability`, `finishAt` (unix-ms).

### 4.5 `ChatContact` / `ChatMessage` — `NestedTypes.cs:124-142`
`code`,`nickname` / `id`,`senderCode`,`recipientCode`,`phraseId`,`sentAt`,`readAt?`.

### 4.6 `LootStash` — `NestedTypes.cs:146-156`
`crystals`,`food`,`coins`,`stone`,`iron`,`wood`,`petBondShards`,`skillPoints`.

### 4.7 `ActiveDungeonRun` — `NestedTypes.cs:160-171`
`dungeonId`,`avatarNodeId`,`visitedNodes`,`clearedEncounters`,`openedChests`,`readLore`,`loot`,`startedAt`.

### 4.8 `DungeonProgress` — `NestedTypes.cs:185-226`
`discovered`,`cleared`,`bestTime`,`noHitClear`; plus three **no-bump additive** optionals:
`deathsByDungeon` (`:199`), `loreReadByDungeon` (`:205`), `seedTree` (`:208` → `SeedTree.plantedAtWave`, `:178-181`).

### 4.9 `QuestState` / `QuestProgress` — `NestedTypes.cs:230-276`
`QuestState`: `beatIndex`, `flags`, `stageId` (WO-290, no bump), `counters` (WO-854, no bump, `:246`).
`QuestProgress`: `active`, `completed`, `available`, `keystones` (WO-290, no bump), `trackedId` (WO-454, no bump, `:263`).

### 4.10 `RegionProgress` — `NestedTypes.cs:280-296` — `discovered`, `cleared`.

### 4.11 `PlacedStructureData` (struct, plain public fields — **no `[JsonProperty]`**) — `PlacedStructureData.cs:37-74`
`itemId`, `cellX`, `cellZ`, `yawSteps`, `level`, `yawOffset`, `worldY` (v27), `wallMounted` (v27).
Because there are no `[JsonProperty]` attributes the wire keys are the **field names verbatim** —
renaming a field here is a silent breaking change.

### 4.12 `PlacedDefenderData` (struct, plain fields) — `PlacedDefenderData.cs:40-52`
`itemId`, `cellX`, `cellZ`, `yawSteps`.

### 4.13 `ArenaProgress` (struct, plain fields) — `ArenaProgress.cs:29-47`
`Wins`, `Losses`, `Streak`, `TotalPurse` (long). `Empty` at `:44`; `TotalRaids` derived `:47`.
⚠ Plain PascalCase fields → the wire keys are `Wins`/`Losses`/`Streak`/`TotalPurse`, unlike every
camelCase neighbour.

### 4.14 `BuildJobData` (struct) — `BuildJobData.cs:97-215`
| key | member | v | note |
|---|---|---|---|
| `structureId` | `StructureId` | 13 | `:104` |
| `jobType` | `JobType` | 13 | `:107`; enum `:35-41` |
| `kind` | `Kind` | 35 | `:116`; absent → 0 = Build; backfilled by v35 |
| `channel` | `Channel` | 35 | `:124`; absent → 0 = Builder |
| `startMs` | `StartMs` | 13 | `:127` |
| `durationMs` | `DurationMs` | 13 | `:130` |
| `targetTier` | `TargetTier` | F8-51, no bump | `:139` |
| `paidWood` | `PaidWood` | **37** | `:160` |
| `paidFood` | `PaidFood` | **37** | `:163` |
| `paidIron` | `PaidIron` | **37** | `:166` |
| `paidCrystals` | `PaidCrystals` | **37** | `:169` |
| `paidMagic` | `PaidMagic` | **37** | `:172` |
| — | `Paid` (`JobCost`) | — | `[JsonIgnore]` view over the 5 ints, `:176` |
| — | `FinishMs`, `Type`, `JobKind`, `ChannelId` | — | `[JsonIgnore]` derived, `:190-214` |

A pre-v37 job refunds **ZERO** on cancel and says so — the cost was never recorded and is not
reconstructable (`SaveMigrator.cs:660-668`).

### 4.15 `ObsidianQueueState` / `ChannelState` — `ObsidianQueueState.cs`
`channels` : `Dictionary<ChannelId,ChannelState>` (`:64`). Per channel: `boughtSlots` (`:36`),
`active` (`:39`), `pending` (`:42`). **Not persisted:** the depth cap (5/line) and slot counts are
config, in `BuildTimerConfig.queueDepthPerLine` — see `SaveSchema.cs:41`'s v37 note.

### 4.16 `ArmyStorage` — `ArmyStorage.cs:40-397`
| key | member | v | note |
|---|---|---|---|
| `owned` | `Owned` : `List<PlayerTroop>` | 22 | `:46` |
| `nextId` | `NextId` | 22 | `:80`, monotonic id mint |
| `lastRecoveryTickMs` | `LastRecoveryTickMs` | WO-779, no bump | `:92`; 0 → seed-to-now, credit nothing (`:357-360`) |
| `loadouts` | `Loadouts` : `List<ArmyLoadoutSlot>` | **38** | `:101` |
| `activeLoadout` | `ActiveLoadoutIndex` | **38** | `:104` |
| — | `MaxArmySize` | — | `[JsonIgnore]` derived (`:58`). **A legacy stored `maxArmySize` key is silently ignored on load** (`:56`) |

`ArmyLoadoutSlot` = `name`, `rows` (`ArmyLoadoutBank.cs:54-55`); `ArmyLoadoutRow` = `troopId`, `count`
(`:38-39`); `SlotCount = 3` (`:20`).

### 4.17 `PlayerTroop` — `PlayerTroop.cs:38-97`
`id`, `troopDefId`, `veterancyRank`, `wounded`, `recoveryRemaining`. `MaxVeterancyRank=6` (`:44`).

### 4.18 World records (plain public fields, **no `[JsonProperty]`**)
- `ZoneState` — `RegionZone.cs:87-113`: `RegionKey` (enum NAME), `Discovered`, `Cleared`, `Neighbors`, `Destination`.
- `TribeState` — `WorldContent.cs:93-133`: `Id`, `RegionKey`, `Anchor` (`WorldPoint`), `MembersRemaining` (-1 = unrolled), `Cleared`, `ClearCount`, `LastSeenAtMs`.
- `SettlementState` — `WorldContent.cs:160-200`: `SiteId`, `RegionKey`, `Position`, `Phase`, `Hp`, `MaxHp`, `RazedUntilDay`.
- `WardStoneState` — `WardContent.cs:111-133`: `Id`, `RegionKey`, `ReachRadiusGranted`, `Lit`.

All four store their region as the **enum NAME string** deliberately, so they survive enum
renumbering (`SaveSchema.cs:349,376`; `RegionZone.cs:89`).

---

## 5. ⚠ DEAD DATA — persisted-but-never-read, and read-but-never-written

**This is the highest-value section of the document.** Every row was verified by grepping the whole
`Assets/` tree and excluding tests, editor regressions and debug overlays.

### 5.1 Declared + migrated, but structurally impossible to save or load

| Field | The gap | Citation |
|---|---|---|
| **`equippedRingId`** | On `PersistedState` (`SaveSchema.cs:441`), **seeded by `MigrateToV26`** (`SaveMigrator.cs:404`) — but there is **no matching `GameState` field**, **no line in `Snapshot()`**, and **no line in `ApplyPersisted()`**. It round-trips a raw `PersistedState` through the migrator and nothing else. The live equip actually persists to PlayerPrefs `dotr-equip-ring-<class>` (`GameStateService.cs:67`, written `GearLoadout.cs:957`). | already documented at `Assets/_Modules/Core/Tests/SaveLoadRoundTripTest.cs:229-235` |
| **`equippedAmuletId`** | identical — `SaveSchema.cs:447`, `SaveMigrator.cs:405`, no SO field, no Snapshot/Apply line; live store is `dotr-equip-amulet-<class>` (`GameStateService.cs:69`, `GearLoadout.cs:962`) | `SaveLoadRoundTripTest.cs:229-235` |

Consequence: a schema version (v26) exists solely to seed two fields that no live code path can ever
populate or consume. Accessory persistence today is entirely outside the save.

### 5.2 Persisted and restored, but with no gameplay consumer

| Field | Only reader found | Citation |
|---|---|---|
| `voidshards` | `DebugCanvasUI.cs:137` (debug readout) | `SaveSchema.cs:245` |
| `towerAbilities` | `DebugCanvasUI.cs:139` (debug readout) | `SaveSchema.cs:247` |
| `stone` | `DevPanelController.cs:617` (readout) + `:1250` (dev grant `+50000`) | `SaveSchema.cs:249` |
| `atbLossStreak` | **none** outside the save layer | `SaveSchema.cs:268` |
| `contacts` / `blockedCodes` / `inbox` / `lastInboxSyncAt` | **none** — the whole social/chat slice has no runtime consumer; `lastInboxSyncAt` survives only as a *unit precedent* other clocks cite (`TimeSource.cs:7`) | `SaveSchema.cs:276-279` |
| `voiceOvers` | none — "stored, not yet wired" | `GameState.cs:121-122` |

### 5.3 Deliberately kept dead (documented, do not "clean up")

| Field | Why it stays | Citation |
|---|---|---|
| `aetherCrystals` | v18 folded it into `resources.crystals`; the `JsonProperty` is kept because removal is a breaking wire change. Always serializes 0 | `SaveSchema.cs:282-289`; `GameState.cs:54-58`; fold `SaveMigrator.cs:331-341` |
| `buildJobs` | v35 folded it into `obsidianQueue.Builder`; retained on the wire for back-compat, **no longer read at runtime** | `GameState.cs:474-476`; `BuildTimerService.cs:28` |
| `pendingBuilds`, `buildingCooldowns` | same v35 fold; the migrator clears/drains them (`SaveMigrator.cs:545,567,589`); no runtime writer remains | `ObsidianQueueState.cs:9` |
| `PetData.damagePerLevel`, `PetData.hpMultiplierPerLevel` (the **SO**, not the save record) | orphaned by WO-993 — their only reader `PetProgression` was deleted with the pet-levelling surface. Kept because they are **serialized fields on shipped `.asset` files**, so deleting them is a data migration, not a code retirement | `Assets/Data/PetData.cs:13-18,41,43`; corroborated `Assets/_Modules/Pets/Pet.cs:653-655` |
| `ArmyStorage.maxArmySize` (legacy stored key) | superseded by the derived `MaxArmySize`; harmlessly ignored on load | `ArmyStorage.cs:56` |

### 5.4 A whole persisted AXIS that no production code can move

**The Echo LEVEL axis is dead data.** `echoLanes` carries a `lane:level` token grammar
(`SaveSchema.cs:507-517`) and the level is read by the bonus math — but the only API that can RAISE
a level, `EchoAssignments.SetLevel` (`Assets/_Modules/Village/Harvest/EchoAssignments.cs:232`),
has **zero production callers**; only the regression harness calls it
(`EchoSpecializationRegression.cs:352`). So every Echo is Lv 1 forever, and the "Lv N" readout was
deliberately removed from the card/roster because it displayed data that can never change:
`EchoCardVM.cs:140`, `EchoRosterVM.cs:124`, `DataRegression.cs:572`.
This is **guarded in both directions** by `EconomySweepRegression.cs:307-343`: it fails if `SetLevel`
is deleted (the readout was the dead surface, not the API) **and** fails if a production caller
appears without the readout coming back.

### 5.5 Read-but-never-written PlayerPrefs keys (the mirror-image defect)

| Key | Read at | Verdict |
|---|---|---|
| **`camerashake`** | `Assets/_Modules/Village/Buildings/Tower.cs:1235` | **LIKELY DEFECT.** No writer exists anywhere. The player-facing screen-shake toggle actually writes `dotr-settings-screen-shake` (`Assets/_Modules/Settings/SettingsModel.cs:218`), so Tower shake can never be turned off from the settings UI |
| `ff.gaitforensics` | `HeroGaitForensics.cs:74` | not registered in the `FeatureFlags` table, so `OwnerDevToolsOverlay` cannot toggle it either — unreachable |
| `castle.liftY` | 11 runtime read sites (`HeroLocomotion.cs:1738`, `CastleMoatBuilder.cs:571,696,858,1012`, `HeroHealth.cs:1000`, `HeroControlEnsurer.cs:651`, `HomeReturnPortalInjector.cs:159`, …) | written **only** by the editor tool `Assets/Editor/WorldMergeBuilder.cs:430`, restored `:459-460`. In a shipped player it is read-only and always falls back to the `3f` default |
| `<SeatOnGroundOnStart._baseLiftPrefsKey>` | `SeatOnGroundOnStart.cs:96,189` | key NAME is inspector data; no writer exists for whatever name is authored |
| `dotr-save.sig` | `HasKey`/`DeleteKey` only, `LocalSaveProvider.cs:46-47` | dead cleanup path — the signature moved inline at `SaveSchema.cs:139-155`; nothing reads or writes the sibling key |
| `dotr-skillbar-extra-v1` (legacy global) | `AssignableSkillBar.cs:299` | migration-read only; no production writer |
| `realm-defenders-settings` | `SaveMigrator.cs:248,250` | migration-read only; production writer retired |
| `dev.console`, `dev.playerbot`, `diag.castlenav`, `autopilot.seed` | `DevBootstrap.cs:77`, `PlayerBot.cs:40`, `CastleNavTopologyDiag.cs:51`, `AutoPilotDriver.cs:7029` | intentional manual opt-ins — not defects |

### 5.6 Written-but-never-consumed PlayerPrefs

| Key | Written | Verdict |
|---|---|---|
| `dotr-legacy-identity-orphaned` | `GameStateService.cs:1284` | read at `:1278` **only to append to itself**. No consumer acts on the list — a deliberate write-only forensic stash (`:1257-1266`), correct as designed but worth knowing it is inert |

---

## 6. WHAT IS NOT IN THE SAVE BUT BEHAVES AS IF IT WERE — the PlayerPrefs census

Invisible to the schema, invisible to the migrator, invisible to the validator, invisible to the
HMAC. **This is where two separate bugs lived on 2026-08-15.** Full write/read/delete citations follow;
absence of a delete column entry means *no code path anywhere clears it except*
`AdminOverlay.OnFullReset` → `PlayerPrefs.DeleteAll()` (`Assets/_Modules/HUD/AdminOverlay.cs:989` —
the **only** `DeleteAll` in the project).

### 6.1 Save envelope & identity

| Key | Type | Stores | Written | Read | Deleted |
|---|---|---|---|---|---|
| `dotr-save` | String | the whole signed envelope | `LocalSaveProvider.cs:36` | `LocalSaveProvider.cs:28,31` | `LocalSaveProvider.cs:43` (no production caller — §7.1) |
| `dotr-save.sig` | String | legacy sibling HMAC | **none** (retired) | none | `LocalSaveProvider.cs:46-47` |
| `realm-defenders-settings` | String | pre-envelope settings | none (tests only) | `SaveMigrator.cs:248,250` | `SaveMigrator.cs:274-275` |
| `dotr-cloud-identity-attested` | String | wallet address that passed attestation | `GameStateService.cs:765` | `:1222` | — |
| `dotr-legacy-identity-orphaned` | String | `;`-list of retired identities | `:1284` | `:1278` (append only) | — |
| `dotr-sync-queue` | String | JSON retry-marker queue | `:1900,1959` | `:1920,1966` | `:1943,1953` |
| `dotr-account-id-v1` | String | **the local guest account GUID** | `ClanService.cs:281` | `:275,277` | — |
| `dotr-clans-v1` | String | clan membership JSON | `ClanService.cs:311` | `:287,290` | `:307` (parse failure only) |

The `guest-local-<64hex>` id itself is **not** a pref — it is minted into `GameState.BoundWallet`
(`GameStateService.cs:1236`) and lives inside the save. Shape rule `IsGuestIdentity` at `:1325`
must match the backend's `GUEST_RE` (`api/_lib/wallet-auth.js`) or guests 401 silently (`:1322-1324`).

### 6.2 Feature flags — `ff.*`

All resolve through one wrapper: `FeatureFlags.Get(name, defaultOn)` →
`PlayerPrefs.GetInt("ff." + name, -1)` at **`Assets/_Modules/Core/FeatureFlags.cs:898`**.
Generic writer: `OwnerDevToolsOverlay.cs:401`. URL allow-list: `FeatureFlags.cs:964`.
**No production path deletes any `ff.*` key.**

72 flags are registered. Declaration lines in `FeatureFlags.cs`:
`ff.raid` :27 · `ff.arena` :33 · `ff.singlehero` :40 · `ff.blinkarmor` :52 · `ff.knightonly` :68 ·
`ff.basebuilding` :77 · `ff.buildtimers` :85 · `ff.raidwalk` :98 · `ff.raidtest` :107 ·
`ff.overworldleaderonlyroam` :114 · `ff.outposttravel` :123 · `ff.blinkchrome` :130 ·
`ff.webtrace` :139 · `ff.buildingupgradepanel` :147 · `ff.partyshop` :157 · `ff.customdialogue` :164 ·
`ff.overworldencounter` :173 · `ff.regionroam` :181 · `ff.bypasspetselect` :190 ·
`ff.runtimeworldseam` :202 · `ff.enemyinjured` :209 · `ff.heroinjured` :217 ·
`ff.enemyrootedcast` :224 · `ff.enemystructureaware` :236 · `ff.enemyweapons` :245 ·
`ff.battlehud9zone` :261 · `ff.waveautostart` :269 · `ff.wavebreachtoatb` :279 ·
`ff.dungeonrealtime` :292 · `ff.devhotkeys` :302 · `ff.devresourcetool` :340 · `ff.flagbutton` :359 ·
`ff.hubambientvfx` :369 · `ff.lockon` :378 · `ff.castlemoat` :390 · `ff.mergedworld` :402 ·
`ff.homereturnportal` :410 · `ff.gatetraversal` :423 · `ff.castleeditorbridgeseam` :429 ·
`ff.gatebeacon` :436 · `ff.outpostcaves` :447 · `ff.dungeonportals` :463 · `ff.worldfeel` :476 ·
`ff.noautoheal` :485 · `ff.combatfeel` :495 · `ff.tutorialv2` :506 · `ff.heropackage` :517 ·
`ff.knightv3` :528 · `ff.mocaploco` :541 (also read raw at `HeroBodySwapper.cs:595`) ·
`ff.weapongripinfer` :549 · `ff.stakedemo` :561 · `ff.skrpreview` :579 ·
`ff.realmstorepurchase` :632 · `ff.rewardedadskip` :664 · **`ff.maptab` :676** ·
`ff.stakingpolishbonus` :693 · `ff.caravanmobile` :702 · `ff.combathud611` :718 ·
`ff.battlehudvm` :729 · `ff.sheathdrawnrot` :742 · `ff.petcombat` :754 · `ff.barracks` :764 ·
`ff.colosseum` :773 · `ff.wallstab` :789 · `ff.poicallouts` :803 · `ff.dungeonfpv` :829 ·
`ff.dungeoniso` :836 · `ff.hubfoliage` :848 · `ff.biomeroads` :873.
Editor-menu writers exist for `ff.blinkchrome` (:988), `ff.overworldencounter` (:1008),
`ff.lockon` (:1028), `ff.stakedemo` (:1048), `ff.skrpreview` (:1068), `ff.combathud611` (:1089).
**Unregistered:** `ff.gaitforensics` (`HeroGaitForensics.cs:74`) — see §5.5.

### 6.3 Per-class equipment & ability bars — `EquipPrefKeys`, `GameStateService.cs:58-118`

Suffix = lowercase class key (`GearLoadout.PrefJobKey()`, `GearLoadout.cs:761`).

| Key pattern | Written | Read | Deleted |
|---|---|---|---|
| `dotr-equip-weapon-<class>` (`:61`) | `GearLoadout.cs:1129,1173,1227` | `:524,679` | `GameStateService.cs:1103` |
| `dotr-equip-offhand-<class>` (`:65`) | `GearLoadout.cs:1131,1168,1191` | `:527,716` | `:1103` |
| `dotr-equip-armor-<class>` (`:63`) | `GearLoadout.cs:1208,1243` | `:727`; `HeroBodySwapper.cs:1072` | `:1103` |
| **`dotr-equip-ring-<class>`** (`:67`) | `GearLoadout.cs:957,987` | `:739` | `:1103` |
| **`dotr-equip-amulet-<class>`** (`:69`) | `GearLoadout.cs:962,992` | `:747` | `:1103` |
| `dotr-loadout-<class>-v1` (`:110-112`) | `HeroLoadout.cs:306` | `:316` | `:1106` |
| `dotr-skillbar-<class>-extra-v1` (`:115-117`) | `AssignableSkillBar.cs:278` | `:295,297` | `:1111` |
| `dotr-skillbar-extra-v1` (legacy global, `:104`) | none | `AssignableSkillBar.cs:299` | `:1115-1117` |

The ring/amulet rows are the live home of what `SaveSchema.equippedRingId/equippedAmuletId` was
supposed to be (§5.1).

### 6.4 Building / village progression — the sharpest blind spot

| Key | Type | Stores | Written | Read | Deleted |
|---|---|---|---|---|---|
| **`dotr.resbuilding.level.<buildingId>`** | Int | resource-building level (default 1) | `ResourceBuildingState.cs:220` | `:67,245` | `:262` (single id), `:280` (`ResetAll` — **orphan**, §7.1) |
| **`dotr.tech.node.<nodeId>`** (today only `…arcane_forge`) | Int | TechTree unlock | `TechTree.cs:57` | `:47` | `:78` (`ResetAll` — **orphan**) |
| **`dotr.migration.dualfamily-level-reset.v1`** | Int | one-shot marker for the dual-family level reset | `DualFamilyLevelResetMigration.cs:222` | `:102` | none in production |
| `dotr.collector.pending.<id>` | Float | uncollected accrued yield | `ResourceCollector.cs:616` | `:599` | — |
| `dotr.collector.hp.<id>` | Float | collector HP | `:617` | `:600` | — |
| `dotr.collector.lastaccrual.<id>` | String (ticks) | last accrual tick | `:618` | `:606` | — |
| `dotr-harvest-last-active` | String (ticks) | last active session | `WorkerManager.cs:225` | `:284` | — |
| `build.hint.sessions` / `build.hint.placements` | Int | build-hint counters | `BuildHudController.cs:983` / `:965` | `:945,1014` / `:946,964,1015` | — |
| `DeNelle.RotationYawCorrections.v1` | String | JSON per-prefab yaw corrections | `RotationCorrectionRegistry.cs:101` | `:64` | `:144` (test-only) |

> ⚠ **CANON CONTRADICTION.** `SaveSchema.cs:401-402` says v23's `buildingTiers` *"Folds in the
> resource-building levels previously kept loose in PlayerPrefs → one persisted source of truth."*
> It did not. `ResourceBuildingState` still reads and writes `dotr.resbuilding.level.<id>`
> (`ResourceBuildingState.cs:54,67,220`), outside the save. There are two level stores, and only one
> of them is versioned, migrated, validated or signed.

### 6.5 World discovery / camps / raids — all persist, none reset

| Key pattern | Type | Written | Read |
|---|---|---|---|
| `dotr-node-discovered-<kind>-<x>_<y>_<z>` (prefix `NodeDiscoverySystem.cs:61`, built `:387-391`) | Int | `:241` | `:185` |
| `dotr-dungeon-portal-discovered-<x>_<y>_<z>` (prefix `DungeonWorldPortalSpawner.cs:174`, built `:1294-1298`) | Int | `:793` | `:554` |
| `dotr-camp-cleared-<CampId>` (`ClaimableCamp.cs:90`) | String "1" | `:251` | `:662` |
| `dotr-camp-claimed-<CampId>` (`:91`) | String (OutpostType) | `:302,360` | `:631` |
| `dotr-camp-secured-<CampId>` (`:92`; also `CampDefenseWave.cs:57`) | String "1" | `:481,599`; `CampDefenseWave.cs:217` | `:628`; `CampDefenseWave.cs:101` |
| `dotr-camp-recipe-<CampId>` (`:93`) | String | `:418` | `:648` |
| `dotr-raid-cleared-<OutpostId>` (`EnemyOutpost.cs:115`) | String "1" | `:622` | `:221` |
| `dotr-raid-owner-<configId>` (`RaidClaimService.cs:52`) | String "1" | `:126` | `:105` (cleared per-id `:142`, gameplay not reset) |

### 6.6 Currencies & wallets outside the save

| Key | Type | Stores | Written | Read |
|---|---|---|---|---|
| **`dotr-talents-v1`** | String | JSON `{Wisdom, Unlocked[]}` — the **Wisdom / talent wallet + unlocked talent nodes** | `WisdomCurrencyService.cs:183` | `:158,161` |
| `dotr-cosmetics-v1` | String | glimmer balance + owned/equipped cosmetics | `GlimmerCurrencyService.cs:319` | `:301,304` |
| **`dotr-arena-wins` / `-losses` / `-streak` / `-purse`** | Int/Int/Int/String | the **arena W/L record mirror** | `ArenaProgressStore.cs:110-113` | `:96,99,100,101,103` |
| `dotr-arena-skr-balance` | String (long) | stub SKR wallet | `ArenaWalletService.cs:123` | `:108,111` |
| `BP_Level` / `BP_XP` / `BP_HasPremium` | Int | battle pass | `BattlePassManager.cs:298-300` | `:306-308` |
| `dotr-referral-code` / `-url` / `-claimed` | String | referral | `ReferralService.cs:153,154,268` | `:286,287,288` |
| `dotr-redeemed-promos` | String CSV | redeemed promo codes | `PromoCodeService.cs:243` | `:234` |
| `dungeon.pendingpolishscores` | String CSV | FIFO of pending polish star scores | `DungeonRunPayout.cs:164` | `:152` (cleared `:145`) |
| `dungeon.polishrollsused` | Int | rolls spent on current stone | `:119` | `:104` (reset `:130`) |

### 6.7 Quests, onboarding, tutorial gates

| Key | Type | Written | Read | Deleted |
|---|---|---|---|---|
| `dotr-daily-quests-v1` (`DailyQuests.cs:198`) | String | `:433` | `:419,420` | — |
| `dotr-daily-quests-day1-done-v1` (`:201`) | Int | `:278` | `:398` | — |
| `dotr-daily-quest-gates-visited-v1` (`DailyQuestGateBridge.cs:39`) | String | `:178` | `:162` | — |
| `onboarding.fullTutorial` (`OnboardingMode.cs:32`) | Int | `:59` | `:50` | — (overwritten by `TitleController.cs:364`) |
| `yarn.companionMeeting.seen` (`CompanionMeetingTrigger.cs:50`) | Int | `:191` | `:163` | `AdminOverlay.cs:738,1003` **only** |
| `dotr-unmute-migration-v1` (`AudioBootstrap.cs:154`) | Int | `:179` | `:158` | — |

### 6.8 Settings, audio, misc runtime

| Key | Type | Written | Read |
|---|---|---|---|
| `dotr-settings-master-volume` (`SettingsModel.cs:64`) | Float | `:95` | `:92` |
| `dotr-settings-quality-tier` (`:65`) | Int | `:196` | `:189,202` |
| `dotr-settings-screen-shake` (`:66`) | Int | `:218` | `:215` (**not** read by `Tower.cs` — §5.5) |
| `dotr-ambient-music-choice-<(int)AmbientContext>` (`AudioService.cs:1028`) | Int | `:1072` | `:1087` |
| `dotr-gameclock-epoch` (`GameClock.cs:33`) | String (ticks) | `:51` | `:47` |
| `dotr-event-queue` (`EventTracker.cs:54`) | String | `:355` | `:367` (cleared `:391`) |
| `atb.controlMode.<memberId>` (`AtbControlModeStore.cs:32`) | Int | `:51` | `:42,59` |
| `dotr.attachment-offsets` (`AttachmentOffsetRegistry.cs:45`) | String | `:278` | `:186,188` |
| `anim.runCadence` (`HeroLocomotionCadence.cs:45`) | Float | `:70` | `:63` |
| `dotr.devunlock` (`HelpMenu.cs:167`) | Int | `:715` | `:714` |
| `castle.liftY` | Float | editor only `WorldMergeBuilder.cs:430` | 11 runtime sites — §5.5 |

---

## 7. NEW GAME / RESET COVERAGE — what is cleared and what SURVIVES

**This section is the most actionable thing in the document.**

### 7.1 The reset entry points

| # | Entry point | Production? | Effect | Citation |
|---|---|---|---|---|
| A | `TitleController.OnStartNew()` — the "Start New" button | **YES — the one real player New Game** | `ResetToNewGame()` + `DialogueResetService.ResetForNewGame()` + `OnboardingMode.ChooseFastPath()` | `TitleController.cs:350-371` (calls `:359,:360`) |
| B | `GameStateService.ResetToNewGame()` | YES (via A) | ~55 `GameState` assignments + `ClearEquipPrefs()` + `StateReplaced` + `Save()` | `GameStateService.cs:945-1073` |
| C | `ClearEquipPrefs()` | YES (only caller = B at `:1066`) | deletes §6.3 keys only | `GameStateService.cs:1095-1125` |
| D | `DialogueResetService.ResetForNewGame()` | YES (via A) | Yarn storage + `DialogueEventBus.ClearAll()` — **in-memory only** | `DialogueResetService.cs:53-71` |
| E | `WaveManager.HandleStateReplacedForDifficulty()` | YES (subscribes to `StateReplaced`) | `DynamicDifficulty.ResetForNewGame()` — in-memory | `WaveManager.cs:2782-2789`, hook `:2773-2780` |
| F | `HelpMenu.OnResetProgress()` "Reset Hero & Pet" | **YES — ships to players, NOT dev-gated** | reflection → `ResetToNewGame`, then `SceneRouter.GoHeroSelect()`. **Does NOT call `DialogueResetService`** | `HelpMenu.cs:622-653`; offered `HelpMenuVM.cs:213` |
| G | `OwnerDevToolsOverlay.ResetToNewGame()` | ships, owner-account gated | `ResetToNewGame()` only | `OwnerDevToolsOverlay.cs:381-387`, wired `:277` |
| H | `AdminOverlay.OnReset()` | dev overlay | `ResetToNewGame` + delete `yarn.companionMeeting.seen` (`:738`) + reload `Village2` | `AdminOverlay.cs:727-747` |
| I | `AdminOverlay.OnFullReset()` — **the only true wipe** | dev overlay, two-tap armed | archives the two authoring JSONs (`:977-985`) then `PlayerPrefs.DeleteAll()` (`:989`) + `Application.Quit()` | `AdminOverlay.cs:962-995` |
| J | `LocalSaveProvider.Delete(slot)` | **no production caller** — test harness only (`AutoPilotDriver.cs:4939`) | deletes `dotr-save` + `.sig` | `LocalSaveProvider.cs:41-49` |
| **L** | **`ResourceBuildingState.ResetAll()`** | **ORPHAN — ZERO callers** | *would* delete `dotr.resbuilding.level.*` then call `TechTree.ResetAll()` | `ResourceBuildingState.cs:276-285`; doc claiming it is the New Game reset `:268-275` |
| **M** | **`TechTree.ResetAll()`** | **ORPHAN** — only caller is L, itself dead | would delete `dotr.tech.node.*` | `TechTree.cs:76-80` |

There is **no file-based save** — everything is PlayerPrefs. No `File.Delete` touches a save path.

### 7.2 What SURVIVES a New Game

`ResetToNewGame` touches **exactly one** PlayerPrefs family: the equip/loadout/skillbar keys
(`GameStateService.cs:1095-1120`). Everything below therefore persists into a "new" game.

**Deliberate carve-outs** (documented at `GameStateService.cs:928-931,1067-1068`):
`BoundWallet`, `BreachStyle`, and all social fields (`MyInviteCode`, `Contacts`, `BlockedCodes`,
`Inbox`, `LastInboxSyncAt`).

**Accidental survivors** — nothing clears them:

| Survives | Store | Why |
|---|---|---|
| **Every resource-building level** | `dotr.resbuilding.level.<id>` (`ResourceBuildingState.cs:220`) | only eraser is the orphan `ResetAll()` (`:276`) |
| **Every TechTree unlock** | `dotr.tech.node.<id>` (`TechTree.cs:57`) | only eraser is orphan `TechTree.ResetAll()` (`:76`) |
| **The arena W/L record — and it RESURRECTS** | `dotr-arena-*` (`ArenaProgressStore.cs:110-113`) | `ResetToNewGame` zeroes `GameState.Arena` (`GameStateService.cs:1053`) but not the mirror, and `ArenaProgressStore.Hydrate()` (`:86-106`) treats `live.TotalRaids == 0` — **exactly the post-reset state** — as "not loaded yet" and re-pulls the old record over the fresh save |
| **The Wisdom / talent wallet + unlocked talents** | `dotr-talents-v1` (`WisdomCurrencyService.cs:183`) | the file contains no `Reset`/`DeleteKey` at all |
| Cosmetics / Glimmer wallet | `dotr-cosmetics-v1` (`GlimmerCurrencyService.cs:319`) | no reset |
| Battle Pass level/XP/premium | `BP_*` (`BattlePassManager.cs:298-300`) | no reset |
| Arena SKR balance | `dotr-arena-skr-balance` (`ArenaWalletService.cs:123`) | no reset |
| **All world/camp/raid discovery flags** | §6.5 | no reset — a new game opens on a pre-explored world |
| **Collector banked haul, HP and accrual clock** | `dotr.collector.*` (`ResourceCollector.cs:616-618`) | a new game inherits the old town's banked resources |
| FTUE companion-meeting gate | `yarn.companionMeeting.seen` (`CompanionMeetingTrigger.cs:191`) | cleared **only** by the dev overlay (`AdminOverlay.cs:738,1003`); neither player-facing path clears it |
| All 72 feature flags | `ff.*` (`FeatureFlags.cs:898`) | no reset |
| Daily quests + gates, promos, referrals, clan + account id | §6.6/6.7 | no reset |
| Game-clock epoch | `dotr-gameclock-epoch` (`GameClock.cs:51`) | a new game keeps the old world calendar |
| The dual-family migration latch | `dotr.migration.dualfamily-level-reset.v1` (`DualFamilyLevelResetMigration.cs:222`) | correct as install-scoped, but note the migration never re-runs for a new game |
| Offline-harvest last-active stamp, dev unlock, build hints, ATB control modes, settings, ambient choices, authoring registries | §6.4/6.8 | no reset |

### 7.3 Guards that exist

`ResetToNewGame`'s doc records THE STANDING RULE (`GameStateService.cs:936-943`): every persisted
`GameState` field is either assigned in the body or is a documented carve-out.
`Assets/Editor/Regression/ResetToNewGameFullClearRegression.cs:69-291` enumerates `GameState` by
**reflection** and fails on the next unassigned field. Two fields once failed that rule —
`Settlements` (never assigned) and `Zones` (only backfilled, so the old realm's discovery survived) —
fixed at `GameStateService.cs:1055` and `:1064-1065`.
**That guard covers `GameState` fields only.** Nothing equivalent exists for the PlayerPrefs
side-band, which is why §7.2 is as long as it is.

### 7.4 Asymmetry between the two player-facing reset paths

`TitleController.OnStartNew` clears dialogue state (`:360`); `HelpMenu.OnResetProgress` does not
(`HelpMenu.cs:622-653`), and it is not dev-gated (`HelpMenuVM.cs:213`). Yarn `$`-toggles and event
latches survive that route.

---

## 8. CROSS-WIRE CONSISTENCY — client ↔ Neon

### 8.1 What the client sends

`SendCurrentSnapshot()` (`GameStateService.cs:1736-1803`) serializes `Snapshot()` to a `JObject`
under the **same camelCase keys** the save file uses, **strips every null property**, adds a single
lowercase `playerId`, and POSTs it as JSON (`:1746-1751`). It is the **full `PersistedState`
snapshot**, not the flat delta shape.

`SyncDeltaPayload` (`:1991-2016`) — the flat 13-field shape (`PlayerId`, `SchemaVersion`,
`Crystals`…`Wood`, `Towers`, `TowerAbilities`, `BestWave`, `PetsJson`, `OwnedPetsJson`,
`StarterPetId`) — **is never uploaded.** It is only a retry MARKER written to the `dotr-sync-queue`
pref (`:1896-1904`), honestly documented as such at `:1716-1734`.

### 8.2 What the server stores

`api/game/save.js:261-305` `buildState()`: stores **every camelCase key** the body carried minus
`RESERVED_KEYS` (`:53-56`), skipping nulls and skipping PascalCase (`:267-275`); then promotes the
legacy PascalCase spellings onto their camelCase homes (`:280-302`). Guards (`:327-374`) enforce
bounds and anti-rollback on `bestWave` and the seven balances, both flat and nested, restoring rather
than deleting a rejected nested field (`:356-363` — the note at `:316-325` explains why deleting it
wiped 500 crystals in the first draft).
The upsert is a **shallow JSONB merge**, `game_state || EXCLUDED.game_state` (`:220`).

`api/schema.sql:59-65`:
```sql
CREATE TABLE IF NOT EXISTS player_data (
    player_id      TEXT        PRIMARY KEY,
    schema_version INTEGER     NOT NULL DEFAULT 10,
    game_state     JSONB       NOT NULL DEFAULT '{}',
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```
plus `trust` (`'wallet' | 'guest' | 'legacy'`), documented `:75-82`, written on every upsert (`save.js:214`).

### 8.3 ⚠ MISMATCHES

| # | Mismatch | Evidence |
|---|---|---|
| **1** | **`schema_version` is a lie — it is written as `10` on every save, forever.** The POST body is the `PersistedState` JObject + `playerId` (`GameStateService.cs:1746-1751`); `PersistedState` has **no** `schemaVersion` member (`SaveSchema.cs:233-643`) and the envelope's `storeVersion` never travels. `save.js:114` therefore reads `undefined` and `:212` writes `${schemaVersion ?? 10}`, matching the column default (`schema.sql:61`). A v38 save is stored labelled v10. | `GameStateService.cs:1750`; `save.js:114,212`; `schema.sql:61` |
| **2** | **The client never runs the migrator or the validator on server data.** `LoadFromBackend` deserializes into `PersistedState` (`GameStateService.cs:1442`) and applies it **directly** — no `SaveMigrator.MigrateForImport`, no `SaveSchema.Validate`, unlike the local `Load()` path (`:335,343`). A stale or hostile row bypasses both gates. It is also not HMAC-checked (the signature covers transport only). | `GameStateService.cs:1394-1489` vs `:334-352` |
| **3** | **The server stores ~82 fields; the client merges back 7.** `load.js:95-98` returns the whole stored object verbatim, and `save.js` stores everything — but `LoadFromBackend` only applies `bestWave` (server-wins), `resources`, `voidshards`, `aetherCrystals` (folded), `stone`, `iron`, `wood` (`GameStateService.cs:1463-1482`). Base layout, army, obsidian queue, hero level, quests, zones, echo lanes are all uploaded and stored and then **never read back**. `save.js:18-22` was written to fix exactly this on the *server* side; the *client* half of the fix was never made. | `GameStateService.cs:1463-1482`; `save.js:18-22`; `load.js:13-17` |
| **4** | `BackendLoadResponse` (`GameStateService.cs:2018-2022`) has `success` / `data` / `config` and **no `schemaVersion` member**, so the version `load.js:104` returns is discarded — the client cannot even detect mismatch #1. | `GameStateService.cs:2020-2022`; `load.js:104` |
| **5** | `equippedRingId`/`equippedAmuletId` are never in the payload (they are never in `Snapshot()`, §5.1), so the server never holds them either. `aetherCrystals` IS uploaded and is always 0. | `GameStateService.cs:442-529` |
| **6** | Payload-size caps are enforced per rail (`GUEST_MAX_BODY_BYTES` / `WALLET_MAX_BODY_BYTES`, `save.js:80,130-136`) and a guest body over the cap is a hard 400. As the snapshot grows with each schema bump, this is a silent cliff — no client-side pre-check exists. | `save.js:80,130-136` |
| **7** | The client's `IsGuestIdentity` (`GameStateService.cs:1325-1337`) and `IsCloudIdentityShaped` (`:1210-1218`) deliberately duplicate the backend's `GUEST_RE` / wallet regex. Two copies of one rule; a drift means saves 401 silently. Documented as a known risk at `:1322-1324`. | as cited |

### 8.4 Not a mismatch (verified)

The shallow merge is intentional and correct given the client strips nulls (`save.js:202-206`) — a
key an older client omits keeps its stored value rather than being nulled.

---

## 9. HOW TO ADD A PERSISTED FIELD CORRECTLY

Follow all six steps. Skipping 3 or 4 produces exactly the §5.1 defect.

1. **Add the field to `GameState`, at the END**, with its fresh-game default as the initializer.
   Append-only so older saves stay loadable — `GameState.cs:530` is the current tail.
2. **Add the wire member to `SaveSchema.PersistedState`, at the END**, nullable, with
   `[JsonProperty("camelCaseKey")]` — `SaveSchema.cs:642` is the current tail.
   *(Plain-field record types like `PlacedStructureData` have no attributes: the field name IS the
   wire key, `PlacedStructureData.cs:40`.)*
3. **Add the `Snapshot()` line** — `GameStateService.cs:527` is the tail. Copy defensively for
   collections (`new List<T>(...)`, see `:518`).
4. **Add the `ApplyPersisted()` line** — `GameStateService.cs:618` is the tail. Guard with
   `if (p.X != null)` / `.HasValue` so an absent field keeps the SO default (`:536-538` explains the
   shallow-merge contract).
5. **Decide bump vs no-bump.**
   - *No bump* if the field is purely additive-default-on-read and its absent case equals the prior
     behaviour — the `barracksLevel`/`troopLevels`/`gearLevels` precedent (`SaveSchema.cs:602-626`).
   - *Bump* if existing saves need a seed, a transform, or if the semantics of an existing field
     change. Then: raise `SaveSchema.cs:41`, and **register a step keyed to the new version** in
     `SaveMigrator.Steps` (`SaveMigrator.cs:37-80`). Register it even when it is a documented no-op —
     the top step must equal `CurrentVersion` or the CORE_SAVE version triple breaks
     (`SaveMigrator.cs:485-487`, `:670-672`). Seed idempotently (`if (s.X == null)`) so the step
     never clobbers data a save already carries (`:500-501`).
6. **Clamp it if it is a number** — add a line to `SaveSchema.Validate` (`:691-867`). Any economy
   number MUST be `NonNegInt`; the paid basket is the worked example (`:889-893` — an unclamped
   negative would mint resources on cancel).
7. **Clear it in `ResetToNewGame`** (`GameStateService.cs:945-1073`) or add it to the documented
   carve-outs (`:928-931`). The reflection guard
   `Assets/Editor/Regression/ResetToNewGameFullClearRegression.cs:69-291` fails on the next
   unassigned field — do not defeat it.
8. **Update this map** — add the row to §3 with its `file:line`, add the ladder entry to §2, and if
   it lives in PlayerPrefs instead, add it to §6 **and** §7.2. Do not write the version number into
   any other doc.

**If the state belongs in PlayerPrefs instead of the save, know what you are giving up:** no version,
no migration, no validation, no HMAC, and no New Game reset unless you write one and wire it to a
real caller (§7.1 rows L and M are what "forgot to wire it" looks like).

---

## 10. LIMITS I DID NOT CROSS

- **Static verification only.** No Unity run, no gate, no headless capture — per the task
  constraints. Every claim is read at source; none is confirmed against a live save blob or a live
  Neon row. Per CLAUDE.md §12, the §8 mismatches are *located*, not *proven at runtime*: mismatch #1
  in particular should be confirmed by reading a real `player_data.schema_version` before anyone
  acts on it.
- **The live Neon database was not queried.** `api/schema.sql` is the declared schema; the deployed
  table may differ (the file itself records one such past drift at `:67-73`).
- **`.asset` files were not opened.** The claim that `PetData.damagePerLevel` is serialized on
  shipped assets is taken from the two source comments that assert it
  (`Assets/Data/PetData.cs:13-18`, `Assets/_Modules/Pets/Pet.cs:653-655`), not from the YAML.
- **"No reader" in §5.2 means no reader found** by a full `Assets/**/*.cs` grep excluding tests,
  editor regressions and debug overlays. Reflection-based or string-keyed access would not appear.
  `AdminOverlay` reaches Village types by reflection *by design* (CLAUDE.md §5), so reflection is a
  live pattern in this tree.
- **The PlayerPrefs census covers `Assets/` only.** Keys written by native plugins or by the
  Firebase/LevelPlay SDKs are out of scope.
