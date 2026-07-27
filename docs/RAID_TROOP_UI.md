# Raid & Troop UI — Spec (owner design 2026-06-14)

Consolidates raid-UI.txt + raid-UI-layout.txt + the "top-right intel area" call + the RAIDS banner.
**Mobile-first portrait. PROJECT LAW — Code-built uGUI ONLY, via `ElarionUiKit`.** UXML / UI Toolkit
does **NOT** ship in player builds — every Barracks / Raid-select / Raid-deploy screen here MUST be
code-built uGUI through `ElarionUiKit` (the exact same fix pattern as the pet-roster repair). Do NOT
author any `.uxml`/`.uss` for these screens; ignore any older "UI Toolkit" guidance in the WO specs.
**Visual style = dark wood panel + gold trim**, matching the Population/HUD icon set (metallic-gold card
highlights, gold serif titles, ember/glow on primary CTAs). Banner asset: `Assets/Art/UI/Raids/Raids_banner.jpg`.

## Three screens (deliberate separation — avoid HUD sprawl)

### 1. Barracks screen — the army-management HUB (from town/castle map)
The strategic base activity. Tabs: **Train** (queue new troops — type → quantity → resource cost → timer;
training spends resources and **enqueues a `TrainTroop` job on the common Obsidian queue — the Train
channel of WO-773**, offline-fair + slot-gated; the troop lands in the roster on completion, not
instantly — do NOT reinvent a private timer) · **Army** (view all troops, sort by type/role, level them)
· **Veterans** (light
veterancy overview). This is "home" for the army; NOT where you deploy mid-raid.

### 2. Raid Selection screen (Town → Raids tab)
Dark-wood + gold grid of **raid cards**, one framed plaque each: fortress thumbnail (difficulty color tint —
**green/yellow/red** = Regular/Hard/Extreme), raid name (gold serif), difficulty badge, **3★ target time**,
reward icons (resources + **Echo Shard**). Cards = the 3 flagship configs (`raider_camp_small` /
`fortified_garrison` / `mage_enclave`), data-driven from `scene-configs.json` (the enriched contract).

### 3. Raid Deploy screen — the TACTICAL decision (on selecting a raid target)
Fast, focused (you're not digging through full army management here). Portrait layout:
- **Header:** ornate wood frame + gold — "RAID: <name>", difficulty banner, 3★ target time.
- **Left — Your Forces:** Hero + Companions row (portraits in small ornate frames, fixed party) + a
  **scrollable troop-card grid** (Archer/Footman…): gold icon, name, owned count, level; tap → **quantity
  slider** with the **army-cap indicator** (cap 10).
- **Center — Battle Preview:** stylized enemy-base preview (the `RaidBaseGenerator` thumbnail) + a **live
  "Estimated Clear Time" gauge** that updates as you add troops (vs the 3★/2★ thresholds from the config).
- **Right/top — Intel + Summary (the "top-right intel area"):** the **Scout report** — the target's defense
  profile (wall tier, AA/tower density, choke vs open, boss) that makes comp matter + drives the soft RPS;
  total troops / **power rating**; **"Auto Recommend"** button (fills a comp from the scout report); big
  **glowing DEPLOY** button (green/gold + ember when ready).

## Bonus polish (MVP+)
**Favorite Squad presets** ("Balanced" / "Archer Heavy" / "Mage Rush") · drag-drop troops into deploy slots ·
**Auto-fill recommended** from the scout report (ties the intel area to the comp decision).

## Scope / sequencing
UI is **post-foundation** — the combat foundation + ArmyStorage shipped (Step 1); the felt **deploy/rally/
retreat verbs** (Step 2) come first with owner playtest, then this UI (Step 3) wraps them. The Raid Selection
+ Deploy screens are the visible surface of everything already built (configs, generator, army, scoring).
Build the screens code-built uGUI from the `ElarionUiKit`/HUD style; reuse `RpgUiCatalog` sprites where they
fit. The RAIDS banner heads the Raids tab.
