# WORK ORDER 1338 - Move the hero bodies and atlases to R2 remote content

**Status:** FIXED 2026-09-03 - SHIPPED and INSTALLED in `2026.09.03.353742`. APK 506 MB -> 457 MB; `Resources/Heroes` 101.8 MB -> 22 MB. Gates `COMPILE_GATE_OK` + `REGRESSION_OK 354/354` (0 red); all five hero bundles and this build's catalog verified **HTTP 200** on R2 by HEAD, not by marker. AWAITING OWNER FELT-VERIFY (check hero art on class-select); PO closes.
**Silo / Lane:** Core / content delivery (Addressables + R2)
**Type:** EXISTING pipeline, content family never migrated
**Minted:** 2026-09-03 (CLI) - ⚠ RETROACTIVELY. See the provenance note below.
**Severity:** P1 - 47.4 MB of the initial download, for assets the player mostly never uses.

> ### ⚠ PROVENANCE - THIS TICKET WAS MINTED AFTER THE WORK SHIPPED, AND THAT IS A PROCESS MISS
> The work landed in commit `d706b430b`, whose message cites **"WO-1187"**. That citation is WRONG:
> WO-1187 is `ps1_encoding_class_has_no_gate` and was CLOSED 2026-08-27. The hero migration had **no
> ticket at all** - it went from an owner ruling straight to a shipped commit, so for a few hours the
> board showed nothing for the single largest change of the day. Minted here on her instruction to
> bring the board up to date for testing. **Do not "correct" the commit message; the history stands
> and this note is the pointer.**

## The owner's ruling

> *"definitely definitely definitely move those models to the R2 - and honestly you're only gonna load
> one of them, so once they select that one, while it's going to that load screen, it loads the model
> and the whole thing, they are ready to go right"*

Earlier, on being told the heroes were still local: *"honestly, between me and you, I already thought
those were in the R2 and addressable because they are so big. It seems insane that they're not."*

**She was right that it was surprising - it was a HALF-FINISHED MIGRATION, not a design decision.**
`Assets/Resources/Enemies` and `Assets/Resources/Structures` no longer exist; both families were moved
to R2 long ago (which is why CLAUDE.md §16 warns a missing push yields capsule enemies). The heroes
were simply never taken along.

**Her reasoning IS the design:** the player only ever loads ONE hero, and the class-select -> load-screen
transition is an existing fetch window. **Zero fidelity cost** - which is why this beat compressing or
decimating her art.

## Result

```
APK                      506 MB -> 457 MB   (-49 MB)
Assets/Resources/Heroes  101.8 MB -> 22 MB
Assets/HeroContent (Remote, content-hashed)  -> 81 MB
```

Remote: `KnightV3`, `knightV2`, `Mage`, `Ranger` + 23 texture atlases, in `Hero_<slug>` and
`Hero_Textures` groups bound to the same `Remote.BuildPath`/`LoadPath` profile variables the live
`Enemy_Art` group uses. **No second content path.**

## ⭐ FOUR FINDINGS, EACH OF WHICH WOULD HAVE SUNK IT SILENTLY

1. **WO-545 had already written the entire grouper - and it created LOCAL groups.** Running it as
   found would have put the bytes straight back into the APK while every marker stayed green.
   Retargeted to Remote.
2. **Both loaders probed Resources FIRST.** `HeroAssetLoader.cs:67` called `Resources.Load` before
   the Addressables probe at `:74`, *despite its own header declaring "Addressables-FIRST,
   Resources-FALLBACK"*. **`HeroTextureLoader.cs` had the identical inverted order** and was in
   nobody's plan - it governs the 43.5 MB of atlases. **Grouping without fixing this would have
   changed NOTHING**, and would have looked like the move simply failed.
3. **The migration broke troops, and the oracle caught it RED:**
   `[troop-controllers] 'troop-shieldguard' points at model 'Knight' but no asset resolves at
   Resources/Heroes/Knight - VisualFactory.Skin returns null and the troop deploys as a CAPSULE`.
   `troops.json` gives `troop-shieldguard` and `troop-echo-legionnaire` model `"Knight"`, and
   `TroopFactory` resolves a troop **body** via `VisualFactory.Skin` -> `StructureAssetLoader`, whose
   Addressables tier is the structure resident cache plus an **editor-only** sync probe. **In a player
   build that path has no Addressables arm at all** and lands on `Resources.Load("Heroes/Knight")`.
   `TroopFactory` never touches `HeroAssetLoader`, so the loader-order fix does not reach it.
   ⛔ **So `Knight.fbx` + `Knight.fbm/` STAY LOCAL, deliberately**, as do all five `.controller` files
   (0.3 MB; `TroopFactory` loads those raw too).
