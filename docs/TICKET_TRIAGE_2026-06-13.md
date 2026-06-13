# Ticket Triage — 2026-06-13 (single source of truth for the backlog)

**Source:** `AppData/LocalLow/DeNelle/Defenders of the Realm/break-log.jsonl` (210 raw lines, ~23 playtest sessions, 2026-06-12).
**Cross-refs:** `docs/TICKET_LOG_2026-06-12.md` (closed-loop record), `git log feat/tower-core-loop`.
**Build boundary:** latest build completed ~`2026-06-13T00:15Z`. Sessions at `00:17–00:24Z` (log lines 204–210) tested the latest code — flags there are the freshest signal. Flags before `00:15Z` may already be fixed in-code (→ VERIFY).

## APPLIED — fix committed this pass, awaiting owner retest (orchestrator diff-reviewed + approved)
Owner model: agents provide suggested guidance; the orchestrator (CLI) has final say, applies to main, closes.

| Ticket(s) | Silo | Commit | Note |
|---|---|---|---|
| T-001, T-002, T-005, T-006, T-007 | CASTLE-SCENE | `7aad91b` (+rebake) | Seam reseated interior (gate.z+3, r14); Heart placed (0,0,12) → win-target + un-freezes enemies; 4-side nav; wall gap; verifier tol tightened. `HeartTarget` tag added. **Requires the rebake run (in progress).** |
| T-031, T-019 | DIALOGUE-YARN | `b73acdf` | One-frame command defer kills the `SignalContentComplete` crash + option corruption (root, not patch). |
| T-003, T-004, T-013, T-014, T-016, T-022(timer), T-035 | HUD-HUB-GATING | `fce9e2f` | Combat cluster gated out of idle hub; DEV chip rethemed (the "blue button"); party portraits; timer value; resource auto-size; Talk comet. |
| T-011, T-005(verify), T-022(accessors) | WAVE-COMBAT | `5471f5f` | Varied enemies via EnemyFactory model path (pooled); countdown accessors. |
| T-025, T-026, T-027 | UPGRADE | `f77334a` | Real harvest speed/size upgrade axes + a tick that actually pays out; modal close; theming. |
| T-022(inventory) | INVENTORY-EQUIP | `a826680` | **Root cause: modal at sort 2600 buried under 30000 world-HUD canvases** → raised to 31000; clean paperdoll. |
| T-024, T-032 | STORE+COMPASS | `b77fd0e` | Thin-panel store; compass now shows enemy bearing ticks (was edge-arrows only). |
| T-029 | ONBOARDING | `b00f951` | Pet-select once-only guard + already-have-a-Warden return state. |

**Still OPEN after this pass (next waves):** T-008 (interior texture), T-009 (steps), T-010 (cone — needs repro), T-012 (enemy anim/art — deferred), T-015 (combat-HUD style — folded into gating, re-verify), T-017 (portrait/ghost modal — needs repro), T-018 (Talk — should be fixed via T-031, re-verify), T-020 (NPC-after-reload — verified correct by design), T-028 (Title bg art — needs assets), T-030 (DevTools button wire), T-033 (NPC float), T-034 (interactable signs). VERIFY set (already-fixed): T-021, T-023, T-036, T-037, T-038.

## WAVE 2 + ACTIVE-TESTING FIXES (applied this pass, in the 2026-06-13 rebuild)
| Ticket(s) | Commit | Note |
|---|---|---|
| **P0 NRE storm** ("NRE on load", "froze", "lots of errors") | `(static-init fix)` | ResourceBuildingProgression type-init read a null HarvestIntervalByLevel (declared after _byId) → poisoned the type → exception cascade. Moved the ladder before _byId. **Regression from f77334a, now fixed.** |
| T-002 "no tree of life" (still, post-build) | `(castle rebake2)` | Heart anchor was invisible (no mesh) — added a visible bounds-scaled, ground-seated TreeOfLife child. |
| T-007/T-008/T-009 castle dress | `6831f4e` (+rebake2) | Wall seams closed, interior stone textured, floating stair removed. |
| T-030 "dev tools goes nowhere" | `e46b3a0` | AdminOverlay self-disabled (no PanelSettings in hub) → lazy-build with HelpMenu's borrowed PanelSettings. |
| T-033 NPCs floating | `73b4069` | NpcGroundSeat raycasts to the real floor (navmesh-Y snap left them hovering). |
| T-034 interactable signs | `73b4069` | World-space billboarded type-sign (shop/upgrade/talk/pet/spell) over each interactable. |
| T-010/T-016 "black shape under Talk" | `(talk-disc fix)` | Talk is the only disabled icon button → ColorTint painted its seat (HudTheme.Disc sprite) with the dark disabledColor. Set Talk transition=None. |

