# PROD-009 — Enemy/structure content is ALL-OR-NOTHING and loads on the MAIN THREAD: per-family on-demand + async loader + roster lookahead

**Status:** ❌ **CLOSED — SUPERSEDED by PROD-010** (owner ruling 2026-08-19: *"PROD 10 kills 10 and 09"*).
This ticket shrank the first-run download by splitting enemy content per family and fetching on demand.
PROD-010 answers the same player problem a different way — the player opts in and pulls the WHOLE set once,
then runs local — so the per-family split buys nothing and would add a second content-partitioning scheme to
maintain. Nothing here is orphaned: the honest size figure PROD-010 shows the player (~88 MB) is exactly the
number this ticket would have reduced, and PROD-010 measures it rather than promising a shrink.
Do not re-open without an owner ruling that reverses the 08-19 decision.
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** HIGH — the freeze is on the LIVE build, and it lands inside the FTUE.
**Silo:** Addressables / content delivery. **Lane:** `Assets/_Modules/Core/Addressables` + `AddressableAssetsData`. No scenes.
**Provenance:** owner rulings 2026-08-18 — *"why cant we gait the enemies in a family at a time?"* /
*"one family not every family"* / *"what if they play for 10 minutes and only face two types of enemies"* /
*"streaming all seems wasteful"*.
**Cross-refs:** **PROD-011** (retry/timeout) is a **PREREQUISITE**, not a nicety. **PROD-010**'s first-run
signal covers a much smaller window if this lands first — its copy and duration assumptions change.

---

## 1. The two defects, both verified at source

### 1a. The load BLOCKS THE MAIN THREAD

`Assets/_Modules/Core/Addressables/StructureAssetLoader.cs:101`:

```csharp
var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(address);
result = handle.WaitForCompletion();
```

A synchronous main-thread freeze for as long as the whole remote bundle takes to download. Same shape
in `HeroAssetLoader.cs:73`, **`EnemyAssetLoader.cs:115`**, `VfxAssetLoader.cs:140`,
`AudioAssetLoader.cs:148`, `HeroTextureLoader.cs:74`.

### 1b. It is ALL-OR-NOTHING, and there is NO PREWARM

- Both remote groups are `m_BundleMode: 0` (**PackTogether**) —
  `Schemas/Structure_Art_BundledAssetGroupSchema.asset:41`, `Schemas/Enemy_Art_BundledAssetGroupSchema.asset:41`.
- **ZERO labels are authored.** All **35** `Structure_Art` entries and all **78** `Enemy_Art` entries
  carry `m_SerializedLabels: []` (counted at source); the project label table
  (`AddressableAssetSettings.asset:109-112`) holds only `default`, `Locale`, `Locale-en`.
- **No Addressables prewarm exists anywhere in the project** — a repo-wide search for
  `DownloadDependenciesAsync` and `GetDownloadSizeAsync` returns **zero hits**.

So resolving **one** enemy pulls the **entire** bestiary bundle. Measured first-run sizes (owner's
build, 2026-08-18): structures **19.71 MiB**, enemies **64.45 MiB**, total **84.26 MiB**.

### 1c. Where it hurts most — mid-FTUE

FTUE beat **7/8** `founding_defend` (`Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json:88`,
`"order": 40`) spawns its scripted band through `EnemyAssetLoader`'s blocking call against the
**64.45 MiB** bundle — 3.3x the structure stall, mid-tutorial, exactly as combat opens.

The owner's "10 seconds" budget does not hold: 10 s is ~16.5 Mbps **and structures only**. At 5 Mbps
it is ~33 s for structures and ~141 s for enemies. **A fixed-duration progress signal would itself be
a lie** — any beat must be driven by measured `GetDownloadStatus()`.

---

## 2. THE APPROACH — on-demand PER FAMILY. Three parts, ONE change.

Owner ruling: download only the families the player **actually encounters**. A session that meets two
enemy types must not pay for 78 models / 64.45 MiB of bandwidth and device storage. On mobile data
that is the argument that decides it. This attacks the root cause instead of dressing the stall in a
progress bar.

**The three parts are interdependent — implement them as one change, in this order.**

### Part 1 — the loader goes ASYNC. THIS IS A HARD PREREQUISITE, sequenced FIRST.

`EnemyAssetLoader.cs:115` is a synchronous `WaitForCompletion`. **On-demand loading layered on a
blocking loader is strictly WORSE than today**: it moves the freeze out of the loading screen and into
a fight, the first time each family appears. **No on-demand until the loader is async.** Put that
sentence in the code comment too, so nobody re-orders the work later.

### Part 2 — per-family bundles, labels DERIVED not hand-assigned

`PackTogetherByLabel` on `Enemy_Art`, with labels derived from the family taxonomy **that already
exists in this project** — verified at source, so this is cheap and non-arbitrary:

| Source | What it carries |
|---|---|
| `Assets/Resources/Data/Canonical/enemies.json` | every row has a **`family`** field. **19 rows, 0 missing**: `hollow` x10, `troll` x5, `orc` x4 |
| `Assets/_Modules/Core/Enemies/EnemyResolver.cs:2` | *"the ONE authority that maps an enemy id -> family -> class -> model"*; `Families` at `:277`; `FactionForFamily(string family)` at `:338` |
| `docs/MONSTER_FAMILY_ARCHITECTURE.md` | the leader/follower pack design the taxonomy serves |

**Derive the labels from `enemies.json`'s `family` field via a re-runnable editor tool — never
hand-assign them.** Hand-assigned labels rot the moment someone adds an enemy, and this project's
derive-never-hand-author law (`docs/ARCHITECTURE_PRINCIPLES.md` §4) is exactly the rule that keeps the
taxonomy and the bundle layout from silently diverging. `Assets/Editor/Catalog/SupercyanGearAddressableMarker.cs`
is the shape to copy.

