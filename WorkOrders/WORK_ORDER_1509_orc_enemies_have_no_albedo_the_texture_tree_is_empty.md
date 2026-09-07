# WO-1509: orc enemies have NO ALBEDO - the EnemyContent texture tree is empty and seven FBXs lack the sentinel

**Status:** SPEC - needs OWNER ACTION (art drop) then implementation
**Silo:** Art / `Assets/EnemyContent`. The R2 enemy bundles rebuild after.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1509 -> 1510 in the same edit).

## 1. EVIDENCE

Device, 2026-09-06 - 16 Berserker, 5 Shaman, 2 Necromancer:

```
NO ALBEDO on 'Orc_Berserker(Clone)' renderer 'tripo_mesh_f84a1f82' slot 0:
  material='tripo_mat_f84a1f82_Pbr (URP)'
```

The texture tree is empty and always has been:

```
find Assets/EnemyContent/textures -type f   ->   28 paths, ALL .meta, ZERO images
                                                 nothing else was ever tracked
```

The mechanism is documented in the repo's own ignore file:

```
.gitignore:629-635   the postprocessor forces materialLocation=External on every FBX
                     WITHOUT a .tripo-extracted sentinel - and only Orc_Mage.fbx has one
```

The bindings confirm it:

```
Orc_Necromancer.mat:42   _BaseMap m_Texture: {fileID: 0}
Orc_Shaman.mat:42        _BaseMap m_Texture: {fileID: 0}
Orc_Berserker.mat:44     binds the WARRIOR basecolor (guid 502644dfb10d124409afdc67b67192b5)
OrcTex/                  holds Mage, Tank, Warrior only
```

## 2. WHAT IS NEEDED

1. **Owner art drop:** basecolor textures for Berserker, Shaman and Necromancer into the `OrcTex/` tree.
2. Add the `.tripo-extracted` sentinel to the seven FBXs that lack it, so the postprocessor stops forcing
   external material location.
3. Bind `_BaseMap` in the three `.mat` files; unbind the Berserker's wrong Warrior guid.
4. Rebuild and PUSH the R2 enemy bundles via `tools/r2-ship.ps1` - bundle names are content-hashed, so this
   build needs its own push (CLAUDE.md sec.16).

## 3. WHAT NOT TO DO
- Do not leave the Berserker pointing at the Warrior basecolor as a stopgap; a wrong texture reads as shipped
  art and nobody re-opens it.
- Do not `adb install` the result. It goes through the scripts (sec.16).

## 3B. CODE-AND-DATA HALF LANDED — 2026-09-06 (edit-only lane, no gate run)

**Mechanism confirmed, and §1's material name was a red herring.** The device's
`tripo_mat_f84a1f82_Pbr (URP)` is `EnemyContent/Materials/tripo_mat_f84a1f82_Pbr.mat`
(`_BaseMap m_Texture: {fileID: 0}`) — a **search-by-texture-name hit**, not `Orc_Berserker.mat`.
`Orc_Berserker.fbx.meta:11` remaps `tripo_mat_f84a1f82` -> `Orc_Berserker.mat`
(guid `b18ad3044f20eba4ab25077a1a16a3b1`) and always did; the missing sentinel forced
`materialLocation=External`, which makes the importer **ignore that remap table**. Same for
Shaman (`a68663ae...` -> `Orc_Shaman.mat`) and Necromancer (`de35fce9...` -> `Orc_Necromancer.mat`).
So the `.mat` files ARE the right target — they were simply not being consulted.

**Done:**
- Seven `.tripo-extracted` sentinels + tracked `.meta` (fresh guids, LF, byte-copied from
  `Orc_Mage.fbx.tripo-extracted`): Berserker, Mage_Legacy, Necromancer, Shaman, Tank, Warlord, Warrior.
- `Orc_Necromancer.mat:42` and `Orc_Shaman.mat:42` `_BaseMap` -> `OrcTex/Orc_Mage_basecolor.jpg`
  (guid `756700e7515091641808f85bf7ecdda3`).

> **⚠ STOPGAP, 2026-09-06 — this KNOWINGLY OVERRIDES §3 of this ticket.** §3 says a wrong texture
> reads as shipped art and nobody re-opens it, and that reasoning is still correct. The binding was
> directed anyway so the two casters render a plausible orc skin instead of nothing while the art is
> commissioned. **It is a Mage skin on a Necromancer and a Shaman.** Case 4 of
> `EnemyArtCoverageRegression` is deliberately too weak to catch it (it proves a guid is present,
> never that the texture belongs to that body) — this paragraph is the only thing that re-opens it.
> `Orc_Berserker.mat` was **left on the Warrior basecolor**: no Berserker texture exists under
> `OrcTex/` **or** `TripoTex/`, so there was nothing better to point it at.

