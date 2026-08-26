# WORK ORDER 1234 - Hero card art: re-point the UI, and put the resource path in ONE constant

**Status:** IMPLEMENTED + gate-green - SCREENSHOT/GREYSCALE PROOF OWED (not FIXED/DONE)
**Silo:** Onboarding / UI
**Origin:** Owner supplied new hero art 2026-08-26 and ruled: ***"the point you said is correct and i
am ok if you repoint and use a constrant string for reference"*** -> ***"to start moving to
consistency"***.

---

## What already landed (art is IN — do not re-do it)

`Assets/_Modules/Onboarding/Resources/HeroPortraits/{Sylas,Elara,Thrain,Grom}.jpg` have been
REPLACED with the owner's new art, converted PNG -> JPEG q90 (1501 KB total, vs 9600 KB as PNG —
these ship INSIDE the APK, unlike enemy art which streams from R2). Filenames are unchanged and
already match `HeroSelectController.SlugFor` character for character.

**⚠ THE ART IS NOW A FULL CARD**, not a bare portrait: it carries a gold frame AND a baked
name/role plate ("SYLAS / Ranger"). **And the aspect changed: 832x1248 (2:3) -> 1086x1448 (3:4).**

## Part A — the UI must stop drawing what the art now carries

`HeroSelectController.BuildCenterStage` currently draws all of these around the portrait:

- a `FocalFrame` image — now **doubled** by the card's own gold frame
- a name label from `CanonStrings.Locale(info.NameKey)` and a role label beneath it — now
  **doubled** by the baked plate
- a `Well` at `(0.022, 0.295) -> (0.978, 0.985)` sized for the old **2:3** art; the new art is
  **3:4**, and `preserveAspect = true`, so it will letterbox unless the well is re-proportioned

**⭐ THE ONE THAT LOOKS BROKEN, FIX IT FIRST:** the `LockScrim` is parented to the **well**, so it
covers the portrait only. Elara is the **Cleric, which is LOCKED**. Today that renders a crisp gold
**"ELARA / Cleric"** nameplate sitting cleanly BELOW a greyed `LOCKED / Coming Soon` scrim — the
locked state visibly contradicting itself. **The scrim must cover the whole card, plate included.**

⚠ **Do NOT delete the name/role labels outright.** They are `CanonStrings.Locale(...)` — the
LOCALISED path — and the baked plate is English-only. Removing them makes the screen untranslatable.
Prefer suppressing them for card-style art and say how a future localisation would re-enable them;
if you conclude deletion is genuinely right, argue it explicitly in the RESULT.

## Part B — ONE constant, and this is the part that generalises

The resource path is written out **three times today**:

| Site | Form |
|---|---|
| `HeroSelectController.cs:982` | `Resources.Load<Texture2D>($"HeroPortraits/{slug}")` |
| `HeroSelectController.cs:1116` | `string path = $"HeroPortraits/{SlugFor(hero)}"` |
| `Assets/Editor/Regression/ArtResourceRegression.cs:46-47` | literals `"HeroPortraits/Sylas"`, `"HeroPortraits/Grom"`, ... |

Plus prose copies in `Assets/Editor/HeroPortraitRenderer.cs:2,16,143`.

**This is the exact duplicated-state drift CLAUDE.md records over and over** — the stale WO number
block (§2), the retired dependency table (§5), the hardcoded repo root (§0). Every copy is correct
the day it is written and rots independently.

**Required:** one public named constant — the folder segment — with **every** consumer reading it,
**the regression included**. The regression asserting against the same constant it is guarding is
the point: it stops the list being a fourth copy.

- Put it where both `DeNelle.Onboarding` and `DeNelle.Editor` can see it without a new asmdef
  reference. **READ THE `.asmdef` before choosing the home** (CLAUDE.md §5: the asmdef is the
  authority, not the convenience table).
- Keep `SlugFor` as the id->filename mapping; the constant is the FOLDER, not the whole path.
- ⛔ Do NOT rename the folder or the four files. They are live resource keys and the art is already
  in place.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. **A DEVICE OR EDITOR SCREENSHOT at 2670x1200 of the hero-select screen, opened and looked at**,
   showing (a) a playable hero with NO doubled name and NO doubled frame, and (b) **Elara, locked**,
   with the scrim covering the nameplate. `UI_CAPTURE_OK` alone is not acceptance.
3. A greyscale check — the owner is red/green colourblind; the locked state must read without hue.
4. A regression case asserting **zero** occurrences of the literal `"HeroPortraits/"` outside the
   constant's own declaration. Prove it RED first (WO-1138) — it IS red today, three times over.
5. The RESULT states where the constant lives and why that assembly, citing the `.asmdef`.

## What NOT to touch

- ⛔ The art files. They are installed and owner-approved.
- ⛔ `SlugFor`'s mapping (`Mage->Thrain`, `Knight->Grom`, `Ranger->Sylas`, `Cleric->Elara`).
- ⛔ WO-1083's `HeroStageWell` / close-band work beyond re-proportioning the well for 3:4 — read its
  RESULT first; that screen's overlaps came from ONE cause and must not be re-fragmented.
- ⛔ Which classes are playable. Cleric stays locked; that is owner-ruled ("its a one day thing").
## LANDED-WORK AUDIT (2026-08-26)

The portrait path consolidation landed in `b303c4fbf`: `HeroPortraitPaths.cs` is the shared
authority used by the hero-select and portrait loaders. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83492` `ART RESOURCES OK`; and `:83814`
`REGRESSION_OK 291/291`. Still owed: the 2670x1200 playable/locked-Elara screenshot opened and
inspected, doubled-name/frame checks, greyscale locked-state check, and a RESULT citing assembly ownership.
