<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-14
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-14) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 717 — Unstyled-class kill (real frames, no mask fills)

**Status:** READY TO IMPLEMENT  
**Priority:** P0 (felt “not Obsidian”)  
**Phase:** 1 (Bleed)  
**Effort:** M  
**Depends on:** ideally 716 baseline shots (can start in parallel on known offenders)  
**Program:** Grok-03 · **Guidance:** Grok-02 §1 style bar, §6 failure modes  

---

## Goal

Eliminate the class of surfaces that read as **flat procedural black + gold trim** or **pack frame painted over by a solid fill** — the #1 reason players still say “it doesn’t look like the pack.”

---

## Tasks

1. **Audit** open/build paths for:
   - `BuildObsidianPanel` / Modal called **without** `frameName` (or with null frame when a default exists).  
   - Solid fill Images covering frame art (BLINK_UI masking-fill class — alpha 1 over ornate sprite).  
   - Content parented to full panel rect instead of `layout.body` (double-border / content under chrome).
2. **Fix** demo-critical + FIX-listed screens from 716:
   - Force correct `RpgUiCatalog.Frame*` per Grok-02 §5 table.  
   - Kill or alpha-0 decorative fills that occlude frames.  
   - Re-parent content into zones only.
3. **Instrumentation:** `[Flow:UiChrome] frame=… spriteNull=… fillMask=…` once per panel open (throttled).  
4. **Optional oracle:** extend or add regression that fails if a known screen list opens with `frameName` null when art exists.  
5. **Same-breath canon:** one line in Grok-02 or RESULT listing screens fixed.

---

## Files (expected)

- Consumers under `Assets/_Modules/HUD/**`, `Village/**` panels (only FIX targets).  
- Possibly `ElarionUiKit` / `ElarionUiKitObsidian` if solid-fill is factory-default wrong.  
- **Do not** hand-edit Blink pack under `Assets/Blink`.

---

## Acceptance

- [ ] Owner pair-walk re-capture: previously “flat” screens show **real frame embossing**.  
- [ ] Grep/list of fixed screens in RESULT.  
- [ ] No new UXML. Brace/NUL + COMPILE_GATE_OK.  
- [ ] Blink-absent fallback still non-blank (sprite-first preserved).

---

## Not in scope

- New frames art · full HUD redesign (→ 721) · build dock layout (→ 719).

---

## RESULT

`WorkOrders/WORK_ORDER_717_unstyled_class_kill.RESULT.md`