**Notion sync (2026-06-13):** WO-410 (P0 0.1fps GC storm) triaged → linked to the pooling pass; WO-166/WO-178 backfilled as Done; WO-331 (WebGL SignalContentComplete crash) linked to the T-031 dialogue-defer fix. Owner closes WOs as playtest confirms.

## Counts
- **Raw entries:** 210 — session_start 23, scene_loaded 114, possible_softlock 8 (noise), flagged 57, exception 4, error 4. **Signal = 65** (flagged + exception + error).
- **Unique canonical tickets:** 38 (T-001 … T-038).
- **By priority:** P0 = 6 · P1 = 9 · P2 = 14 · P3 = 9.
- **By status:** OPEN = 18 · DIAGNOSED = 6 · VERIFY = 8 · NEEDS-REPRO = 6.
- **Softlock correlation:** all 8 `possible_softlock` are in MainCastle_Hall and align with the "cannot exit castle" / "cannot close screen" / dialogue-restart stuck-states (T-001, T-019, T-031) — they corroborate, not new tickets.

> **Post-build truth (sessions 00:17–00:24Z):** the owner re-confirmed STILL-OPEN: cannot exit to world (T-001), no Tree of Life (T-002), blue button (T-003), green 10/10 bar (T-004), wave timer shows no value (T-022), dialogue-restart-breaks-options (T-031). These are the live P0/P1 set. The CanvasGroup HUD crash did NOT recur post-build → its fix landed.

---

## SILO: CASTLE-SCENE  — owns `Assets/_Modules/.../CastleHubBuilder.cs`, `CastleGateNavVerify`, castle navmesh bake
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-001 | P0 | DIAGNOSED | MainCastle_Hall | "cannot exit castle / cannot get into world — seam issue" | 6 (lines 42,82,94"3rd time",95,132,209) | Exit seam trigger not seated at the recipe gate; gate arch voxelizes solid (WO-168 class). Confirmed STILL open post-build (line 209, 00:23Z). | `CastleHubBuilder.EnsureExitSeamAtRecipeGate` — seat interior trigger (gate.z+3, radius 14) + tighten `CastleGateNavVerify` tolerance; rebuild + rebake. |
| T-002 | P0 | DIAGNOSED | MainCastle_Hall | "no tree of life here — what does enemy target as win scenario" | 2 (lines 185,210) | `CastleHubBuilder` never places a Heart; `Enemy.DriveNav` early-returns on null `_heart` → enemies freeze (root-shared w/ T-005). STILL open post-build (line 210). | Place one scale-1 `HeartController` at courtyard centre (~0,0,12), tag `HeartTarget`, rebake. **CROSS-LINK → T-005.** |
| T-006 | P1 | OPEN | MainCastle_Hall | "need navmesh + plane, probably all 4 (sides)" | 1 (line 28) | Owner suspects missing walkable floor on 4 quadrants — same bake pass as T-001. | Fold into the T-001 rebake: verify ±65 walkable floor on all 4 gate strips. |
| T-007 | P2 | OPEN | MainCastle_Hall | "castle walls 100% broken / building wall not connected / seam still here" | 3 (lines 14,81,82) | Castle shell wall segments not connected; visual seam. (line 82 seam may overlap exit-seam T-001.) | Audit `CastleHubBuilder` wall placement; close mesh gaps. |
| T-008 | P2 | OPEN | MainCastle_Hall | "add texture to inside of castle structure" | 1 (line 10) | Interior shell untextured. | Assign interior material in builder. |
| T-009 | P3 | OPEN | MainCastle_Hall | "steps should be removed" | 1 (line 25) | Stray staircase geometry. | Remove steps prop from recipe. |
| T-010 | P3 | NEEDS-REPRO | MainCastle_Hall | "not sure what this cone is" | 1 (line 35) | Unidentified cone primitive in scene (likely a debug/spawn gizmo). | Locate stray primitive; hide/remove. |

