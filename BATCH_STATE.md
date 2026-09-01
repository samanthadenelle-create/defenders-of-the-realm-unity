# BATCH_STATE — handover to Codex, 2026-08-26

**Lead:** Claude Code (CLI seat, sole committer). **Courier:** the owner. Append-only by section.

---

## PART 1 — WHERE THE PROJECT ACTUALLY IS

**The game is LIVE on the Solana dApp Store.** This is not pre-release work. A regression that
reaches the store reaches real players.

**Today the owner ran a long felt-test on a Seeker device** (build `2026.08.26.342290`). It produced
~25 new tickets and 11 approved UI specs. The board reads **58 READY** — that number is misleading,
see PART 2.

### Eight finished lanes are in the tree, gated or not — check `git status` first

If the lead gated before handing over, these are committed and the tree is clean. If not, they are
uncommitted and **you must not touch any file they contain** (PART 3.4 lists them).

| Lane | What was actually wrong |
|---|---|
| Staff pose (WO-1226) | The drawn staff lay across the body. Six prior fixes failed because **a regression PINNED the wrong value**. Owner ruled it stands vertical; the pin moved WITH the ruling. |
| Battle-lock (WO-1233) | **P0.** Winning an arena left the town unresponsive 8 times in 9. Root: `PursuitBattleProbe` held the lock; `PostureSignals.ClearPursuits` had ONE caller, on scene load — and the arena stages in-place with no scene load. |
| Raid payout | Troops killing defenders banked per-kill materials mid-raid. Owner ruled raids pay ONCE at the end. Gold and XP still pay per kill. |
| Fail-closed gating (WO-1223) | **FOUR OF FIVE** failure modes resolved to OPEN. `ParseState` literally ended `default: return Open`. The gate was no gate in every degraded condition. |
| Enemy level (WO-1232) | Two call sites still ran a retired `maxHp/25` heuristic; one drove the danger skull, so every enemy read LETHAL. |
| VFX pool (WO-1229) | **Not a leak.** 44 candle anchors against a global 24-slot pool with no bound. Also found: the dungeon 48-tier had **never engaged in a shipped build**. |
| Hollow passes | Four regression guards returned without asserting and landed in the GREEN column. |
| Hero select (WO-1083/1234) | The portrait resource path was written out in **eleven literals across seven files**. Now 2, both inside one constant. |

**Also live as of today: the F8 DEVICE BRIDGE (WO-1227).** Until this morning device captures never
reached any seat — 736 entries had accumulated unread since 2026-07-20. The chain is whole and
delivered its first two captures within the hour.

---

## PART 2 — THE GOAL

**The goal is a PROD BUILD, and it does NOT require closing 58 tickets.**

Ship gates are FOUR MARKERS (CLAUDE.md §8 + §16), not an empty board:

```
COMPILE_GATE_OK  +  REGRESSION_OK <n>/<n>  +  UI_CAPTURE_OK  +  R2_PARITY_OK
```

**What actually blocks a prod push:**

1. **WO-1233** battle-lock softlock — FIXED, awaiting gate
2. **WO-1223** fail-closed gating — FIXED, awaiting gate
3. **The R2 content push** — `tools\r2-ship.ps1`. Bundle names are **content-hashed**, so every
   content build needs **its own** push. A previous push can never cover this one. This has already
   burned the project three times.

Everything else on the board is polish, presentation, or new feature. Shipping with an untidy
Treasure panel is legal. Shipping with a town the player cannot interact with is not.

**Your job in this window is NOT to burn down the board.** It is to advance work genuinely disjoint
from the gate, so that when the lead returns the gate runs once and cleanly.

---

## PART 3 — THE PROCESS

### 3.1 You are in a linked worktree
Your `git diff` will report the lead's committed work as if it were uncommitted or duplicated.
**It is not.** Hash before believing duplication. Never merge your worktree as a branch.

### 3.2 NO git commit, add, or push. Ever.
There is exactly ONE committer and it is not you. Two committers duel on `.git/index.lock` and
produce stale locks plus false "pushed" reports. Leave work in the tree; describe it in the handback.