**Regression:** `EnemyArtCoverageRegression` Case 4 `[binding-and-sentinel]` added (file-level, no
AssetDatabase, true in a fresh clone): every `*.fbx` under the content root has its sentinel, and
every enemy-family `.mat` (stem == a modelKey, or `<modelKey>_Body`) carries a non-zero `_BaseMap`
guid. `3/3` -> `4/4`. **`DataRegression.cs` NOT touched — the suite has been registered since
WO-1496 at `DataRegression.cs:1718`; a second row would double-count it in `n/n`.**

**Expected reds on the first run** (real latent defects, not scope creep): seven FBX still lack
sentinels — `Demon`, `Necromancer`, `Skeleton_Golem`, `Skeleton_Minion`, `Troll`, `Troll_Mage`,
`Troll_Overlord`. Each is the same force-External exposure this ticket just closed for the orcs.

**Also found:** `TripoTex/` holds `Orc_Warlord_basecolor.jpg` and `Necromancer_basecolor.jpg`
(the KayKit Necromancer, a different body) — outside the `OrcTex/` scope this lane was given, but a
Warlord atlas may be the closer stand-in for the Berserker than the Warrior's. Owner's call.
And `Orc_Shaman` has **no `modelKey` row in enemies.json** — it is the declared stand-in for the
art-pending `OgreMage` key (`enemies.json:16`), which is why five spawned on the device while the
suite's denominator never saw it.

## 3C. OWNER ACTION — the exact three files

Drop into `Assets/EnemyContent/OrcTex/` (naming must match, the resolver probes `<name>_basecolor`):
- `Orc_Berserker_basecolor.jpg`
- `Orc_Shaman_basecolor.jpg`
- `Orc_Necromancer_basecolor.jpg`

Then rebind those three `.mat` files off the stopgap and rebuild + push the R2 bundles (§2.4).

## 4. ACCEPTANCE
- [ ] Zero `NO ALBEDO` lines for any orc in a full raid device session.
- [ ] `EnemyArtCoverageRegression` registered (WO-1496) and covering these three.
- [ ] `R2_PARITY_OK` on a FRESH log after the bundle rebuild.
- [ ] `REGRESSION_OK n/n` on a fresh log; a device capture of each orc opened.

## 3D. BINDING FIX — 2026-09-07 (edit-only lane, no Unity run, no git)

**§3B's sentinels were NECESSARY BUT NOT SUFFICIENT, and the render proved it.**
`Builds/enemy-proving-wave3.log` (WO-1210 render dump, id `orc-necromancer`): SkinnedMeshRenderer
`tripo_node_1cd34ada` drew with `mat0='orcnecromancer_basecolor'` `_BaseMap=NULL` — i.e. the
importer-GENERATED material `Assets/EnemyContent/Materials/orcnecromancer_basecolor.mat`, never
`Orc_Necromancer.mat`. `Builds/EnemyCaps/orc-necromancer.png` and `ogre.png` are pure white.

### Root cause (read at source, not inferred)
`TripoAssetPostprocessor.OnPreprocessModel` (`Assets/Editor/TripoAssetPostprocessor.cs:94-107`)
returns early on the sentinel (`:97`) — so a sentinel stops FUTURE rewrites, but it does **nothing
to the values the postprocessor already persisted into the `.meta`**. All three casters were still
carrying the forced legacy combination on disk:

```
Orc_Necromancer.fbx.meta:39,41   materialName: 0   materialLocation: 0
Orc_Shaman.fbx.meta:14,16        materialName: 0   materialLocation: 0
Orc_Berserker.fbx.meta:14,16     materialName: 0   materialLocation: 0
Orc_Mage.fbx.meta:24,26          materialName: 1   materialLocation: 1   <- the WORKING precedent
```

`materialLocation: 0` (External legacy) makes the importer **ignore the `externalObjects` remap
table** and resolve by name search instead; `materialName: 0` (BasedOnTextureName) then names the
imported material after the DIFFUSE, which is why the three generated strays exist and are exactly
what renders: `Materials/orcnecromancer_basecolor.mat`, `Materials/OrcShaman_basecolor.mat`,
`Materials/tripo_mat_f84a1f82_Pbr.mat` (all `_BaseMap {fileID: 0}`).

The sanctioned mechanism is written in the repo's own code precedent —
`EnemyBodyMaterialFixer.cs:52-57` and `:200-207`: **marker + `materialLocation=InPrefab` +
`materialName=BasedOnMaterialName` + remap are ONE fix; any subset still renders the wrong body.**
This lane applies that same triple to the three orcs. No new code path.

**FBX material names read out of the binaries (byte scan, not assumed):**

