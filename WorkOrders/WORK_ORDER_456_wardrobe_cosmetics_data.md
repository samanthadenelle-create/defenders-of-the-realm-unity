# WORK ORDER 456 — Data-driven wardrobe + cosmetic-store feed

**Status:** SPEC DRAFT (not ready to implement — owner to ratify data shape first)
**Depends on:** the rig-level wardrobe foundation (SHIPPED — `BlinkWardrobe` + `VisualFactory.Skin` hook).
**Canon:** `docs/WARDROBE_ARCHITECTURE.md` (read first). **Owner architecture:** 2026-06-20.

## Goal
Turn the hardcoded `DefaultOutfit = "Starter"` into a **data-driven, per-character wardrobe collection**
that both **dresses the character** and **feeds the cosmetic store** — one data model, two consumers.

## What exists (the seam — do NOT rebuild)
- `BlinkWardrobe.Dress(GameObject body, string outfit)` — already a named-outfit entry point.
- `BlinkWardrobe.IsDressable(body)` — capability gate (ships outfit renderers).
- Invoked at the rig level in `VisualFactory.Skin`. `HeroArmorVisual` uses it for armor overlays.

## To build
1. **Wardrobe data (canonical JSON).** A per-character wardrobe record in the
   `Resources/StreamingAssets/Data/Canonical` system (dual-copy convention):
   - `defaultOutfit` (string, outfit-set id — e.g. `Starter`, `Cloth1`).
   - `owned` (collection of outfit-set ids the character has).
   - `available` (the catalog of outfit sets that CAN be owned — store inventory source).
   - Keyed by character/selection — *"depending on the character selected the JSON is different."*
2. **A wardrobe controller** (component/service) that, on body build, reads the record and calls
   `BlinkWardrobe.Dress(body, defaultOutfit)`; exposes `Equip(outfitId)` → `Dress(...)`; and mutates
   `owned` as a **living collection** (unlock/buy adds to it).
3. **Cosmetic store feed.** `PanelId.CosmeticShop` reads `available` minus `owned` as buyable; a purchase
   adds the id to `owned` (the same collection) → immediately equippable. Wardrobe + store never drift.
4. **Outfit-set catalog.** Enumerate the Blink outfit sets actually present on the body
   (`Starter`/`Cloth1`/`Cloth2`/`Cloth3`…) + display names/prices for the store.

## Acceptance
- Two different characters selected → different default outfit from JSON (no code change).
- Buying an outfit in the cosmetic store → it appears in the wardrobe → equip → body re-dresses.
- Non-dressable bodies untouched. Regression: a dressable body is never in underwear at spawn.

## NOT in scope
- New armor art. The bone-share/full-body armor pipeline (`HeroArmorVisual`) is done — this is cosmetics.
- Monetization backend / payments (separate isolated lane).
