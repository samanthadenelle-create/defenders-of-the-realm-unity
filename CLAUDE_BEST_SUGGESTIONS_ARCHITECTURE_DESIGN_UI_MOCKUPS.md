# CLAUDE (Agent) — Best Suggestions on Issues, Architecture & Design

**Status:** Living document. Update on every major review or WO.  
**Purpose:** Compiled guidance distilled from MASTER_CATALOG risk ledger, ARCHITECTURE_PRINCIPLES, CLI_LANES_WO_NUMBERS, PIPELINE_STATE, all area catalogs (hud/battle-atb/village-hero/editor-tools/scenes/dialogue/etc.), WORK_ORDER_430 handover triage, real code (ElarionUiKit, VillageHudController, BattleHudUgui, CastleHubBuilder, HeroLocomotion, etc.), open P0/P1 tickets (363–437), owner playtests, and process learnings.  
**Audience:** Claude UI/CLI agents (and future contributors). Read this + mandatory catalogs before every session.  
**Generated:** 2026-06-13 (post full silent review of architecture/docs + current state).  
**Core Lens (ARCHITECTURE_PRINCIPLES + CLAUDE.md):** What is right, not what is easy. Player-felt + high-leverage holistic. Bounded contexts. Presentation never touches objects. Quality over speed. Owner playtest (felt) is the only verdict — never "green gate = done".

---

## 0. How to Use This Document (Agent Rules)
- **Before any work:** Re-read CLAUDE.md (full), docs/MASTER_CATALOG.md + relevant /docs/MASTER_CATALOG/<area>.md, docs/ARCHITECTURE_PRINCIPLES.md, this file, PROJECT_INDEX.md, Assets/_Modules/README.md, recent HANDOVER_*/TICKET_TRIAGE_*.md, and the WO-430 file.
- **For any ticket/issue:** Map it to the clusters below. Use suggestions to scope the WO (files, acceptance, NOT touch). Generate or reference styled mockups for any UI work.
- **"Silently" mode (low battery or focus):** Batch all reads/greps/image gens first in one parallel step. Produce the deliverable (file, code, RESULT) with minimal prose.
- Update this doc + indices + MASTER_CATALOG sections + relevant READMEs when you surface new patterns or close issues.
- Every suggestion here is derived from verified code/docs (not comments — comments lie).

---

## 1. Agent (Claude) Process & Issue-Handling Suggestions
These address recurring friction from tickets, mount sync, brace gates, doc drift, assumption bugs, and orchestration.

### 1.1 Mandatory Rituals (Never Skip — Binding)
- Always start with the exact read order in MASTER_CATALOG.md header and CLAUDE.md §0. Even in "continuation" or "low battery" sessions — the 3% left sessions are exactly when assumptions kill quality.
- Before grepping/exploring code: Read the README system (PROJECT_INDEX + module/docs/Assets READMEs). Then targeted reads of MASTER_CATALOG areas.
- For every `.cs` touched: Run the exact brace python (CLAUDE.md §1) immediately after edit. Log "Braces balanced (N) ✓" in your RESULT. CLI will revert mismatches.
- Mount rule (CLAUDE.md §0): UI **never** writes/edits `.cs` via bash/Linux mount. Only use Write/Edit tools (Windows paths). If you see garble (brace ok on mount but not Windows), stop and signal the committer.
- Numbering: Always consult CLI_LANES_WO_NUMBERS.md + MASTER_CATALOG/docs-wo-state.md for next free (READ IT OFF THE BANNER - do not trust any number written here or in any other doc). Never mint from filesystem max. Slot into lanes. Update the lanes file when minting.
- Work orders: Every non-trivial task gets (or extends) a root `WORK_ORDER_NNN_*.md` with Status: READY TO IMPLEMENT, files, acceptance (brace + compile + regression + owner playtest), NOT touch. On done: CLI produces `.RESULT.md`.
- Trust rule: "Never mark a WO/fix DONE on a green gate alone — only the owner's playtest is the verdict."

