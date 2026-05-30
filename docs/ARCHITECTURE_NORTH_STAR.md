# Architecture North Star — "does the foundation grow into the dream, or need a rewrite?"

> Companion to `docs/NORTH_STAR.md` (the vision). This answers the technical mirror of *"is it ever
> possible?"* — **yes, IF a few load-bearing choices are made early.** The dream stays plausible not by
> building it all now, but by keeping the seams right so each ladder rung *adds* a system instead of
> forcing a rewrite. Good news: the project is already aligned to most of these.

---

## The 6 load-bearing principles (keep these true and the dream stays reachable)

1. **Data-driven, not hand-authored.** The single biggest "stays plausible / needs rewrite" fork.
   Today the village is *hand-built* by `VillageSceneBuilder` (a fixed layout). The dream is the
   **player** authoring layouts. So generalize from *"we build the level in code"* → **"anything builds
   the level from data"** (bases, worlds, units, tactics, economy = data). **Build mode = a UI that edits
   that data.** Make this the spine and rungs 4–6 (place/structure/build-free) are *content*, not rebuilds.

2. **Server-authoritative for anything persistent or competitive.** Bases, snapshots, economy, tournaments,
   offline accrual → **server is the source of truth**, client renders + predicts. This is the
   never-connected backend becoming real. Without it: cheating, and async PvP is impossible. (Verify on
   the server, never the client — same rule as payments.)

3. **Deterministic, headless-simulatable combat.** ⚠ **Hardest to retrofit — design it in EARLY.** Async
   PvP = *your authored attack runs against their base snapshot* with no one live; tournaments + offline
   need the same. That only works if the combat sim is **deterministic** (same inputs → same result) and
   **runnable headless** (server can replay/validate). If combat is built as live-only spaghetti, PvP
   forces a rewrite. Build it as a deterministic sim from the start; rendering is a *view* on top.

4. **Everything swappable behind interfaces (modules/providers).** Already proven: `CurrencyKind`
   providers, modular `Wallet`/`Web3` asmdefs. Extend the discipline to **AI tactics** (composable
   behaviors), **content** (data packs), **payment** (Stripe/IAP/wallet). This is what makes the 3-build
   distribution + Pi/Solana/fiat *wiring, not architecture.*

5. **Mobile-scalable rendering = the literal answer to "how big can the world be."** A big world lives on
   a phone via **streaming chunks + instancing + LOD + culling + lightweight assets** — never "draw it
   all." Already in motion (the polyperfect lightweight pack, the LOD/culling WOs). The world scales by
   *not rendering what you can't see*, so "how big" becomes a streaming problem, not a draw-call wall.

6. **Offline-first + sync.** The idle/farm/log-in-and-collect loop needs **local-first state + server
   sync + timestamped offline accrual** (already the persistence model). This is what turns "dead time on
   a phone" into a retention loop.

---

## Design-early-or-pay-later (the only real risks)
- **#3 deterministic/headless combat** — the expensive retrofit. Decide before the PvP rung.
- **#2 server authority** — design the data + verification seams before real economy/PvP money moves.
- **#1 data-driven content** — start treating layouts/units as data now, so build-mode isn't a rewrite later.

Everything else (#4 modularity, #5 rendering, #6 offline) the project is *already* doing.

## The ladder maps to architecture increments (not rewrites)
| Rung | Architecture it adds | New foundation needed? |
|---|---|---|
| Tower / Town | combat + waves + base systems | built |
| Explore | **streaming world chunks** (#5) | extend rendering |
| Place your base | **data-driven placement** (#1) over existing primitives | seam, not rebuild |
| Structure settlement | **build-mode UI** editing layout data (#1) | content on the seam |
| Build-free / PvP | **server snapshots (#2) + deterministic sim (#3)** | the two to design EARLY |

## Already-aligned (the recurring truth: foundations match the vision)
Modular asmdefs · currency-provider rail · offline-first persistence · lightweight streamable assets ·
clans/chat · multi-build distribution. The drift was *focus*, never *foundation*.

---

## Bottom line
**The dream is architecturally plausible.** Keep content data-driven, make combat a deterministic
headless sim, and put a server-authoritative spine under the persistent/competitive parts — decide those
*before* the PvP/build-mode rungs — and every other rung is additive on seams that already exist. Not a
rewrite. A climb.