### 3.3 NO Unity. You cannot gate.
Unity is single-instance and the lead owns the gate. Do **not** run `run-unity-method.ps1`,
`CompileGate`, or `DataRegression`. A collision can corrupt a gate log the lead depends on.

**Therefore prefer work verifiable WITHOUT Unity.** `api/` has node tests. PowerShell has
`[System.Management.Automation.PSParser]::Tokenize` for parse checking. Use them and **paste the
output** in your handback.

For any C# you write: brace-balance check (`{` vs `}`) plus a NUL scan on every file, counts
reported. That is this repo's minimum.

### 3.4 Do not touch files the lead has uncommitted
**Run `git status --short -- Assets/` FIRST.** Every file it lists is locked. If the tree is clean,
this section is moot and you have more room.

If an assignment appears to require a locked file: **STOP and say so in the handback.** Do not work
around it, do not copy the file, do not "just add one small thing".

### 3.5 Where you can safely work
- **`api/`** — the Vercel serverless backend. It is **in this repo**, not a separate project. Fully
  disjoint from the gameplay lanes, and node-testable.
- `.claude/skills/run-defenders/*.ps1` — committed as of `89977006a`.
- `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs`.
- `docs/`, `WorkOrders/`.
- Canonical JSON **except `waves.json`** (mid-change).

### 3.6 Engineering rules that are not negotiable
- **Instrument before you fix** (CLAUDE.md §12). Static reading LOCATES a candidate; it never
  CONCLUDES a cause. If you cannot cite captured data proving the cause, you have not earned the
  edit. Every wrong theory this project shipped came from skipping this.
- **No hollow passes.** A guard that returns without asserting lands in the GREEN column and is a P1
  defect here. Missing dependency → FAIL naming it, or return an explicit skip token. Never a silent
  bare return.
- **Prove RED first.** A regression that has never failed proves nothing.
- **Never raise a cap or threshold to make a symptom go away.** The VFX pool went 20 → 40 → 24 that
  way and the real cause sat unfound for months.
- **A failure-only oracle is not acceptance.** Assert the good path too — this repo shipped a guard
  that aborted every good run while exiting 0.
- The owner is **red/green colourblind**. Nothing may carry meaning by hue alone.
- **ASCII-only** TMP strings and PowerShell.

---

## PART 4 — ASSIGNMENTS, in priority order

### 1. WO-1237 — the softlock detector fires on AFK  *(safe, self-contained)*
`WorkOrders/WORK_ORDER_1237_softlock_detector_fires_on_afk.md` — read it in full.

The detector labels 180s of no movement as `possible_softlock`. Capture seq 3609 proves a false
positive: the screenshot shows full HP, a five-face bar, and a wave clock counting down normally. The
owner was idle, not stuck.

**Why it matters:** `possible_softlock` is one of four kinds that PAGE a seat, and the device backfill
holds 8 of them. Noise trains the seat to discount the kind — and the one real softlock then arrives
already discredited.

Build an IDLE-vs-STUCK discriminator. Candidates (instrument, do not assume): input presence,
`Application.isFocused`, world liveness (the wave clock was ticking).

- **Do NOT just raise the 180s threshold** — that trades a false positive for a slower true positive
  and leaves the classifier equally blind.
- **Do NOT silence the kind.** An idle capture is still RECORDED, just not paged.
- Re-run your classifier over `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md` (736 entries) and
  **report how many of the 8 reclassify**. That number is the ticket's value.
- Any `.ps1` must be **pure ASCII**. A BOM-less UTF-8 `.ps1` is read as ANSI by PS 5.1, and CP1252
  turns smart-quote bytes into string delimiters — silently mis-parsing while every gate stays green.
  A regression FAILS on this and it caught the lead's own hook today.

### 2. `api/` hardening  *(safe, node-testable)*
- `api/schema.sql` uses `ON CONFLICT DO NOTHING` on its seeds. **That is exactly why two
  `dungeon_status` rows never reached production and the owner's dungeons were shut all day.** Audit
  every seed in that file for the same trap and report which others would silently fail to back-fill
  an already-provisioned database.
  **Do NOT change production data** — the lead already wrote the two rows and verified them by shape
  query (`DUNGEON_ROWS_OK 6/6 covered`).
