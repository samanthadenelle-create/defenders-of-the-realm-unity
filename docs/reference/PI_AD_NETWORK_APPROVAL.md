# Pi Developer Ad Network — what approval actually requires

**Researched 2026-09-02 (CLI) from official Pi docs. Every claim is marked DOCUMENTED / INFERRED /
UNKNOWN. Pi's ad documentation is thin — its own SDK page says "Coming soon..." — so that separation
matters more than usual.**

App under discussion: "Echos of Elarion", slug `echos-of-elarion-r9c5`,
hosted `https://echoes-of-elarion.vercel.app`, reachable at `https://echoesofelarions6578.pinet.com`.
Portal shows badge **`Testnet`** and **"Completed Steps: 8 of 10"**.

## The headline: the ads form is the easy half

Ad eligibility IS a separate application with its own button and 3-step checklist — but its
SELECTION CRITERION is that the app is already listed in the **Mainnet Ecosystem Interface**.

> "The premise of selection is to be listed in the Mainnet Ecosystem Interface and compliant with
> developer ecosystem guidelines." — Pi Core Team, https://x.com/PiCoreTeam/status/1911881484237152290

> "Applying does not guarantee your app will be included - apps must still meet the Mainnet ecosystem
> listing requirements." — https://minepi.com/blog/ad-network-expansion/

> "Displaying ads is open to all applications in the Pi ecosystem, but **only applications approved by
> Pi Core Team can be monetized**." — https://raw.githubusercontent.com/pi-apps/pi-platform-docs/master/ads.md

**So the SDK will render ads in a Testnet app right now and pay nothing.** The real work is the
mainnet listing.

## THE POSSIBLE BLOCKER — network is fixed at registration (DOCUMENTED)

> "An app can only connect to one network at a time, and once you register the app, **this option
> cannot be changed**." — https://github.com/pi-apps/pi-platform-docs/blob/master/developer_portal.md

The official checklist treats mainnet as a SEPARATE project: "Create Developer Portal Project for Pi
Mainnet", "Generate API Key and update from Testnet version", "Verify App URL, ensuring uniqueness
across projects".
https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/gettingStartedChecklist/

**INFERRED, not documented, that THIS app needs re-registration** — and there is a real contradiction
in our own evidence that must be resolved before anyone acts:

⚠ **Pi sign-in on this app authenticates on MAINNET.** Captured nine times on 2026-09-02:
`PiInit(sandbox=False)` then `Signed in as samanthadenelle`, zero failures. Yet the portal badges it
`Testnet`. The docs do not explain that combination. Either the badge is stale/cosmetic, or auth is
network-agnostic and the badge is the truth. **Do not delete or re-register anything until the check
below settles it.**

If re-registration IS needed it is cheap: same hosted URL, same code, new registration on Mainnet, new
API key swapped into the server env, re-run domain validation. The Vercel deployment does not move.

## The 20-minute check that replaces every inference here with fact

Do these BEFORE building or writing anything.

1. **Open the app on PiNet in Pi Browser. Is there a black-and-yellow diagonal stripe across the top?**
   That stripe is the documented Testnet indicator (`developer_portal.md`). Present = the network
   blocker is real.
2. **Portal -> the app -> scroll the detail page to the BOTTOM. Is there a "Dev Ad Network" button?**
   Its absence is itself the signal that the app is not yet eligible.
3. **Portal -> Checklist. Which 2 of the 10 are unticked?** Screenshot it.
4. **Portal -> Configure App Details.** Do privacy-policy / ToS / age-rating fields exist?
5. **Portal -> Ecosystem Listing.** Does a submit action exist, and what does it demand?

## The remaining 2 of 10 — INFERRED

The portal checklist is documented as **9** steps (mapping to steps 5-13 of the public checklist); the
owner's portal says **10**, so the numbering has drifted and **no published enumeration matches a
10-step portal**. Treat the following as the documented sequence, not a 1:1 map.

Steps 1-11 are visibly done (deployed, signs in, reachable on PiNet). The two needing an artifact
outside the form, and almost certainly the outstanding pair:

- **Step 12 — Validate Domain Ownership.** Serve `validation-key.txt` at the domain root as plain
  text, then hit Verify. ✅ **Already true for us**:
  `https://echoes-of-elarion.vercel.app/validation-key.txt` returns 200 with the 128-char key
  (WO-1313), and the app is Published, which requires validation to have passed.
- **Step 13 — Process a transaction on your app.** ONE real Pi payment completed end to end, including
  the server-side `approve` and `complete` calls. **A payment created but never completed does not
  count.** This is the step most apps stall on. It is also exactly what WO-1318 built and what has
  never been exercised.

## The ads click-path (DOCUMENTED — https://minepi.com/blog/ad-network-expansion/)

Pi Browser -> **Develop** -> select the app -> **scroll down the app detail page** ->
**"Dev Ad Network"** button -> **"Ads Checklist"** -> complete all **three** steps -> submit.

