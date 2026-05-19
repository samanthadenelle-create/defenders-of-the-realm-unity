# Dragon wave wiring — Syndrath as the apex wave-boss

**Date:** 2026-05-19
**Slice:** Wire the built dragon boss ("Syndrath the Devourer" — `DragonBoss.cs`
+ `Boss_Dragon.prefab` + `Dragon.controller`) into the Avalon village wave loop
so it actually spawns in-game. The boss was built by a prior slice; nothing
released it.
**Design source:** `docs/port-notes/dragon-boss.md` §2 — a special apex village
wave-boss, above the canon Necromancer.
**Status:** Source written. Cannot run Unity here — the integrator runs the
village build to verify (see §6).

---

## 1. The apex wave (waves.json wave 4 — "The Last Wing")

Added as the **terminal wave** of the canonical schedule:

| Field | Value |
| ----- | ----- |
| `waveId` | **4** |
| `name` | "The Last Wing" |
| `countdownSeconds` | 300 (React `LATER_PREPARE_SECONDS` build window) |
| `enemies` | `[]` — **empty by design**: the dragon *is* the wave |
| `apexBoss` | `{ id: "boss-dragon-syndrath", hp: 4200, nameKey: "bossSyndrath" }` |

**Why wave 4, not "well beyond wave 6":** `dragon-boss.md` §2 frames the apex
wave as sitting *above the Necromancer cadence* and says the owner picks the
number. The canonical `waves.json` schedule only runs waves 1–3 (waves 2–3 are
themselves authored continuations; there is no Necromancer wave in the file
yet). `WaveManager.EnterCountdown` advances `waveId + 1` and `WaveSchedule.Find`
requires the wave to exist — a gap (e.g. jumping to wave 12) would make the loop
report `Complete` at wave 4. So the apex wave is added as the **next contiguous
wave (4)** and is the *final* wave in the schedule — it is the apex by virtue of
being terminal and by HP (4200 vs the Necromancer's 1700), exactly the anchoring
`dragon-boss.md` §4 calls for. When a Necromancer boss wave is later authored
into the schedule, the dragon wave's `waveId` simply shifts to remain last.

The apex wave has **no ground enemy batches**: per `dragon-boss.md` §3 the
dragon owns its own kinematic flight, circles the Heart and strikes it directly
(`DragonBoss.DealStrike` → the Heart structure), so it needs no NavMesh enemies
and no breach ring. `enemies: []` deserialises to an empty `List<WaveBatch>`;
`WaveManager.StartWave`'s batch loop is null/empty-safe.

## 2. Schema extension — `apexBoss` vs `boss`

`WaveDef` already had a `boss` string field — but that names a **ground
NavMesh enemy** from `enemies.json` (the Necromancer), released by `SpawnBatch`
as an ordinary `Enemy`. The dragon is a different order of thing: a kinematic
flying `DragonBoss`, **not** in `enemies.json`, **not** a NavMesh agent. Reusing
`boss` would have routed it through `EnemyCatalog.Find` and failed.

So `WaveData.cs` gains a **new typed record** `ApexBossDef` and a new
`WaveDef.ApexBoss` field (JSON key `apexBoss`):

```
public sealed class ApexBossDef
{
    [JsonProperty("id")]      public string Id = "boss-dragon";
    [JsonProperty("hp")]      public float  Hp;        // <=0 keeps prefab default
    [JsonProperty("nameKey")] public string NameKey;   // canon-strings key
}
```

`WaveDef` also gets a convenience flag `IsApexBossWave => ApexBoss != null`.
The existing `boss` field is untouched — a wave could in principle field both.

## 3. WaveManager changes

`WaveManager` and `DragonBoss` are both in `DeNelle.Village` (one asmdef), so the
direct type reference is fine — no reflection needed in the gameplay code.

- **`[SerializeField] private DragonBoss _apexBossPrefab;`** — the Boss_Dragon
  prefab. Blank-tolerant (logs an error at runtime; loop does not stall).
- **`StartWave`** — after the normal batch spawn + the `boss` ground-enemy spawn,
  `if (wave.IsApexBossWave) SpawnApexBoss(wave.ApexBoss);`. It also now drives
  the Heart to `HeartState.Boss` (instead of `Vigilant`) for an apex wave.
- **`SpawnApexBoss`** — instantiates `_apexBossPrefab` at cruise height (Heart
  position + 22u, the `DragonBoss._orbitHeight` default) and calls
  **`dragon.Configure(bossId, heartTransform, boss.Hp)`** — the exact
  `Configure(id, anchor, maxHp)` signature. Subscribes `dragon.Died`, stores
  `_liveApexBoss`, fires `OnApexBossSpawned`.
- **`TickActiveWave`** — the wave-clear test now also waits on the dragon:
  `_liveEnemies.Count == 0 && !(_liveApexBoss alive)`. Without this an apex wave
  (zero ground enemies) would clear on frame 1.
- **`HandleApexBossDied`** — drops `_liveApexBoss`; the dragon destroys its own
  GameObject after its death-fall, after which `TickActiveWave` clears the wave.
- **`TriggerBreach`** — if a (hybrid) apex wave's ground enemies breach to ATB,
  the orphan dragon is destroyed so it does not orbit an empty village.
- New `WaveBossEvent : UnityEvent<DragonBoss>` + `OnApexBossSpawned` event +
  `LiveApexBoss` accessor — the HUD/camera bind the boss HP bar here.

## 4. Canon string

`canon-strings.json` (proper nouns) gains:

| Key | Value |
| --- | ----- |
| **`bossSyndrath`** | **"Syndrath the Devourer"** |
| `bossSyndrathEpithet` | "the Devourer" |
| `_bossSyndrathNote` | provenance note (agent-authored, owner-ratified 2026-05-19) |

Keyed `bossSyndrath` — consistent with the existing proper-noun convention
(`alduin`, `aetherSprite`, `flamePup`, …: a flat camelCase key → display
string). Resolved by `VillageStrings.Canon("bossSyndrath")` (the Village-local
twin of `CanonStrings`). The `waves.json` `apexBoss.nameKey` field points at it,
so the HUD/boss bar never hardcodes the name and it can be re-pointed without a
code change.

`en.json` (localizable copy) gains two apex wave-warning lines —
`wave.warning.apex.1` / `.2` — mirroring the existing `wave.warning.boss.*`
multi-variant pattern (numbered `.1 .2`, pick at random per trigger).

## 5. VillageSceneBuilder

`BuildWaveManager` now also wires the boss prefab. `DeNelle.Editor` references
no gameplay asmdef, so `DragonBoss` is touched by **full-name reflection**, the
same discipline the rest of the Week-4 wiring uses:

- New constants: `TypeDragonBoss = "DeNelle.Village.DragonBoss"` and
  `BossDragonPrefabPath = "Assets/Prefabs/Village/Generated/Boss_Dragon.prefab"`.
- New `WireApexBossPrefab(SerializedObject so)` — loads the Boss_Dragon prefab,
  gets its `DragonBoss` component by reflected type, and assigns it to the
  serialized `_apexBossPrefab` field via the existing `SetObjectField` helper.
- Non-fatal on a miss: a not-yet-built prefab logs a *warning* (run
  `Defenders ▸ Animation ▸ Build Dragon Boss` first); the builder never blocks —
  the project's builder discipline.

## 6. Integrator — what to verify

1. **Build the dragon boss first** — `Defenders ▸ Animation ▸ Build Dragon Boss
   (Controller + Prefab)` so `Boss_Dragon.prefab` exists at
   `Assets/Prefabs/Village/Generated/`. (See `dragon-boss.md` §11.)
2. **Run the village build** — `Defenders ▸ Week 3 ▸ Build Village Scene`. In
   the build log expect:
   - `[VillageSceneBuilder] WaveManager._apexBossPrefab wired to Boss_Dragon
     (apex wave 'The Last Wing' will release Syndrath the Devourer).`
   - If the prefab was not built: `[VillageSceneBuilder] Boss_Dragon prefab not
     found … apex-boss wave will have no dragon.` — build the prefab and re-run.
3. **Inspect the WaveManager** GameObject (`VillageRoot/GameplaySystems/
   WaveManager`) — the new **Apex Boss Prefab** field should reference
   `Boss_Dragon`.
4. **Play to wave 4** (or set `WaveManager._startWave = 4` as a dev override).
   On wave start expect the log line:
   `[WaveManager] Apex wave 4 — released flying boss 'boss-dragon-syndrath'
   (maxHp 4200).` The dragon should spawn ~22u above the Heart and begin its
   orbit; the Heart should go to its **Boss** threat state (violet).
5. **Confirm the wave clears** only when the dragon is dead — kill it and the
   loop should report wave 4 cleared then `All 4 waves cleared — schedule
   complete.` (wave 4 is terminal).
6. **EditMode JSON integrity test** — `CanonicalJsonIntegrityTest` still passes
   (`waves.json` keeps its top-level `version`, all three edited files parse and
   carry no stray markup).

## 7. Known gaps / follow-ups

- **Boss HP bar / camera** — `OnApexBossSpawned` + `LiveApexBoss` are exposed but
  nothing binds them yet; the HUD pass wires the boss HP bar and the camera that
  frames the orbit ring. `DragonBoss` also raises `PhaseChanged` / `StruckHeart`
  for camera shake + Heart-threat polish.
- **Wave ordering** — wave 4 is terminal *today*. When a canon Necromancer boss
  wave is authored into `waves.json`, bump the dragon wave's `waveId` so it
  stays last (the apex above the Necromancer, per `dragon-boss.md` §2).
- **Apex wave-warning copy** — `wave.warning.apex.*` strings exist in `en.json`
  but, like `wave.warning.boss.*`, are not yet shown by the wave-warning UI.
