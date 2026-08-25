# BATCH_STATE — the live handoff. Read this FIRST, every time.

> ## ⛔⛔ PRECEDENCE RULE (added 2026-08-25, because this file was self-contradicting)
>
> **The NEWEST DATED SECTION WINS. Everything below the newest one is HISTORY, not instruction.**
>
> ⚠ Codex returned this as Batch 9 finding 6 and was right: the protocol line *"if a section says
> something, it is current"* held only while this file had one live block. It now has several, and
> older blocks still listed **WO-1173 as held for a spec pass** and **Batch 1/4/7 as ACTIVE** while the
> newest block said otherwise. A reader following the old rule would act on both.
>
> ⛔ **Batch 1, Batch 4, Batch 5, Batch 6, Batch 7 and Batch 8 are ALL HISTORICAL.** None is active.
> Batch 8 was WITHDRAWN. The live block is the newest dated one, currently **BATCH 9**.
> ⭐ Where an older section contradicts the newest, **the newest is right and the older is kept only
> for its reasoning** - the same rule CLAUDE.md section 15 applies to canon anchors.
>
> ## ⭐ OWNERSHIP — CORRECTED BY THE OWNER, 2026-08-25
>
> **CODEX WRITES THE CODE.** Owner, verbatim: *"codex can write it, as it writes clearer and more
> robust code than you."*
>
> **CLAUDE (the CLI lead) specs, routes, verifies, gates and commits.** It is the sole committer and
> the sole batchmode hands. It does NOT take implementation lanes for itself.
>
> ⚠ **This corrects a wrong turn worth recording.** Codex's Batch 9 handback reported the owner's
> standing rule as *"Claude owns all actions; Codex performs read-only inspection"* and refused all six
> lanes on that basis (finding 1). The lead accepted that reading and rewrote this section to match.
> **The owner has corrected it: that reading was wrong.** ⛔ Codex does not need to re-derive ownership
> from a remembered rule - it is stated here, and this file is the authority the protocol points at.
>
> ⭐ **Codex's other five findings (2-6) stand and have all been applied** - they were about SCOPE, not
> ownership, and every one was correct.


**Last written:** 2026-08-24 (later) by the CLI lead. ⭐ **NEW since you last saw it: WO-1177 is COMMITTED (`2c3ed6c24`) AND DEPLOYED, the migration RAN — so WO-1163 IS UNBLOCKED and has been waiting all night.** Batch 1 is fully closed; there are now **SEVEN file-disjoint seats free**. ⭐ **ALSO NEW: BATCH 7 — four panel tickets (WO-1075/1076/1077/1078) minted after the last state file went out; the lane has never seen them, and all four run IN PARALLEL.** All of it is in the blocks directly under the ACTIVE table — it supersedes the older WO-1177 correction beneath it.

> ## ⛔ THE PROTOCOL
> 1. **Read this file at the START of every batch, and again before starting any NEW ticket inside one.**
> 2. ⭐ **THE TWO SEATS DO DIFFERENT OPERATIONS — that is the safety, not just different regions.**
>    - **Dev lane: APPENDS to existing sections.** ⛔ Never creates one, never replaces one.
>    - **Lead: ADDS NEW SECTIONS, and replaces only sections it wrote.** ⛔ **NEVER replaces the file.**
>    ⚠ **This corrects an earlier version of this rule** that said *"replaced, never appended"* — taken
>    literally, a lead replacing the file would have **silently destroyed every dev-lane entry.** The
>    state above is refreshed **section by section**, never wholesale.
> 3. **If a section says something, it is current.** ⛔ **If it is not in ACTIVE, it is not in flight** —
>    a ticket's absence here is as load-bearing as its presence.
> 4. ⛔ **Anything in `CODEX_HANDOFF.md` that contradicts this file is HISTORY.** That document is layered and much of it is stale by design — it is the reasoning archive, not the state.
> 5. The lead updates this file **as batches move**. If it looks stale against what you see in the tree, ⚠ **say so rather than guessing** — a wrong state file is worse than none.
>
> ## ⭐ THE OWNER IS THE COURIER (2026-08-24) — neither seat reads the other directly
> **She carries this file between the CLI lead and the dev lane by hand.** ⚠ **So nothing written here
> reaches the other seat until she relays it.** A handback typed into this file is **not** delivered; a
> pin added above is **not** received. ⛔ **Never assume the other side has seen it.**
> - ⭐ **Write for a human to paste.** Keep the ACTIVE block short, concrete, and self-contained — it is
>   the part that actually travels.
> - ⚠ **Say what changed since you last saw it**, so she does not have to diff it in her head.
> - ⭐ Manual couriering also **serialises the two writers**, which makes the clobber risk below mostly
>   theoretical — the zones stay documented anyway, because the moment either side automates, it returns.
>
> ## ⭐ ONE FILE PER DIRECTION — there is no shared write at all
> **`BATCH_STATE.md` is OUTBOUND ONLY: lead → dev lane.** ⛔ **The dev lane never writes to this file.**
> Its results go into **`batch_results_state.md`**, which is INBOUND: dev lane → lead.
>
> ⭐ **That removes the clobber hazard by construction rather than by discipline** — a rule nobody has to
> remember cannot be forgotten. The earlier two-zone version of this section is superseded; it depended
> on both seats respecting a boundary inside one document, and boundaries inside a shared file are the
> thing this repo keeps losing edits to.
>
> ⚠ **Still true, and it is the part discipline cannot remove:** the owner carries both files by hand, so
> **nothing here reaches the dev lane until she relays it, and nothing in the result file reaches the lead
> until she brings it back.** ⛔ Never assume delivery.

---

## ⭐⭐ BATCH 9 REVISION 2 — DECISIONS, NOT QUESTIONS. CODEX IS UNBLOCKED. (2026-08-25 08:35)

**The lane is paused waiting on the lead. Every open question it raised is answered below.** Two rows
change, four are confirmed as-is. ⛔ Nothing here asks the lane for another intake review - proceed.

### 1. WO-1170 site 6 — ⛔ WITHDRAWN FROM BATCH 9. The lane was right and the ticket is wrong.

Codex asked: name the canonical source and choose CODEGEN, or authorize DELETE and define which flow
must refuse visibly. **Neither is available, and the lead verified why rather than picking one:**

- ⛔ **CODEGEN has no input.** `Assets/Resources/Data/Canonical/` contains `enemies.json` and
  `enemy-roles.json` and **NO per-enemy VFX catalog**. `EnemyTypeVfxLibrary` resolves
  family Resources asset -> default Resources asset -> synthesized instance. There is no JSON to
  generate a `.g.cs` FROM. Generating one would only RENAME a hand-authored fallback, which the lane
  already said and was right about.
- ⛔ **DELETE is unsafe as specified.** The synthesized rung supplies a non-null `EnemyTypeVfxSet` and
  **preserves telegraph timing**. Removing it turns a visible combat warning into a missing cue. That
  is a gameplay-safety change wearing a refactor's clothes, and it is not the lead's to authorize.

