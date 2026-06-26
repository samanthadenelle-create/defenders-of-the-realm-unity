> ⚠ **HISTORICAL — describes the retired React v1 client (decommissioned in the Unity port).** Not an active mandate. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Architectural Refactor — Feature Modules + Prototype Branch

**Status:** Architectural plan. The owner has explicitly stated this is the right structure for the codebase and the prior single-megafile pattern was the wrong call. The work runs on a long-lived prototype branch, gets validated before merge, and replaces the current sprawl with isolated feature modules.

**Audience:** Claude Code, executing autonomously on a prototype branch.

**The single biggest reason this is needed:** `src/components/village/Village3D.tsx` is ~9,000 lines and contains village + waves + enemies + buildings + walls + hero + pets + abilities + HUD + settings + repair + breach triggers + ATB mount + Tower-Sim mount + audio handoff + camera + day/night + ambient + dev tools. It just got TRUNCATED at line 8567 (`docs/uat-playthrough-report.md` §3) which broke the entire build. A file that one Claude Code session can accidentally cut in half mid-statement is too big to be a single file. The refactor is the structural fix; the truncation was the symptom.

**Pre-flight blockers (must be resolved BEFORE this work starts):**

1. Village3D.tsx restoration P0 (the truncation) — restore from git.
2. Poof removal (`docs/poof-removal-overnight-spec.md`) — refactor on a clean tree, not one mid-rip.

Both should land on `main` before the refactor branch is cut. Refactoring a broken build is impossible; refactoring while ripping out a platform creates merge hell.

---

## 1. Target architecture — feature modules

Replace the current `src/components/<grab-bag>` with **module folders** that group everything one module needs in one place. Each module folder owns its components, hooks, types, and content. Cross-cutting presentational primitives live in `ui/`. External boundaries (DB, auth, APIs) live in `services/`. State lives in `state/`. Static asset metadata lives in `assets/`. Static content (story, tooltips, quests, dungeons) stays in `content/` (unchanged from today).

### 1.1 Proposed top-level structure

**Updated 2026-05-17 (owner refinement):** the module boundary set below
supersedes the earlier draft. Pets are now their own module (they appear in
village + battle + dungeons), clans + chat are separated (different concerns),
and `services/` + `assets/` are explicit boundaries.

```
src/
  core/                           # App shell, routing, providers
    App.tsx                       # Route table only — no Providers inline
    providers/                    # ThemeProvider, AudioBootstrap, etc.
    routes/                       # One file per route, each ~5 lines
    bootstrap/                    # main.tsx, globals.css wiring
  modules/                        # ONE FOLDER PER MODULE — see §1.2 for shape
    player/                       # Profile, save state, progression, settings
    village/                      # Village3D, buildings, walls, gate, waves, hero rig
    battle-atb/                   # ATB Last-Stand combat
    battle-tower-sim/             # FPS-style breach combat
    dungeons/                     # SVG dungeon exploration + quest runtime
    pets/                         # Pet rendering, AI, bond — used by village + battle + dungeons
    clans/                        # Clan creation, membership, leadership
    chat/                         # Mailbox, clan chat, message UI
    wallet/                       # Solana wallet integration + identity
  contracts/                      # Shared TYPE interfaces only (no runtime). Combatant, Pet,
                                  # Hero, Enemy, Inventory, Progression, Persistence shapes.
                                  # The escape hatch for the "no module imports another module"
                                  # rule — see §1.5. Pure types, no React, no functions.
  ui/                             # Reusable presentational primitives (SkillNode, GameTooltip,
                                  # shadcn primitives, layout components, effects)
  assets/                         # Static asset URLs, asset registry, sprite metadata
  services/                       # External boundaries: database, auth, persistence APIs,
                                  # api-client, anything that talks to a network or storage
  state/                          # Zustand stores + save schema (slice-per-module — §3.6)
    gameStore.ts                  # Composed from per-module slices
    atbStore.ts
    dungeonRuntimeStore.ts
    towerSimStore.ts
    saveSchema.ts
  data/                           # Static data tables (unchanged)
    enemyRegistry.ts
    enemyRoles.ts
    biomeElements.ts
  content/                        # Static content (unchanged)
    quests/
    dungeons/
    story.ts
    tooltips.ts
    atb-tooltips.ts
    tower-sim-lines.ts
    loot-tables.ts
  types/                          # Cross-cutting types
```