## SILO: WAVE-COMBAT  — owns `WaveManager`, `Enemy`, `EnemyBrain`, `EnemyFactory`
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-005 | P0 | DIAGNOSED | MainCastle_Hall | "no enemy engagement" | 1 (line 184) | Same root as T-002: enemies freeze with no Heart target. | Resolved by T-002 Heart placement; verify `Enemy.DriveNav` advances once `_heart` set. **CROSS-LINK → T-002.** |
| T-011 | P1 | OPEN | MainCastle_Hall | "friendly enemies? / all enemies still this (one type)" | 2 (lines 85,86) | `WaveManager._enemyPrefab` single-prefab override defeats `EnemyFactory.Build(def)` + family system → one enemy type; "friendly" = no aggro (T-005 freeze). | Remove `_enemyPrefab` override; drive varied defs through `EnemyFactory`. (Matches TICKET_LOG "Open: Enemy variety".) |
| T-012 | P1 | OPEN | MainCastle_Hall | "enemy animations way too pixelated / need fixed" | 2 (lines 149,84"animation should be sleep") | Enemy/NPC idle anim wrong + low-res; line 84 = idle should be sleep pose. | Deferred art/texture pass (TICKET_LOG "Pixelated animations"); set correct idle/sleep clip. |

## SILO: HUD-HUB-GATING  — owns `VillageHudController` (hub vs combat), `DevPanelController`
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-003 | P2 | DIAGNOSED | MainCastle_Hall / Title | "blue circle / blue button / blue bubble (reported 30+ times)" | 7 (lines 5,23,42,83?,186"30 times",207?,210) | `DevPanelController` DEV corner chip (`DevPanelController.cs:569` navy), sort 9000, rides every scene incl. Title. STILL open post-build (line 210). | Retheme / gate the DEV chip off in hub + Title; ship-build hide. |
| T-004 | P2 | DIAGNOSED | MainCastle_Hall | "what is the green 10/10 bar" | 1 (line 207) | Combat party-HP green fill + mana "10/10" leaking into non-combat hub. STILL open post-build. | Gate combat vitals/party cluster off in MainCastle_Hall (or label). **CROSS-LINK → T-008-hud below.** |
| T-013 | P2 | OPEN | MainCastle_Hall | "text overlaps / color font and overlap needs corrected / different" | 3 (lines 20,80,29?) | HUD label overlap + wrong color font in hub. | Layout pass on hub HUD text; fix font color tokens. |
| T-014 | P2 | OPEN | MainCastle_Hall | "top resource bar should fill screen / be responsive to text growth" | 3 (lines 69,116,173) | Resource bar fixed width, not responsive; on load resources don't show with the bar. | ContentSizeFitter / responsive layout on top resource bar; ensure populated on load. |
| T-015 | P1 | OPEN | MainCastle_Hall | "combat heads-up display is broken and not styled" | 1 (line 148) | Combat HUD unstyled in hub context (partially addressed by `8af4180` ElarionUiKit pass — re-verify). | Confirm combat HUD routes through ElarionUiKit; restyle remaining. |
| T-016 | P2 | OPEN | MainCastle_Hall | "spinning comet over Talk missing + black shade under Talk" | 2 (lines 11, also pre-fix context) | Talk affordance comet/indicator + stray black shade. Earlier root (HUD partial) fixed; this is the residual indicator + shade. | Restore comet indicator; remove black shade quad under Talk button. |
| T-017 | P2 | NEEDS-REPRO | MainCastle_Hall | "I should see image we added as portrait and no menu — could be over previous menu not closing" | 1 (line 29) | Portrait image not showing; suspected stale modal overlay. | Repro: confirm portrait asset wired; check modal arbiter not leaving ghost panel. |