- `test/dungeon-status.manifest.test.js` — confirm it still REDS when a portal-gated id has no seed
  row. Run it; paste the output.

### 3. WO-1121 — live money rails and buy gate  *(oldest READY — TRIAGE, do not build)*
Read it and **report whether it is actually actionable** before writing a line. It may be owner-gated
or need rulings. A truthful *"this is blocked on X, here is the evidence"* is a valid and valuable
handback — more valuable than code built on a wrong assumption.

---

## PART 5 — HOW TO HAND BACK

Write `batch_results_state.md`, append-only by section. For each assignment:

- files changed, with line numbers
- the **command output** proving your claim — not a description of it
- brace counts + NUL scan for any C#
- anything you could not do, and why

**Your handback is a CLAIM until the lead proves it.** That is not distrust; it is the protocol this
repo runs on. Every assertion will be re-verified against the tree.

---

## PART 6 — CONTEXT YOU WILL OTHERWISE GET WRONG

- **`Enemy.Level` IS the `maxHp/25` heuristic** (`Enemy.cs:623`). There is **no authored level field**
  on `EnemyDef`. Its own doc comment claims otherwise and is WRONG — it misled a ticket today.
  Comments lie; read the code.
- **Five action-bar faces in open town is CORRECT.** Talk is proximity-gated on
  `TalkPromptRegistry.Count > 0`. Do not "fix" it — doing so cost an RCA this morning.
- **Offline first-run = every dungeon SEALED.** Owner ruling, not a bug.
- **Passive Echo repair SPENDS wood and iron.** The owner ruled the spend stays.
- **The repo root is machine-dependent** (`C:\eoa` on one machine, `D:\eoa` here). Never hardcode it.
- **58 READY is not 58 ship blockers.** See PART 2.

---

# APPENDED 2026-09-01 — PART 7: THE SYNTY ART RE-THEME LANE

**Lead:** Claude Code (CLI seat, sole committer). **Branch: `feat/synty-art-retheme`, 7 commits,
NOTHING PUSHED.** Base = `wip/village2-and-f8-tickets`.

## 7.1 — WHAT THIS LANE IS

Owner bought Synty **POLYGON Fantasy Kingdom + POLYGON Generic** ($350, 2026-09-01) and ruled:
**FULL re-theme, everything Synty**, and **walls at NATIVE height, zero scaling**. Four lanes were
minted: WO-1289 ground, WO-1290 walls, WO-1291 buildings, WO-1292 environment dressing.
Banner bumped 1289 -> 1293 (and PROD 021 -> 022) in the same edits.

| commit | lane |
|---|---|
| `0fe54026b` | lane opened, WOs minted, `Assets/Synty/` gitignored |
| `f434a5e0f` `57e2f7355` | WO-1289 ground regrade + chroma oracle — **DONE, gated** |
| `26ef6af55` `9687ecb22` | WO-1290 castle perimeter — **IN PROGRESS, gated** |
| `707f55ba0` | WO-1291 structure re-theme — **IN PROGRESS, gated** |
| `0b776276f` | parks PROD-021 + WO-1293 tickets (unrelated to this lane) |

## 7.2 — THE PACK: FACTS YOU WILL OTHERWISE GET WRONG

- **`Assets/Synty/` is GITIGNORED** (461 MB, 2,682 prefabs) — same policy as polyperfect /
  Quaternius / Blink. A fresh clone re-imports from the Asset Store. Builders reference
  `Assets/Synty/...` directly, exactly as the old walls referenced gitignored polyperfect.
- **It is URP-NATIVE. There is no magenta pass to run.** Materials point at
  `PolygonGeneric/Shaders/Generic_Basic.shadergraph`, whose targets include `UniversalTarget` +
  `UniversalLitSubTarget`; `com.unity.shadergraph 17.4.0` is already in `Packages/manifest.json`.