### 1.2 Diagnosis & Execution Best Practices
- **Flow-first triage (orchestrator rule):** When handed a bug/stack/NRE, first model "what *should* happen given the state?" (is the state expected?). Use the catalogs to know the real model (e.g., HeroLocomotion is NavMeshAgent-driven; HUD is passive code-built uGUI only; Battle is pure Engine + ATBRuntimeState + BattleHudUgui).
- **Parallel agents:** For complex tickets (e.g., any P0 cluster in WO-430), spawn subagents (one per lane or one diagnosis + one fix). Use for read-only diagnosis (unlimited) and file-disjoint edits. Orchestrator batches the gate + sole commit.
- **Plan mode for ambiguity:** High-impact (HUD restyle, camera, builder changes, arch splits) → enter_plan_mode first. Explore, propose plan, exit with user approval. Avoids expensive re-work.
- **No assumptions / guess-and-grep:** Catalogs are verified from code. Stale comments (HeroLocomotion "pure transform", many headers) are a **known hazard class**. When touching a file with a header/doc mismatch, fix the comment as part of the change.
- **NRE spam & perf (WO-328, 410, risk §2/14):** Mandate registries/O(1) lookups (see TownHudBridge, Compass bootstrap patterns). Audit and replace every `FindObjectsByType` / whole-world scan in hot paths (towers, companions, bridges). Add PerfDiagnostic hooks. For NRE: Add `?.` on all CoreServices + reflection paths; wrap HUD/Build bootstrap in try/catch (never blank the player).
- **UXML trap (risk §8, many WOs):** Any new panel or Settings/Pause must have **code-built fallback** (like Onboarding). Settings + Pause are the last UXML-only surfaces — treat as P0 latent build risk.
- **Dead code & drift hygiene:** When you discover orphaned (VirtualDPadLean + non-existent HUDManager, old BattleHUD.uxml in scene, duplicate WebGL menu items, dead DTT paths), either delete (with RESULT note) or explicitly mark DEAD in headers + update READMEs. Update docs-wo-state on collisions.
- **Doc maintenance (risk §32, numbering §7/5d):** On any structural change or WO close, update:
  - Relevant MASTER_CATALOG/<area>.md
  - PROJECT_INDEX.md / Assets/_Modules/README.md if files move
  - CLI_LANES_WO_NUMBERS.md (status/lane)
  - This suggestions file
  - PIPELINE_STATE top block if BUILT/WIRED state changes
  - Indices lag whenever they RESTATE a number instead of pointing at the banner - fix them by removing the number, not by updating it.
- **Mockups & visuals:** For any UI/UX ticket (HUD, vendor, hero select, build, settings, dialogue), **generate or reference styled mockups** (see §4). Use image_gen with precise ElarionUiKit + reference mockup language. Embed paths + textual spec in the WO or this doc. Owner approval on visuals before coding.
- **Low-battery / "3% left" sessions:** Batch every possible read + image_gen + grep in the first message. Produce the file/deliverable in one write. Defer non-critical exploration.
- **Orchestration (CLAUDE.md §11):** You are the orchestrator/lead. Route focused tasks to subagents. Batch-gate the tree. Sole committer on Windows. Reconcile multi-session diffs by explicit path only (never `git add -A`).
- **Regression & gates:** Every change that could affect orientation (363), camera, locomotion, HUD visibility, seam, build feedback, or perf must strengthen RegressionSuite cases (especially source-grep ones). Always run CompileGate + relevant cases before claiming.
- **Yarn / Dialogue (331/375/330/391/377):** Harden around SignalContentComplete, OptionItem static re-entrancy guard, bare-arg literal gotcha (use DialogueService.CurrentStructureId), wait_for_event timeouts. Review Yarn docs on every dialogue-adjacent ticket. Remove debug elements (blue Next). Test WebGL load paths.
- **Builders (serial bottleneck):** CastleHubBuilder + VillageSceneBuilder partials = one agent at a time. Never hand-edit .unity (especially Village). After regen, run full visual + nav regression. For castle: Consider "adopt offsets" or recipe delta mode so owner hand-dialed MainCastle_Hall isn't reverted.
- **Cross-assembly & reflection:** HUD/BattleATB/Editor **never** hard-ref Village. Use CoreServices + reflection-by-name + FindType. Add `using DeNelle.Core.Combat;` for IDamageableStructure impls. Always `?.` on service calls.

