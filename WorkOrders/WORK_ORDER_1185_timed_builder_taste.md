# WO-1185 — A 1-day extra builder: the taste that differentiates hearth-spark

**Status:** SPEC — needs the owner's duration + the balance pass. **Silo:** Monetization/build queue.
**Origin:** owner, 2026-08-24: *"why not include a 1 day extra builder"* · *"make it different a
little in a way that wets their whistle."*

## Why this beats re-mixing resources

`hearth-spark` and `starters-hand` both sit at **$4.99**, and starters-hand **strictly dominates** it
— more of all five resources at the same price. Re-mixing the grants would fix that by making them
different *amounts on the same axes*, which works but stays a spreadsheet comparison.

⭐ **A timed builder is a DIFFERENT KIND of thing, so the two packs stop being comparable at all.**
Domination cannot occur between "bulk resources" and "a day of extra hands" — there is no axis to
compare them on. And it makes the choice a real fork: *build wide now, or build faster now.*

## ⭐ Why it is clean under `FOUNDATIONAL_RULINGS.md` §1 — and this is the load-bearing part

§1: **progression gates cannot be purchased; money accelerates the path, it never deletes it.**
The extra queue slot is **Echo-gated** (WO-911 Q6 — *each Echo above 2 unlocks the RIGHT to buy,
crystals complete it*), and this morning the owner ruled the **Founder's Vow grants crystals TOWARD
the slot, never the slot** precisely to respect that.

⭐ **A TASTE IS THE OPPOSITE OF A BYPASS.** A permanent slot would be buying past the gate. One day of
extra hands lets the player *feel* what a third builder is like and then **hands it back** — after
which the only way to keep it is the Echo path. It does not accelerate the gate; **it advertises it.**
That is a stronger §1 position than the Vow's crystals, which at least shortens the path.

⚠ **THE ONE CONDITION THAT KEEPS IT TRUE: it must not be stackable into permanence.** If a player can
hold thirty overlapping days, they have bought the slot in instalments. **Same shape as the shield's
non-stacking problem** — buying a second while one is live must extend by refusing or queueing, never
without bound, and ⛔ **a repeatable purchase of this must never exist.** A one-time starter-pack
inclusion is safe by construction; a standalone repeatable SKU is not, and should not be authored.

## ⛔ It is NEW MACHINERY, not a content change — budget for it honestly

Verified at HEAD: **no temporary or expiring slot concept exists anywhere.** Zero hits for
`tempSlot` / `slotExpires` / `builderUntil` / any equivalent. The model is
`BuildTimerService`: *"N concurrent ACTIVE slots (`BuildTimerConfig.freeBuildSlots` + purchased
slots)"* — **permanent only**.

What has to be built:
1. **An expiring slot** — a count plus an expiry, distinct from purchased-permanent slots.
   ⛔ Do NOT add it to the purchased count; that conflates temporary with permanent and the Echo gate
   reads the permanent number.
2. **Expiry while a job is running.** ⚠ **The design question nobody has answered:** what happens to
   the third job when the day ends? ⭐ **RECOMMEND: let the running job FINISH, and simply stop
   accepting a new third.** Cancelling mid-build to reclaim a slot would destroy paid progress and is
   the version players write reviews about.
3. **Persistence across sessions.** It is wall-clock time, so it must survive a quit — and it is
   save-adjacent. ⛔ Prefer an additive, default-on-read field; **a schema bump is the owner's call.**
4. ⚠ **It must be visible.** A silent extra slot is a purchase the player never notices. The queue
   UI has to show the temporary slot AND its remaining time, or the whole whistle-wetting intent —
   which is the entire point — fails silently.

## Open — owner's

1. **Duration.** "1 day" is the proposal; 24h from claim, or until a fixed daily reset?
2. **The rest of hearth-spark's grant.** With a builder day as its identity, its resource bundle can
   shrink — it no longer has to compete with starters-hand on bulk. Numbers are balance.
3. ⚠ **Does it belong in the starter pack at all, or is it its own thing?** As written it is a
   differentiator for a $4.99 entry pack, which is the safest home. ⛔ **Do NOT spin it into a
   standalone repeatable SKU** — see the stackability condition above.

## Acceptance (provisional)

- [ ] A temporary slot expires on wall-clock time and survives a quit
- [ ] ⛔ It is NOT counted as a purchased slot, and the Echo gate is unaffected by it
- [ ] A job running at expiry **completes**; only new work is refused
- [ ] The queue UI shows the temporary slot and its remaining time
- [ ] ⛔ A regression asserts the temporary count cannot be stacked or made permanent
- [ ] `hearth-spark` is no longer strictly dominated by `starters-hand` — and the widened
      anti-domination oracle proves it across **all** purchasable packs, not just impulse rungs

---

# ⭐⭐ 2026-08-24 — THE SYMMETRIC DESIGN, and the two halves cost VERY differently

Owner: *"one double offline harvester added other buider 24 added?"* — one 24-hour taste each, so the
two $4.99 packs differentiate on **capability** rather than on amounts of the same resources.

| $4.99 | its 24h taste | speaks to | build cost |
|---|---|---|---|
| **starters-hand** | **double offline harvest** | the away player — earn more while gone | ⭐ **nearly free, it exists** |
| **hearth-spark** | **extra builder** | the active player — build faster while here | ⛔ **new machinery** |

