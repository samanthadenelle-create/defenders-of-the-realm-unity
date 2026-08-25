> ⚠ **NUMBER COLLISION — this document does not own WO-282; `WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md` does.**
> Referred to hereafter as **WO-282-B (heroes Resources to Addressables)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> the two files were added in the **same commit**, so first-on-disk is a tie; ownership decided on **cross-references** (the winner is the file the rest of the corpus cites).
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 282 — Hero models: Resources/Heroes → Addressables `Heroes` group

**Status:** CLOSED — ** — SUPERSEDED BY WO-545.** Do not implement this as written.

WO-545 shipped the hero-asset seam with a **different and contradictory** contract:
`HeroAssetLoader.cs` + `HeroAddressablesGrouper.cs` use **Addressables-first with a Resources fallback
and a synchronous `WaitForCompletion` shim** — whereas this WO's §4 explicitly requires
"`LoadAssetAsync` + `await`, **not** `.WaitForCompletion()`". WO-545 shipped, so it wins.
This WO's target folder `Assets/Art/Characters/Heroes/` was never created; WO-545 used
`Assets/HeroContent/` instead.

**MEASURED STATE 2026-08-09 (why this is not urgent):** Addressables is set up but effectively unused on
the hot path — there is **no `Heroes` or `Hero_<slug>` group** (only `Default Local Group`, `Gear`, and
three Unity Localization groups), **314 `Resources.Load` calls remain** under `Assets/_Modules/`, and
`HeroAssetLoader` probes Addressables then always falls through, logging "no Addressables entry -
expected in V1". `HeroAddressablesGrouper.GroupAndMigrateHeroes` has never been run.

⚠ **LIVE DEBT inherited from WO-545, worth a ticket of its own:** `HeroAddressablesGrouper.cs:35-40`
warns that **WebGL does not support `WaitForCompletion` on an undownloaded bundle.**

⚠ **NUMBER COLLISION:** `WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md` also claims 282. Two
different work orders share this number. Resolve per CLAUDE.md §2 (first-on-disk-and-referenced-wins)
before either is actioned.

See also the sibling `WORK_ORDER_282_heroes_resources_to_addressables.HOLD.md`, whose reasoning still
stands: the async conversion touches the "does the hero appear at all" path in village/ATB/story/DTT,
and a subtle bug there **compiles fine but yields no hero body in every scene**.

> ⚠ **§15 STALENESS FLAG (2026-08-09).**
**Date:** 2026-06-06
**Author:** UI (creative/architecture lane)
**Owner approval:** Samantha — greenlit; scope = "follow the plan doc"
**Priority:** Medium — base-build size + streamable skins. Not a gameplay blocker;
sequence after any in-flight combat/scene work.
**Lane:** Code + Addressables config + asset-group bake. **Combat/AI + asset lane.**
NO `VillageSceneBuilder.cs` edits (frozen, CLAUDE.md §3/§9). NO `.unity` hand-edits.
**Implemented + build-verified by:** CLI (owns batchmode + Addressables bake).
UI does not fire batchmode.

**Primary reference:** `docs/addressables-implementation-plan.md` — esp. §1 (Heroes
group row), §0 Golden Rules, §3 memory rules, §4 SkinController, §5 AddressablesGroupConfig.
This WO implements the **Heroes** slice of that plan.

**SEQUENCING — run WO-283 FIRST.** WO-283 (canonical animation library) rebuilds the
hero animator controllers in their current `Resources/Heroes/` home. THIS WO then
relocates the hero models **and those freshly-built controllers** into the Addressables
`Heroes` group. If 282 runs before 283, the controllers 283 produces will land in
Resources and have to be moved again. Whichever runs second reconciles the controller
output/lookup path (`HeroAnimatorFactory` writes `Resources/Heroes/<slug>.controller`).

---

## 1. Goal

Move the four hero models (+ their animator controllers) out of
`Assets/Resources/Heroes/` into an Addressables **`Heroes`** group, and convert
every `Resources.Load("Heroes/...")` call site to async Addressables loads with
proper handle release. End state: heroes are no longer in the base-build Resources
payload; they load on demand and can be packed separately for future per-skin
streaming.

Per plan §1: `Heroes` group = On Demand / Remote / LZ4 / **Pack Separately**.

---

## 2. Assets in scope

Currently at `Assets/Resources/Heroes/` (do NOT delete the `.meta` GUIDs — move the
assets so Unity preserves GUIDs; moving inside the project keeps references intact):

- `Cleric.fbx`, `Knight.fbx`, `Mage.fbx`, `Ranger.fbx` (models — just replaced, WO context)
- `Cleric.controller`, `Knight.controller`, `Mage.controller`, `Ranger.controller`
- Associated `_tex/`, `Textures/`, `Materials/`, `*.fbm/`, `Props/` that the FBX/materials reference

**Target location:** `Assets/Art/Characters/Heroes/` (out of any `Resources/` folder),
marked Addressable in group `Heroes`. Confirm final path with owner if `Assets/Art/`
conflicts with existing art conventions — see `Assets/README.md`.

### Secondary (only if low-risk; otherwise spin a follow-up WO)
`HeroPortraits/*` is loaded via `Resources.Load("HeroPortraits/{slug}")` in
`BattleHud`, `HeroSelectController`, `TitleController`. Portraits are tiny and may be
fine to leave in Resources for now. **Default: leave HeroPortraits in Resources this
pass** unless CLI judges the move trivial. Heroes models/controllers are the required deliverable.

---

## 3. Call sites to convert (sync `Resources.Load` → async Addressables)

