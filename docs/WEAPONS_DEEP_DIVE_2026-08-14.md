# Weapons — Deep Dive, 2026-08-14

**Frozen dated analysis** (CLAUDE.md §15). Commissioned by the owner: *"deep dive the weapons as we need
weapons resolved and worth working on."* Every claim below was read at source; anything not settled is
labelled **UNPROVEN** rather than inferred.

---

## 0. The one-sentence answer

**The game has 96 weapons in its runtime catalog, 24 of which a player can obtain, and the 24 split into
two disjoint halves that are each unfinished in the opposite direction: the weapons with art have no
design, and the weapons with design have no art.**

Underneath that, the authoring pipeline has **two live menu commands that each destroy the other's
output.**

---

## 1. ⛔ The two landmines — fix these before anything else

Neither is a "don't hand-edit" warning. Both are commands an owner or agent could run any day.

**Landmine A — `Defenders/Catalog/Generate Gear Catalog` destroys the curation.**
`GearCatalogGenerator.MergeAndWrite:404` seeds from the **Resources** copy (96 rows), `:452-457` appends
every scanned row not already present, `:467-468` writes the result to **both** files. Running it
re-inflates Resources **96 → 431** and puts all 335 dormant placeholder weapons back on the shelf.
`GearCurationExporter` **cannot undo it** — it is additive-only and never drops (`:161-173`).

**Landmine B — `Defenders/Catalog/Render Gear Icons` destroys the library.**
`GearIconRenderer.ProcessCatalog:113` reads the **Resources** copy (96 rows) and `:192-193` writes that
same JSON to **both** paths. The first time any row gets a new icon (`dirty=true`), StreamingAssets is
truncated **431 → 96** and the browse library the Gear Caster depends on is gone.

### The 96/431 asymmetry is NOT a designed projection

It is the residue of a **one-time hand prune**. `git log` on the Resources copy: `b78c81cfd` = 434 rows →
`0d8185d1a` *"curate catalog — 434->34 weapons"* = 34 → the exporter walked it back up to 99 → 100 → 96.
StreamingAssets was never pruned.

> ⚠ **No tool can reproduce the 96.** The asymmetry survives only because nobody has run stage 3 or
> stage 8 since. This corrects an earlier session note that treated the split as a working pipeline.

**Canon correction:** `docs/MASTER_CATALOG/data-catalogs.md:205-215` justifies the drift exemption with
*"Resources may hold authored ids that exist ONLY there."* Set math says **Resources-only ids = 0** —
Resources is a pure **subset** of StreamingAssets today. That makes a cheap subset-oracle available, and
no gate asserts it.

---

## 2. What a player can actually obtain — **24 of 96**

| Authored | Reachable |
|---|---|
| 431 library rows | — |
| 96 curated runtime rows | **24 obtainable (25%)** |
| 427 Addressable entries | 65 referenced, **335 dormant** |

**72 of the 96 weapons in the shipped catalog cannot be acquired by any non-debug path.**

Gates, all verified at source:

- **Forge — the only weapon vendor.** Its `vendors.json` row carries `"excludeIdPrefixes": ["blink_"]`,
  `"onlyEquippable": true`, `"perLevelCap": 2`. **All 65 art-bearing rows have ids beginning `blink_`** —
  so *every weapon with art is excluded from the only weapon shop.* Enforced at
  `VendorStockResolver.cs:288` / `:302` / `:469-511`. Ceiling: **20 distinct ids ever.**
- **Loot** — `loot-tables.json` has **zero** weapon references (71 drop lines, all `materialId`). Weapons
  drop only from two hardcoded paths: outpost clear (`EnemyOutpost.cs:716-798`, 15 ids, no `blink_`
  filter — the only leak, and it leaks exactly `blink_shield1h_02/03`) and arena win
  (`BattleArena.cs:2798-2871`, 9 ids, ~4%).