### 1.3 Common Anti-Patterns to Catch & Fix
- Stale comment vs code (HeroLocomotion is the archetype; also many headers, READMEs, builder comments).
- Private/dead handlers kept "in case" (AdminOverlay) — either wire or delete.
- Invented APIs (BattleHudUgui early versions had wrong BattleState.ActiveUnit vs real ActiveUnitId, etc.) — always read Types.cs / Defs.cs / Engine first.
- Assuming UXML will work in builds.
- Bypassing PanelManager for modals.
- Leaving NRE paths or per-frame allocs.
- Numbering or index drift.
- Touching abandoned DTT or Village.unity code without freezing.

---

## 2. Architecture Suggestions (High Leverage)
These are player-felt + holistic improvements that prevent whole classes of bugs and speed future work.

### 2.1 Presentation Layer (Strongly Enforce §2 of ARCHITECTURE_PRINCIPLES)
- **Complete ElarionUiKit adoption (WO-405 as true gate):** The kit (Core/UI/ElarionUiKit.cs + ElarionUi.cs) already consolidates glass/rim/panel/button/niche/well/ scrim + palette (dark glass, gilt, parchment, aether, affordable green, danger). BattleHudUgui routes through it; VillageHudController is partially migrated to sleek dark-glass. 
  - **Suggestion:** Make kit the *only* way to build chrome. Add missing factories: `AbilitySlotButton(Transform, slot, onClick, cooldownRing)`, `ResourceStrip(...)`, `PartyFrame(...)` with live delegates, `CooldownRing(Image fill, Color ready)`, `PortraitSlot`, `BuildPaletteCard`, `VendorItemRow`. Force all panels (vendor 415/412, settings 417, hero select 257/328/329, build preview 314, quest tracker, etc.) through the kit.
  - Remove all private "L*" parchment or ad-hoc anchors/colors from HUD/ panels.
  - Result: One visual language, easier mobile tweaks, WebGL-safe procedural fallbacks, no more 10-deviation mockup mismatches (411).
- **Code-built only contract:** Re-state in every HUD-related file and README: "No UXML at runtime for gameplay HUDs." Add a simple editor gate or regression case that fails if UIDocument is active in gameplay scenes without fallback.
- **Settings/Pause (risk §8):** Highest latent build risk. Add full code-built versions using the kit (or mark as P0 and block other HUD work until done).

### 2.2 Performance & World (P1 blockers)
- **Registries over scans (risk §2, WO-410 castle 0.1 fps GC storm, OuterWorld 1 fps):** DefenseTower/ArcaneTower.Rescan, StoryCompanion.TryClericMend, some bridges still use FindObjectsByType. Mandate O(1) registries (see existing TownHudBridge / Compass patterns). Add a "Registry<T>" helper in Core if missing. Audit every hot path in a dedicated WO.
- **Castle perf (WO-410):** MainCastle_Hall is the home hub. Profile on load + idle + transition. Combat-object leak on additive unload. Fix before any "feels good" claim.
- **WebGL size (risk §4, WO-408):** Scripts exist but "NOT run". Make texture opt part of the Desktop/WebGL build menu or a mandatory pre-ship step. Mirror the 6 StreamingAssets-only catalogs (enemy-roles, towers, walls, realm-map, heart, audio-mix) into Resources (CanonicalJson contract).
- **Pooling (ARCHITECTURE_PRINCIPLES §2b.2 + risk):** VfxPool and ProjectilePool exist and are proven. Expand to a small generic `UnityEngine.Pool.ObjectPool<T>` (WebGL-safe) for enemies, projectiles, floating text, prompts, spawned VFX. Audit `Instantiate(` call sites (waves, NPCs, dialogue, cinematics). One owner per concern.

