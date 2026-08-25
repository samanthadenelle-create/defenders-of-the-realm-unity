# WORK ORDER 1196 - the wallet preference chain becomes the player's

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner reconciled 1195 -> 1197 in the same edit)
**Silo:** Wallet
**Split from:** WO-1171 section 4, because the lead fenced `Wallet/` read-only and then ruled a change
that lives entirely inside it. Codex caught the contradiction and refused rather than widening
silently. **That refusal was correct.**

---

## Why this is its own ticket

WO-1171 section 4 is **placement** - putting connect/disconnect somewhere a player can reach, in
`Settings/`. That half is returned and stands.

This ticket is the **mechanism**, and it lives in `Wallet/`. Splitting them lets the mechanism proceed
now without waiting for the picker's visual design, which the UI seat is authoring separately.

## The owner's ruling

> *"Make the wallet preference chain player-selectable."* - owner, 2026-08-25

And the reasoning, recorded so it is not re-litigated as a nice-to-have:

> *"Who says that just because they have the Solana wallet, that's the one they want to use? Maybe
> they want to use a more robust Android wallet that's gonna be better."*

⛔ **The current behaviour encodes a wrong inference: owning a Seeker does not mean wanting to transact
from its Seed Vault.** The app decides silently and then SEALS that decision. This is a defaulting bug
with a preference-shaped fix.

## What exists today

`Assets/_Modules/Wallet/TargetedLocalAssociationScenario.cs:123`:

    public static readonly string[] PreferredWalletPackages =
    {
        "com.solanamobile.wallet",         // Seeker / Seed Vault - RANK 1 (owner ruling 2026-08-05)
        "app.phantom",
        "com.solflare.mobile",
        "app.backpack.mobile.standalone",
        "ag.jup.jupiter.android",          // Jupiter - LAST
    };

⭐ **The chain is already DATA** - that file's own header calls it *"data, not logic."* So this is a
small change to a list that was designed to be one.

⚠ **READ THAT FILE'S HEADER BEFORE TOUCHING IT.** The clone exists because the SDK's generic
`LocalAssociationScenario` fires an IMPLICIT intent and Android picks the winner - on the owner's
Seeker that winner was **Jupiter, and the Seeker wallet was never offered.** ⛔ Do not "simplify" back
to the SDK scenario; that reintroduces the exact bug this file was written to fix. `setPackage()`
narrows DELIVERY ONLY - action, category, data URI and the websocket association stay byte-identical,
and the MWA identity check depends on that.

## FILE GRANT (this is the part WO-1171 got wrong - it is explicit here)

**WRITE:**
- `Assets/_Modules/Wallet/TargetedLocalAssociationScenario.cs` - consult a stored preference before the chain
- `Assets/_Modules/Wallet/MwaSessionStore.cs` - only if a public clear/reset seam does not already exist
- ONE new small file under `Assets/_Modules/Wallet/` if a preference store is cleaner than inlining it

**READ-ONLY:** everything else under `Assets/_Modules/Wallet/`, all of `Assets/_Modules/Settings/`
(WO-1171 owns it), `Assets/_Modules/Core/Platform/CurrencySkinResolver.cs`.

⛔ Do NOT touch `SolanaWalletProvider.Connect`'s association protocol, the identity URI, or the
Web3 host lifecycle.

## Requirements

1. **A persisted player choice consulted BEFORE the chain.** Chosen package wins; the existing chain
   stays the fallback order.
2. ⛔ **PlayerPrefs, NOT the save schema.** It is a device/wallet-app choice, not player progress, and
   it must not force a schema bump.
3. **Default stays SEEKER.** The 2026-08-05 ruling is not repealed - it becomes a default rather than
   the only outcome. A player who never chooses sees exactly today's behaviour.
4. **Expose the INSTALLED handlers** so a picker can list real options. The scenario already
   enumerates them via `queryIntentActivities()` - reuse it, do not re-implement.
   ⛔ Never offer a wallet that is not installed; an option that fails on tap is worse than no option.
5. ⛔⛔ **CHANGING THE WALLET MUST CLEAR THE SEALED SESSION** (`MwaSessionStore`). Otherwise
   `WalletSkinBootstrap.TryAutoResumeAsync` silently reconnects the OLD wallet at next boot and the
   choice appears to do nothing. **This is the single most likely way to ship this feature broken.**
6. **A stored choice whose wallet was later uninstalled falls back to the chain, never hard-fails.**
   That file already guarantees this for the chain - preserve it for the stored choice too.
7. ⭐ Instrument it (CLAUDE.md section 12): trace the RESOLVED package and WHY it won - stored choice,
   chain rank, or implicit fallback. A silent selection is what produced this ticket.

## ⛔ THE SAVE-IDENTITY HAZARD - ruled, and binding

**`GameState.BoundWallet` IS THE SAVE KEY** (`SolanaWalletProvider.cs:437`;
`GameStateService.cs:1809` warns of a *"wrong-key write"*). So switching wallets switches the player's
saved kingdom.

⭐ **OWNER RULING 2026-08-25: option (a).** The picker states plainly, BEFORE switching, that a
different wallet means a different saved kingdom, and requires a **deliberate confirm**.

- ⛔ **(c) - re-keying the save to follow the player - is NOT AUTHORIZED.** Nothing in this lane may
  move, copy, merge or re-key save data. That is a live-build data migration and the owner's call.
- The confirm names the CONSEQUENCE, not the mechanism. A player does not know what a "save key" is;
  they know what "your kingdom" is.
- ⛔ A deliberate confirm - not a toast, not an inline caption. The whole hazard is switching by
  accident and finding an empty town.
- ⭐ **Say that nothing is destroyed:** the old kingdom stays keyed to the old wallet and returns when
  that wallet is reselected. It is TRUE, and it turns a frightening dialog into an understandable one.
- ⚠ **Exact wording is the OWNER's.** Propose it; do not settle it. ⛔ Do not block on it.

⚠ **The confirm UI itself is WO-1171 section 4 / the UI seat's design.** This ticket must EXPOSE what
that flow needs - current bound wallet, installed options, a switch entry point that clears the seal -
without drawing it.

## Acceptance

1. With no stored choice, resolution is byte-identical to today (Seeker wins). Prove it.
2. With a stored choice, that package is targeted, and a trace line names why it won.
3. Changing the choice clears the sealed session - proven, not assumed.
4. A stored-but-uninstalled choice falls back to the chain without a hard failure.
5. ⛔ No save data is read, written, moved or re-keyed by this lane.
6. ASCII-only strings; the file is currently pure ASCII and the encoding oracle will red it otherwise.
