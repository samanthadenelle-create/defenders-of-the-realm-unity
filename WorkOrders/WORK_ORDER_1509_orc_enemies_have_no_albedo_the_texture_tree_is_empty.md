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
