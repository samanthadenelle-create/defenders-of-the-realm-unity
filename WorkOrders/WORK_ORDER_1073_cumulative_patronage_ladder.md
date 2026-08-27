# WORK ORDER 1073 — The Patronage ladder: cumulative lifetime support, visible status, zero combat stats

**Status:** IN PROGRESS - the SERVER half is built and oracle-proven (2026-08-27), now including the owner's PER-PATRON bespoke-monument ruling: `patronage_benefactors` (11 columns) + migration 0003, `api/_lib/patron-name.js`, `api/_lib/benefactors.js`, `GET /api/patronage/benefactors`, `POST /api/patronage/name`, `test/benefactors.test.js` 34 cases (40 mutations proven RED, 1 control GREEN). REMAINING: the Unity render - place the stand-in monument near the Heart and open the wall on interact - plus the Command Center assign screen (WO-1244) that calls the seam. $500 switches on when the stand-in renders. Architecture slice landed cb57b1a41; thresholds CONFIRMED at $50 / $150 / $500.
**Minted:** 2026-08-24 (UI seat), banner header bumped with the 1069–1074 block.
**Provenance:** the external review the owner ADOPTED 2026-08-24 (*"Create a Patronage system based
on cumulative support, with zero combat stats … Whales generally don't need 900,000 stone. They want
something that says: I have supported this world more than almost anybody else."*).

---

## 1. Why this is the missing whale mechanic (RCA of the gap)

After $49.99 there is nowhere to go — WO-1165 §4 showed the top rung actively deterring the
highest-intent buyer, and nothing in the lineup accumulates. Every purchase today is an island.
A **cumulative** ladder means every ordinary purchase (a $4.99 Ledger included) feeds the long-term
track, so a light spender who later becomes a heavy one is never "starting over" — and the whale's
destination is **visible status**, which the covenant permits without limit, rather than power,
which it forbids.

## 2. The system

- **Source of truth: server-side lifetime USD**, summed from `purchase_entitlements` per wallet
  (the table already records every settled purchase — no new bookkeeping, one aggregate query).
  The client renders; it never computes.
- **Thresholds and unlocks are DATA** (one authored table; illustrative from the adopted review —
  owner tunes): $25 profile frame · $50 banner · $100 exclusive settlement cosmetic · $250 animated
  Heart-of-Elarion cosmetic · $500 named patron monument + title · $1,000 rarest visual treatment.
- **Unlocks are cosmetic/status ONLY.** No resources, no crystals, no tempo, no slots — a Patronage
  tier that granted anything spendable would let the ladder compound with itself.
- Milestone unlocks are **granted, never purchased** — reaching the threshold flips the entitlement
  server-side; the client is told, and celebrates.
- **Not tradable, initially wallet-verifiable later** (adopted ruling: *"I would not make them
  freely tradable initially. Otherwise you're suddenly balancing a secondary market"*). No SPL mint,
  no transferability in v1; the wallet-attestation door stays open by keying everything to
  `BoundWallet`.

## 3. ⛔ Constraints

1. **Zero combat, zero tempo — pinned by oracle.** The WO-1165 §1 ruling survives on the ad-skip
   caps; Patronage must not add a third door. A regression asserts the Patronage unlock table
   contains no resource/currency/timer grant — the `battle_monthly.json:3` ZERO-COMBAT-POWER build
   gate pattern, applied here.
2. **Renders on the cosmetic rail or not at all.** Frames/banners/monuments depend on the cosmetic
   render work (WO-1176 §4 companion is the pathfinder; WO-1074 is the program). A threshold whose
   cosmetic cannot render yet is not authored yet — never a dead unlock.
3. The $500 "named patron monument" and WO-1070 §4's "named on the Heart" decision are **the same
   surface** — one owner ruling, one implementation, two tickets consume it.
4. Refund/chargeback semantics: an SPL transfer cannot reverse, so lifetime totals only ever grow —
   state this so nobody builds clawback logic for a rail that cannot claw back.

## 4. Where it surfaces (v1)

Profile screen (frame, title, total-agnostic tier emblem — show the TIER, never the dollar figure
publicly), the kingdom view (monument at $500+), leaderboard card chrome. The four-lane store
(WO-1165 §12) gives it the fourth lane: **👑 Patronage**, where the Vow and future prestige bundles
live alongside the milestone track's progress display.

## 5. Acceptance

- [ ] Lifetime total computed server-side from settled purchases; client displays only
- [ ] Every authored purchase (packs, ledgers, Vow) increments it — asserted across the catalog
- [ ] Unlock table contains zero spendable/tempo grants — oracle-enforced
- [ ] Tier entitlements survive reinstall (wallet-keyed) and are never client-grantable
- [ ] Public surfaces show tier, never dollars
- [ ] Owner sign-off recorded on thresholds + unlock list before implementation

---

## ⭐ OWNER RULING 2026-08-24

**Build the ARCHITECTURE now.** The server-side lifetime-USD aggregate, the data-driven threshold
table, the granted-not-purchased entitlement flip, and the cosmetic-only oracle are all approved and
implementable today. This ticket moves **SPEC → READY (architecture)**.

### Thresholds — ⚠ **TENTATIVE**, three tiers only

| Tier | Threshold (tentative) | Unlocks |
|---|---|---|
| **Patron** | **$50** | permanent Patron crest · profile border · banner component |
| **High Patron** | **$150** | exclusive kingdom decoration · animated heraldry · premium Heart aura |
| **Founder / Benefactor** | **$500** | permanent monument · player/house inscription · unique animated kingdom marker |

These **supersede** §2's illustrative six-rung list ($25/$50/$100/$250/$500/$1,000) as the shape to
build against. They are authored as **DATA** (§2 already requires it), so re-tuning a threshold is a
data edit, not a rebuild — which is precisely why "tentative" is safe to ship the architecture on.

### ⛔ NO WHALE LADDER ABOVE $500 — owner, verbatim:

> *"Do not design a $2,500 whale ladder before you know whether you have $500 whales."*

Higher tiers are authored **only after real $500 patrons exist** in `purchase_entitlements`. This is
an evidence gate, not a preference. ⚠ Do not pre-author placeholder rows above $500 — an unrendered
threshold is the dead unlock §3.2 forbids.

### The $500 monument — this is where "Named on the Heart" LANDS

§3.3 predicted that WO-1070 §4.2 and this tier are the same surface, and the owner ruled it that way:
the `packs.json` "Founders are named on the Heart" copy is **removed now** from the Vow, and the
capability re-appears here as the **$500 Patron Monument** with player/house inscription.

⭐ **OWNER'S SITING CONSTRAINT, BINDING ON THIS TIER:** the monument stands **NEAR the Heart and never
alters the Heart itself.** Verbatim: *"that protects your most important world object from becoming a
NASCAR hood covered in sponsor names."* No inscription on the Heart mesh, no per-patron decal, no name
list on the world tree; a **separate adjacent object**, bounded in scale and density however many
patrons accumulate.

### Still owed before implementation completes

- The unlock list above is the owner's sign-off for **v1**; §5's last acceptance box is satisfied for
  these three tiers and these three tiers only.
- §3.2 still governs: a tier whose cosmetic cannot render yet is **not authored yet**.

---

## OWNER RULING 2026-08-27 - THE BENEFACTORS OF THE REALM WALL

Owner, verbatim: *"we add a benefactors of the Realm wall and they get added to that, and every
kingdom can see it. and a custom monumnet."*

### Why this resolves the ticket's own weak point
Section 4 surfaced the ladder on a profile screen and the player's own kingdom view. A status reward
seen only by its owner is not status. WO-1175 stated the same law from the other side: *"a title is
a SOCIAL reward: it is worth exactly as many people as [can see it]."* A GLOBAL wall gives it an
audience, which is what makes the top rung worth reaching.

### The wall
- **A single, GLOBAL Benefactors of the Realm wall, visible from EVERY kingdom.** Not per-player,
  not per-realm.
- ⚠ **This makes it SERVER-BACKED, not a client cosmetic.** Cross-kingdom visibility cannot be
  satisfied by local state - it needs a benefactors table and a read endpoint in `api/` (which is in
  THIS repo). Do not attempt a local-only version; a wall only this player can see is the defect the
  ruling exists to fix.

### RULED: who is on it
**$500 Founders ONLY.**

| Tier | Threshold | Unlocks |
|---|---|---|
| Patron | $50 | crest, profile border, banner component |
| High Patron | $150 | kingdom decoration, animated heraldry, premium Heart aura |
| **Founder / Benefactor** | **$500** | **THE WALL** + a **CUSTOM MONUMENT** + inscription |

⛔ Do NOT list $50 or $150 on the wall. Scarcity is what makes a public list read as an honour
rather than a subscriber roster. Those tiers keep personal cosmetics; the wall is the top rung's
whole point.

### RULED: the name shown is a PLAYER-CHOSEN PATRON NAME
Stored beside the entitlement, separate from any account identity.
- ⛔ **NEVER the wallet address, NEVER an email, NEVER a real name.** The player is on a public list
  as a consequence of PAYING; they choose how they appear.
- Requires: a name field, a length cap, and a profanity/impersonation filter. It is **public and
  permanent** - refunds cannot claw back (section 3.4: an SPL transfer cannot reverse, so lifetime
  totals only ever grow), so a bad name is permanent too unless an edit path exists. Decide that
  explicitly rather than by omission.

### Still binding, unchanged
- Cosmetic/status ONLY - no resources, no crystals, no tempo, no slots. Pinned by oracle (section 3.1).
- Granted, never purchased - the server flips the entitlement and the client celebrates.
- Not tradable in v1; keyed to `BoundWallet` so the attestation door stays open.
- ⛔ NO tier above $500 until real $500 patrons exist in `purchase_entitlements`. Evidence gate.
- The $500 monument and WO-1070 section 4.2 "named on the Heart" are THE SAME SURFACE - one
  implementation, two tickets consume it.
- ⚠ Section 3.2 still binds: **a threshold whose cosmetic cannot render is not authored yet.** The
  wall and the monument must actually render before $500 is switched on.

⭐ **Money is real now** (mainnet sales and SKR are live as of 2026-08-27), so `purchase_entitlements`
will start carrying real lifetime totals. The evidence gate above is no longer hypothetical.


---

## IMPLEMENTATION NOTE 2026-08-27 - the server half of the wall, and what is deliberately NOT built

### Built (api/, this repo)

| Piece | File |
|---|---|
| `patronage_benefactors` table (wallet PK, tier CHECK pinned to founder, `patron_name` + generated `patron_name_ci` UNIQUE, `name_edits_used`, `granted_at`) | `api/schema.sql` section 17 |
| The migration that provisions it on the live database | `api/migrations/20260827_0003_patronage_benefactors.sql` |
| Patron-name policy: cap, charset, profanity + impersonation, wallet-resemblance, edit allowance | `api/_lib/patron-name.js` |
| Wall reads/writes, eligibility re-derived from settled purchases on every call | `api/_lib/benefactors.js` |
| `GET /api/patronage/benefactors` - PUBLIC, unauthenticated, one global list | `api/patronage/benefactors.js` |
| `POST /api/patronage/name` - wallet-signed; no body = read own status, body = set/edit the name | `api/patronage/name.js` |
| 24-case oracle, 26 mutations proven RED + 1 control proven GREEN | `test/benefactors.test.js` |

The landed `api/_lib/patronage.js` (cb57b1a41) is **built on, not re-implemented**: it keeps its exact
four exports and its own suite untouched; the lifetime aggregate exists in exactly one place, and a
test fails if a second `SUM(usd_anchor)` ever appears in the wall module.

### The patron name: cap, filter, and the EDIT PATH (decided, not omitted)

- **Cap 24 characters** (`PATRON_NAME_MAX_LEN`), min 3. Wider than a username's 16 because a house
  name is the point of the rung; narrow enough that one wall row is one line.
- **Charset** is ASCII letters/digits/space/apostrophe/hyphen/underscore, must begin and end on an
  alphanumeric, no punctuation runs. `@` is not in it, so an email shape cannot be typed; homoglyphs,
  zero-width joiners and RTL overrides are not expressible; padding to sort to the top of the wall is
  refused.
- **Filter** reuses the username denylist (same leetspeak/repeat normalisation) plus reserved
  impersonation tokens. `api/_lib/username-policy.js` gained ONE additive export
  (`PROFANITY_DENYLIST`) so there is not a second copy of the list to drift.
- **Never the address:** a name that is a 6+ character run of the caller's own wallet is refused.
- **EDIT PATH = a bounded allowance: 3 self-serve edits** (`MAX_PATRON_NAME_EDITS`), each re-running
  the whole gate. *Reasoning:* wall entry is permanent because an SPL transfer cannot reverse
  (section 3.4), so with NO edit path one typo is permanent public harm on a list the player reached
  by paying - the worst outcome available. With UNLIMITED edits the wall becomes a broadcast channel
  a moderator cannot keep up with, and a filtered name can be swapped back once attention moves on.
  Exhausting the allowance returns `PATRON_NAME_EDITS_EXHAUSTED`, a support decision, not a dead end.
  Re-submitting the identical name is a no-op and never burns an edit, so a retried request after a
  dropped response cannot cost one.

### Privacy shape

Membership is **opt-in by construction**: a row exists only once the player chooses a name, so a
founder who never chooses one is never published - choosing the name IS the consent. The public read
emits `{ ordinal, patronName, foundedOn }` and nothing else: no wallet, no email, no dollar figure,
and a founding DATE rather than a timestamp.

### The BESPOKE monument - per patron, not one mesh (owner ruling 2026-08-27)

Owner verbatim: *"being it will be a custom fbx i will work with them one on to create and then add
in game"*. The $500 rung is a **collaboration**, so there is no catalog row for it and there never
will be one shared mesh.

- **`monument_asset_id` lives on the PATRON'S ROW.** NULL means that patron is still on the shared
  stand-in `monument_founder_standin`. That is the ONLY spelling of "placeholder": a database CHECK
  forbids storing the stand-in id, so it can never be written two ways and drift.
- **Per patron, never a global phase.** Founder A can carry their real monument in the same wall read
  in which Founder B is still on the stand-in. The public row now carries `monumentAssetId` +
  `monumentIsBespoke`, and a regression asserts exactly that mixed state.
- The stand-in id is a **pinned literal** with a source-shape assertion, the same fix M03 earned for
  `FOUNDER_TIER_ID` - a placeholder read out of "the first entry" of any list would silently
  re-point every un-collaborated founder the day that list moved.

### The seam the Command Center (WO-1244) will call

`assignPatronMonument(sql, wallet, assetId, { verifyAssetPresent })` in `api/_lib/benefactors.js`.
Not built here, per instruction; this is the contract it consumes.

- `verifyAssetPresent(assetId) -> { present: boolean, source: string }` is a **REQUIRED argument**.
  Omitting it is a `TypeError`, not a default - because a default here would be a default to "ship it
  and hope". The console passes the probe that asks the shipped catalog/bucket, the same question
  `tools/r2-ship.ps1` answers with `R2_PARITY_OK`.
- `present !== true` (including `null`, `{}`, `'yes'`, `1`) **refuses and writes nothing**. A
  monument nobody can see is never recorded as assigned.
- Refuses the stand-in id, a malformed key (paths, extensions, capitals, >64 chars), and a wallet
  that is not on the wall - the last WITHOUT probing the bucket.
- On success it stamps `monument_verified_at` so the proof has a DATE.

### How the R2 push is made impossible to forget (section 16)

The one-time check at assignment is necessary and **not sufficient**: bundle names are
content-hashed, so the next content build re-hashes every bundle and invalidates every earlier proof
at once. `monumentsNeedingRepush(sql, contentBuildIso)` returns the asset ids whose proof predates a
given content build - the list still needing a push - and **throws** if the build stamp is missing
rather than quietly reporting "all current". It returns **asset ids only**, no wallet and no patron
name, so the ship chain can print the answer without printing an identity.

### Still deferred, and why

- **The Unity render.** Server-side is done; the client is not. Needed: an addressable authored under
  the exact key `monument_founder_standin` (it does not exist yet) and pushed per section 16; the
  stand-in seated near the Heart via `HubStructureVisualInjector.Places[]` (runtime placer, no scene
  edit, and `HeartOfElarion` is at world (0,0,12), so "near the Heart, never ON it" is satisfiable);
  a code-built wall list on the `LeaderboardPanel.cs` pattern opened by interacting with the
  monument; and a first remote list source for the client, which has none today
  (`LocalStubLeaderboardSource` is the default). **$500 stays off until the stand-in renders** -
  section 3.2 is unchanged by the ruling, it is simply now satisfiable.
- **The Command Center assign screen** (WO-1244), by instruction. The seam above is its contract.

### Flags for the lead

- **`api/schema.sql` CHANGED** - it now declares `patronage_benefactors` (11 columns). Until migration 0003 is
  applied to the provisioned database, `node tools/schema-parity.mjs` will report
  `SCHEMA_PARITY_FAIL / TABLE MISSING: patronage_benefactors` and the push gate will refuse. That
  failure is correct. `CREATE TABLE IF NOT EXISTS` + `ON CONFLICT DO NOTHING` do **not** back-fill a
  provisioned database - verify by shape query, never by exit code.
- **Migration 0003 was AMENDED, not superseded.** The three monument columns were added to it before
  it had ever been applied to any database, so a 0004 would have been ceremony. It is guarded end to
  end (`ADD COLUMN IF NOT EXISTS`, constraint-existence checks), so re-running it is a no-op.
- **`api/schema.sql` also carries a SECOND uncommitted table from another seat**
  (`maintenance_toggles`, section 18, WO-1243). Two new tables, two migrations to sequence.

---

## OWNER RULING 2026-08-27 (c) - THE MONUMENT IS BESPOKE PER PATRON

Owner: *"being it will be a custom fbx i will work with them one on to create and then add in game"*
plus, on the placeholder question: **placeholder now, real art later.**

### This is NOT one shared monument mesh. Read that again before designing anything.
Each Founder's monument is a **CUSTOM FBX the owner creates WITH that patron, one-on-one**, and adds
to the game afterwards. The $500 reward is a COLLABORATION, not an unlock - which is what makes it
the top of the ladder and why it cannot be a catalog row like every other cosmetic.

### The two answers resolve together
Because the tier ships with a **placeholder monument**, the monument EXISTS from day one - so it is
the wall's door immediately, and the "where does the wall open from" question needs no separate
answer. Walking up to the monument and reading the names is the moment; a menu item is not.

### What this means for the build
1. **A PER-PATRON monument asset slot**, not a single shared visual. The wall row must be able to
   name that patron's own asset, defaulting to the placeholder until theirs exists.
2. **The placeholder is temporary PER PATRON**, not a global phase. Founder A may have their real
   monument while Founder B is still on the stand-in. Do not model this as one global flag.
3. **The owner needs a way to ASSIGN a monument asset to a patron.** That is an operator action and
   belongs on the Command Center console (WO-1244), not in a catalog file - she will do it as each
   collaboration finishes.
4. ⚠ **SECTION 16 APPLIES TO EVERY NEW MONUMENT.** Structure art is served from the R2 CDN with NO
   local fallback, and bundle names are CONTENT-HASHED. So **each custom monument is its own bundle
   and needs ITS OWN content build + push.** A monument that is authored but never pushed renders
   as nothing, with no error on screen - the exact failure that has already hit this project three
   times. Whatever assigns a monument must make that push impossible to forget.
5. The $500 tier switches **ON** with the placeholder. The server already records lifetime totals, so
   a patron who crosses $500 is credited and published immediately; their bespoke monument replaces
   the stand-in later with no data change.

⛔ Do NOT build one generic monument and call the tier done.
⛔ Do NOT model the placeholder as a global on/off - it is per patron.
