# WO-1289 — Ground regrade: the neon meadow, and the oracle that let it through

**Status:** DONE (gated 2026-09-01: COMPILE_GATE_OK + TERRAIN_LAYER_FAIL/OK guard-bites proof + REGRESSION_OK 339/339)
**Minted:** 2026-09-01 (CLI, banner bumped 1289 -> 1293 in the same edit)
**Branch:** `feat/synty-art-retheme`   **Lane:** 1 of 4 (Synty art re-theme)
**Silo:** World / terrain art + regression oracle. File-disjoint from WO-1290/1291/1292.

---

## PROVING DATA (measured 2026-09-01, not inferred — CLAUDE.md §12)

Every shipped BaseColor PNG in `Assets/Generated/Terrain/Layers/`, sampled on a 32x32 grid:

| layer | avg RGB | Rec.709 luminance | **chroma (max-min)** |
|---|---|---|---|
| **`Ground_Meadow`** (the hub — what the player stands on) | **93, 189, 39** | 0.620 | **150** |
| `Mirewood_Roots` | 126, 52, 13 | 0.254 | 113 |
| `Path_Dirt` | 137, 66, 28 | 0.308 | 109 |
| `Mirewood_Mire` | 120, 56, 15 | 0.263 | 105 |
| `Stoneback_Rock` | 159, 98, 58 | 0.423 | 100 |
| `Ashwood_Ash` | 185, 143, 98 | 0.583 | 87 |
| `Goldfields_Field` | 177, 198, 121 | 0.737 | 77 |
| `Stoneback_Snow` | 219, 232, 239 | 0.900 | 20 |

Owner report (2026-09-01): *"the ground which is a bright neon green grass"*. RGB 93/189/39 is a
fluorescent yellow-green and is **35% more saturated than any other layer in the game**.

## ROOT CAUSE — the oracle bounds VALUE and nothing else

`TerrainLayerSet.cs:149` authors Meadow at `TargetLuminance = 0.62`. The shipped PNG measures
**0.620**. `TerrainLayerRegression.cs:210` is the ONLY bound applied:

```
if (Mathf.Abs(l - def.TargetLuminance) > TerrainLayerSet.LuminanceTolerance)
```

and `TerrainLayerRegression.cs:521-550` computes ONLY `0.2126*r + 0.7152*g + 0.0722*b`. There is
**no chroma / saturation bound anywhere in the contract**. So a neon texture passes the exact gate
that exists to keep the palette honest for a red/green colourblind owner (memory
`owner-colorblind-delegate-visual-creative`). Regrading the PNG without closing the oracle hole
means the next authored texture does this again.

## THE WORK

1. **Regrade `Assets/Generated/Terrain/Layers/Ground_Meadow_BaseColor.png`** — pull chroma down to
   the band the other seven layers occupy (target **chroma <= 100**, in line with Mirewood/Stoneback)
   while **holding Rec.709 luminance at 0.62 +/- 0.02** so the existing oracle still passes and the
   biome value-separation contract (WO-1044 / WO-1101) is untouched. Per `TerrainLayerSet.cs:26-28`
   the grade is baked INTO the shipped PNG, never applied via `diffuseRemapMax` — keep that.
2. **Add a chroma ceiling to the contract.** New field on `GroundLayerDef`
   (`Assets/_Modules/Core/World/TerrainLayerSet.cs`) — `MaxChroma`, authored per layer — plus
   `TerrainLayerSet.ChromaTolerance`. DeNelle.Core is the one place Editor + Village + EditorRegression
   can all reach (§5); **there must never be a second table** (the file's own header says so).
3. **Enforce it in `Assets/Editor/Regression/TerrainLayerRegression.cs`** — extend the PNG sampler
   beside the luminance pass and FAIL on `chroma > def.MaxChroma + ChromaTolerance`. Emit the measured
   chroma in the pass line so the number is visible, same shape as the existing luminance line (:209).
4. **Author `MaxChroma` for all eight layers** from the measured table above, with the outlier's
   ceiling set to the regraded value — not to 150.

## ACCEPTANCE CRITERIA

- [ ] `Ground_Meadow_BaseColor.png` measures chroma <= 100 and luminance 0.62 +/- 0.02.
- [ ] `TerrainLayerRegression` FAILS if the pre-regrade PNG is restored (prove the guard bites —
      memory `prove-the-success-path-not-just-the-refusal`: run it against the OLD file and see it fail,
      then against the new one and see it pass. A failure-only or pass-only proof is not acceptance).
- [ ] `REGRESSION_OK <n>/<n> suites` on a FRESH log (marker, never the exit code — memory
      `gates-report-success-without-proving-it`).
- [ ] Greyscale check: the hub ground still reads mid-value against sky and structures.
- [ ] A `RunCaptureHeadless` PNG of the hub, opened and looked at (memory
      `headless-screenshot-verify-ui-before-build` / `screenshots-are-primary-evidence-for-visual-defects`).

## DO NOT TOUCH

- The seven other layer PNGs, their `TargetLuminance` values, or the layer INDEX contract
  (`TerrainLayerSet.cs:86-100` — "Never renumber; append only").
- `WorldSceneLoader`'s DEF-108 runtime repaint logic (it reads the shared table; it is not the defect).
- `diffuseRemapMax` — the grade is baked into the PNG by design.
- Anything under `Assets/Synty/` (that is WO-1290/1291/1292).

## NOTES

- Owner is red/green colourblind. **Do not ask her to pick a hue** — pick the grade, prove it with
  the measured chroma/luminance numbers and a greyscale check, and show the screenshot.
- Ships independently of the other three lanes. No Addressables/R2 involvement.