### 1.2 What a module folder looks like

Use `village/` as the worked example. Same shape applies to every module.
**Note:** pets are NOT under `village/` anymore — they're their own top-level
module (`modules/pets/`) because pet code is consumed by village + battle-atb +
battle-tower-sim + dungeons. Same logic applied to the wallet and clans.

```
src/modules/village/
  index.ts                        # Barrel export — public surface ONLY
  Village3D.tsx                   # Top-level mount; ~200 lines; composes the modules below
  scene/                          # The 3D scene + camera + lighting
    VillageScene.tsx
    VillageCamera.tsx
    VillageLighting.tsx
    DayNightCycle.tsx
  buildings/                      # Building system (placement, damage, repair UI)
    BUILDING_SPOTS.ts             # Static spot definitions
    BuildingMesh.tsx
    BuildingPlacementUI.tsx
    BuildingMenu.tsx
    useBuildingPlacement.ts
  walls/                          # Wall ring + gate
    KayWalls.tsx
    Gate.tsx                      # NEW per docs/gate-design-spec.md
    types.ts                      # Collider with acceptedEntityTypes
  hero/                           # Hero rig, movement, abilities (the hero IS village-specific —
                                  # battle has its own party-combatant rendering)
    HeroRig.tsx
    HeroAbilities.tsx             # Q/F/E/R per class (W is movement — see commit 8a93c1754)
    HeroMovement.tsx              # WASD + joystick + tap-to-move
    abilities/                    # One file per ABILITY_SETS class
      mage.ts
      knight.ts
      ranger.ts
  heart/                          # The Heart, HP, voice lines
    Heart.tsx
    HeartVoice.tsx
  waves/                          # Wave manager, spawn pacing
    WaveManager.tsx
    WaveTimer.tsx
    waveScaling.ts                # MOVE from src/components/village/
  enemies/                        # Enemy rigs + opportunistic AI in 3D space
    WaveEnemy.tsx                 # The KayKit skeleton; opportunistic-attack tick lives here
    enemyArchetypes.ts            # KAY_ENEMIES + NECROMANCER
    enemyAI.ts                    # Path + attack logic, pure
  ambient/                        # Decoration that has no gameplay effect
    SwimmingFish.tsx
    Fireflies.tsx
    Particles.tsx                 # If village particles are village-specific
  dev/                            # Dev-only buttons (Reset Village, +Resources, etc.)
    DevToolsPanel.tsx
  hooks/                          # Village-specific hooks
    useVillageInput.ts
    useCombatRegistry.ts
  types.ts                        # Village-specific types (CombatState, EnemyData, etc.)
  village.css
```

The whole module is **discoverable from one folder.** A new contributor (human
or Claude session) opens `src/modules/village/` and can see everything the
village is, in one place, without grep.

#### Other module shapes (sketches — same pattern, smaller)

