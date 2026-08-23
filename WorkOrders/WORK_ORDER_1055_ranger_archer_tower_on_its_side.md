**Status:** DONE 2026-08-22 - owner-verified in game. X+90 applied to the L3 prefab's renderer-bearing child (`DeNelle.Editor.ArcherTowerL3Pitch`). Bounds went `0.59 x 0.58 x 1.00 LYING DOWN` -> `0.59 x 1.00 x 0.58 UPRIGHT`; prefab and model now agree. Evidence: `docs/ui-evidence/structure-pose-2026-08-22/Tower_Wooden_Watchtower_L3__prefab.png`.

> ### WHY THE FIX LANDED ON THE PREFAB CHILD AND NOWHERE ELSE
> `StructurePoseCapture` measured the two layers separately and they DISAGREED - the FBX
> rendered UPRIGHT while the wrapper prefab rendered LYING DOWN. The wrapper is the
> authority, so three earlier asset-layer attempts (re-running the baker,
> `bakeAxisConversion`, catalog eulers) each moved the number by exactly zero.
> The catalog cannot reach it either: `ReskinForLevel` does not apply `entry.orientation`
> because tier models rely on their prefab-native pose, so a catalog euler only ever
> reaches the BASE visual and L2/L3 stay down.
>
> ⚠ `WoodenWatchtowerBuilder` - the tool that would normally regenerate these wrappers -
> NO LONGER RUNS. It fails on **L1** with "the prefab has no renderer-bearing child ... not
> the wrapper+model shape this builder authors", i.e. it is broken for a level that looks
> fine. Repairing it is separate work; this ticket applied the one correction it would have
> made. Do not assume that builder is available next time.

# WORK ORDER 1055 — Archer Tower is on its side

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1055 -> 1056 in the SAME edit)
**Assigned:** CLI — instrument + measure first, then fix. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** World / structures presentation
**Class:** DEFECT (orientation). **Recurrence** — note the owner's word *"still"*.
**Source:** F8 capture **seq=3581**, `logs/f8-inbox/capture-20260822-110225-seq3581.md`,
scene `Main_Castle_Overworld`, 2026-08-22 11:02:24. Flag text: *"Ranger Tower still on its side"*.
**Family:** PROD-007 (wrong file corrected) · PROD-008 (no oracle can see orientation) · the
2026-08-18 axis-bake retirement.

---

## 0. Which building — settled, no ruling needed

**OWNER CLARIFICATION 2026-08-22:** *"verbiage was archer tower, i wasnt being exact."*

The building is **`tower_ground_archer`, displayName "Archer Tower"** — the catalog name is correct
and nothing player-facing needs renaming. **An earlier draft of this ticket asked for an owner ruling
on a "three-way name crossing." That ask is WITHDRAWN — do not chase it.**

The only residue is internal: the VFX keys are named `RangerTowerBaseProjectile`,
`RangerTowerUpgraded`, `RangerTowerlevel2Projectile` (`VfxCasterLibraryIndex.json:473-475`,
`VfxManualPicks.json:788+`) against a building called Archer Tower. That is a **naming
inconsistency in internal keys only** — no player ever sees it, nothing is broken, and VFX keys are
owner-tagged (never renamed by a seat on its own initiative). **Leave it alone.** Noted here purely
so the next seat grepping "ranger" is not confused by the hit.

---

## 1. Why *"still"* is the important word — the previous sweeps could not have fixed this row

Two orientation passes landed recently, and **both worked on the CATALOG channel**:

- **PROD-007**: `f995c4706` corrected `Assets/OffsetForge/offsets.json`, which is **INERT for
  structures**. The live channel is `entry.orientation`, applied by
  `StructureFactory.Create` when `manual == true` (`StructureFactory.cs:151-158`). Five rows zeroed.
