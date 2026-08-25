# WORK ORDER 902 — Archer Tower L1–L3 medieval castle visual swap (Option A)

> # ⚠ SUPERSEDED 2026-08-06 — DO NOT IMPLEMENT
>
> **Replaced by the owner's ALL-WOOD archer ladder.** Implementing this WO today would *revert* a
> newer owner ruling and swap the owner's own commissioned art back out for polyperfect castles.
>
> **Proof, from the live catalog rather than from memory** — `structures-catalog.json`, row
> `tower_ground_archer`, field `_bug22`:
>
> > *"SUPERSEDED 2026-08-06 by the owner's ALL-WOOD ladder ('New Wooden Tower Level 1', then L2 and
> > L3): visualPrefabPath + both upgradeVisualPath steps are now Tower_Wooden_Watchtower / _L2 / _L3
> > — owner-sourced Tripo art staged under Resources/Structures and built by
> > DeNelle.Editor.WoodenWatchtowerBuilder.Build."*
>
> The two are in direct opposition: this WO routes L1–L3 to `Tower_Castle_Round` /
> `Tower_Castle_Square` / `Tower_Medieval_Big`, and §2 explicitly **bans** the wooden look
> (*"Do NOT re-use Tower_Medieval_Wood — BUG#22 lumber-pile look — banned for archer"*). The catalog
> is the newer ruling and it wins.
>
> **Body left intact** per CLAUDE.md §15 — dated WOs are frozen; a superseded one gets a banner, not
> a rewrite. It is kept for provenance: it records *why* the Tribal ladder was rejected, which is
> still true and still useful.
>
> **Still open, and tracked separately — the wooden ladder is not finished.** Two defects were
> captured with runtime data on 2026-08-07 and neither is fixed:
> - **L3 binds NO ALBEDO.** Runtime line: `NO ALBEDO on 'Tower_Wooden_Watchtower_L3(Clone)' …
>   tint=(0.60,0.58,0.54)`, while L1 binds correctly.
> - **L3's baked rotation is stripped.** `VisualFactory` forces `localRotation = identity`, so
>   `euler=(270,0,0)` becomes `(0,0,0)`. The owner's OffsetForge fix (`rot.x = -90`) is committed and
>   correct but cannot take effect until that strip is addressed.
>
> Fixing those belongs to the wooden ladder, **not** to this WO. Do not reopen this one to do it.

**Status:** SUPERSEDED — do not implement (was: READY TO IMPLEMENT)  
**Minted:** 2026-08-04 (CLI / Grok — owner: hates Tribal archer look; ruled **Option A** castle ladder)  
**Silo:** Structures / catalog art (data + Resources mirror only)  
**Roles:** CLI or Claude-with-code seat — **no combat logic rewrite**  
**Numbering:** main line next free was **902** (901 = collector loop umbrella; 860–899 UI seat reserved)

---

## Owner ruling

Replace the **Tribal** archer tower ladder with the **medieval castle** ladder:

| Level | New visual (Resources path) | Source prefab if missing |
|-------|-----------------------------|---------------------------|
| **L1** | `Structures/Tower_Castle_Round` | Already in `Assets/Resources/Structures/` |
| **L2** | `Structures/Tower_Castle_Square` | Polyperfect `_M/Prefabs_M/Medieval_M/Tower_Castle_Square.prefab` — **must mirror into Resources** if absent |
| **L3** | `Structures/Tower_Medieval_Big` | Already in `Assets/Resources/Structures/` |

