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

## Bot-validated — headless autopilot fleet (12 runs, 2026-06-17)
- [x] **Boot → gameplay, hero resolve** (every run)
- [x] **Vendors open — 0 contract violations, 0 empty-warns** (8 contexts)
- [x] **Economy deduct** (buy charges + adds to inventory)
- [x] **Equip** (gear equips)
- [x] **5/5 HUD panels open**
- [x] **Castle exit AUTO-CROSS works** — "seam did NOT fire" error gone; 1/4 → 4/4 gates reachable
      (commit `104bec93`, nav rebake)

## Base-loop fixes landed this session (committed, gate-green — owner felt-test pending)
- [x] **WO-438** (`f44e218a`) — dialogue-greyed (introducer deregister), Inn no-node (open board via C#
      node-start hook, Brom's narration preserved), companion follow (robust hero resolve), party NRE
      (activeInHierarchy guard), wight half-underground (re-ground visual)
- [x] **WO-437** (`1a64858e`) — input/state gate: battle-lock (panels locked mid-fight) + killed the
      13-windows global hotkeys + registered the 2 arbiter-bypass panels + centralized ESC
- [x] **WO-435** grip · **WO-436** live HP/MP · **WO-431–434** shop+inventory MVVM (all committed)

## Open base-loop bugs (fleet-found, NOT yet fixed — need care)
- [ ] **Wave-trigger** — intermittent (works ~5/12) → async race in `WaveManager.BeginLoop().Forget()`;
      diagnose (instrument the load) before fixing — combat-critical
- [ ] **Navmesh from wander positions** — from-spawn is 4/4, but ~7/12 still can't path to the gate from
      where the hero ends up → remaining reachability holes (deeper nav/bake work)
- [ ] TMPro NRE (GenerateTextMesh) 2/12 — low; a text field getting a null/bad string

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
