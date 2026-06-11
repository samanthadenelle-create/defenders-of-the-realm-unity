# WO-391 — Interaction / Presentation Separation Strategy

**Status:** STRATEGY (read-only architecture pass — NO code changed). Gated by owner.
**Governing law:** `docs/ARCHITECTURE_PRINCIPLES.md` §1 (bounded context), §2
(presentation never touches objects), §3 (holistic work, done deliberately).
**Author:** dev-lead/architect pass, 2026-06-10. Owner makes the call on when + how.

> This is the "do it correctly when it's time" plan. It is intentionally NOT
> implemented tonight — re-architecting the interaction model of every structure is
> holistic, load-bearing work that earns its own WO + playtest, not a 1am improvisation.

---

## 1. Problem statement (the debt)

Proximity interaction is **decentralized across ~27 callers**. Each one independently:
1. resolves the hero + runs its own per-frame range check,
2. calls `MobileInteractButton.Request(owner, label, onTap)` when in range,
3. some ALSO render their own world-space prompt bubble (`_promptGo`),
4. polls a global `KeyCode.F` and/or consults `MobileInteractButton.Suppressed` +
   `PanelManager.AnyOpen` to gate itself.

Detection logic, gating logic, label strings, AND presentation are duplicated and
interleaved in every caller. This is the split-brain that caused DEF-213 (F firing
several buildings; toggle closing the wrong panel) and forces a fragile priority hack
(`Request(..., priority)`) to stop label flicker when watchers overlap on one building.

**Why it violates the law:** objects own their presentation (§2 — they build bubbles,
hold colors + "Tap / F" strings) and the interaction *concern* is not bounded into one
area (§1 — it's smeared across 27 files).

## 2. Caller inventory (current state)

Direct `Request(...)` callers (each = an interactable today):

| Caller | Action label today | Action |
|---|---|---|
| `BuildingInteractable` | `"Interact: <Building>"` | Yarn structure dialogue / PanelRouter |
| `CastleVendorNpcInjector` | `"Talk: <label>"` | `DialogueService.PlayStructure` |
| `MineNode` | `"Mine <Resource>"` | `Extract` / `ExtractReserve` |
| `CrystalMine` | `"Upgrade Crystal Mine"` / `"Confirm Upgrade"` | upgrade UI / simple upgrade |
| `DungeonPortal` | `"Enter: <name>"` | `EnterDungeon` |
| `DungeonEntrance` | `"Enter: <name>"` | `EnterDungeon` |
| `ArenaHeraldSpawner` | `"Enter Arena"` | `OpenArena` |
| `MarketplaceInteractor` | (release-only in grep window) | market |
| `BuildingUpgradePanelBootstrap` | release-only (priority watcher) | upgrade panel |
| `VillageCraftingPanelBootstrap` | release-only (priority watcher) | crafting |

Self-rendered prompt bubbles (`_promptGo`) live in: `BuildingInteractable`,
`CrystalMine`, `DungeonPortal`, `DungeonEntrance` (+ others using the bubble pattern).

Gating consumers: every caller reads `MobileInteractButton.Suppressed` (build mode) and
many read `PanelManager.AnyOpen`. `MobileInteractButton.IsActive` is read by the bubble
owners to suppress their bubble when the shared button shows.

## 3. Target architecture

A single **bounded interaction concern**, three isolated layers communicating by contract:

```
  GAMEPLAY OBJECT            INTERACTION SERVICE              PRESENTATION
  (exposes state)            (detection + arbitration)        (renders, never drives)
  ───────────────           ────────────────────────         ────────────────────────
  IInteractable      ──►     InteractionService         ──►   HUD context affordance
   • Label/ActionId           • registry of interactables      • reads "current action"
   • IsInteractable           • ONE nearest-in-range test      • shows/enables/dims it
   • CanInteract              • build-mode/modal gating         • NO range logic,
   • Interact()               • exposes CurrentInteractable      NO object refs
                              • fires F / tap → Interact()
```

### 3.1 Contract — `IInteractable` (Core or Village-interaction namespace)
```
string  InteractionLabel  { get; }   // clean noun/verb only ("Talk", "Mine Wood", "Enter Arena") — NO "[F]"
string  ActionId          { get; }   // stable id for analytics / context icon
Vector3 WorldPosition     { get; }   // for the nearest-in-range test
float   ActivateRadius    { get; }
bool    CanInteract       { get; }   // false ⇒ in range but not actionable (dim, don't hide)
void    Interact();                  // the object performs its own action
```
Objects EXPOSE state + perform their own action; they hold NO presentation.

### 3.2 `InteractionService` (one isolated area — the bounded concern)
- Maintains the registry (objects register on enable, unregister on disable).
- Runs **ONE** nearest-in-range evaluation per frame (kills the per-caller scans +
  the `IsNearestInRange` O(n²) loop + the priority hack).
- Owns ALL gating: build-mode suppression, `PanelManager.AnyOpen`, walk-away.
- Exposes `CurrentInteractable` (or null) + `CurrentLabel`/`CurrentActionId`.
- Routes desktop F + the presentation's tap → `CurrentInteractable.Interact()`.
- Pure logic. NO Image, NO Canvas, NO colors. Lives in `DeNelle.Village` (objects do).

### 3.3 Presentation (HUD layer — reads only)
- The HUD (DeNelle.HUD) reads the service's current action (via a Core seam /
  `CoreServices`, same pattern as `IVillageHud`) and shows a single context affordance
  styled to the HUD (parchment/gilt), enabled when `CanInteract`, dimmed when not,
  hidden when no interactable. NO range math, NO object references.
