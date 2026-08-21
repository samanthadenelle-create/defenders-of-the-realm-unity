<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-10
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-10) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — Knight 4-Button Always-Visible Combat Set

**Status:** DONE — owner-confirmed 2026-08-21.
**Minted:** 2026-07-10 (owner)
**Project:** Defenders of the Realm (Unity 6 + URP)
**Hero:** Knight
**Animation source:** ActorCore packs — Hero Motion, Sword & Shield Moves, Magical Moves
**Goal:** Polished, responsive 4-button combat for the Knight that feels great and reuses the mocap we own.

## ⚠ RECONCILIATION (do NOT greenfield — owner "reuse built systems")
This maps onto EXISTING systems — refactor them, don't reinvent:
- **Controller already exists:** `Assets/Editor/.../KnightPackageControllerBuilder.cs` builds `KnightPackage.controller` with `Attack0` + `Cast_q/w/e/r` states from `SpellCastClips`. Its slot labels are STALE WO-494 names ("Shield Bash/Lantern Charge") mismatched to live abilities — realign, don't rebuild from scratch.
- **Clips already imported:** ~60 `Combat_Weapon_WeaponSkill_*` / `Signature_*` / `Passive_*` `.anim` in `Assets/HeroPackages/Knight/Animations/Extracted/` ARE the ActorCore Studio Mocap Hero set, retargeted to the live Paladin (`Knight_Hero`) avatar. Old-rig source under `Assets/Action/Knight/*.fbx` (incl. the true `standing melee run jump attack.fbx`).
- **HUD already renders 4 always-visible slots:** the Q/W/E/R arc (`HudKitController.BuildAbilityRow`) hugging the Attack pill = the "4 buttons". Owner ruling [[hud-ability-routing-skilltree-to-hotswap]]: this arc = FIXED class kit (from best mocap); skill-tree actives go to the separate hot-swap row.
- **Cast path exists:** `HeroAbilities.TryCast(slot)` → `CastResolved` → `PlayAttack/PlayCast`. GAP: the `dash`/`knockback`/etc. effect branches never select a bespoke clip — needs an animation hook.

## Button layout (always visible = the Q/W/E/R arc)
1. Basic Attack (light / combo starter)
2. Heavy Attack / Shield Bash
3. Skill 1 (Cleave / Sweep)
4. Skill 2 (Charge / Shout / Block)

## WO-KNIGHT-ANIM-001 — Inventory & select best animations (P0)
Enumerate every relevant Knight animation across the three ActorCore packs in `Assets/`. Table: Animation Name | Pack Source | Type (Attack/Block/Locomotion/Special) | Recommended Use (Basic/Heavy/Skill1/Skill2/Idle) | Quality Notes (smoothness/length/blend potential). Focus: Sword & Shield Moves (primary), Hero Motion (supplement), Magical Moves (empowered versions).
**Deliverable:** `docs/animations/Knight_Anim_Inventory.md`

## WO-KNIGHT-ANIM-002 — Animator controller architecture (P0)
Refactor `KnightPackageControllerBuilder` into a clean Base + Combat layer set:
- Base: Idle (armed/shield-up variations), Locomotion (Walk→Run blend tree), Jump/Fall/Land.
- Combat (higher priority): Basic Attack Combo (3–4 hits), Heavy/Shield Bash, Skill 1 (Cleave/Sweep), Skill 2 (Charge/Defensive), Block/Parry stance, Hit Reactions + Death.
- Params: `float Speed`, `bool IsAttacking`, `bool IsBlocking`, `int AttackIndex`, `Trigger Skill1`, `Trigger Skill2`. Blend 0.15–0.3s; Exit Time + fixed duration where appropriate.
**Deliverable:** working controller + params/transition docs.

## WO-KNIGHT-ANIM-003 — Input & combat system mapping (P1)
Wire the 4 always-visible buttons: B1→light combo (advances AttackIndex), B2→Heavy/Shield Bash, B3→Skill 1, B4→Skill 2. Include cooldowns, animation locking (or interrupt for feel), skill-tree/combat-event integration, VFX triggers on key frames (swing impact, bash hit).
**Deliverable:** updated Knight input/combat script driving Animator via SetTrigger/SetInteger.

## WO-KNIGHT-ANIM-004 — Polish & blending (P2)
Smooth locomotion↔attack transitions, root motion where beneficial (charges), IK/hand adjust if shield looks off, feel-test attack timings, VFX hooks for impacts (pair with Hovl later).

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: the four-button knight combat is already done.
