# Core State / Persistence / Routing — C# Port Spec (Week 1)

**Status:** Port specification for the Week-1 Core module. Read-only analysis of the
React v1 client at `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\`.
Authored per `docs/v2-unity-port-spec.md` Part 3 (the C# port table) and Part 5
(Week 1 build order). Default posture is **spec fidelity** — a literal translation
of the React behavior. Unity-idiomatic departures are confined to the final
"Improvement suggestions" section.

**Scope:** the `_Modules/Core/` deliverables —
`State/GameState.cs`, `State/GameStateService.cs`, `State/SaveSchema.cs`,
`State/SaveMigrator.cs`, `SceneRouter.cs`, `Theme/Theme.cs` + `Theme.uss`,
`Constants.cs`.

---

## 0. Source files analyzed

| File | Result |
| --- | --- |
| `src/store/gameStore.ts` | Read in full. **It is a thin re-export shim** (Phase-3 slicing) — the real store lives elsewhere; see below. |
| `src/store/saveSchema.ts` | Read in full. Zod validation schema for the persisted payload. |
| `src/App.tsx` | Read in full. React-Router route table + `AudioBootstrap`. |
| `src/lib/constants.ts` | Read in full. Four Solana address/mint constants. |
| `src/lib/themes.ts` | Read in full. Seven preset themes. |

Because `src/store/gameStore.ts` is only a re-export shim, the following
*authoritative* sources were also read to recover the persisted shape, the schema
version, and the migration chain (none of these are in the task's required-read
list — recording them here so the dependency is explicit):

- `src/state/gameStore.ts` — the real composition root: `CURRENT_SCHEMA_VERSION`,
  `migratePersistedState()`, the zustand `persist` config (`partialize`, `migrate`),
  and the `reset()` action.
- `src/state/gameState.ts` — the composed `GameState` type (intersection of 10 slices).
- `src/state/saveExport.ts` — the Save Export / Import surface (`SaveExport` file shape).
- `src/state/slices/{settings,player,tutorial,resources,pets,village,dungeons,regions,social,combat}Slice.ts`
  — per-field defaults, value ranges, and data shapes.

**No missing files.** Every file named in the task brief exists and was readable.

---

## 1. `gameStore.ts` → `State/GameState.cs` + `State/GameStateService.cs`

### 1.1 What the React store actually is

`src/store/gameStore.ts` re-exports `useGameStore` and a pile of slice types/constants.
The live store (`src/state/gameStore.ts`) is **one** Zustand store wrapped in **one**
`persist` middleware:

- `name: 'dotr-save'` — the localStorage key.
- `version: CURRENT_SCHEMA_VERSION` (= **10**).
- `migrate` — delegates to `migratePersistedState()`.
- `partialize` — selects the subset of state that is actually persisted.

State is a single **flat** object composed from ten feature slices
(`settings, tutorial, social, player, resources, pets, village, dungeons, regions,
combat`, plus a non-persisting `lighting` slice). Slices are a code-organization
device only — at runtime there is no namespace nesting. Components subscribe via
`useGameStore(selector)`.

### 1.2 Exported symbols (from the `gameStore.ts` shim)

The shim re-exports — types in *italics*, values in `code`:

- `useGameStore` (the hook)
- *MovementStyle*, *BreachStyle*, *Difficulty*; `DIFFICULTY_CONFIG`
- *TutorialStep*
- `ZERO_RESOURCES`, `canAffordCost`; *ResourceBalance*, *ResourceCost*, *WallCost*
- `xpForLevel`, `PET_MAX_BOND`, `PET_BOND_COSTS`
- `TOWER_SLOTS`, `TOWER_MAX_LEVEL`, `TOWER_TIER_COSTS`, `WALL_MAX_LEVEL`,
  `WALL_UPGRADE_COSTS`, `RESOURCE_BUILDINGS`; *BuildingYield*, *PendingTowerBuild*
- *DungeonRunResult*, *ActiveDungeonRun*, *DungeonProgress*, *QuestProgress*
- *RegionProgress*, *RegionsSlice*
- *AtbInventory*, *AtbItemKind*, *AtbBattleResult*
- `buildSaveExport`, `applySaveImport`, `downloadSaveExport`; *SaveExport*, *SaveImportResult*

For Week 1, the Core module needs only the **persisted-state** subset. Slice
*action* signatures and constants like `TOWER_TIER_COSTS` belong to Village /
Combat / Pets modules (Weeks 2-4) and are out of scope here — but the persisted
*fields* they own must still round-trip through Core's save layer, so they are
included in `GameState.cs` as plain serialized fields.

### 1.3 Persisted state — every field (the `partialize` output)

`partialize` (and the identical `snapshotPersistedState()` in `saveExport.ts`)
select exactly **38 persisted fields**. This is the precise contract `GameState.cs`
must serialize. Fields NOT in this list (`prepTimerLocked`, `paused`,
`dungeonEnteredAt`, `activeRegionRun`, `torchBurnEndsAt`, and all slice action
closures) are transient/runtime and are **not** persisted.

| # | Field | TS type | C# type | Fresh default | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | `pets` | `Pet[]` | `List<PetData>` | `[]` | owned pet roster |
| 2 | `starterPetId` | `string \| null` | `string` (nullable) | `null` | |
| 3 | `onboarded` | `boolean` | `bool` | `false` | |
| 4 | `bestWave` | `number` | `int` | `0` | clamped ≥0 |
| 5 | `resources` | `ResourceBalance` | `ResourceBalance` struct | `{crystals:250, food:80, coins:15}` | `STARTER_RESOURCES` |
| 6 | `ownedItemIds` | `string[]` | `List<string>` | `[]` | |
| 7 | `petBonds` | `number[]` | `List<int>` | `[0,0,0]` | `[Aether, Flame, Ice]` |
| 8 | `voidshards` | `number` | `int` | `5` | clamped ≥0 |
| 9 | `towers` | `number[]` | `List<int>` | `[0]×9` | length = `TOWER_SLOTS` (9) |
| 10 | `towerAbilities` | `number[]` | `List<int>` | `[0]×9` | length = 9 |
| 11 | `wallLevel` | `number` | `int` | `0` | 0..`WALL_MAX_LEVEL` (3) |
| 12 | `stone` | `number` | `int` | `20` | clamped ≥0 |
| 13 | `iron` | `number` | `int` | `5` | clamped ≥0 |
| 14 | `wood` | `number` | `int` | `15` | clamped ≥0 |
| 15 | `buildingCooldowns` | `Record<string,number>` | `Dictionary<string,double>` | `{}` | ms timestamps |
| 16 | `pendingBuilds` | `PendingTowerBuild[]` | `List<PendingTowerBuild>` | `[]` | `{slot,ability,finishAt}` |
| 17 | `tutorialStep` | `1..7 \| 'done'` | `TutorialStep` enum | `1` (fresh) / `Done` (migrated) | see §1.5 |
| 18 | `joystickSensitivity` | `number` | `float` | `1` | clamped 0.3..1.5 by setter |
| 19 | `movementStyle` | `'auto'\|'joystick'\|'tap'\|'both'` | `MovementStyle` enum | `Auto` | |
| 20 | `muted` | `boolean` | `bool` | `true` | a11y: fresh visitor muted (T24) |
| 21 | `musicVolume` | `number` | `float` | `70` | range 0..100 |
| 22 | `sfxVolume` | `number` | `float` | `80` | range 0..100 |
| 23 | `difficulty` | `'easy'\|'normal'\|'hard'` | `Difficulty` enum | `Normal` | |
| 24 | `voiceOvers` | `boolean` | `bool` | `false` | stored, not yet wired |
| 25 | `ownedPets` | `PetSpecies[]` | `List<PetSpecies>` enum | `[]` | |
| 26 | `seenTutorials` | `Record<string,boolean>` | `Dictionary<string,bool>` | `{}` | one-shot tutorial flags |
| 27 | `boundWallet` | `string \| null` | `string` (nullable) | `null` | wallet save is tagged to |
| 28 | `heroClass` | `'mage'\|'knight'\|'ranger'\| null` | `HeroClass?` enum | `null` | |
| 29 | `inventory` | `AtbInventory` | `AtbInventory` struct | `{potions:0, manaCrystals:0, cleanses:0}` | `torches` optional, defaults 0 |
| 30 | `atbLossStreak` | `number` | `int` | `0` | clamped ≥0 |
| 31 | `breachStyle` | `'ask'\|'atb'\|'tower-sim'` | `BreachStyle` enum | `Ask` | survives New Game |
| 32 | `buildingDamage` | `Record<string,number>` | `Dictionary<string,double>` | `{}` | keys incl. `gate-0..gate-3`, `HEART_DAMAGE_ID` |
| 33 | `dungeons` | `DungeonProgress` | `DungeonProgress` class | `emptyDungeonProgress()` | see §1.4 |
| 34 | `activeDungeonRun` | `ActiveDungeonRun \| null` | `ActiveDungeonRun` (nullable) | `null` | mid-run dungeon save |
| 35 | `quests` | `QuestProgress` | `QuestProgress` class | `emptyQuestProgress()` | see §1.4 |
| 36 | `regions` | `RegionProgress` | `RegionProgress` class | `{discovered:{}, cleared:{}}` | Realm Map ledger |
| 37 | `myInviteCode` | `string` | `string` | `generateInviteCode(null)` | 6-char chat code |
| 38 | `contacts` | `ChatContact[]` | `List<ChatContact>` | `[]` | |
| 38a | `blockedCodes` | `string[]` | `List<string>` | `[]` | (part of social — counted within #38 group below) |
| 38b | `inbox` | `ChatMessage[]` | `List<ChatMessage>` | `[]` | |
| 38c | `lastInboxSyncAt` | `number` | `double` | `0` | unix ms |

> **Exact count.** `partialize` lists **41** key/value pairs. Three of them
> (`blockedCodes`, `inbox`, `lastInboxSyncAt`) are folded into the row #38 group
> above for readability. The literal persisted-field count is **41**. The save
> layer must serialize all 41.

### 1.4 Nested data shapes

```
PetData (mirrors Pet in gameDesign.ts)
  id              : string
  ownerId         : string                 // always "local-player"
  species         : PetSpecies enum         // aether-sprite | flame-pup | ice-wolf
  nickname        : string?  (optional)
  level           : int  (>=0)
  xp              : int  (>=0)
  unlockedSkillIds: List<string>
  equippedActiveIds: List<string>

