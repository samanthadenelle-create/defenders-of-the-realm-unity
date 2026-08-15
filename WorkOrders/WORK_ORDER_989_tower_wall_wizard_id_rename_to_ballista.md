# WORK ORDER 989 — `tower_wall_wizard` still carries a wizard identity for a structure renamed to Ballista

**Status:** IMPLEMENTED — 2026-08-15 `tower_ballista` + CatalogRegistry alias + load rewrite; art path still WizardTower_1
**Minted:** 2026-08-14 (CLI)
**Silo:** Catalog / data identity
**Source:** OWNER ASK, 2026-08-14 — *"tower_wall_wizard - Where did that name come from? Should match Ballista"*

---

## Where the name came from (traced, not guessed)

The id dates from the **original** build-catalog commit `9de2aac56`
(*"feat: build-catalog JSON-driven + HP-bars hide-until-engaged + gate-wall seam + night torches + Day-1 quest"*).
At that point it genuinely **was** a wizard tower. The name was correct when written.

On **2026-07-08** the owner ruled the model is a ballista. That ruling is quoted in the row's own
`orientation.note`:

> *"manual — owner ruling 2026-07-08: the model IS a ballista (renamed); Tripo fbx stands upright with
> the standard X-90 (supersedes the dialed (0,45,-90))."*

The row was retuned to match, and today reads:

```json
"id":               "tower_wall_wizard",
"displayName":      "Ballista",
"visualPrefabPath": "Structures/WizardTower_1",
"repo": { "element": "None", "projectileStyle": "bolt", "behaviorId": "DefenseTower", ... }
```

**The display name and the stats were renamed. The identity never was.** The `id` and the
`visualPrefabPath` are the last two fields still calling it a wizard.

## Why this is worth a ticket rather than a shrug

It has already cost real work. On 2026-08-14, WO-947 (cost-basket separation) classified this row as
**MAGICAL from its id** and proposed a crystal basket. The row's data said the opposite — Ballista,
element None, projectileStyle bolt. **The two readings sent 70 crystals in opposite directions**, and
resolving it consumed an owner pin (*"thats a baliista mechanical"*) to answer a question the data had
already answered.

A stale identity is not cosmetic. It **actively misroutes downstream work** — the same failure as the
stale WO-number block (CLAUDE.md §2) and the hardcoded repo root (§0): a value that was accurate when
written, that the project outgrew, and that keeps re-seeding the error it caused.

## ⛔ This is NOT a find-and-replace

Two facts make a bare rename destructive:

**1. The id is referenced in 15 files:**

```
Assets/Editor/Regression/CostBasketSeparationRegression.cs
Assets/Editor/Regression/TowerProjectileMapRegression.cs
Assets/Editor/CatalogPrefabImporter.cs
Assets/Editor/RegressionSuite.cs
Assets/Editor/VfxProofCapture.cs
Assets/Editor/WoodenWatchtowerBuilder.cs
Assets/Resources/Data/Canonical/structures-catalog.json
Assets/StreamingAssets/Data/Canonical/structures-catalog.json
Assets/Tests/EditMode/BuildMenuVMTests.cs
Assets/_Modules/Core/Catalog/RepoProps.cs
Assets/_Modules/Village/Buildings/DefenseTower.cs
Assets/_Modules/Village/BuildMode/BuildModeController.cs
Assets/_Modules/Village/Catalog/CatalogBootstrap.cs
Assets/_Modules/Village/Catalog/StructureFactory.cs
Assets/_Modules/Village/VisualFactory.cs
```

**2. Catalog ids are PERSISTED.** Save schema **v36** added `everBuiltStructureIds`, and base layouts
replay **by id**. A bare rename silently orphans every saved town holding one: the layout replays an id
the catalog no longer knows. The player loses a built structure and nothing reports why.

## The required shape — read-migration, following this project's own precedent

Canon already records the pattern. The persisted token grammar change (`harvest:3` → `wood:3`, WO-830)
was **read-migrated with no schema bump**. Do the same here:

- New canonical id: **`tower_ballista`**.
- The loader **aliases the old id on read**: a save or layout carrying `tower_wall_wizard` resolves to
  `tower_ballista`. Old saves keep working; new writes use the real name.
- The alias is **deletable** once no live save carries the old id — so record where it lives and what
  would justify removing it, rather than leaving an undated permanent shim.
- **Instrument the alias (§12 / §1.4b).** When the alias fires, log it — naming the old id, the new id,
  and where it came from (save vs layout vs catalog). Without that line there is no way to know whether
  any live save still needs it, and the shim becomes immortal by default. A silent alias is how the
  migration never ends.

## The prefab path — a separate, smaller decision

`visualPrefabPath: "Structures/WizardTower_1"` is the other half. Renaming the **asset** touches art and
its `.meta` (GUID stability matters — rename via the editor, never by hand on disk). Options, in order
of preference:

1. Rename the path with the id, keeping the GUID, so nothing anywhere still says "wizard".
2. Leave the asset and **flag it in the row** with a dated note, if the art rename is not worth the churn.

Do **not** silently leave it unflagged — that reproduces this exact ticket in six months.

## Acceptance criteria

- No catalog row, code reference, or test refers to `tower_wall_wizard` except the deliberate alias.
- Both catalog copies updated and **byte-identical** (verify md5 — `Resources/` WINS at runtime; this is
  the drift trap that hit all 7 dungeon layouts on 2026-08-14).
- A save written **before** the rename loads with the structure intact, and the alias trace line fires
  naming old id → new id. **Prove this with an actual old save, not by reading the loader.**
- A save written **after** the rename contains only the new id.
- `COMPILE_GATE_OK`, and the existing suites that name this id are updated in the same change.

## Sequencing

⚠ **Land this AFTER the WO-947 completion lane**, which is editing these same catalog rows
(the five owner-ruled cost baskets). Two lanes on the same rows will collide.

## What NOT to touch

- The **cost basket**. WO-947 settled it: this row is REGULAR (wood + iron) per the owner's
  *"thats a baliista mechanical"*. Do not revisit it while renaming.
- WO-947's note recording that **the data won over the id**. Keep it even once the id agrees — the
  record of why it was ambiguous is what stops the next reader re-litigating it.
- Any `.unity` scene file.
