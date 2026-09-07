# WO-1485: APK texture pass - 741 MB of textures, duplicated particle art, uncompressed UI atlas, demo folders shipping

**Status:** READY TO IMPLEMENT
**Silo:** Build size. Asset import settings + folder pruning; no gameplay code.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1485 -> 1486 in the same edit).

## 1. EVIDENCE

```
Builds/apk-build.log:26485-26494
  textures 740.9 MB  =  81.7% of 907.1 MB user assets
```

What is in it:

- `SmokePuff.png` duplicated across FOUR ParticlePack folders.
- Unreferenced `.tif` ORIGINALS shipping alongside the used textures, 5.3 to 10.7 MB each
  (`LargeFlame02.tif` 10.7 MB).
- `Assets/UnityTechnologies` ParticlePack DEMO content: 159.1 MB.
- `Assets/Mirza Beig`: 37.5 MB, including two 16 MB spritesheets.
- `Packages/com.solana.unity_sdk/Resources/background.png`: 5.9 MB, shipped unconditionally.
- `Assets/Resources/UI/ElarionMedieval`: 161.6 MB in the build from 51 MB of source - the atlas has no Android
  override at all (`card-frame-empty.png.meta`: `overridden: 0`, `crunchedCompression: 0`,
  `maxTextureSize: 4096`).

## 2. FIX SHAPE

- Android platform override + crunch compression on the UI atlas, with a sane `maxTextureSize`; or move the
  atlas to R2 as WO-1338 did for the heroes. Name the choice in the RESULT.
- Deduplicate `SmokePuff.png` to one asset; delete the unreferenced `.tif` originals.
- Strip the ParticlePack and Mirza Beig demo folders from the build (move out of `Assets/` or exclude).
- Gate the Solana SDK `background.png` behind whatever ships it, or strip it.

## 3. WHAT NOT TO DO
- Do not delete art a scene references without checking. Measure references before removing any file.

## 4. ACCEPTANCE
- [ ] APK size measured BEFORE and AFTER, both numbers pasted with the build log line.
- [ ] Zero duplicate `SmokePuff.png`; zero unreferenced `.tif` in the build report.
- [ ] The UI atlas carries an Android override; the new texture total quoted.
- [ ] `REGRESSION_OK n/n` on a fresh log; the APK still launches and the UI renders (capture opened).
