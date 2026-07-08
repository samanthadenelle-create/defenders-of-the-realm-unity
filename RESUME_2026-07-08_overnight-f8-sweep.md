# ☀️ MORNING REPORT — overnight 2026-07-08 (the F8 sweep night)

**Read this first.** Owner directives executed: "fix these and verify all overnight" + "after fixed
deploy to web ui" + "step in step out flags on everything... verified data that we caught the root
cause for every single bug overnight". Everything below is committed LOCAL on
`wip/village2-and-f8-tickets` (~75 commits this arc), gates green at every step,
**git push still HELD for your word**.

## ⭐ THE P0 ARC — "still cant do the tower" (found, fixed, PROVEN)
Your late-night session captured it: after the tutorial dialogue, clicks in build mode did NOTHING —
`HeroLocomotion.InputSuppressed` stuck TRUE, `BuildModeController.Update` frozen at its first gate,
**zero PlaceConfirm evaluations in the whole session**. Root (from the captured trace, not a theory):
a dialogue's `Closed` event that synchronously chains into the NEXT dialogue (exactly what the
tutorial does) let the STALE dialogue's Closed handler destroy the successor's just-built panel —
the new conversation was alive but headless, its `Ended` never fired, input never released. Fix:
the Closed handler is now bound PER-VM and a stale close is IGNORED with a traced Warn
(`DialogueView.OnClosedFor`, commit 82422d11).

## ✅ THE VERIFICATION LEDGER (the directive: root cause + repaired chain, both in captured data)
Every reported-broken flow now has step-in/step-out gates **and** an automated probe that drives the
REAL code path (real input seams, no logic bypasses) in the shipped player exe. Final fleet:
4/4 runs on exe **2026-07-07 23:50:04**, verbatim PASS lines (`Builds/autopilot-tickets.md`, run
summaries 2026-07-08 04:53Z):

