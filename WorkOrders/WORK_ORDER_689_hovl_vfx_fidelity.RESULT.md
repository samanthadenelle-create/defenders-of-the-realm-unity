# WO-689 RESULT — Hovl VFX Fidelity (2026-07-12) *(renumbered from WO-678, 2026-07-13 collision cleanup)*

**Status: IMPLEMENTED (items 1–2), NO-CHANGE-NEEDED (item 3), DEFERRED (item 4). Pending owner felt-verify.**

| # | Gap | Outcome |
|---|---|---|
| 1 | Bloom off overworld | **DONE** — WorldFeelInjector BloomIntensity 0.45 → **4.5**, threshold 0.90 → **1.1** (demo parity, from the pack's own VolumeURP.asset). Arena local volume aligned 1.4 → 4.5 in the same breath (at priority 100 it would otherwise have DIMMED combat relative to town). Both are the owner-dial consts; `ff.worldfeel = 0` restores the prior look. |
| 2 | Flat tint destroys authored color layering | **DONE** — `ApplyStartColor` rewritten from MinMaxGradient flood-fill to vendor-style **hue shift**: each particle system keeps its authored saturation/value/alpha (bright core / soft halo survives); near-white hot cores (sat < 0.05) untouched; all four MinMaxGradient modes handled; idempotent under pooling. |
| 3 | Trails cut mid-air on projectile stop | **NO CHANGE NEEDED** — the dossier claim was stale: `VFXHandle.Stop()` already defaults to soft-stop (StopEmitting + 2.5 s deferred pool return, VFXHandle.cs:61-88), and no projectile-path caller passes `immediate:true` (verified by grep — only aura/callout/burn cleanups do, correctly). |
| 4 | Impacts spawn unrotated (identity) | **DEFERRED** — hero impact VFX are currently suppressed entirely by the owner's registry-only directive (2026-07-12), so the fix would be invisible; the enemy-cast impact site (Enemy.cs RootedCast) is the one live consumer. Do together with the registry gaining an impact-phase field. |

Verification: brace/NUL gate OK on all three files; compile via the next build gate. Felt-verify = owner: cast anything — effects should GLOW (bloom) and tinted variants should keep their layered look instead of a flat single-color stamp.