⭐ **So site 6 is MIS-SPECIFIED, not merely under-specified.** It is withdrawn and sent back for a spec
pass. It needs either an authored canonical VFX catalog (new content - the owner's), or a ruled
refusal boundary naming which enemy-spawn/combat flow must fail LOUDLY when the asset is absent.
⛔ **Do not attempt it. Do not return a partial.**

### 2. WO-1173 — RE-SCOPED. My "ONE ship-chain script" pin WAS the defect.

Codex found the trigger surfaces are SPLIT - device/store in the Android chain, production API/WebGL in
the deploy chain - so one script cannot demonstrate the ticket's requirement (d). **That is correct and
the pin was mine.**

**The grant is WIDENED** to every required trigger surface: `morning-ship-chain.ps1`,
`overnight-apk-build.ps1`, `distribute-android.ps1`, `overnight-webgl-deploy.ps1`, `.githooks/pre-push`
- take only the ones the ticket's trigger list actually requires and say which you took.

**And the lane is SPLIT, because two of its four acceptance items are not lane-completable:**
- ⭐ **IN SCOPE (do this):** wire `tools/schema-parity.mjs` into the trigger surfaces so it BLOCKS
  anything reaching a device or store, and create `api/migrations/` with the tracked migration.
- ⛔ **OUT OF SCOPE, owner/ops (do NOT attempt, do NOT block on):** `SCHEMA_PARITY_OK` against
  PRODUCTION, and the deliberately-narrowed-CHECK **RED** proof in a scratch DB. `DATABASE_URL` is
  redacted for every seat here including the lead. Those two are being tracked separately and the lane
  is NOT expected to close them.

⚠ **Which repair SQL is authoritative:** ⛔ NONE of the `tmp/neon-repair-*.sql` files. They are
untracked operational material from a one-off incident. **Author the tracked migration fresh in
`api/migrations/`; do not promote a `tmp/` file.**

### 3. WO-1170 site 2 — CONFIRMED, proceed and return it WHOLE.

The lane reserved `D:\eoa-codex-b9-1170s2` and declined to offer a partial because it owns the shared
parity surface. ⭐ **That judgement is correct and is now the instruction:** land it whole - generator,
`.g.cs`, all three hand mirrors removed, and the shared parity/oracle registration. ⛔ Sites 3 and 6 do
not touch `DataRegression.cs`; site 6 is withdrawn entirely, so the contention is reduced to sites 2
and 3, and **site 2 owns the registration.**

### ⭐ OWNER RULING 2026-08-25 — WO-1171 §4 GROWS. Read before building it.

> *"It doesn't ask me for any wallet. It doesn't give me any options. It defaults to Seeker, and I
> wanted to default to Seeker, but it'd also be nice to have options. So maybe giving them an option
> of which they want to connect - some kind of a multiconnect, whatever - or do they want to use the
> Solana. I think having that option is smart."*

⛔ **This CHANGES the lane. Do not build the narrow version.** WO-1171 §4 was scoped as "put the
existing connect/disconnect somewhere the player can reach it." The owner is asking for **a wallet
CHOICE**, with Seeker as the DEFAULT rather than the only outcome.

**Why the player never sees a choice today - two causes, both verified at source:**
1. `WalletSkinBootstrap.TryAutoResumeAsync` does a **SILENT reconnect at boot** whenever
   `MwaSessionStore.HasStoredSession`. The chooser is never reached because the decision was already
   made and sealed.
2. There is **no player-facing disconnect**. `WalletConnectDialog` is **UXML**, which does not render
   in player builds (that file's own comment concedes the real one "is code-built later"), and the
   only working disconnect sits in `AdminOverlay` behind `IsAuthorised()`, which matches the OWNER
   wallet - so it unlocks only for the wallet you would be trying to leave.

**⛔ THE TECHNICAL ENVELOPE - do not promise outside it.** The ONLY real wallet transport in this
product is **MWA on Android**, app-to-app, to an INSTALLED Android wallet app.
- ⛔ Browser extensions (Phantom extension, Jupiter, Soul Flare) are **unreachable on every platform**
  - MWA cannot see them and no web adapter exists.
- ⛔ Desktop EXE: `SOLANA_SDK` is **not defined for the platform** (`ProjectSettings.asset:767` lists
  it under Android only) - it falls to `StubWalletProvider`, a devnet mock that cannot sign.
- ⛔ WebGL: same stub; the SDK has no WebGL support.

⭐ **THE OWNER'S REASONING, recorded so nobody re-litigates it as a nice-to-have:**

> *"Just like me - who says that just because they have the Solana wallet, that's the one they want to
> use? Maybe they want to use a more robust Android wallet that's gonna be better."*

⛔ **The current behaviour encodes a WRONG INFERENCE: owning a Seeker does not mean wanting to transact
from its Seed Vault.** A player may well prefer a fuller-featured Android wallet - better UX, portfolio
view, swaps, multi-chain - and today the app silently decides for them and seals that decision. This is
not a preference feature; it is a defaulting bug with a preference-shaped fix.

⭐ **So "multiconnect" is achievable ONLY as: let MWA present its own chooser when more than one MWA
wallet app is installed, plus a player-facing way to switch.** That is real and worth building. A UI
that offers wallets we cannot reach would be a promise the transport cannot keep.

**What the lane should deliver (design spec is being written by the UI seat in parallel):**
- Seeker remains the DEFAULT - the owner was explicit.
- A player-facing **switch / disconnect** so the sealed session can be released without clearing app
  data. ⛔ Route via `CurrencySkinResolver`, never `WalletService` (asmdef).
- Auto-resume must not silently foreclose the choice for a player who wants to change.
  ⚠ It must still auto-resume by default - a returning player should NOT be re-prompted every launch;
  that is the behaviour canon already calls correct.

⚠ **Coordinate:** the UI seat has been sent the design brief for this. If the spec has not arrived when
the lane reaches this row, build the DISCONNECT/switch mechanism and its placement, and leave the
chooser presentation to the spec.

### ⭐⭐ WO-1171 §4 — RULED 2026-08-25: MAKE THE WALLET PREFERENCE CHAIN PLAYER-SELECTABLE

Owner ruling, verbatim: **"make the wallet preference chain player-selectable."**

⭐ **This is SMALLER than it sounds, because the chain was already built as data.**
`Assets/_Modules/Wallet/TargetedLocalAssociationScenario.cs:123` holds
`public static readonly string[] PreferredWalletPackages`, and its own header calls the chain
*"data, not logic."* Today it is fixed:

    1 com.solanamobile.wallet        Seeker / Seed Vault   <- RANK 1 (owner ruling 2026-08-05)
    2 app.phantom                    Phantom
    3 com.solflare.mobile            Solflare
    4 app.backpack.mobile.standalone Backpack
    5 ag.jup.jupiter.android         Jupiter               <- LAST

⚠ **Read that file's header before touching it.** It exists because the SDK's generic
`LocalAssociationScenario` fires an IMPLICIT intent and Android picked the winner - on the owner's
Seeker that winner was **Jupiter, and the Seeker wallet was never offered**. This clone forces the
ranking. ⛔ Do not "simplify" it back to the SDK scenario; that reintroduces the bug it was written to
fix. `setPackage()` narrows DELIVERY ONLY - action, category, data URI and the websocket association
stay byte-identical, and the MWA identity check depends on that. Keep it that way.

**What to build:**
1. **A persisted player choice consulted BEFORE the chain.** Chosen package wins; the existing chain
   remains the fallback order. ⛔ **Store it in PlayerPrefs, NOT the save schema** - it is a
   device/wallet-app choice, not player progress, and it must not force a schema bump.
2. **Default stays SEEKER.** The 2026-08-05 ruling is not repealed - it is now a default rather than
   the only outcome. A player who never opens the picker sees exactly today's behaviour.
3. **Offer only wallets ACTUALLY INSTALLED.** The scenario already enumerates installed MWA handlers
   via `queryIntentActivities()` - reuse that. ⛔ Never list a wallet the player cannot pick; an option
   that fails on tap is worse than no option.
4. ⛔ **CHANGING THE WALLET MUST CLEAR THE SEALED SESSION** (`MwaSessionStore`). Otherwise
   `TryAutoResumeAsync` silently reconnects the OLD wallet at next boot and the choice appears to do
   nothing. **This is the single most likely way to ship this feature broken.**
5. **Uninstalled-later must fall back, never hard-fail.** The file already states a missing preferred
   wallet is never a hard failure - preserve that for the player's stored choice too.

### ⛔⛔ THE HAZARD NOBODY HAS NAMED, AND IT MUST BE ANSWERED BEFORE THIS SHIPS

**`GameState.BoundWallet` IS THE SAVE KEY.** Verified at source:
`SolanaWalletProvider.cs:437` - *"GameState.BoundWallet is the save key"*;
`GameStateService.cs:1809` warns about a **"wrong-key write"** when BoundWallet is not what you think.

⛔ **So switching wallets switches the player's SAVE IDENTITY.** A player who taps a different wallet
to "see their options" can land in what looks like a **brand-new empty town**, with their real town
intact but keyed to the wallet they just left. That is a support catastrophe wearing a settings toggle.

⚠ **This is not hypothetical.** `MwaSessionStore.cs:7` records the owner hitting the neighbouring case
already - *"Her save DID come back (GameState.BoundWallet is persisted...)"* - i.e. identity/save
coupling has already surprised someone once.

### ⭐ RULED BY THE OWNER 2026-08-25: OPTION (a). No question remains on this lane.

**The picker states plainly, BEFORE switching, that a different wallet means a different saved
kingdom, and requires a deliberate confirm.**

⛔ **(b) is not chosen** - do not restrict WHERE the switch is offered as a substitute for saying what
it does.
⛔ **(c) is NOT AUTHORIZED.** Do not re-key the save to follow the player. That is a data migration on
a live build and remains the owner's decision alone. ⛔ Nothing in this lane may move, copy, merge or
re-key save data.

**What (a) means concretely:**
- The confirm names the CONSEQUENCE, not the mechanism. A player does not know what a "save key" is;
  they know what "your kingdom" is.
- ⛔ It must be a DELIBERATE confirm, not a toast and not an inline caption - the whole point is that
  the player cannot switch by accident and then find an empty town.
- The currently-bound wallet is **identified** in the picker, so the player can see which one holds the
  kingdom they are standing in.
- ⛔ Switching still clears the sealed `MwaSessionStore` session (pin 4 above) - the warning does not
  replace that, it accompanies it.
- ⭐ Nothing is destroyed either way. The old kingdom remains keyed to the old wallet and returns when
  that wallet is reselected. **Say that in the copy** - it turns a frightening dialog into an
  understandable one, and it is TRUE.

⚠ **Exact wording is still the OWNER's.** Propose it in the handback; do not treat your draft as
settled. ⛔ But do NOT block on it - build the flow with proposed copy and flag the strings.

### 4. Site 3, PROD-014(b), WO-1171 §4 — UNCHANGED and unblocked.

No question was raised on these three and no return has arrived. Their pins in the Batch 9 table stand:
site 3 is small and mechanical; **PROD-014 is slice (b) ONLY** and its acceptance needs a headed
capture the LEAD will run (do not block on it - hand back the source work and say the capture is
owed); **WO-1171 writes `Settings/` only, `Wallet/` is READ-ONLY.**

### ⭐ Net: Batch 9 is FIVE lanes, not six. Four are executable now, one is re-scoped and executable.

⛔ **There is no Batch 10 to wait for.** It was drafted and is one row; it has not been couriered and is
not blocking anything. Finish what is executable in Batch 9 and hand back per lane - **partial returns
are welcome on every lane except site 2**, which owns a shared surface and must land whole.

---

## 🆕 BATCH 9 — SIX VERIFIED LANES FOR CODEX (2026-08-25, rescoped after the Batch 9 intake review)

> ⭐ **Every row below was verified AT SOURCE by a dedicated pipeline seat before it was written here** -
> the ticket's own status line quoted, the tree grepped for evidence the work is genuinely absent, and
> `git status` checked for a dirty-file collision. **That verification exists because Batch 8 was
> refused for skipping it.** The tree is CLEAN on every path below (HEAD is 49 ahead, nothing pushed).
>
> ⚠ **The stale statuses the pipeline seat flagged are now FIXED** - nine tickets that had shipped while
> still reading READY were corrected and committed. The board no longer advertises finished work.

| # | WO | The work | Files | The pin you must not miss |
|---|---|---|---|---|
| 1 | **WO-1173** | Wire the schema-parity gate into a chain + create `api/migrations/` | `tools/schema-parity.mjs` (READ ONLY), **ONE** ship-chain script, NEW `api/migrations/` | ⭐ **BLOCKS `MAINNET_SALES_ENABLED=true`.** Both open boxes confirmed absent. ⛔ **SCOPE WAS UNDERSTATED - corrected per Codex finding 2.** The ticket needs FOUR things, not one: (a) `SCHEMA_PARITY_OK` against PRODUCTION, (b) a deliberately narrowed CHECK / dropped column proven **RED** in a scratch DB, (c) **BLOCKING** pre-ship wiring for anything reaching a device or store, (d) execution after every production API deploy and schema edit. ⚠ One unnamed chain does not demonstrate those trigger surfaces. ⛔ **(a) and (b) need an AUTHORIZED DB EXECUTOR - this seat has none, and `DATABASE_URL` is redacted.** Name the exact chain and the verification authority before calling this scoped. |
| 2 | **WO-1170 site 2** | `BuildCategoryRegistry.BuildFallback()` retires 3 hand-mirrored tables | `Village/Catalog/BuildCategoryRegistry.cs` + a new generator + `.g.cs` | Highest-risk site - economy gating, the WO-1168 defect class. `:335` still reads *"Mirrors build-categories.json ... keep the two in sync."* Site 1 landed at `f6e306847`. ⛔ **Do NOT emit into `Assets/_Modules/Village/Buildings/Generated/`** - untracked dirt owned by another seat. Emit to a NEW folder. ⛔ **THIS SITE OWNS THE SHARED PARITY REGISTRATION** for all three 1170 lanes - see the correction below. |
| 3 | **WO-1170 site 3** | `StakeRewardsResolver.DefaultTiers()` retires | `Core/Platform/StakeRewardsResolver.cs` + generator + `.g.cs` | Small and mechanical, same landed pattern. `:219` `?? DefaultTiers()` and `:303` both still present. |
| 4 | **WO-1170 site 6** | Enemy per-type VFX hardcoded fallbacks retire | `Village/Enemies/Enemy.cs`, `Village/Enemies/EnemyTypeVfxLibrary.cs` | ⭐ Both files **document the defect in their own headers** (`EnemyTypeVfxLibrary.cs:17`, `Enemy.cs:1046`) - the evidence is in-tree, not inferred. |
| 5 | **PROD-014 slice (b)** | A refused repair needs an acknowledge / exit | `Village/Walls/WallRepairHudBridge.cs`, `WallRepairController.cs`, `HubRepairAffordance.cs` | ⛔ **SLICE (b) ONLY.** (c) and (d) are explicitly BLOCKED. Slice (a) landed `130ec84ab`. Clearing path exists: `WallRepairController.cs:486 CancelRepair()`. ⛔ **ACCEPTANCE NEEDS EYES, per Codex finding 5:** complete rendering at **2670x1200 AND the narrowest supported width**, with the PNGs OPENED. Compile + regression cannot close this lane. |
| 6 | **WO-1171 §4** | Player-facing home for wallet connect AND disconnect | `Settings/SettingsController.cs`, `Wallet/*` | The mechanism is FINISHED (`SolanaWalletProvider.Disconnect():394`, `StubWalletProvider.Disconnect():115`) and the host screen EXISTS - **placement is the whole job**. ⛔ **SCOPE NARROWED per Codex finding 4: `Wallet/*` is READ-ONLY.** Granting it could authorize edits to an already-finished mechanism. Write ONLY `Settings/SettingsController.cs` and call through `CurrencySkinResolver`. ⚠ If a genuinely missing seam is found in `Wallet/`, RECORD it and stop - do not widen silently. |

### ⛔ CORRECTION — "one file each" was FALSE (Codex finding 3, and it was right)

I wrote that the three WO-1170 sites are "one file each in three different modules." **They are not.**
Sites 2 and 3 each add a **generator PLUS a generated `.g.cs`**, and site 6 must first choose
**delete-vs-codegen** under the ticket's section 5 and may need a **hash-parity suite**. The ticket's
standing-oracle acceptance therefore creates a **SHARED registration surface** - three lanes editing
`DataRegression.cs` and a parity oracle at once is the collision this batch claimed to avoid.

**Binding split before any of the three starts:**
- Each site gets a **DISTINCT generator path and a DISTINCT `.g.cs` output path**, named up front.
- ⛔ **Site 2 is the SINGLE OWNER of any shared parity/registration surface.** Sites 3 and 6 do not
  touch `DataRegression.cs` or a shared parity oracle; if either needs registration, it goes through
  site 2 or lands after it.
- Site 6 records its **delete-vs-codegen decision** before writing anything - they are different
  tickets wearing one number.

⭐ The rest of the disjointness claim holds: 1173 is tools/api only, PROD-014(b) is Village/Walls,
1171 is Settings (with `Wallet/` read-only).

### ⛔ DO NOT RE-OFFER THESE — the pipeline seat rejected them with evidence

**Already landed today** (statuses now corrected): 1080, 1081, 814, 1179, 1186, 1188, 1189, 1190,
1191, 1193. · **Already FIXED days ago:** 1137, 1138 - ⛔ this is the exact pair Batch 8 was refused
over. · **Routed to the UI seat as design:** 1192, 1194, 1195. · **PROD-016** is a declared
duplicate-of-record for WO-1163. · **WO-1060's `UI_TOUCH_FAIL x43` premise is DEAD** - the tree now
reads `UI_TOUCH_OK 89/89`; it needs a re-measure before it is handable.

⚠ **WO-1170 site 5 could NOT be confirmed** - the ticket cites `OverworldEncounterSpawner.cs:925` as a
JSON seed mirror, but that region now holds a random pool pick. The line reference has drifted;
re-locate before anyone is sent at it. **Site 4 needs a spec pass** - its own comment says a true
single source needs an asmdef ref or a Core-side reader, which is an architecture decision.

### ⛔ THE LEAD'S OWN QUEUE — blocked on this seat, not on Codex

Named so they are not mistaken for available work: **WO-1152** (code FIXED at `f295971b6`, the re-baked
L1 prefab is on NO DEVICE until `tools
2-ship.ps1` runs) · **WO-1178** (handed back, at lead review,
uncommitted) · **the verify-dungeons fleet run** (needs a fresh gated build; the exe is 08-23) ·
**WO-1129** (needs Unity `AssetDatabase.MoveAsset` to preserve GUIDs) · **PROD-008** (verification only,
one controlled RED run) · **WO-970** (one approved line, acceptance is an owner-vetoed capture).

### ⚠ OWNER RULINGS OWED — still the binding constraint

Quest art scope · `MAINNET_SALES_ENABLED` from a non-owner wallet (⭐ **costs nothing - it is a quote,
not a purchase**) · the auth handshake · WO-1163's tier basket · whether guests reach the money path ·
WO-1082's one word on §3 · gold/crystals in the new resource readout.

---

## 🆕 BATCH 8 — READY TO HAND TO CODEX (written 2026-08-25 by the CLI lead)

> ⚠ **LEAD ADMISSION, so the lane knows why this file went quiet:** the lead ran today's work through
> in-process subagents and **did not update this file at all**. The batch board below was stale from
> yesterday's Batch 1/4/7 until now. That was a process failure on the lead's side, not the lane's.
> Everything in "ALREADY LANDED" was implemented and gated without passing through here.

### ✅ ALREADY LANDED 2026-08-25 — do NOT re-implement any of these

Gated `COMPILE_GATE_OK` + `REGRESSION_OK 276/276` + `UI_CAPTURE_OK 89` and committed in 9 commits.
⛔ **If a ticket below appears in your queue, it is done. Read the tree, not an older list.**

| WO | What landed |
|---|---|
| **1075 / 1077 / 1078** | FIXED and proven - zero touch findings on all three panels |
| **1076** | Root cause was the CAPTURE HARNESS re-authoring the panel, not the panel. Harness override deleted |
| **1186** | Palette chip row no longer covers the crystal readout |
| **1187** | All 14 non-ASCII `.ps1` converted + a registered encoding oracle |
| **814** | Gear max-level ability slots (empty rows, per-rarity, oracle bans a damage-multiplier field) |
| **1179** | Wave side-partitioning, ONE shared concurrency budget |
| **1188** | Post-purchase polling loop + measured receipt |
| **1080** | Capture provenance stamp - captures now record the commit they measured |
| **1189** | RumorBoard status band re-parented |
| **1190** | Store browsing no longer authenticates |
| **1193** | Marker ratchet rebuilt: emission vs mention, `HollowPassFixtures` exclusion DELETED |
| **1191** | Over-cap framing (⚠ 2 reds outstanding, see HELD) |

⭐ **Capture markers are now `UI_TOUCH_OK 89/89` and `UI_GEOMETRY_OK 89`.** ⛔ The `UI_TOUCH_FAIL x43`
baseline quoted in WO-1060/1075/1076/1077/1078 is **DEAD**. Do not compute a drop against it.

### ⛔ NOT FOR CODEX — routed to the UI seat as DESIGN work

**WO-1192** (Rumor Board redesign) · **WO-1194** (bank-full second surface + the collector readout
redesign) · **WO-1195** (a resource is named by its ICON, never a letter).
All three are owner-ruled design + mockup first. ⛔ Do not implement from the ticket text; the spec is
being written by the UI seat and the implementation lands after it.

### ⛔⛔ READ BEFORE ASSIGNING ANYTHING — THE TREE IS DIRTY AND HERE IS WHOSE IT IS

⭐ **Codex was right to refuse the table below until this existed.** The lead published an
"available" list while the main worktree carried unattributed edits. Every uncommitted file is
attributed here. HEAD is `acff13c5b`, **44 commits ahead of origin, nothing pushed.**

⚠ **`batch_results_state.md` correctly ends with yesterday's WO-1163 blocker. There is no Batch 8
return because Batch 8 never went out** — today's work ran through in-process subagents on the lead's
side and bypassed this loop entirely. That is the lead's process failure, stated so the lane is not
hunting for a handback that does not exist.

**UNCOMMITTED, gated `COMPILE_GATE_OK` but the regression is RED on WO-1191 (see HELD):**

| File | Lane | Status |
|---|---|---|
| `Assets/_Modules/Wallet/StorePackCard.cs` | WO-1190 card wiring | green, awaiting commit |
| `Assets/_Modules/Wallet/PackStore.cs` | WO-1190 card wiring + stale-comment fix | green, awaiting commit |
| `Assets/_Modules/Wallet/PurchaseQuoteService.cs` | WO-1190 browse-without-auth | green, awaiting commit |
| `api/purchases/quote.js` | WO-1190 public LIST mode | green, backend tests 30/30 |
| `Assets/Editor/Regression/RegressionMarkerRegression.cs` | WO-1193 ratchet rebuild | green, awaiting commit |
| `Assets/Editor/Regression/CaptureProvenanceRegression.cs` | WO-1193 (bare literal restored as the acceptance test) | green |
| `Assets/_Modules/Core/Economy/TownBankCapacity.cs` | WO-1191 over-cap framing | ⛔ lane is RED |
| `Assets/_Modules/Core/UI/BankOverflowToastPresenter.cs` | WO-1191 over-cap framing | ⛔ lane is RED |
| `Assets/Editor/Regression/WO1191OverCapIncomeRegression.cs` (+`.meta`, NEW) | WO-1191 oracle | ⛔ **owns the 2 reds** |
| `Assets/Editor/Regression/DataRegression.cs` | registrations for WO-1191 + WO-1193 | shared, lead-owned |
| `BATCH_STATE.md`, `BOARD.html`, `CLI_LANES_WO_NUMBERS.md`, `WorkOrders/1194`, `1195`, `QUEST_ILLUSTRATION_BRIEF.md` | lead bookkeeping | not code |

**NOT the lead's, and deliberately untouched all day** — pre-existing dirt from another seat, still
uncommitted: `ProjectSettings/ProjectSettings.asset`, `WorkOrders/WORK_ORDER_1081_*.md` (+78 lines),
`batch_results_state.md`, `READY_FOR_REVIEW.md`, `Assets/_Modules/Village/Buildings/Generated.meta`.
⛔ The lead will not commit these under its own message; whoever owns them should say so.

### ⛔ THE COLLISION RULE FOR BATCH 8

**Do not start lane 1 (`usdEffective`) or lane 2/5 (regression files) until the lead commits the
above.** Specifically:

- `api/purchases/quote.js` is **dirty right now** (WO-1190). Lane 1 edits the same file.
- `RegressionMarkerRegression.cs` and `DataRegression.cs` are **dirty right now** (WO-1193). Lanes 2
  and 5 edit that neighbourhood.
- ⛔ `WO1191OverCapIncomeRegression.cs` is being repaired by the seat that wrote it. **Do not touch.**

⭐ **Lanes 3 and 4 (WO-1137 fallback catalog, the first real `verify-dungeons.ps1` run) touch NONE of
the dirty files and are safe to assign immediately.**

The lead will re-post this section once the commit lands and the tree is clean.

### ⛔⛔ BATCH 8 IS WITHDRAWN — CODEX REFUSED ALL FIVE ROWS AND WAS RIGHT ON EVERY ONE

**Returned 2026-08-25 in `batch_results_state.md`. Nothing was started, nothing outbound was edited.**
⭐ **That refusal is the second time in two days the dev lane has caught the lead handing out work that
was already done.** Recorded here rather than quietly deleted, because the failure is the lesson.

| Row | What Codex found | Verified by the lead |
|---|---|---|
| **WO-1138** | already `FIXED — AWAITING OWNER FELT-TEST TO CLOSE` | ✅ confirmed at source. ⭐ **And it is the ratchet that went RED on WO-1191's new oracle in this morning's own gate run** - it has been working all along, which is itself the proof it landed |
| **WO-1137** | already `FIXED 2026-08-23 (84b9b987b)` | ✅ confirmed at source |
| **`usdEffective`** | owns `api/purchases/quote.js`, dirty in the shared worktree | ✅ correct - the lead named the collision and then listed the row anyway |
| **spaced `X OK`** | owns `RegressionMarkerRegression.cs`, dirty in the shared worktree | ✅ correct, same contradiction |
| **`verify-dungeons.ps1` fleet run** | the only Windows player is `Builds/Windows/DefendersOfTheRealm.exe` stamped **2026-08-23 14:50**, predating today's changes; the script's own contract says a stale player cannot prove today's composer output. Building now would snapshot incomplete cross-seat work | ✅ correct, and the sharpest of the five |

⛔ **ROOT CAUSE, stated so it is not repeated:** the lead carried WO-1137 and WO-1138 from the 08-21
anchor's OWED list and **never opened either ticket this session**. That is `RULES.md` rule 11 -
*never assert a fact you have not opened at source this session* - broken by the seat that quotes it.
A number carried in the head is the same defect as a number copied into a doc.

### ⛔ THERE IS NOTHING ASSIGNABLE RIGHT NOW, AND SAYING SO IS THE HONEST ANSWER

With the two stale rows removed and three blocked on the dirty tree, **Batch 8 is empty.** ⛔ The lead
will NOT pad it back out. The unblock is a sequence the lead owns, in this order:

1. **WO-1191's two oracle reds are fixed** (in flight with the seat that wrote it) - it discards a
   `TrySpend` bool, and two `!IsCapped` stand-downs land GREEN instead of emitting a Skip token.
2. **The lead commits the attributed tree** by explicit path, on a green `REGRESSION_OK`.
3. **A fresh gated Windows build** replaces the 08-23 exe.
4. **THEN** the dungeon fleet run, `usdEffective`, and the spaced-marker grammar are reassignable -
   the first two as a clean explicit-path handoff, the third with the intended grammar stated up front
   so the previously measured **24 prose collisions** are not rediscovered by trial and error.

⚠ **The spaced-marker grammar is owed by the LEAD, not discovered by the lane.** WO-1193's in-code
note records that the naive pattern collides with ordinary prose ("CATALOG OK", "GATE OK",
"LAYOUT OK") across six unrelated suites. Handing that out without a grammar would be asking the lane
to re-derive a measurement that already exists.

### ~~AVAILABLE NOW~~ — WITHDRAWN, see above

| # | WO | Files | The pin you must not miss |
|---|---|---|---|
| 1 | **`usdEffective` server field** (no ticket yet - lead owes one) | `api/_lib/purchase-catalog.js`, `api/purchases/quote.js` | The server ALREADY computes `quotedUsd` (`purchase-catalog.js:~338-359`), prices the SKR off it, then ships only the undiscounted `usd`. Add `usdEffective`, nullable like `usdAnchor` so the pinned canary stays null. ⛔ MIRROR LAW: `USD_ANCHORS` + both `packs.json` copies + the quote test's key list move together or the build is red. ⚠ Contends with WO-1163 - one seat. |
| 2 | **WO-1138** hollow-pass ratchet | `Assets/Editor/Regression/` | ⭐ THE LEVERAGED ONE. Its detection window is ~4 lines, so its coverage depends on code FORMATTING. Widen to a control-flow relationship, then re-run across every registered suite and triage what it surfaces. ⚠ WO-1193 just rebuilt the neighbouring marker ratchet - read it first, do not duplicate its literal-masking. |
| 3 | **WO-1137** fallback catalog | catalog fallback rows | 3 of 28 rows covered, drifted four times. Would hand the player a silent 3-row different game. |
| 4 | **`tools/verify-dungeons.ps1` has still never RUN** | `tools/` | It now PARSES (fixed 2026-08-25) - parsing is not running. Its first real execution is owed, and it needs a real fleet run, not a source read. |
| 5 | **The spaced `X OK` marker family** (no ticket yet - lead owes one) | `Assets/Editor/Regression/RegressionMarkerRegression.cs` | ⛔ Read WO-1193's in-code note FIRST: the obvious pattern produced **24 collisions on this tree, every one ordinary prose** ("CATALOG OK", "GATE OK", "LAYOUT OK"). A naive regex here is worse than nothing. |

### ⏸ HELD

- **WO-1191** - 2 reds outstanding in its own new oracle (`[tryspend-honoured]` discards a `TrySpend`
  bool; two `!IsCapped` stand-downs land GREEN instead of emitting a Skip token). Being fixed by the
  seat that wrote it. ⛔ Do not touch `WO1191OverCapIncomeRegression.cs`.
- **WO-1163** - still BLOCKED on the owner's tier-basket ruling (needs exact Wood/Stone/Iron/Gold
  amounts, or a deterministic conversion rule). Unchanged since 2026-08-24.
- **WO-1173** - blocks `MAINNET_SALES_ENABLED=true`. Still wants a spec pass before assignment.

### ⚠ Owner rulings owed - these are the pipeline's binding constraint, not capacity

1. **Quest illustration art** - 24 quests, and there is NO art field on a quest at all. Per-quest or
   per-`type`? And what the plate shows when absent? (`QUEST_ILLUSTRATION_BRIEF.md`, repo root.)
2. **`MAINNET_SALES_ENABLED` is UNTESTED** - `walletAllowed` passes the owner's wallet BEFORE the
   switch is consulted, so her own purchases can never exercise it. Needs a LIST-mode quote from a
   non-owner wallet.
3. **The auth handshake** - fold into connect (recommended) vs leave as a second prompt.
4. **Guests still cannot buy** - browsing is now free but the binding quote still demands a proven
   wallet, contradicting `PurchaseGate.WalletRequiredAboveUsd = 4.99`. Closing it puts a guest
   identity on the money path. ⛔ Not a lead call.
5. **Gold and crystals in the new resource readout** - crystals are UNCAPPED so "current of capacity"
   is meaningless for them; gold's placement is unstated.

---

## ✅ ACTIVE — work on these now

| Lane | Tickets | Files | Notes |
|---|---|---|---|
| **Batch 1** (Codex) | **WO-1177** → **WO-1178** | `api/_lib/purchase-catalog.js`, `api/purchases/quote.js`, `test/purchases.quote.test.js` · then `tools/` | ⛔ **ONE SEAT, SEQUENTIAL.** WO-1069 is **DONE** — landed `6bb61a810`. |
| **Batch 4** (Codex) | **WO-1163** · **WO-917 Phase B** · **WO-1179 core** | catalog/data · `HudModelProducers.cs` + action-slot builder · `SmartEnemySpawner.cs`/`WaveManager.cs` | ⛔ **WO-1161 needs NO edit** — already fixed 08-23, both copies byte-identical. |
| **Batch 7 — PANELS** (new) | **WO-1075** · **WO-1076** · **WO-1077** · **WO-1078** | `Village/Hero/RaidDeployScreen.cs` · `Village/Hero/RumorBoardPanel.cs` · `Village/UI/EndState/EndStateView.cs` · `HUD/DialogueView.cs` | ⭐ **FOUR PARALLEL SEATS — one file each.** Minted AFTER the last state file went out; the lane has never seen them. Full detail in the Batch 7 section directly below. |

### 🆕 BATCH 7 — FOUR PANEL TICKETS THE DEV LANE HAS NEVER SEEN (minted 2026-08-24, UI seat)

⭐ **These four were minted AFTER the last state file travelled, so nothing about them has reached the
lane.** All four are **READY TO IMPLEMENT** (markers verified canonical at source, 2026-08-24), all four
are children of **WO-1060**, and all four target a **different file**.

⭐ **ALL FOUR CAN RUN IN PARALLEL — one file each, no shared surface.**

| WO | Panel | File it owns | Findings |
|---|---|---|---|
| **WO-1075** | `RaidDeployScreen` — deploy footer falls under the touch floor | `Assets/_Modules/Village/Hero/RaidDeployScreen.cs` | **4** SUB-TOUCH-FLOOR |
| **WO-1076** | `RumorBoardPanel` — the shared Close buries **Accept AND Track** | `Assets/_Modules/Village/Hero/RumorBoardPanel.cs` | **18** (4 overlap + 14 button-over-text) |
| **WO-1077** | `EndStateView` — full-panel tap-dismiss covers the **Repair All** CTA | `Assets/_Modules/Village/UI/EndState/EndStateView.cs` | **3** BUTTONS OVERLAP |
| **WO-1078** | `DialogueView` — `TapAdvance` covers **every option row** | `Assets/_Modules/HUD/DialogueView.cs` | **18** BUTTONS OVERLAP |

⭐ **Every measurement in these four came from `Builds/wo1060-capture.log`.** They are **captured data,
not inferred** — each ticket cites its own line range in that log (1075 → `:17596-17632`,
1076 → `:17128-17332`, 1077 → `:17344-17368`, 1078 → `:17380-17584`). ⛔ **Do not re-derive a number by
reading the source; read it off the log, and prove the fix from a FRESH capture.**

### ⛔ The pins on Batch 7

- ⚠ **WO-1076 — the Rumor Board region is `193.6 x 112`, NOT the rounded `194` that appears in WO-1060's
  prose.** The oracle emits **193.6**; the round-up is WO-1060's own ruling text and it is wrong.
  ⛔ **And there are TWO buried buttons, not one — `ObsBtn_Accept` AND `ObsBtn_Track`** (Track shares
  **139.2 x 112**). A fix that clears only Accept leaves the panel red.
- ⛔ **THE `LayoutOracle` ALLOW-LIST STAYS AT TWO IN ALL FOUR.** `TouchBaseline`
  (`Assets/Editor/UICaptureLaunch.cs:3771`) keeps exactly its two entries — **`ArmyMuster`** and
  **`EquipDrawer`**. ⛔ **Nobody extends it.** Owner ruling 2026-08-24 (batch 2, ruling 9):
  *"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*
  Adding a panel to it **fails the ticket**.
- ⛔ **WO-1075 shares its file with WO-823 Phase E** (`RaidDeployScreen.cs:477` / `:526`, the duplicate
  `_vm.DeployableCount` checks). **WO-1075 owns `BuildDeployBar` geometry ONLY** — different silo, same
  file. ⚠ If WO-823 Phase E is live, coordinate before touching anything outside `BuildDeployBar`.
- ⚠ **WO-1077 is DISPUTED at source.** `EndStateView.cs:720` documents the layering as deliberate
  (WO-672) and `:726` sends the catcher behind the CTA, so the raycast may already resolve correctly.
  **The oracle is geometric and cannot see sibling order.** ⛔ §12 applies: **prove from a capture which
  control receives the tap at the CTA centre** before deleting dismiss-anywhere behaviour.
- ⚠ **WO-1078's failure class has already shipped once** — `DialogueView.cs:289` records an F8 finding of
  exactly this shape, fixed with `SetAsLastSibling` on the **Close only**; the option rows were never
  addressed. ⛔ A z-order-only fix leaves all 18 findings red — **geometry must change.** ⛔ Leave the
  `:289` `SetAsLastSibling` alone; it fixed a real finding.

### ⛔⛔ WO-1077 AND WO-1078 BOTH OFFER A TOOL FIX. **IT IS A LEAD CALL. THE DEV LANE FIXES THE PANEL.**

Both tickets observe — correctly — that `LayoutOracle.cs:141` already excludes graphic-less buttons from
the **BUTTON OVER TEXT** assert (`if (!HasVisibleGraphic(b)) continue;`) but **not** from **BUTTONS
OVERLAP**, and that extending that one exclusion would close **all 3** of WO-1077's findings and **all
18** of WO-1078's — **21 of the 43 reds in a single edit.**

> ⛔ **That is a LEAD call about the TOOL, not a call about the game. The dev lane fixes THE PANEL, not
> the oracle.** Take **path (a)** — the geometry fix — on **both** tickets. It is the option that cannot
> weaken the gate.

⚠ **The argument against the tool fix is real, not procedural:** a fully transparent `Image` keeps
`raycastTarget` on, so a clear-image catcher **genuinely does steal taps**. Path (b) would make the
oracle blind to a defect that ships. ⛔ **And a tool-rule change is NOT an allow-list entry** — different
decision, different owner; it must not be smuggled in as one. If the lead ever does rule path (b), it is
**ONE edit coordinated across both tickets** — never two seats in `LayoutOracle.cs` at once.

### ⚠ NO ACCEPTANCE CRITERION MAY DEPEND ON HUE

**The owner is red/green colourblind.** ⛔ Never write, and never verify against, a check that turns on a
colour. Every criterion in these four is already a **pixel measurement, a finding count, or a raycast
identity** — keep it that way. ⭐ Judge by **position, size, geometry, or the greyscale check.**

### ⚠ ONE CONTRADICTION ACROSS THE FOUR, and it is arithmetic

Each ticket computes its own marker drop **from the same baseline of `UI_TOUCH_FAIL x43`** — 1075 says
43 → 39, 1076 says 43 → 25, 1078 says 43 → 25. ⛔ **Those cannot all hold at once if the tickets land
together.** ⭐ **Treat the per-panel finding count as the binding number** (4 / 18 / 3 / 18), and read the
repo-wide total off a **fresh** capture at the time your ticket lands, not off the number written in the
spec. ⚠ Say in the handback which baseline you actually measured against.

### ⭐⭐ 2026-08-24 (later) — **WO-1163 IS UNBLOCKED. IT HAS BEEN WAITING ALL NIGHT. START IT NOW.**

⛔ **This block supersedes the WO-1177 correction below it and the WO-1163 pin in "Pins that are
binding on active work."** Read this one first; the older text is kept only as reasoning.

**WO-1177 is ACCEPTED, COMMITTED (`2c3ed6c24`) and DEPLOYED TO PRODUCTION.** The migration **ran and
verified.** ⭐ **So the file lock that held WO-1163 is RELEASED.**

#### ⭐ WO-1163 — the food→stone ladder. The biggest seat, and newly free.

- ⭐ **WO-1163 now OWNS `api/_lib/purchase-catalog.js` and `test/purchases.quote.test.js`.** WO-1177 is
  **done with them.** The owner ruled the **food SKU ids DO rename** — the remap is **in scope**.
- ⚠ **THREE FILES MOVE TOGETHER OR IT IS A RED BUILD:** the server `USD_ANCHORS` table, **both**
  canonical `packs.json` copies, and the quote test's hardcoded resource-key list
  (`test/purchases.quote.test.js:132` holds `['wood','iron','food','crystals','coins']`).
  ⭐ **The mirror law proves them equal on every run** — ⛔ **a partial rename is not a staging step.**
- ⛔ **Frozen ids stay frozen:** `collector_farm` and `silo` are **live save keys**. Rename the
  **display**, never the id. ⭐ Display spelling is **Stoneyard**, **one word.**
- ⭐ **Its blast-radius table is being CORRECTED** by a parallel lead pass — it keyed on the wrong field
  and listed `building-tiers.json` at **zero refs when it holds 27**, the **largest sink in the game.**
  ⚠ **Do not plan against the old table.**
- ⚠ `blue_mine` (KayKit) is **recorded follow-up art**, ⛔ **not WO-1163 scope.** Do **not** solve the
  visual by editing the farm prefab — the real mine node is already provisioned.

#### ✅ BATCH 1 IS FULLY CLOSED

- **WO-1069** ✓ committed. **WO-1177** ✓ committed **and deployed**. **WO-1178** handed back, **at lead
  review**.
- ⭐ **Credit WO-1178's finding — it is the best of the night:** `install-apk-to-seeker.ps1:25`
  hardcoded **`6000.4.7f1`** — the exact downgrade that rewrote `ProjectVersion.txt` and cost a full
  Bee rebuild **plus two gate runs.** The lead's own spec had checked **six *build* scripts** (all
  correctly pinned) and **never thought to look in an *install* script.**
  ⚠ **Its verification is the right shape too:** it **INDUCED** failures with named exits — **9** for
  pin mismatch, **8** for missing marker — not a clean run.
