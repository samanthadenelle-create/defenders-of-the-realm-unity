# The treasury, in plain English

**Written 2026-08-23.** Keep this. You do not need to understand the cryptography — you need to
know which address is which, what is safe, and who to trust when something looks wrong.

Everything here was **checked against the Solana blockchain**, not copied from a screenshot or
taken on anyone's word. To re-check it at any time:

```
node tools/treasury-verify.mjs 9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj
```

If that prints `TREASURY_VERIFY_OK`, everything below is still true. If it prints
`TREASURY_VERIFY_FAIL`, **stop and read what it says** — something changed.

---

## The four addresses, and what each one is

### 1. Your treasury — where money arrives
```
9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj
```
The Squads vault, shown in the app as **"EoAAccount"**. When a player buys something, this is
where it lands. It currently holds **100 SKR** and a very small amount of SOL.

This is also the address in your browser's URL bar on `app.squads.so` — which is why it looks like
"the" address for everything.

### 2. The SKR pocket inside the treasury
```
6SVNy7VP7xSiniYD1RAh4DQN2YnCnpmtybC1SYKk3uL3
```
On Solana a wallet does not hold different coins in one place — each type of coin gets its own
sub-account. This is the treasury's **SKR pocket**. Your 100 SKR is physically here. You will
rarely touch it directly, but the game's server needs to know it exists.

### 3. The controller — who is allowed to move the money
```
BcHLoNCsnGD6oegywkP19PALKMQYoFeQWTvmPLmp22no
```
The vault holds the money; **this** decides who can move it. The Squads app never shows it — we
worked it out mathematically and confirmed it points at your vault. You do not need to use it.
It is written down so nobody has to re-derive it later.

### 4. SKR itself — the coin, not an account
```
SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3
```
This is the **identity of the SKR coin**, the way "USD" names a currency. It is not a place and
cannot receive anything.

⛔ **SKR is not ours.** It belongs to Solana Mobile. We never created it and we never hold a supply
of it. If any tool or guide tells you to "create your game's SKR mint", **it is wrong** — following
it produces a fake coin that looks real and is worth nothing.

---

## ⛔ Two addresses that are NOT places to send money

| Looks like an address | What it actually is |
|---|---|
| `So11111111111111111111111111111111111111112` | The **name of SOL as a coin**, not an account. You saw it in the app under the SOL row. Money sent here is gone. |
| `SKRbvo6Gf7…NPGZhW3` | Same thing for SKR — the coin's identity, not a destination. |

The giveaway is that both appear in a **coin list**, next to a balance. Anything shown as a *coin*
is a currency name. The thing you send money **to** is the vault (#1).

---

## ⭐ The one real problem — FIXED 2026-08-24

**Your treasury is now "2 of 3".** Any two of three keys must approve a withdrawal, so losing one
key is survivable and no single key can move the money alone. We read this straight off the
blockchain rather than trusting any note in the project, and it came back *"2-of-3, timeLock 0 —
production-shaped"*.

**Everything below is the OLD problem, kept so the history reads straight. It no longer applies.**

<details><summary>What the problem used to be (resolved)</summary>

Your treasury was set to **"1 of 1"**. That meant **one key could move every cent, immediately, with
nothing else required.** There was no second approval and no delay.

That is still an improvement on what we had — the old address was an ordinary wallet, and this one
at least has a proper control layer that can be upgraded. But it is not the protection we set out
to get.

**What this means in practice:** if that one key is lost, stolen, or the device dies, the money goes
with it. There is no recovery.

**What to do about it:** in the Squads app, **Add Member** twice and set the threshold to **2**.
That gives you "2 of 3" — any two of three keys approve a withdrawal, and losing one is survivable.

**When:** before real players can buy anything. It is fine as-is for the single 1-SKR test.

⭐ **The address does not change when you add members.** So doing it later cost nothing and broke
nothing — which is also why the rest of the project never noticed it had been done, and eight files
went on claiming "1 of 1" for a day after it was already fixed.

</details>

Also worth doing at the same time: set a **time lock** (currently 0). That puts a delay between
approving a withdrawal and it happening, which is the difference between noticing a bad withdrawal
and reading about it afterwards.

---

## The number that nearly cost real money

SKR uses **6 decimal places**. So:

> **1 SKR = 1,000,000** of the smallest unit.

A document in this project said **9** decimal places. That number was real — but it belonged to our
own *test* coin, not to the real SKR. If it had been used, **1 SKR would have been 1,000 SKR** — a
thousand times too much.

And the safety check would not have saved us: it runs **after** the payment settles. It would have
correctly reported a problem, with the money already gone.

**The rule that came out of it:** the amount is always read from the coin itself, on the
blockchain, before the first payment — never from a document, and never from a different network's
settings. The verifier does this every time it runs.

---

## What is still left before you can take real money

1. **You** set four settings on Vercel (the game's server): the treasury address, the SKR pocket
   address, a blockchain connection URL, and a switch to enable the test. *I cannot reach those
   settings.*
2. **You** put a little SOL into the treasury — it needs a small amount to pay its own fees.
   It has 0.001; your wallet has ~6.
3. **A rehearsal:** start a purchase and *cancel it*, and we confirm nothing moved.
4. **One real 1 SKR purchase**, then we check that the blockchain, the server, your wallet, and the
   game all agree — and that the item is granted exactly once, even after a relaunch or reinstall.
5. ~~Then the threshold fix above~~ — **done 2026-08-24.**

---

## If something ever looks wrong

- **Run the verifier** (top of this page). It answers "is the treasury still what we think it is".
- **Do not send test money to an address to see if it works.** Ask first — the addresses that look
  most like destinations are sometimes the ones that destroy funds.
- **Nobody should ever ask you for a private key or seed phrase** — not a tool, not a script, not
  me. Nothing in this project needs one. There is no key stored anywhere in this repository, and
  the verifier here holds none: it can only *look*, never *move*.

**Related, if you want the detail:** `docs/TREASURY_RUNBOOK_2026-08-23.md` (the step-by-step) and
`docs/MONETIZATION_STATE_2026-08-23.md` (where the money rail stands overall).
