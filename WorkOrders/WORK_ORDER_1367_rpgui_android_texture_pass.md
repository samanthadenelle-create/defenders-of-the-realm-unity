# WORK ORDER 1367 - Finish the Android texture pass on Resources/RpgUi (the 10.5 MB, and then some)

**Status:** IN PROGRESS 2026-09-04 - owner authorised autonomous execution; quality tier RULED (§5)
**Silo / Lane:** Art pipeline / texture import settings - `Assets/Resources/RpgUi/**` `.meta` only
**Type:** EXISTING art, incomplete import pass
**Minted:** 2026-09-04 (CLI)
**Closes:** Gate A of WO-1362 (the Play upload blocker). Runs in its own lane; touches no `.cs`.

## WHY THIS EXISTS

The Google Play AAB is **over the upload ceiling**, measured 2026-09-04 with bundletool:

```
$ bundletool get-size total --apks=...
MIN,MAX
510443276,510523099          vs a 500,000,000-byte ceiling  =  OVER BY ~10.5 MB
```

⭐ **The owner identified the cause directly** - *"that new weight you mentioned was the UI redo"*.
That matches the record: every build log from 2026-09-01 is named `ui-reskin-*`, and the AAB grew
+31.2 MB between the 08-30 RC (482,843,623 B) and 09-01 (514,062,537 B). **WO-1365's "find the 31 MB"
is ANSWERED - do not spend a session investigating it.**

## ⛔ FIRST, THE THING NOT TO DO: RpgUi IS NOT DEAD ART

The owner asked whether `Resources/RpgUi` (96.58 MiB) is still used *"since we switched to a custom
made one"*. **It is used, and deleting it would break the entire UI.**

`Assets/Editor/BlinkUiImporter.cs:3-4` states the relationship verbatim: sprites are written to
`Resources/RpgUi/<role>/<canonical>.png` *"so the EXISTING sprite-first UI kit (RpgUiCatalog /
ElarionUiKit) re-skins the ENTIRE game UI - store, equip, ..."*.

**`ElarionUiKit` is the code-built LAYOUT layer; `RpgUi` is where its ART comes from**, resolved at
runtime through `RpgUiCatalog.Get(role, name)`. 103 `.cs` files reference `RpgUi`.
⛔ **The custom kit did not replace RpgUi - it consumes it.** This ticket changes IMPORT SETTINGS
only. **No PNG is deleted, no source art is edited, no dimension of authored art changes on disk.**

## THE MEASURED STATE - `Assets/Resources/RpgUi`, 568 PNGs

| Fact | Value |
|---|---|
| Folder total on disk | **96.58 MiB** (86.33 MiB `.png`, 9.88 MiB `.asset` TMP fonts) |
| Est. current build footprint of the PNGs | **85.81 MiB** |
| `textureCompression: 0` (**UNCOMPRESSED** -> RGBA32) | **202 of 568** |
| **No Android platform override at all** (`overridden: 0`) | **405 of 568** |
| Has an Android ASTC override (`textureFormat: 50`) | 163 of 568 |
| `maxTextureSize: 2048` | **568 of 568 - every file, including icons** |

By role: `spellicons` 23.67 · `troop` 20.25 · `frame` 10.01 · `font` 9.88 (`.asset`) · `emblem` 6.60
· `panel` 6.47 · `currency` 2.60 · `crown` 2.53 · `classslot` 2.52 · `hud` 2.46.

**So the RC doc's *"conservative Android texture pass, 65 eligible overrides"* was only ever partial.
163 files carry an override today; 405 do not, and 202 of those are importing UNCOMPRESSED.**

## THE PROJECTION - computed from image dimensions x target format

| Option | Est. build bytes | Saving vs today |
|---|---|---|
| Today | 85.81 MiB | - |
| **(A) ASTC 6x6 on all, dimensions UNTOUCHED** | **58.88 MiB** | **26.9 MiB (2.5x the gap)** |
| (B) ASTC 6x6 + cap at 512px | 22.68 MiB | 63.1 MiB (6x the gap) |

⚠ **These are ESTIMATES from `width x height x format`, not from a built artifact.** The only proof
is a rebuild + `bundletool get-size`. Two known imprecisions, stated rather than smoothed:
`maxTextureSize: 2048` already clamps the four 2779x1843 files (`panel/panel_talent.png`,
`frame/frame_talent.png`, `decoration/deco_talent_1.png`, `deco_talent_2.png`), so "today" is
slightly overstated; and the "compressed" default for the 366 non-uncompressed files was modelled at
~8 bpp, which varies by actual format.

⭐ **Option A alone closes the gap 2.5x over without resizing a single piece of the owner's art.**
Start there. Do NOT reach for Play Asset Delivery - see WO-1365's ranking.

## ⛔ THE WORK IS `.meta` FILES ONLY