## SILO: DIALOGUE-YARN  — owns `.yarn` files, `DialogueService`, Yarn command bridge
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-018 | P1 | OPEN | MainCastle_Hall | "talk doesn't work / talk not activated / not clickable / no t clickable" | 4 (lines 13,73,131,206-related) | Multiple causes folded over the day; early HUD-partial root fixed (`702d808`). Residual: Talk not firing in some loads. Confirm against latest. | Re-verify Talk wiring post-HUD-fix; if still dead, trace `SetTalkAvailable` enable path. |
| T-019 | P1 | OPEN | MainCastle_Hall | "button takes you a different way that restarts yarn then breaks other options" | 1 (line 206) | Re-entering dialogue restarts the Yarn node but corrupts subsequent option state. POST-BUILD (00:20Z) — fresh. Correlates w/ `SignalContentComplete` exception (line 6) + softlock 00:20Z. | Guard re-entrancy in `DialogueService`; ensure node restart resets option stack cleanly. |
| T-020 | P2 | OPEN | MainCastle_Hall | "after clicking/reloading expect yarn NPC but not there" | 2 (lines 169,29-related) | NPC introducer not present after reload. | Verify introducer NPC persists/re-spawns across reload. |
| T-021 | P1 | VERIFY | MainCastle_Hall | "No Command \"command:\" error / error in log add-command handler" | 2 (lines 141,142) | Dead `<<command: Verb>>` prefix in `.yarn`. **Fixed** `7e6d80f`,`f3245f0` (strip prefix, 13 files). Errors predate latest build. | Owner retest vendor/recruit dialogue. |
| T-031 | P0 | OPEN | MainCastle_Hall | `SignalContentComplete can only be called when a command is being dispatched` (Yarn exception) | 1 (line 6) + softlocks | Yarn VM command-dispatch ordering bug; ties to T-019 re-entrancy + dialogue softlocks. Crashes the running dialogue. | Root-cause the command/await sequencing in the Yarn command bridge; likely same fix as T-019. **CROSS-LINK → T-019.** |

## SILO: INVENTORY-EQUIP  — owns inventory panel + paperdoll
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-022 | P1 | OPEN | MainCastle_Hall | "no inventory / still no inventory + this layout is awful" | 4 (lines 41,157,180,131-related) | Inventory not opening in hub; layout poor. Partial work `81fcc79` (equip/paperdoll) + `e902fab` (BAG retry) — re-verify it actually opens in MainCastle_Hall. | Confirm BAG → inventory opens in castle; redo layout. |

## SILO: STORE  — owns `ShopPanel` / `PackStore`
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-023 | P1 | VERIFY | MainCastle_Hall | "store no stock / no stock" | 2 (lines 130,131-related) | Rows used normalized anchors → off-content past ~12 items. **Fixed** `2a2d1c8` (VerticalLayoutGroup + ContentSizeFitter). Pre-build flag. | Owner retest store stock list. |
| T-024 | P2 | OPEN | MainCastle_Hall | "style store to match UI theming — thin panels, minimized for web UI" | 2 (lines 181,182) | Store styling not yet matching thin-panel web theme. (`e83a9bf` did a presentation pass — re-verify against thin-panel ask.) | Apply thin-panel ElarionUi treatment per `158` tech-pack ask. |

## SILO: UPGRADE  — owns `BuildingUpgradePanel`
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-025 | P1 | OPEN | MainCastle_Hall | "click upgrade → nothing to upgrade; expect forge-style enhancements / harvest speed/size/cooldown options" | 2 (lines 155,179) | Upgrade panel opens empty — no upgrade options authored (Warcraft/SC-style forge tree missing). `687a502` declared the upgrade flags but no content. | Author upgrade option set (harvest speed/size/cooldown/cost) + structure enhancement visuals. |
| T-026 | P2 | OPEN | MainCastle_Hall | "upgrade/cannot close this screen" | 2 (lines 27,179-related) | Upgrade/modal can't be dismissed → stuck (softlock correlate). | Add close/back handling to the panel; join modal arbiter. |
| T-027 | P2 | OPEN | MainCastle_Hall | "style upgrade/stored panels to match UI theming + tech pack" | 1 (line 158) | Needs styling off the tech pack in assets. | Assign tech-pack theme to upgrade/storage panels. |

