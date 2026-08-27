# WORK ORDER 1191 - earned income adds nothing while a resource is over cap

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1190 -> 1192 with WO-1190 in the same edit)
**Silo:** Economy
**Ruling:** `FOUNDATIONAL_RULINGS.md` section 7 - CITE it, never restate it.

---

## Scope

This is the **EARNED half** of the owner's 2026-08-25 overflow ruling. The PAID half - a purchase
credits in full, above the cap - belongs to WO-1188's confirmation path and the purchase credit seam.

**This ticket:** while a resource sits **above** its cap, earned income adds **NOTHING** to it.
Not partial, not queued, not escrowed. It resumes contributing once the player spends back under.

"Earned" = harvester / Echo yield, offline accrual, raid loot, quest and daily payouts - every credit
the player did not pay money for.

## Why the suppression is the half that can hurt

The overflow itself is permissive and cannot hurt the player. The suppression CAN: a faucet that has
silently stopped is the *"I did the raid and got nothing"* complaint, and WO-978 records exactly that
class - four economy callers logged the amount REQUESTED as though it were the amount CREDITED, so
every log agreed the player had been paid while the bank took nothing.

**So it must be traced, and it must be told.**
- The credit path emits a `FlowTrace` line naming the resource, the measured cap, the measured
  balance, and that it suppressed - with MEASURED before/after, never the requested amount.
- The player is told in WORDS when a resource is over capacity and earning nothing into it.
  Never by colour alone; the owner is red/green colourblind.

## Rules

1. **The capped test is `TownBankCapacity.IsCapped()`.** Never a hardcoded resource-name list - a
   name written into a rule goes stale the day WO-1163 lands.
2. **Crystals are UNCAPPED and always pay in full** (`TownBankCapacity.cs:238-242`, `:478-482`,
   pinned by `[no-crystal-cap]`). Do not implement a crystal cap by implication - that contradiction
   is what sent WO-978 back.
3. **No overflow wallet, no escrow, no held value.** Suppressed income is not stored anywhere; it is
   not earned.
4. **Above cap is a legitimate state, not an error.** Nothing may clamp, "repair", or silently
   truncate a balance back down to the cap. Audit for existing clamps BEFORE writing anything - a
   repair pass that normalises balances would delete purchased value.
5. Existing at-cap behaviour for earned income (pay what fits, disclose) is unchanged BELOW the cap.
   The new rule concerns the region ABOVE it, which could not previously be reached.

## Acceptance criteria

1. A purchase that pushes a capped resource above capacity leaves the full amount in the balance.
2. With that resource above cap, an earned credit adds exactly **zero** to it - proven by a MEASURED
   before/after delta in a regression, not by reading the code.
3. Spending back under the cap restores earned income, proven the same way.
4. An uncapped resource (crystals) is unaffected in every case.
5. A trace line names each suppression with measured numbers.
6. No path clamps a balance down to the cap.

## Where to look

`TownBankCapacity.cs` and the `EconomyService` credit seam. Find EVERY earned credit path before
implementing - WO-978 found four callers that all reported wrongly, which is evidence these paths are
numerous and were not centralised. If they are not centralised, say so and propose the seam rather
than patching four places.

---

## LANDED 2026-08-25 - `bfcb1adaf`

**The mechanics were already correct** and were verified at source rather than taken on report:
`IsClampable` gates on `EarnedIncome` only, purchases grant through `GrantInternal(PurchasedOrPromised)`
and never reach `ClampGrant`, and `ClampGrant`'s `room = max(0, max - current)` already returned zero
above the cap. **The clamp audit found NOTHING anywhere that clamps a balance down to the cap** - so no
purchased value was ever at risk.

What landed is the framing: `OverCap` + `Current` on the status, the warn branched so `BANK FULL ...
LOST n` never appears above the cap, and the toast reading as a state rather than a danger.

⭐ **The suite shipped the exact hollow-pass class it exists to catch, and two existing ratchets caught
it** - a discarded `TrySpend` bool, and two guarded stand-downs landing GREEN. Both fixed with neither
ratchet touched, widened or exempted. The fix went past the instruction: the guards were a SYMPTOM of a
hardcoded `Wood` test vehicle - itself the resource-name list this ticket's own rule 1 forbids - so the
vehicle is discovered at runtime and one guard dissolved rather than being tokenised.

⚠ Follow-up already ticketed as **WO-1194 Part 1**: `HasHeadroom` returns false at OR above cap, so two
other surfaces still say "Bank full" to a player who paid to be there.