### 2.3 Builders & Scene Flow (serial + canon)
- **CastleHubBuilder offset problem (risk §18/5c, editor-tools):** Owner hand-dialed MainCastle_Hall is canonical. Regen reverts it. Suggestion: Add an "AdoptCurrentOffsets" or recipe-diff mode, or store per-piece TRS in a committed JSON that the builder can "apply delta" from. Keep the "build from blank" path for new work.
- **One agent rule:** Enforce in process (and perhaps a simple file lock or comment at top of builder files). Update MASTER_CATALOG/editor-tools.md on any change.
- **Scene canon:** Hard-code in code + docs that Village2 + MainCastle_Hall + OuterWorld (additive) + ATBBattle + Garrisons + 2 Dungeons are the live set. Village.unity and DTT scene are dead. Update any stale router comments (PetSelect, SceneRouter headers still say "Village" for onboarding end state).

### 2.4 Data, Persistence, Economy
- **Single source + dual copy (risk §3/17/29):** Enforce Resources + StreamingAssets byte-equal for all canonical JSON (regression already has the check — make it louder on drift). Kill the 3rd stale gear copy in Assets/Data/Canonical.
- **Persistence unification (risk §19/20/22):** Multiple stores (GameStateService, PlayerPrefs for pets/glimmer/BP, Arena stub). Suggestion: Accelerate full GameState for append-only fields (Tribes, Settlements, Wards, PetName, ArenaDefense). Reconcile cosmetic ownership.
- **Vendor / shop data (many WOs 406/412/413/415/429):** Move toward data-driven (isUpgradable vs isShoppable flags on structures). Serve stock from Neon/DB with offline fallback (WO-429). Yarn verbs already route OpenShop — just populate the catalogs.

### 2.5 Animation, Locomotion, Combat
- **Comment-vs-code hazard (HeroLocomotion archetype):** Fix the header on touch. Treat as NavMeshAgent kinematically driven. Same for Pet/Enemy.
- **Orientation / facing (WO-363 cluster):** Centralize rotate-to-target + idle-pose + camera-yaw logic. ActorAnimator + HeroBodySwapper are the seams. Strengthen the regression gate that source-greps the (now-fixed) comment.
- **ATB model variance & wave (battle flags F-SWAP-2, F-WAVE-1, F-CTRL-comment):** ResolveEnemySlug should use the live encounter def. BattleHudUgui wave text should read BattleState.Wave. Caster-vs-melee anim decision should inspect the real active unit, not fallback name.
- **Auto-attack & control mode (F-MGR-1, F-CTL-1):** Wire the existing events/UI or explicitly deprecate the punitive timer / ControlMode plumbing.

### 2.6 Other High-Leverage
- **Audio mixer (risk §6):** The .mixer is a stub (only Master). Either build the 5-group/5-param documented mixer or delete the dead SetFloat/FirstGroup paths and document the AudioSource-direct reality.
- **Arena stubs (risk §21):** Either finish (SKR mint, real catalogs, DefensePatternLibrary) or mark clearly STUB so playtests don't expect it.
- **Monolith split (docs-wo):** Continue the safe deletes (BattleHud + BattleVfx already gone). Large files (>800 lines) have plans — follow them.
- **Generic "Capability" model (One Model §2b):** Buildings already use entry + composable (Interactable/Upgradable/Destructible). Extend to more world objects (nodes, gates, camps) so HUD/interaction/combat/targeting are pure readers of flags + registries.

---

