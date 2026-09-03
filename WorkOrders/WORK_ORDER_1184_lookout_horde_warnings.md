# WO-1184 — Earned lookout warnings: phone alerts + a LOOKOUT REPORT HUD surface

**Status:** READY TO IMPLEMENT - owner felt-test 2026-09-03 Needs Work - "right now its a red dot middle of screen, Have UI refine to maybe vibration and pulsing warning". Bounced from Fixed. PRIOR STATUS: FIXED 2026-08-27 — implemented; awaiting owner felt-verify to CLOSE.
**Silo:** HUD / notifications.
**Origin:** owner, ad hoc alongside batch 4 — *"i added that adhoc"*. ⚠ Minted **after** the code
landed, as a **ticket of record**: work with no work order is how a fix becomes invisible (two
unattributed fixes were flagged on the board the same day).

⛔ **This is NOT WO-1179 and does not advance it.** It is orthogonal presentation over
`SiegeScheduler`'s existing cadence. WO-1179's core — multi-side partitioning of one wave under one
shared concurrency budget — is **untouched**.

## What landed

- Phone warnings via the existing **Mobile Notifications 2.4.3** package (verified in
  `Packages/manifest.json`; the asmdef reference `Unity.Notifications.Unified` is the **real runtime
  assembly name**, confirmed against the resolved package).
- Tiered lead time: **15 / 30 / 60 minutes**.
- **Broad force-size intelligence gated to level-3 lookouts** — a reason to level a watchtower.
- **One replaceable notification**, cancelled on return, so no stale alert survives.
- A fixed top-centre **LOOKOUT REPORT** surface reusing the existing red `!` visual language.
- `SiegeScheduler.SiegeIntervalMs` (read-only) and `SiegeClock.TryGetDueIn` — presentation reads
  cadence; ⛔ it never writes it.

## ⭐ Why the copy is what it is — do not "improve" it

**Under the banked-pressure ruling nothing attacks while the player is away**, so the message says
*"Horde approaching. Expected at the town in {timing}. Return to defend live."* ⛔ It **never** claims
combat is occurring offline, because that would be **factually false**.
See `FOUNDATIONAL_RULINGS.md` §3 — including the hard fence that **a notification may never be paired
with a shield offer.**

## Review findings — neither blocks, both matter

### ⚠ 1. The HUD surface may not render on device — and this is PRE-EXISTING

`AlertIntelSystem` hosts its banner on `FindAnyObjectByType<UIDocument>()` — from commit `98131cb07`,
**not** this work. But **CLAUDE.md §8: *"UXML in builds: does NOT work"***, and **WO-1182 exists
precisely because a `UIDocument` surface renders blank on device.**

⭐ **UPGRADED FROM A DOUBT TO A RECOMMENDATION - the owner confirmed 2026-08-24: *"the alert for
raids, it was old legacy."***

That settles it. The new LOOKOUT REPORT is **back-patched onto a legacy path**, and onto **the exact
substrate WO-1182 exists to replace** - `UIDocument` surfaces that render blank on device. So this may
be **unreachable content**: written, compiled, gated, and invisible on the phone. ⛔ *Unreachable
content is not content.*

**RECOMMEND: move the LOOKOUT REPORT to the code-built Obsidian kit (uGUI)**, the same substrate every
other player-facing surface uses and the same move WO-1182 is making for the crafting modal. ⛔ Not
because the code is poor - it is clean - but because it sits on a substrate the project has already
decided does not ship.

⚠ **The notification half is UNAFFECTED and is the valuable half** - it needs no UI substrate at all.
⭐ **So this ticket can felt-verify as a SUCCESS even if the on-screen report never appears**, and the
two halves must be judged separately or a working feature reads as broken.

⚠ **Capture first, either way** (§12): confirm whether the report renders in a player build before
anyone rebuilds it. A rebuild on inference is the banned move even when the inference is good.

### ⚠ 2. `BestLookoutLevel()` matches towers by DISPLAY-NAME SUBSTRING

`tower.Data.towerName.IndexOf("Archer" / "Watchtower")`. ⛔ Display names are **not stable keys**, and
this repo already fixed keyword-matching **at the mechanism** once (`a698ec5ed`).

⚠ **CORRECTION 2026-08-24, by the lead, against my own finding.** I first wrote that WO-1163 makes
this fragile *right now*. **That overstated it:** WO-1163 renames `collector_farm` → Quarry and
`silo` → Stoneyard, and **touches no tower name at all**. There is no live collision today.

The finding still stands as a **pattern** rather than an emergency: a display name is not a stable
key, this repo already fixed keyword-matching at the mechanism once (`a698ec5ed`), and **the failure
mode is silent** — a level-0 lookout simply sends no
force-size intel. Match a catalog **id** or the **role enum** instead.

## Acceptance

- [ ] Owner felt-verifies a real warning on the Seeker, with correct lead time
- [ ] ⚠ A capture proves the LOOKOUT REPORT actually renders **in a player build**, not just the editor
- [ ] Level-3 gating proven — a level-2 lookout sends no force-size line
- [ ] Returning to the app cancels the pending notification (no stale alert)
- [ ] `BestLookoutLevel` keys off a stable identifier, not a display name