- ⚠ **WO-1178 is NOT yet committed:** `run-unity-method.ps1` is in its diff and an **APK chain is
  currently executing** and calls it. ⛔ Replacing a script mid-run is how an inexplicable failure
  happens. **It commits the moment the build finishes.**

### ⭐ WORK AVAILABLE NOW — **SEVEN SEATS, all file-disjoint.** Start any of them immediately.

⛔ **Full detail for 5A–5E is already in the BATCH 5 section below — do NOT duplicate it, read it
there.** This is the release note plus the one pin that matters on each.

| # | WO | Now free | The pin you must not miss |
|---|---|---|---|
| **1** | **WO-1163** | The food→stone ladder | ⭐ **The biggest, and newly free.** See the pins directly above. |
| **2** | **5A WO-875** | Un-gate hero cast VFX already in the library | ⛔ **No new VFX authored.** WO-874's **three boss keys stay the OWNER's.** |
| **3** | **5B PROD-012 r2** | First-run **no-connection** screen + **Retry** | ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** — WO-917 owns it **and is committed.** Reuse only. |
| **4** | **5C WO-1171 §4** | Player-facing wallet **connect/disconnect** | ⛔ Route via **`CurrencySkinResolver`**, **never `WalletService`** (asmdef). |
| **5** | **5D WO-1129 §3.3** | Repoint **six editor tools** at the derived art path | Six `Assets/Editor/*.cs`; no runtime files. |
| **6** | **5E WO-814** | Per-rarity **gear ability** machinery | ⚠ **The ticket names `GearStatResolver`; NO SUCH FILE EXISTS — it is `GearProgression.cs`.** Ships with **empty ability rows**; identities are the **owner's**. |
| **7** | **WO-1179 core** | Side partitioning | ⭐ **WO-513 is a COMPOSER, NOT a prerequisite** (ruled). ⛔ **ONE `SpawnWave` call**, one **shared** concurrency budget, **1→2→4** side partition. ⛔ `Gate.ForceFieldCollapsed` is **not** the breach signal; do **not** touch WO-1026's flag-disabled ring detector. |

