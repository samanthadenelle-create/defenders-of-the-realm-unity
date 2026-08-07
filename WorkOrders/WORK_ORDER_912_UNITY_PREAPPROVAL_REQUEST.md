# WO-912 — Unity pre-approval request (DRAFT for the owner to send)

**Status: READY TO SEND** · **Drafted:** 2026-08-07 (CLI) · **Blocks:** WO-912 D3 (no SDK until this returns in writing)

> ### ★ ARCHITECTURE AMENDED 2026-08-07 — MAX MEDIATOR + UNITY BIDDING (not LevelPlay direct) ★
>
> D2 first ruled **Unity LevelPlay direct**. A Grok-proposed alternative — **AppLovin MAX as the
> mediator with Unity demand pulled through its bidding adapter** — was checked against Unity's own
> docs and is **better**, so it is adopted: it keeps MAX's lightest-in-class dependency profile and
> cleanest no-fill signal (204 vs -5001) *and* still gets Unity demand.
>
> **Two claims in that proposal did NOT survive the check** ([Unity's setup doc](https://docs.unity.com/en-us/grow/dashboard/bidding/applovin), fetched at source):
>
> - *"Do not install the separate Unity Ads SDK"* — **FALSE.** *"you first need to install and
>   initialize the latest version of the Unity Ads SDK"* (4.9.2+) **and** the MAX Unity Ads adapter.
>   Both ship in the binary.
> - *"No need to email Unity"* — **DOES NOT FOLLOW.** Setup *"requires you to create a new ad unit and
>   a bidding placement in the Monetization dashboard"*, then copy the **bidding Placement ID** into
>   MAX. You hold a Unity publisher account with live placements, so Unity's Content Policy applies to
>   your **inventory**. Mediation changes who fills the ad, not whose policy governs the app.
>
> *(That proposal was right about the rest, including "set AppLovin MAX as the mediation partner in the
> Unity Monetization dashboard", which matches the doc verbatim: project → Settings → Mediation Partner.)*
>
> **CORRECTION TO MY OWN EARLIER CLAIM:** I described the Unity adapter as a *waterfall* adapter needing
> a **Game ID**. It is **bidding**, and the credential is a **bidding Placement ID**. The conclusion
> (you need your own Unity account) held; the mechanism I gave for it was wrong.
>
> **UNVERIFIED, FLAGGED RATHER THAN GLOSSED:** `unity.com/legal/content-policy` returns **HTTP 403** to
> direct fetch, so the Regulated Activities wording quoted below comes from a **search snippet, not a
> page opened at source**. Confirm it in the case itself. Separately, a search result claiming
> *"waterfall mediation for Unity Ads ends January 31, 2026"* is **wrong** — the real event is
> [ironSource Ads **direct demand** sunsetting **April 30, 2026**](https://unity.com/products/ironsource-ads-sunset)
> (already past); iSX programmatic continues and **LevelPlay is explicitly unaffected**.
>
> **BECAUSE BOTH NETWORKS NOW HOLD AN ACCOUNT, Q2a GOES LIVE TOO.** AppLovin's publisher policy is
> **silent** on crypto — no provision at all. Send the AppLovin question in parallel with this one.
> That is the price of the better technical stack, and it is worth paying with eyes open.

Unity's Content Policy names *"cryptocurrency trading"* among Regulated Activities permitted *"only
with prior approval by Unity."* This is that request. **Do not integrate the SDK until a written answer
comes back** (WO-912 D3).

---

## Why this draft says what it says

Three deliberate choices, because how the app is described at signup is what a human reviewer acts on:

1. **It leads with the reward mechanic, not the wallet.** Our reward is the thing that passes their
   policy by construction; the wallet is the thing that triggers review. Leading with the answer rather
   than the concern frames the conversation correctly — and every claim is verifiable in our binary.
2. **It volunteers the crypto facts rather than waiting to be asked.** Disclosure that arrives later
   reads as concealment, and the downside here is a terminated publisher account, not a rejected
   placement.
3. **It names the one-way valve explicitly.** Value flows IN (players buy packs); nothing earned from an
   ad can ever flow OUT. That distinction *is* the policy question, so it is stated in their vocabulary,
   not ours.

**Do not soften the crypto paragraph to improve the odds.** An approval obtained by understating the app
is worth nothing — it is void the moment a reviewer opens the build, and the account loss it was meant
to prevent happens anyway, later and worse.

---

## Where to send it

Unity's Regulated Activities pre-approval runs through Unity's **publisher / monetization support**, not
a self-serve form. Open a support case from the Unity dashboard (Monetization → Support) or via
`unity.com/support`, category *Monetization / Ad policy*. Ask explicitly for **written confirmation**,
not a verbal or chat assurance — D3 requires it in writing.

⚠️ **Unverified:** the exact intake route was not confirmed at source. If the dashboard has no such
category, ask support to route it to the ad policy / Regulated Activities team and keep the case ID.

**Unity organization: `samanthadenelle`** (owner, confirmed 2026-08-07 from the Unity Cloud dashboard).
**Unity Monetization Project ID: `435f5e1e-b8bf-4f9f-8143-7d5eca669c67`** (created 2026-08-07).
Include both in the case so it routes to the right account *and* the right project.

### Where the owner is in the runbook right now (2026-08-07)

| Step | State |
|---|---|
| Unity org exists | ✅ `samanthadenelle` |
| Unity Ads Monetization **project** created | ✅ `435f5e1e-…` |
| Developer Data → Unity Ads access granted | ✅ (a data-sharing consent, **not** an approval — see above) |
| Payout profile | ⬜ not set — **and not needed to ask a policy question** |
| Install Unity Ads SDK | ⛔ **BLOCKED BY D3** |
| Install MAX SDK + Unity Ads adapter | ⛔ **BLOCKED BY D3** |
| Set Mediation Partner = AppLovin MAX | ⬜ project → Settings → Mediation Partner |
| Create bidding placement + copy Placement ID | ⬜ |
| Create MAX ad units + map the IDs | ⬜ |

**The dashboard's "Install the SDK — Required" card is exactly the step D3 gates.** Creating a project
is reversible and commits nothing; installing the SDK and minting placements is where we start
operating inventory under a policy we have not yet been told we pass.

**The "now available for in-app bidding in Unity LevelPlay" banner is Unity marketing its own
mediation.** D2 chose **AppLovin MAX**. Ignore the banner; the step that matters is setting the
Mediation Partner explicitly.

### What is already done, and what it is NOT

On 2026-08-07 the owner granted **Unity Ads access to Developer Data** for this organization
(Developer Data → Additional access → Unity Ads → *Access granted*).

**That is a data-sharing consent, not an approval.** It lets diagnostics flow between the org and Unity
Ads projects. Nothing on that screen asks Unity a question, so it has told nobody at Unity that this app
carries a non-custodial wallet and sells packs for crypto tokens — which is the whole substance of this
request. **It does not satisfy D3 and does not shorten this document.**

It is also slightly ahead of the gate, and harmlessly so: no SDK is installed (`Packages/manifest.json`
carries no `unity.services`/LevelPlay entry), and the grant is reversible via **Modify Access** if Unity
declines. Revoke it in that case rather than leaving a dormant data path to a provider we did not take.

---

## THE DRAFT

> **Subject:** Regulated Activities pre-approval request — rewarded video in a game with a non-custodial
> crypto wallet (Echoes of Elarion, `com.denellestudios.echoesofelarion`)
>
> Hello,
>
> I'm preparing to integrate LevelPlay rewarded video into a mobile game and I'd like written
> pre-approval before I build, because the app touches an area your Content Policy lists as a Regulated
> Activity.
>
> **The app.** *Echoes of Elarion* — a single-player base-building and raiding game for Android.
> Package id `com.denellestudios.echoesofelarion`, ARM64, minSdk 26. Primary distribution is the Solana
> dApp Store, with Google Play as a later step. Target devices are standard Android phones and the
> Solana Seeker; I've confirmed on a physical Seeker that it ships Google Play as a system app and is
> GMS-certified, so these users have full Play access.
>
> **The rewarded placement, which I believe is the deciding fact.** Watching a rewarded video takes a
> fixed number of minutes off an in-progress construction timer. That is the entire reward. It cannot be
> transferred to another player, traded, sold, gifted, or converted into any in-game currency — and
> there is no path from it to money of any kind. Mechanically it only advances a timestamp on a build
> job already owned by that player. If the player has nothing building, the offer isn't shown.
>
> I've designed it this way deliberately so it fits your rewarded-inventory policy: it is not cash, a
> prize, a gift card, a good, a service, a voucher, or anything of value outside the player's own
> account.
>
> **The part I want to disclose up front.** The game contains a non-custodial Solana wallet, used for
> player identity, cloud-save keying, and purchasing optional content packs priced in crypto tokens.
> The wallet is non-custodial — I never hold player funds.
>
> I want to be precise about how that interacts with the ad system, because it runs in one direction
> only. Players can spend money to buy an optional pack that improves the free path. Nothing a player
> earns from watching an ad can ever leave their account or become money. Value flows in; it does not
> flow out. My reading is that your policy prohibits rewards convertible *out* into money, and that
> inbound purchases are ordinary in-app commerce — but I would rather have your confirmation than my
> reading.
>
> **What I'm asking for:**
>
> 1. Is a rewarded placement of this shape — a non-transferable in-game timer reduction with no cash-out
>    path — permitted in an app that also contains a non-custodial crypto wallet and sells packs for
>    crypto tokens?
> 2. Does the presence of the wallet and token payments require formal Regulated Activities approval,
>    and if so, is this request sufficient or is there a separate process?
> 3. Are there disclosure or configuration requirements on my side (dashboard declarations, consent
>    flow, ad-unit setup) that I should complete before integrating?
>
> I'd appreciate the answer in writing, as I'm holding SDK integration until I have it. Happy to provide
> a build, a demo video of the placement, or anything else useful.
>
> Thank you,
> Samantha DeNelle — DeNelle Studios

---

## THE INTEGRATION RUNBOOK (Unity's own, captured 2026-08-07 — do NOT start it before D3 clears)

Source: [Get started with Unity Ads in-app bidding in AppLovin MAX](https://docs.unity.com/en-us/monetization/bidding/in-app-bidding/applovin)
(owner-supplied canonical path; *last updated a month ago*. The `/grow/dashboard/` path serves the
same content).
Recorded here because it is the exact sequence, and because step 4 is the reason this whole
document exists.

**Prerequisites**
1. Install the latest **Unity Ads SDK** (4.9.2+).
2. Install the **MAX Mediation SDK with the Unity Ads adapter** (4.9.2.1+; Interstitial/Rewarded 4.6.0.0+).
3. Set **AppLovin MAX** as the Mediation Partner in the Unity **Monetization dashboard**
   (project → Settings → Mediation Partner → Save).

**Then**
4. Create a **bidding placement** in the Unity Monetization dashboard and **copy its bidding Placement ID**.
5. Create matching **ad units** in the AppLovin MAX dashboard.
6. **Map** the Unity bidding Placement ID to the AppLovin bidding placement. The IDs must match exactly.

### ⚠ THE ONE-WAY DOOR, quoted because it is easy to walk through by accident

> *"The Unity Ads network can only be set up for **bidding or waterfall, not both** simultaneously. If
> both bidding and waterfall placements are created, bidding placements take precedence by default for
> ad fill requests over waterfall placements. **You can't repurpose waterfall placements for in-app
> bidding** because you need to generate a bidding Placement ID from the Monetization dashboard."*

Choosing bidding closes the waterfall path for Unity demand. Fine for us — bidding is the whole point
of the MAX-mediator architecture — but it is not casually reversible, so do not create placements to
"try it out" before the policy answer lands.

### ★ WHY THIS RUNBOOK IS ITSELF THE ARGUMENT FOR SENDING THE REQUEST ★

Step 4 has you **creating live placements in a Unity Monetization account you own**. That is *your
inventory*, and Unity's Content Policy governs inventory tied to Regulated Activities. Mediation
changes which network fills the impression; it does not change whose policy governs the app. Any
reading of "MAX mediates, so Unity is not our counterparty" dies at step 4.

**CLI PREREQUISITE BEFORE STEP 1** (WO-912 sec.10.1 risk 4): `AndroidTargetSdkVersion` is still `0`
(unpinned) while the Seeker measures **API 36**. Pin it, build, verify the payload — as its own change
with its own verification — *before* any SDK is installed, so a targetSdk break is legible on its own
rather than blamed on the ad SDK.

---

## When the answer arrives

| Answer | Action |
|---|---|
| **Approved in writing** | D3 satisfied for Unity. **Pin the target SDK first** (WO-912 sec.10.1 risk 4 — device is API 36, `AndroidTargetSdkVersion` is still 0), then integrate behind the thin `IAdService` seam per sec.10.5. Run the Android Resolver and **read the generated `mainTemplate.gradle` diff** before committing — sec.10.4 trap 4 notes LevelPlay's dependency manifest was never opened at source, and secondary sources put its deps *above* our current pins. |
| **Declined** | Fall back to **AppLovin MAX** and send Q2a (their crypto policy is silent — get it in writing). Q3a is defused for Seeker but still applies to any non-GMS Android target. |
| **No answer / stalls** | Do **not** integrate. sec.10.5: *"Both silent → ship the free path with no ad rather than risk the account."* The covenant survives without ads; the account does not survive a termination. |

**File the written answer in this WO when it lands** — CLAUDE.md §15: a state change with no canon update
is an incomplete change.