## SILO: ONBOARDING  (Title / Pet / Hero select)
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-028 | P2 | OPEN | Title | "Title creative — change star background to images, stars don't match story vibe" | 1 (line 4) | Title art direction. (`ec84ea8` retheme palette done; this is the bg-imagery ask.) | Swap star bg for story-vibe imagery. |
| T-029 | P1 | OPEN | MainCastle_Hall | "lets me select pet twice — should change dialog on return, not re-allow; offer how to get another" | 1 (line 22) | Pet-select has no already-selected guard → double pet. | Gate pet-select once-only; branch return dialogue. |
| T-030 | P1 | OPEN | MainCastle_Hall | "dev tools goes nowhere" | 1 (line 26) | DevTools button opens nothing in hub (`48c311a` un-gated the button — wiring target missing). | Wire DevTools button → AdminOverlay in MainCastle_Hall. |

## SILO: COMPASS-WORLD  — owns `CompassHud` / minimap
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-032 | P1 | OPEN | MainCastle_Hall | "compass does not work — should see enemies" | 1 (line 183) | Compass not plotting enemy blips. | Feed enemy transforms to `CompassHud`; verify it rendered (`4d78b2f` routed styling only). |

## SILO: ART-ANIM / WORLD-DRESS
| ID | P | Status | Scene | Canonical ticket | occ | Root-cause / notes | Proposed fix direction |
|---|---|---|---|---|---|---|---|
| T-033 | P2 | OPEN | MainCastle_Hall | "NPCs seem to be floating" | 1 (line 88) | NPCs not seated to ground (y-offset / navmesh sample). | Ground-snap NPC spawns. |
| T-034 | P2 | OPEN | MainCastle_Hall | "add a sign with a symbol like most games — can't tell what this (interactable) is" | 2 (lines 87,158-related) | Interactables lack a readable icon/sign affordance. | Add symbol-sign above interactables (recurring owner ask). |
| T-035 | P2 | OPEN | MainCastle_Hall | "party hero should be added to portrait for team members" | 1 (line 208) | Party portraits not populated. POST-BUILD (00:22Z). | Bind companion roster → party portrait slots. |

## SILO: PARTY-FRAME (HUD combat cluster — distinct from hub gating)
> T-004 (green 10/10) and T-035 (party portraits) both touch the combat party-frame; sequence T-004 gating BEFORE T-035 population so they don't collide. Same `VillageHudController` combat region — **single agent owns both.**

---

## VERIFY — fix committed, awaiting owner re-test
| ID | Ticket | Commit | Note |
|---|---|---|---|
| T-036 | HUD partial / CanvasGroup `SetTalkAvailable` crash (lines 99,100,104,105,109,110 — recurred 17:58→18:12Z) | `702d808` + sweep `330bed6` | TryGetComponent replaces `??`-on-UnityObject. Did NOT recur post-build (00:17Z+) → fix landed. |
| T-021 | Yarn `No Command "command:"` (lines 141,142) | `7e6d80f`, `f3245f0` | Dead `<<command:>>` prefix stripped, 13 files. |
| T-023 | Store "no stock" (line 130) | `2a2d1c8` | Vertical layout + ContentSizeFitter. |
| T-037 | "dev panel breaking caused text not to load (now loads)" (line 79) | `702d808`/`330bed6` | Owner self-noted RESOLVED in log; HUD-partial root. |
| T-038 | "Add Resources did nothing" (folded — line 142 add-command) | `17d69d0` | Routed via `EconomyService.GrantSpendable`. |
| T-018(partial) | Talk dead (early occurrences lines 11,13) | `702d808` | Early root fixed; residual tracked OPEN under T-018 for post-build confirm. |
| T-008(top-bar populate) | resources show with bar on load (line 116 partial) | `17d69d0` | Grant→HUD ping; re-verify with T-014. |
| T-009(wave) | wave timer/Start button dead (early) | `2a2d1c8` | WaveManager added; residual "timer shows no value" is T-022-new → see below. |

