# QA Felt-Test Checklist — build of 2026-06-28

PO (owner) felt-verifies each, then closes. ✅ = confirmed good · ❌ = needs rework (note what's wrong) · ⏳ = needs art before judgeable.

## World / ground
- [ ] **Ground at y=0** — hub floor is flush under the player; no "counter-sunk 3m below" look; props/hero sit on it (was a stale-build artifact; fix already in HEAD).

## Combat & rewards
- [ ] **Victory summary screen** — after winning a battle, an exit screen shows with **time-based stars** (faster = more stars).
- [ ] **Rewards scale** with the stars on that screen.
- [ ] **~5% boss spawn** — occasionally a battle adds a boss (extra-challenge mob).
- [ ] **Boss-only gem/gear drops** — gems + gear drop only from bosses, low rate (trash mobs don't).
- [ ] **Battles feel a bit more challenging** than before.

## Crafting
- [ ] **Jeweler** — bench is reachable in the hub; gems + rings craft into jewelry; uses the existing crystal gems from boss drops.

## Hero select
- [ ] **Carousel** — hero on the LEFT, lore BELOW, stats on the RIGHT; prev/next cycles heroes.
- [ ] **Locked heroes** show as preview-only (Knight selectable, others "coming soon").

## Quests
- [ ] **Daily quests populate** and are completable (combat / explore / wildcard slots).
- [ ] **Completing a daily actually pays out** the reward (crystals/food/glimmer/wisdom/item).

## Economy
- [ ] **Building passive income** — upgrading a resource building increases passive income (it now ticks).

## UI theme (everything inherits the common presentation layer)
- [ ] **Black panel + gold trim** across ALL panels (inventory, shops, skill tree, crafting, equipment, popups, toasts) — no brown.
- [ ] **One consistent Close button** everywhere — no per-panel "X".
- [ ] **Stores' selected tab** = gold-on-black (not violet).
- [ ] **AdminOverlay** keeps its red rim (intentional dev signal).
- [ ] **Inventory** — grid aligns; no broken left card; dead Sort/Filter buttons gone.

## Dialogue / intro (Yarn fully removed)
- [ ] **No dialogue breakages** at the echo Hollow or anywhere (Yarn is gone; all dialogue is the C# Obsidian system).
- [ ] **Intro** — ~30s, skippable (tap / Skip button / any key). ⏳ runs as caption-on-black until the 5 intro images are generated (prompts in `docs/ART/INTRO_IMAGE_SLATES.md`).
- [ ] Building/vendor menus still open their panels (transactions are panels, not dialogue).

## Battle HUD (with the HUD agent — in this build)
- [ ] **Old battle HUD is gone**; the new 9-zone HUD is the only one, in all battle contexts.
- [ ] **9-zone FOCUS buttons** (Heal/Attack/Mode) actually do something (no dead taps).
- [ ] **Clan / Leaderboard / Jukebox** reachable via a touch button (were built but had no opener).

## Offline / harvest
- [ ] **Harvest button** sits non-intrusively near Settings (top-right), opens its panel.

---
## Art deliverables (your generation, separate from build)
- [ ] **Cover art** — direction + paste-ready prompt in `docs/ART/GAME_COVER_ART_DIRECTION.md`.
- [ ] **Intro images** — 5 slate prompts in `docs/ART/INTRO_IMAGE_SLATES.md` → drop into `Assets/Resources/Intro/`.

## Follow-ups queued (NOT in this build — from the gap/VFX/perf audits)
- WO-560 VFX pass (arena enemy telegraph + victory burst + wire the unused VFX library)
- Perf: shared-material cache in `TripoMaterialFixer` (biggest FPS win)
- Talent effect interpreter (~70% of Knight nodes are sold-but-inert)
- Equip → visual (armor changes stats but not look yet)