- **`Assets/MedievalCastlePackLite` is SHELVED** — Built-in Standard shader (magenta under URP),
  zero prefabs, 2.64 m wall against our 8.49 m, single visual tier. Do not use it.
- **`SyntyPackageHelper.cs`** is `[InitializeOnLoad]` + `projectChanged` and calls
  `EditorUtility.DisplayDialog` and a Package Manager `AddAndRemoveRequest`. It has not misbehaved
  in ~20 batchmode runs, but it is a live hazard in a headless chain. Left alone deliberately.
- **Synty is authored in cm: 500 file units = 5.00 m.** Confirmed by `MeasureX` at runtime.

### The measured castle kit (from the FBX, then confirmed on instances)

| module | size (m) | tris |
|---|---|---|
| `SM_Bld_Castle_Wall_01` | 5.00 x 5.00 x 0.50 | **20** |
| `SM_Bld_Castle_Battlements_01` | 5.00 x 1.38 x 0.50 | 146 |
| `SM_Bld_Castle_Wall_Gate_01` | 5.67 x 5.86 x 1.26 | 2,530 |
| `SM_Bld_Castle_Wall_Tower_S/M/L_01` | 2.44 / 3.05 / 3.82 dia x 7.52 tall | 608 |

Full ring ~15 k tris. The retired path was ~8 k for 20 stretched slabs; the
`GridWallBuilder`/Tripo path is **1.28 M** (`Resources/Walls/wood_wall.fbx` alone is 26,691 tris).

## 7.3 — THE FOUR TRAPS THAT COST ME TIME. DO NOT RE-LEARN THEM.

1. **UNITY MIRRORS X ON FBX IMPORT.** `SM_Bld_Castle_Wall_01` reports `X -0.00..5.00` in the FBX
   (pivot on the LEFT edge) but the **instantiated prefab measures `-5.00..0.00`** (pivot on the
   RIGHT edge). Reading the pivot convention off the FBX put the whole ring 5 m out and opened a
   2.5 m hole at every corner. `SyntyCastlePerimeterBuilder` now MEASURES the pivot
   (`wallPivotMinX`) off a live instance and places at `(runLeft - pivotMinX)`, which is correct for
   left-edge, right-edge AND centred pivots. **Never infer a pivot from a mesh file.**
2. **`??` DOES NOT WORK WITH UNITY'S FAKE-NULL.**
   `GetComponent<BoxCollider>() ?? AddComponent<BoxCollider>()` returns the fake-null and the next
   line throws. That single operator failed **27 of 29** structures. Always `if (x == null)`.
3. **ADDRESSABLE ADDRESSES ARE FREE TEXT AND CAN CONTAIN SPACES.** The live address is
   `Structures/arcane tower`. A diagnostic that dumped addresses with `\S+` silently truncated it to
   `arcane`, so the map key was wrong and the entry was reported unmapped. Match to end-of-line.
4. **THE PROJECT'S OWN ORACLES CAUGHT ALL OF THESE, NOT ME.** `AssetRootsRegression` (I re-typed
   `"Assets/StructureContent"` instead of `AssetRoots.StructureContent`), the **art-ledger** (I
   re-typed an `EnemyArtPaths` naming token in a hand-written texture-address list), and
   `STRUCTURE_ORIENTATION_FAIL`. Run `DataRegression.RunAll` before believing anything.

**Also:** `PrefabUtility.SaveAsPrefabAsset` THROWS into a folder made with
`Directory.CreateDirectory` — the AssetDatabase does not know it exists. Use
`AssetDatabase.IsValidFolder` + `Refresh`.

## 7.4 — WO-1289 GROUND: DONE, and WHY IT MATTERS BEYOND THE GRASS

`Ground_Meadow_BaseColor.png` shipped at **RGB 93/189/39, chroma 150** — 35 % more saturated than
any other terrain layer, and it is the ground the player stands on. Owner: *"a bright neon green
grass."*

**It passed every gate**, because `TerrainLayerRegression` bounded **Rec.709 luminance only**. Value
cannot see saturation. So the colourblind-safety oracle waved a fluorescent texture straight through.

