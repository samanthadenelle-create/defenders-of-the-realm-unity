# Grant MVP — Demo Validation Checklist (LIVING DOC)

**Purpose:** the single place "what's validated for the grant demo" lives — so it stops living in our heads.
Update as items are confirmed. **Started:** 2026-06-17.

## The demo law (from `FeatureFlags.cs`)
A reachable feature must **WORK or be HIDDEN**. A broken-but-visible feature is worse than an absent one.

| Flag | State | Why |
|------|-------|-----|
| `ARENA` | **ON — demo-ready** | Full loop verified: enter → fight → win/lose → reward → return. SKR wallet = intentional client-side MVP stub. |
| `RAID` | **OFF — hidden** | A *cleared* raid soft-locks (no victory/return wired, hero spawns as a capsule). Keep hidden so a tester can't get stuck. |
| `BLINKCHROME` | optional | Hides our chrome so Blink Obsidian panels show. Cosmetic toggle (`Defenders > Debug > Blink Chrome`). |

## The demo path (validate end-to-end on ONE fresh build)
Village hub → **Shop** (buy / sell) → **Equip / Inventory** → **Arena** (the demo-ready loop) → win → reward → return.

## Validated by feel (this session, owner F8 / playtest)
- [x] **Shop — Buy** works (ShopVM, model-driven from JSON catalog)
- [x] **Shop — Sell** works ("sell functionality works")
- [x] **Inventory / Equip UI** reads good ("UI looks good"); equip → hero visual applies
- [x] **Victory animation** ("victory animation looks good")
- [x] **Barracks (training) skin** ("train barracks skinned nice")

## Pending validation (before calling the demo grant-ready)
- [ ] **Weapon grip** in-hand per archetype (WO-435 fix committed local `1053ebc9`, **unpushed** — needs visual verify; staff/wand most likely to need a nudge dial)
- [ ] **Live HP/MP + stats** on the equip panel (WO-436 — code done, **gate pending editor-closed**; kills the "Not live data" placeholder look)
- [ ] **End-to-end demo run on a fresh build** along the path above
- [ ] **Confirm RAID is hidden** (off) in the demo build — no soft-lock reachable
- [ ] **Arena full loop** re-confirmed on the current build (enter→win→reward→return)

## Check-in / build reproducibility
- [x] Blink-imported RpgUi sprites committed (this commit) — fresh clone builds reproducibly
- [ ] Push the batch (textures + grip + WO-436) after grip verify + WO-436 gate
- [ ] Produce + smoke-test the demo build (Windows and/or WebGL — `docs/webgl-hosting-notes.md`)

## Known stubs / limitations (intended — not demo blockers, but know them)
- **Armor body-art** = NO-OP stub (weapon shows on hero; armor doesn't change the body mesh yet)
- **Outfits tab** empty (no cosmetic-ownership model)
- **SKR wallet** = client-side MVP stub (intentional)
- **Monetization/store scene-wiring** ~70% (PackStore exists; CosmeticApplier / BattlePass runtime are stubs) — not on the core demo path

## Notes
- Inventory is **owned-driven** (real JSON model, not catalog dump) — sparse until the player buys/loots; buy in the shop to populate. Expected, not a bug.
- Authoritative demo-readiness signal = `FeatureFlags.cs` (keep it current as features land).

*Cross-ref:* `FeatureFlags.cs`, `PIPELINE_STATE.md`, `docs/UI_MVVM_BINDING_MAP.md`, WO-431/434/435/436.
