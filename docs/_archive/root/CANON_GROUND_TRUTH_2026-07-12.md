# CANON GROUND TRUTH — 2026-07-12 (evening)

> ⚠ **SUPERSEDED 2026-07-13 — the live anchor is now `CANON_GROUND_TRUTH_2026-07-13.md`.**
> Known-stale lines here: the world statement (line ~70 — MergedWorld is ON, the live scene is
> `Main_Castle_Overworld`), "~95+ commits ahead / push HELD" (a push landed 07-12 morning; ahead 22
> as of 07-13), and `ff.strategicplacement` (REMOVED 07-13, WO-695).

> **Purpose:** the single anchor of *current reality*, verified from the working tree, HEAD, the db
> (WebTrace runtime-log echo), the exe on disk, and owner rulings given live this session.
> **Supersedes `CANON_GROUND_TRUTH_2026-07-08.md`** (banner it SUPERSEDED). If a doc contradicts a
> line here, the doc is STALE.

## Repo / git
- **Branch `wip/village2-and-f8-tickets`, HEAD `f123859d`** (build-mode pointer-universal input +
  PLACE button). ~95+ commits ahead of origin, **push HELD** (owner's call). Sole committer = CLI.
- **Save schema = v30** (v29 heroLevel/heroXp/heroLifetimeXp F8-47; v30 strategicPlacementMigrated WO-673 — verified from SaveSchema.cs by the CoreSave SME 07-12; "v29" claims are stale). PetName + Settlements ARE persisted (GameState field comments claiming otherwise are stale); Tribes/Wards/Arena W-L are NOT (fail-by-design oracles in CoreSaveRegression).
- **Dirty tree (in-flight, gated `COMPILE_GATE_OK` 2026-07-12):** WO-677 mobile build-mode verbs
  (uGUI verb bar rebuild + cancel latch + select-loop instrumentation) + WO-678 Pi-SDK 120s timeout
  clean wrap (index.html unhandledrejection/showBanner ownership + PiBridge/C# late-error handling)
  + AutoPilotDriver probes + HelpMenu changes. WO-682/683 implementation lanes in flight (agents).

## Build / deploy state
- **Windows exe:** `Builds/Windows/DefendersOfTheRealm.exe` stamped 2026-07-12 16:40 (post-677/678
  tree). Owner's last named felt-verify exe was 2026-07-11 23:51:48.
- **WebGL PREVIEW (current): https://defenders-of-the-realm-v2-mexharnff.vercel.app** (built from
  the 677/678 tree, deployed 2026-07-12 17:29; supersedes `h0h6hfsf5`). Preview URLs sit behind
  Vercel SSO — device testing needs a share-bypass link (23h expiry) or a protection change.
- **Prod UNTOUCHED** = deployment `dpl_HqS5KBchwUdv79nLHKK1Ymuv3GzD` (07-04), aliased to
  `defenders-of-the-realm-v2.vercel.app`, serving the Pi build + the `api/*` serverless functions.
- **`api/` lives IN THIS REPO (gitignored)** — `C:\EOA\api\` (trace.js, events/track.js, game/*,
  bug-report.js, schema.sql). The "backend is a separate React repo" line in older canon is WRONG.

## Web debugging — PROVEN WORKING end-to-end this session
- **WebTrace (WO-443) is LIVE:** web sessions stream the full log pump to `POST /api/trace` →
  Neon `analytics_events` (event_name `web_trace`; NOT a `web_traces` table — the WO's table was
  never made; the sink reuses analytics_events). `?trace=1` / `ff.webtrace` / account flag activate.
- **The CLI's db read path = the runtime-log echo:** `api/trace.js` echoes a summary + every
  SIGNAL line (`[sig]` — errors, Fail, Exception, Pi flow, [Flow:Perf], [Flow:Build], flagged…) to
  Vercel runtime logs, BECAUSE `DATABASE_URL` is a sensitive env var (unpullable). Query via the
  Vercel MCP `get_runtime_logs` or `vercel logs`. Full non-signal lines are db-only (Neon console).
- **Proven capture this session:** `error: Loading FSB failed for audio clip "SwordSwing"` ×2
  sessions, each paired with a LOW-fps stall (167ms / 4000ms) → WO-682's proving lines.

## Live thread — mobile-web demo readiness (the owner's evening session)
- **Owner rulings (2026-07-12 evening, all specced):**
  1. **WO-682 — web errors caught QUIETLY**: never a player-visible failure screen ("not a giant
     json failure screen"); kill the Development-build error overlay on ship WebGL; fix/guard the
     SwordSwing FSB decode; **pre-warm combat audio on battle load** (owner ask) with dead-clip
     skip. Proving lines in the WO.
  2. **WO-683 — build-screen D-pad**: the SAME kit d-pad as the combat/friendly HUD shows in
     build mode; its vector merges into the build move-read (~BuildModeController:2049) to move
     the armed/moving asset; verb-bar labels become TEXT "Rotate Left"/"Rotate Right" (screenshot
     evidence: ⟲/⟳ render as tofu "□"; palette chips "□ Land + Air" same fix). P0 — "demo is
     unplayable without it."
- **Working on device (screenshot + db):** WO-677 uGUI verb bar renders; touch placement chain
  healthy end-to-end in multiple sessions (`Armed → tap → PlaceConfirm → Place() → under-construction`).
- **Open web perf signal:** browser long-task violation 229ms + scattered `[Flow:Perf] LOW` lines
  around scene-load/audit moments; F8-37 arena audit walks 51 renderers in one frame (soft suspect
  `Backdrop_Cap`). Watch after WO-682's prewarm lands.

## Hygiene debts (flagged, not yet paid)
- **WO numbering authority is ~270 stale** (`CLI_LANES_WO_NUMBERS.md` says next-free 412; disk max
  is now 683). **Collisions on disk: 677** (mobile-buildmode vs HANDOVER's "Asset Caster") **and
  678** (Pi timeout vs Hovl VFX RESULT). Needs an authority-doc refresh + renumber ruling.
  *(RESOLVED 2026-07-13: collisions 677–683 + 685 renumbered — losing-side specs now 688
  asset-caster, 689 hovl-vfx, 690 swordshield, 691 blink-orcs, 692 blink-icons, 693 jeweler-crafting
  readability, 694 webtrace-lifecycle, 695 strategic-placement-lock-on; banner next-free = 696.)*
- SAMANTHA.md (root) is the boot-confirmation gate but is NOT referenced from CLAUDE.md — linked
  now via memory `samantha-md-boot-confirmation-first`.
- Loader-stage web errors (before Unity boots) reach no telemetry — WebTrace can't install if the
  engine never starts; the WO-678 wrapper shows them locally only. Candidate follow-up: a tiny
  template JS beacon POSTing loader errors to `/api/trace`.

## Canon corrections carried forward (unchanged from 07-08/07-11)
- V1 = single Knight in overworld + isolated real-time BattleArena; base/tower-defense V2-gated.
- Hero = single Tripo self-rigged model; Blink = UI kit only. Orc rig family CLOSED via AccuRig
  re-export (07-11). Registry-only motion VFX (owner directive 07-12 morning).
- World: hub `MainCastle_Hall` + additive OuterWorld; `Village2` raid target; `Village.unity`
  ABANDONED. Title "Echoes of Elarion" / series "Defenders of the Realm" / "Hold the last light."
- Player-defined map pivot (07-11): build mode IS the demo; Pi hackathon target July 31.

## Read order for a cold start
This file → `SESSION_CANON_LOADER.md` → `SAMANTHA.md` (boot gate) → `docs/HANDOVER.md` (07-12
morning addendum) → `docs/MASTER_CATALOG.md` (area) → `CLAUDE.md` + `PREFLIGHT_GATE.md`.