## 3. Design Suggestions
- **One visual language via the kit:** Dark fantasy (misty dusk, hopeful rebuild, runic/stone). Warm parchment + hewn stone + runic gold (ElarionUi) or sleek dark-glass + gilt (current Hud + kit evolution). Use Tech hud elements pack for combat/vendor rings/buttons per owner directive (437/415). Consistent glyphs (Crest "*", Rune strings), tap targets ≥44–56 px, mobile portrait-first (1080x1920 ref, 0.5 match).
- **Context & minimalism:** Town vs open-world chrome gating (already in VillageHudController) is good — keep the "maximised play area" philosophy. Only vital info on screen; modals for everything else (PanelManager discipline).
- **Feedback everywhere (WO-394, build click, blocked actions):** Every button that can be invalid must surface *why* (resources short, wave active, placement invalid, cooldown). Gold/confirm/danger states from the kit.
- **Hero select & onboarding:** Portraits must fit without clip/overflow (328). Stat cards/specs visible per hero (329). Blue dot / option complete must follow Yarn SignalContentComplete exactly (330).
- **Vendor / economy surfaces:** Armor/weapons first (415). Clear BUY/SELL/UPGRADE split (413). Populated from real data (406/412). Skinned from Tech pack + kit, not placeholder.
- **Combat HUD (334/421/437):** After 405 kit, full restyle: command bottom-left (Attack + dynamic Skills with rings + symbols + costs), party bottom-right with live HP/MP/ATB (rings charging → gilt ready), info top. No hard-coded "WAVE 1". Use real BattleState.
- **Settings/Dev (417):** Visible rows, correct font (LegacyRuntime.ttf explicit), no labels under row layer. Code-built.
- **Town HUD parity (411):** 0 deviations from hud_mobile_town.png reference once kit is complete (top banner resources + Heart, left party, wave + gold PLAY, bottom actions + dpad zone respected).
- **General mobile:** D-pad (left thumb) zones kept clear. Right thumb for actions/abilities. Safe area aware. Landscape + portrait both tested.
- **Dialogue:** Clean options (no overlap 391), portrait + name banner, dark-ink text on parchment, no blue debug elements.

---

## 4. Full Styled UI Mockups (Generated References + Specs)

These were produced via image_gen using the project's exact canon (ElarionUiKit dark glass/gilt/parchment + Tech hud elements where specified, mobile 9:16 portrait, thumb-friendly, no clipping, matching the reference `docs/UI_Mockups/hud_mobile_town.png` layout + current code direction in VillageHudController/BattleHudUgui/ElarionUiKit).

**Generated mockup image paths (session images — copy to docs/UI_Mockups/ or Assets/Art/UI_References/ for permanence and reference in WOs):**
- Town HUD (reference fidelity + Elarion style): `.../images/4.jpg`
- Combat / ATB Battle HUD (rings, skills, party): `.../images/5.jpg`
- Vendor Storefront (armor first, Tech skinned): `.../images/3.jpg`
- Settings + Dev Tools (visible rows): `.../images/2.jpg`
- Hero Select (fitting portraits + stat cards): `.../images/6.jpg`

### 4.1 Town HUD — Full Mobile (Matches hud_mobile_town.png Spirit + Kit)
**Layout (from reference image + code):**
- Top ornate gold-framed resource banner: Wood (brown icon) 320 | Iron (grey) 45 | Food (blue) 12 | Crystals (purple) 8. Dark parchment/metal trim.
- Top-left: "Heart of Elarion" niche with red HP bar + small crystal icon/count.
- Top-right: Intel (gear) button.
- Left vertical stack: 4 party frames (Hero top + 3 companions). Circular portrait, name, red HP bar, green secondary bar. Dark stone frames with gold accents.
- Center: "Next Wave 05:32" (subtle timer) + large gold banner "PLAY" / "Start Next Wave" button (or context "Defend" when wave imminent).
- Bottom-right: 4 town action buttons (BUILD axe, TALK, BAG, QUESTS) in stone/gold frames.
- Bottom-left: Virtual d-pad (large circle, knob).
- Footer note (design only): "TOWN — actions right thumb • move left thumb • quests = modal".
- Background: Subtle green/brown ground + house silhouette (play area visible).

