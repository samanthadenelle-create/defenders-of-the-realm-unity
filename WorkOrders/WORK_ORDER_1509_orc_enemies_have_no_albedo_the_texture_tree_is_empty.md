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

## 4. ACCEPTANCE
- [ ] Zero `NO ALBEDO` lines for any orc in a full raid device session.
- [ ] `EnemyArtCoverageRegression` registered (WO-1496) and covering these three.
- [ ] `R2_PARITY_OK` on a FRESH log after the bundle rebuild.
- [ ] `REGRESSION_OK n/n` on a fresh log; a device capture of each orc opened.