```
src/modules/pets/
  index.ts
  PetRig.tsx                      # In-village pet rendering
  PetAI.tsx                       # In-village AI (slot, tend, follow)
  PetBondHud.tsx
  battle/                         # Pet representations in battle screens
    PetBattleCombatant.tsx        # Wraps a Pet for ATB unit shape
    PetBattlePortrait.tsx         # Portrait for both ATB + Tower-Sim
  bond/                           # Bond math + ranking — pure
    bond.ts
  abilities/                      # Per-species battle abilities
    aether-sprite.ts
    flame-pup.ts
    ice-wolf.ts
  types.ts

src/modules/player/
  index.ts
  ProfilePanel.tsx                # Settings entry, hero/pet snapshot
  ProgressionHud.tsx              # Level, XP, talent counts
  SavePanel.tsx                   # Import/export buttons
  hooks/
    useProfile.ts
    useProgression.ts
  types.ts                        # Profile, Progression, SaveSnapshot

src/modules/clans/
  index.ts
  ClanPanel.tsx
  ClanCreateForm.tsx
  ClanInvitePanel.tsx
  ClanLeadershipControls.tsx
  hooks/
    useClanPoll.ts                # MOVE from src/hooks/
    useClan.ts
  types.ts

src/modules/chat/
  index.ts
  MailboxPanel.tsx
  SendMessageModal.tsx
  ClanChatPanel.tsx               # Lives here (uses the chat primitive), not in clans/
  hooks/
    useMailbox.ts
  types.ts

src/modules/wallet/
  index.ts
  GameWalletProvider.tsx          # After Poof removal — passthrough or real provider
  WalletConnectButton.tsx
  WalletStatusPill.tsx
  hooks/
    useLocalIdentity.ts           # The shim replacing @pooflabs/web useAuth
  types.ts
```

`ui/`, `assets/`, and `services/` follow the same `index.ts` barrel pattern —
flat folders of files, no nested sub-modules, since they're leaves.

### 1.3 Rules — read these before touching anything

