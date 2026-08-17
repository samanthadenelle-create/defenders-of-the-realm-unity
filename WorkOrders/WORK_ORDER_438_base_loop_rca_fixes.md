> ⚠ **UNRESOLVED NUMBER COLLISION — WO-438 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_438_global_tech_skin_rollout.md` (06-13, first-on-disk), `WORK_ORDER_438_base_loop_rca_fixes.md` (06-17), `WORK_ORDER_438_compass_minimap_widget.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 438 — Base-loop RCA fixes (dialogue, companion, enemy placement)

**Status: READY TO IMPLEMENT.** Editor-closed (gate + felt-test). Each item below is RCA'd (read-only
agents, 2026-06-17) with a pinpointed root cause + fix. Surgical, mostly additive. Pairs with WO-437
(input gate). Confirm current line numbers before editing (the MVVM refactor moved some).

---

## A. Dialogue "greyed out after yarn" / "talk doesn't activate on walk up"  — HIGH (95%)
**Root:** `CastleCompanionIntroducerInjector.cs` — the companion-introducer NPC sets a one-shot `_fired`
flag and `Update()` early-returns (`if (_fired) return;` ~L323) but **never deregisters from
`TalkPromptRegistry`**. For the ~0.5s before it's destroyed (`Destroy(holder, 0.5f)` ~L236) it stays
registered; `NearestTalk()` can return its action; tapping Talk hits the stale `Interact()` (returns at
~L361, does nothing) → button lit but dead = "greyed out". Also blocks other NPCs' talk.
**Fix:** in `Interact()` on success (after `_fired=true`, ~L364) call `TalkPromptRegistry.Deregister(transform)`
immediately, AND/OR destroy the holder synchronously (not `0.5f` delayed) — or deregister in the
`if (_fired)` branch of `Update()`. Verify the auto-fire at `AutoFireRadius` (~L350) isn't firing
prematurely. Files: `Assets/_Modules/Village/NPCs/CastleCompanionIntroducerInjector.cs:323,350,361,364`.
**Verify:** after recruiting the intro companion, the Talk prompt + dev tools work normally on other NPCs.

## B. Companion won't leave the tree / "fighting solo, stuck"  — MED
**Root:** `StoryCompanion.cs` follow path — hero resolved via `FindFirstObjectByType<HeroLocomotion>()`
(~L374); if the hero isn't found on first scan (timing) `_heroT` stays null and the companion **parks
idle** (~L768-772), re-resolving every 1s (~L732-740). Also a fallen companion `SetActive(false)`
(~L132) never reaches `UpdateFollow()`. Net: companion strands at its spawn (the central tree) and
doesn't trail the hero.
**Fix:** make hero-resolution robust (also try `FindWithTag("Player")` like the shop's `ResolveActiveHero`,
and keep retrying); ensure `UpdateFollow` engages once the hero exists; verify the leash/catch-up
teleport (~L766-847) actually triggers when the companion is beyond the explore radius. Files:
`Assets/_Modules/Village/NPCs/StoryCompanion.cs:374,732-741,766-847`. **Verify:** companion follows the
hero out of the hub + during a fight, no stranding at the tree.

## C. Party NullReferenceException  — HIGH
**Root:** `PartyHudBridge.cs` reads the companion registry then accesses `c.DisplayName` (~L97); a
companion destroyed between the read and the access passes the C# null-guard (~L89) but is Unity-fake-null
→ NRE. **Fix:** after the null-guard add a Unity-destroyed/active revalidation:
`if (c != null && c.gameObject != null && c.gameObject.activeInHierarchy && slot < rosterCount)`. File:
`Assets/_Modules/Village/NPCs/PartyHudBridge.cs:89-100`. **Verify:** no NRE when a companion falls/despawns.

## D. Companion portrait missing in the party UI  — MED
**Root:** `VillageHudController.SetPartyMember()` (~L2906-2910) silently no-ops when the portrait sprite
resolves null. `PortraitNameForRosterName()` (~L1642-1645) maps roster names → class keys
("Knight/knight" etc.); if the key or the asset name mismatches, `WidgetSprite()` returns null silently.
**Fix:** confirm the portrait keys (~L1630-1631) EXACTLY match the assets under `Resources/HudIcons/` (and
that `DisplayName`/`NameFor()` returns the exact roster names with no case/whitespace drift); add a
`FlowTrace.Warn` when a portrait fails to resolve so it's never silent. Files:
`Assets/_Modules/HUD/VillageHudController.cs:1630-1645,2906-2916`. **Verify:** each party member shows a portrait.

---

## Non-code (note, don't "fix" in code)
- **"Blue spell pixelated"** — texture import on the Frost/Ice particle prefab (maxSize/format/mipmap),
  not a code bug. Inspect the wired VFXCatalog Frost/Ice prefab's particle textures. Separate art task.
- **"Tree prefab at index N missing" ×14** — Unity terrain log; the KayKit Forest pack (gitignored) isn't
  imported, so `ExteriorTerrainBuilder` (~L700-738) builds a sparse prototype array. **Import step, not a
  code bug** — re-import the pack via the `Defenders/Art` tooling. (`PaintTrees` already clamps indices.)

## What NOT to touch
- Surgical fixes only; do not refactor the companion/HUD systems. Do not change the input gate (WO-437).
- §0: CLI edits on the Windows path. Brace-check + compile-gate before commit; owner felt-test per item.

*Cross-ref:* the 4 RCA agent reports (this session), WO-437 (input gate), `GRANT_DEMO_VALIDATION.md`.
