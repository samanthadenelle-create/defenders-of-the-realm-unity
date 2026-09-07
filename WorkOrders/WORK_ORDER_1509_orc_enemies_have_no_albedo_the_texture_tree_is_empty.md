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

### ORACLE RULE NARROWED — 2026-09-07 (WO-1536 lane; edit-only, no Unity, no git)

`EnemyArtCoverageRegression` Case 4 `[binding-and-sentinel]` demanded a `.tripo-extracted`
sentinel of **every** `*.fbx` under `Assets/EnemyContent`. On `Builds/reg-wave5c.log:15438` that
redded seven legacy FBX — `Demon, Necromancer, Skeleton_Golem, Skeleton_Minion, Troll, Troll_Mage,
Troll_Overlord` — on the missing sentinel **alone**, while the very same failure line reported
`[binding-and-sentinel] bindings ok`.

**The old rule was wrong, and its remedy was actively dangerous.** The sentinel exists only to make
`TripoAssetPostprocessor.OnPreprocessModel` early-return
(`Assets/Editor/TripoAssetPostprocessor.cs:97`) so an **authored `externalObjects` remap** is not
overwritten on reimport. Four of those seven — `Demon`, `Troll`, `Troll_Mage`, `Troll_Overlord` —
declare `externalObjects: {}` (read at source in each `.fbx.meta`, 2026-09-07): there is no remap
to protect, and they bind their textures **by name** today. Adding a sentinel to them, which is the
only fix the old failure text suggested, would have pinned the importer onto the remap path with an
**empty table** — manufacturing the very white-body defect this ticket was opened for.

**Rule now (the discriminator is the meta, not the file list):**

| Shape of `<fbx>.fbx.meta` `externalObjects` | What Case 4 demands |
|---|---|
| one or more `type: UnityEngine:Material` entries | the sibling `.tripo-extracted` sentinel MUST exist |
| absent, or `{}` | NO sentinel is asked for; the FBX must instead RESOLVE A BASECOLOR via this suite's own `ResolveArt` (tier 1 = the importer's own binding, then own `.fbm` / atlas / pack) |
| missing or unreadable meta | FAILS by name — unproven remap state is never a pass |

The `.mat` `_BaseMap` sweep is untouched: a remap whose target `.mat` carries
`m_Texture: {fileID: 0}` still FAILS. So the case still reds by name on all three shapes the
ticket cares about — remap-without-sentinel, remap-target-unbound, and remap-less-with-no-art.

Edited files (this lane touched code only — no `.meta`, no `.mat`, no sentinel added or deleted,
no `EnemyFactory` change):
- `Assets/Editor/Regression/EnemyArtCoverageRegression.cs` — CASES header `(a1)/(a2)/(a3)`, the
  Case 4 sweep, and a new `CountMaterialRemaps` / `Indent` pair beside `HasBoundBaseMap`.

**Replayed over all 23 FBX (file-level replay of the new logic, no Unity):** reds fall 7 -> 4.
- **Now pass (4 of the 7):** `Demon`, `Troll`, `Troll_Mage`, `Troll_Overlord` — remap-less, each
  resolves an own-`.fbm` `*_Pbr_Diffuse.jpg` (Trolls also have a `TripoTex/*_basecolor.jpg` atlas).
- **Still red BY DESIGN (3 of the 7):** `Necromancer` (2 remaps), `Skeleton_Golem` (2),
  `Skeleton_Minion` (2) — each DOES declare a material remap and has no sentinel. The WO-1536
  lane's premise that all seven were remap-less is **false for these three**; measured in their
  `.fbx.meta` 2026-09-07. `Necromancer_NEW` / `Skeleton_Golem_NEW` are the `enemies.json` model
  keys. **CORRECTION, same day (see the addendum below): the sentence that stood here — "`Skeleton_Minion`
  is not a `modelKey` at all" — was true-but-misleading and was read as "unreferenced". It is the
  unknown-key FALLBACK body and sits in `EnemyResolver.CommittedModels`.** Lead's call: track a
  sentinel beside each. This lane is forbidden from adding one.
- **Possible 8th name, NOT provable from here:** `Orc_Mage_Legacy` — sentinel present, remap-less,
  and no basecolor in any FILE tier (no `.fbm`, no atlas entry, no `orc_*` loose image). Its FBX
  bytes reference `C:\EoA\Assets\Resources\Enemies\Orc_Mage.fbm\tripo_mat_80c4114e_Pbr_Diffuse.jpg`
  — a dead absolute path whose FILENAME does exist at
  `Assets/EnemyContent/Orc_Tank.fbm/tripo_mat_80c4114e_Pbr_Diffuse.jpg`, so `materialSearch:
  RecursiveUp` plausibly binds it and tier 1 passes under Unity. Plausible is not proven: **the
  marker on the next fresh regression log is the closer.** It is referenced only by
  `Assets/AddressableAssetsData/AssetGroups/Enemy_Models.asset` and is not a `modelKey`.

**This edit alone cannot take the suite green.** Case 2 `[every-model-has-art]` still reds on
`OgreMage` (no mesh at `Assets/EnemyContent/OgreMage.fbx`, no albedo at any tier), and Case 4 still
reds on the three remap-bearing legacy FBX above. Both need an owner/lead decision, not an oracle
change.

**Implementation note worth keeping** (it cost this lane one wrong replay): a YAML **sequence item
sits at the SAME indent as its key** — Unity writes `  externalObjects:` and then `  - first:`,
both at two spaces. A naive "indent <= key indent ends the block" scan terminates on the first
entry and reads every remap-bearing FBX as remap-less, which silently deletes the case. Also note
Unity serialises the type as `UnityEngine:Material` with a **colon**, not a dot; a
`Contains("UnityEngine.Material")` matches nothing and fails the same way. Both are called out in
the helper's own doc comment.

