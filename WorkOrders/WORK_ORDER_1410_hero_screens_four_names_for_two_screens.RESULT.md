# WO-1410 RESULT - Hero screens: ONE source for BAG / SKILLS / LOADOUT, Wisdom explained, Loadout owns the sockets

**Status:** FIXED 2026-09-05 (Codex dev lane; gated by the CLI; device build after the owner's reboot; felt-test closes)

## What landed (worktree `D:\eoa-codex-1410-ready`, base `003b64ce2`, applied three-way)
- `canon-strings.json` twins author `heroBag = BAG`, `heroSkills = SKILLS`, `heroLoadout = LOADOUT` (byte-identical,
  CRLF preserved, +5 lines); every face (deck, chrome, cross-buttons, rail, VMs) reads them through `HudStrings`
  with a site-bearing `FlowTrace`. Retired "TALENT TREE" literal gone (proven RED on base).
- Skills Wisdom chip: `WISDOM N - next point at Level M` with M = level + 1 - TRUE at every level
  (`HeroProgression.WisdomForLevel` returns 2 or 3, never 0; `ApplyLevelRewards` fires per level).
- Loadout is the single socket owner: the Skills quick-swap rail is read-only (assign/clear callbacks deleted, zero
  callers left repo-wide); Loadout empty state = "No skills unlocked yet." + a touch-clamped `OPEN SKILLS` door.
- Re-pointed suites: `HeroSkillTreeDoorRegression`, `InventoryArmoryRailRegression`, `SkillsPanelLayoutRegression`
  (now also FAILS if the Skills VM ever calls `AssignableSkillBarAccess.Assign/Clear`); new
  `HeroNameSingleSourceRegression` (7 cases, RED recipes), registered by the lead.
- Lead review finding fixed before commit: the skill popup's CONFIRM word was a two-way ternary that painted OWNED
  on locked and unaffordable nodes; now one true word per state.

## Evidence
- Read-only lead review (assembly edges, single source, socket ownership, suites not hollow, ASCII, touch floor) - PASS
  except the popup word, fixed. Gates and the opened `HeroSkillTree` / `HeroLoadout` frames: see the commit message.

## Open
- Ruling: the Loadout door reads `OPEN SKILLS` (the WO's diagram) rather than the bare key value `SKILLS` (the WO's
  line 38) - owner's word.
- `invHeaderTalents` ("Talents") is still authored in canon-strings (dormant, pinned into `InventoryArmoryRailRegression`
  AllKeys) - follow-up to retire with the pin.