**Do NOT** re-use `Tower_Medieval_Wood` (BUG#22 lumber-pile look — banned for archer).

---

## Current state (before)

`tower_ground_archer` in dual-copy `structures-catalog.json`:

- `visualPrefabPath`: `Structures/Tower_Tribal_Tier1`
- `repo.upgradeVisualPath`: `[Structures/Tower_Tribal_Tier2, Structures/Tower_Tribal_Tier3]`
- `repo.maxLevel`: 3

Live upgrade reskin already follows `upgradeVisualPath` / StructureTierVisual pattern — **only paths change**.

---

## Scope

### 1. Resources mirror (if needed)

1. Confirm on disk:
   - `Assets/Resources/Structures/Tower_Castle_Round.prefab` — expect **present**
   - `Assets/Resources/Structures/Tower_Castle_Square.prefab` — expect **missing** (only under polyperfect Medieval_M)
   - `Assets/Resources/Structures/Tower_Medieval_Big.prefab` — expect **present**
2. If Square missing: copy from  
   `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/Tower_Castle_Square.prefab`  
   into `Assets/Resources/Structures/` (same pattern as prior Tribal import / CatalogPrefabImporter).  
   Fix URP materials if pink (`Defenders/Art/Fix Polyperfect URP Materials` if project menu exists).  
3. Do **not** hand-edit `.unity` scenes.

### 2. Catalog dual-copy (binding edit)

Both files, byte-identical field:

- `Assets/Resources/Data/Canonical/structures-catalog.json`
- `Assets/StreamingAssets/Data/Canonical/structures-catalog.json`

On entry `id: "tower_ground_archer"`:

```json
"visualPrefabPath": "Structures/Tower_Castle_Round",
"repo": {
  ...
  "maxLevel": 3,
  "upgradeVisualPath": [
    "Structures/Tower_Castle_Square",
    "Structures/Tower_Medieval_Big"
  ]
}
```

Update `_bug22` / notes on that row to record: **owner 2026-08-04 Option A medieval castle ladder (Tribal retired for archer).**

Leave `heightMul`, range/damage, costs, placement **unchanged** unless a scale bug appears in play (then one heightMul tweak only).

### 3. Orientation / scale play-check

After swap, place L1 and upgrade L2/L3 in hub:

- Not sideways (euler/orient tool if needed — follow house Orient-tool, no random scene edits)
- Footprint still fits ground tower placement
- L3 not absurdly huge vs L1 (if yes, slight `heightMul` or scale note — prefer catalog scale fields already on row)

### 4. Portraits (optional same PR)

`Assets/Resources/Portraits/archer-tower.png` (+ `-2` / `-3`) may still show old art.  
**Optional:** flag for UI capture later; **not blocking** if mesh swap is correct.

### 5. Do NOT touch

- Other towers (ballista, catapult, arcane spire, cathedral)
- Tower combat DPS / costs (WO-855 economy if separate)
- Tribal prefabs themselves (leave on disk for outposts / other uses)
- Combat scripts

### 6. Verify

- [ ] `Resources.Load("Structures/Tower_Castle_Round")` etc. non-null (or equivalent runtime path)
- [ ] Fresh place shows Round
- [ ] Upgrade to L2 → Square; L3 → Medieval Big
- [ ] Dual-copy catalog parity
- [ ] COMPILE_GATE_OK if any .cs touched (prefer **zero** .cs)
- [ ] No pink materials on new Square import

### 7. RESULT

Short `WORK_ORDER_902_archer_tower_medieval_castle_visuals.RESULT.md`: before/after paths + screenshot note.

---

## Acceptance

- Owner no longer sees Tribal T1–T3 for archer tower in hub  
- L1/L2/L3 are Castle Round → Castle Square → Medieval Big  
- Train/build/upgrade **logic** unchanged  
- Dual-copy JSON + Resources loadable  

---

## Paste for Claude / CLI

```text
Implement WORK_ORDER_902_archer_tower_medieval_castle_visuals.md.
Archer tower Option A: L1 Tower_Castle_Round, L2 Tower_Castle_Square, L3 Tower_Medieval_Big.
Mirror Castle_Square into Resources/Structures if missing (from polyperfect Medieval_M).
Update BOTH structures-catalog.json copies only — no combat rewrite. Do NOT use Tower_Medieval_Wood.
Fix pink URP if needed. COMPILE only if .cs touched. Dual-copy parity.
```

---

## One-line truth

**Retire Tribal archer looks; castle Round → Square → Big via catalog paths + one Resources mirror.**
