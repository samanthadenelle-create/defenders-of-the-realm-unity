<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 330 — "No account → local-only progress" player warning

**Status: READY (small) — pick the surface.** **Lane:** 4 (UI/HUD) + 7 (Persistence).
**Origin:** 2026-06-07 roundtable. NOT a bug — the save system is working AS DESIGNED:
without a connected wallet/account the backend sync correctly **no-ops** (both
`GameStateService.SyncToBackend` line ~868 and `LoadFromBackend` line 672 guard
`if (string.IsNullOrEmpty(_state?.BoundWallet)) return;`), and progress still saves
**locally** via PlayerPrefs (offline-first). The only gap is the player isn't TOLD.

## Goal
When no account/wallet is connected, warn the player **once**: *"Heads up — without an
account your progress is saved on this device only and won't sync to the cloud. Connect a
wallet to enable cross-device save."*

## Surface (owner pick — design call)
- **A. One-time Yarn line** (fits the dialogue-as-interaction-layer pattern) — fire a
  `<<warn_no_account>>` style notice during/after the intro when `BoundWallet` is empty.
- **B. Startup HUD banner** — add a generic notice to `IVillageHud` (none exists today; only
  `ShowWaveClearBanner`/`ShowRepairPrompt`) and show it on first village load with no wallet.
- **C. Settings/Store line** — a persistent "Local-only — connect a wallet to sync" status row.

## Wiring
- Gate on `GameStateService.Instance.State.BoundWallet` being null/empty.
- Show ONCE per session (a static shown-flag), not every save.
- Optional: a dev `Debug.LogWarning` at the backend-skip point for telemetry.

## Notes
- Do NOT force a login (offline-first is the design). This is informational only.
- Local WO; next free 332 (331 = harden save/load tests).