| File | Line (approx) | Current |
|---|---|---|
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` | 36 | `Resources.Load<GameObject>("Heroes/" + slug)` |
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` | 147 | `Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)` |
| `Assets/_Modules/BattleATB/AtbCombatantSwapper.cs` | 92 | `Resources.Load<GameObject>("Heroes/" + slug)` |
| `Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs` | 179 | `Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)` |
| `Assets/_Modules/Village/PatriciaLight/PatriciaLightController.cs` | ~690 | references missing `Resources/Heroes/{slug}` (stand-in path) — update warning/fallback path |
| `Assets/Editor/HeroAnimatorFactory.cs` | 15 (comment) | doc comment references load path — update comment |
| `Assets/Editor/HeroPortraitRenderer.cs` | 143 (comment) | doc comment — update if portraits move (else leave) |

**Texture fallback strings** in `HeroBodySwapper.cs:456` and
`StoryCompanionInjector.cs:313` (`"Heroes/Cleric_tex/HumanCleric_basecolor"`) also
assume the Resources path — reconcile these (either keep a tiny Resources texture
fallback, or route through the group config). Call out the decision in the RESULT.

---

## 4. Critical risk — sync→async signature change

`Resources.Load` is **synchronous**; Addressables is **async**. This is the main
hazard, not the asset move. Each call site returns the model/controller inline today;
callers (e.g. `HeroBodySwapper` swapping the body mid-frame) assume the result is
ready immediately.

Required handling:
- Use the project's async convention (**UniTask** is in the tree — see
  `docs/UNITASK_NOTES.md`; plan §7 shows `UniTask` usage). Prefer `LoadAssetAsync` +
  `await`, not `.WaitForCompletion()` (blocking defeats the purpose and can stall WebGL).
- Where a caller truly cannot be made async, document why and use a guarded
  `WaitForCompletion()` with a code comment — but treat that as a last resort.
- Show a placeholder/stand-in body until the async load completes (HeroBodySwapper
  already has placeholder-body handling per its missing-asset warning — reuse it).

Follow plan §0 rule #4 (no hardcoded address strings — use `AssetReference` on an
`AddressablesGroupConfig`) and rule #3 (**every handle opened must be released** —
release on body-swap replace, scene unload, and `OnDestroy`).

---

## 5. Steps (mirrors plan §8 implementation order, Heroes slice)

1. Ensure `Local`/`Remote` Addressables profiles exist (plan §2.1). If the broader
   Addressables setup hasn't run yet, this WO may be the first group — set up the
   minimum profile needed and note it in RESULT.
2. Create the `Heroes` group; settings: On Demand, Remote build path, LZ4,
   **Pack Separately** (plan §1 + §39 policy).
3. Move the four FBX + controllers (+ deps) from `Resources/Heroes/` to
   `Assets/Art/Characters/Heroes/`; mark Addressable, assign to `Heroes` group.
   Address each as `Heroes/{slug}` (model) — keep the existing slug scheme so address
   ↔ `HeroClass` mapping is unchanged.
4. Add/extend an `AddressablesGroupConfig` (or `AssetReference` fields) so call sites
   resolve heroes without hardcoded strings (plan §5).
5. Convert the §3 call sites to async loads + handle tracking + release.
6. Update editor doc comments (`HeroAnimatorFactory.cs`, `HeroPortraitRenderer.cs`)
   and the texture-fallback paths in §3.
7. Update `Assets/_Modules/Village/Hero/README.md`-adjacent docs and
   `docs/addressables-implementation-plan.md` (tick the Heroes item) + this WO's RESULT.

---

## 6. Acceptance criteria

- [ ] `Assets/Resources/Heroes/` no longer contains the 4 hero FBX or controllers
      (Resources base payload reduced). No stray duplicate import of the moved assets.
- [ ] `Heroes` Addressables group exists: On Demand, Remote, LZ4, Pack Separately.
- [ ] All §3 call sites compile and load heroes via Addressables (no
      `Resources.Load("Heroes/...")` remains in `.cs`; verify with grep).
- [ ] Every opened handle is released (body re-swap, scene unload, OnDestroy).
      No leaked handle for a hero after leaving the village scene (Event Viewer, plan §3.1).
- [ ] Hero select → spawn in village → ATB battle → PatriciaLight all still show the
      correct hero body + animations (manual play smoke test, all 4 classes).
- [ ] StoryCompanion still resolves its controller.
- [ ] **Brace balance check passes on every `.cs` edited** (CLAUDE.md §1).
- [ ] Build-verify in batchmode succeeds (CLI).
- [ ] RESULT documents: final asset path, whether HeroPortraits moved, and any
      `WaitForCompletion` fallbacks with justification.

## 7. Do NOT touch

- `VillageSceneBuilder.cs` (frozen) or any `.unity` scene file by hand.
- The just-replaced FBX **contents** (WO context: hero models were swapped 2026-06-06).
  This WO only relocates + re-addresses them; it must not re-import different meshes.
- HeroPortraits move is optional and secondary — do not let it block the models migration.
- Towers/Pets/VFX/Audio groups — out of scope; separate slices of the plan.

## 8. Notes for CLI

- Async conversion is the real work; the asset move is mechanical. Budget accordingly.
- If the `Heroes` group is genuinely the first Addressables group built in the project,
  flag in RESULT — owner may want the towers/pets slices sequenced right after to amortize
  the profile/CDN setup (plan §8 suggests towers/pets first; we're doing Heroes first by
  owner request since the models were just refreshed).
- Backups of the original (pre-swap) FBX are in `Backups/hero_fbx_20260606_005717/`.
