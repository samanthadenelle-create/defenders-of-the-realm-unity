# PAIRWALK — WO-716 (owner PASS/FIX sheet)

**Built:** 2026-07-15 overnight (Grok-03 run) · **Branch:** `wip/village2-and-f8-tickets` · CLI does NOT mark PASS.

> ## ⚠ REBUILT 2026-07-15 18:45 — USE THE LINKS BELOW, the 07-15 early URLs are SUPERSEDED
> The `er71p62s5` preview and the 07-14 22:00 exe were BOTH broken in ways that made a pair-walk
> meaningless, and are withdrawn:
> - **Magenta ground** — the terrain material was never in git; `materialTemplate` resolved NULL.
> - **No town structures** — that exe was built 45 min BEFORE the art landed; it shipped 4 of 41 models.
> Both fixed in `fdad1ce5`. The surfaces below are the first ones worth marking.

## How to review (two surfaces — use either)
- **LIVE WEB PREVIEW (best — this is the demo):**
  https://defenders-of-the-realm-v2-psdvw6e46.vercel.app/?x-vercel-protection-bypass=z5Q9cJNC4JpMoxgXDsddkK8oe7BFGlyP&x-vercel-set-bypass-cookie=true
  - Open on your **phone** (Pi Browser / any mobile browser) to felt-test the CoC build HUD landscape.
  - The bypass param defeats the team SSO wall (`ssoProtection: all_except_custom_domains`) and does
    NOT expire like a dashboard share link. Verified: the `_vercel_jwt` cookie carries the 127.9 MB
    `.data` fetch, so the whole game loads, not just the first page.
  - Revoke any time: `PATCH /v1/projects/defenders-of-the-realm-v2/protection-bypass` with `{"revoke":{...}}`.
- **DESKTOP EXE:** `Builds\Windows\DefendersOfTheRealm.exe` (2026-07-15 18:31; Development build;
  F1 = dev tools). Build mode = the demo. NOTE: the desktop build is the only one that emits
  `[Flow:*]` diagnostics — the web ship build is instrumentation-blind (WO-723).

> **Note on captures:** the automated headless screenshot fleet (`run-autopilot-fleet.ps1 -Graphics`) was **skipped** — a windowed graphics capture in the non-interactive overnight context reliably produces blank shots, and per the WO's own "don't burn the night" rule I used the live build as the review surface instead. The live preview is a strictly better pair-walk than a contact sheet.

## Screens to walk (mark PASS or FIX)

| # | Screen / flow | Where to look | PASS / FIX | Owner notes |
|---|---------------|---------------|------------|-------------|
| 1 | Title | boot | | |
| 2 | HeroSelect | title flanks | | |
| 3 | FoundingDialogue | new game / Sylas | | |
| 4 | **Build HUD — Town tab** | Build → Town (large CoC carousel, all-pool wallet, PLACE bar) | | ⭐ new HUD |
| 5 | **Build HUD — Defenses tab** | Build → Defenses | | ⭐ new HUD |
| 6 | **Build HUD — carousel size (phone)** | Build, on phone | | you asked to enlarge — 160→260px tiles |
| 7 | **Build HUD — place a building** | pick tile → ghost → PLACE | | real buildings (art now in) |
| 8 | **Build HUD — Lean Touch camera** | two-finger pinch=zoom, twist=rotate | | |
| 9 | **Build HUD — backup D-pad (bottom-left)** | drag the d-pad while placing | | |
| 10 | UpgradePanel | tap a placed building → Upgrade | | |
| 11 | Shop | store building | | |
| 12 | SettingsPause | pause | | |
| 13 | WaveReport | after a wave | | |
| 14 | CombatVitals | in an arena fight | | |
| 15 | Bloom / VFX feel | any spell / tower bolt | | GROK-01 #1: bloom now ON globally |
| 16 | Button sizes / contrast on phone | any panel, on the Seeker | | touch floor 112px + deeper green/red faces |
| 17 | **Ground is NOT magenta** | walk the courtyard / overworld | | ⭐ 07-15 fix — terrain material restored |
| 18 | **Town structures are real buildings** | Build > Town, place one | | ⭐ 07-15 fix — 37 models were missing from the old exe |

## After you mark this
Tell the CLI: **"do 720 on the FIX rows"** — I'll fix only what you marked FIX (and felt-pass whatever passed).
