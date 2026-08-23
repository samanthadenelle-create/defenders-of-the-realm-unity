# WO-1114 â€” Dungeon Status: a remotely-flippable, in-world door state

**Status:** BLOCKED ON DEPLOY — BUILT, NOT DEPLOYED (reconciled 2026-08-22). R1-R6 landed and the oracle is
registered; nothing further is owed in the Unity tree. *(Was: READY - PARTIAL - 2026-08-21 CLI, gate-green
(COMPILE_GATE_OK + REGRESSION_OK 234/234).)*

> ### VERIFIED AT SOURCE 2026-08-22 — what is done, and the exact three things that are not
> **Done:** the oracle is registered at `Assets/Editor/Regression/DataRegression.cs:718` as the
> `dungeon-status suite` (`DungeonStatusRegression`, logs `[dungeon-status]`). The backend half exists in-repo:
> `api/schema.sql:810-848` declares `CREATE TABLE IF NOT EXISTS dungeon_status` (`dungeon_id` PK, `status`,
> `updated_at`), read by `api/dungeon-status.js` (public GET, per `schema.sql:831`).
>
> **NOT done — all three are deploy/data steps, none of them code:**
> 1. `api/schema.sql` has not been run against Neon (the table does not exist in the live DB).
> 2. `vercel --prod` has not been run, so `api/dungeon-status.js` is not live.
> 3. **SEED ROWS ARE MISSING for the two REAL dungeons.** `schema.sql:843-848` seeds only
>    `dg_starter_loop`, `dg_sunken_vault`, `dg_bonecrypt` and `dg_ember_deep`. There is **no**
>    `dg_healers_cottage` and **no** `dg_folks_granary` row — and the `INSERT` is
>    `ON CONFLICT (dungeon_id) DO NOTHING`, so simply re-running the file will never add them.
>    Add those two rows before the schema is applied, or the two shipped dungeons fall to the
>    client default with no server row behind them.

**Minted:** 2026-08-17 (CLI seat, main line â€” banner bumped 1113 â†’ 1114 in this same edit)
**Owner ruling captured:** *"I want to have a line that reads if any dungeon is closed for dev work,
that it's under construction, or states mine collapse, rescue in process, or anything that allows us
to flip on or off without a build so we can make all dungeons feel like the two real ones with depth."*
Follow-up ruling: **the client side carries the most weight.**

---

## 1. The problem, stated honestly

We ship more dungeon *doors* than we ship finished dungeon *rooms*. Today a player who walks into an
unfinished one gets an empty or half-built space, which reads as a **broken game**. The two real
dungeons read as deliberate; the rest read as unfinished.

The fix is NOT to hide the unfinished doors. Hiding them makes the world smaller and telegraphs "this
is a demo". The fix is to give every door a **state that is part of the world**, and to make that
state flippable from the backend so content can open and close **without a store build** â€” which
matters doubly on the Solana dApp Store, where every change otherwise costs a review cycle.

> ### âš  THE DESIGN RULE THIS WO EXISTS TO ENFORCE
> **A closed dungeon must read as WORLD, never as BUILD STATUS.**
> "Under construction" / "coming soon" / "disabled for dev" are BANNED strings in player-facing copy.
> They convert a deliberate-feeling world back into a visibly unfinished build â€” the exact outcome
> this WO is buying our way out of. The dev meaning lives in the `status` enum; the PLAYER only ever
> sees authored in-world prose. See Â§4.

---

## 2. Scope split â€” client is the weight (owner ruling)

| Half | Weight | Why |
|---|---|---|
| **CLIENT** (portal door state, banner, gating, fallback) | **~80%** | This is what the player experiences. It is also where every failure mode that can hurt us lives (see Â§6). |
| **BACKEND** (one table, one endpoint) | ~20% | Small and well-understood; `api/` already exists in-repo (Vercel serverless + Neon). No new infrastructure. |

Build the client FIRST against a local stub JSON, prove the whole experience, and wire the endpoint
last. The client must be fully correct with the backend switched off â€” that is not a fallback path,
it is the **default** path (Â§6).

---

## 3. Data contract

