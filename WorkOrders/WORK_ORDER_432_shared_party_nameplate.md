<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-04
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-04) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION — this document does not own WO-432; `WORK_ORDER_432_building_perk_research_techtree.md` does.**
> Referred to hereafter as **WO-432-C (shared party nameplate)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-432 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WO-432 — P1 UI: Shared PartyNameplate common in ElarionUiKit (HP+MP) + HUD wiring

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Priority:** P1  
**Lane:** 4 UI/HUD  
**Notion:** https://app.notion.com/p/393bf190c6898137bd49c0daefdbb318  
**Minted:** 2026-07-03

---

## Owner decision — 2026-07-03

`PartyNameplate.prefab` (Blink Obsidian) is the confirmed style target for all HP/MP display
in the HUD. CLI must study the prefab and build a **shared code-built equivalent** in
`ElarionUiKit` so it can be dropped anywhere — hero panel, Heart of Elarion bar, future
party frames. HP and MP bars are both part of every instance.

---

## What

Three deliverables, in dependency order:

### 1 — `ElarionUiKit.BuildPartyNameplate()` — new shared builder

Add to `Assets/_Modules/Core/UI/ElarionUiKit.cs` (or a new sibling partial
`ElarionUiKitNameplate.cs` in `DeNelle.Core.UI`):

```csharp
public struct NameplateHandle
{
    public RectTransform Root;
    public TMP_Text      NameLabel;
    public Image         HealthFill;   // set fillAmount = hp/maxHp
    public Image         ManaFill;     // set fillAmount = mp/maxMp
}

public static NameplateHandle BuildPartyNameplate(
    RectTransform parent,
    string playerName,
    Vector2 anchorMin, Vector2 anchorMax,
    Vector2 offsetMin = default, Vector2 offsetMax = default)
```

**Visual reference: `Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/PartyNameplate.prefab`**

Prefab structure to mirror (code-built, no prefab instantiation):

| Layer | Object | Key data |
|---|---|---|
| Root | PartyNameplate (Image) | sprite `0bf4c931cca6bed4dba777541c5739b6`, color white |
| Child 0 | PlayerName (TMP_Text) | white, auto-size 18–72, left-aligned, anchor top-center, offset (-76.63, -15.12), size 240×24 |
| Child 1 | StatBars (GridLayoutGroup) | 2 rows × 1 col, cell 348×31, spacing 0, at (-22.42, -15.11), size 348×64 |
| StatBars[0] | HealthBackground (Image) | color #1f1f1f, sprite `6a8076f69453cbc4b8eaa55f654b6de1` |
| StatBars[0][0] | HealthFill (Image) | sprite `fd3066864748cb842a036195e2742c3a`, FillMethod=Horizontal, inset 2px, fillAmount=1.0 |
| StatBars[1] | ManaBackground (Image) | color #5e5e5e, sprite `6a8076f69453cbc4b8eaa55f654b6de1` |
| StatBars[1][0] | ManaFill (Image) | sprite `4791157a6698a10459e8ba9b101cd0ff`, FillMethod=Horizontal, inset 2px, fillAmount=1.0 |

Sprite loading: use `AssetDatabase.GUIDToAssetPath` → `AssetDatabase.LoadAssetAtPath` at
build time. If a GUID resolves null → `Debug.LogWarning` + solid-color fallback. Never
silently blank.

### 2 — Kill center panel noise (from original WO-432 scope)

- Remove `Panel()` background from `BuildWaveBlock()` — replace with plain transparent
  RectTransform container (no Image component)
- Hide `_waveLabel` when `w.Number == 0`:
  `_waveLabel.gameObject.SetActive(w.Number > 0);`

### 3 — Wire `BuildPartyNameplate()` into HudKitController (two call sites)

**Hero nameplate** — replace/augment existing hero nameplate in the PLAYER area pool.
`NameplateHandle.NameLabel` = hero name/level. `HealthFill` driven by existing
`SetHeroHp()` / `UpdateHeroVitals()` calls. `ManaFill` driven by mana/energy if available.

**Heart of Elarion bar** — replace current `BuildObsidianBar(ObsidianBarKind.Heart)`
(center-anchored) with `BuildPartyNameplate()` parented to PLAYER area pool, anchored
directly below the hero nameplate. `NameLabel` = "♥ Elarion". Only `HealthFill` used;
`ManaFill.fillAmount = 0` and/or `ManaBackground.gameObject.SetActive(false)`.

---

## Blink SME note for CLI

Before writing any code, read:
`Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/PartyNameplate.prefab`

Key facts:
- `GridLayoutGroup` uses `ConstraintCount: 2`, `m_StartAxis: 0` → 2 rows, flows down Y
- Cell size 348×31, zero spacing
- Both bar backgrounds share sprite GUID `6a8076f69453cbc4b8eaa55f654b6de1`
- Fill images are different sprites (health ≠ mana textures)
- `FillMethod: 0` on both fills = **Horizontal** (not radial)
- HealthBackground color: `r:0.12 g:0.12 b:0.12` = very dark
- ManaBackground color: `r:0.37 g:0.37 b:0.37` = medium gray

---

## Files to touch

- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — add `BuildPartyNameplate()` + `NameplateHandle`
  (or new sibling partial `ElarionUiKitNameplate.cs`)
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — `BuildWaveBlock()` bg removal, wave label
  hide, hero nameplate + heart bar rewire to `BuildPartyNameplate()`

## Do NOT touch

- `VillageHudController.cs`, `HudAreasConfig`, any `.unity` scene files

---

## Acceptance criteria

- [ ] `ElarionUiKit.BuildPartyNameplate()` compiles in `DeNelle.Core.UI`, no assembly violations
- [ ] `NameplateHandle` exposes `HealthFill` + `ManaFill` as `Image` refs; callers set `fillAmount`
- [ ] Hero nameplate shows name + HP bar + MP bar in PartyNameplate style
- [ ] Heart of Elarion shown below hero nameplate; name = "♥ Elarion"; MP fill hidden/zero
- [ ] No olive/tinted bg visible in center area between waves
- [ ] "The village rests" label invisible when wave number == 0; shows "Wave N" when active
- [ ] All sprite-load misses guarded with `FlowTrace.Warn` + solid-color fallback
- [ ] Headless AutoPilot smoke run passes (no null refs, no compile errors)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