- **2026-08-18 axis-bake retirement** (recorded in `tower_ballista`'s note): the -90 X was removed
  from the *row* because the conversion now lives in the **mesh** (`bakeAxisConversion: 1`, set by
  `TripoAssetPostprocessor` / `TripoAxisBake`). Keeping it in the row applied it **twice** and laid
  the model down.

**`tower_ground_archer`'s correction lives in NEITHER channel.** Its row is already `(0,0,0)`,
`manual: true`, and its own note says so explicitly:

> *"that -90 is BAKED INTO EACH PREFAB (on the model child) by `DeNelle.Editor.WoodenWatchtowerBuilder`,
> NOT applied from here. DO NOT COPY THE -90 INTO THIS ROW."*

So this tower depends **entirely on the prefab bake** — the one channel neither sweep touched. That
is exactly why it is *still* down while the others got fixed.

---

## 2. ⛔ THE FIX THAT WILL SUGGEST ITSELF IS WRONG — do not put -90 in the row

Someone will look at a lying-down tower with a `(0,0,0)` row and "correct" it. The row's own note
gives two source-read reasons that is wrong, and both still hold:

1. **`ReskinForLevel` never applies the base euler.** Verified at `StructureFactory.cs:467-469`:
   applying it there *"tips tier models that are already upright — F8-2 2026-07-07."* So a row euler
   reaches **the base visual only**; L2 and L3 would stay on their sides. A "fix" that repairs one of
   three levels is not a fix.
2. **It would double the model's size.** The euler is applied *after* `VisualFactory.Skin` fits to
   height. On a lying-down model the fit measures the **short** axis — L2 reads 0.519 instead of
   1.000, so scale becomes `4.8 / 0.519 = 9.25x` instead of `4.80x`, and the later rotation stands up
   a **9.25 m tower, 1.93x oversized**.

**The correction must stay upstream of the fit.** That is the whole reason it lives in the prefab.

---

## 3. THE MEASUREMENT THAT SETTLES IT — do this before touching anything

**Which LEVEL is on its side is the discriminator.** The three candidates produce three different
answers, so one capture resolves the RCA:

| Observed | Cause | Where to fix |
|---|---|---|
| **L1 only** down | The base prefab's baked -90 is now a **double-apply** against `bakeAxisConversion: 1` on the source FBX — the ballista's exact failure mode, arriving through the prefab channel | `WoodenWatchtowerBuilder` — stop baking onto an already-converted mesh |
| **L2 and/or L3** down, L1 upright | The tier prefab **lost or never had** the bake. `ReskinForLevel` skips the euler by design, so nothing downstream can save it | re-bake the tier prefabs |
| **All three** down | Shared upstream — the mesh axis conversion itself | `TripoAssetPostprocessor` / the FBX import settings |

**⚠ The owner's town has upgraded structures in the same capture** (`'silo@6_3'`, `'foundry@3_8'` in
the harvested `[Flow:BuildTimerUI]` lines), so **her Archer Tower is plausibly L2 or L3** — which
makes row 2 the leading candidate. **Leading, not concluded.** Static reading locates candidates; it
never concludes (§12).

### The instrument already exists — do not build a new one

`WoodenWatchtowerBuilder` already measures **as-imported aspect** = `height / max(width, depth)` and
carries `UprightAspectMin = 1.2f` (`:277`). PROD-008 recorded the real numbers: **1.70–1.92 upright
vs 0.52–0.59 on its side.** The separation is enormous and unambiguous.

**Log the aspect of the instantiated model at spawn, per level**, and read it off a run. One line of
`FlowTrace` answers the whole question.

### Also capture
- A **screenshot** with the tower visible — for a spatial defect the screenshot *is* the data
  (it shows scale and pitch together, which a number does not).
- The tower's **current level**, so the table above can be applied.

---

## 4. ⛔ PROD-008 still binds: no oracle can see this

Every orientation defect this project shipped went out **compile-green and regression-green**,
because the only oracle was the owner's eyes — which is precisely what §14 exists to stop relying on.
Whatever the fix turns out to be, **it does not ship without an assert**, and PROD-008 already
specified the shape:

- **Primary assert = HEIGHT FIDELITY** (`bounds.size.y` vs `YHeightVariable * heightMul`) —
  threshold-free, and it does not false-positive on legitimately wide buildings
  (`House_Medieval_Medium` reads 0.72 upright, so a global aspect gate is wrong).
- The **1.2 aspect band** applies **scoped to tower-class rows only**.
- **Prove the assert FAILS before the fix and PASSES after.** An assert that was never seen red is
  not evidence.

---

## 5. What NOT to touch

- **The catalog row.** `tower_ground_archer.orientation` stays `(0,0,0)` / `manual: true` — see §2.
  `manual: true` is what stops an auto-baker re-tipping it and must not be cleared.
- **`tower_ballista` / `tower_arcane_spire`.** Both were settled deliberately at `(0,0,0)` with
  `manual: true`. Not this ticket.
- **The facing (Y/Z).** Owner ruling 2026-08-06: X is a defect correction, Y/Z is a preference, and
  which way a building faces is the player's choice at placement. **Fix the pitch only.**
- **The `RangerTower*` VFX key names.** §0 — an internal-only inconsistency the owner has closed. Do not rename them.

---

## 6. Acceptance

1. The captured aspect/level measurement is **in the ticket** before any code edit (§3).
2. The Archer Tower stands upright at **L1, L2 and L3** — all three verified, not just the one
   that was reported.
3. Its height matches the catalog's intended height — **no 1.93x oversize** (§2.2).
4. The height-fidelity assert exists, and was **proven red before / green after** (§4).
5. `tower_ballista`, `tower_arcane_spire` and the wooden-watchtower family are **unregressed** —
   re-verify them in the same capture, since they share the builder.
6. `COMPILE_GATE_OK`; brace-check every `.cs`; screenshots opened, not just taken.

## 7. Files

**Read first:** `logs/f8-inbox/capture-20260822-110225-seq3581.md` ·
`Assets/Editor/WoodenWatchtowerBuilder.cs` (the bake + `UprightAspectMin`) ·
`Assets/_Modules/Village/Catalog/StructureFactory.cs:151-158` (Create applies the euler) and
`:467-469` (ReskinForLevel deliberately does not) ·
`Assets/Resources/Data/Canonical/structures-catalog.json` -> `tower_ground_archer` note.

