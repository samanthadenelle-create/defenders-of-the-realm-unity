<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 694 — WebTrace LIFECYCLE: capture → retention → read → alert (the web debug rail, end-to-end) *(renumbered from WO-685, 2026-07-13 collision cleanup)*

**Status: READY TO IMPLEMENT** (owner ask 2026-07-12: "work orders for the web trace life").
**Lane:** Platform/Backend (7) + Diagnostics. Halves: Unity client + `api/` (both in THIS repo).
**Context:** the loop was PROVEN live 2026-07-12 (`?trace=1` → `POST /api/trace` → Neon
`analytics_events` → `[sig]` echo in Vercel runtime logs → CLI RCA — the SwordSwing FSB root was
found this way). This WO closes its lifecycle gaps so it survives real players.

## The lifecycle stages + exact asks

### 1. CAPTURE (client) — close the two blind spots
- **1a. Loader-stage beacon:** errors before Unity boots (loader/decompress failures) reach no
  telemetry. STEPS: in `Assets/WebGLTemplates/Pi/index.html`, extend the WO-678 wrapper — on a
  genuine loader error (the