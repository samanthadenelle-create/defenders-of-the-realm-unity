# WORK ORDER 466 — Real Store Items: Display, Equip-on-Hero + Tighter Animation

**Status: DRAFT (spec) — finalize once the usage audit lands + imports are done.**
**Branch:** `feat/tower-core-loop` (the recovered real version).
**Numbering note:** master backlog says "next free 430" but that's stale (431, 465 exist on disk). Using 466; register in `MASTER_PIPELINES_BACKLOG` + Notion and fix the "next free" line.

## Goal (owner vision)
Populate the shops with **real items** that **display** (proper icons + a preview) and **equip** —
the equipped gear is **visible on the hero** — and **tighten hero animation** using the Humanoid
clip libraries now on disk. "Real items, displayed and equipped; cleaner animation."

## Canon + grounding (read first)
- `docs/RAID_PILLAR_VISION.md`, `docs/TROOPS_PILLAR_SPEC.md` (gear feeds the loop; CoC-lean control).
- `docs/ASSET_PACK_CATALOG_2026-06-16.md` (what packs exist) + `docs/ASSET_USAGE_AUDIT_2026-06-16.md` (used slice — IN PROGRESS).
- `docs/CLEAN_BASELINE_AND_ASSET_HYGIENE.md` (asset policy below).
- Current pipeline: `GearCatalog` (weapons.json/armor.json), `ItemIconCatalog` (sliced icon sheets), `GearLoadout` (equip = stats + a *coarse visual tier* only), `ShopPanel` (detail pane shows a blank icon).

## Asset policy (owner: "gitignore all till we import what's used")
- **ALL art packs stay gitignored** (Blink, KayKit, Quaternius, polyperfect, …). Already enforced.
- Only the **used closure** (the specific gear prefabs/icons/clips this WO actually references) is
  ever committed — and only after the usage audit confirms it. Everything else stays on-disk,
  re-importable, **never committed**.
- "Used" = GUID refs in scenes/prefabs **+** code path-string / `Resources.Load` loads (the trap).

## Scope — IN
1. **Store icons:** wire `Blink/Art/Icons` (and KayKit gear art) into `ItemIconCatalog` → real item icons; kill the blank-white-square in the detail pane.
2. **Gear catalog:** map the used Blink/KayKit **weapon + armor prefabs** → `GearCatalog` entries (weapons.json/armor.json) with model refs.
3. **Equip-on-hero (the core):** extend `GearLoadout.EquipWeaponById/EquipArmorById` to **instantiate the gear prefab and attach it to the hero's hand/body bone socket** (standard Transform attach + the existing `WeaponOrientHelper` for grip orientation). Replaces the stats-only / coarse-tier behavior with a *visible* equip.
4. **Store detail pane:** show the selected item's real icon/preview.
5. **Animation:** wire the Humanoid clip library (`Assets/Action` 198 Mixamo clips, already Humanoid+tracked; optionally Blink `Art/Animations`) into the hero animator (retarget) to tighten motion. Supercyan-310 only if owner re-imports it (`docs/SUPERCYAN_REIMPORT.md`).

## Scope — OUT (do NOT do)
- **Do NOT import the Spark Framework** (the no-code framework behind Blink) — it clashes with our `GearCatalog`/`GearLoadout`/save architecture and is over-scope. Use our own attach.
- **Do NOT build the full troop/warband pillar** (follow-hero AI, finite army) — that's the post-grant pillar per `TROOPS_PILLAR_SPEC`.
- **Do NOT commit whole packs.** Only the used slice, after the audit.
- **Do NOT hand-edit scenes** or change render pipeline/global settings.

## Files likely touched
- `Assets/_Modules/Village/Hero/GearCatalog.cs` (item defs / model refs)
- `Assets/_Modules/Village/Hero/ItemIconCatalog.cs` (real icons from packs)
- `Assets/_Modules/Village/Hero/GearLoadout.cs` (equip → attach real model to hero socket)
- `Assets/_Modules/Village/Hero/ShopPanel.cs` (detail-pane preview) — the "nightmare file", minimal additive edits only
- hero animator setup (`HeroAnimatorSetup` / the controller) for the clip wiring
- `weapons.json` / `armor.json` (BOTH canonical copies — Resources wins, keep StreamingAssets in sync)

## Acceptance criteria
- [ ] Opening a vendor shows items with **real icons** (no blank square) in list + detail pane.
- [ ] Buying + equipping a weapon/armor makes it **visibly appear on the hero** (right socket, correct grip).
- [ ] Equip persists via `GearLoadout` and the existing save path; stats still apply.
- [ ] Hero plays the new Humanoid clips (locomotion/attack) cleanly — no slide/T-pose.
- [ ] Only the **used** asset slice is committed; the rest stays gitignored. Braces balanced on every `.cs`. COMPILE_GATE_OK.

## Dependencies / sequencing
1. Usage audit (`docs/ASSET_USAGE_AUDIT_2026-06-16.md`) lands → defines the used gear/icon/clip slice.
2. Owner finishes importing packs.
3. Then implement in order: **(a) animation wiring** (top priority), **(b) icons + gear catalog**, **(c) equip-attach-to-hero**, **(d) store detail preview.**