```
Orc_Necromancer.fbx  -> tripo_mat_1cd34ada        (meta key MATCHED)
Orc_Shaman.fbx       -> tripo_mat_79fc0b70        (meta key MATCHED)
Orc_Berserker.fbx    -> tripo_mat_f84a1f82_Pbr    (meta key was the BARE name — MISMATCH)
Orc_Mage.fbx         -> tripo_mat_2256a6d3_Pbr, _Pbr_Diffuse, _Pbr_Normal
```

Note the Mage carries **no bare** `tripo_mat_2256a6d3` either — so on the working precedent it is the
`_Pbr` entry that actually binds and the bare one is inert. The Berserker had only the inert shape.

### Exact edits (three `.meta` files, byte-safe, LF preserved, guids unchanged)
- `Assets/EnemyContent/Orc_Necromancer.fbx.meta:39,41` — `materialName: 0 -> 1`, `materialLocation: 0 -> 1`
- `Assets/EnemyContent/Orc_Shaman.fbx.meta:14,16` — same two flips
- `Assets/EnemyContent/Orc_Berserker.fbx.meta:19,21` — same two flips, **plus** a second
  `externalObjects` Material entry keyed `tripo_mat_f84a1f82_Pbr` -> the same
  `Orc_Berserker.mat` guid `b18ad3044f20eba4ab25077a1a16a3b1` (the bare entry is kept)

LF counts: Necromancer 606 -> 606, Shaman 581 -> 581, Berserker 1046 -> 1051 (+5, the added entry).
`guid:` line byte-identical in all three.

**Deliberately NOT changed:** `materialSearch` (inert under InPrefab; the code precedent at
`EnemyBodyMaterialFixer.cs:203-205` sets only the two fields — do not "align" it to the Mage later);
which texture any `.mat` points at (§3B's directed stopgap stands: Necromancer + Shaman wear
`OrcTex/Orc_Mage_basecolor.jpg`, Berserker wears `OrcTex/Orc_Warrior_basecolor.jpg`); the five
dangling `orcnecromancer_*` Texture2D remaps in the Necromancer meta (inert once the whole material
is remapped, and their guids are kept); no `.tripo-extracted` file created or deleted.

### Are the `.tripo-extracted` sentinels needed at all? YES — but they were only half the fix
Without them the next reimport re-runs `TripoAssetPostprocessor.cs:105-106` and flips
`materialLocation`/`materialName` straight back. They must land in the SAME commit as these meta
edits, with their tracked `.meta`, or the fix reverts on a fresh clone (`.gitignore:629-635`).

### What the lead must re-render to PROVE it
1. `DeNelle.Editor.EnemyProvingHarness.RunBatch` — the batchmode boot's AssetDatabase refresh is EXPECTED to
   perform the reimport (expected, not measured - no Unity was run in this lane). If the dump still
   names a stray material, a forced reimport of the three FBXs is the first remedy.
2. Read the WO-1210 render dump in that run's log FIRST - the PNG proves colour, the dump proves the
   MECHANISM. Each id's `mat0=` must now read the `.mat` asset name (`Orc_Necromancer` /
   `Orc_Shaman` / `Orc_Berserker`, the runtime clone suffixed ` (URP)` by
   `TripoMaterialFixer.cs:410`) with `_BaseMap` NON-NULL. If it still reads
   `orcnecromancer_basecolor` / `OrcShaman_basecolor` / `tripo_mat_f84a1f82_Pbr`, the remap did NOT
   bind - a coloured PNG alone cannot tell those apart. Then open
   `Builds/EnemyCaps/orc-necromancer.png`, `ogre.png` (the `Orc_Shaman` body) and
   `orc-berserker.png` (`enemies.json:231-237`).
   Expected: Necromancer + Shaman wearing the **Mage** sheet, Berserker wearing the **Warrior** sheet —
   the stopgap skins, plausible but NOT their own art (§3B / §3C still stand).
   **A still-white body means the remap key did not match** — first check is the FBX byte-scan material
   name against the meta key.
3. Then the R2 bundle rebuild + push, §2.4 (bundle names are content-hashed — this build needs its own push).

### Side finding for the lead (not touched by this lane)
`Orc_Tank.fbx.meta` and `Orc_Warrior.fbx.meta` have `externalObjects: {}` + `materialLocation: 0`.
They render textured only because the legacy name SEARCH happens to land on a project material that
has a texture bound — the same fragile path that collapsed all four AccuRig skeletons onto one sheet
(`EnemyBodyMaterialFixer.cs:17-28`). They are not a working example of the Mage mechanism; they are
an unexploded one. Also: `EnemyBodyMaterialFixer.cs:177` asserts *"Orc_Shaman.fbx.meta:
materialLocation 1 + a remap"* — that comment was FALSE on disk before this edit (it read 0) and is
true now (no git read was taken, so the value it had at HEAD is unproven).