- **Quests** — across 24 quests, **one** `grantItemId`: `knight_iron`.
- **Crafting — DEAD.** `gear-recipes.json` defines 6 weapon recipes; `GearCraftingService` has **zero
  runtime callers**. The Workshop panel the player opens binds `WorkshopCraftVM` →
  `crafting-recipes.json`, whose only recipe is `torch`.
- **Starters** — `GearLoadout.cs:78-86` authors exactly one kit (Knight). Ranger and Mage fall to
  `StarterOrCatalogFloor` (`:609-621`) = catalog-best-for-level, which **at level 10 hands out an
  `aegis_*` legendary for free.**

### The two-halves problem, in numbers

| | 65 `blink_*` (art) | 31 designed |
|---|---|---|
| `damageMult` | **63/65 are exactly 1.0** | 1.0 → 2.4 ladder |
| `req.level` | **all 1** | 1 / 3 / 6 / 10 |
| Buy cost | **all identical** (20/20/20) | graded |
| Flavor text | 3/65 | 27/31 |
| 3D model | **all 65** | 12/31 |
| Icon PNG | **all 65** | 11/31 |
| Buyable at the Forge | **0** | 20 |

`WORK_ORDER_500_weapon_armor_balance.md` diagnosed exactly this in its §0 and has sat at
**"PROPOSAL / DESIGN — READY FOR OWNER REVIEW"** ever since. **The `blink_` exclusion is not a bug — it
is WO-860 Part B deliberately hiding 65 flat placeholders. The exclusion is the symptom; WO-500's
unratified curve is the cause.**

---

## 3. Broken or hollow

**a) Gitignored art, quantified.** All 65 curated addressable rows resolve to real `Assets/Blink/` assets
*on this machine* (every address → GUID → `.meta`, zero misses). `Assets/Blink/` is gitignored, so on a
fresh clone **65 of 96 weapons have no art at all**. Worse: `Gear_BundledAssetGroupSchema.asset:41` sets
`m_BundleMode: 0` (PackTogether) with `m_IncludeInBuild: 1` — all 426 entries ship in **one 15.0 MB
bundle** (15,056,364 bytes = **99.5% of all Addressables weight**). Loading one curated sword pulls the
335 dormant ones with it.

**b) A §1.4b hollow assertion in the seat path.**
```csharp
// EquipmentController.cs:1599-1600
bool hasOffset = AttachmentOffsetRegistry.TryGetOffset(offsetKey, out var fo) ||
                 (offsetKey != id && AttachmentOffsetRegistry.TryGetOffset(id, out fo));
```
One bool from two lookups, **no else-branch log**. An unseated weapon and a deliberately-unauthored one
are indistinguishable in the trace.

**c) Failure that renders as success.** `EquipmentController.Resolve:2666-2693` **never returns null** —
the last line is `return Sword("sword_A")`. Then `FallbackResourcesAttach:814` is
`LoadWeaponMesh(...) ?? BuildFallbackPrimitive(...)` — a tinted cube. A missing model surfaces to the
player as *a generic sword or a grey box*, never an error. The one log that would catch it
(`LoadWeaponMesh:2745-2746`) emits `MISSING -> primitive fallback` as a **`FlowTrace.Step`, not a Warn or
Fail** — so it trips no gate and raises no F8 flag.
Live consequence: `cleric_starter` and `knight_flameblade` have `category: null`, no `prefabPath`, and no
`IdMap` entry — **a cleric is silently holding `sword_A`.**
*(Credit: the Addressable path at `:735-802` IS properly instrumented — Fail on throw, bad status, and
null instantiate.)*

**d) Duplicate art source — the WO-954 disease, confirmed.** `EquipmentController.cs:186-214` is a
hardcoded `IdMap` of id → mesh, parallel to `prefabPath` in JSON, carrying its own `gripPos`/`gripEuler`.
Drifted **both** ways: 4 keys reference weapons in **neither** JSON copy (`mage_starter`, `ranger_yew`,
`ranger_storm`, `ranger_eclipse`); 7 catalog rows have neither `prefabPath` nor an `IdMap` entry. Its own
comment names the exit and nobody took it — `:185` *"TODO data-driven: delete this once weapons.json
carries visualMesh/grip."*

