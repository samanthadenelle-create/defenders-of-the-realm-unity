# WORK ORDER 1286 - Mobile-first Obsidian card navigation

**Status:** DONE - implemented, device-captured and SME-approved 2026-08-31

## Player problem

The game exposes destinations through a bottom action bar, a persistent left-side pair, a drawer,
world interactions, tabbed panels, and modal-to-modal routes. Each surface can be correct locally
while the whole asks the player to remember where features live. Player feedback names navigation,
not world movement, as difficult.

## Ruled design

Obsidian remains the visual system. Cards become the recognition and navigation system. One shared
workspace contract governs every migrated destination:

- Back returns exactly one page within the workspace.
- Close exits the workspace to play.
- Done commits/ends a mode and returns to its declared parent.
- A visible title and optional subtitle identify the current page.
- Locked destinations remain stable and explain their requirement in words.
- Cards launch recognition-heavy categories; rows remain for queues and transactions.
- At most five stable peer destinations form the primary deck: Realm, Build, Manage, Hero, Journey.

## Architecture

1. `NavigationStack<T>` is a pure, testable history/state owner.
2. `ObsidianNavigationWorkspace` owns one `PanelManager` handle, one renewable `WorldHold`, one
   modal canvas and one render seam. It never owns gameplay/economy/catalog logic.
3. Feature workspaces provide page data and content rendering through overrides/delegates.
4. Existing `PanelRouter` destinations and authorities remain the execution seams.

## Migration order

1. Shared foundation and permission-gate tests.
2. Build Collections and Manage.
3. Hero (Bag, Equipment, Skills, Loadouts).
4. Journey (Quests, Map, Raids, Dungeons/seasonal destinations when registered).
5. Realm plus Store/Menu secondary actions; retire duplicate navigation only after parity is pinned.

## Acceptance gate

- One predictable Back/Close contract across migrated workspaces.
- No gameplay, economy, billing, persistence, or catalog-authority change.
- No more than five stable primary destinations.
- Every primary destination carries a word label and visual identity; no color-only state.
- Minimum touch floor and safe area honored at the Seeker landscape reference.
- Common tasks reach their content in at most two navigation selections.
- Compile, EditMode, data regression and captured UI suites are green and fresh.
- Captured PNGs are opened and reviewed at phone scale.
- Final SME review evaluates hierarchy, legibility, reachability, consistency, reversibility,
  accessibility and task efficiency. Findings are resolved before this WO can be called DONE.

## Explicit exclusions

- Camera/wall clipping is a separate camera defect.
- No monetization, price, reward, economy or save migration is authorized here.
- No hand edits to `.unity` scenes.

## Completion evidence

- Result: `WORK_ORDER_1286_mobile_first_obsidian_card_navigation_RESULT.md`
- SME review: `docs/qa/WO_1286_MOBILE_NAVIGATION_SME_REVIEW_2026-08-31.md`
- Focused device capture: 15/15 frames, 15/15 geometry-clean and 15/15 touch-clean.
- EditMode: 1,033/1,033 passed on the final source.
- Data regression: 332/332 passed.