Set the **Android platform override** on the 405 PNGs that lack one: `overridden: 1`, an ASTC format,
and a role-appropriate `maxTextureSize`. Prefer driving this from the existing importers
(`Assets/Editor/BlinkUiImporter.cs`, `BlinkIconImporter.cs`, `RpgUiImporter`) which already own
`ForceSpriteImport` settings - ⛔ **do not hand-edit 405 `.meta` files, and do not write a second
importer.** Extend the one that already exists (PREFLIGHT Gate A item 3).

⚠ **`.meta` files are GUID-bearing.** Change import settings inside them; never regenerate or delete
one. A lost `.meta` re-GUIDs the asset and silently breaks every reference to it.

## §5 - THE QUALITY TIER - ⛔ DELEGATED TO THE LEAD AND RULED 2026-09-04

Owner, verbatim: ***"be smart and do what is best. Run autonoumously and try to get that build size
down, also confirm the exact values play store allows"***.

**She delegated this call rather than making it, so the lead ruled it - and the ruling is recorded
here so it is auditable and reversible, not buried in a commit:**

| Roles | Format | Why |
|---|---|---|
| `frame` `panel` `slot` `button` `classslot` `bars` `hud` | **ASTC 4x4** | 9-sliced chrome with sharp edges, where block artifacts read as *damage* rather than softness |
| `spellicons` `troop` `emblem` `currency` `crown` `abilities` `element` `decoration` `icons` `prefab_deps` | **ASTC 6x6** | Illustrative art; the eye forgives block compression here |

Dimensions are **NOT** changed (Option A). No source art is edited or deleted.

⚠ **This is a delegated call, not an owner preference.** If she dislikes the result, the lever is
one line per role - re-run at 4x4 everywhere for maximum quality, or 8x8 for maximum saving. ⛔ **The
felt-verify is still hers and it is still required** - see the acceptance box below.

ASTC block size is a **visual quality** trade, and this is the owner's brand-new UI art:

| Format | Bytes | Character |
|---|---|---|
| ASTC 4x4 | ~2x of 6x6 | Highest quality; safest for 9-sliced frames + crisp edges |
| **ASTC 6x6** | baseline above | Standard mobile UI; the projection uses this |
| ASTC 8x8 | ~0.55x of 6x6 | Smallest; visible softening on sharp icon edges |

⚠ **SUPERSEDED LINE REMOVED:** this section previously read *"Do not pick this alone (SAMANTHA.md
rule 8 - creative calls are hers)"*. That was correct when written and is now **wrong** - she
explicitly delegated it above. Kept as a note rather than silently deleted, because the underlying
rule still stands for every OTHER creative call in this ticket.

⚠ **The owner is red/green colourblind** - ask about **crispness, edge artifacts and banding**, never
about hue shift. And she is the only one who can judge it.

## ACCEPTANCE

- [ ] ⛔ **Proven by a REBUILD + `bundletool get-size total`, quoting MIN,MAX before and after.**
      An estimate does not close this ticket - the estimate is what the ticket already has.
- [ ] The measured download total is **under 500,000,000 bytes**, with the margin stated.
- [ ] ⛔ **The owner has seen the UI on device and accepted the quality.** A green size number with
      degraded art is a worse outcome than being over the ceiling. `UI_CAPTURE_OK` proves a panel
      RENDERED, never that it looks right - **open the PNGs, then hand it to her**
      (memory: `screenshots-are-primary-evidence-for-visual-defects`).
- [ ] Zero `.cs` changed. Zero PNGs deleted. Zero source dimensions changed (Option A).
- [ ] No `.meta` regenerated or GUID changed - `git diff --stat` shows only import-setting lines.
- [ ] The settings are applied by the EXISTING importer, so a re-import cannot silently revert them.
      ⚠ This is the whole reason the last pass evaporated: **65 overrides in the RC, 163 today, 405
      still missing** - a hand pass that new art does not inherit is a pass that decays. Make new art
      inherit it by construction.

## WHAT NOT TO TOUCH

- ⛔ Do not delete or trim `Resources/RpgUi` - `ElarionUiKit` renders the whole game UI from it.
- ⛔ Do not reach for Play Asset Delivery. It moves 423.94 MiB of `bin/Data` out of the base module
      and collides with our custom R2 remote `LoadPath`, which has **no local fallback** (§16).
- ⛔ Do not chase R8 or managed stripping for this - all three DEX files total 8.33 MiB compressed,
      less than the gap, and `Assets/link.xml` already `preserve="all"`s every runtime assembly
      deliberately. See WO-1365's ranking.
- ⛔ Do not touch `Resources/VFX` (89.70 MiB) in this ticket. It is a real question - canon records
      ~23.85 MB mirrored there in the 08-06 gitignored-art fix and it is now ~4x that - but it is a
      different lane and a different risk profile. Separate ticket if it is wanted.
