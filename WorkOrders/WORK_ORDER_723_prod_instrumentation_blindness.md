# WORK ORDER 723 — Ship builds are BLIND: FlowTrace is compiled-on but runtime-off in prod

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-15 (from the `CLI_LANES_WO_NUMBERS.md` banner; next free bumped to 724)
**Lane:** Core / Diagnostics (file-disjoint from BuildMode UI + VFX lanes)
**Origin:** fallout of the 2026-07-15 magenta-ground RCA — found while proving that bug, not by theory.

---

## The finding (PROVEN)

`Assets/_Modules/Core/Diagnostics/FlowTrace.cs:28`

```csharp
public static bool Enabled = Application.isEditor || Debug.isDebugBuild;
```

Ship builds are `BuildOptions.None` (`WebGLBuild.cs:124`; `build-webgl.ps1:90-93` only passes
`-devBuild` behind an opt-in switch that deploys never use — and correctly so, per KEY_FACTS:
a Development WebGL player paints the full-screen error overlay).

Therefore in the **deployed build**: `Debug.isDebugBuild == false` → **`FlowTrace.Enabled == false`** →
every `[Flow:*]` line is suppressed. That includes:

- all `[Flow:FloorDiag]` TERRAIN / GROUND / LIGHTING dumps (`MagentaGuard.cs:223-271`)
- every `MagentaGuard` FLOOR-FIX / recovery line
- `FloorDeepDiag` entirely (`FloorDeepDiag.cs:68-147`)
- every `Guard.Try` / `Guard.TryEach` failure report

**The guards still ACT — only their reporting is gated.** So prod self-heals silently and tells us nothing.

## Why this matters (the concrete cost, already paid)

The owner reported a magenta ground on the LIVE web build on 2026-07-15. The live build emitted
**zero** diagnostic lines. The RCA only succeeded because a *desktop* `Player.log` (a Development
build) happened to exist from the night before and contained the proving line:

```
[Flow:FloorDiag] TERRAIN 'ExteriorTerrain' ... mat='<NULL>' shader='<null-mat-or-shader>' broken=False
```

Without that lucky desktop log there was **no path** to the §12 proving line for a live-web bug —
which directly contradicts CLAUDE.md §12 ("no code edit until CAPTURED DATA proves the cause") and
the whole WebTrace investment (`?trace=1` → `POST /api/trace` → Neon `analytics_events`): the trace
pipe is live, but FlowTrace never hands it anything in a ship build.

## The tension to resolve (this is the actual design question — do NOT just flip the flag)

- We CANNOT ship a Development build (full-screen error overlay — hard owner law, KEY_FACTS).
- We MUST NOT spam a shipped player's console/network with hot-loop traces (perf + cost; the open
  `/api/trace` POST has **no rate limit** per KEY_FACTS, and there is **no TTL cron** on trace rows).
- We DO need Fail/Warn-class lines out of prod, on demand, for exactly this bug class.

## Proposed shape (implementer may improve — spec the decision, don't guess it)

1. Decouple `FlowTrace.Enabled` from `Debug.isDebugBuild`. Suggested precedence:
   `Application.isEditor || Debug.isDebugBuild || FeatureFlags.ff.webtrace || <?trace=1 URL param> || <account flag>`
   — i.e. the SAME opt-in that already drives WebTrace, so one switch lights the whole pipe.
2. Consider severity tiering so an opt-in prod session ships `Fail`/`Warn` only, with `Step`/`Throttle`
   staying dev-only. A magenta ground is a `Fail` — that alone would have solved this in one read.
3. Ensure `FlowTrace.Fail` reaches `POST /api/trace` when the trace flag is on, so the CLI read path
   (the `[sig]` echo in Vercel runtime logs) sees it. Errors stay **quiet for the player** (owner law:
   "not a giant json failure screen") — loud only in the db.
4. Keep the default OFF for an ordinary player. Opt-in only.

## Acceptance criteria

- [ ] A ship (`BuildOptions.None`) WebGL build opened with `?trace=1` emits `[Flow:*]` Fail/Warn lines
      that land in Neon `analytics_events` and are readable via the `[sig]` echo in Vercel runtime logs.
- [ ] The same build WITHOUT the flag emits nothing (no perf cost, no network chatter, no player-visible change).
- [ ] Proven by capture, not assertion: deploy a preview, hit it with `?trace=1`, paste the retrieved
      `[Flow:FloorDiag]` TERRAIN line in the RESULT. (Temporarily nulling the terrain material is a clean repro.)
- [ ] `COMPILE_GATE_OK` + brace/NUL on every `.cs` touched.
- [ ] KEY_FACTS "the instruments are OFF in ship builds" bullet updated in the SAME commit (§15).

## What NOT to touch

- Do **not** flip ship builds to `BuildOptions.Development` — that reintroduces the error overlay.
- Do **not** widen the open `/api/trace` POST surface without rate limiting (KEY_FACTS security H1).
- Do **not** restyle/refactor MagentaGuard or FloorDeepDiag here — this WO is the *reporting path* only.
- Stay out of the BuildMode UI (WO-719) and VFX (WO-715) lanes.

## Reference

- `Assets/_Modules/Core/Diagnostics/FlowTrace.cs:28` (the gate)
- `Assets/_Modules/Core/MagentaGuard.cs` · `Assets/_Modules/Core/Diagnostics/FloorDeepDiag.cs:32`
  (note: also hard-scoped to `TargetScene = "MainCastle_Hall"` — it never runs in the live merged
  world `Main_Castle_Overworld`; worth fixing here or in its own ticket)
- `Assets/Editor/WebGLBuild.cs:124` · `build-webgl.ps1:90-93`
- CLAUDE.md §12 (instrument-don't-guess) · §14 (F8 live triage) · KEY_FACTS "Backend / web"
