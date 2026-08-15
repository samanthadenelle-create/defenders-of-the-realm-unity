# RESULT — WO-996 armor.json dual-copy

**Status:** IMPLEMENTED — 2026-08-15

## Decision

- **Resources** = curated **runtime** set (CanonicalJson wins).
- **StreamingAssets** = **library** superset (fallback path).

## Change

- Merged 15 class ladder rows (`armor_{knight,mage,ranger}_{common..legendary}`) into StreamingAssets.
- SA version **1 → 2** (matches Resources).
- SA total rows **30 → 45**. Resources unchanged at 24 (no blink_ placeholders added).
- Header notes on both files (WO-996).
- `DataRegression.CheckArmorDualCopy`: Resources ids ⊆ StreamingAssets + version equality.

## Not done

- Full `GearCurationExporter` for armor (weapons-style generator) — optional future.
- blink_ rows remain library-only placeholders.
