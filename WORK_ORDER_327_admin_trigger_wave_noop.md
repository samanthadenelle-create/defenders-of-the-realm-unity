# WORK_ORDER_327 — Admin "Trigger next wave" does nothing (ForceBeginNextWave)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `WaveManager`, `AdminOverlay` (the owner-only panel)

## Problem
The Admin (owner-only) panel's **"Trigger next wave"** button does nothing. The panel itself prints
*"Triggered ForceBeginNextWave() — if missing, will need to add the public method."* — i.e. the admin tool
calls `ForceBeginNextWave()` but the method is **missing or not wired** on WaveManager, so no wave starts.

## Goal
Pressing "Trigger next wave" actually begins the next wave immediately.

## Scope
- Add/confirm a public `WaveManager.ForceBeginNextWave()` that starts the next wave now (advances the wave
  counter + spawns), safe to call between waves and mid-lull.
- Wire `AdminOverlay`'s button to it (resolve the live WaveManager instance; null-guard).
- Confirm it works in town defend + DTT contexts where a WaveManager exists.

## Acceptance criteria
- [ ] Clicking "Trigger next wave" begins the next wave (enemies spawn, wave counter advances).
- [ ] `ForceBeginNextWave()` is public + safe to call between waves (no double-start/soft-lock).
- [ ] No NullReferenceException from the admin path (resolve + guard the WaveManager ref).
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Confirmed (the WO's premise is wrong).** `WaveManager.ForceBeginNextWave()` **exists and is
public** — `Assets/_Modules/Village/Waves/WaveManager.cs:493`. So "the method is missing" is incorrect; the
real cause is on the Admin side:
- `AdminOverlay.OnTriggerWave` resolves the WaveManager by reflection `FindObjectOfType`
  (`Assets/_Modules/HUD/AdminOverlay.cs:186-192`) then `InvokeMethod(_waveManagerInstance, "ForceBeginNextWave")`
  (`:221-226`). `InvokeMethod` **returns silently when the instance is null** (`:212-217` `if (obj == null) return;`).
- So if no live `WaveManager` exists in the current scene/context (e.g. town before a defend session), the
  button does nothing with no feedback. Additionally, calling it during `WavePhase.Active` is an intentional
  no-op that just logs (`WaveManager.cs:502-504`).
- The status line "if missing, will need to add the public method" is misleading — the method is present.

**Suggested minimal fix:** in `AdminOverlay`, when `_waveManagerInstance` is null, surface a clear status
("No WaveManager in this scene") instead of silence; otherwise the existing call works. No WaveManager fork
needed — it already has the public entry point.

## Do NOT touch
- No `.unity` edits. Don't fork WaveManager — add the public entry point + wire the existing AdminOverlay button.
