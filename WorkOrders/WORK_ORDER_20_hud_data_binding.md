**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 20 — HUD data binding: Heart HP + Crystals runtime push

**Date:** 2026-05-24 (filed as a follow-up from WO-10 smoke test; root-caused in WO-07)
**Owner:** Samantha Denelle
**Authority:** Standing Authority #35 + WO-025.
**Priority:** Medium-High — two always-visible HUD readouts show stale data in normal gameplay.
**Depends on:** WO-06 (HUD), WO-07 (established the per-frame HUD-push pattern).
**Expected runtime:** 30–45 minutes.

---

## 1. Problem statement (statically confirmed — not yet eyes-on)

`VillageHudController` exposes `SetHeartHp(current,max)` and `SetCrystals(amount)`, but a full-repo grep shows **neither is called during normal gameplay**:

- `SetHeartHp` — only caller is `DevPanelController` (the dev overlay). So the Heart HP bar ("Elarion") stays at its UXML default `100/100` and does **not** drop when a gate is breached / the Heart takes damage.
- `SetCrystals` — **no runtime caller at all**. The crystal counter stays at its UXML default.

This is the same class of gap WO-07 fixed for mana/ability-cooldown: the HUD setter exists but no bridge pushes state into it. WO-07 added `HeroAbilitiesHudBridge`'s per-frame push for mana + cooldowns; Heart HP and Crystals still have no equivalent.

(For contrast, `SetWave` IS pushed by `WaveHudBridge`, and mana/cooldowns now by `HeroAbilitiesHudBridge` — so the *pattern* to copy already exists in the repo.)

## 2. Suggested fix (additive, mirror the existing bridges)

- **Heart HP:** a `HeartHudBridge` (or fold into `VillageController`) that pushes `hud.SetHeartHp(heart.Hp, heart.MaxHp)` — either every frame, or on `HeartController.HpChanged` if such an event exists (cheaper). `VillageController` already holds the `_heart` ref and the HUD ref.
- **Crystals:** push `hud.SetCrystals(GameStateService.Instance.State.Resources.Crystals)` — ideally subscribe to `GameStateService.ResourcesChanged` if it exists (per the integrator note in `VillageHudController.cs`), else per-frame.
- Cross-asmdef: `DeNelle.Village` → `DeNelle.HUD` uses the same reflection seam as `WaveHudBridge` / `HeroAbilitiesHudBridge`.

## 3. Acceptance criteria

1. In Village playmode, the Heart HP bar drops when the Heart/a gate takes damage and rises on repair.
2. The crystal counter reflects `GameState.Resources.Crystals` and updates on spend/earn.
3. No gameplay-balance values changed; additive only.
4. Build clean; `WORK_ORDER_20_hud_data_binding.RESULT.md` written.

## 4. Notes

- Root cause documented in `WORK_ORDER_07_hero_abilities.RESULT.md` §6 and surfaced again by the WO-10 smoke test. Low-risk, well-patterned.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