ResourceBalance        { crystals:int>=0, food:int>=0, coins:int>=0 }
PendingTowerBuild      { slot:int>=0, ability:int>=0, finishAt:double }
AtbInventory           { potions:int>=0, manaCrystals:int>=0, cleanses:int>=0, torches:int>=0 (optional) }
ChatContact            { code:string, nickname:string? }
ChatMessage            { id, senderCode, recipientCode, phraseId:string,
                         sentAt:double, readAt:double? (nullable) }

DungeonProgress
  discovered        : Dictionary<string,bool>   // value always true
  cleared           : Dictionary<string,int>    // clear count
  bestTime          : Dictionary<string,double> // fastest clear, seconds
  noHitClear        : Dictionary<string,bool>   // value always true
  deathsByDungeon   : Dictionary<string,int>?   (optional, defaults {})
  loreReadByDungeon : Dictionary<string,Dictionary<string,bool>>? (optional, defaults {})

QuestProgress
  active    : Dictionary<string, QuestState>    // QuestState { beatIndex:int, flags:Dict<string,bool> }
  completed : Dictionary<string,bool>
  available : Dictionary<string,bool>

ActiveDungeonRun
  dungeonId         : string
  avatarNodeId      : string
  visitedNodes      : List<string>
  clearedEncounters : List<string>
  openedChests      : List<string>
  readLore          : List<string>
  loot              : LootStash
  startedAt         : double

LootStash
  crystals, food, coins, stone, iron, wood : int >= 0
  petBondShards : Dictionary<string,double>
  skillPoints   : Dictionary<string,double>

RegionProgress
  discovered : Dictionary<string,bool>   // value always true
  cleared    : Dictionary<string,bool>   // value always true
