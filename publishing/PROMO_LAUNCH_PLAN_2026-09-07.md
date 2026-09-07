# Promo plan - "Founders' Week" to pull people in (2026-09-07)

**Status:** DRAFT for the owner. Nothing here is live until she runs the INSERT and posts the copy.
**Why now:** measured 2026-09-07 from `analytics_events` (read-only): 79 ids ever sent `session_start`
(67 guests, 12 wallets); 20 came back on a second day, 9 on five or more days; 18 active in the last
week, 11 new in the last week, 3 active today. The game retains the people who stay (9 of 79 play on 5+
days) and simply has not been put in front of enough people. The promo's job is REACH, and the code's
job is to make the first session a win and to bind a wallet (a wallet is the identity that cloud-saves).

## 1. The offer (uses what the game already has - no code)

The `promo_codes` table already supports a TIERED public code (WO-1256): the first N redeemers get a
pack, everyone after gets crystals. The Night Market's "Redeem a Code" door and the promo guards are
live (WO-1533 owner bypass, WO-1440 guest redeem, WO-1453 signature rail).

**Code:** `FOUNDER` (short, spoken, no zero/O confusion).
- Tier 1, first **100**: `starters-hand` (the Starter's Hand pack: the resource bundle the store already
  sells - a first session with a full build queue).
- Tier 2, everyone after: **60 crystals + 400 coins** (one skip on a timer, one troop hire).
- Per-player limit 1, expires in 14 days, global cap 1000.

Run this in Neon (it is the owner's SQL; the pack sku exists in `packs.json:307`):

```sql
INSERT INTO promo_codes (code, reward_crystals, reward_coins, message, active,
                         max_redemptions, per_player_limit, expires_at,
                         tier1_pack_sku, tier1_limit, tier2_reward_crystals, tier2_reward_coins,
                         created_by)
VALUES ('FOUNDER', 0, 0,
        'Welcome, Founder. The first hundred hold a Starter''s Hand; everyone after, a purse to spend tonight.',
        TRUE, 1000, 1, NOW() + INTERVAL '14 days',
        'starters-hand', 100, 60, 400,
        'owner-2026-09-07-founders-week')
ON CONFLICT (code) DO NOTHING;
```

Verify after: `SELECT code, active, tier1_limit, redemption_count, expires_at FROM promo_codes WHERE code='FOUNDER';`

## 2. Where to post (the Seeker audience is the audience)

The app is on the Solana dApp Store as an UPDATE of an existing listing (App NFT
`5MG4atMRDSVn9t75oFz1KVxKdUkyz2wPi2MeunT8yFe6`); there is NO web listing URL - the only door is the
in-store link `solanadappstore://details?id=<app id>` on a Seeker, or searching "Echoes of Elarion" in
the dApp Store. Post where Seeker owners are:

1. **X** - reply-quote the Solana Mobile and dApp Store accounts' latest posts (they re-share indie
   launches), then a standalone post with a 15 s clip (see s4). Tag `@solanamobile`.
2. **Solana Mobile Discord** - the `#dapp-store` / builders channel: one post, the code, the clip.
3. **r/solana and r/SolanaMobile** - one post each, the "why it's different" line first, the code last.
4. **Firebase testers** - they already have the build; send the code as the release note of the next
   tester push so they redeem before the public does (they are your first hundred).
5. **The Night Market itself** - the redeem door is one tap from the store; the code should be in the
   dApp Store "What's New" text so a new installer sees it before they open the game.

## 3. Copy (ASCII, paste-ready; three lengths)

**One line (X, Discord):**
> Echoes of Elarion is live on the Seeker dApp Store. Build a town, raise an army, raid your neighbours,
> polish the stones you pull from the dungeons. Code FOUNDER: the first 100 get a Starter's Hand. 0% store fee.

**Short (Reddit title + body):**
> Title: A town-builder RPG on the Seeker where your wallet IS your save - code FOUNDER for the first 100
> Body: Echoes of Elarion: hold the last tree, build Elarion, train troops that cost time not gold, raid
> other towns, dive dungeons for rough stones the Jeweler turns into Rings of Power. Sign in once with
> your wallet and it remembers you; the wallet is only asked for when you buy or redeem. Packs are paid in
> SKR with a 0% store fee - every payment reaches the realm. Redeem FOUNDER in the Night Market: first 100
> get a Starter's Hand, everyone after a purse of crystals and coins. Two weeks only.

**Long (Discord / a pinned post):** the Short copy plus the three-beat hook in s4 and one honest line:
> Built by one studio, patched daily; if the town does not do what the screen says, the FLAG button in
> the corner sends me the frame.

## 4. The 15-second clip (record on the Seeker, no edit)

1. The Rough Stone fanfare (WO-1596, next build) - the moment the game says THIS IS A BIG DEAL.
2. A raid: tap to deploy, the army holds the line, three stars snuff down (WO-1594 on Grok's branch).
3. Manage hub with the three painted cards and the QUEUE badge (WO-1597, next build).
Record after the next tester build lands; these three are the screens that changed this week.

## 5. What to watch (the same table you opened today)

- `promo_guest_redeem` / `promo_redeem` count and ids, and `redemption_count` on the FOUNDER row.
- `session_start` **new_7d** (was 11 this week) and `active_1d` (was 3).
- `save_sanity_reject` must stay at zero for new ids once WO-1598 lands; a rise means new games are
  being refused by the cloud again.
- A read-only breakdown is one command from the repo: `node Builds/query-players.mjs`.

## 6. Do NOT

- Do not tie the promo to a SKR balance or an airdrop of SKR (it is Solana Mobile's token, never ours).
- Do not promise cloud sync until WO-1598 is on the phone.
- Do not run it before the store update ships: the live store build is 2026-08-17 and lacks the exit
  buttons, the sign-in fix and the fanfare; the first hundred should meet this week's build.