⭐ **Neither pack can dominate the other**, because each holds something the other has no axis to be
compared on. That is what makes this better than re-mixing resource amounts.

## ⭐ The harvest half is ALREADY BUILT — verified at source

`Assets/_Modules/Village/Monetization/HarvestBoostService.cs` (WO-1119):
- **`StandardMultiplier = 2.0f`** — literally "double".
- Already **TIMED**: `EndsAtUnixMs` (`:102`), `IsActive` (`:108`), `SecondsRemaining` (`:127`),
  `MultiplierNow` (`:115`).
- Already **ATTRIBUTED**: `Source` (`:139`) — so a pack-granted boost is distinguishable from a
  crystal-bought one in the data.
- Already **integrated into the offline path** — `OfflineHarvestService.cs:75` imports it, and the
  file's own header says *"The boost multiplies the RATE we integrate and NOTHING ELSE."*
- ⭐ **Already NON-STACKABLE: `MaxMultiplier = 2.0f`, clamped on READ as well as on write** so a
  bad write cannot exceed it. **The exact safety condition this ticket demands of the builder half is
  already enforced here.**

⭐ **So "24h double offline harvest" is a DURATION + SOURCE change on a working service.** Content,
not machinery.

⚠ **THE BALANCE RELATIONSHIP TO RULE FIRST:** the existing crystal purchase is
**`PurchasePriceCrystals = 120` for `PurchaseDurationSeconds = 4h`** (`:83`, `:86`). A $4.99 pack
granting **24 hours is SIX TIMES that duration**. ⛔ If the pack is dramatically better value it
**quietly retires the crystal sink** — and crystals are the one currency the economy needs sinks for
(WO-1165 §3). Pick the pack duration against that ratio deliberately, not by picking a round number.

## ⛔ The builder half is still entirely new — the asymmetry is the plan

Everything in the section above about expiring slots, persistence, mid-job expiry and queue UI stands.
⭐ **RECOMMEND SEQUENCING: ship the harvest taste on `starters-hand` first** (it is content), and
**build the builder taste for `hearth-spark` as its own slice.** Until it lands, `hearth-spark` stays
off-shelf rather than sitting there strictly dominated.

⚠ And note what the harvest half proves about the builder half: `HarvestBoostService` is timed,
attributed, capped and read-clamped. **That is the shape to copy** — not to reinvent.

---

# ⭐⭐ THE END-OF-WINDOW OFFER — and why the §1-safe version is also the BETTER RETENTION play

Owner, 2026-08-24: *"at the end of the window offer a one time special to upgrade to the full builder
with that discount that they already paid"* · *"its a starter pack. one time purchase"* ·
⭐ *"the idea is to get them to care enough to want to come back."*

**That last line settles the design**, and it settles it in the direction the ruling already pointed.

## ✅ "One-time purchase" resolves the stackability condition by construction

No repeatable SKU exists, so nobody buys the permanent slot in instalments. The condition this ticket
demanded is **met**, not merely promised.

## ⛔ But "upgrade to the FULL builder" IS the Echo gate

The permanent slot is gated by **WO-911 Q6** — *each Echo above 2 unlocks the RIGHT to buy; crystals
complete it.* On this same day the owner ruled the **Founder's Vow grants crystals TOWARD the slot,
never the slot**, precisely to protect that gate. An end-of-window offer that hands over the full
builder buys past the very gate the Vow was forbidden from bypassing.
⛔ See `FOUNDATIONAL_RULINGS.md` §1 — do not restate it here, cite it.

⚠ **And the TIMING is the sharper half.** Offering it **at the moment the taste is removed** puts the
decision at peak felt-loss — the sell-the-cure-for-a-disease-we-added shape the shield fence already
names. Same trade, different door.

## ⭐⭐ THE RESOLUTION: grant CRYSTALS TOWARD the slot, credited by what they already paid

This is not a safer compromise — **it is the version that serves the stated goal better.**

- **Granting the slot ENDS THE STORY.** Transaction complete, nothing left to want. A player who has
  the thing has no reason to return for it.
- **Granting PROGRESS leaves something unfinished that only PLAYING completes.** They still need the
  Echoes, and Echoes come from coming back. ⭐ **The unfinished thing is the hook.**
- The credit for what they already paid is **real and generous** — which was the owner's actual
  intent — without being a bypass.
- It needs **no new precedent**: it is the exact shape already ruled for the Founder's Vow.
- ⭐ It reframes the moment from *"pay to stop losing it"* to *"you are partway to keeping it"* — a
  better feeling, and a better conversion story.

⭐ **The taste already does the emotional work.** They feel three builders, lose it, want it back.
What is handed over at that moment decides whether the answer is **"pay again"** or **"play more"** —
and only the second makes them care.

⚠ **The credit must be SERVER-RECORDED and one-time**, or it is replayable — same rail as WO-1177's
7-day window. ⛔ A client-side "you already paid" flag is trivially forged.

## ⛔ If the owner instead wants the slot granted outright

That is her call, but it **requires amending `FOUNDATIONAL_RULINGS.md` §1 in the same change**, not
leaving it contradicting — a seat reading §1 would correctly refuse to build it, and a rule that is
quietly violated by one feature stops being enforceable for the next.