Not Ecosystem Listing, not Revenue Dashboard, not PiNet Settings.

⚠ **UNKNOWN: what the three Ads Checklist steps contain.** No official page enumerates them and the
SDK docs say "Coming soon...". They will be seen for the first time in the portal.

## Listing requirements — ALL DOCUMENTED

https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/mainnetListingRequirements/

1. **Functional and professional** — fully operational, every interactive element works. No dead ends,
   no placeholder screens.
2. **Developer KYC completed** — done in the Pi mining app; also gates the Mainnet wallet.
3. **Branding** — domain must not start with "pi"; no misuse of Pi logos or design elements.
4. **Pi Authentication ONLY** — "no alternative login methods permitted".
5. **Pi currency EXCLUSIVELY** — "All transactions must be conducted in Pi, with **no support for
   non-Pi Tokens or fiat currencies**."
6. **No external redirects** — exceptions case-by-case on absolute necessity.
7. **Minimal data collection** — collecting emails/phone numbers prohibited unless functionally required.

⚠ **UNKNOWN: whether a hosted privacy policy / ToS / age rating is required.** Not found anywhere in
official Pi documentation. That is a gap in their docs, not a confirmed absence — check the Configure
App Details fields before writing legal pages.

## WHAT THIS MEANS FOR US — requirements 4 and 5 are a direct hit

**The two defects fixed on 2026-09-02 are not polish. They are listing requirements.**

- **WO-1322** — the wallet gate demanded a **Solana wallet** after a successful Pi sign-in. That is an
  alternative login method on the critical path: a direct violation of requirement **#4**.
- **WO-1323** — the Night Market priced everything in **$SKR**. A non-Pi token quoted as the purchase
  currency: a direct violation of requirement **#5**.

Both are fixed and gated but **NOT yet deployed and never verified in the field.** Submitting for
listing before that deploy would very likely fail review on the two most explicit rules Pi publishes.

**Remaining audit still owed before submission** (highest risk first):

1. Walk the DEPLOYED Pi build as a player and confirm **no Solana path, no fiat price, no non-Pi token,
   no non-Pi login** is reachable anywhere.
2. Any external link (Discord, store, support) is a documented rejection reason — remove or justify.
3. Any dead-end button or placeholder panel fails "fully operational".
4. Test on the PHONE in Pi Browser, not Pi Desktop. A slow or failing WebGL load reads as a broken app.
   ⚠ Note the unexplained archer-tower crash (WO-1324) — a reviewer hitting that is a rejection.

## Timeline — UNKNOWN

No official SLA is published for either mainnet listing or the ad application. Community reporting
suggests slow, unpredictable listing times; that is anecdote, not documentation. Plan as though a
rejected submission costs weeks, which is why the audit above is worth doing first.

## Ads implementation requirements (DOCUMENTED, already built under WO-1320)

- Rewarded ads **require an authenticated user**.
- Reward **only** after server-side verification returns `mediator_ack_status == "granted"`
  (`GET /v2/ads_network/status/:adId`, `Authorization: Key <PI_NETWORK_API_KEY>`). Client-side trust is
  exploitable via hacked SDK builds.
- Check `Pi.nativeFeaturesList()` contains `"ad_network"`; otherwise the SDK returns
  `ADS_NOT_SUPPORTED` and the user needs a Pi Browser update.

All three are implemented in WO-1320 and pinned by `PI_AD_REWARD_OK`.

## Ordered plan

**Blocking, in order:**

1. The 20-minute portal/PiNet check above — converts inference to fact.
2. Developer KYC, if not already complete.
3. If Testnet-locked: register a new Mainnet project, same URL, new API key swapped server-side,
   re-run domain validation.
4. Deploy the WO-1322 / WO-1323 fixes and verify in Pi Browser that no Solana / SKR / fiat /
   non-Pi-login surface remains. **This is the requirement-4/5 fix and it gates submission.**
5. Complete ONE real Pi payment end to end (WO-1318) to tick checklist step 13.
6. Submit Ecosystem Listing.
7. Once listed: app detail page -> "Dev Ad Network" -> Ads Checklist -> submit.

## Sources

- https://raw.githubusercontent.com/pi-apps/pi-platform-docs/master/ads.md
- https://pi-apps.github.io/pi-sdk-docs/platform/Ads
- https://github.com/pi-apps/pi-platform-docs/blob/master/developer_portal.md
- https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/mainnetListingRequirements/
- https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/gettingStartedChecklist/
- https://pi-apps.github.io/community-developer-guide/docs/gettingStarted/devPortal/
- https://pi-apps.github.io/community-developer-guide/docs/importantTopics/mainnetVsTestnet/
- https://minepi.com/blog/ad-network-expansion/
- https://x.com/PiCoreTeam/status/1911881484237152290
