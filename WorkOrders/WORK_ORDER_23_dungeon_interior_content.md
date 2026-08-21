<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 23 — Dungeon interiors are placeholder primitives (content)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

**Date:** 2026-05-24 (filed from owner playtest triage). **Authority:** #35 + WO-025.
**Priority:** High. **Depends on:** WO-05. **Class:** CONTENT (missing gitignored art).

## Bug (#3) — "dungeons only load into a stub"
The dungeon ROUTING is correct (WO-19 entrances → `SceneRouter.LoadScene("Dungeon_HealersCottage"/"Dungeon_FolksGranary")`, both in Build Settings), and both scenes are fully authored (rooms, lanterns, Bryn, encounters, crafting, controllers). **But the geometry is built from Unity primitives + `URP/Lit`, with dozens of objects literally named `[PLACEHOLDER] … (no KayKit mesh)`** — because the builders (`DungeonSceneBuilder.cs`, `FolksGranaryBuilder.cs`) assemble from the **KayKit Dungeon Remastered** pack under `Assets/Models/KayKit/…`, which `.gitignore` excludes (`/Assets/Models/`) and which is **not present locally** (only KayKit Adventurers/Forest/Hexagon packs are). Missing mesh path → labelled placeholder primitive → the "empty room with basic shapes" the owner sees.

(There is also a separate `DungeonStubBuilder.cs` that makes a literal floor+4-walls+capsule stub — the "square room with two pills" — but the two Build-Settings dungeons are the richer scenes, not that.)

## Fix (content)
1. Copy **KayKit Dungeon Remastered 1.1** into `Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/` (it's in the owner's `C:\Users\Elden\Downloads\The Complete KayKit Collection v5 (1)\…`). robocopy, same pattern as the other KayKit packs.
2. Re-run the dungeon builders so the `[PLACEHOLDER]` primitives resolve to real KayKit meshes.
3. (Same gitignored-`Models` fresh-clone class as WO-05/18 — the pack lives outside git by design.)

## Acceptance criteria
1. `Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/` present; dungeon builders find the meshes.
2. Entering `Dungeon_HealersCottage` / `Dungeon_FolksGranary` shows real KayKit dungeon geometry, not `[PLACEHOLDER]` primitives.
3. `WORK_ORDER_23_*.RESULT.md` with screenshots + the placeholder-count before/after.

Key files: `Assets/Editor/DungeonSceneBuilder.cs`, `Assets/Editor/FolksGranaryBuilder.cs`, `Assets/Scenes/Dungeon_*.unity`, `.gitignore`.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `Dungeon_HealersCottage.unity:1979,5642,81244` — 3+7 [PLACEHOLDER] objects; needs re-bake. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