4. **TWO double-ships.** An asset in Resources AND in a bundle ships TWICE - bigger build, all markers
   green.
   - `Hero_Knight` held one entry whose GUID (`aceafb246f74b5a48b1c818de2d5aff2`) is the one in
     `Resources/Heroes/Knight.fbx.meta` - live in both places after Knight was moved back by hand.
     Group and schemas deleted.
   - ⭐ **`Resources` force-includes an asset's whole DEPENDENCY CLOSURE.** Two **orphan** materials in
     Resources pointed at migrated atlases and dragged them back into the APK while R2 also served
     them: `Materials/Ranger.mat` -> `ranger_basecolor` (addressed in `Hero_Textures` - a true
     double-ship) and `Materials/Material_Pbr_Diffuse.mat` -> `KnightV3.fbm/Material_Pbr_Diffuse.png`
     (1.5 MB). Both referenced by NOTHING. Moved GUID-preserving to `HeroContent/LegacyMaterials/`,
     not deleted, fully reversible.

## Fail visibly, never as a capsule

`HeroContentPrewarmer` fetches the chosen hero during `SceneRouter.LoadSceneWithFade` - after the
fade-out, before `LoadSceneAsync` - three attempts, 2 s apart. On final failure the scene load is
**REFUSED** and the player is held on the existing `LoadingOverlay` barrier:

> *"Could not download your Knight. Your hero's artwork is missing, so the game has stopped here
> instead of dropping you in without it. Check your internet connection and tap Retry."*

All words, no colour - the owner is red/green colourblind. `LoadingOverlay.SetRetryOverride` was added
because the barrier's built-in Retry re-probes the **offline** content source and would have reported
success and dismissed **with the art still missing** - a button that lies. The 30 s failsafe is
suppressed while a download is in flight: a 43 MB first fetch legitimately exceeds it, and dismissing
there would uncover a black screen.

## Oracles - narrowed to a documented constraint, never weakened

`HeroRemoteContentRegression` allows **exactly** `Heroes/Knight.fbx` and `Heroes/Knight.fbm/**`,
matched per-slug and exactly, so `KnightV3.fbx` / `KnightV3.fbm/` are still REQUIRED remote. Not a
prefix, not the directory. New group 6 `[no-double-ship]`: nothing under `Resources/Heroes` may also be
in ANY Addressables group - **compared by GUID, because a filename compare would have missed this
entire incident** - and nothing there may depend on an addressed asset in `HeroContent`.
`HeroAddressablesGrouper` gains `KeepLocalSlugs = { "Knight" }`, consulted by BOTH the migrator and the
grouper, so re-running the tool cannot re-break troops or re-create the double-ship.

Proven RED first: 3 of 4 cases failed pre-migration, and the order case failed against HEAD at measured
code offsets (960 < 1186 in `HeroAssetLoader`, 741 < 964 in `HeroTextureLoader`). It reads
comment-stripped source on purpose - a raw-text lint would have scored `HeroAssetLoader`'s own header as
the first `Resources.Load` and passed on every day the defect shipped.

## Flagged, no action

`HeroContent/Textures/knight_basecolor.PNG` is addressed **nowhere** - it lost a duplicate-address race
to the `.JPEG`, which IS in `Hero_Textures`. Addresses are extension-less so only one can win, and no
call site asks for the PNG. Pre-existing.

## Acceptance

- [x] `Resources/Heroes` reduced; heroes served from R2.
- [x] `COMPILE_GATE_OK` + `REGRESSION_OK 354/354` on fresh logs.
- [x] All five hero bundles + this build's catalog return HTTP 200 (verified by HEAD).
- [x] Failure path refuses the scene and states the problem in words.
- [ ] ⛔ **Owner felt-verifies hero art on class-select and CLOSES.** If a hero renders as a pill the
      download failed - but she should never see that, by construction.