**e) Dead player-facing copy.** `VendorStockResolver.FooterLineFor:191` has **no runtime consumer**.
`PartyShopVM` exposes `EmptyLine` only; `PartyShopPanelMvvm.cs:754` renders only that. Two vendors have
authored footer copy sitting unread — forge and armorer, both *"come back after you level up for new
stock."* **A Knight who sees 2 rows at the Forge gets no explanation.** This is the felt symptom that
opened WO-860.

**f) `GEAR_CURATION_OK` is hollow.** `DataWebRegression.CheckGearCuration:519-567` asserts only that
picked ids are present as rows and that ids are non-empty and unique. It never checks that `prefabPath`
resolves, that the Addressable exists, that art is on disk, or that the weapon is obtainable.
**A fully green `GEAR_CURATION_OK` is compatible with all 65 curated weapons having no art and all 65
being unreachable in the shop — which is the actual current state.**

---

## 4. Seating — solved for exactly one rig, one class

**On `HeroBodySwapper.cs:263`'s `-90`: weapons do NOT inherit it as a mis-orientation.** That line
computes `forwardYaw = (cls == Knight) ? 15f : -90f` and feeds `VisualFactory.Skin(...)` at `:273`,
rotating the **hero body root** on the legacy Resources path only. Weapons parent to the *hand bone*, a
child of that root, so the yaw carries hand and weapon together and the local grip is unaffected.
**WO-966's PARTIAL is a body-yaw concern, not a weapon-seating one.**
⚠ **UNPROVEN:** that no seat code reads world-space forward and thus reacts to the body yaw — the
parenting was read, not a run.

**Seating is NOT solved in general.** `Assets/Resources/OffsetForge/offsets.json` holds 18 rows, of which
only **7 are weapon meshes** — `shield_A`, `sword_A`, `sword_D`, `sword_F`, `sword_G`, plus two
`@sheathed`. **All Knight.** There is **zero** authored seating for staff, bow, dagger, axe, hammer, wand,
and **zero for any Blink mesh key** — all 65 art-bearing weapons take the un-dialed path.
`Assets/OffsetForge/rig-profiles.json` is `{"profiles": []}` — empty, so there is no per-rig fallback.

⚠ **UNPROVEN whether the unauthored ones look wrong** — geometry-normalize may land them acceptably.
**Seating is a visual defect class; only a screenshot can close it.**

---

## 5. The plan

> ## ✅ BOTH DECISIONS MADE — OWNER, 2026-08-14: *"approve WO-500 curve and finish the 65"*
>
> **D1 = RATIFIED.** WO-500's balance curve is approved. It stops being a proposal and becomes the
> authority the 65 rows are graded against.
>
> **D2 = OPTION A.** Finish the 65 `blink_*` rows — apply the curve, set `manual: true`, drop the
> `excludeIdPrefixes` that hides them from the Forge. Option B (commission art for the 31 designed
> weapons) is **not** taken; the 65 already have models and icons, so the missing half was a spreadsheet
> rather than an art budget.
>
> **Steps 1–4 dispatched 2026-08-14.** Sequencing note for anyone reading later: Step 1 (disarming the
> two landmine commands) ran **concurrently with** Step 4 (applying the curve), because either landmine
> firing would have destroyed the curve work. They touch disjoint files.
>
> ⚠ **Step 4 was instructed to read WO-500 at source and apply what it actually says — never to invent
> a curve.** If WO-500 does not cover some class or weapon category present in the 65, that is a REAL
> GAP to be named and left untouched, not filled with plausible numbers.

### ⛔ The two decisions that gated this (both now ANSWERED — kept for the record)

**D1 — Ratify or reject WO-500's balance curve.** A finished design proposal that had been waiting for
review. **This was the single decision that unblocked weapons.** → **RATIFIED.**

**D2 — Which half do we finish?** → **(A), finish the 65.**
- **(A) Finish the 65** — apply a curve to the `blink_*` rows, set `manual:true`, drop
  `excludeIdPrefixes`. Buys a real ladder with art and icons already done. Cost: a balance pass on 65
  rows. Keeps the 15 MB bundle and the gitignored-art dependency.