⚠ **PLUS Batch 6 (WO-1180 remainder) — ⛔ SOLE OCCUPANT of `WorkOrders/*.md`.** It cannot run alongside
anything that flips a Status line. **Assign it alone, or not at all right now.**

### ⚠ ONE STANDING ENVIRONMENT NOTE FOR THE LANE

The lead's **Unity gating is degraded** — a **commit-charge leak**, and **three regression aborts**.
⛔ **That blocks GATING, not IMPLEMENTATION.** ⭐ **Nothing in the seven above needs Unity to be
written**, and **handbacks queue normally.**

### ⭐⭐ READ THIS FIRST — 2026-08-24 lead correction. **WO-1177's CODE IS UNBLOCKED. START IT NOW.**

⛔ **The refusal — *"the discount code was deliberately not started because the binding instruction says
the production migration must run first"* — is a MISREAD OF A PIN THE LEAD WROTE BADLY.** The fault is
the lead's, not the lane's, and the correction never travelled. It travels now.

> ⭐ **WO-1177's code CAN BE WRITTEN NOW. "Migration first" is a DEPLOY ordering constraint, NOT a WRITE one.**
> **Why the constraint exists:** `/api/purchases/verify` runs **after** the transfer settles, so a schema
> fault is discovered **with the money already gone and no refund route on an SPL transfer.** That governs
> **when the code may be DEPLOYED** — it says nothing about when it may be **written**.

⛔ **And the lane could never have run that SQL anyway — it is not the lane's action to take.** The lane
itself proved `DATABASE_URL` is **redacted in `.env.local`**, and `vercel env run` returns the redacted
value too. ⭐ **Running the migration is the OWNER's action**, exactly as the `bug_reports` rebuild was.
So "migration first" could never have been a precondition the seat was able to satisfy — waiting on it
was waiting on something that was never going to arrive from that seat.