Fixed BOTH halves: regraded to chroma **85.0** with luminance held at **0.6195 -> 0.6195** (greyscale
read pixel-identical, WO-1044 biome value contract untouched), and added `GroundLayerDef.MaxChroma`
+ `TerrainLayerSet.ChromaTolerance`, enforced in Case 3, authored for all eight layers **from the
suite's own full-resolution measurements** (my first caps came from a 32x32 downsample, which
averages chroma DOWN, and were systematically too tight — the fail-run caught it).

Guard-bites proven **both directions**: old PNG -> `TERRAIN_LAYER_FAIL` naming
`CHROMA=149.7 ... capped at 90`; new PNG -> `TERRAIN_LAYER_OK`.

## 7.5 — WO-1290 WALLS: IN PROGRESS

`Assets/Editor/WallTools/SyntyCastlePerimeterBuilder.cs` replaces `CastleWallsFromRecipe` as the
perimeter source. **Why the old path is retired:** `castle-south-recipe.json` is four pieces mirrored
x4 — 20 objects for the whole castle — and `SM_Wall_Medieval_Stone` (15.75 x 8.49 x 2.39 m) is placed
at `scale.x 1.62` and `1.95`, i.e. the SAME slab rendered at 25.5 m and 30.7 m, plus an arbitrarily
scaled seam filler and a ROUND corner tower squashed 1.28 on X only. Non-uniform scale also breaks
the normal-map tangent basis. Its unit of construction is a scaled slab, so it cannot produce a good
wall; every "seam fix" adds another stretch factor.

**Result:** module 5.00 m (measured), 15 slots/side, span 75.0 m, extent +-39.0 m (plinth 44),
**56 walls + 56 battlements + 4 gates + 4 towers = 120 objects, ALL at scale 1.**
Symmetry oracle: south wall run `X [-37.50, 37.50]`, centre `0.00`, width `75.00` vs 75.00 expected.

**Load-bearing things it preserves — each is a scar with an F8 behind it:**
- `CastleSide_*` root names — `CastleWallNavObstacleInstaller` matches them to carve the NavMesh.
  **The hero is a NavMeshAgent and IGNORES physics colliders**; only a carved NavMesh stops her.
- `Structure` layer on all masonry (WO-449 line-of-sight occlusion).
- **No `Shader.Find`** — returns NULL in batchmode (`CastleHubBuilder.cs:2549`).

**Gate moved deliberately:** the odd-slot algorithm centres the gate at x=0; the old `-4.37` was
hand-placement drift. `castle-south-recipe.json` is re-pointed to `(0,0,-39.0)` because
`BuildGateExitStrips` and `CastleMoatBuilder` (`gateLateral = southGate.x`) both derive the four
bridge/exit positions from it. Its four wall PIECES are now inert; the file survives only as that
gate anchor and says so in a `_wo1290Note`.

**NOT DONE:** the corner tower stands proud of the corner rather than being built into it, and reads
SHORTER than the wall (5.02 m visible vs 5.00 + 1.38 battlement) once its 2.50 m authored foundation
is correctly buried. The ring is CLOSED — this is aesthetic, not a hole. The kit ships
`SM_Bld_Castle_Wall_Corner_M_01` (4.00 m turn) if you want a true bastion.

## 7.6 — WO-1291 STRUCTURES: IN PROGRESS

`Assets/Editor/SyntyStructureRetheme.cs`. **`structures-catalog.json` is NOT touched.** Its `id`
strings are LIVE SAVE KEYS — renaming one orphans every player's building. Instead every
`Structures/*` **address keeps its exact name** and is re-pointed at a wrapper prefab under
`AssetRoots.StructureContent + "/Synty"`. Catalog, save format, `VisualFactory` and every caller are
untouched; only the mesh behind the address changes. Each wrapper carries a BoxCollider fitted to
measured bounds on the `Structure` layer.

**`STRUCTURE_RETHEME_OK swapped=30 unmapped=3 missing=0`**, `REGRESSION_OK 339/339 suites`.

- Texture addresses are skipped **by asset type**, not by a name list (the list re-typed an
  `EnemyArtPaths` token and would go stale).