> ⚠ Note the arithmetic gap to resolve during implementation: `enemies.json` holds **19** rows while
> `Enemy_Art` holds **78** entries. The mapping from an addressable entry to a family is therefore
> **not** 1:1 with catalog rows (rigs, textures, variants, shared assets). The tool must decide each
> entry's family from its **address**, and every entry it cannot classify must land in an explicit
> shared/`enemy_common` label — never silently in the default bundle.

### Part 3 — ROSTER LOOKAHEAD, so a download NEVER lands during combat

The wave roster is **generated, not authored** (`_smartComposition = true`,
`WaveManager.cs:197`), and — verified — the generator is a **pure, deterministic static**:

`Assets/_Modules/Village/Waves/WaveCompositionBuilder.cs:169`
```csharp
public static EnemyWaveComposition Build(
    int waveId, bool waveHasAuthoredHeavy, EnemyCatalog catalog = null, int seedSalt = 0)
```
seeded at `:179` with `UnityEngine.Random.InitState(waveId * 7919 + seedSalt * 104729 + (waveId & 1) * 31)`,
and called from `WaveManager.cs:1886` as `Build(waveId, WaveHasAuthoredHeavy(wave), _enemyCatalog)`.

**So the next wave's roster CAN be computed one wave ahead** — same waveId, same inputs, same result.
Map its entries' `EnemyId` -> `family` -> label, and prefetch exactly those families
(`DownloadDependenciesAsync`, async, fire-and-forget) during the **calm / Prepare-Phase countdown**
(`WaveManager.CountdownRemaining` at `:505`, `SecondsUntilNextWave` at `:518`) while the player is
placing buildings.

> ⛔ **CAVEAT, verified and load-bearing:** `Build` calls `UnityEngine.Random.InitState`, which
> perturbs the **global** RNG stream. A lookahead call therefore has a side effect on every other
> consumer of `Random` that frame. Save and restore `UnityEngine.Random.state` around the lookahead
> call, or give the builder a side-effect-free variant. Do not skip this — a "harmless" extra `Build`
> call would silently re-seed the game's randomness.

**Net effect:** nothing downloaded for families never met; no stall when a new family first appears;
no 64 MiB up front.

---

## 3. Costs — record them honestly, MEASURE them, do not estimate

1. **Shared-dependency duplication.** Shared rigs, shaders and animation clips get **copied into every
   family bundle that uses them**. The mitigation is a shared `enemy_common` label/bundle. ⛔ **The
   size impact MUST be measured with Addressables' `Check Duplicate Bundle Dependencies` before
   committing to a layout** — do not estimate it, and do not assume the `enemy_common` split is needed
   until the analyzer says so.
2. **Re-grouping rehashes every bundle**, so **every already-installed player re-downloads once**
   (content-hashed names). Unavoidable and one-time — which is precisely why the layout must land as
   **ONE deliberate change**, not two passes. This is the same already-installed-APK hazard recorded in
   PROD-010 §2.
3. **More bundles = more HTTP requests**, and `m_RetryCount: 0` (`Enemy_Art_BundledAssetGroupSchema.asset:36`,
   `m_Timeout: 0` at `:33`) means **one dropped request is a permanent miss for that session**.
   PROD-011's retry/timeout fix is a **PREREQUISITE** of this split.

---

## 4. Structures — a DIFFERENT answer, and the asymmetry is the reason

`Structure_Art` has the same shape (PackTogether, zero labels, **19.71 MiB**) but **not** the same
opportunity: **enemies are predictable and structures are not.** The wave roster is generated, so it
can be read ahead (§2 Part 3); **what the player builds is the player's choice, so there is no roster
to look ahead at.**

**Recommendation: leave `Structure_Art` WHOLE for now.** Reasons: (a) at 19.71 MiB it is under a
third of the enemy cost and it lands during a loading screen rather than mid-combat; (b) the only
honest on-demand trigger would be **build-palette card selection / ghost preview**, which fires
milliseconds before the player expects to see a model — the worst possible moment to start a
download; (c) a structures split would pay the §3.2 forced-re-download cost a second time for a
smaller win.

Revisit only if structures grow materially, and if so trigger on palette selection, **never** on a
prediction.

---

## 5. Acceptance criteria

1. `EnemyAssetLoader` resolves asynchronously; **no `WaitForCompletion` remains on the enemy spawn
   path**. Verified by a headless run that spawns a wave with a cold cache and shows no main-thread
   stall in the trace.
2. `Enemy_Art` is `PackTogetherByLabel`; every entry's label is **derived** by a re-runnable editor
   tool; adding an enemy to `enemies.json` and re-running the tool labels it with no hand edit; every
   unclassifiable entry lands in an explicit shared label, never silently in the default bundle.
3. A cold-cache session that meets only one family downloads **only that family's bundle** — proved by
   a measured byte count, not by inspection.
4. Lookahead: during the Prepare-Phase countdown the next wave's families are already in flight, and
   the trace shows the prefetch starting before the spawn. `UnityEngine.Random.state` is provably
   unchanged across the lookahead call.
5. `Check Duplicate Bundle Dependencies` output is recorded in the RESULT with the measured
   duplication, and the `enemy_common` decision is made **from that number**.
6. PROD-011's retry/timeout landed first.

## 6. What NOT to touch

- `Structure_Art`'s grouping (§4).
- The local `Default Local Group` layout.
- Do not add a fixed-duration progress bar to paper over the stall — that is PROD-010's surface, and it
  must be measurement-driven there too.
