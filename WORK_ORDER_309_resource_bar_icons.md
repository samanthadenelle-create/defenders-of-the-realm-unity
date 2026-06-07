# WORK_ORDER_309 — Resource bar with icons + quantity (food/wood/iron/crystals)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Origin:** owner playtest 2026-06-06
**Depends on:** WO-307 (HUD shell) · **Reads:** GameStateService / EconomyService snapshot

## Problem
Resources are plain text scattered across the top ("Wood 200", "Iron 80", "Food 80", "Gems 250") with no
icons and inconsistent naming ("Gems" vs canon "Crystals").

## Goal
A single grouped resource bar where each resource is an **icon + quantity**, themed and readable on web + mobile.

## Scope
- One top resource cluster: Wood, Food, Iron, Crystals — each = icon + count, consistent spacing.
- **Rename "Gems" → "Crystals"** (canon AetherCrystal) everywhere it surfaces in HUD.
- Pull values from GameState/Economy via the HUD bridge; subscribe to change events (live update, no per-frame alloc).
- Placeholder icon set if final art isn't ready (clearly themed, swappable).

## Files
- `Assets/_Modules/HUD/` resource cluster (in the WO-307 shell or a small `ResourceBarPanel.cs`).

## Acceptance criteria
- [ ] Wood/Food/Iron/Crystals each render as icon + quantity in one grouped bar.
- [ ] "Gems" wording is gone — reads "Crystals" consistently.
- [ ] Values update live from Economy/GameState.
- [ ] Readable on web + mobile; HUD→Core only; code-built; brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't change the economy ledger — display only.