| Flow (ticket) | Post-fix verification line (verbatim, player build) |
|---|---|
| Tower placement chain (the P0) | `AssertTutorialFirstTower — PASS — placed 'tower_ground_archer', signal raised (polls=178, clicks=1)` — real click → PlaceConfirm → StructurePlaced → build.tower_placed → step-complete |
| Dialogue re-entrancy (P0 root) | `AssertDialogueChain — PASS — chain A->B survived (staleWarns=1, panelBuilds=2, input released)` — the stale close fires, is IGNORED, successor survives, input releases |
| Tutorial arms on fresh save (F8-29) | `AssertTutorialArms — PASS — armed + LIVE (phase 'AwaitCompletion', fresh save)` |
| Orient-modal click lock (F8-30) | `AssertOrientModalReleases — PASS open+registered+released via CloseAll ('tower_ground_archer')` |
| Wave vendor/shop/build rules (F8-14) | `AssertWaveVendorRules — PASS — authority armed, 10 vendor(s) hidden, shop blocked, build gate open` (restore leg not reached inside the probe window — traced, honest) |
| Compass enemy pips (F8-16) | `AssertCompassMarks — PASS buffer=1 pips=1 rect=10x23px` (≥10x16 visibility floor) |
| Overworld scatter bands (F8-8) | `AssertScatterRecords — PASS gen=18 (near 6/mid 6/far 6) act+3 cull+3` — generation, 85m activation, walk-away cull all exercised |
| White hero (fleet's oldest ticket) | `AssertHeroHasAlbedo — PASS albedo 19/19, no WHITE HERO ROOT` |

**The white-hero "regression" that appeared mid-night was a measurement artifact, now retired for
good:** the first probe run reported `audit 0/19` in the fleet while a windowed run of the SAME exe
showed 19/19. RCA (commit 7e663981): every material read was gated on `Material.HasProperty`, which
consults the *shader* property table — and shaders never resolve under `-nographics`. The audit now
reads the serialized property sheet (ungated GetTexture/GetColor), so it measures the real shipped
binding in every environment — and still fails on a genuinely unbound slot. Your eyes on the hero
remain the final word on COLOR (headless proves binding, not feel).

**Step-in/step-out coverage shipped (stays in, toggleable):** every early-return between "armed" and
the click evaluation in `BuildModeController` names itself while armed (`PlaceLoop BLOCKED at
<gate>: <state>`) plus a 1/sec LIVE heartbeat with input-device state when all gates pass —
"nothing happened" is no longer capturable silently. Same for `OverworldEncounterSpawner.MaintainLoop`'s
four silent gates and the dialogue bootstrap decline.

**Remaining fleet tickets = the 3 known pre-existers, unchanged:** WO-602 home-return unwired ·
CavePortal seam unreachable (bake gap) · WO-453 overworld rep spawn-gate. None are in tonight's scope;
all carry their flow lines in `Builds/autopilot-tickets.md`.

## THE HEADLINE
The entire F8 board from your two felt-sessions (30 tickets) is fixed, spec'd, or evidence-pinned —
every fix carries its verbatim proof line per your RCA-proof rule. The fleet's oldest every-run
ticket (**WHITE PALADIN**) is fixed at the root: the fbx *embedded* its textures all along (the
"not shipped" comment was disproven by byte-scan); extraction + durable remap executed —
`externalObjects=1 (Paladin_MAT)` in the meta, runtime audit `19/19 material(s) carry a _BaseMap`.

## VERIFY LIST (your felt pass — ONE build, exe stamped after the overnight chain)
- **Tutorial end-to-end on a fresh reset:** it arms (bootstrap fixed — it literally could not
  construct before), the Build button EXISTS during the wave countdown (your F8-14 ruling applied
  to the hostile HUD rows), and placing completes `first_tower` (the signal finally has a live
  source — it listened to two dead legacy systems).
- **No click-locks:** the orient modal now registers with PanelManager (ESC/CloseAll/skip all
  release it) and its Orient button is dev-gated out of normal play (F8-30).
- **Hero has COLOR** (white Paladin fix — one look settles the residual stale-check line, see Known).
- **Compass:** red triangle pips across a wide band + edge-arrows for enemies behind you (F8-16).
- **Overworld:** scattered enemy families by distance band, appearing as you approach (F8-8 —
  runtime traces `[Flow:Encounter] scatter …` in your session log are the proof; bots exit the
  overworld too fast to capture them).
- **Dialogue:** one window, Continue/Close never overlap (F8-1/5 — the factory math was measuring
  the canvas pre-scaler; reconciled to the third decimal against your live trace).
- **Waves:** vendors hide with the townsfolk, shops toast closed, building allowed (F8-14).
- **Towers:** Ballista bolts (22 range, heavy), Arcane casts (orb + blast on arrival), Archer fast
  bolts; Ballista stays a ballista at L2/L3; ground placement everywhere.
- **World:** no invisible SE walls (prefab MeshColliders stripped — footprint box is the one
  collider), the flat gray "trap" tiles by the stronghold no longer render (Village2 rebuilt),
  no floating stairs, harvest nodes say Chop Wood / Harvest Food / Mine Iron.
- **HUD details:** resource-dock tab properly sized (the 9-slice collapse), PartyBar names render
  (fleet-verified gone), v8 combat HUD is the default.

## KNOWN / OPEN (the honest list)
- **WHITE HERO residual line:** the old error still logs from an EARLIER stage of the same flow
  while the after-audit proves 19/19 bound — a stale-check ordering artifact to retire; your eyes
  on the hero decide (ticket note added).
- **WO-602 home-return + WO-453 encounter-strand:** the two known pre-existers, unchanged.
- **Owner design pins:** wave-countdown-as-Battle posture (F8-23/26 — should a countdown read as
  combat? the incoming-wave visual spec waits on this) · WO-613B outpost chunk rebuild (spec READY)
  · F8-22 needs a re-capture (its screenshot was destroyed by the old overwrite bug — now fixed:
  flag files are session-stamped).
- **F8-15 death popups:** instrumentation is live — your first death names all three popups.
- **Deploy:** WebGL built overnight + pushed to the **Vercel PREVIEW** (URL in the final HANDOVER
  stamp); production untouched — promotion is yours.

## PROOF INDEX (per the RCA rule — where every claim lives)
Board tickets F8-1..F8-30 carry verbatim lines + commits in their metadata. RCA docs:
`docs/RCA_DIALOGUE_DOUBLE_FRAME_2026-07-07.md` · `docs/STRUCTURE_TRANSFORM_CENSUS_2026-07-08.md`
(risks R1-R6) · `WorkOrders/WORK_ORDER_613B_outpost_prefab_chunk_assembly.md`. Gate logs under
`Builds/og-*.log`; fleet tickets `Builds/autopilot-tickets.md`.

## NEXT (recommended order)
1. Your felt pass on the overnight exe (list above) → name passes → **push**.
2. Rule the two design pins (countdown posture; WO-613B go).
3. Promote the Vercel preview if the web build feels right.
