# WORK ORDER 122 — Crystal Mine Site (wire the on-map crystal as the mine)

**Status:** READY TO IMPLEMENT — CLI lane (held until green tree)
**Date:** 2026-05-29
**Priority:** Medium — world polish + first node of the resource-gathering pillar
**Scope:** Small (Phase 1 wire-up) — `VillageSceneBuilder` wiring + a small `CrystalMine` flag
**Depends on:** green tree (UI ③/④) → WO-103 rebake. The `Art/Crystals` mesh (untracked, already on the map).

---

## Reconciliation (this is a WIRE-UP, not a build)

> The crystal-mine gameplay **already exists** — do not rebuild it.

| Piece | State | Where |
|---|---|---|
| Crystal-mine gameplay | **BUILT** — passive Aether-Crystal yield on `WaveManager.OnWaveCleared` via `CrystalEconomy.AddCrystals`; L1→L3 upgrades paid in Coins; `[F]` upgrade prompt | `Assets/_Modules/Village/Buildings/CrystalMine.cs` |
| Spin + colour pulse | **BUILT this session** — slow spin + palette pulse via MaterialPropertyBlock (mobile-cheap) | `Assets/_Modules/Village/Buildings/CrystalVisual.cs` |
| Crystal mesh | present on the map (owner placed it), not yet hooked to anything | `Assets/Art/Crystals/` (untracked/local) |

**Goal:** make the owner's on-map crystal **the** crystal-mine site — its body, spinning + pulsing, with the existing CrystalMine gameplay on it.

---

## Phase 1 — wire it up (CLI, rides the next rebake)

### A. `CrystalMine.cs` — skip the placeholder when an external visual is supplied
`CrystalMine` builds its own `CrystalMineVisual_L{level}` placeholder (~line 377). Add a
serialized `bool _useExternalVisual` (default false → unchanged). When true, skip building
the placeholder and instead drive the crystal level-up feedback on the assigned crystal
mesh (e.g. brighten emission / swap palette per level). Keep the upgrade/yield logic intact.

### B. `VillageSceneBuilder` — place the crystal at the mine plot

> ⚠ **Placement is PROVISIONAL.** The owner dropped a crystal GO at the **NW corner**, but
> only because that area was open — not a firm design choice — and **WO-104 (castle + moat)
> will reshape the whole village layout.** So: the manual NW GO will be **wiped by the
> rebake** (builder owns placement); and the final crystal-mine position should be
> **decided after WO-104 lands**. For now place it at the crystal-mine plot (or NW) as a
> stand-in; expect to move it once the castle structure is known.

At the crystal-mine plot (Buildings `Type = 0`, now at `(-20, 0, +10)` per WO-105):
1. Instantiate the `Art/Crystals` crystal mesh as the plot's visual (fit height ~2.5–3 m, seat on ground; `LogWarning` + fall back to current poly visual if the mesh is missing — it's gitignored/untracked).
2. Attach `CrystalMine` (via reflection — `AddVillageComponent`) with `_useExternalVisual = true`.
3. Attach `CrystalVisual` to the crystal mesh for the spin + pulse.
4. Keep the `[F]` upgrade prompt + the `CrystalEconomy` wiring as-is.

### C. Acceptance (Phase 1)
- [ ] Crystal mesh renders at the mine plot, slowly spinning + pulsing colour
- [ ] `CrystalMine` still yields per wave + upgrades on `[F]` (gameplay unchanged)
- [ ] No duplicate placeholder visual (external-visual flag suppresses it)
- [ ] Brace-balanced; builds green; `Art/Crystals` missing → warning + graceful fallback

---

## Phase 2 — active / idle harvest (design fork — owner's call, future)

The current mine is **passive** (per-wave trickle). The roadmap's resource-gathering pillar
wants an **active / idle** node ([[resource-idle-economy-roadmap]]):
- Hold/tap the crystal to harvest; or assign a **pet to auto-harvest** over time.
- **Offline accrual** up to a cap → "welcome back, your pet mined N crystals."
- Tower upgrades spend the harvested crystals (and later forge/enchant).

This reuses `GameState.AetherCrystals` + the existing `CrystalEconomy` seam (write
`GameState.AetherCrystals` directly — Core can't reference Village, see
[[core-cannot-reference-village-award-crystals-via-gamestate]]). **Recommend: ship Phase 1
passive now (works today), spec Phase 2 as its own pillar WO when the idle loop is prioritised.**

---

## Lane / Do NOT
- CLI owns the wiring; **held until the tree is green** (no new code on a red tree).
- Do NOT hand-edit `Village.unity` — the builder regenerates it (WO-103 rebake).
- Reconcile, don't duplicate — `CrystalMine` + `CrystalEconomy` already exist.

🤖 Drafted by the build-connected CLI (gameplay reconciled against existing CrystalMine.cs).