One row per dungeon id. Ids are the existing `dg_*` contract â€” **do not invent new ones** and do not
rename any (`dg_hollow_roads` is a hard contract in `BiomeRoads.ArmRoomIdFor`, the graph json, the
injector and `BiomeRoadsRegression`).

```json
{
  "version": 1,
  "dungeons": {
    "dg_starter_loop":  { "status": "open" },
    "dg_ember_deep":    { "status": "open" },
    "dg_bonecrypt":     { "status": "sealed",   "headline": "The Bonecrypt is sealed",
                          "body": "The Wardens drove iron through the doors after the last delve. None of them will say why.",
                          "sigil": "seal" },
    "dg_sunken_vault":  { "status": "collapsed", "headline": "The lower shaft has collapsed",
                          "body": "Rescue crews are still digging. The Vault will not take visitors.",
                          "sigil": "rubble" }
  }
}
```

**Fields**
- `status` â€” REQUIRED enum: `open` | `sealed` | `collapsed` | `rescue` | `flooded`.
  This is the DEV-MEANINGFUL field and the only thing code branches on. Anything not `open` closes the door.
- `headline` / `body` â€” OPTIONAL authored prose. When absent, fall back to a per-status default
  string from `canon-strings.json` (NOT a hardcoded literal â€” CLAUDE.md Â§7).
- `sigil` â€” OPTIONAL art key for the door treatment. Unknown/absent â†’ the default seal.

**Unknown `status` value = treat as `open`.** A future backend typo must never lock a player out of
working content. Log it via `FlowTrace.Warn`; do not fail closed.

---

## 4. Client work (the weight)

### 4a. The door, not the loading screen
Gating happens **at the portal, before entry** â€” `DungeonWorldPortalSpawner` / the portal's
interactable. A player must never load into a dungeon and get bounced back out; that is
indistinguishable from a crash. The sealed door is the content.

### 4b. What the player sees
On approaching a non-`open` portal:
- the portal's vortex/particle treatment swaps to the `sigil` state (sealed = iron-barred, collapsed =
  rubble, flooded = water line). Reuse the existing portal structure â€” **do not add a second spawner**
  (CLAUDE.md Â§7, one appearance owner).
- an interact prompt reading the `headline`, and on interact, the `body` in the standard Obsidian
  dialogue frame. Use `ElarionUiKit` â€” hand-rolled UI trips the conformance gate.
- **no error styling, no dev vocabulary.** It should feel authored.

### 4c. Copy defaults (per status, authored â€” owner to ratify)
These are the strings used when the backend supplies no prose. Written as world, per Â§1:
- `sealed` â€” "The way is barred." / "Iron and old prayers hold this door. It will not open today."
- `collapsed` â€” "The shaft has fallen in." / "Rescue crews are still digging through it."
- `rescue` â€” "A rescue is under way." / "The Wardens have closed the approach until their people are out."
- `flooded` â€” "Black water fills the stair." / "Whatever is down there can wait for the dry season."

âš  **OPEN RULING for the owner:** ratify or rewrite these four pairs. They are the player's entire
impression of an unfinished dungeon, so they are creative canon, not filler.

### 4d. Fetch + cache lifecycle
- Fetch ONCE at boot, async, **non-blocking** â€” the title screen must never wait on it.
- Cache the last good payload to `Application.persistentDataPath` (NOT `Resources`, NOT
  `PlayerPrefs` for a JSON blob).
- Resolution order: **live fetch â†’ cached payload â†’ all-open default.**
- A dungeon already in progress is NEVER kicked mid-run by a status change. Status is read at the door.

---

## 5. Backend work (small)

- One table `dungeon_status` in the existing Neon database (id, status, headline, body, sigil, updated_at).
- One read endpoint under the existing `api/` tree returning the Â§3 payload. **Public read, no auth** â€”
  it is not sensitive and must resolve before sign-in.
- Cache-Control short (â‰¤60 s) so a flip propagates in about a minute without a client change.
- Writes are admin-only; reuse the existing admin-db path rather than minting a new auth surface.

---

## 6. Failure modes that MUST be closed (these are why client is 80%)

