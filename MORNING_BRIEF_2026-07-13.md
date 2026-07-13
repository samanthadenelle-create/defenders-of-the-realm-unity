# MORNING BRIEF — 2026-07-13 (built overnight 2026-07-12)

## YOUR BUILD (Seeker-ready; share link bypasses the Vercel login wall, valid ~24h from 21:26 07-12)
- Play:  https://defenders-of-the-realm-v2-9ncz1sks9.vercel.app/?_vercel_share=qtfu5qboYkyYOhNl0OmavSf1d0zb76oR
- Trace: append &trace=1 to that URL so the session streams to the DB for triage.
- Clean SHIP build (BuildOptions.None) - no full-screen error overlays. Preview only; prod untouched.

## WHAT TO CHECK (tonight's fixes)
1. Sound on/off is now under Settings, removed from the HUD (was over the mobile controls).
2. Build mode: left-side d-pad moves the armed/placed structure; PLACE button; "Rotate Left / Rotate Right" text (no tofu boxes).
3. Menus de-glyphed (rotate menu, tower/crystal/fountain panels) - HUDUI tofu oracle 47 -> 5.
4. Combat audio plays + no stutter; SwordSwing FSB decode fixed + combat sfx pre-warmed on battle load.
5. Pi sign-in: no raw "promise timed out" popup in a normal mobile browser.
6. NEW: upgrade panel redesign (WO-675, behind ff.buildingupgradepanel); DeNelle Tools hub in-editor.

## IF SOMETHING BREAKS
- From Claude Code on your phone: say "triage - <what you saw>" (skill: /triage-web-issue). It pulls the
  web-trace from the DB, RCAs it, writes a WO left READY for the Windows machine.
- Notion runbook: "SKILL: Triage Web Issue (phone runbook)"; drop notes in the Notion "CLI Inbox".

## STATE FOR THE NEXT CLI
- Read START_HERE.md. ~20 commits local tonight, push HELD. COMPILE_GATE_OK; DataRegression baseline =
  8 truthful fail-by-design/known reds, zero new. 6 regression SME suites cover every architect path.
- Highest-value next move (PM audit docs/PM_AUDIT_2026-07-12.md): close the demo-lethal flow bugs
  (WO-602 home-return, WO-453 encounter strand) and do ONE real Pi-Browser traced run.