- **(B) Finish the 31** — commission art for the 19 designed weapons, delete the 65 from Resources, prune
  the group. Buys a small, fully-authored, cheap catalog. Cost: art budget; kills 15 MB of build weight.
- ⚠ **Recommended: (A), then (B)'s pruning.** The 65 already have models *and* icons — **the missing half
  is a spreadsheet, not an art budget.**

*(If rarity presentation is touched: it must read by **shape or text**, never hue alone.)*

### Engineering — Steps 1–3 are dispatchable NOW, before D1/D2

**Step 1 — Disarm the landmines** (`GearCatalogGenerator.cs`, `GearIconRenderer.cs`). Make `MergeAndWrite`
write the *library* to StreamingAssets and never re-inflate Resources; make the icon renderer patch
`iconPath` in place rather than cross-writing whole files. Add a hard refusal + `FlowTrace.Fail` when a
write would change either file's row count beyond the rows actually touched.
**PROOF:** run each tool with nothing pending; `git diff --stat` must be empty. Success line:
`[GearCatalogGenerator] weapons: +0 new, N refreshed, 96 preserved`. Failure looks like the count moving
to 431, or a non-empty StreamingAssets diff after an icon run.

**Step 2 — Close the hollow reports.** Three one-line fixes: promote `EquipmentController.cs:2745`
`Step` → `Warn`; add the missing `else` at `:1599` logging both keys tried; add to
`CheckGearCuration:519` assertion (c) every curated row's `prefabPath` resolves and (d) the Resources id
set is a subset of StreamingAssets.
**PROOF — and this one is the good kind:** boot headless with `Assets/Blink/` temporarily renamed.
*Before:* gate green, hero armed with `sword_A`. *After:* `GEAR_CURATION_FAIL: 65 curated row(s) have an
unresolvable prefabPath` plus 65 Warn lines. **That inversion — green becoming red on the same tree — is
the proof the gate was hollow.**

**Step 3 — Fix the felt symptom** (no design decision needed). Wire `FooterLineFor` through: add
`FooterLine` to `PartyShopVM` mirroring `EmptyLine`, render at `PartyShopPanelMvvm.cs:754` when the shelf
was thinned.
**PROOF:** `UI_CAPTURE_OK` screenshot of the Forge at level 1 showing the line under a 2-row shelf.
Screenshot, not a log line — presentation defect.

**Step 4 — Apply D1's curve** to whichever set D2 names; `manual:true` on every touched row; adjust
`excludeIdPrefixes`.
**PROOF:** re-run the obtainability set math. **Success = the obtainable count moves off 24 toward 96,
with ≥2 distinct weapons per class per level bucket.** Make it a standing regression.

**Step 5 — Collapse the duplicate art source.** Take the `:185` TODO: move `mesh`/`gripPos`/`gripEuler`
into the JSON schema, delete `IdMap`. **After** Step 4, so the curve pass and the schema change do not
collide in one file.
**PROOF:** the 4 dead keys and 7 uncovered rows both go to zero; `cleric_starter` stops resolving to
`sword_A`.

**Step 6 — Prune the Addressables group** to the referenced set; flip `m_BundleMode` off PackTogether.
**PROOF:** `gear_assets_all_*.bundle` drops from 15.0 MB. A build-size measurement, not a log line.

**Step 7 — Seating sweep.** Author `offsets.json` entries for the categories with none.
**PROOF:** headless capture per class × category, **opened and looked at**. No log line closes this.

### Parallelism
Steps 1, 2 and 3 touch disjoint files (`Editor/Catalog/*` · `EquipmentController` + `DataWebRegression` ·
`PartyShop*`) and can run as three lanes under one gate. Steps 4–7 are sequential and wait on D1/D2.

---

## Unproven, not asserted

1. Whether the un-dialed seating actually looks wrong in play — needs a screenshot.
2. Whether the 15 MB bundle's contents match the group manifest — needs an unpack.