```

### 1.5 Enums

| TS union | C# enum | Members | JSON serialized as |
| --- | --- | --- | --- |
| `Difficulty` | `Difficulty` | Easy, Normal, Hard | `"easy"`, `"normal"`, `"hard"` |
| `MovementStyle` | `MovementStyle` | Auto, Joystick, Tap, Both | `"auto"`, `"joystick"`, `"tap"`, `"both"` |
| `BreachStyle` | `BreachStyle` | Ask, Atb, TowerSim | `"ask"`, `"atb"`, `"tower-sim"` |
| `HeroClass` | `HeroClass` | Mage, Knight, Ranger | `"mage"`, `"knight"`, `"ranger"` |
| `PetSpecies` | `PetSpecies` | AetherSprite, FlamePup, IceWolf | `"aether-sprite"`, `"flame-pup"`, `"ice-wolf"` |
| `TutorialStep` | `TutorialStep` | Step1..Step7, Done | numbers `1`..`7` or string `"done"` |

`TutorialStep` is the awkward one — a `1..7 | 'done'` union. The JSON value is a
**number for steps and a string for done**. Port as an enum
`{ Step1=1,...,Step7=7, Done=99 }` with a custom `JsonConverter` that writes
`1..7` as raw numbers and `Done` as the literal string `"done"`, and reads either
form back. `tutorialStep` and `seenTutorials` are owned by `tutorialSlice`;
the `advanceTutorial()` action caps at `Done`.

> **Serialization rule:** every string-union enum must serialize to the **exact**
> lowercase/kebab string the React save uses, because `data/*.json` and any
> imported `.json` save are cross-engine. Use Newtonsoft `StringEnumConverter`
> with explicit `[EnumMember(Value="…")]` (or a custom converter for the
> non-trivial cases above). `JsonUtility` cannot do this — Newtonsoft is required
> (already a Part-2 package decision).

### 1.6 Proposed C# design

**`State/GameState.cs`** — a `ScriptableObject` (`[CreateAssetMenu]`) that holds
exactly the 41 persisted fields above as serialized fields, plus `SchemaVersion`.
It is a pure data container — **no logic**, no events. One asset instance lives in
`Assets/_Modules/Core/State/GameState.asset` and is the in-memory live state. The
SO is the Unity analog of the Zustand store's *data*; mutators and subscribers
live in the service (next).

```csharp
[CreateAssetMenu(menuName = "Defenders/Core/Game State")]
public sealed class GameState : ScriptableObject
{
    public int SchemaVersion = SaveSchema.CurrentVersion;   // = 10

    // ── Player ──
    public bool Onboarded;
    public int BestWave;
    public HeroClassOpt HeroClass = HeroClassOpt.None;       // nullable enum wrapper
    public string BoundWallet;                               // null when unbound

    // ── Resources ──
    public ResourceBalance Resources = ResourceBalance.Starter;
    public int Voidshards = 5;
    public int Stone = 20, Iron = 5, Wood = 15;
    public List<string> OwnedItemIds = new();

    // ── Pets ──
    public List<PetData> Pets = new();
    public string StarterPetId;
    public List<int> PetBonds = new() { 0, 0, 0 };
    public List<PetSpecies> OwnedPets = new();

    // ── Village ──
    public List<int> Towers = NewZeroed(Constants.TowerSlots);          // 9
    public List<int> TowerAbilities = NewZeroed(Constants.TowerSlots);  // 9
    public int WallLevel;
    public SerializableDict<string, double> BuildingCooldowns = new();
    public List<PendingTowerBuild> PendingBuilds = new();
    public SerializableDict<string, double> BuildingDamage = new();

    // ── Settings ──
    public float JoystickSensitivity = 1f;
    public MovementStyle MovementStyle = MovementStyle.Auto;
    public BreachStyle BreachStyle = BreachStyle.Ask;
    public bool Muted = true;
    public float MusicVolume = 70f, SfxVolume = 80f;
    public Difficulty Difficulty = Difficulty.Normal;
    public bool VoiceOvers;

    // ── Tutorial ──
    public TutorialStep TutorialStep = TutorialStep.Step1;
    public SerializableDict<string, bool> SeenTutorials = new();

    // ── ATB / combat ──
    public AtbInventory Inventory = AtbInventory.Empty;
    public int AtbLossStreak;

    // ── Dungeons / quests / regions ──
    public DungeonProgress Dungeons = DungeonProgress.Empty();
    public ActiveDungeonRun ActiveDungeonRun;                // null when no run
    public QuestProgress Quests = QuestProgress.Empty();
    public RegionProgress Regions = RegionProgress.Empty();

    // ── Social ──
    public string MyInviteCode;
    public List<ChatContact> Contacts = new();
    public List<string> BlockedCodes = new();
    public List<ChatMessage> Inbox = new();
    public double LastInboxSyncAt;
}
```

> Unity's `JsonUtility`/SO inspector cannot serialize `Dictionary<,>`. Use
> Newtonsoft for the **save file** (it handles dictionaries natively) and a
> `SerializableDict<TK,TV>` helper (parallel key/value `List`s + `ISerializationCallbackReceiver`)
> only if the live SO must also survive a domain reload in the editor. Newtonsoft
> is the path of least resistance — the save file is JSON anyway.

**`State/GameStateService.cs`** — the behavior layer. Mirrors Zustand's
"subscribe" + the persist middleware. Recommended as a plain C# singleton (or a
`Core` bootstrap MonoBehaviour) holding a reference to the `GameState` SO.

Responsibilities:

- **Load** on boot: read `PlayerPrefs` key `dotr-save` → JSON string → if present,
  `SaveMigrator.Migrate()` → deserialize into the SO. If absent, the SO keeps its
  fresh defaults (= a brand-new game).
- **Mutators** — typed methods that map 1:1 to the Zustand slice actions the Core
  module needs in Week 1 (`FinishOnboarding()`, `RecordRun(int)`, `BindWallet(string)`,
  `ChooseHero(HeroClass)`, `SetMuted(bool)`, `AdvanceTutorial()`, `Reset()` …).
  Each mutator (a) writes the SO, (b) raises the change event, (c) calls `Save()`
  (debounced).
- **`Save()`** — Newtonsoft-serialize the SO's 41 persisted fields to JSON, write
  `PlayerPrefs.SetString("dotr-save", json)` then `PlayerPrefs.Save()`. This is
  the literal analog of Zustand `persist` writing to localStorage.
- **Events** — a `GameStateChanged` `UnityEvent` (or per-domain events:
  `ResourcesChanged`, `WaveRecorded`, …). HUD/UI subscribes in `OnEnable`,
  unsubscribes in `OnDisable`. This is the Part-3 "ScriptableObject + UnityEvent"
  pattern.
- **`Reset()`** — wipes progression exactly like the React `reset()` action:
  pets `[]`, `onboarded=false`, `bestWave=0`, resources back to `STARTER_RESOURCES`,
  `petBonds=[0,0,0]`, `voidshards=5`, towers/towerAbilities all-zero (×9),
  `wallLevel=0`, `stone=20`, `iron=5`, `wood=15`, `tutorialStep=1`,
  `dungeons/quests/regions` back to empty, `activeDungeonRun=null`,
  `inventory={0,0,0}`, `heroClass=null`. **Does NOT** clear `boundWallet` and
  **does NOT** clear `breachStyle` (preferences survive a New Game) — replicate
  this carve-out exactly. Social fields (`myInviteCode`, `contacts`, etc.) are
  also never touched by `reset()`.

`reset()` in React also clears purely-transient fields (`prepTimerLocked`,
`paused`, `dungeonEnteredAt`, `torchBurnEndsAt`) — in Unity those live in runtime
SOs (`ATBRuntimeState`, `DungeonRuntimeState`, a future `LightingRuntime`), not in
`GameState`, so Core's `Reset()` simply omits them.

---

## 2. `saveSchema.ts` → `State/SaveSchema.cs` + `State/SaveMigrator.cs`

### 2.1 Exported symbols (`saveSchema.ts`)

- `persistedStateSchema` — a `z.object({...}).partial()` Zod schema. Every field is
  `.optional()` (partial save tolerance) but each present field is **strictly typed**.
- `PersistedStateInput` — `z.infer` of the above; the validated, type-safe shape.

Two private Zod helpers define the numeric clamping rules the C# validator must
reproduce:

- **`nonNegInt`** — must be finite; transforms to `Math.max(0, Math.floor(n))`.
  → C#: reject `NaN`/`±Infinity`, then `Mathf.Max(0, (int)Math.Floor(n))`.
- **`finiteInt`** — must be finite; transforms to `Math.floor(n)` (may be negative).
  → C#: reject `NaN`/`±Infinity`, then `(int)Math.Floor(n)`.

### 2.2 The exact persisted JSON shape

The save **file** is the `SaveExport` envelope (`saveExport.ts`), and inside it the
`state` payload is the 41-field persisted object validated by `persistedStateSchema`.

```jsonc
// SaveExport envelope (the downloadable / importable file)
{
  "format": 1,                       // file format version (NOT the schema version)
  "storeVersion": 10,                // mirrors CURRENT_SCHEMA_VERSION
  "exportedAt": "2026-05-18T12:34:56.000Z",   // ISO-8601
  "wallet": "Bw...JV" | null,        // wallet the save is tagged to
  "state": { /* the 41 persisted fields below */ }
}
```

```jsonc
// SaveExport.state — the persistedStateSchema payload
{
  "pets": [
    { "id": "…", "ownerId": "local-player", "species": "flame-pup",
      "nickname": "…", "level": 3, "xp": 120,
      "unlockedSkillIds": ["…"], "equippedActiveIds": ["…"] }
  ],
  "starterPetId": "…" | null,
  "onboarded": true,
  "bestWave": 7,
  "resources": { "crystals": 250, "food": 80, "coins": 15 },
  "ownedItemIds": ["…"],
  "petBonds": [0, 0, 0],
  "voidshards": 5,
  "towers": [0,0,0,0,0,0,0,0,0],
  "towerAbilities": [0,0,0,0,0,0,0,0,0],
  "wallLevel": 0,
  "stone": 20, "iron": 5, "wood": 15,
  "buildingCooldowns": { "crystal-mine": 1716000000000 },
  "pendingBuilds": [ { "slot": 0, "ability": 2, "finishAt": 1716000300000 } ],
  "tutorialStep": 1,                 // number 1..7, OR the string "done"
  "joystickSensitivity": 1,
  "movementStyle": "auto",
  "muted": true,
  "musicVolume": 70,
  "sfxVolume": 80,
  "difficulty": "normal",
  "voiceOvers": false,
  "ownedPets": ["aether-sprite"],
  "seenTutorials": { "firstVillageEntry": true },
  "boundWallet": "…" | null,
  "heroClass": "mage" | null,
  "inventory": { "potions": 0, "manaCrystals": 0, "cleanses": 0, "torches": 0 },
  "atbLossStreak": 0,
  "breachStyle": "ask",
  "buildingDamage": { "gate-2": 40, "heart": 0 },
  "dungeons": {
    "discovered": { "healers_cottage": true },
    "cleared": { "healers_cottage": 2 },
    "bestTime": { "healers_cottage": 184.5 },
    "noHitClear": { "healers_cottage": true },
    "deathsByDungeon": { "healers_cottage": 1 },        // optional
    "loreReadByDungeon": { "healers_cottage": { "lore-1": true } }  // optional
  },
  "activeDungeonRun": null,          // or the ActiveDungeonRun object (§1.4)
  "quests": { "active": {}, "completed": {}, "available": {} },
  "regions": { "discovered": {}, "cleared": {} },
  "myInviteCode": "ABC123",
  "contacts": [ { "code": "XYZ789", "nickname": "…" } ],
  "blockedCodes": ["…"],
  "inbox": [
    { "id": "…", "senderCode": "…", "recipientCode": "…",
      "phraseId": "…", "sentAt": 1716000000000, "readAt": null }
  ],
  "lastInboxSyncAt": 1716000000000
}
```

**localStorage form (the live save).** Zustand `persist` writes the SAME `state`
payload to localStorage key `dotr-save`, wrapped in its own envelope:
`{ "state": { …41 fields… }, "version": 10 }`. In Unity, `PlayerPrefs` key
`dotr-save` should hold the equivalent — recommend storing the **`SaveExport`
envelope** for one consistent shape across live-save and exported-file (the
`wallet`/`exportedAt` fields are harmless extra metadata on the live save).

### 2.3 Current schema version

**`CURRENT_SCHEMA_VERSION = 10`** (`src/state/gameStore.ts` line 44). This is the
single source of truth — `persist.version`, `SaveExport.storeVersion`, and the
migrate target all read it. Port as `SaveSchema.CurrentVersion = 10`.

The `SaveExport` file format also has its own separate version: **`format: 1`**
(bumped only if the *envelope* shape changes, independently of `storeVersion`).

### 2.4 The migration chain — every step

`migratePersistedState(persisted, fromVersion)` is one shared chain used by **both**
the localStorage rehydrator AND `applySaveImport` (migrate-on-import). Every step is
**additive** — it seeds new fields with empty defaults and never mutates data an
in-progress save already carries. Steps are cumulative `if (fromVersion < N)` blocks
(an ancient save runs every step in order). **Nine migration steps** (v1→v2 through
v9→v10):

| Step | Guard | What it does |
| --- | --- | --- |
| **v1→v2** | `fromVersion < 2` | Seed `resources = STARTER_RESOURCES` ({crystals:250, food:80, coins:15}) and `ownedItemIds = []`. |
| **v2→v3** | `fromVersion < 3` | Seed `heroClass = state.heroClass ?? 'mage'` — pre-hero-select saves default to Mage so their existing ability set/model stay valid. |
| **v3→v4** | `fromVersion < 4` | Seed `wood ?? 15`, `buildingCooldowns ?? {}`, `tutorialStep ?? 'done'` (an in-progress save skips the first-time tutorial). |
| **v4→v5** | `fromVersion < 5` | Seed `towerAbilities ?? Array(TOWER_SLOTS).fill(0)` — a v4 save with a built tower would crash on render without it. |
| **v5→v6** | `fromVersion < 6` | Seed the whole ATB+dungeon block: `inventory ?? {potions:0,manaCrystals:0,cleanses:0}`, `atbLossStreak ?? 0`, `prepTimerLocked ?? false`, `breachStyle ?? 'ask'`, `buildingDamage ?? {}`, `dungeons ?? emptyDungeonProgress()`, `activeDungeonRun ?? null`, `quests ?? emptyQuestProgress()`. |
| **v6→v7** | `fromVersion < 7` | Non-destructively merge the starter dungeon `healers_cottage` into `dungeons.discovered` (so the Dungeon Select screen always has ≥1 entry). Also **force** `prepTimerLocked = false` — a v6 save written while locked still carries `true` and would soft-lock the prep timer. |
| **v7→v8** | `fromVersion < 8` | Four-cardinal-gates rename: the south gate's id moved `gate-0` → `gate-2`. If `buildingDamage` has a `gate-0` key, copy its value to `gate-2` and delete `gate-0`. Wrapped in try/catch — on failure, drop the orphan `gate-0` (worst case the south gate loads at 0 damage). |
| **v8→v9** | `fromVersion < 9` | Two additions: (1) seed `pendingBuilds ?? []`. (2) Migrate audio/difficulty prefs out of the **legacy standalone** localStorage store keyed `realm-defenders-settings`: read+JSON.parse that key, then set `muted/musicVolume/sfxVolume/difficulty/voiceOvers` via `state.<f> ?? legacy.<f> ?? <default>` (defaults: `muted=false`, `musicVolume=70`, `sfxVolume=80`, `difficulty='normal'`, `voiceOvers=false`). Then `localStorage.removeItem('realm-defenders-settings')`. Note: existing v8 players fall back to `muted=false` — only brand-new players get muted-by-default. |
| **v9→v10** | `fromVersion < 10` | Seed the Realm Map: `regions ?? emptyRegionProgress()` ({discovered:{}, cleared:{}}) and `activeRegionRun ?? null`. Purely additive. |

> **No save-schema bump for some additions.** Three fields were added *without* a
> version bump because they ride inside an existing persisted object and an old
> save simply omits the key (the reader null-coalesces): `inventory.torches`,
> `dungeons.deathsByDungeon`, `dungeons.loreReadByDungeon`. The C# schema must
> treat all three as **optional**, default-on-read.

### 2.5 Proposed C# design

**`State/SaveSchema.cs`** — the strongly-typed save shape + validation, the C#
analog of the Zod schema:

- `public const int CurrentVersion = 10;`
- `public const int FileFormat = 1;`
- `public const string PlayerPrefsKey = "dotr-save";`
- `public const string LegacySettingsKey = "realm-defenders-settings";` (for the v8→v9 step)
- A `SaveFile` record/class = the `SaveExport` envelope
  (`Format`, `StoreVersion`, `ExportedAt`, `Wallet`, `State`).
- A `PersistedState` record = the 41-field payload (every field nullable so a
  partial save deserializes, mirroring `.partial()`).
- `static SaveValidationResult Validate(PersistedState raw)` — the C# port of
  `safeParse`: reject `NaN`/`Infinity` numerics, clamp via `NonNegInt`/`FiniteInt`
  rules (§2.1), reject enum values outside their allowed set. On a type error,
  return a failure carrying the first bad field path (mirrors the React
  `invalid field: <path>` toast). Newtonsoft naturally drops unknown extra keys,
  matching `.partial()`'s tolerance of stale fields like `prepTimerLocked`.
- Numeric clamps to enforce on present fields (mirror the Zod schema): every
  resource (`crystals/food/coins`), `voidshards`, `stone/iron/wood`, `bestWave`,
  `atbLossStreak`, all `petBonds[]` entries, `pet.level`, `pet.xp`,
  `inventory.*`, `pendingBuild.slot/ability`, all `LootStash` int fields → `nonNegInt`.
  `chatMessage.sentAt`, `chatMessage.readAt`, `pendingBuild.finishAt`,
  `lastInboxSyncAt` → `finiteInt`. `musicVolume`/`sfxVolume`/`joystickSensitivity`
  → finite-only (not clamped on load; the React schema only rejects NaN/Infinity).

**`State/SaveMigrator.cs`** — the C# port of `migratePersistedState`. One public
entry point used by **both** the boot loader and a future Save-Import path:

```csharp
public static class SaveMigrator
{
    /// Migrates a parsed PersistedState from `fromVersion` up to CurrentVersion.
    /// Additive only — never mutates data the save already carries.
    public static PersistedState Migrate(PersistedState state, int fromVersion);
}
```

Implement the nine steps as cumulative `if (fromVersion < N)` blocks, in order,
each calling a private `MigrateToVN(state)` so the chain reads like the React
source. Notes for fidelity:

- **v6→v7** must *force* `prepTimerLocked = false` (Unity equivalent: don't carry
  it at all — it's runtime-only) and merge the starter dungeon.
- **v7→v8** must wrap the `gate-0`→`gate-2` rename in try/catch with the orphan-drop
  fallback.
- **v8→v9** reads the legacy `PlayerPrefs` key `realm-defenders-settings`
  (the localStorage analog), then `PlayerPrefs.DeleteKey(...)` it.
- **Version gate** (from `applySaveImport`): a save with
  `storeVersion > CurrentVersion` is **rejected** (no forward migration); a
  non-numeric/`NaN` version is rejected; an older version is migrated; an equal
  version is a no-op pass-through. Replicate this gate when import is implemented
  (Week 1 only needs load/save round-trip, but the migrator should expose it).

**Storage layer.** `PlayerPrefs` replaces `localStorage`. One key, `dotr-save`,
holding the JSON string. Caveat to log as a decision: `PlayerPrefs` on Windows is
the registry and has a practical per-string size ceiling — a large `inbox`/`pets`
save could approach it. For Week 1 this is fine; flag a future move to a
`Application.persistentDataPath` JSON file if saves grow (see Improvement #4).

---

## 3. `App.tsx` (router) → `SceneRouter.cs`

### 3.1 What `App.tsx` exports and does

`App.tsx` exports a single default `App` component. It is **not** state — it is the
React-Router route table plus an `AudioBootstrap` side-effect component. Relevant
to the port:

- The route table maps URL paths to lazily-loaded page components.
- `AudioBootstrap` is a route→music director: village routes own their own music,
  every other route plays the `title` track; first user gesture unlocks audio;
  syncs the persisted `muted` pref into `audioManager`.

### 3.2 The React routes

| Path | Component | Unity scene equivalent |
| --- | --- | --- |
| `/` | `LandingPage` | `Title` |
| `/onboarding` | `OnboardingPage` | (Onboarding flow within `Title` / a dedicated scene — Week 1 Onboarding module) |
| `/village` | `Village3D` (ErrorBoundary) | `Village` |
| `/house` | `VillagePage` | (deferred — not a Week-1 scene) |
| `/store` | `StorePage` | (Week 7) |
| `/enemy-codex` | `EnemyCodex` | (deferred) |
| `/dungeons` | `DungeonSelectScreen` | (UI within Village or a select scene) |
| `/dungeon/:id` | `DungeonScene3D` / `DungeonExplorer` | `Dungeon_HealersCottage` (and future `Dungeon_X`) |
| `/realm` | `RealmMapScreen` | (deferred) |
| `/region/:id` | `RegionScene` | (deferred) |
| `/showcase` | `AnimationShowcase` | (dev-only, skip) |
| `*` | `NotFoundPage` | n/a — Unity has no unmatched-route case |

`AtbBattleHost` is mounted *globally* (over any route) rather than being its own
route — in Unity the ATB battle is its own scene (`ATBBattle`) loaded additively
or as a transition by the breach handler. Per the Part-3 table, the four canonical
Unity scenes are **`Title`, `Village`, `Dungeon_HealersCottage`, `ATBBattle`**.

### 3.3 Proposed C# design

**`SceneRouter.cs`** — a `static` class. The Unity analog of React-Router's
`<Routes>` / `useNavigate`.

```csharp
public static class SceneRouter
{
    public const string Title   = "Title";
    public const string Village = "Village";
    public const string ATBBattle = "ATBBattle";
    public static string Dungeon(string id) => $"Dungeon_{id}";
    // Week 1 ships Dungeon_HealersCottage.

