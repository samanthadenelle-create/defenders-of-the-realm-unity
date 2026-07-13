# CANON GROUND TRUTH — 2026-07-03

> ## ⚠ SUPERSEDED 2026-07-08 — this is no longer the live anchor.
> Current anchor = **`CANON_GROUND_TRUTH_2026-07-08.md`**. This 07-03 snapshot is frozen history: it
> predates the 07-07/08 F8 ticket program (P0 tower re-entrancy fix owner-confirmed, 30+ F8 tickets,
> wave-2 close on exe 2026-07-08 05:10:11, WebGL preview `h0h6hfsf5`). Read the 07-08 anchor for live
> state; the body below is kept per §15 (banner, don't rewrite).

> **Purpose:** the single anchor of *current reality*, derived ONLY from verified sources (tonight's
> fleet captures, bake logs, live HTTP reads, the working tree, owner felt-verdicts). If a doc
> contradicts a line here, the doc is STALE. **Supersedes `CANON_GROUND_TRUTH_2026-07-01.md`.**
> Sourced 2026-07-03 from: the 07-02→03 convergence session (~25 agents), fleet runs seeds 4000–8000,
> `git status` (533 paths, commit lanes in progress), live HEAD reads of the Vercel deployment.

## Repo / git
- **Branch:** `wip/village2-and-f8-tickets`. Commit lanes for the 07-02→03 session being staged by
  explicit path (~12 lanes); **push held for owner felt-pass**. Sole committer = CLI.
- `link.xml` mystery CLOSED: load-bearing (Addressables providers vs High stripping), auto-regenerated
  by the WebGL build, back in tree byte-identical. Never commit its deletion.

## Live thread (2026-07-03) — THE FEEL ARC (supersedes web/Pi stabilization as current focus)
- Owner verdict arc: son's "this looks bad" → directive "the most important thing is how it FEELS" →
  after the convergence build: **"I love the terrain… feels like there is something real now."**
- **The ten-year-old test is the standing quality bar** (memory `the-ten-year-old-test`): feel outranks
  new mechanics; headed screenshot-compare before claiming polish.
- **South vertical slice = fleet-proven 6/6** round trips, `tapped=False` (masked warp fires mid-bridge
  both directions). The natural seam (raise → moat → water → bridge) works as designed. Owner felt-pass
  pending. N/W/E expansion waits for south "feels perfect."
- **Post-processing was structurally DEAD until 07-02** (null postProcessData — bloom never rendered
  anywhere, the hub camera cleared to solid near-black so no skybox ever drew). Fixed: WorldFeelInjector
  (ff.worldfeel, dusk "hold the last light" palette) + terrain relief/treelines/density (regenerated).
- **Deployed:** Vercel preview `defenders-of-the-realm-v2-69mafg5pj.vercel.app` = the full convergence
  build (data 79.7MB Brotli, ~20MB under the 100MB/file wall). **Production Vercel stays on the 07-01
  verified Pi sign-in build until owner promotes.** Game IS live on Vercel (correcting the stale
  "Vercel blocked" memory); itch remains live.

## What landed 07-02→03 (all fleet-verified, uncommitted → lanes in progress)
- **World:** castle raise fallout fully fixed (8 tickets); seam debris purged (a batch tool was silently
  reverting purges by re-opening the scene — fixed); prop seating derived from mesh vertices + authored
  lifts (`tree.baseLift` PlayerPref); moat water = plinth face→r58; bridge collidered, pivot-agnostic
  seat; sky-catwalk strip removed; WO-602 return crossings ("Enter Elarion") + glow posts.
- **Character:** double-sided hero/orc materials (26 open shells caused see-through joints); WebGL
  texture crush lifted (2048@q90); walk-at-run-speed root-caused (thresholds 0/2/6 + cadence); enemy
  anim smoothing (raw velocity jitter) + crossfades, generator-proof; `anim.runCadence` knob; KayKit
  side-by-side proof button (editor).
- **Combat:** village HUD no longer bleeds over battles; half-off-screen red Attack removed; stone
  arenas dress in stone; SFX variant pools; `camerashake` toggle; telegraphs/hit-stop verified already
  built. Enemy titles = catalog DisplayName ("Orcish Mage" — never concatenated).
- **Spell VFX:** root cause = half-upgraded URP pack materials (not texture res) — healed at runtime
  seams; VFX-master source-fix + spell language + pooling pass IN FLIGHT at anchor time.
- **HP mystery:** displays were reading a dead duplicate hero (stale instance binding) — rebind fixed;
  max = base+gear+talent (155 correct; the /100 was a lying trace).
- **UI/content:** NPC card standard (speakers schema: name/affiliation/portrait; silhouette fallback;
  Sylas/Sable/Brom/Apothecary portraits wired — owner-generated); vendors data-mapped (vendors.json +
  resolver + regression: no wrong-class gear, authored empty-lines); WC3 perk grid ("unlock" verbiage
  law, grep-verified); shared end-state template (+ field-death screen — the audit's MISSING);
  WO-596 bug report (submit-is-consent + PrivacySensitiveUi + api/bug-report.js — needs deploy);
  star-tofu dead (no project font has U+2605); dev console default-hidden; Pi button Title-only + kit
  chrome; popup-close oracle (19 named NO_CLOSE tickets = the UIToolkit redo backlog, quantified).
- **Tutorial V2 BUILT** behind ff.tutorialv2 (default OFF): 7 owner-ratified steps, tutorial-steps.json
  + interpreter + signals (BuildingPlaced now actually raised) + funnel telemetry + spotlight/banner.
  Flip after its own fleet pass. Legacy TutorialDirector deletion = T5, pending.
- **Fleet oracles added:** PROP_SEATING, POPUP_NO_CLOSE/OPEN_FAILED, HomeReturnRoundTrip (self-arming).

## Owner directives ratified 07-02→03 (BINDING)
- **Read-before-assert is a rule for EVERYTHING** — code and non-code; memory lines are pointers, never
  answers (owner: "being lazy cause you want to be efficient"). Strategic + decisive posture.
- **UI canon extended:** earns-its-place test per element; one action = one button; no dead buttons;
  shared currency chip (gold primacy); every path a shining example; smoothness (eased transitions);
  wave chrome contextual; no untested numbers on the HUD; four rounded controller buttons per mockup.
- **"What CAN stream, SHOULD stream"** — full Addressables is the destination architecture (thin
  JSON + interpreters was always the design for it). WO-545 = next session's headline; blueprint at
  `docs/WEBGL_DELIVERY_PLAN_2026-07-03.md` (boot target 15–25MB; Cloudflare R2 remote; live baseline:
  data 85.21MB, Gear bundle already streaming in prod). VFX pooling mandated.
- **Iteration-till-perfect protocol:** specialist → gate → build → fleet → feed back; owner felt pass
  is the only exit. Spend unconstrained (credits reset Sundays).

## Open decisions (owner)
Un-park seam un-stack WO-453 (encounter-return still strands ~7.1km, 6/6 — publisher critique #1) ·
promote preview→prod · push authorization · wall stairs (remove vs decorate) · ramp decks (stone vs
planks) · necromancer 50% beat · caster cast clip · dungeon theme · CastleMoat default-ON confirm.

## Key docs born 07-02→03
docs/UI_BLINK_CONFORMANCE_AUDIT_2026-07-02.md (+ owner addenda) · docs/TUTORIAL_V2_SPEC_2026-07-02.md
(+ creative scope decision) · docs/MONETIZATION_REVIEW_2026-07-02.md (Curiosity Shop; loot boxes NO-GO
mainnet / GO testnet; dev wallet banked) · docs/PUBLISHER_CRITIQUE_2026-07-03.md (pass-with-revisit) ·
docs/WEBGL_DELIVERY_PLAN_2026-07-03.md · WorkOrders 596–602 · RESUME_2026-07-03_morning.md.
