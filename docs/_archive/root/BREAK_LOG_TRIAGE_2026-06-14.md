# Break-Log Triage — owner F8/F9 flagged notes (2026-06-14)

Source: `LocalLow/DeNelle/Defenders of the Realm/break-log.jsonl` — **171 flagged notes**, deduped
into clusters below. **Recurrence = priority signal** (owner flagged it repeatedly). Mapped to
existing WOs where they match; NEW where they don't. Scene is MainCastle_Hall unless noted.

## 🔴 Systemic root causes (fix these first — each explains MANY notes)

### S1. Yarn dialogue-end breaks UI/input/dev-tools/settings  (~12 notes)
*"after finishing yarnspinner nothing in settings will work" · "dev tools not working after yarnspinner" ·
"devtools still broke after yrn" · "dev tool an issue only after yarnspinner" · "ls working after yarnspiner" ·
"after clicking and reloading expect yarn npc not there"*
→ **Strong single root.** A Yarn conversation ending leaves the UI/input layer wedged (settings + dev
tools + hover all die *after* dialogue). Prime suspect: the RPGDialoguePresenter/input-capture release
(we already release a Yarn input-blocker in 2861c9b — incomplete). **NEW — P0.** Likely the parent of S2.

### S2. Dev Tools unclickable / closes on click  (~10 notes)
*"dev tools goes nowhere" · "not clickable" · "clicking devtools closes window" · "still cancels on click of
dev tools" · "fresh start: got to dev toolbar from start menu" (works pre-Yarn)*
→ Maps to **WO-417**, but the break-log proves the trigger: **clickable until a Yarn dialogue runs**, then
dead. Fold into S1. The "reach it from the start menu (pre-Yarn)" note is the confirming clue.

### S3. Enemy outpost runs PLAYER behaviors  (~5 notes)
*"GROUND IS fighting — these need to be enemy turrets, instead are helping me" · "Enemy outpost has NPC and
homes" · "can upgrade in enemy stronghold lol" · "B hotkey works in enemy outpost" · "friendly enemies?"*
→ **NEW — P1.** Generated enemy bases inherit player-ownership behavior (your turrets, build hotkeys,
upgrades). Ties directly to **scene-configs `ownership: Enemy`** not being enforced at runtime (the data
exists; the gating doesn't). One ownership-gate fix kills the cluster.

### S4. Companion won't follow — stays at the Tree  (~6 notes)
*"companion does not follow" · "companion still at tree" · "companion stays at tree, the NPC/companion should be
the same yarn trigger so they travel as a party"*
→ **NEW — P1.** Companion never leaves spawn. Owner's design note: the NPC and the companion should be one
entity (the Yarn-trigger NPC *becomes* the party companion).

## 🔴 Gameplay-critical (single-cause, high impact)

| Cluster | Notes | WO | Sev |
|---|---|---|---|
| **Tree of Life missing in MainCastle_Hall** — "no win/lose target for enemies"; also "tree shows through floor of L2" | ~4 | NEW | P0 — no lose condition |
| **Black screen on `F`** in Garrison_troll_outpost / frost_keep / ruined_keep (raid scenes) | ~4 | NEW | P0 — raids unplayable |
| **ATB battle broken** — "no party, no background" · "no spells/weaponskills" · "broken rig skinning" · "endless loop" · "defeat ATB pops in Village2" | ~6 | WO-421 + NEW | P0 |
| **Seam: can't exit castle / all 4 borders blocked** — "cannot get into world" · "E/S/W/N borders from castlehub blocked, seam persisted" | ~5 | WO-418/383 | P1 |
| **Shop/store empty + buying ignores economy** — "no stock" · "nothing in shop" · "items for sale none" · "NRE after load store" · "buying only requires you have them, never ties into economy/removes them" | ~7 | WO-412/444 + forge-RCA (in flight) | P1 |
| **Enemy variety — all same / all skeletons** — "no families, no trolls/orcs/ogre" | ~5 | NEW | P1 |
| **Building upgrade gives nothing** — lumbermill/forge "click upgrade → nothing"; wants harvest-speed/size/cooldown options (Warcraft-style) | ~3 | WO-392/394 | P1 |
| **Wave timer broken** — "no timer, should start at 5 count down" · "timer doesn't show value" · "timer stopped, no start-wave button" | ~3 | NEW | P1 |
| **Respawn at enemy outpost, not mine** | 1 | NEW | P1 |
| **Can target/attack enemy from inside wall** | 1 | WO-419/423-adjacent | P2 |

## 🟡 UI / styling (cluster → the 405→403/404 HUD chain + tech-skin)

| Cluster | Notes | WO |
|---|---|---|
| **Inventory missing / unstyled / "layout awful"** ("no inventory" ×8, "bag opens needs styled") | ~8 | WO-400/403 |
| **Combat HUD broken/unstyled outside ATB; no hero/companion health bars** | ~4 | WO-403/404 |
| **Compass broken** — no enemies, no N/S/E/W heading | ~4 | NEW |
| **Blue circle / bubble / button** ("reported 30+ times") | ~5 | WO-402 |
| **Top resource bar** — not responsive to text growth; should show food symbol; show on load | ~3 | WO-411/424 |
| **"Every button is a Play button / no content"** (store + panels) | ~3 | WO-2f686f0-adjacent |
| **Talk** — "doesn't work / not activated" + black shade under Talk + no spinning comet | ~3 | WO-414/416 |
| **Quests** — "move quests to a quest panel" · "opens quests you can't see" | ~2 | WO-290-adjacent |
| **Title** — stars→images (vibe), blue circle | ~2 | NEW (polish) |
| General theming — "style to tech pack / thin panels minimized for webui" | ~5 | WO-437/438 |

## 🟡 Art / models / VFX
- **Spell VFX + animations pixelated** ("should be a simple arrow animation, not this") — ~4 — NEW P2
- **Tripo fixes + rotate-90** (unknown models) — ~3 — existing Tripo pipeline
- **Scale**: "they are tiny" / "wok too big" — ~2
- **Wights**: "correct now — add a glowing VFX" ✓ + glow request — P3
- **NPCs floating**, "steps should be removed", "rotate tree + color", "missing companion image", "two Sylas", "party hero → portrait", **walls "100% broken" / "remove these, add as wall wood"** (← the perimeter work in flight) — misc P2/P3

## NEXT — recommended order
1. **S1 (Yarn-end wedges UI)** — almost certainly unblocks S2 (dev tools) + settings + hover in one fix. Highest leverage.
2. **Tree of Life in castle + Black-screen-on-F (raids)** — both are "can't actually play" P0s.
3. **S3 ownership gate** (enemy outpost runs player behaviors) — one gate, kills 5 notes.
4. **S4 companion-follow** + **forge shop** (RCA in flight) + **shop economy deduct**.
5. UI chain (405→403/404 + tech-skin 437/438) absorbs the whole styling column.

*171 raw → ~30 actionable clusters. The 4 systemic roots (S1–S4) cover ~33 of the notes alone.*