| Failure | Required behaviour |
|---|---|
| Backend down / DNS fail / offline play | Cached payload, else **all dungeons open**. Never lock content behind a network call. |
| Malformed JSON | `Guard.Try`, log `FlowTrace.Fail`, fall back to cache/all-open. One bad field never blanks the world. |
| Unknown status string | Treat as `open` + `FlowTrace.Warn`. |
| Unknown dungeon id in payload | Ignore it (a dungeon we haven't shipped yet). Do not throw. |
| Id present in game, absent from payload | Treat as `open`. Absence is not a closure. |
| Slow response | Non-blocking; the door resolves `open` until the payload lands. Never stall boot. |

**No silent catches** (CLAUDE.md Â§12): every one of the above logs.

---

## 7. Acceptance criteria

1. With the backend **unreachable**, every dungeon is enterable and no error reaches the player.
2. With a stub payload marking one dungeon `collapsed`, that portal visibly changes at the door, shows
   the authored prose, and cannot be entered â€” and every other dungeon is unaffected.
3. Flipping the value in the DB changes the door within one cache period **with no rebuild and no
   redeploy of the client**. This is the whole point of the WO â€” demonstrate it explicitly.
4. Zero player-facing string contains "construction", "coming soon", "disabled", "dev", "WIP", or "TODO".
   **Add a regression oracle asserting this over the status strings** â€” this is the rule most likely to
   rot, so it gets a gate, not a comment.
5. A dungeon entered before a status flip is not ejected mid-run.
6. UI routes through `ElarionUiKit` (conformance gate stays green).
7. `REGRESSION_OK` and `COMPILE_GATE_OK` both green.

---

## 8. Explicitly NOT in scope

- Do NOT change any `dg_*` id.
- Do NOT remove or hide any portal â€” the door stays, the state changes.
- Do NOT add a second portal spawner or a parallel appearance owner.
- Do NOT gate this behind sign-in or a wallet.
- Do NOT couple this to the Addressables/remote-content work. They pair well (status flips the door,
  remote content fills the room) but they must ship independently.

---

## 9. Open rulings for the owner

1. Ratify or rewrite the four default copy pairs in Â§4c.
2. Confirm the status enum is the right set â€” is `rescue` distinct enough from `collapsed` to be worth
   a separate state, or is it a `body` variant of `collapsed`?
3. Should a sealed dungeon still show its **name and depth** on the door (world-building, teases the
   content), or stay anonymous?

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `only IMPLEMENTATION_PLAN.md matches DungeonStatus` — remote door status unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

---

## LANE PASS 2026-08-21 (edit-only agent) — the STATUS FIELD landed. Status stays READY.

Built strictly to `WORK_ORDER_1114_IMPLEMENTATION_PLAN.md`. Scope was deliberately limited to the
**data/status half** (plan §2a, §2b, §2g, §2h) because the door/portal runtime was fenced for a
concurrent lane. This matches the plan's own ship order: **Phase 1 client (all-open) → Phase 2 gating
→ Phase 3 oracle → Phase 4 backend.** Phases 1 and 3 are in; Phase 2 and 4 are not.

### LANDED
| Plan | File | What |
|---|---|---|
| §2a | `Assets/_Modules/Core/World/DungeonStatusCatalog.cs` **(NEW)** | `DungeonDoorState` enum, `DungeonDoorInfo` readonly struct, the transport-free table. Atomic whole-table swap; `For()` never throws. |
| §2b | `Assets/_Modules/Core/World/DungeonStatusService.cs` **(NEW)** | `AfterSceneLoad` bootstrap, synchronous cache read, fire-and-forget `RefreshAsync().Forget()`, write-after-parse cache. |
| §2g | both copies of `canon-strings.json` | the eight `dungeon*Headline`/`dungeon*Body` keys + an authoring note. **Byte-identical** (md5 `7b8e30de…`). |
| §2h | `Assets/Editor/Regression/DungeonStatusRegression.cs` **(NEW)** | five cases, markers `DUNGEON_STATUS_OK` / `DUNGEON_STATUS_FAIL`. |

**The safety direction is one-way and it is asserted, not asserted-in-a-comment.** Unknown id, unknown
status string, absent id, null id, garbage payload, a version mismatch, an unshipped id and a `502`
HTML body **all resolve OPEN**, and a rejected payload provably **leaves the standing table intact**
rather than blanking it — Case 2 drives every one of those against the real catalog, headlessly, with
no network and no PlayMode. That is why §2a is transport-free.

**Case 1 is the banned-copy gate (§7 criterion 4).** It scans the **parsed values** in both copies, so
the authoring `_comment` stays free to name the very words it bans. `"dev"` is matched on a **word
boundary** — the plan's warning is honoured, because an oracle that reds on "devout" gets switched off.

### NOT DONE — and each one is named, not glossed
- **§2c `DungeonSealedDoorPanel` / §2d `DungeonPortal` gating / §2e `ApplyDoorState`.** The whole
  client-experience half. **Nothing gates a door yet** — the catalog answers, but no caller asks.
  Fenced off this lane; the plan's §3 names the exact gating point (`DungeonPortal.cs:126`, backstop
  at `:181`), so it is executable without re-derivation.