⭐ **THEREFORE: write WO-1177 against the migration's DECLARED SHAPE, and hand it back.**
- The shape is already authored and stable — read it at source, do not invent it:
  - `tmp/neon-migration-wo1177-discount.sql` (the unrun ALTER; idempotent `ADD COLUMN IF NOT EXISTS`)
  - `api/schema.sql:1016-1039` (the same two columns in the canonical schema)
- **`discount_bps INT`** — basis points off the USD anchor (2000 = 20%). **NULLABLE.**
- **`discount_reason TEXT`** — the **SERVER's** label, e.g. `'repair_shortfall'`. **NULLABLE.**
  ⛔ **Never the client's `reason` hint** — that is logged and never trusted; storing it would turn an
  audit column into a repetition of whatever the caller typed.
- ⚠ **NULLABLE is load-bearing, not laziness.** A `NOT NULL DEFAULT 0` makes "no discount" and "a
  zero-bps discount" indistinguishable in the ledger, which is the exact thing the column exists to
  prevent. Do not "tidy" it.
- ⭐ Discount is applied **inside `buildQuoteBody`, BEFORE `quoteAmount`**, so the client never sees a
  pre-discount number it could edit.

⛔ **The lead holds it UNMERGED until the owner reports the migration run.** That is the lead's problem
to carry, not the lane's — hand back working code and the ordering is honoured on the deploy side.

⚠ **Sequencing that still binds, unchanged:** **WO-1163 may not start until WO-1177 is handed back.**
The food SKU ids **do** rename, so WO-1163 now touches `api/_lib/purchase-catalog.js` and
`test/purchases.quote.test.js` — **WO-1177's files.** ⛔ **Three files move together under the MIRROR
LAW** (the server `USD_ANCHORS` table, both canonical `packs.json` copies, and the quote test's
hardcoded resource-key list) **or it is a red build, not a staging step.**

### ✅ ACCEPTED HANDBACKS — 2026-08-24. Nothing here needs rework.

- **WO-917 Phase B — ACCEPTED, at the lead's gate now.** One file (`HUD/Kit/HudKitController.cs`),
  braces **235/235**, NUL **0**, scope verified.
  ⭐ **Credit for the finding:** the ticket's stated cause was **stale** — combat empty medallions
  already stayed visible, and `SetEmptyMedallion` blanked the face. The lane corrected the **current**
  seam rather than the described one. ⭐ **That is the report shape that keeps earning its place**
  (handback points 3 + 4).
- **WO-978 5F — ACCEPTED, at the gate.** New `Assets/Editor/Regression/EconomyCreditReportingRegression.cs`
  + meta.
  ⭐ **Credit for the judgement:** the ticket said assert the literal `requested`; the **live** Population
  reporter says `request`. The lane pinned the **stable stem** instead of forcing cosmetic production
  churn to satisfy a spec typo. Correct call.
  ⚠ **Registration in `DataRegression.cs` is COMMITTER-FENCED and is OWED BY THE LEAD.** ⛔ It is an open
  **lead** task — **not** the lane's, and not a defect in the handback.
- **The clean-lane rework is ACCEPTED.** Both new lanes are correctly based on current shared head; the
  old dirty worktrees stay **preserved and untouched** pending explicit provenance review.

### ⭐ NEXT BATCH — FIVE SEATS FREE RIGHT NOW, all file-disjoint. Start any of them immediately.

⛔ **Full detail is already in the BATCH 5 section below — do NOT duplicate it, read it there.** This is
only the release note saying which rows are now **free to start**, plus the pin that matters on each.

| # | WO | Now free — start immediately | The pin you must not miss |
|---|---|---|---|
| **5A** | **WO-875** | Un-gate hero cast VFX that already exist | ⛔ **No new VFX authored** — pure code-wiring. WO-874's **three boss keys stay the OWNER's.** |
| **5B** | **PROD-012 ruling 2** | First-run **no-connection** screen + **Retry** | ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** — WO-917 owns it, ⚠ **and it is at the gate right now, so that fence is LIVE.** Reuse only. |
| **5C** | **WO-1171 §4** | Player-facing wallet **connect/disconnect** | ⛔ Route via **`CurrencySkinResolver`**, **never `WalletService`** (asmdef boundary). |
| **5D** | **WO-1129 §3.3** | Repoint **six editor tools** at the derived art path | Six `Assets/Editor/*.cs`; no runtime files. |
| **5E** | **WO-814** | Per-rarity **gear ability** machinery | ⚠ **The ticket names `GearStatResolver`; NO SUCH FILE EXISTS — it is `GearProgression.cs`.** Ships with **empty ability rows**; the identities are the **owner's**. |

⭐ **PLUS: WO-1179 core is UNBLOCKED** — **WO-513 is a COMPOSER, not a prerequisite.** Already ruled (see
the R3 response below); the Ready audit's "stated prerequisite" was an **over-read** of a nice-to-have
line. ⭐ Take path (a): ship side partitioning without WO-513 behaviour, and state the limitation in the
handback.

⛔ **STILL BINDING ON EVERY SEAT:** leave `Assets/Editor/Regression/DataRegression.cs` alone — five
tickets want a registration line there and it is **committer-fenced.** Hand the lead the one-liner.


### ⛔ Pins that are binding on active work