- The legacy `MobileInteractButton` becomes (or is replaced by) this thin presenter.

## 4. Migration sequence (nothing breaks mid-flight)

This is the part that makes it safe. Strangler-fig, not big-bang.

1. **Add the contract + service ALONGSIDE the existing button** (no caller changes).
   Service is dormant until objects opt in.
2. **Adapter shim:** `MobileInteractButton.Request(owner,label,onTap)` internally
   registers a transient `IInteractable` with the service. Existing 27 callers keep
   compiling + working unchanged — they're now feeding the new service via the shim.
3. **Service becomes the single arbiter** (nearest-in-range, gating). Delete the
   per-caller `IsNearestInRange` loop + the `priority` overload once the service owns
   arbitration. Verify parity.
4. **Presentation swap:** route the HUD context affordance off the service; retire the
   old button visuals. (The Tier-A reskin done tonight is interim presentation only —
   it does NOT block this.)
5. **Migrate callers off the shim** one lane at a time (buildings → mines → portals →
   arena → vendors), each replacing its `Request/Release/IsActive/_promptGo` block with
   a clean `IInteractable` implementation. Per lane: one PR, one playtest.
6. **Delete the shim + bubble code** once the last caller is migrated.

Each step is independently shippable and reversible — no flag day.

## 5. Risks + mitigations

- **Behavior drift** (e.g. walk-away auto-close on Yarn structures in
  `BuildingInteractable`): port these into the service explicitly; don't assume.
- **Priority/overlap semantics** (DEF-217): the nearest-in-range + `CanInteract`
  model must reproduce "one stable prompt per shared building." Validate with the
  Farm/Workshop overlap case that originally flickered.
- **Build-mode + modal suppression** must stay exactly as strict (the single chokepoint
  is a feature, not a bug).
- **WebGL:** keep it throw-safe (uncaught throw halts the player); service ticks guarded.
- **No scene edits / bakes:** service self-bootstraps (RuntimeInitialize), same as the
  current button + bridges.

## 6. Test plan (the playtest gate)

Every interaction type still works AND reads clean (no "[F]", no bubble): talk-to-NPC,
mine (wood/iron/crystal/food), enter dungeon (portal + entrance), enter arena, market,
building upgrade, crafting, vendor. Overlap case (stand between two buildings) shows ONE
stable action. Build mode + open modal suppress it. Desktop F + touch tap both fire the
nearest. Owner/Tricia felt-test.

## 7. Effort / sequencing call (for the PM)

- Steps 1–4 (contract + service + arbiter + presentation) ≈ the core; one focused WO.
- Step 5 (caller migration) parallelizes by lane (§9 of CLAUDE.md) — disjoint files,
  fan out edit-only agents, batch-gate once.
- **Do NOT start until the owner schedules it** as the highest-leverage move. Until
  then, the Tier-A presentation cleanup gives the desired *look* with zero architecture
  risk; this plan delivers the *right structure* when it's time.

---

*Want triangulation: spin 2–3 parallel read-only architect agents to each propose a
separation design, then synthesize. Deferred to an awake decision (don't fan out a
fleet overnight unprompted).*
