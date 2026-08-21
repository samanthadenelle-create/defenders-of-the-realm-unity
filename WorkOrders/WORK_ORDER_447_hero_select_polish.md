**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 447 — Hero-Select: POLISH (not rebuild)

**Status: READY TO IMPLEMENT** (design locked; functional verification done by agent 2026-06-17).
Lane: Onboarding (HeroSelect) + UI skin. Mobile-first portrait. Part of the front-door flow (WO-446).

## The finding that reframes this (§5 — verified, not assumed)
A read-only agent verified the actual code (not the UXML comments):
- **There is NO carousel.** `HeroSelectController` builds a **static 4-card grid** (all heroes visible, tap
  to select, one shared `_detailCard`). **No nav arrows, no swipe.** The UXML *describes* arrows; the
  code-built screen does not have them. A swipe carousel = **net-new build**, NOT a re-activation — and it
  fights the `VerifyFourPanelsEven` regression guard + reopens the duplicate-surface decision.
- **It WORKS today** — compiles, runs, no rot. Function is there.
- **The image+lore+stats DATA already exists** — `HeroCardInfo` carries `Hp`/`Attack`/`Speed`/`AbilityName`/
  `AbilityDesc` (WO-329) — but the controller **never renders the stats** (only name/role/blurb).
- **4 heroes** (Mage/Thrain, Knight/Grom, Ranger/Sylas, Cleric/Elara) — not three.
- **Duplicate hero-pick surfaces:** `TitleController` (the LIVE, regression-hardened "title IS the select")
  AND the standalone `HeroSelect` scene (`HeroSelectController`, reached via the intro cinematic path).

## Decision: POLISH the working hero-pick — do NOT build a swipe carousel
"Function → there, purpose → there, **now polish**" (owner). Since a carousel doesn't exist and rebuilding
one is net-new risk (fights the guard, reopens the regression), we **polish what works** into the clean
image+lore+stats look. The owner's carousel condition was *"only if a carousel exists and can work"* — it
does not exist, so we don't chase it.

## Scope (all polish on proven function)
1. **Render the stats that already exist** — add HP/Attack/Speed pip rows + ability name/desc to the card/
   detail UI. Data is in `HeroCardInfo` (WO-329); this is display-only, no new data.
2. **Clean layout: image + lore + stats** — hero portrait + lore blurb + the stat row, single-tap to select,
   single confirm. (Portrait/mobile-first.)
3. **Strip the "dragon"** — remove `BuildDragonStage()` + its call (the `heart-wing` banner top half);
   relocate the brand title/subtitle it contained into the roster panel. Self-contained, no external refs.
4. **Blink Obsidian skin** — restyle from the inline `ElarionUi` stone/gold to Blink Obsidian panel/button/
   slot sprites (consistent with the project's BlinkChrome approach). Visual pass, no structural change.
5. **One canonical surface** — pick ONE: recommend polishing the LIVE `TitleController` surface (or
   consolidating both onto one code-built component) and retiring the duplicate **carefully** — removing the
   Title pick must be coordinated so the "empty hero-pick" regression (the reason it was moved to Title) does
   NOT reopen. Do not blind-delete either surface.
6. **Reconcile hero count** — 4 heroes (incl. Cleric/Elara) vs the cold open naming "three" (Knight/Ranger/
   one Chorister). Narrative reconcile: either the cold open names only the 3 companions + "you", or add the
   Cleric to the lore. (Copy decision, coordinate with WO-446.)

## Acceptance
- [ ] Stats (HP/Attack/Speed + ability) render on the hero pick; image + lore + stats read cleanly on a phone.
- [ ] Single-tap select + single confirm; no two-tap.
- [ ] Dragon/heart-wing banner gone; Blink Obsidian skin applied; no color breaks.
- [ ] ONE canonical hero-pick surface; the duplicate retired WITHOUT reopening the empty-hero-pick regression.
- [ ] Confirm routes correctly onward (today: `GoPetSelect`; reconcile with the front-door routing in WO-446).
- [ ] Compile gate green; brace + NUL guards; no UXML reliance (code-built).

## What NOT to touch / notes
- Do NOT build a swipe/paging carousel (net-new; fights `VerifyFourPanelsEven`; out of scope).
- Do NOT blind-delete either hero-pick surface — coordinate the retire (regression risk).
- Confirm `HeroSelect.unity` UIDocument has valid PanelSettings before relying on the scene (PanelSettingsRepair flagged it).
- §0: CLI edits `.cs` on the Windows path; UI does not touch code. §8: code-built UI only (UXML won't ship).

*Cross-ref:* WO-446 (front-door flow this feeds), `HeroSelectController.cs`, `TitleController.cs` (the live
surface), `HeroCatalog.cs` (the stat data), agent verification report 2026-06-17, memory `ui-mvvm-binding-seam`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