## NEEDS-REPRO — too vague to action
| ID | Entry | Line | Why |
|---|---|---|---|
| N-1 | "(no note)" | 83 | Empty flag; no context. |
| N-2 | "blue bubble" (no clear scene-object) | 23 | Likely T-003 DEV chip but "bubble" ambiguous vs a dialogue bubble — confirm which. |
| N-3 | "not sure what this cone is" (→ T-010) | 35 | Need a screenshot to locate the cone primitive. |
| N-4 | "same bug" / "3rd time" / "seriously!!!!" | 94,95 | Frustration repeats of cannot-exit (T-001) — no new info, counted into T-001. |
| N-5 | "I should see image as portrait, no menu" (→ T-017) | 29 | Ambiguous: portrait wiring vs stale modal — needs repro. |
| N-6 | "materials should fill on screen" (→ T-014) | 69 | Resource-bar layout; confirm exact element. |

> **Wave-timer post-build (T-022-new, fold into WAVE-COMBAT):** line 204 (00:19Z) "start wave goes in that small icon beside timer; timer doesn't show any value" — POST-BUILD, fresh. WaveManager exists (`2a2d1c8`) but the **timer label binds no value** in MainCastle_Hall. **P1, silo WAVE-COMBAT** — add as the live wave ticket (Start-wave icon placement OK per owner; just bind the countdown value).

---

## SILO DISPATCH PLAN (collision-free; P0 first)

Each silo = one agent, file-disjoint. Dispatch order:

1. **CASTLE-SCENE** (P0) — `CastleHubBuilder.cs`, `CastleGateNavVerify`, castle navmesh bake. Tickets T-001, T-002, T-005(via T-002), T-006, T-007. **Rebake gate-locked — single agent, then CLI bakes.** *Unblocks the whole loop (exit + Heart + enemy engagement).*
2. **WAVE-COMBAT** (P0/P1) — `WaveManager`, `Enemy`, `EnemyBrain`, `EnemyFactory`. Tickets T-005(verify after Heart), T-011, T-012, T-022-new(wave-timer bind). *Depends on T-002 Heart for verify; the timer-bind + enemy-variety are independent edits.*
3. **DIALOGUE-YARN** (P0) — `.yarn`, `DialogueService`, Yarn command bridge. Tickets T-031(+T-019 same root), T-018(confirm), T-020, T-021(verify). *T-031/T-019 are the live dialogue crash/restart — one agent.*
4. **HUD-HUB-GATING / PARTY-FRAME** (P2, high visibility) — `VillageHudController` (hub vs combat region), `DevPanelController`. Tickets T-003(blue chip), T-004(green bar)+T-035(party portraits, same combat region — one agent), T-013, T-014, T-015, T-016, T-017. *Single agent owns the whole VillageHudController to avoid the serialization collision.*
5. **UPGRADE** (P1) — `BuildingUpgradePanel`. Tickets T-025(author options), T-026(close), T-027(style).
6. **INVENTORY-EQUIP** (P1) — inventory panel + paperdoll. Ticket T-022.
7. **STORE** (P1/P2) — `ShopPanel`/`PackStore`. Tickets T-023(verify), T-024(thin-panel style).
8. **ONBOARDING** (P1/P2) — Title/Pet/Hero select. Tickets T-028, T-029(pet double-select guard), T-030(DevTools wire).
9. **COMPASS-WORLD** (P1) — `CompassHud`. Ticket T-032.
10. **ART-ANIM / WORLD-DRESS** (P2/P3) — scene props/NPCs. Tickets T-008, T-009, T-010, T-033, T-034. *Coordinate with CASTLE-SCENE if it touches CastleHubBuilder recipe — defer prop removals to after #1's rebake.*

**Hard collision notes:**
- `VillageHudController.cs` is a serialization bottleneck — silo #4 is **one agent only**.
- `CastleHubBuilder.cs` is the castle serialization bottleneck — silo #1 + any ART-ANIM prop edits to the recipe (#10) must not run concurrently; do #1 first, then #10.
- Navmesh rebake is a Unity-gate step — CLI owns it after silo #1's edits land.
