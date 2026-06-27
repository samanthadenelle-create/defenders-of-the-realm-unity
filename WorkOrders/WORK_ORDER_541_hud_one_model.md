# WORK ORDER 541 — HUD One-Model (data-model-first; dumb presentation)

**Status:** READY TO IMPLEMENT (foundation lane)
**Silo:** HUD/Core (code-only; no scene hand-edit)
**Source:** Owner directive 2026-06-27 — "architect should look at ALL models, get all data groups by what all HUDs need, design those models, then a common CoreServices FROM the model, then the model is exposed to the presentation layer which is dumb and simply skins — no logic, just presentation." Backed by F8 flags 00–10.
**Supersedes:** the rejected per-canvas "context gate" patch (WORK_ORDER_541_hud_context_authority.md). Subsumes WO-540 (HUD leak) + WO-535 (panel register) + the duplicate-hero-card / Echo-bleed / green-MP-box defects.
**Architecture law:** HP-B2B One-Model + MVVM. Model owns state; presentation is a dumb skin. (CLAUDE.md §5 assembly law; owner-thinks-in-data-structures.)

---

## Why (proven, data-cited)

The HUD has NO single model. Same datum fetched 2–4 ways → duplication + bleed (owner flags 00–10):
- Hero HP read ×4 (VillageHudController party push; BattleHud9Zone `HeroHealth.Instance` reflection; BattleHudUgui ATB snapshot; equip VM). Mana ×4, wave state ×3, ability loadout ×2.
- TWO context evaluators: `VillageHudController.ApplyContext`/`InVillage` (scene+radius) AND `BattleHudVisibilityManager.EvaluateMode` (wave/battle-lock/enemy-scene).
- `BattleHud9Zone.cs:301-312` draws its OWN Knight card on top of the party frame (duplicate hero card, flags 02/07/08/09).
- `EchoWorkforceHud.cs` has zero context gating → Echoes/Silo/Dump on every screen (flags 00–10).
The fix is not gates — it is ONE model the dumb views read.

## The architecture

**1. Models (Core records; one writer each; context is a FIELD):**
`HeroVitalsModel` (Hp/MaxHp/Mana/MaxMana/Xp/XpToNext/Level/ClassId) · `PartyModel` (member records) ·
`EconomyModel` (Gold/Wood/Iron/Food/Crystals) · `WaveModel` (Phase/Number/Max/Countdown/Imminent/Lookout/EnemiesLive/Total/ClearBanner) ·
`TargetModel` (HasTarget/Name(friendly)/Level/Hp/MaxHp/HpFraction/Role(Core enum)/Locked) · `TargetCycleModel` (sorted records) ·
`AbilityLoadoutModel` (4 slot records: Key/Glyph/Name/Icon/Accent/Equipped/CooldownRem/Total) ·
`WorldMetricsModel` (Heart HP/pct, Towers, Population, PassiveXp, Forgetting, Wards, MinimapPoi) ·
`MomentumModel` (Combo/KillStreak/Stars/BattleElapsed/KeepStarSeconds) · `EchoModel` (EchoCount/Max/Silo/FillFraction) ·
`HudContextModel` (**Context: Town/Overworld/Battle/Modal** + InVillage/CombatActive/ModalOpen).
Each = plain data + `Changed` event, no UI/Village types (Core enums only; map `EnemyRole`→Core `HudRole`, `WeaponDef`→string IconKey in the producer, as PartyShopVM already projects).

**2. Producers (Village/BattleATB write the models):** `HeroVitalsPublisher`, `PartyPublisher`, `WavePublisher`, `TargetPublisher`, `TargetRosterPublisher`, `AbilityLoadoutPublisher`, `WorldMetricsPublisher`, `MomentumPublisher`, Economy/Echo adapters, and **`HudContextEvaluator`** = the ONE context writer (consolidates today's two evaluators). A DDOL `HudModelHost` constructs the holders, wires producers, registers the facade.

**3. CoreServices exposure (single source of truth):**
`CoreServices.HudModel` (read-only `IHudModel` facade) + `RegisterHudModel`/`UnregisterHudModel`, mirroring `CoreServices.Hud`. Models in `Assets/_Modules/Core/HudModel/`. Village WRITES (refs Core), HUD/BattleATB READ (ref Core). No Village↔HUD edge — no asmdef cycle.

**4. Dumb presentation:** each view binds to its model(s), subscribes `Changed`, copies field→widget, dispatches commands. NOTHING else. Representative conversion = `BattleHud9Zone`: remove system-resolution, all fraction/level/threat/star/clock math, friendly-name build, enemy distance sort, loadout resolution, spawn-gate → all move to producers. View renders for `HudContextModel.Context`; not its context → renders nothing (model-driven, not self-gated). Duplicate hero card dies because only the model-designated view renders it per context.

## Staged migration (no big-bang)
- **Stage 0:** FlowTrace `[Flow:HUD]` on every model mutation + context transition (headless-verifiable first).
- **Stage 1 (serialized lane):** Core model records + `IHudModel` + `CoreServices.RegisterHudModel` (additive, dark).
- **Stage 2 (serialized lane):** producers + `HudContextEvaluator`; headless-verify model values match live HUD BEFORE touching views.
- **Stage 3 (parallel, file-disjoint lanes):** migrate views one at a time — EchoWorkforceHud → BattleHud9Zone → VillageHudController (keep `IVillageHud` setters as shims writing the model during transition) → BattleHudUgui.
- **Stage 4:** delete duplicate context logic; `BattleHudVisibilityManager`/`ApplyContext` READ `HudContextModel.Context`. One evaluator.
- **Stage 5 (parallel lanes):** rebuild inventory/weaponskills/talent/gear modals as Views over VMs composing EconomyModel/PartyModel/HeroVitalsModel + Village gear seams. (Needs open-state captures of those four screens first.)

## Acceptance (headless via AutoPilot fleet + break-log.jsonl)
- Exactly ONE `[Flow:HUD]` context transition per real transition (double-evaluator gone).
- Sampled model field == rendered widget value (view is a pure skin).
- Battle: one Knight card, no Echo/Silo, no town skill bar, Heal/Attack/Mode on-screen. Town: town chrome + Echo, no target/cycle/ability bar, no green MP box. Overworld post-wave: no town chrome, no battle bleed. Modal: opening inventory/gear sets Context=Modal (fixes WO-535).
- COMPILE_GATE_OK; braces balanced every edited file.

## What NOT to touch
- No `.unity` hand-edits. No System.Reflection in HUD bridges. HUD→Core only.
- Don't redesign the four modal INTERNALS until their open-state is captured (Stage 5 gate).