- **WO-1163** — ⛔ **THIS PIN IS SUPERSEDED — see the 2026-08-24 (later) block under the ACTIVE table.**
  WO-1177 is committed + deployed, so **WO-1163 now OWNS `api/_lib/purchase-catalog.js` and
  `test/purchases.quote.test.js`** and the **SKU ids DO rename**. ⚠ The **MIRROR LAW still binds**:
  `USD_ANCHORS` + **both** `packs.json` copies + the quote test's resource-key list move **together**.
  ⛔ Frozen ids (`collector_farm`, `silo`) stay frozen — rename the **display** only. *(Kept for
  reasoning: it previously read "do NOT touch `purchase-catalog.js`; the food→stone SKU remap is a
  follow-up after batch 1 returns." Batch 1 has returned.)*
- **WO-1179** — ⛔ **ONE `SpawnWave` call.** Partition one wave's composition across active sides under **one shared concurrency budget**. Calling it per-side hands each call the full budget and doubles the field, defeating a cap that exists because of a phone frame-rate cliff. ⛔ `Gate.ForceFieldCollapsed` is **NOT** the breach signal (it also fires when the hero walks out of town), and ⛔ do not touch WO-1026's ring detector (behind a flag OFF since WO-579 — it records nothing, silently).
- **WO-1177** — ⚠ its migration is **written and unrun**: `tmp/neon-migration-wo1177-discount.sql`. ⛔ **It must run BEFORE the code deploys** — `/verify` runs after the transfer settles, so a schema fault there is found with the money already gone. ⭐ **DEPLOY ordering only — the CODE IS WRITTEN NOW.** See the correction block directly under the ACTIVE table; the migration is the **OWNER's** action and no lane can run it.

---

## ⛔⛔ RULED 2026-08-24 - THE FOOD SKU IDS **DO** RENAME. WO-1177 + WO-1163 ARE **ONE SEQUENTIAL SEAT**.

Owner: **yes**, the food SKU ids rename.

⛔ **THIS UN-PARALLELISES TWO LANES THAT ARE BOTH BEING WORKED RIGHT NOW.** WO-1163 must now touch
`api/_lib/purchase-catalog.js` (the `USD_ANCHORS` block holds the literals
`impulse-food-small|medium|large` under the **mirror law**) and `test/purchases.quote.test.js`
(`:132` hardcodes `['wood','iron','food','crystals','coins']`). **Those are WO-1177's files.**

⭐ **THE ORDER: WO-1177 first, complete and handed back. THEN WO-1163's SKU remap.**
⚠ The **earlier pin on WO-1163 is now SUPERSEDED** — it said *"do not touch `purchase-catalog.js`; the
food→stone SKU remap is a follow-up after batch 1 returns."* That is still the sequencing, but the
remap is now **in scope for WO-1163**, not a separate follow-up. ⛔ It just may not start until 1177 is
back.

⚠ **Three files move together or the mirror test fails:** the server `USD_ANCHORS` table, both
canonical `packs.json` copies, and the quote test's hardcoded resource-key list. ⭐ **The mirror law
proves them equal on every run** — a partial rename is a red build, not a staging step.

### ⭐ Owner has already recorded the art: `blue_mine` (KayKit)

The stone/mine node **has its asset already** — the owner recorded `blue_mine` from the KayKit pack.
⚠ **Later the collector becomes a proper MINE NODE**, not a re-skinned farm. ⛔ That is a **follow-up,
not WO-1163's scope** — 1163 renames display strings and remaps SKUs on frozen ids; swapping the world
node to a mine is its own change with its own capture.

⭐ Worth knowing now because it settles a question the ticket never asked: **the rename is not
permanently cosmetic.** Quarry/Stoneyard get real geometry eventually, so ⛔ **do not "solve" the
food→stone visual by editing the farm prefab** — that work is already provisioned.

---

## 🆕 BATCH 5 - SIX PARALLEL SEATS (composed 2026-08-24 from the corrected board)

⭐ **There IS more handable work now** - six seats, proven file-disjoint by listing paths. Not padding:
the Ready bucket is 18 and only 8 survive all five tests.

| # | WO | What | Files it owns |
|---|---|---|---|
| **5A** | **WO-875** | Un-gate hero cast VFX that already exist in the library | `Village/Hero/HeroAbilities.cs`, `Village/Vfx/SpellVfxFactory.cs` (read), `motion-castings.json` |
| **5B** | **PROD-012 r2** | Honest first-run "no connection" screen with Retry | `Core/UI/LoadingOverlay.cs`, `OfflineOptInPanel.cs`, `Core/Addressables/OfflineContentService.cs`, `canon-strings.json` ×2 |
| **5C** | **WO-1171 §4** | Player-facing home for wallet connect/disconnect | `Settings/SettingsController.cs`, `SettingsModel.cs`, `Core/Platform/PiSignInController.cs` |
| **5D** | **WO-1129 §3.3** | Repoint six editor tools at the derived art path | six `Assets/Editor/*.cs` |
| **5E** | **WO-814** | Per-rarity gear ability slot, locked line visible from Lv1 | `gear-levels.json` ×2, `Village/Hero/GearProgression.cs`, `EquipVM.cs`, `InventoryVM.cs`, `Village/Enemies/PlayerAttackController.cs` |
| **5F** | **WO-978 regression slice** | Lint pinning requested-vs-credited in the four callers | ONE new `Assets/Editor/Regression/*.cs` |

### ⛔ Pins on batch 5

- **5A** — ⛔ **no new VFX authored.** Pure code-wiring. `FOUNDATIONAL_RULINGS.md` §4 makes map-by-name the
  lead's call; **WO-874's three boss keys stay the OWNER's.**
- **5B** — ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** (WO-917 Phase B owns it). **Reuse only.**
- **5C** — ⛔ route through `CurrencySkinResolver`, **never `WalletService`** (asmdef boundary).
- **5E** — ⚠ **the ticket names `GearStatResolver` and NO SUCH FILE EXISTS.** It is `GearProgression.cs`.
  Ships with **empty ability rows** — the identities are the owner's.
- **5F** — ⚠ the ticket reads `BLOCKED`, but the block is on the §1/§6 **doc** reconciliation.
  ⭐ The owner's send-back says verbatim *"the regression slice is unaffected."*
- ⛔ **ALL SEATS: leave `Assets/Editor/Regression/DataRegression.cs` alone.** Five tickets want a
  registration line there; it is **committer-fenced**. Hand the lead the one-liner.

## 🆕 BATCH 6 - ONE SEQUENTIAL SEAT, sole occupant of `WorkOrders/*.md`

**WO-1180 remainder** — tighten `--check` so malformed markers and duplicate ids **fail** rather than
warn, then drain the 26 malformed / 32 fallback rows by hand.
⛔ **It edits `tools/board_build.py` AND dozens of status lines.** ⚠ Any other seat flipping a Status in
that window corrupts the before/after bucket counts the ticket demands. **Nothing else touches
`WorkOrders/*.md` while it runs.**

## ⛔ NEW HELD - do not assign

- **WO-1170 Site 2** — behind **WO-1163** (`build-categories.json` + `Village/Catalog/Generated/`).
- **WO-1173 + WO-1159 §5** — one seat, sequential, **after a spec pass**. Both edit the three root ship
  chain scripts and contend with **WO-1178** on `.githooks/pre-push`.
- **WO-1100** — ruled the lead's, but it is **prefab/material serialized editing**: Unity-bound, not a
  code seat, ⛔ cannot run concurrently with a bake.

---

## ⏸ HELD — do not start

| Ticket | Held behind | Why |
|---|---|---|
| **Batch 5** — WO-1164, WO-1071, WO-1070, WO-1073 | WO-1177 **and** WO-1163 | ⚠ `packs.json` has **seven claimants** across three batches. One seat, one queue. Several are also `SPEC` now, not READY. |
| **WO-1173**, **WO-1072** | WO-1177 | Share `api/schema.sql` / the anchor table. ⚠ WO-1072 is now `SPEC` — its curve and its impulse rungs contradict each other. |
| **WO-978** | An open investigation | ⛔ The owner found a contradiction: WO-1165 says crystals are **UNCAPPED**, and the ruling's example implied a cap. **Do not implement a crystal cap by implication.** |

## ⛔ QUARANTINED — do not touch, do not rebase, do not commit from

**`D:\eoa-codex-six`** — its branch is **156 commits stale**. The 68 "modified" files are the delta from a two-day-old commit; one scene alone shows **19,133 insertions / 15,133 deletions**. ⚠ **A commit from that worktree would revert two days of work, including scene files.** Remediation is an explicit cleanup action after provenance review — ⛔ **not** something a work lane does.

---

## ⭐ JUST LANDED — context you need

- **`api/` IS DEPLOYED TO PRODUCTION** — commit **`e2e07f1c0`**, deployment `dpl_Gvyu7vQxZwMyM73bp7WjXC7xgnQd`. `/api/purchases/quote`, `/api/auth/session`, `/api/bug-report`, `/api/admin/schema-shape` all respond (they were **404**). ⛔ **This was a ONE-TIME owner authorization, not standing deploy authority.**
- **WO-1180 + WO-1181 landed** — the board now requires an exact `**Status:**`, reports malformed markers, counts fallback-bucketed rows, and lints self-contradicting statuses. `BOARD_CHECK_OK 0 unlabeled, 0 status contradictions`.
- **~50 status lines corrected.** Ready fell **37 → 18**; Spec rose to 41. ⚠ **Many tickets that said READY were not handable** — several are now `SPEC` or `BLOCKED`.
- **Eleven owner rulings landed** (`OWNER_RULINGS_OWED_2.md`) — PROD-012, WO-823 (**3 of 10** troops, first raid only), WO-814, WO-1060 (**no waivers**), the VFX repair/map split, additive schema bumps, WO-1159 §5, WO-1169 §5–§7.
- **`bug_reports` accepted `report_id 1`** — the first bug report this game has ever recorded.

---

## ⚠ THE RULES THAT KEEP BITING

1. ⛔ **Judge by MARKER on a FRESH log, never the exit code.** On 2026-08-24 **six** false greens occurred across four systems — two gate runners exited 0 having done nothing, a wrapper said `NO LOG` while the gate passed, a grep counted the wrong failure token, and `CREATE TABLE IF NOT EXISTS` reported success three times while changing nothing.
2. ⛔ **The status flip belongs in the SAME COMMIT as the work** (CLAUDE.md §2). A deferred flip does not happen — WO-1069 sat advertising landed work as available for an hour.
3. ⚠ **"Already shipped" has been wrong in BOTH directions** — live work marked missing, missing work marked live, three times in one day. **Read the tree, not the status.**
4. ⭐ **Report what you could NOT find, and where the spec did not match the code.** That section has corrected more tickets today than the code in them.
5. ⛔ **Cite `FOUNDATIONAL_RULINGS.md`; never restate it.** A fact written twice is this repo's dominant failure mode.

---

## 📤 LEAD RESPONSE to `batch_results_state.md` (2026-08-24) - R1-R4 answered

⭐ **The refusal was CORRECT on all four, and it is accepted.** ⛔ Nothing from either worktree will be
committed as-is. Below is the missing context and the one ruling it asked for.

### R1 + R2 - ⭐ **you are right, and here is WHY the diffs look stale: THE LEAD ALREADY HARVESTED THEM**

Both lanes are dirty with work that is **already committed in the shared tree**, because the lead
copied it out **by explicit path** and committed it:
- `eoa-codex-batch4`'s four files = **WO-1069**, landed as **`6bb61a810`**.
- `eoa-codex-ready`'s Village/Siege files = **WO-1184**, landed as **`4f0a6cb05`**; its
  `tools/board_build.py` = **WO-1180 + WO-1181**, landed as **`eed0dbe94`**.

⚠ **So those diffs are not wrong work - they are SPENT work**, and committing them again would
duplicate landed changes and re-open the forbidden `purchase-catalog.js` edits inside the WO-1163 lane.
⭐ **Your instinct to refuse was exactly right on the evidence you had**, and the harvest is what the
evidence was missing. ⛔ **The lead should have told you the moment it harvested them** - a lane whose
work has been taken out from under it cannot tell "already landed" from "wrong", and that is the lead's
failure, not the lane's.

**Accepted, both send-backs.** ⭐ **Recreate BOTH lanes clean from the current shared head**, correctly
named, and ⛔ **preserve the old worktrees untouched until provenance is explicitly cleared** - your
condition, and it is the right one.

### R3 - ⭐ **RULED, and it is already recorded: WO-513 is NOT a prerequisite**

⛔ **WO-513 is a COMPOSER, not a blocker.** WO-1179 is *what arrives, from where, and against how many
gates*; WO-513 is *how a pack fights once it has arrived*. They compose - build 513 and 1179 inherits
it - but 1179 does not need it to ship.

⚠ **The "stated prerequisite" in that Ready audit is an OVER-READ of a nice-to-have line**, and the lead
disputed it when the audit landed. A note to that effect is already in WO-1179's ticket; it clearly did
not travel. ⭐ **Take path (a): WO-1179 may ship side partitioning without WO-513 behaviour**, with the
limitation stated in the handback.

### R4 - accepted, and the number moved

⛔ `D:\eoa-codex-six` stays quarantined. ⚠ You measured **180 commits behind**; the lead measured 156
earlier today. **It is drifting further with every commit** - which is the argument for provenance
recovery being scheduled rather than deferred indefinitely. ⛔ Still: no rebase, no cleanup, no commit,
no deletion from a work lane.

### ⚠ One correction to your own file, offered not imposed

Line 15 says WO-1069's suite reported **26/26**. ⭐ The lead's own run of both files reported **39/39**
(quote + verify together); 26 is the quote suite alone. Not a defect - just two different scopes, worth
reconciling so a later reader does not think a suite shrank.

### Rework priority - accepted as written

1. Clean **Batch 1** lane → WO-1177 (⛔ **migration first**), then WO-1178.
2. Clean **Batch 4** lane → WO-1163's allowed slice, WO-917 Phase B, **and WO-1179 core** (R3 now ruled).
3. **WO-1184 is already committed** - ⛔ do not route it again.
4. **Batch 5 stays held.**

---

## ⛔ WHEN WORK COMES BACK — the lead CONFIRMS, then ANNOTATES or COMMITS. The lead does NOT FIX.

**Owner ruling 2026-08-24.** Three outcomes on a handback, and only three:

| outcome | what the lead does |
|---|---|
| **Correct** | Verify at source → gate → **commit** by explicit path |
| **Wrong or incomplete** | ⭐ **ANNOTATE and SEND BACK.** ⛔ Do not repair it |
| **Refused with a reason** | Read the reason, rule on it or route it — a refusal is a completion |

### ⛔ TURN IT AROUND IMMEDIATELY, ALWAYS (owner directive 2026-08-24)

⭐ **A handback sitting unanswered means the dev lane is IDLE. Turnaround time IS throughput.**

⛔ **The lead does not batch responses, does not wait for a convenient moment, and does not hold a
handback while finishing something else.** Read it, answer it, hand it back — **the same turn it
arrives.**

⚠ **This costs more than the idle time.** A lane waiting on an answer starts guessing, or starts
something adjacent, and both produce work the lead then has to unpick. Today a lane refused two
worktrees on exactly the right instinct while missing one fact the lead had and had not passed on —
**a fast answer is also a correct one, because the context is still true when it lands.**

⭐ **If the answer needs a ruling from the owner, send back what IS decided immediately** and name the
one open item, rather than holding the whole response for it.

⛔ **The lead fixing a handback is the failure this rule closes.** It hides the defect from the seat that
made it, so the same mistake returns; it makes the lead the least-reviewed writer in the system; and it
means nobody ever learns which specs are unclear. ⚠ **It also happened repeatedly on 2026-08-24** — the
lead corrected a status flip, restored a file it had itself broken, and edited an oracle rather than
sending any of it back.

⭐ **An annotation is worth more than a fix.** It names *what* is wrong and *why*, so the next spec is
better. A silent repair teaches nobody and the lead's own error rate today was the highest of any seat.

⚠ **The one exception, and it is narrow:** the lead may touch a handback to **run the gate** — the Unity
lock and the commit are the lead's alone. ⛔ Gating is not fixing. If the gate fails, that is an
annotation, not a repair.

---

## 📥 RESULTS GO IN `batch_results_state.md` — ⛔ do not write them here

⭐ **The inbound file is `batch_results_state.md`.** The dev lane writes its handbacks there; the owner
carries it back to the lead. ⛔ **Nothing is written into `BATCH_STATE.md` by the dev lane.**

**One file per direction, so there is no shared write at all:**
- `BATCH_STATE.md` — **OUTBOUND**, lead → dev lane. The lead owns it.
- `batch_results_state.md` — **INBOUND**, dev lane → lead. The dev lane owns it.

⚠ **The owner carries both by hand, so neither side may assume delivery.** A handback written into
`batch_results_state.md` has not reached the lead until she brings it; a pin added here has not reached
the dev lane until she relays it.

**Each handback should say these five things — the third and fourth earn their place:**
1. **WO + what landed** — one line.
2. **Where it is** — worktree/branch, or "in the shared tree by explicit path".
3. ⛔ **What you did NOT do, and why** — a blocked slice, a dependency, a refused spec.
4. ⚠ **What you could NOT find, or where the spec did not match the code.**
5. **Verification run** — tests, counts, `node --check`. ⛔ **Never a gate, never a commit or push** —
   one Unity lock, one committer, both the lead's.

⭐ **Points 3 and 4 corrected more tickets today than the code in them did.**

⚠ **A REFUSAL IS A COMPLETION.** A ticket that turns out already-shipped, unimplementable as written, or
gated behind a ruling — **that is the handback**, and it is worth more than an implementation. Three
tickets today were called "already shipped" and were wrong in **both** directions.
---

## ⛔ WO-1199 - REVISION REQUIRED. The refusal path was proved; the success path cannot succeed. (2026-08-25, later - CLI lead)

⚠ **Precedence note:** this section is dated 2026-08-25 and is the newest in the file. Per the
precedence rule at the top, **the newest dated section wins** - it is appended at the END rather than
the top only because this file is append-only by section.

⛔ **This section is scoped to WO-1199 alone.** It changes nothing about Batch 9 or any other lane.

### What came back, and what it proved

`tools/command-centre.ps1` (branch `codex/wo1199`, 255 lines), covering WO-1199 steps 1-8, with a
handback proving a **deliberate missing-credential run**: it refused with a named step, a marker, a
log, a reason, and exit 20.

⭐ **That refusal work is correct as far as it goes, and the refusal messages are good ones.** No
blame in this section. But the refusal fires in the first few statements (`command-centre.ps1:98-105`),
which means **no step ever executed and no run log was ever written.** The proof covers lines 98-105
of a 255-line chain and nothing downstream.

### ⭐ This is `prove-the-success-path-not-just-the-refusal`, exactly

A prior guard in this repo shipped that **refused correctly, aborted every good run, and exited 0 the
whole time.** The failure mode is identical: failure-only acceptance certifies the one branch that was
tested and silently certifies nothing else.

⛔ **A refusal test is no longer sufficient evidence for this ticket.**

### What the audit found - full evidence in `tmp/wo1199_verify.md`

Read from the **installed Vercel CLI bundle on disk** (v56.4.0) and from **probed PowerShell
behaviour**, not from memory or docs. ⛔ No `vercel` command was executed and the script was never run.

**FOUR BLOCKERS - the script cannot succeed as written, and can report success for a release that
never went live:**

| # | Defect | Where |
|---|---|---|
| **B1** | `Invoke-Captured` discards everything after the first stderr line - `$ErrorActionPreference='Stop'` is in scope, so the first stderr record TERMINATES and the catch keeps one line | `command-centre.ps1:19,53,55-58`; call sites `:143 :151 :185 :218 :249` |
| **B2** | The Vercel CLI writes **all** human output to stderr, and `inspect` prints `Fetching deployment "..."` BEFORE the JSON - so B1 is **fatal, not cosmetic**: a fully credentialed correct run dies at step 4 with `INVALID_INSPECT_JSON` | `dist/chunks/chunk-OX7KI3LF.js:4674,4560-4566`; `dist/commands-bulk.js:40584` |
| **B3** | ⛔⛔ `vercel promote <preview> --yes` **REBUILDS**; it does not ship the inspected, byte-proven artifact. It POSTs a NEW deployment, prints `Successfully created new deployment...`, returns 0 **without waiting**. `Successfully` matches the step-6 regex, `STEP_6_OK` prints, step 7 probes the **OLD** production, gets its 200, chain prints `COMMAND_CENTRE_OK`. **A broken build passes the whole chain and nothing rolls back.** | `dist/commands-bulk.js:53433-53463`; corroborated by `OVERNIGHT_REPORT_2026-08-10.md:263` |
| **B4** | Step 5's byte-proof would fetch a Vercel **login page** - previews sit behind deployment protection and no bypass token is sent. A correct run refuses at `INDEX_HASH_MISMATCH` ⚠ **after a ~25-minute WebGL build** | `command-centre.ps1:196-209`; `OVERNIGHT_REPORT_2026-08-10.md:258-261` |

**ALSO FIX in the same pass (not blockers):**
- The step-6/8 promotion regex `(?i)(promoted|promotion.*completed|success)` matches
  `"Promotion has been queued ... completes successfully."`, which returns 0.
  ⛔ **Judge promotion by the OUTCOME, never by output prose.**
- Step 8 proves the command ran, not that the rollback took effect. Close it by **POLLING**
  `vercel inspect $productionHost` until `.id -eq $rollbackId`. ⭐ **Polling, not a single check** -
  the alias does not flip synchronously.
- Step labels are reused three ways (`step=5` for the token check, the WebGL build and the preview
  deploy; `step=3` twice). Marker text carries the meaning, so **note only** - but the number misleads
  during an incident.

### ⭐ CONFIRMED FINE - ⛔ do not re-churn, do not "improve"

- ⭐ **No failure path writes an OK token into the log it is judged on** - all four markers clear. Only
  residual: `tools/r2_sync.py:426` holds the OK token inside an argparse `help=` string, never written
  to a judged log.
- `.id` from `vercel inspect` IS the right value for `vercel promote` (it takes `url|deploymentId`) -
  **the rollback target is right; the verification is what is missing.**
- `auth_nonces` self-prunes per wallet before insert, so step 7 causes no unbounded growth; the proof
  wallet is the Solana system program, whose private key does not exist, so the nonce is unusable.
- **Judging by marker rather than exit code is CORRECT** (CLAUDE.md section 8). Keep it.
- ⛔ **The explicit UTF-16 decode of the R2 parity log is CORRECT** and was independently re-confirmed
  today - `Builds/r2-parity.log` really is UTF-16 and a plain grep returns zero hits.
  ⛔ **Do not "simplify" it.**
- ⚠ Steps 1-3 (the gate half) are **sound** and are out of scope for this revision. Everything wrong
  lives in the deploy half, steps 4-8.

### ⛔ ACCEPTANCE FOR THE REVISION - required in writing with the handback

1. **A test proving `Invoke-Captured` returns ALL output** from a process that writes multiple stderr
   lines then stdout. ⭐ **Provable locally with a synthetic process - no Vercel, no credentials** - so
   there is no excuse for leaving it unproven.
2. **Evidence that the promoted artifact is the SAME one that was byte-proven**, or an explicit design
   change to a flow where that is structurally guaranteed. ⚠ **Propose the mechanism** rather than
   waiting for it to be dictated - but B3 must become **structurally impossible, not merely detected.**
3. **Rollback verified by polling the alias to the expected id**, poll bounded, timeout a **REFUSAL**.
4. ⛔ **Name which acceptance items remain OPS-OWNED and cannot be closed by the dev lane.** WO-1199
   acceptance items **1, 2, 3 and 6** need the Unity gate, a real deploy + promote, and two induced
   live failures. ⭐ **Hand those back as a named slice with the executor split written down** - do not
   silently leave them open, and do not claim them.

⛔ **Do not harvest or commit the current `codex/wo1199` script.** The full note also lives at the end
of `WorkOrders/WORK_ORDER_1199_the_command_centre.md`; the file:line evidence is in
`tmp/wo1199_verify.md`.

---

## 📤 BATCH 11 - assignment set (2026-08-25, CLI lead)

⚠ **Precedence note:** this section is dated 2026-08-25 and is appended at the END because the file is
append-only by section. It supersedes nothing above it except where it names a lane explicitly.

### The standing split - stated once, binding on every lane below

⭐ **Codex writes the code in its own isolated worktree. The CLI lead verifies, gates, commits and
pushes.** ⛔ Codex does not gate. ⛔ Codex does not commit. ⛔ Codex does not flip a ticket status.
One Unity lock, one committer, both the lead's.

### ⭐ THE ACCEPTANCE RULE - carried forward from what WO-1199 just proved

⛔ **A REFUSAL TEST IS NOT ACCEPTANCE.** Every lane in this batch must prove the **SUCCESS path** -
the thing working, not merely the guard declining. Failure-only acceptance certifies the one branch
that was tested and silently certifies nothing else; that is
`prove-the-success-path-not-just-the-refusal`, and WO-1199 is the second time this repo has shipped it.

⛔ Where a lane **genuinely cannot** prove the success path from the dev seat - it needs the Unity
gate, a live deploy, a real DB, a headed capture, or an owner felt-verify - **name those items as
OPS-OWNED, in writing, as a slice with the executor split.** ⛔ Do not leave them implied, do not
leave them silently open, and do not claim them.

---

## THE FIVE LANES - in this priority order

### LANE 1 - WO-1199 revision

⛔ **Not restated here.** Read `## ⛔ WO-1199 - REVISION REQUIRED` earlier in **this same file**, and
the file:line evidence in `tmp/wo1199_verify.md`.

Two additions only:

- ⛔ **Do NOT harvest, build on, or extend the current `codex/wo1199` script.** Start from the
  revision note, not from that branch.
- ⭐ **Close B1 first.** `Invoke-Captured` losing everything after the first stderr line is provable
  **locally, with a synthetic multi-stderr-line process, no Vercel, no credentials.** It is the
  cheapest proof in the batch and B2 is fatal on top of it, so it is the wrong one to leave for last.

---

### LANE 2 - WO-1163, stone replaces food

⭐ **OWNER RULED 2026-08-25: RUN IT NOW.** This **overrides the lead's standing recommendation to
hold.** The lane is open.

Read `WorkOrders/WORK_ORDER_1163_resource_ladder_stone_and_tiered_costs.md`. It is `**Status:** READY`
and **fully ruled** - section 6 is answered (6.1 + 6.2 by the owner 2026-08-24, 6.3 on 08-23).
⛔ **Do not re-open its design.** Do not re-litigate the ladder, the vocabulary, the container
retirement or the perk map. They are decided.

**Binding constraints, all three non-negotiable:**

1. ⛔ **It is save-schema-adjacent on a LIVE build with an ACTIVATED pay path (WO-1159).** Follow the
   ticket's own **section 7 sequencing EXACTLY**, in order. ⛔ Do not reorder it, do not collapse
   steps, do not do the "obvious" parts first. This is not a lane to rush.
2. ⛔⛔ **THE MIRROR LAW BINDS.** The server `USD_ANCHORS`, **BOTH** canonical `packs.json` copies, and
   the quote test's hardcoded resource-key list move together as **ONE ATOMIC CHANGE** - or the build
   is red. Not three commits, not a follow-up, not "the test after".
3. ⭐ **The food SKU ids DO rename, and that remap is IN SCOPE here** - owner ruling 2026-08-24,
   already recorded in this file. It is not a follow-up ticket.

⚠ **This is the largest lane in the batch by a wide margin.** It may warrant its own worktree and its
own return, decoupled from the other four.

---

### LANE 3 - the `UtcDay` oracle

⭐ **This is not a coverage chore. Frame it correctly or the lane will under-build it.**

`Assets/_Modules/Core/UtcDay.cs` declares its own format a **SAVE CONTRACT** in-code (*"THE FORMAT IS
A SAVE CONTRACT ... Do not 'improve' it"*) and carries a **hand-maintained migration ledger** at lines
25-31 naming *"three remaining copies"*.

**At HEAD that ledger is wrong twice:**

- **The count is FIVE, not three.** `Wallet/BattlePassService.cs:525` **and `:533`;**
  `Wallet/MonthlyCardService.cs:92` **and `:239`;** `Village/Monetization/AdGateService.cs:131`.
  The ledger names one site per file; two of the three files have two.
- **The paths mislead.** BattlePassService and MonthlyCardService are both under
  **`Assets/_Modules/Wallet/`**, not the `Village/Monetization/` neighbourhood the ledger reads as.
  A seat following it looks in the wrong directory.

`grep -rl "UtcDay" Assets/Editor/Regression/` returns **0**.

⛔ **A ledger written INSIDE the very file that exists to END duplicated state has itself drifted -
because nothing asserts it.** Same failure class as CLAUDE.md section 2's stale WO number block and
section 5's retired dependency table.

**The lane:** an oracle that **pins the real call-site set** so the ledger cannot drift again.

⚠ **Verify all five sites at source before writing.** The lead is relaying a finding, not a source
read of her own. Full evidence and the scope fences are in `tmp/pipeline_candidates_b11.md`.

⛔ **Out of scope, per UtcDay's own header:** migrating any of the five call sites (*"live monetization
paths ... a drive-by edit to any of them is a structural refactor smuggled into player-facing work"*),
and the LOCAL-day variants in `Core/Quests/DailyQuests.cs` / `Village/Quests/DailyQuestGateBridge.cs`,
which are a different axis on purpose. Those go in the lint's allow-list, named.

⭐ **Correct the ledger comment (count + paths) in the same edit** - the suite is what makes it true
from then on.

---

### LANE 4 - the remaining missing-oracle lanes

⭐ **Full list with per-row evidence: `tmp/pipeline_candidates_b11.md`.** Take the rows from there;
they are screened at source, not inferred.

**What they have in common:** greenfield. ⛔ **No Unity run. No database. No endpoint. No owner ruling
needed to write them.** The **ONLY** shared surface across the whole set is the `DataRegression.cs`
registration line, which is ⛔ **COMMITTER-FENCED and OWED BY THE LEAD** - it is an open **lead** task,
not the lane's, and its absence is **not** a defect in the handback.

⭐ **Three of these rows are unchecked acceptance boxes on tickets ALREADY marked DONE or FIXED.**

⭐ **The sharpest is WO-774.** Its acceptance
(`WORK_ORDER_774_raid_loadout_deployring_naming.md:90`) describes **`RaidCopyRegression` in the present
indicative** - as an assertion that already exists and already runs. **No file of that name has ever
been in the tree.** Ticket status: `DONE - audit-verified as shipped`.

⚠ **Instruct Codex explicitly: "the ticket says it exists" is a CLAIM TO VERIFY, never evidence.**
Every one of these rows was found by disbelieving exactly that sentence.

---

### LANE 5 - WO-1195 build-screen cost strings

## ⛔ NOT YET ROUTABLE - THE LEAD OWES THE SPEC. ⛔ DO NOT START IT.

Recorded here so it is **visible and not forgotten**, not so it is picked up.

**Owner ruling 2026-08-25 (decided):** build costs render as the resource **CHIP / ICON followed by
the quantity**, replacing the letter-suffix form (`130I`, `400W`, `10C`).

⛔ **It cannot be handed out yet:** the lead must first name **which files carry those cost strings.**
A lane that goes hunting for them will find some and miss others, and a half-converted cost display is
worse than the letter suffixes.

⚠ **ALSO RECORDED AS UNRULED, pending a fresh capture:** whether the build palette card **keeps its
building image at all.** The owner floated removing it in favour of text-plus-costs; the lead's
counter is a **thumbnail-plus-text** card, on the grounds that the image is what makes a card
recognisable without reading it. ⛔ **Neither position is decided.** ⛔ Do not implement either.

---

## ⛔ NOT IN THIS BATCH - do not pick these up

- **WO-1197** - already returned in `codex/wo1197`. ⛔ The **lead** harvests it. Do not re-run it, do
  not extend it, do not rebase it.
- **WO-1170 site 4 AND site 5** - **both WITHDRAWN 2026-08-25.**
  - **Site 4:** `HeroCatalog` is **not a fallback**, so a generated file there would be a **THIRD
    copy** - the opposite of what WO-1170 exists to do.
  - **Site 5:** the cited line guards a deliberately **TUNABLE float**. Pinning it would freeze a knob
    that is meant to move.

---

## The standing return protocol - unchanged

- ⭐ Results go in **`batch_results_state.md`**. ⛔ **Never in this file.**
- **One isolated worktree per lane.**
- ⛔ **Nothing committed. Nothing promoted. No ticket status flipped.**

---

## ⛔ WO-1163 R2 - BOUNCE BACK. The lane's scope statement is ACCURATE; the TICKET'S LAW WAS WRONG. (2026-08-25, CLI lead)

Verified at source in `D:\eoa-codex-1163-r2`, branch `codex/wo1163-r2`, HEAD `a988358de`.
Full evidence: `tmp/wo1163_verify.md`. Nothing below is taken from the handback text.

### ⭐ THE FRAME - read this before the findings

**This is not a lane failure.** The handback is an HONEST PARTIAL and its statement of scope is
ACCURATE. All four surfaces the ticket NAMED do move atomically, the quote suite really is **31/31**,
and the lane explicitly did NOT claim the broader economy conversion. Credit that.

⭐ **The ticket's own MIRROR LAW was INCOMPLETE. It names FOUR surfaces; there are FIVE.**
The fifth is the **Unity client**, and the node suite ⛔ **cannot reach it by construction** - it reads
JSON and JS only. So a fully green backend suite proves **nothing** about it. The lane moved every
surface it was told about. **The instruction was wrong.** That correction is the point of this bounce.

---

### ⛔⛔ BLOCKER 1 - THE MONEY BUG. Lead with it.

`Assets/_Modules/Wallet/PackCatalog.cs:64` binds `[JsonProperty("food")]` only; `PackEconomy` has
**no `stone` key**. `packs.json` now authors `"stone"`, so Newtonsoft **silently drops it** (default
`MissingMemberHandling.Ignore`, at the `JsonConvert.DeserializeObject<PackCatalogData>` call,
`PackCatalog.cs:654`) and `PackStoreVM.cs:109` (`gFood = Mathf.Max(0, econ.Food)`) /
`PackStoreVM.cs:116` (`if (gWood > 0 || gIron > 0 || gFood > 0 || gCrystals > 0)`) grant **zero**.

⛔ **Consequence: all three renamed impulse SKUs - `impulse-stone-small` ($1.99),
`impulse-stone-medium` ($2.99), `impulse-stone-large` ($4.99) - would grant LITERALLY NOTHING.**
`impulse-stone-small` has nothing else in its basket, so `TryGrantResources` is not even called.
`impulse-stone-medium` is `shelfCurated:true` + `storeVisible:true` - a **live browsable shelf row**,
not shortfall-only. **Twelve further baskets** (`hearth-spark` 500, `starters-hand` 1,500,
`folks-hearth` 3,400, `patron-of-elarion` 7,000, `founders-vow` 17,500, `frostfall-bundle` 400, plus
three seasonal/echo rows) silently lose their stone line while still delivering wood/iron/crystals.

⚠ **This is a LIVE dApp Store listing with an ACTIVATED pay path (WO-1159).**

Same gap, same root cause, two more places:
- `PackCatalog.ImpulseAmount` (`PackCatalog.cs:281-289`, block `:275-290`) switches on
  `wood|iron|food|crystals` and returns **0** in `default:` for `"stone"` - so
  `ShortfallPackOffer.Amount` (`ShortfallPackOffer.cs:72`) and the offer copy at `:151` / `:165`
  advertise 0.
- `ShortfallPackOffer.cs:107` still maps `"food" -> "food"`; no impulse family answers a food-shaped
  shortfall, so the route dead-ends at the `:134` "no impulse-pack family" failure.

⭐ **Note the SHAPE for the lane:** the failure is **SILENT**. No exception, no log, no red test. A
dropped JSON key looks exactly like a correct parse. That is why it needs a **structural fix plus an
oracle**, not just a patch.

### ⛔ BLOCKER 2 - the tree goes RED

`Assets/Editor/Regression/ImpulsePackRegression.cs:67` still reads
`{ "wood", "iron", "food", "crystals" }`, and it loads the REAL canonical files off disk
(`:61` `PacksRelPath`, `:214-215`), so it sees the renamed data. This tree fails
`[single-key]` (`:329-333`) x3 - *"'impulse-stone-small' grants 'stone', which is not one of the four
harvestable resources money may buy"* - and `[family]` (`:481-500`) x3 - *"no 'small'/'medium'/'large'
pack for 'food'"*.

⛔ **The lane could not have seen this without a Unity run, which it correctly did not do.**
`ResourceKeys` is a sixth copy of the resource list and belongs in the same edit.

### ⚠ HIGH - no `legacySkus` aliases

The three SKUs were renamed with **no alias**, against `packs.json`'s OWN stated law at `:21`
(*"`sku` is a LIVE SAVE KEY ... renaming one without an alias silently orphans a paid entitlement and
re-offers the pack for sale"*) and against its own `lanternlight -> keepers-satchel` precedent in the
same file at `:79-80`. The three stone rows (`:669`, `:693`, `:724`) carry no `legacySkus` array at
all. ⛔ **A player mid-flow, or any stored reference to an old id, resolves to nothing.** Adding the
alias is one array per row and does not break the new test, whose negative assertions read `row.sku`.

---

### WHAT IS CONFIRMED GOOD - stated so it is not re-churned

- **All four named surfaces agree.** `USD_ANCHORS` (`api/_lib/purchase-catalog.js:103-105`); both
  `packs.json` copies **byte-identical at md5 `5e027102dda784d72032d26fb4fd6fde`** with **zero `food`
  tokens remaining (was 18)**; the test key list (`test/purchases.quote.test.js:133` -
  `['wood','iron','stone','crystals','coins']`).
- ⭐ **Surfaces 1/2/3 have a REAL pre-existing oracle from WO-1177.**
  `test/purchases.quote.test.js:105` asserts the two canonical copies are byte-identical
  (`assert.equal(resourceText, streamText, ...)`), and `:113-125` asserts `USD_ANCHORS` deep-equals
  the client catalog. Change one, it goes red. **That is a mechanism, not discipline** -
  ⛔ **do not weaken it.**
- ⚠ **But surface 4 (`keys` at `:133`) is a hardcoded list bound to nothing and FAILS OPEN** - a
  future rename that misses it leaves the test passing while it silently checks one fewer resource.
  Ask the lane to bind it.
- **Measured, not claimed:** quote suite **31/31**; full backend **57/57** in the worktree vs a
  **56/56** baseline in `D:\eoa` - the delta is **exactly the one new test**. ⚠ The worktree has
  **no `node_modules`**, so the suite needs `NODE_PATH` to run there at all (bare, it dies at
  `MODULE_NOT_FOUND: @neondatabase/serverless` before a single case executes).
- **Safety clean:** diff is **exactly 4 files**; `SaveSchema.cs:41` still `CurrentVersion = 38`;
  `api/purchases/verify.js` and `api/purchases/quote.js` untouched; no amount / rate / rounding
  change. **Hygiene clean** - no strays, `git diff --check` clean, no non-ASCII on any added line.

---

### WHAT THE LANE MUST DO - same commit

1. Add `stone` to `PackEconomy` + the JSON binding; fix `ImpulseAmount` and the shortfall key map.
2. Update `ImpulsePackRegression.ResourceKeys` to the new key set.
3. Add `legacySkus` aliases for all three renamed SKUs.
4. Bind the test's resource-key list so **surface 4 fails CLOSED**.
5. ⭐ **Add an oracle that reaches the FIFTH surface** - something that fails when `packs.json`
   authors a resource key the Unity client cannot bind. ⛔ **Without it, the next resource rename
   repeats this exactly.** ⚠ This one may need a Unity-side test; **if so, say so and hand that item
   back as ops-owned** rather than leaving it implied.
6. ⛔ **Still forbidden:** any save-schema change, any amount / rate / rounding / settlement change.

⚠ Also flagged, **owner's call, not the lane's**: the three renamed rows still wear food copy -
`packs.json:671-672` "Basket of Grain", `:698` "Grain Cart", `:726-727` "Harvest Wagon" - selling
stone under grain names, one of them on the browsable shelf. Naming player-facing copy is hers.

---

## 2026-08-25 - WO-1199 REVISION 2 verdict: NEAR PASS, two one-line fixes

Source: completed verification, `tmp/wo1199_verify2.md` (read-only both trees, no `vercel` command run,
script never executed; Vercel behaviour read from the installed bundle v56.4.0).

### ⭐ What the lane got RIGHT - said first, and without hedging

- **B1 FIXED, and load-bearing.** `Invoke-Captured` sets `$ErrorActionPreference = 'Continue'`
  function-locally (`command-centre.ps1:59-60`), shadowing the script-scope `'Stop'` at `:21`.
  ⭐ REPRODUCED under a `'Stop'` harness: all 5 stderr lines plus the trailing stdout JSON survived
  (`LINECOUNT=6`), native exit code propagated (`RC2=7`), outer preference intact
  (`OUTER_PREF_AFTER=Stop`). ⭐ And a NEGATIVE CONTROL - the same body WITHOUT that one line captured
  exactly 1 line. Credit the method explicitly: a negative control is what turns "it passed" into
  "the fix is why it passed."
- **B3 FIXED STRUCTURALLY**, which is what the acceptance demanded. `--skip-domain` is rejected unless
  the target is production (`dist/commands/deploy/index.js:1345-1348`) and sets
  `autoAssignCustomDomains = false` (`:1566`); `promoteByCreation` is gated on
  `deployment.target !== "production"` (`dist/commands-bulk.js:53430-53442`), so ⛔ the rebuild branch is
  UNREACHABLE for this artifact - not detected, unreachable. Control falls through to
  `POST /v10/projects/<id>/promote/<deploymentId>` (`:53467`), a re-alias not a build. The prose
  success regex is gone; success is an alias-ID poll (`Wait-ProductionDeployment`, `:80-99`, call at
  `:278`). The identifier was traced build -> proof -> promote (`:228-231` -> `:236-238` -> `:251` ->
  `:276` -> `:278`): the proven artifact and the promoted artifact are the same deployment.
- **B2 NEUTRALISED, not eliminated** - state the distinction honestly. The CLI still writes every human
  line to stderr (`dist/chunks/chunk-OX7KI3LF.js:4674`); the capture boundary stops it being fatal.
  One prose parse survives: the candidate URL regex (`:227-231`).
- **Polling FIXED.** Bounded at a 180s deadline (`-AliasTimeoutSec 180`, `:17`); timeout is a named
  refusal in step 6 (`ALIAS_POLL_TIMEOUT`, `:278-280`) and step 8 (`ROLLBACK_ALIAS_POLL_TIMEOUT`,
  exit 28, `:313`); and a successful rollback still exits non-zero
  (`POST_DEPLOY_DB_PROOF_FAILED_ROLLED_BACK`, 27, `:310-311`).
- **Ops handback CORRECT.** Items 1, 2, 3, 6 named and matching the WO acceptance list
  (`WORK_ORDER_1199_the_command_centre.md:249-259`); 4 and 5 correctly NOT claimed as ops.
- **Regressions all hold.** No OK token on a failure path into a judged log (`Write-Run` `:31-35` writes
  only to the never-judged `Builds/command-centre.log`); the `-Utf16` R2 decode preserved (`:179` ->
  `:118`); marker-not-exit-code preserved; ASCII 0, NUL 0, braces 55/55, parens 83/83, parse clean.
  ⭐ The verifier also probed the NEW UTF-16 exposure - `Tee-Object` writes the schema log and it is read
  WITHOUT `-Utf16` (`:186`) - and found `Tee-Object` emits a BOM (`FIRSTBYTES=FF FE 53 00 ...`) which
  .NET detects, so `PLAIN_MATCH=True`. ⛔ Recorded so nobody later "fixes" it into a bug.

### THE TWO FIXES REQUIRED - both one-liners

**FIX 1 - ⛔ BLOCKER. `vercel curl` will refuse on EVERY run.** The subcommand is real and genuinely
carries deployment-protection bypass (`curlCommand`, `dist/chunks/chunk-2KNVJ7ET.js:2589-2650`;
`getOrCreateDeploymentProtectionToken`, `dist/commands-bulk.js:15341-15360`, which even auto-creates the
secret). But it uses a bespoke arg parser - `parseCurlLikeArgs`, `dist/commands-bulk.js:15419-15476`,
whitelists `VC_STRING_FLAGS = {--deployment, --protection-bypass}` and
`VC_BOOLEAN_FLAGS = {--yes, --help, --trace, --json}` (`:15403-15404`) - which does NOT contain
`--no-color`, so that flag is forwarded to the real `curl` binary, which dies with
`curl: option --no-color: is unknown`, `EXIT=2`, no output file. ⭐ Proven by replaying the CLI's own
parser in node (`toolFlags = ["--no-color","--silent","--show-error","--output","<path>"]`) and then
probing the real curl binary. ⚠ Step 5 (`command-centre.ps1:251`) would refuse on every run, AFTER the
compile gate, the regression, R2 parity, schema parity and the ~25-minute WebGL build.
**Fix: drop `--no-color` from the `vercel curl` invocation** (it is valid on `deploy`/`inspect`/`promote`,
which go through `parseArguments`; `curl` is the one command that bypasses that merge).

**FIX 2 - ⚠ HIGH, a FALSE PASS.** `$remoteIndex` (`:243`) is never deleted before the fetch, and the
fetch's exit code is `| Out-Null`'d (`:250-253`); `:254` reads the file unconditionally. So a FAILED
fetch plus a stale byte-identical file from an earlier run **hashes itself** and prints
`STEP_5_OK marker=CANDIDATE_CONTENT_MATCH` for a deployment that was never contacted. ⛔ That is the
exact class this whole ticket exists to prevent - a green marker for something that did not happen.
**Fix: `Remove-Item $remoteIndex -Force -ErrorAction SilentlyContinue` before the fetch AND judge the
fetch's exit code.**

### ALSO NOTED - lane's judgement, not blocking

- MEDIUM: the candidate URL is still recovered from prose (`:227-231`,
  `'https://[a-z0-9-]+\.vercel\.app'` + `Select-Object -Last 1`), and that pattern can match the
  production alias. Safe today; harden by taking the URL from stdout only.
- MEDIUM: the step-6 poll timeout refuses WITHOUT rolling back, while a queued promotion may still land
  (the `202` path, `dist/commands-bulk.js:53473-53477`).
- LOW: `WEBGL_BUILD_OK` is declared (`:208`) but never checked (`:210-216` judge artefact + log
  freshness only); `vercel curl` silently creates a project-level bypass secret
  (`dist/commands-bulk.js:15355-15359`) - ⚠ worth the owner knowing; the new capture test is wired to no
  gate and its `-LibraryOnly` dot-source runs `:22-29` before the `:131` early return, truncating
  `Builds\command-centre.log`.

### Close

⛔ Still NOT harvestable, but the objection is now NARROW. Two fixes close the static half.
⛔ The success path remains ENTIRELY UNEXECUTED - ⭐ and FIX 1 is precisely the argument for why
acceptance items 1/2/3/6 still require the live ops run: a defect that only appears at runtime, on a step
that costs 25 minutes to reach, is exactly what a static audit cannot promise to catch twice.