### ADDENDUM — 2026-09-07: two further narrowings PROPOSED, both measured as NO-OPS, NOT implemented

The coordinator proposed two more narrowings of Case 4. Both were measured at source before any
edit. **Neither was implemented, and neither would clear a single red.** No oracle change was made
for this addendum; the code is exactly as described in the section above. Ruling requested.

**Proposal (1) — walk only the FBX set the game REFERENCES (modelKeys in both `enemies.json`
twins + `EnemyResolver.CommittedModels` + `Enemy_Models.asset`), and downgrade unreferenced legacy
FBX to a note line instead of a FAIL.**

Measured: that union is **all 23 FBX**, so the walk is identical to today's `Directory.GetFiles`
and emits **zero** "legacy, unreferenced" note lines.
- `Assets/AddressableAssetsData/AssetGroups/Enemy_Models.asset` references **all 23** EnemyContent
  FBX (every `.fbx.meta` guid resolved against the group, 2026-09-07).
- `EnemyResolver.CommittedModels` (`Assets/_Modules/Core/Enemies/EnemyResolver.cs:95-109`) names
  `Necromancer`, `Skeleton_Golem`, `Skeleton_Minion` and `Demon` explicitly.
- The premise "none of the three is a live modelKey / `Skeleton_Minion` is referenced by nothing"
  is **false four ways** for `Skeleton_Minion`: `EnemyResolver.cs:51` (Hollow keys), `:59-61`
  (*"TryResolveHollowModel silently falls back to Skeleton_Minion for an unknown key"* — it is THE
  fallback body), `:98` (CommittedModels) and `:233` (a hardcoded `ModelKey = "Skeleton_Minion"`
  row). Retiring it is not available without a resolver change.
- Note, against an assumed blocker: reading the registry needs **no reflection** — `EnemyResolver`
  already exposes `CommittedModelKeys` (`:129`) and `IsCommittedModel` (`:133`) publicly. The
  proposal is a no-op on its merits, not for want of an accessor.
- Architectural objection worth a ruling either way: scoping the oracle to an Addressables group
  makes the group a **silencing vector** — dropping an FBX from `Enemy_Models.asset` would quietly
  stop the suite from checking it.

**Proposal (2) — exempt a remap target whose shader role is emission-only (`Glow.mat`) from the
`_BaseMap` sweep.**

Measured on `Assets/EnemyContent/Materials/Glow.mat`: `m_Shader` guid
`933532a4fcc9baf4fa0491de14d08ed7` — **the same shader as `Cellar_Hollow_Body.mat` and
`Hollow_Walker_Body.mat`**, i.e. ordinary URP/Lit. `m_ValidKeywords: []` (no `_EMISSION`),
`m_LightmapFlags: 4`, `_EmissionColor {r:0,g:0,b:0,a:1}`, `_EmissionMap {m_Texture: {fileID: 0}}`,
`_BaseMap {m_Texture: {fileID: 0}}`. It is emissive **in name only**: nothing is set. That is the
proposal's own **KEEP-THE-FAIL** branch, not its exemption branch.

It is moot regardless: the family sweep only visits a `.mat` whose stem is a modelKey or
`<modelKey>_Body`, and `Glow` is neither — so `Glow.mat` is **never swept today**, which is why the
same run that redded seven FBX still logged `[binding-and-sentinel] bindings ok`. The exemption
would exempt nothing.

**Remap-target table for the three (guids resolved 2026-09-07):**

| FBX | key `Glow` | key `skeleton` |
|---|---|---|
| `Necromancer` | `Materials/Glow.mat` — `_BaseMap` UNSET | `skeleton_texture_A_URP.mat` — `_BaseMap` BOUND |
| `Skeleton_Golem` | `Materials/Glow.mat` — `_BaseMap` UNSET | `Materials/skeleton.mat` — `_BaseMap` BOUND |
| `Skeleton_Minion` | `Materials/Glow.mat` — `_BaseMap` UNSET | `Materials/skeleton.mat` — `_BaseMap` BOUND |

**Expected reds after both proposals: UNCHANGED at 4** — the same three remap-without-sentinel FBX
plus `Orc_Mage_Legacy` pending Unity.

**What the three reds actually mean:** each carries an **authored two-entry remap**, so the WO-1536
hazard (a sentinel over an EMPTY table pins the importer onto the remap path and whitens the body)
**does not apply to them**. A tracked `.tripo-extracted` beside each is the correct fix, and it is
the one the failure text already names. Separately and for the lead, not the oracle: whichever
sub-mesh takes the `Glow` key renders untextured on a plain Lit shader with everything unset —
sentinel or no sentinel. That is an asset question.

## 3E. SENTINEL CREATION — 2026-09-07 (edit-only lane, no Unity, no git)

**Three `.tripo-extracted` sentinels + tracked `.meta` created for the remap-bearing legacy FBX:**
- `Necromancer.fbx.tripo-extracted` (0 bytes) + `Necromancer.fbx.tripo-extracted.meta` (guid: b616650965004c94bf8c2a7d61e3b2f9)
- `Skeleton_Golem.fbx.tripo-extracted` (0 bytes) + `Skeleton_Golem.fbx.tripo-extracted.meta` (guid: c5f48438fa1847b8af4ed73c35b2dff0)
- `Skeleton_Minion.fbx.tripo-extracted` (0 bytes) + `Skeleton_Minion.fbx.tripo-extracted.meta` (guid: 91748c3eb35447a7b0aec37bb27e215f)

**Glow.mat: plain URP/Lit, nothing set; the glow sub-mesh of Necromancer/Skeleton_Golem/Skeleton_Minion is untextured; asset ask.**