**Styled spec for implementers (use ElarionUiKit.Panel/Well/Niche + Button(Gold/Quiet) + custom Resource/Party factories once added):**
- CanvasScaler ref 1080x1920, match 0.5.
- Top banner anchors: min (0, 0.92), max (1, 1).
- Party rows ~112px tall, 7px gap.
- All buttons minHeight 56px, generous padding.
- Dark misty purple-brown bg with fog. Gold (#d4af37 / Gilt brighter) + dark glass (0.06,0.07,0.09,0.66).
- No overflow, large legible text, high contrast (dark ink on gold CTAs).

**Adoption note:** Current VillageHudController is "sleek minimal dark-glass" — evolve it (or the kit) to exactly this reference once 405 is complete. Add the wave/play affordance visibility via the existing SetStartWaveAvailable bridge.

### 4.2 Combat / ATB HUD
**Layout:**
- Thin top info: "The Last Stand" + WAVE X + "Active: Knight" (or current unit).
- Bottom-left Command panel (glass + gold): Big Attack button. Skills (expands to 4 Q/W/E/R slots with ability name, cost, icon + cooldown ring). Item, Defend.
- Bottom-right Party (4 slots vertical or 2x2 on landscape): Portrait (kit resolver), name, HP red bar + text, MP blue, ATB ring (purple charging → gilt ready pop).
- Subtle aether violet accents for magic/ATB. Same misty dark bg or dimmed battle vignette.
- Large rings for cooldown/ATB (procedural or 9-slice from Tech pack).

**Key from code (BattleHudUgui + kit):** Already routes through ElarionUiKit for panels/bars/portraits. Fix remaining: wave text from BattleState.Wave, real abilities from Defs.HERO_ABILITIES[active], no empty skill subpanel, render on every state change.

### 4.3 Vendor Storefront (Armor First)
**Layout:**
- Top tabs (gold): BUY | SELL | UPGRADE | TALK (or context).
- Main scroll grid: Armor/weapons cards first (large icons, name, price in gold, BUY/SELL button). Rarity frames escalate.
- Side or header: Current wallet (resources + any SKR/glimmer).
- Consistent kit panels, large thumb rows (min 56–64px), clear affordance (green if can afford).
- Close "X" top-right. Scrim background.

**Style:** Tech hud elements buttons/frames + ElarionUiKit glass/gold/parchment. Dark ink text.

### 4.4 Settings + Dev Tools
**Layout:**
- Centered stone/parchment panel on dark scrim.
- Sections: Audio (sliders Master/SFX/Music — even if mixer is stub, UI must show), Graphics Quality, Controls (sensitivity, invert), Gameplay (difficulty), Account/Wallet.
- Dev-only (chord or dev build): Grant buttons, "Reset Yarn", "Load Full Base", diagnostics.
- Every row has visible label + control. No text under the interactive layer.
- Use kit rows + explicit LegacyRuntime.ttf or TMP for labels.

**Fix for WO-417:** Code-built (or hybrid with safe fallback). Rows must render on owner test harness.

### 4.5 Hero Select
**Layout:**
- Dark misty bg + subtle Elarion architecture.
- Gilt title "Choose Your Champion".
- 2x2 (or horizontal scroll on some devices) hero cards: Large portrait (fits fully, no clip), class + companion name (Grom/Sylas/etc.), short flavor text, 4 small stat icons/bars (or numbers), prominent gold "Select" button.
- Cards use Niche + gold rim from kit. Generous spacing, large targets.
- Returning player "Continue with X" path clearly visible.

**Fix for 328/329:** Portraits sized/anchored to stay inside card bounds. Stat cards/specs always rendered per hero.

---

## 5. Prioritized Quick Wins (Low Effort, High Felt or Holistic)
1. Fix all stale headers/comments on next touch of the file (esp HeroLocomotion).
2. Adopt ElarionUiKit in the remaining HUD surfaces + vendor/settings/hero select (post-405 owner kit approval).
3. Add the missing 6 catalog mirrors to Resources (WebGL safety).
4. Run the committed WO-408 texture opt scripts + document in build menus.
5. Strengthen RegressionSuite orientation + camera-yaw + HUD non-empty + seam cases (source-grep safe).
6. Delete or clearly mark dead (HUDManager/README_HUD, VirtualDPadLean, old Battle UXML wiring, duplicate build menu items).
7. Add explicit code-built fallback path for Settings/Pause.
8. CastleHubBuilder: non-destructive "apply current hand offsets" helper.
9. One generic pool + audit of 10 hottest Instantiate sites.
10. Vendor data population + isUpgradable/isShoppable split (unblocks several WOs).

---

**End of suggestions.** This file + the catalogs + WO-430 give a complete picture of the current issues and the right path forward. When in doubt: read the code (via catalogs), respect the lanes and serial bottlenecks, generate styled mockups for UI, run the gates, and let the owner playtest decide.

Update this document with new patterns as they are discovered. Deliver QUALITY.