    public static void LoadScene(string sceneName);
    public static UniTask LoadSceneWithFade(string sceneName, float fadeSeconds = 0.4f);

    // Optional typed entry points mirroring the React routes:
    public static void GoTitle();
    public static void GoVillage();
    public static UniTask GoDungeon(string dungeonId);   // Dungeon_HealersCottage
    public static UniTask GoBattle(BattleParams p);      // ATBBattle, params handed off
}
```

- `LoadScene(string)` — thin wrapper over `SceneManager.LoadScene` (or async).
- `LoadSceneWithFade()` — `async UniTask` (Part-3 mandates `UniTask`, never
  `async void`): fade a full-screen black overlay → `SceneManager.LoadSceneAsync` →
  fade back in. The overlay is a tiny persistent UI Toolkit element or a
  `DontDestroyOnLoad` canvas owned by the Core bootstrap.
- **Scene-transition music** is the `AudioBootstrap` port: a `Core` audio director
  (likely in the Audio/Core service, not the router itself) reacts to scene loads —
  `Village` owns village BGM, every other scene gets the `title` track. Keep that
  concern *out* of `SceneRouter` (router loads scenes; audio director listens) to
  preserve module separation. For Week 1 the router only needs `Title ↔ Village`.
- No catch-all: Unity scene names are compile-time; an unknown name is a
  programmer error, not a user-facing 404. `LoadScene` should `Debug.LogError` on
  an unregistered name rather than silently failing.
- `BattleParams` (hero, pets, breaching enemies) is handed to the `ATBBattle`
  scene via a runtime ScriptableObject or a static handoff field — this is the
  React `AtbBattleHost` "battle is active" signal. Detailed in the Week-2 BattleATB
  spec; `SceneRouter.GoBattle` just needs the signature stub now.

---

## 4. `themes.ts` → `Theme/Theme.cs` + `Theme.uss`

### 4.1 Exported symbols (`themes.ts`)

- *`ThemeConfig`* — interface: `{ name, description, colors: Record<string,string>,
  font: { url, family }, radius }`.
- `themes` — `Record<string, ThemeConfig>`; **7** presets.
- `DEFAULT_THEME` — `'midnight-luxe'`.
- `themeNames` — `Object.keys(themes)` (the 7 keys).

Each theme defines **36 color tokens** as **HSL triples** (`"H S% L%"` — the
shadcn/CSS-variable convention, *not* hex). The seven theme keys:
`midnight-luxe`, `neon-brutalist`, `warm-earth`, `cyberpunk`, `soft-pastels`,
`arctic-clean`, `sunset-warm`.

### 4.2 Concrete color values — the default theme (`midnight-luxe`)

The task asks for concrete hex. React stores HSL triples and CSS converts them;
below is `midnight-luxe` (the `DEFAULT_THEME`) with hex computed from the HSL.
**Source of truth remains the HSL triple** — port HSL verbatim and let USS/C#
convert, so a future theme edit in `data/` stays lossless.

| Token | HSL (verbatim) | Hex (computed) |
| --- | --- | --- |
| `background` | `222 47% 7%` | `#0A0E1A` |
| `foreground` | `45 30% 90%` | `#EAE4D4` |
| `card` | `222 40% 10%` | `#0F1421` |
| `popover` | `222 40% 10%` | `#0F1421` |
| `primary` | `45 70% 50%` | `#D9A726` |
| `primary-foreground` | `222 47% 7%` | `#0A0E1A` |
| `secondary` | `222 30% 15%` | `#1A2030` |
| `muted` | `222 25% 14%` | `#1A1F2C` |
| `muted-foreground` | `222 15% 55%` | `#7A8499` |
| `accent` | `45 70% 50%` | `#D9A726` |
| `destructive` | `0 72% 51%` | `#DC2828` |
| `border` | `222 20% 18%` | `#252C3B` |
| `input` | `222 20% 18%` | `#252C3B` |
| `ring` | `45 70% 50%` | `#D9A726` |
| `link` | `45 60% 60%` | `#D9B459` |
| `link-hover` | `45 70% 70%` | `#E6CB7A` |
| `button` | `222 30% 15%` | `#1A2030` |
| `button-foreground` | `45 30% 90%` | `#EAE4D4` |

