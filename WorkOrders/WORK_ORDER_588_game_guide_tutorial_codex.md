# WORK_ORDER_588 — Game Guide / Tutorial Codex (data-driven, opened from Settings)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** No per-WO path:line was recorded for this one:
> verified present in HEAD by the 2026-08-14 phantom sweep; see that sweep for the implementation site.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT (owner request 2026-06-29) · UI/Onboarding lane · data-driven content_

**Origin:** owner — *"a full tutorial section with tabs on left for quests, basic mechanics, inventory, crafting,
armory, leveling, objectives, exploration, one for each building type (what & why), the skill tree & hot-swap,
arena battles, defense building, and echoes. Create it as a tutorial guide and link to Settings so they can use it
if wanted."*
**Canon:** memory `ui-blink-template-master-frame-formula` (Obsidian master-frame + drop-zones),
`obsidian-panel-chrome-black-gold-shared-close`, `owner-thinks-in-data-structures` (content = data, not hardcode),
`drop-yarnspinner-custom-dialogue` (we build our own data-driven UI text). §5 (presentation = Core/HUD only) + §12.

---

## Goal
An **optional, always-available Game Guide** — a tabbed reference panel (tabs down the LEFT, content on the right)
explaining every player-facing system: *what it is + why it matters + how to use it*. Opened from a **Settings**
entry ("Game Guide" / "How to Play"). Never blocks play; it's a help codex, not a forced tutorial.

## Architecture (REUSE — do not reinvent)
- **Data-driven content:** all copy lives in a content file `guide-content.json` (Resources/Data/Canonical or the
  same loader the other canonical catalogs use). The panel renders whatever sections the JSON declares — adding/
  editing a section is a **data change, no code**. (Creative authors the JSON; CLI builds the renderer.)
- **MVVM:** `GuideVM` projects the loaded sections (tab list + selected section body); the View is dumb (renders
  tabs + body, raises "select tab"). Logic/data in the VM/loader, never in the View.
- **Obsidian chrome:** build via the shared master-frame (`ElarionUiKit.BuildObsidianPanel`) — black panel + gold
  trim + the ONE shared Close. Left tab rail mirrors the existing Inventory tab pattern
  (`InventoryUIBuilder.BuildTabs`) but vertical. **No UXML** (code-built uGUI only — §8 landmine).
- **Settings link:** add a "Game Guide" button/row to the existing Settings panel that opens the guide via
  `PanelRouter` / `PanelManager` (one-modal-at-a-time, so it swaps cleanly with other panels).
- Both `ff.blinkchrome` on/off states must render correctly.

## Content sections (tabs) — author each as `{ id, tab, title, body[], tips[] }`
Creative writes ACCURATE copy grounded in the real game (read canon + code; FLAG any section whose system isn't
built so we don't document vapor). Required tabs:
1. **Basic Mechanics** — movement, camera, interact, the core loop (hub → defend/explore → grow).
2. **Objectives** — how the current objective/goal is shown and advanced.
3. **Quests** — accept, track, complete; rewards; daily vs story (per the dialogue/quest system).
4. **Exploration** — overworld, region gates/crossings, encounters.
5. **Inventory** — tabs (Weapons/Armor/Accessories/Consumables), equip, the gear preview.
6. **Crafting** — what can be crafted, materials, where.
7. **Armory** — gear tiers, stats, off-hand/shield rules, upgrading.
8. **Leveling System** — XP, levels, what leveling grants (stats / skill points).
9. **Skill Tree** — the talent tree (tiers/capstones), how Wisdom/skill points are earned & spent.
10. **Hot-Swap** — the hot-swap skill bar vs the fixed class kit; assigning talent skills to slots.
11. **Arena Battles** — entering an encounter, the real-time kite arena, win/return, star rating.
12. **Defense Building** — the defend-the-village loop, waves, towers/walls/gates (note V2-gated parts honestly).
13. **Echoes** — the echo workforce (life-force growth, slots up to 5, drag-drop assign Wood/Iron/Grain, auto-gather)
    + how population growth unlocks more (ties to **WO-587**).
14. **Buildings** — one entry PER building type (what it does + why you'd build/upgrade it). Enumerate from the real
    building set (e.g. Heart of Elarion, Market, Forge, Armorer, Arcane, Farm, Pet House, Barracks, Towers, Walls,
    Gates — confirm the actual list from the building catalog/prefabs; do not invent buildings).
15. **(open) any other built system** creative finds while grounding — add a tab rather than omit.

## Deliverables (in order)
1. `guide-content.json` — creative-authored, accurate, all sections above (CLI gate just validates schema/parse).
2. `GuideVM.cs` + the guide panel View (code-built Obsidian, left vertical tabs, scrollable body) — CLI.
3. Settings entry that opens the guide (PanelRouter/PanelManager) — CLI.
4. DataRegression: validate `guide-content.json` parses + every declared tab has non-empty title/body.
5. Update `CANON_GROUND_TRUTH_<date>.md` + docs index.

## Acceptance criteria
- Settings → "Game Guide" opens the tabbed Obsidian panel; Close returns; obeys one-modal-at-a-time.
- Left tab rail lists every section from the JSON; selecting a tab shows its body + tips on the right; scrolls if long.
- Editing `guide-content.json` (add/remove/edit a section) changes the guide with **no code change**.
- Content is ACCURATE to the shipped game; any not-yet-built system is labeled as "coming" rather than described as live.
- Renders correctly with `ff.blinkchrome` ON and OFF. DataRegression passes.

## What NOT to touch / out of scope
- Do **not** build a forced step-by-step tutorial overlay here (that's the existing `TutorialDirector`'s territory) —
  this is a passive, opt-in reference codex. (May later cross-link, but not in this WO.)
- Do **not** hardcode section copy in C# — it lives in the JSON.
- Do **not** use UXML/UI Toolkit for the panel (code-built uGUI only).
- Do **not** fabricate systems/buildings — enumerate from the real catalog; flag unbuilt as "coming".

## Roles
- **Creative:** authors `guide-content.json` (this WO's content deliverable), grounded in canon/code, flags gaps.
- **CLI:** builds GuideVM + View + Settings link + DataRegression; gates + commits.