- **§2f `FeatureFlags.DungeonStatus`.** `FeatureFlags.cs` was lane-fenced and `FeatureFlags.Get` is
  `private`. The kill switch is therefore **inlined in `DungeonStatusService` with identical
  semantics** (PlayerPrefs `ff.dungeonstatus`, 0/1/absent), and flagged in-file as a **temporary home**
  with a ⛔ note to delete the local helper when the flag moves — two readers of one key is the
  duplicated-state failure this repo keeps paying for. **This is a one-line hand-off to the committer.**
- **§2i** the `DataRegression.RunAll` registration line (that file is lane-fenced, committer's job).
- **§2j** the backend (`api/dungeon-status.js`, `api/schema.sql`) and **§2k** the dev menu.
- **Case 5 `[door-appearance]`** stands down with a `PartialSkip` that **names the hole** rather than
  pretending coverage; it starts asserting automatically the moment `ApplyDoorState` exists.
- **Nothing here has been compiled** — no Unity, no `COMPILE_GATE_OK`, no `REGRESSION_OK`.

### ⚠ STILL OPEN FOR THE OWNER (§9) — unchanged, and now shipped as unratified copy
The four default copy pairs (§4c) are in `canon-strings.json` **as written in this WO**, marked
UNRATIFIED in the file's own authoring note. They are the player's entire impression of an unfinished
dungeon, so they are creative canon: **ratify or rewrite (§9.1)**. §9.2 (`rescue` vs `collapsed`) and
§9.3 (does a sealed door still show its name and depth) are also still open — neither blocks the
data layer, both block the door UI.

> **CLI 2026-08-21:** 62afe3201 - data half landed and DUNGEON_STATUS_OK ran green headlessly. REMAINING: door appearance (DungeonWorldPortalSpawner.ApplyDoorState does not exist - the oracle PARTIAL-SKIPs it by name), the backend, and the dev menu.

---

## REMAINING — named (PARTIAL stays until these land)

| # | Hole | Evidence |
|---|---|---|
| **R1** | **Nothing gates a door** | `DungeonPortal` still always `EnterDungeon` — catalog answers, no caller asks (`:126` / backstop `:181`). |
| **R2** | **No sealed dialogue** | `DungeonSealedDoorPanel` does not exist. |
| **R3** | **No door appearance** | `DungeonWorldPortalSpawner.ApplyDoorState` **absent**. Oracle Case 5 `[door-appearance]` emits `PartialSkip` by name until the symbol exists — then auto-asserts measurement helpers + `SwapInSharedStructureAsync` re-seat. |
| **R4** | **FeatureFlags home** | Kill switch still inlined in `DungeonStatusService` (`ff.dungeonstatus`). Move to `FeatureFlags.DungeonStatus`; delete the duplicate helper. |
| **R5** | **No backend** | No `api/dungeon-status.js` / schema table. |
| **R6** | **No dev menu** | Cannot flip statuses without a rebuild (needed to prove AC-2). |
| **R7** | **Owner §9 open** | Copy UNRATIFIED; `rescue` vs `collapsed`; sealed door shows name+depth? |

Data half that landed (keep): `DungeonStatusCatalog`, `DungeonStatusService`, canon-strings keys, `DungeonStatusRegression` (Cases 1–4 green; Case 5 honest skip).

---

## SOLUTION — concrete close-out (research 2026-08-17)

Follow `WORK_ORDER_1114_IMPLEMENTATION_PLAN.md` Phase 2 → 4. Sites are already named — no re-derivation.

### S1 — Client experience (closes R1–R3; shippable without backend)

1. **Create** `Assets/_Modules/Village/Buildings/DungeonSealedDoorPanel.cs`  
   Pattern = `JewelPolishConfirmPanel` (`BuildObsidianModal` + `PanelManager`). Headline/body only — no error chrome.

2. **Edit** `DungeonPortal.cs` (four surgical points from plan §2d):
   - Each proximity tick: `_door = DungeonStatusCatalog.For(_dungeonId)`
   - Interact: if open → `EnterDungeon`; else → sealed panel
   - Prompt text uses sealed headline when closed
   - First line of `EnterDungeon`: `if (!IsOpen) { ShowSealedDoor(); return; }`

3. **Add** `DungeonWorldPortalSpawner.ApplyDoorState(Portal, DungeonDoorInfo)`  
   Call from **`BuildPortal` and after `SwapInSharedStructureAsync` re-seat**.  
   Default closed look: strip threshold aura + circle (dark/inert). **Do not invent sigil art** until owner tags keys (`seal`/`rubble`/`water`).  
   → Case 5 stops PartialSkip the moment the symbol exists.

### S2 — Hygiene + prove without network (closes R4, R6)

4. Move kill switch to `FeatureFlags.DungeonStatus => Get("dungeonstatus", defaultOn: true)`; delete inline helper.  
5. **Create** `Assets/Editor/DungeonStatusDevMenu.cs` — write/delete `persistentDataPath/dungeon-status-cache.json` stub so a sealed door is provable headlessly / in-editor with **no** backend.

### S3 — Backend last (closes R5)

6. `api/dungeon-status.js` public GET + `dungeon_status` table + admin write. Client is correct after S1+S2 with cache stub alone.

### S4 — Owner before “felt done” (R7)

- Ratify or rewrite the four default copy pairs in `canon-strings.json`.  
- Rule `rescue` as own enum vs `collapsed` body variant.  
- Rule: sealed door still shows **name + depth**? (recommend: yes — teases world, does not hide the door).

### Acceptance that flips PARTIAL → DONE

- [ ] Sealed stub visibly changes door + dialogue; open still enters  
- [ ] Mid-run status flip never kicks an active delve  
- [ ] Case 5 `[door-appearance]` asserts (no PartialSkip)  
- [ ] Dev menu can flip without rebuild  
- [ ] Backend optional for first ship; required for remote ops  

**Do not** hide portals or gate in `SceneRouter`. **Do not** mark DONE on data-only.

---

## LANE PASS 2026-08-21 (edit-only agent) — R1–R6 CLOSED. Not gated, not committed.

Built to the owner's stated order: sealed panel + the four surgical `DungeonPortal` edits, then
`ApplyDoorState`, then the dev menu, then the backend last. Nothing here has been compiled — no
Unity, no `COMPILE_GATE_OK`, no `REGRESSION_OK`. Brace-balance + NUL check ran clean on every `.cs`.

| Hole | Now | Where |
|---|---|---|
| **R1** gating | **CLOSED** | `DungeonPortal.cs` — four edits, listed below. `EnterDungeon` is never handed to the interact button while the door is closed, and a backstop refuses it anyway. |
| **R2** dialogue | **CLOSED** | `Assets/_Modules/Village/Buildings/DungeonSealedDoorPanel.cs` **(NEW)** — `BuildObsidianModal` + the null-guard-and-destroy + `PanelManager` Register/NotifyOpened/NotifyClosed. Also the SINGLE owner of the per-status canon-copy fallback. |
| **R3** appearance | **CLOSED** | `DungeonWorldPortalSpawner.ApplyDoorState(Portal, DungeonDoorInfo)` — the one appearance owner. Case 5 `[door-appearance]` now ASSERTS instead of PartialSkipping. |
| **R4** flag home | **CLOSED** | `FeatureFlags.DungeonStatus => Get("dungeonstatus", defaultOn: true)`. The inlined PlayerPrefs copy in `DungeonStatusService` was **deleted in the same edit** — there is never a second authority. |
| **R5** backend | **WRITTEN** | `api/dungeon-status.js` **(NEW)**, `dungeon_status` table + all-open seed in `api/schema.sql`, probe row in `api/admin/db.js`. ⚠ NOT DEPLOYED — `vercel.json` disables git deploys, so someone must run `vercel --prod`, and the SQL must be run by hand in the Neon editor. |
| **R6** dev menu | **CLOSED** | `Assets/Editor/DungeonStatusDevMenu.cs` **(NEW)** — writes the REAL cache file, so a sealed door is demonstrable with no server. |
| **R7** owner §9 | **STILL OPEN** | unchanged, see below. |

### The four surgical `DungeonPortal` edits (nothing else in that file changed)
1. **Door state on the existing 0.15 s proximity tick** — `_door = DungeonStatusCatalog.For(_dungeonId)`.
   No second timer. A state change re-renders a showing prompt so a door that closes stops offering entry.
2. **The gate, at the interact registration.** Open → `Request(..., "Enter: " + name, EnterDungeon)`.
   Closed → `Request(..., DoorHeadline(), ShowSealedDoor)`. `EnterDungeon` is never handed over.
3. **`ShowPrompt()` prints the authored headline when closed.** The bubble stays legacy `TextMesh`
   ON PURPOSE — rewriting it in TMP would hard-fail `[ui-obsidian]`.
4. **The backstop, first statement of `EnterDungeon()`** — `if (!DungeonStatusCatalog.IsOpen(...))`
   warns with id/state/provenance, resets `_loading` (dead-latch discipline: a flip back to open must
   leave the portal live), shows the prose and returns. It runs before the scene-name resolution and
   long before `SceneRouter.GoDungeonScene`, so **no scene load is ever started**.

### `ApplyDoorState` — one owner, and where it is called
Open → the standard treatment stands (and is re-attached if a previous closed pass took it off).
Closed → threshold aura stopped, dark-star circle destroyed, procedural glow surfaces suppressed:
the portal reads **dark and inert**, which is already correct world-language for "this does not open".
The sigil seat is **measured** (`MeasurePortalBounds` → `OpeningCentre` → `OpeningTargetSize`) and
logged with numbers, but **no sigil art is invented** — an un-tagged key logs `FlowTrace.Once` and the
default treatment ships (owner tags the key, the CLI maps it verbatim).
Call sites: **`BuildPortal`**, **the `SwapInSharedStructureAsync` re-seat** (the easy one to forget —
without it the real art silently reverts the closed look), **`Discover`** (a sealed door must not bloom
open the moment the hero finds it), and a **0.5 s poll in `TickDiscovery`** — the payload lands
asynchronously and can arrive after the portals are built, so without the poll a flip would never reach
the world. All four go through the same single method; no second spawner, no rival visual path.

### What a player gets
Walk up to a closed dungeon: the prompt reads its authored line ("The way is barred.") instead of
"Enter". Tap it and the ordinary Obsidian frame opens with the dungeon's NAME and two sentences of
world prose in the parchment body palette — no red, no warning glyph, no dev vocabulary, and the frame's
usual labelled Close (never an X). Nothing loads. The door is dark and inert behind it.

### STILL OWNER-OWED (R7 / §9) — none of it blocks the code
1. **The eight canon copy values are UNRATIFIED** (§9.1). They ship marked as such in
   `canon-strings.json`'s own authoring note. Ratify or rewrite — data-only, no code, no schema bump.
2. **`rescue` vs `collapsed`** (§9.2) — still five states in the enum, per the plan's position.
3. **Name + depth on a sealed door** (§9.3) — the panel **shows the NAME** (plan's recommendation).
   **Depth is not shown** and is not surfaced anywhere in the game today. Owner's word still needed.
4. **Which dungeons are the "two real ones"** — every seed row is `open` on purpose; seeding the wrong
   pair would close finished content.

### Left for the committer / next gate
- `DataRegression.RunAll` registration line for `[dungeon-status]` (that file is lane-fenced) — §2i.
- `COMPILE_GATE_OK` + `REGRESSION_OK` + `DUNGEON_STATUS_OK`, and a `RunCaptureHeadless` screenshot of
  the sealed-door dialogue (compile-green never proves a panel looks right).
- Backend deploy: run `api/schema.sql` in Neon, then `vercel --prod`. AC-7 cannot be shown without it.