> The gold `primary` `#D9A726` is the brand accent (the "gold accents" of Midnight
> Luxe). It is **not** the Heart's violet — the v2 spec Part-5 Week-3 note cites a
> Heart emissive of `#7C3AED`-ish; that violet is NOT in `themes.ts` (it lives in
> the Heart shader / `data/heart.json`). Flag: do not source the Heart color from
> `Theme.cs`.

For completeness the other six themes' `primary` accents (HSL → hex):
`neon-brutalist` `142 100% 50%` → `#00FF55` · `warm-earth` `145 25% 40%` → `#4D8066` ·
`cyberpunk` `180 100% 50%` → `#00FFFF` · `soft-pastels` `330 40% 65%` → `#CC78A0` ·
`arctic-clean` `215 80% 50%` → `#1A75E6` · `sunset-warm` `15 80% 55%` → `#E85426`.
The full 36-token tables for all 7 themes are in `themes.ts` lines 19-390 and
should be transcribed verbatim into the canonical data file (see Improvement #2).

### 4.3 Fonts

| Theme | Font family | Google Fonts |
| --- | --- | --- |
| `midnight-luxe` (default) | `Playfair Display`, serif | Playfair Display 400..900 |
| `neon-brutalist` | `Space Mono`, monospace | Space Mono 400/700 |
| `warm-earth` | `Fraunces`, serif | Fraunces variable |
| `cyberpunk` | `Sora`, sans-serif | Sora 100..800 |
| `soft-pastels` | `Outfit`, sans-serif | Outfit 100..900 |
| `arctic-clean` | `Hanken Grotesk`, sans-serif | Hanken Grotesk variable |
| `sunset-warm` | `DM Serif Display`, serif | DM Serif Display |

`radius` per theme: midnight-luxe `0.5rem`, neon-brutalist `0px`,
warm-earth `0.75rem`, cyberpunk `0.25rem`, soft-pastels `1rem`,
arctic-clean `0.375rem`, sunset-warm `0.625rem`.

> Fonts are remote Google Fonts URLs in React. Unity cannot fetch a webfont CSS at
> runtime cheaply — the Week-1 deliverable should **import the default font
> (`Playfair Display`) as a local font asset** (a `FontAsset` for UI Toolkit) and
> defer the other six. Owner-supplied font files preferred; if absent, flag in the
> decisions log (per Part 10 — do not silently substitute a different typeface).

### 4.4 Proposed C# design

**`Theme/Theme.cs`** — a `static` class exposing the **default theme** as typed
`Color` properties (`Theme.Background`, `Theme.Primary`, `Theme.Foreground`, …),
parsed once from the canonical HSL triples. Keep an `HslToColor(string)` helper so
the values come from data, not hardcoded hex. For Week 1 only `midnight-luxe`
needs to be live; expose the others behind a `Theme.Apply(string themeKey)` for
later (the React `useTheme` hook's job).

**`Theme/Theme.uss`** — a USS file declaring the 36 tokens as USS custom
properties on `:root`, so UI Toolkit documents reference `var(--primary)` etc.,
exactly as React components reference `hsl(var(--primary))`. USS supports
`rgb()`/hex; convert each HSL triple to hex (USS has no `hsl()` function). The
HUD module (Week 3-4) consumes this file. Example:

```css
:root {
    --background: #0A0E1A;
    --foreground: #EAE4D4;
    --primary:    #D9A726;
    --primary-foreground: #0A0E1A;
    --destructive: #DC2828;
    --border: #252C3B;
    --radius: 8px;            /* 0.5rem */
    /* …all 36 tokens… */
}
```

Theme-switching at runtime (the React `useTheme` behavior) means swapping the
active USS or rewriting the root variables — out of Week-1 scope; ship `midnight-luxe`
as the single baked theme and log a decision that multi-theme is deferred.

---

## 5. `constants.ts` → `Constants.cs`

### 5.1 Exported symbols (`constants.ts`)

The file is four exports — Solana addresses/mints, nothing else:

| Symbol | Value | Meaning |
| --- | --- | --- |
| `ADMIN_ADDRESS` | `BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV` | Admin Solana wallet |
| `PROJECT_VAULT_ADDRESS` | `CsNvnGxP3kkJ2hdkDpeC46q6cqkCK5SM7FSJ4C1fem33` | Project vault / treasury |
| `SOL` | `solana` | SOL identifier string |
| `USDC` | `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v` | USDC SPL-token mint |

### 5.2 Proposed C# design

**`Constants.cs`** — a plain `static class Constants` with four `public const string`
fields. Direct, literal port:

```csharp
public static class Constants
{
    public const string AdminAddress        = "BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV";
    public const string ProjectVaultAddress = "CsNvnGxP3kkJ2hdkDpeC46q6cqkCK5SM7FSJ4C1fem33";
    public const string Sol                 = "solana";
    public const string Usdc                = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    // Engine constant used by GameState (TOWER_SLOTS from villageSlice.ts)
    public const int TowerSlots = 9;
}
```

> **Caveat — wallet addresses are canon-adjacent.** These four values look like
> public addresses, but `docs/v2-unity-port-spec.md` Part 4 says public Solana
> addresses must flow from `data/wallets.json` (sourced from
> `docs/wallets-of-record.md`), and Part 10 forbids the agent changing wallet
> values. **Action:** port these literally into `Constants.cs` for Week 1 so the
> Wallet module (Week 7) compiles, but cross-check them against
> `docs/wallets-of-record.md` and `data/wallets.json` during the Week-7 wallet
> work — if they disagree, the docs win and the agent flags it (do not silently
> reconcile). Logged as a flag, not a decision.

`TOWER_SLOTS` is included because `GameState` needs it for the 9-element
`towers`/`towerAbilities` arrays and it has no other natural home in Week 1.
It is *not* from `constants.ts` (it lives in `villageSlice.ts`) — noting the
provenance so a reviewer is not surprised.

---

## 6. Week-1 deliverable checklist (Core module)

| Output file | Source | Status of spec |
| --- | --- | --- |
| `State/GameState.cs` | `gameStore.ts` (+ `state/*` slices) | §1 — 41 persisted fields, 6 enums, 9 nested shapes |
| `State/GameStateService.cs` | Zustand `persist` + `subscribe` | §1.6 — load/save/events/mutators/Reset |
| `State/SaveSchema.cs` | `saveSchema.ts` | §2.5 — version 10, validation + clamps |
| `State/SaveMigrator.cs` | `migratePersistedState()` | §2.4 — 9 migration steps v1→v10 |
| `SceneRouter.cs` | `App.tsx` route table | §3.3 — 4 scenes, `LoadScene`/`LoadSceneWithFade` |
| `Theme/Theme.cs` + `Theme/Theme.uss` | `themes.ts` | §4.4 — default `midnight-luxe`, 36 tokens |
| `Constants.cs` | `constants.ts` | §5.2 — 4 Solana constants + `TowerSlots` |

Save/load round-trip acceptance (Part 5 Week-1 deliverable): launch → "New Game"
(`GameStateService.Reset()`) → quit → relaunch → `GameStateService.Load()` reads
`PlayerPrefs["dotr-save"]` → SO restored. The migrator is exercised on any save
whose `SchemaVersion < 10`.

---

## 7. Improvement suggestions

These are Unity-idiomatic departures from a literal React translation. The baseline
spec above does **not** depend on any of them — they are optional, and each would
warrant a decisions-log row if adopted.

1. **Split `GameStateChanged` into domain events instead of one fat event.**
   Zustand selectors give React fine-grained subscriptions for free
   (`useGameStore(s => s.resources)` only re-renders on resource change). A single
   `UnityEvent GameStateChanged` loses that — every HUD widget wakes on every
   mutation. Recommend per-domain events on `GameStateService`
   (`ResourcesChanged`, `WaveRecorded`, `TutorialAdvanced`, `DungeonProgressChanged`,
   …). *Rationale:* preserves the selective-subscription performance the React
   store had; low cost; matches the Part-3 "UnityEvent on mutation" pattern at a
   finer grain. *Cost:* ~8 events instead of 1.

2. **Make the 7 themes a canonical `data/themes.json`, not hardcoded C#.**
   `themes.ts` is pure data (7×36 color tokens + fonts). Part 4 of the port spec
   says data crosses the streams as `data/*.json`. Hardcoding 252 color values in
   `Theme.cs` violates that principle and guarantees drift when a designer retunes
   a theme. Recommend extracting `themes.ts` → `data/themes.json` (HSL triples
   verbatim) and having `Theme.cs` load+parse it, generating `Theme.uss` from it
   via an Editor script. *Rationale:* single source of truth, designer-editable,
   no drift, matches the data-extraction protocol. *Cost:* one Editor codegen
   script; the file is not yet in the React `data/` folder so the agent authors
   it Unity-side (same posture as the Week-1 `canon-strings.json` decision already
   in the decisions log).

3. **Use a versioned migrator with a step registry, not a long `if`-cascade.**
   The React `migratePersistedState` is nine stacked `if (fromVersion < N)` blocks.
   A C# `Dictionary<int, Func<PersistedState,PersistedState>>` keyed by target
   version, applied in order from `fromVersion+1` to `CurrentVersion`, is easier to
   unit-test (one test per step) and to extend. *Rationale:* the Part-4 schema-test
   discipline wants per-step tests; a registry makes each step independently
   addressable. *Cost:* trivial; behavior is identical to the cascade.

4. **Store the save as a file in `Application.persistentDataPath`, not `PlayerPrefs`.**
   `PlayerPrefs` is the literal `localStorage` analog (and the port spec Part 3
   explicitly says "PlayerPrefs as the storage layer"), so the **baseline keeps
   PlayerPrefs**. But on Windows `PlayerPrefs` is the registry, with a practical
   per-value size limit; this save can carry an unbounded `inbox`/`pets`/`contacts`
   list. A `dotr-save.json` file under `persistentDataPath` removes the ceiling,
   is human-inspectable for QA, and makes the Save Export/Import feature (Weeks 7+)
   a trivial file copy. *Rationale:* avoids a latent size-limit bug; better QA
   ergonomics. *Cost:* deviates from the port-spec's stated storage choice — would
   need an explicit decisions-log row and arguably owner sign-off, since the spec
   named PlayerPrefs. Recommend: ship PlayerPrefs for Week 1, log the file-based
   option as a known future migration.

5. **Model the nullable-enum fields as a real `enum?`, not a sentinel.**
   `heroClass` is `'mage'|'knight'|'ranger'|null`. The cleanest C# is `HeroClass?`
   with a Newtonsoft converter that maps `null`↔JSON `null`. The sketch in §1.6
   used a `HeroClassOpt.None` wrapper to stay friendly to Unity's inspector
   serialization (which can't serialize `enum?`). If `GameState` is only ever
   serialized via Newtonsoft (not the Unity inspector), prefer the genuine
   `HeroClass?` — it is more honest about "no class chosen yet" and avoids a fake
   enum member. *Rationale:* type honesty. *Cost:* the SO won't show the field in
   the default inspector — acceptable if the save layer is Newtonsoft-only.
