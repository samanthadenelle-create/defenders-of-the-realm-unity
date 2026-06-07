# Lanes — Work-Order Numbers Only (for CLI)  ·  topped up 2026-06-06

Branch **feat/tower-core-loop**. Numbers only, run order. `→` = serial (same lane, in order);
commas = parallel-safe. Detail in `MASTER_PIPELINES_BACKLOG_2026-06-06.md`. New WOs ≥290 are spec'd by
this session's design docs (see "Newly minted" below) — full WO files on request.
**Numbering authority = the master doc + this file, NOT the filesystem max. Next free WO = 335** (287/288/306–334 used, 289 free, 290–305 minted).
**Live board (Notion mirror):** https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f — see `NOTION_SOURCE_OF_TRUTH.md`.

---

## Newly minted this session (≥290 — keeps lanes full)

- **290** QuestService + quest tracker UI (backbone for all questlines) — *foundational, do early*
- **291** Vendor Yarn pack (9 NPCs) + NPCCommandBridge quest verbs (StartQuest/Advance/Complete/GiveKeystone)
- **292** Keystone → Spire finale wiring (≥6 Keystones → Spire Defense → Necromancer)
- **293** Crafting tiers (Common/Fine/Master/Legendary) + legendary recipe system
- **294** Forgemasters' Saga: 4 deep crafter Yarn files + 3 reconciliation scenes
- **295** Legendary set "Aegis of Elarion" items + Oathweld ward effect
- **296** Reforge choice (Heart vs cleansed regions) → finale/ending wiring
- **297** Pet acquisition + slots (tame / egg-hatch / rescue)
- **298** Pet skill catalog content + balance (4 branches + signatures)
- **299** Pet bond questlines (Fenn "Wild Hearts" + per-species)
- **300** Elarion weaponsmithing lore integration (item flavor, maker's marks, appraisal)
- **301** Party persistence — wallet-keyed roster in GameState (+ migrate pet PlayerPrefs blob)
- **302** Floating health-bar oversize fix (green-pill host-scale)
- **303** Combat party HUD wire-to-live-data (HUDManager)
- **304** Brom's rumor board (quest-board UI; can fold into 290)
- **305** Relic-recovery quests (Dawnedge / garrison blades / pattern-blade)

---

## Lanes (topped up)

**Lane 0 — Verify/build now:** 283, 284, 285, 286, 107, 108, 109, 110, 111, 329(regression suite), 302, 303  ~~328 CLOSED (ambiguous/no repro)~~
**Lane 1 — World/Env (VillageSceneBuilder = SOLE WRITER, serial):** 253 → 166 → 167 → 168 → 157 → 137, then 173, 245, 246, 247, 263, 311, 312, 313, 321, 323
**Lane 2 — Combat/AI (parallel):** 254, 255, 135, 145, 146, 147, 155, 128, 287(SPEC), 310, 315, 316, 317, 318, 320, 326, 327, 330(DTT cyan hero), 331(DTT hotkeys), 332(DTT aim sensitivity), 333(village death→DTT/ATB HIGH)
**Lane 3 — Combat Feel (serial):** 288(in-progress) → 213 → 217 → 218 → 219 → 220, then 295 (legendary set feel), 319 (DTT parity/anim)
**Lane 4 — UI/HUD (parallel):** 307 → 308, 309; 303, 302, 110, 124, 156, 178, 237, 257, 304, 322
**Lane 5 — World/Exploration:** 164 → 153, 159, 160, 165, 142, 143, 144, 154, 305, 324
**Lane 6 — Economy/Progression:** 228 → 229, 151, 115, 117, 119, 194, 293, 297, 298, 325
**Lane 7 — Persistence/Backend:** 301 → 120, 80, 129, 121, 118
**Lane 8 — Monetization/Store:** 72, 73, 74, 75, 76, 77, 78, 79, 80, 236
**Lane 9 — VFX/Audio (parallel):** 256, 264, 272, 195, 170, 171, 66, 111, 243
**Lane 10 — Build/Deploy/Perf:** 196 → 211 → 191, 51, 53, 54, 57, 282(HELD)
**Lane 11 — Build Mode / Player Base:** 108 → 215, 282, 113, 114, 181, 104, 239, 292, 314, 334(tower placement rotate menu)
**Lane 12 — Narrative/Onboarding/Quests:** 290 → 291 → 304, 230, 222 → 227, 238, 277, 116, 235, 133, 294, 296, 299, 300

**Hard rules:** ONE agent in Lane 1. `GameState.cs`/`SaveSchema` field-adds (Lanes 5/6/7/11/301) additive,
one-at-a-time. **Do early:** 164 (zone), wallet/economy merge, 290 (QuestService) — many lanes depend on them.
Overlaps: 108 (5/11), 282 (10/11), 80 (7/8), 111 (0/9), 295 (3/6).
