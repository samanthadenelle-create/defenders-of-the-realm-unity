# RESULT — WO-991 Healing Caravan mobility

**Status:** IMPLEMENTED (shell) — 2026-08-15  
Heal **field unlock** still later; out-of-battle Heart heal remains `HealingFountain`.

## Change

- `HealingCaravanMobility`: NavMesh crawl follow (~1.05 m/s), catch-up leash, **glass HP** (48, 1.75× damage).
- `StructureFactory` attaches mobility when `id == healing_caravan`.