> ## 🚫 The hard rule (owner-locked 2026-05-17)
>
> **DO NOT refactor behavior and change gameplay in the same commit.**
>
> A refactor commit moves code, renames things, extracts modules, updates
> imports. The diff should be exclusively structural. If you find a bug while
> moving a file: **file the bug as a TODO comment with a reference**, then
> address it in a separate commit on `main` AFTER the refactor merges (or as
> a parallel `fix/<thing>` PR — not on this branch).
>
> Why this matters: a mixed refactor-plus-behavior diff is impossible to
> review and impossible to bisect. If the build breaks after a refactor
> commit, the only thing the author should be debating with git is "did the
> import paths land correctly?" — never "did I change a side effect?"
>
> Concretely:
>
> - ✅ ALLOWED in a refactor commit: file moves, import path updates, barrel
>   creation, type-only renames, comment cleanup, splitting one file into
>   several, adding `index.ts` exports.
> - 🚫 FORBIDDEN in a refactor commit: changing default values, fixing a bug
>   "while you're in there", adjusting timings/durations, swapping algorithms,
>   altering store shape, adding/removing tracked fields, modifying render
>   output, adding new features or props, "improving" code style beyond what
>   the move strictly requires.
> - 📝 PROCESS: if you can't resist a fix mid-move, stash it into a TODO and
>   open a tracking entry in `docs/refactor-followups.md` (create the file if
>   it doesn't exist). The owner picks those up after merge.

Other rules each module must follow:

1. **Public surface via `index.ts` only.** A module exports a small, curated set of things from its `index.ts`. Nothing outside the module reaches deeper. Enforced by lint rule (`eslint-plugin-import` with `no-restricted-paths`) or by convention with a CI check.
2. **No module imports another module directly.** If two modules need to share, the shared thing moves to `ui/` or `services/`. If they need to coordinate state, they coordinate through `state/` (a Zustand store), not through a direct import.
3. **`ui/` and `services/` never import a module.** Hard rule. `ui/`/`services/`/`assets/` are leaves; `modules/` are branches; `core/` is the trunk.
4. **State stores live in `state/`, not in modules.** Easier to find, easier to migrate, save schema stays in one place.
5. **Files inside a module stay small.** Soft target: 250 lines per file. Hard ceiling: 500. If a file is creeping past 500, that's a sub-module trying to be born.
6. **One thing per file.** A `.tsx` file exports one component; a `.ts` file exports one cohesive set of helpers. No mega-files of unrelated exports.
7. **Tests colocate with code.** If a unit test exists, it sits next to the file it tests as `<file>.test.ts`. Skipped if the project doesn't have a test runner wired (flag it).

### 1.4 Ownership declarations — every module's contract with the rest

Each module gets an `OWNERSHIP.md` at its root that explicitly declares what it owns, what it may consume, and what it may NEVER touch. This becomes the contract the lint rule enforces and the document a future contributor reads first. **Required for every module — no exceptions.** Modules without an `OWNERSHIP.md` fail the lint check.

Template (`src/modules/<module>/OWNERSHIP.md`):

```markdown
# <module> — Ownership Declaration

## Owns

- (the runtime state, components, hooks, types this module is the
  single source of truth for)

## May consume (read-only or via explicit contract)

- player data via state/playerSlice
- pet contracts via src/contracts/pet.ts
- ui/ primitives
- audio cues via services/audio

## May NOT

- mutate <other-module>'s state directly
- import from modules/<other-module>/\* (must route via state or contracts)
- access persistence directly (must go via services/)
- redefine entity shapes already in src/contracts/
```

Worked example for `battle-atb/`:

```markdown
# battle-atb — Ownership Declaration

## Owns

- ATB combat runtime (atbStore, atbEngine)
- ATB HUD (battle screen, action panel, ATB bar strip)
- turn logic + combat action queues
- ATB-only animations (BreachVignette, dmg-rise floats)

## May consume

- player data (read-only via state/playerSlice)
- pet contracts (via src/contracts/pet.ts)
- enemy registry (via assets/enemyRegistry)
- audio cues (via services/audio)
- ui/ primitives (SkillNode, GameTooltip, shadcn)

## May NOT

- mutate village state directly (must request via state contracts)
- call dungeon runtime directly
- access persistence directly (must go via services/persistence)
- redefine PetCombatant / EnemyCombatant shapes (those live in src/contracts/)
```

This becomes critical the moment multiple Claude Code sessions, contributors, or future contractors enter the picture. The lint rule reads each `OWNERSHIP.md`'s "May NOT" list and flags violating imports.

### 1.5 `src/contracts/` — the shared-interface escape hatch

The "no module imports another module" rule (1.3 rule #2) is philosophically correct but creates a real problem in practice: pets need to know what a "battle combatant" looks like; battles need to know what a "pet" looks like; persistence needs to know what an "entity" is. Without an escape hatch, you get duplicated adapters, awkward state bridging, or over-centralized state.

`src/contracts/` is the escape hatch. It contains **shared interface definitions only — pure types, no runtime code.** Modules import from contracts freely; contracts never import from modules.

```
src/contracts/
  combatant.ts          # BattleCombatant, CombatantSide, CombatantStats
  pet.ts                # Pet, PetSpecies, PetBondRank, PetCombatant
  hero.ts               # Hero, HeroClass, HeroAbilitySlot
  enemy.ts              # EnemyDef (shape; data lives in assets/)
  inventory.ts          # ItemKind, ItemEffect, Inventory
  progression.ts        # XP, Level, TalentTree shape
  persistence.ts        # SaveSnapshot, save schema interfaces
  index.ts              # Barrel
```

Rules for `contracts/`:

- **Pure TypeScript types only.** No functions. No constants. No React. No runtime imports.
- **No imports from anything except other contracts.** Contracts never reach into modules, services, state, or assets.
- **Versioned with care.** Changing a contract shape is a breaking change for every consumer; bump a comment header.
- **Owned by the project**, not any single module. Lives at the trunk alongside `core/`.

With contracts in place, the worked rule becomes: _"No module imports another module's RUNTIME. Shared TYPE shapes go in `contracts/`."_

---

## 2. Refactor order (smallest → biggest, lowest-risk → highest-risk)

The refactor lands as **one feature per commit**. Each commit on the prototype branch is a complete extraction of one feature: the feature builds, the rest of the app still builds, no behavior changes. This makes `git bisect` cheap when something inevitably regresses.

### Phase A — Safe lifts (already-isolated code, just moves into the new shape)

These are quick wins. The components already live in their own files; we just move them into module folders and update imports. **No risk to behavior — see §1.3 hard rule.**

1. **`modules/dungeons/`** — `src/components/dungeon/*` and `src/components/DungeonExplorer/...` move under the module. Add a barrel `index.ts`. Update imports.
2. **`modules/battle-atb/`** — `src/components/battle/Atb*` + `AtbTutorialOverlay` + `AtbBattleScreen` + `ActionPanel` + `PartyRow` + `EnemyRow` + the rest of the ATB UI. Tower-Sim components in the same folder get extracted next. Barrel export.
3. **`modules/battle-tower-sim/`** — `src/components/battle/TowerSim*` + `TowerSimCamera` + `TowerSimSpire` + supporting hooks. Barrel export.
4. **`modules/pets/`** — pull pet rendering out of `Village3D.tsx` (Phase B item) AND the battle PartyPortrait pieces that are pet-specific. _Note:_ the pet AI in the village is village-side; the pet's BATTLE representation is pets-module-side. The pet's data + bond math + per-species abilities all live here.
5. **`modules/clans/`** — `src/components/clan/*` moves under `modules/clans/`. Clan creation, membership, leadership UI.
6. **`modules/chat/`** — `src/components/chat/*` (mailbox + send-message modal) moves under `modules/chat/`. Plus `ClanChatPanel` from the clan folder — chat primitives live here, even when the host is a clan.
7. **`modules/wallet/`** — `src/wallet/*` + `GameWalletProvider` + the `useLocalIdentity` shim (post-Poof-removal) move under `modules/wallet/`.
8. **`modules/player/`** — extract the SettingsPanel, the SaveSnapshot UI, the import/export buttons. These currently live scattered in Village3D and the HUD; consolidate into one module.
9. **`ui/`** — flat moves from `src/components/ui/` (shadcn primitives) + `src/components/effects/` + `src/components/poof-ui/` (renamed in Poof removal) + the new `SkillNode` primitive + GameTooltip. Flat folder, barrel export from `src/ui/index.ts`.
10. **`assets/`** — move `src/data/enemyRegistry.ts`, `enemyRoles.ts`, `biomeElements.ts`, the asset URL helpers, sprite metadata. The actual asset files in `public/` stay where they are.
11. **`services/`** — move `src/lib/api-client.ts` + everything that talks to a network or backend. The `audioManager` is a borderline call — it's pure-client but uses the network for asset URLs; my lean is to put it under `services/` because it's a singleton.

After Phase A, the tree is mostly reshuffled but `Village3D.tsx` is still a monolith. The build is green. Functionality is unchanged. We have a clean foundation for the harder work.

### Phase B — Village extraction (the big one)

Now the actual work. Village3D.tsx gets cut into the pieces under `src/modules/village/` per the structure in §1.2. **One extraction per commit.** Each commit:

- Pulls a self-contained chunk out of `Village3D.tsx` into its own file under `modules/village/<chunk>/`.
- Updates `Village3D.tsx` to import the new module.
- `bun run build:full` green.
- Smoke-test: village still loads, the extracted feature still works.

Recommended extraction order — **smallest/least-coupled first**, leaving the most-entangled bits for last when you have the most familiarity with the file:

1. **`village/ambient/`** — SwimmingFish, Fireflies, particles. Pure decoration; zero coupling to gameplay.
2. **`village/dev/`** — DevToolsPanel (Reset Village, +Resources, Reset Defenses). Already conditional; trivial move.
3. **`village/scene/DayNightCycle.tsx`** — already named; extract.
4. **`village/scene/VillageLighting.tsx`** + **`VillageCamera.tsx`** — pull out the camera and lighting refs.
5. **`village/heart/`** — the Heart mesh + HP + voice lines.
6. **`village/walls/`** — KayWalls + the new Gate from `docs/gate-design-spec.md` if it lands first. Collider system goes here.
7. **`village/buildings/`** — BUILDING_SPOTS + the building mesh + placement UI. Likely the second-largest chunk after Village3D itself.
8. **`village/hero/`** — HeroRig + HeroAbilities + HeroMovement. Q/F/E/R per class moves into `hero/abilities/{mage,knight,ranger}.ts`. (W reserved for movement.)
9. **`modules/pets/` (from village)** — PetRig + village-side PetAI. The pets module already exists from Phase A; this commit extends it with the village-rendering bits that were inline in Village3D.
10. **`village/waves/`** — WaveManager + WaveTimer + waveScaling.
11. **`village/enemies/`** — WaveEnemy + enemyArchetypes + the opportunistic-attack tick from `docs/enemy-aggression-spec.md`. This is the chunk most coupled to gameplay state — leave for late.
12. **`village/Village3D.tsx`** (the top-level mount) — what's left should be ~200 lines that just composes the modules.

If a chunk is too coupled to extract cleanly (you'd need to touch many call sites to do it), **leave it and extract something else first**. You'll often find an entire sub-tree of dependencies becomes extractable once you've cleared the air around it.

### Phase C — State slicing (optional, depends on appetite)

`gameStore.ts` is large. AFTER Village3D is broken up, you can split it into per-feature slices:

- `state/slices/heartSlice.ts` — heartHp, heart-related actions
- `state/slices/buildingsSlice.ts` — buildingDamage, repair actions
- `state/slices/heroSlice.ts` — heroClass, hero level, talents
- `state/slices/petsSlice.ts` — pet bond, pet selection
- `state/slices/resourcesSlice.ts` — wood/stone/iron/coins/crystals
- `state/slices/waveSlice.ts` — wave number, prep timer
- `state/slices/dungeonSlice.ts` — already mostly its own thing
- `state/slices/tutorialSlice.ts` — seenTutorials + markTutorialSeen
- `state/slices/settingsSlice.ts` — movementStyle, breachStyle, etc.

Compose them into the single `useGameStore` at the top level (Zustand supports this cleanly). Save schema in `state/saveSchema.ts` reflects the slice shape.

This is OPTIONAL. The store is less painful than Village3D. If Phase B uses up the prototype budget, ship without Phase C and add a follow-up issue.

### Phase D — Doc + lint reflow

Once the structure is in, lock it in:

1. **CLAUDE.md** — rewrite the File Structure section to show the new layout. Add explicit rules about module isolation, the `ui/` + `services/` leaf constraint, the 500-line ceiling, the `index.ts` barrel pattern, and the §1.3 hard rule.
2. **AREAS.md** — restructure around the feature folders.
3. **Lint rule** — add `eslint-plugin-import` with `no-restricted-paths` to enforce no cross-module imports (no `modules/* → modules/*` reach-throughs) and no `ui/` / `services/` importing from `modules/`. If the rule is too aggressive at first, ship a warning-level rule and tighten over time.
4. **CI** — add a single check that fails if any file under `src/modules/*/` is over 500 lines (`find` + `wc -l` + `awk`). Hard ceiling enforced.

---

## 3. Prototype branch workflow

### 3.1 Branch creation

```
git checkout main
git pull
git checkout -b refactor/feature-modules
git push -u origin refactor/feature-modules
```

The branch lives until the refactor is fully validated and ready to merge. Expect days, not hours.

### 3.2 Commit per extraction

Every commit on the branch is:

- ONE feature folder created or extended, OR
- ONE chunk of Village3D extracted into its module, OR
- a tooling change (lint rule, CI check, doc update).

Subject line follows the existing pattern:

- `Refactor: lift <thing> into modules/<module>/`
- `Refactor: extract <chunk> from Village3D`
- `Refactor: enforce feature-module lint`

Body explains WHAT moved and verifies the smoke tests passed.

### 3.3 Rebase strategy

`main` will move underneath the branch while the refactor runs (bugfixes, content changes). Rebase the branch **daily** to catch conflicts early. Don't let the branch drift more than ~3 days behind main.

Use `git rebase --interactive main` from the prototype branch. Resolve conflicts per commit; the small-commit discipline pays off here.

### 3.4 Don't ship features on the prototype branch

If a new feature spec lands during the refactor, it gets built on `main`, not on the prototype branch. The prototype is a structural refactor; new code goes elsewhere. This is the rule that keeps the branch from becoming a forever-fork.

If a feature is BLOCKED by the refactor (e.g. a new HUD widget needs the new `ui/` layout), wait for the refactor to merge, then build it.

### 3.5 Verification gates per commit

Each commit must satisfy:

- `bun run build:full` exits 0.
- No new warnings about unused imports / unused exports.
- The dev server boots and `/`, `/onboarding`, `/village`, `/dungeons` each render.
- Any feature touched by the commit still works in manual smoke test (e.g. tap a Hollowmouth → /dungeons routes correctly).

If a commit fails any gate, FIX the commit on the branch before adding another. Don't pile commits on top of broken ones.

### 3.6 Merge-back acceptance criteria

The branch is ready to merge to `main` when ALL of:

- [ ] Every Phase A item completed.
- [ ] At least items 1-9 of Phase B completed (ambient through pets); items 10-12 may be deferred to a follow-up if appetite runs out, with a clearly-flagged TODO.
- [ ] Village3D.tsx is under 1,000 lines (was ~9,000).
- [ ] No file in the entire `src/` tree is over 500 lines for logic/state/services (per §4 answer 3), except rendering-heavy orchestrators allowed up to 700.
- [ ] **Every module has an `OWNERSHIP.md`** at its root following the §1.4 template — owns / may consume / may NOT.
- [ ] **`src/contracts/` exists, contains only pure type definitions**, and imports nothing from `modules/`, `ui/`, `services/`, `state/`, or `assets/`.
- [ ] No module imports another module's RUNTIME directly (lint check passes). Cross-module TYPE imports through `contracts/` are allowed and encouraged.
- [ ] `ui/`, `services/`, and `assets/` do not import any `modules/` file (lint check passes).
- [ ] Lint level: still WARNING during the branch; switch to ERROR happens in a SEPARATE follow-up commit on `main` after merge (per §4 answer 2).
- [ ] CLAUDE.md File Structure section is rewritten and accurate, with the §1.3 hard rule + §1.4 ownership requirement + §1.5 contracts explainer.
- [ ] `bun run build:full` green.
- [ ] Dev server smoke test: every route renders, every major flow works (start a wave, place a building, deploy a pet, trigger a breach, enter a dungeon, save/load).
- [ ] Branch has been rebased on `main` within the last 24 hours.
- [ ] A consolidated PR description lists every feature module created with a one-line summary of what's in it, plus links to each `OWNERSHIP.md`.

When all of these pass, **the owner opens the PR and approves the merge** — same convention as the ATB and dungeon merges. Do not auto-merge.

---

## 4. Open questions — ANSWERED (locked 2026-05-17 after external review)

1. **State slicing — YES, but NOT during initial extraction.** Phase A + Phase B first (same behavior, same store contracts, new module boundaries). Phase C state slicing happens AFTER those merge and stabilize. Refactoring structure AND state ownership simultaneously massively increases regression risk.
2. **Lint — WARNING during the prototype branch, STRICT after merge-back.** During migration Claude Code WILL temporarily violate boundaries (unused exports, intermediate import paths). Strict-from-day-one would slow momentum badly. Flip to error-level after the branch lands on main.
3. **500-line ceiling — HARD for logic/state/services; SOFT 700 for rendering-heavy orchestration.** Hard 500 enforced via CI for anything under `state/`, `services/`, `modules/*/hooks/`, `modules/*/abilities/`, pure-logic `.ts` files. Soft 700 grandfathered for orchestrator files that naturally compose scenes / routes / providers / layout mounts (e.g. `Village3D.tsx`, top-level battle screen mounts). The true enemy is mixed responsibility, not line count alone — but the ceiling is a useful tripwire.
4. **Tests during refactor — NO.** Pure structural moves only. Adding test fragility on top of moving files multiplies debugging surface. After Phase D, open a separate spec for integration/store/persistence/smoke tests.
5. **Folder naming — kebab folders, Pascal components.** `battle-atb/`, `battle-tower-sim/`, `village/` for folders; `VillageScene.tsx`, `HeroRig.tsx`, `AtbBattleScreen.tsx` for files inside.

---

## 5. What this is NOT

- Not a rewrite. No behavior changes during the refactor.
- Not a redesign of components. If a component is ugly inside, it stays ugly — moved, but not rewritten.
- Not a TypeScript strictness pass.
- Not an asset reorganization.
- Not a feature-add. Specs that landed today (gate, aggression, touch movement, tutorial, etc.) get built on `main`, not on this branch.

If a refactor reveals a bug, file the bug for later. Don't fix it in the refactor commit — it muddies the diff and breaks the "no behavior change" guarantee.

---

## 6. The first three commits (so Claude Code has a clear starting move)

To get the branch started concretely, here's the suggested first three commits.
Each one obeys the §1.3 hard rule — pure structure, no behavior change.

1. **`Refactor: scaffold core/, modules/, contracts/, ui/, assets/, services/, state/`** —
   create empty folders + a brief `README.md` in each explaining what goes
   there + the §1.3 hard rule pinned at the top of every README. No file
   moves yet. Update `CLAUDE.md` File Structure section preview. Add the
   lint rule (warning-only for now). Also create `docs/refactor-followups.md`
   as the TODO landing pad. Seed `src/contracts/` with empty `combatant.ts`,
   `pet.ts`, `hero.ts`, `enemy.ts`, `inventory.ts`, `progression.ts`,
   `persistence.ts`, plus `index.ts` barrel — content extracted in Phase A
   commits as types get pulled out of their current homes.
2. **`Refactor: lift dungeons UI into modules/dungeons/`** — flat move of
   `src/components/dungeon/*` and the dungeon route handler.
   Barrel export from `src/modules/dungeons/index.ts`. Add the module's
   `OWNERSHIP.md` per the §1.4 template. Build green.
3. **`Refactor: lift ATB battle UI into modules/battle-atb/`** — flat move
   of `src/components/battle/Atb*` + `AtbTutorialOverlay` + the supporting
   screens. Barrel export. Add the module's `OWNERSHIP.md`. Build green.

After these three, the muscle memory is set: every subsequent commit follows the same pattern (move into feature folder, barrel export, build green, commit).

---

## 7. Estimated effort

- **Phase A (flat moves):** ~6–8 commits, half a day with smoke tests between each.
- **Phase B (Village3D extraction):** ~12 commits, 1.5–2 days if going methodically. The first chunks are quick; the last few will be tricky.
- **Phase C (state slicing) — OPTIONAL:** ~half a day.
- **Phase D (doc + lint reflow):** ~half a day.
- **Verification + smoke tests + PR description:** half a day.

**Total: ~3 days of focused autonomous work** for one Claude Code session. Faster if the agent gets the rhythm; slower if it hits coupling surprises in Village3D (likely, especially around the combat registry and the ability cast loop).

---

## 8. Coordination with other in-flight work

This refactor is large; other work shouldn't pile on top of it. Suggested rule for the next ~3 days:

- **Block on prototype branch:** any change to `Village3D.tsx`, `src/components/battle/`, `src/components/dungeon/`. Those files are being moved and edits will conflict.
- **Fair game on `main`:** Poof removal (already running), bug fixes elsewhere, doc edits, asset additions, new presentational primitives (since they end up in `ui/` anyway).
- **Postpone:** gate spec build, aggression spec build, touch-movement spec build, SkillNode extraction — these all touch files being refactored. They go on the queue right after the merge-back lands.

---

## 9. What the owner needs to do

- ~~Confirm the architectural shape in §1~~ ✅ confirmed 2026-05-17
- ~~Answer the §4 open questions before Phase C starts~~ ✅ all 5 answered 2026-05-17 (see §4)
- Approve the prototype branch creation.
- Review the PR description before merging.

That's it. Claude Code can run the rest autonomously per the workflow in §3.

---

_This is the structural fix the codebase needs. The Village3D truncation last night was the canary in the coal mine — a file that big is too big for any single Claude session to safely edit. After this refactor, every feature lives in its own folder, every file fits in a context window, and a new contributor (human or AI) can find anything in under a minute._