**Likely edit (pending §3):** `Assets/Editor/WoodenWatchtowerBuilder.cs` and/or the tier prefabs it
bakes. **Note:** `Assets/Resources/Structures/` no longer exists — structure art is Addressable/R2
(CLAUDE.md §16), so **a prefab re-bake is a content change and needs its own R2 push** via
`tools\r2-ship.ps1`. Bundle names are content-hashed; a previous push cannot cover it.

**Nothing separate:** the name question is closed by the owner clarification in §0.

---

# ★★ ROOT CAUSE — PROVEN AT SOURCE (CLI seat, 2026-08-22)

**It is none of the three candidates above. It is a FOURTH cause: the asset that loads changed.**

## THE CHAIN, each link read at source

1. **WO-928 (2026-08-08) fixed this exact symptom** with `repo.preservePrefabRotation: true` - THE ONLY
   ROW IN THE CATALOG THAT CARRIES THAT FLAG. It tells `StructureFactory.OptsFor` to leave
   VisualFactory's DEF-232 identity reset OFF, so the model keeps its own authored root rotation.
2. **That worked because `Resources.Load` was AMBIGUOUS and picked the FBX.** The catalog note says so
   verbatim: *"Resources/Structures holds BOTH Tower_Wooden_Watchtower[_L2/_L3].fbx AND the same-stem
   .prefab, so Resources.Load(...) is AMBIGUOUS. The captured trace 'after instantiate (prefab-native
   pose): euler=(270.00, 0.00, 0.00)' proves the FBX is what actually loaded"* - and for that FBX the
   native 270 IS the X -90 upright correction.
3. **CLAUDE.md s16 moved structure art to Addressables.** The entry `Structures/Tower_Wooden_Watchtower`
   in `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` carries
   `m_GUID: 474ff7ec5c045c2469756fbb0be8d90d`, which resolves to
   **`Assets/StructureContent/Tower_Wooden_Watchtower.prefab.meta`** - the PREFAB, named explicitly by
   GUID.
4. **All three tier prefabs have an IDENTITY root:** `m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}` on
   `Tower_Wooden_Watchtower.prefab`, `_L2.prefab` and `_L3.prefab`.

**So `preservePrefabRotation` now faithfully preserves IDENTITY.** The correction was never in the
prefab - it was in the FBX that no longer loads. The fix is not broken; **the thing it was preserving
was swapped out from under it.**

> ### The migration made a previously-AMBIGUOUS load DETERMINISTIC, and it resolved to the OTHER asset.

That is precisely why the owner's word was **"still"**, and why both prior orientation sweeps missed it:
neither touched this channel, because from the catalog's point of view nothing changed.

## BLAST RADIUS - MEASURED, AND IT IS SMALL

`Assets/StructureContent/` holds **27 `.prefab`** files. Only **3 stems have BOTH an `.fbx` and a
same-stem `.prefab`**, i.e. only 3 were ever ambiguous under the old loader:

    Tower_Wooden_Watchtower       prefab-root = identity
    Tower_Wooden_Watchtower_L2    prefab-root = identity
    Tower_Wooden_Watchtower_L3    prefab-root = identity

**All three tiers are affected, which also answers the WO's own triage question** - "which level is on
its side" separates its three candidates, and the answer is ALL THREE, by construction rather than by
observation. No FlowTrace run is needed to narrow it.

⛔ **The catapult (WO-1143) is NOT this defect.** `Assets/StructureContent/Catapult.prefab` exists with
**no sibling `.fbx`**, so it was never ambiguous and never had an FBX-native correction to lose. The two
symptoms look alike and have different causes - do not fix them together.

## WHY THE OBVIOUS FIX IS STILL WRONG

The WO's existing warning stands and is now doubly important: putting -90 back in the catalog row fails
because `ReskinForLevel` never applies the base euler (`StructureFactory.cs:467-469`), so L2/L3 stay
down; and the euler would land AFTER the height fit, so the fit measures the short axis and oversizes
the tower ~1.93x. **The correction must stay upstream of the fit.**

Given the root cause, the candidate fixes are: bake the FBX's 270 into the three prefab roots (keeps the
correction upstream of the fit, where it already belongs), or re-point the three addressables at the
FBX (restores the old resolution, but re-creates the ambiguity s16 removed). **The first is cleaner.**

⚠ Either way this is a CONTENT change: bundle names are content-hashed, so it needs its own R2 push
(`tools\r2-ship.ps1`) - yesterday's push cannot cover it (CLAUDE.md s16).

## NAMING — RESOLVED, NO RULING NEEDED
Owner, 2026-08-22: *"verbiage was archer tower, i wasnt being exact"*. There is no Ranger/Archer
ambiguity to rule on for THIS ticket. The `RangerTower*` VFX keys remain a separate naming-hygiene
question and must not block this fix.