- `ArcaneSpire_2` is `Wall_Tower_L_01`, **not** `Church_01_A`: the latter measures upright aspect
  **1.08**, under the 1.2 floor every Tower-class row must clear. The oracle states plainly that
  widening the floor is an **OWNER RULING, not a fix** — so the art changed.
- **STILL UNMAPPED, reported not skipped:** `CrystalMine`, `IronMine`, `GenericContainer`. No Synty
  equivalent the lead would stand behind.
- **THE ART PAIRINGS ARE THE LEAD'S AND THE OWNER HAS NOT SEEN THEM.** Blacksmith->armorer/Forge,
  Tavern->ShopAndCrafting, Stables->barracks, Hut->farm, Windmill->Windmill/Watermill, etc. A first
  pass to correct, **not a ruling**. The table is one dictionary at the top of the file.

## 7.7 — TWO THINGS THAT ARE NOT PROVEN, AND MUST NOT BE CLAIMED

1. **The building swap has NO VISUAL PROOF.** Catalog structures spawn at RUNTIME via Addressables,
   so an editor capture cannot show them — the buildings in
   `docs/ui-evidence/wo1290_synty_perimeter/` are HAND-PLACED SCENE OBJECTS that this lane has not
   touched. Proving it needs a runtime capture on pushed content.
2. **THIS LANE CANNOT SHIP WITHOUT `tools\r2-ship.ps1`.** The re-theme re-hashes the Addressable
   content and **bundle names are content-hashed — this build needs ITS OWN push.** A missing push
   fails SILENTLY: installs, launches, plays, placeholder buildings, no on-screen error. It has
   happened FOUR times. Judge by `R2_PUSH_OK` + `R2_PARITY_OK` on a FRESH log, never the exit code,
   and note `Builds/r2-parity.log` is **UTF-16LE** — a plain `grep` finds nothing and reads as a
   false failure.

## 7.8 — WHAT IS LEFT

- **WO-1292 environment dressing** — untouched. ~140 `Rock_*` instances, paths, banners, furniture.
- **WO-1290** corner bastion + tower height.
- **WO-1291** three unmapped addresses; the hand-placed scene storefronts
  (`Blacksmith_Weapons_Storefront`, `Forge_Armor_Storefront`, `Windmill_Food_Storefront`,
  `Lumbermill_Wood_Storefront`, `Jeweler_Gems_Storefront`, `Marketplace_Monetization`,
  `ArcaneTower_MagicUpgrades`, `CastleBarracks`) are still polyperfect.
- **PROD-021** (parked, unrelated to this lane): the shipped build 404s on
  `StandaloneWindows64/catalog_2026.08.31.349579.hash` while that exact file sits in `ServerData/`.
  Root gate defect: `r2-ship.ps1:115` verifies ONE explicit target, so a run that pushes the parent
  and verifies Android emits `R2_PARITY_OK` while Windows 404s.
- **WO-1293** (parked): `BuildPeekStrip` NRE. The method MOVED to `InventoryGrid.cs:290` in
  `d6d3146b2` — grepping `HeroInventoryController.cs` finds nothing.

## 7.9 — HOW TO VERIFY ANY OF THIS

Unity editor **must be CLOSED**; the runner refuses on the lock, and a just-closed editor takes
~40 s to release it.

```
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run                        -LogName compile-gate.log     -ExpectMarker COMPILE_GATE_OK
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.SyntyCastlePerimeterBuilder.BuildInHub -LogName synty-perimeter.log  -ExpectMarker PERIMETER_OK
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.SyntyStructureRetheme.Run              -LogName synty-retheme.log    -ExpectMarker STRUCTURE_RETHEME_OK
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.SyntyPerimeterProofCapture.Run         -LogName perimeter-proof.log  -ExpectMarker PERIMETER_PROOF_OK
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll                  -LogName data-regression.log  -ExpectMarker REGRESSION_OK
```

**Always pass `-ExpectMarker`.** Without it the runner prints `PASS-UNASSERTED` and proves nothing.
