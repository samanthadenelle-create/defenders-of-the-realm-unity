# WORK ORDER 1379 - Heartfire replaces "Raid Orders"

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Raid pacing / HUD charge display
**Type:** NEW GATE + naming, owner-ruled creative direction
**Minted:** 2026-09-04 (CLI)
**Source of truth:** `docs/CREATIVE_CANON_ELARION_2026-09-04.md` §4 - ⛔ points at that file, does not
restate it.

---

## §1. THE RULING

**"Raid Orders" is dead** - the player is the ruler, nobody is issuing them orders. **Heartfire** is
the Heart's ability to sustain an expedition beyond its own reach: three charges, one rekindles every
four hours, stacks to three so a player who sleeps or works is not punished.

The integer is unchanged from the economy map. What changes is what it says:

> Not *you may not raid because TIMER*, but **the Heart is not ready to send you back yet.**

## §2. ⛔ HEARTFIRE IS A CHARGE, NOT A CURRENCY

Never earned, traded, stored, gifted or bought. **Economy map §3 (do not add another currency) is not
violated and must not be read as licence to add one.** No wallet row, no `ResourceType` member, no
storage cap, no vendor. **If the implementation grows a balance, it is wrong.**

## §3. ⚠ THE THREE-GATE STACK - the real risk in this ticket

The conformance audit (2026-09-04) measured the current state: **the only raid clock is a
NON-STACKING per-camp lockout** - `scene-configs.json:107,170,225` = 14400 / 28800 / 43200s. That is
exactly the *"come back in exactly four hours"* shape the map's §5 exists to replace. The second gate
is the once-per-UTC-day crystal stamp (`RaidClaimService.CrystalsPaidToday`).

Under the new fiction each gate has a distinct, true reason, so all three can coexist. **The acceptance
criterion is behavioural:**

> ⛔ **A player holding Heartfire always has somewhere to spend it.**

⛔ **Do NOT shorten `raidCooldownSeconds` to make room.** That file's own authoring note explains at
length why those hours are not the lever, and shortening them defunds the timer ladder the game is
paced by.

## §4. HUD

Three flames around the Heart symbol; a spent charge is dark, with the rekindle timer beneath.
⛔ **Colour and icon treatment are the owner's creative call, not the implementer's**
(memory `owner-colorblind-delegate-visual-creative`) - build the states, and confirm the greyscale
check reads before claiming done.

⚠ Clocks are read from the **server-anchored `TimeSource`**, never the device clock.

## §5. ACCEPTANCE

- [ ] No player-facing string contains "Raid Order"; **"march" survives as the VERB** (canon §2)
- [ ] Charges: 3 max, 4h regen, stacking, `TimeSource`-anchored
- [ ] **A regression that FAILS if Heartfire ever gains a balance, a cap or a vendor** - proven RED first
- [ ] A regression proving the "somewhere to spend it" criterion against a saturated-cooldown fixture
- [ ] Registered in `DataRegression`; `REGRESSION_OK n/n suites` on a fresh log
