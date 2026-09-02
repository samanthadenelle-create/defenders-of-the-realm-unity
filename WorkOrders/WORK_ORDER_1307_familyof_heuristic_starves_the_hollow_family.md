# WORK ORDER 1307 — `FamilyOf` is a heuristic; the game's largest enemy family gets no pre-fetch

**Status:** READY TO IMPLEMENT
**Silo:** Content / Addressables
**Minted:** 2026-09-02 (CLI), found while fixing WO-1303. Not owner-reported — nothing on screen says this.

## What WO-1303 fixed, and what it exposed

WO-1303 closed the *manufactured* keys: `EnemyAnimatorLateBinder` passed a CONTROLLER name
(`SkeletonHumanoid`, `OrcHumanoid`, `LargeHumanoid`) where a MODEL was contracted. Fixed.

Fixing it surfaced a deeper defect the ticket had explicitly assumed away. WO-1303 stated that
`EnemyFactory.cs:1004` and `EnemyLateSkinner.cs:92` were fine "because they pass the model". **They
pass the right KIND of value and still resolve the wrong family for most of the roster.**

## The mechanism, proven at source

`EnemyAssetLoader.cs:124-125`
→ `PrewarmFamily(modelOrAddress) => EnemyContentWarmer.WarmFamily(EnemyContentWarmer.FamilyOf(modelOrAddress))`

`EnemyContentWarmer.FamilyOf` (`:215-225`) strips the `Enemies/` prefix and any path, then takes
**the text before the first `_`**. `WarmFamily` lowercases it and concatenates `"enemyfam-" + family`.

That is a NAMING HEURISTIC, not a lookup. Measured against `Assets/Resources/Data/Canonical/enemies.json`:

| authored family | models | `FamilyOf` yields | declared label? |
|---|---|---|---|
| **hollow** (10 enemies) | `Skeleton_Warrior`, `Skeleton_Rogue`, `Skeleton_Mage`, `Skeleton_Healer` | `skeleton` | **NO** |
| **troll** | `OgreMage` | `ogremage` | **NO** |
| orc | `Orc_*` | `orc` | yes |
| troll | `Troll*` | `troll` | yes |
| hollow | `Hollow_Walker` | `hollow` | yes |

**The five declared labels are** `enemyfam-orc`, `enemyfam-hollow`, `enemyfam-shared`,
`enemyfam-troll`, `enemyfam-bosses`.

So `FamilyOf` is **only accidentally correct** — it works where the model name happens to start with
the family name. For the game's LARGEST family it does not, and `enemyfam-skeleton` was the fourth
bad key in the owner's captures.

## Player impact — why this is not cosmetic bookkeeping

The per-family pre-fetch is what pulls an enemy's art down BEFORE it spawns. With `hollow` never
resolving, **no hollow/skeleton enemy is ever pre-warmed**: every one of them relies on the late-bind
path, spawning first and skinning later. That is the seam that produces unskinned/placeholder enemies
and late pops. It fails silently — WO-1303 downgraded the exception to a throttled warning, so after
that fix it is invisible unless someone reads the log.

⚠ NOTE THE TRAP: WO-1303 made this SAFER but not FIXED. Do not read "no more exceptions" as closed.

## The authority that already exists — REUSE IT, do not invent one

`EnemyDef.Family` is the authored truth, and `UpcomingWaveWarmPlanner.AppendFamilyOf`
(`UpcomingWaveWarmPlanner.cs:218-230`) already resolves through it correctly. `FamilyOf` simply does
not consult the catalog.

**The fix is to route the model→family resolution through `EnemyDef.Family`** at the two remaining
call sites (`EnemyFactory.cs:1004`, `EnemyLateSkinner.cs:92`), and to make `FamilyOf`'s string-split
a LAST-RESORT fallback that says so in the log when it is used — never the primary path.

## Acceptance criteria

1. Every authored family in `enemies.json` resolves to a DECLARED label from every call site.
   Assert over `def.Family`, **never** over `FamilyOf(model)` — pinning the derived token pins the bug.
2. `hollow` pre-fetches. Prove it with a captured `[Flow:EnemyAssets]` line naming `enemyfam-hollow`
   for a `Skeleton_*` model, not just an absence of warnings.
3. The string-split fallback survives only for genuinely unknown input, and logs when it fires.
4. No second warmer, loader, resolver or pool (ARCHITECTURE_PRINCIPLES 2b). One authority.
5. `EnemyFamilyLabelRegression` (added by WO-1303, markers `ENEMY_FAMILY_LABEL_OK` / `_FAIL`) must
   still pass, and gains a case covering the two re-pointed call sites.

## What NOT to touch

- ⛔ `Assets/AddressableAssetsData/**`. The labels are CORRECT; the callers are wrong. A change there
  re-hashes every bundle and mandates a fresh `tools\r2-ship.ps1` push (CLAUDE.md sec.16 — four incidents).
  **Do not "fix" this by declaring an `enemyfam-skeleton` label.** That would bless the heuristic and
  split one authored family across two labels.
- ⛔ Do not introduce any WAIT on the late-bind seam. `EnemyAnimatorLateBinder`'s comment records that
  waiting there deadlocked the game on 2026-08-20.
- ⛔ Do not weaken the refusal warning WO-1303 added; it is the only remaining detector.
- ⛔ Do not change enemy art, prefabs, or the animator factories.
