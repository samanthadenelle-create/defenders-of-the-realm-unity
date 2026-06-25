# Overnight Summary — 2026-06-25 (good morning ☀️)

Everything you asked for ran clean. Nothing pushed to **git** (your boundary); the **itch** publish you authorized went out.

## 🌐 LIVE ON ITCH
- The deeper-world WebGL build is **pushed + live**: `denellestudios/defenders-of-the-realm-defend-the-tower` (channel `html5`).
- Open your itch project page in a browser to play it — you can finally **see the UI on web**.
- Status check any time: `butler status denellestudios/defenders-of-the-realm-defend-the-tower:html5`

## 📐 Vercel question — ANSWERED (honest): still too big
- The game's Brotli-compressed `.data` payload = **~119 MB** (147 MB total dir). The manifest-lean swap barely moved it — **the bulk is the committed game art, not VFX**.
- So **Vercel is still not viable** as-is (its practical limits choke on a ~119 MB payload — same wall as before). A Brotli toggle doesn't fix it.
- To get back on Vercel we'd need a real **asset-payload shrink** (texture compression, Resources trim, the deferred asset purge) — a genuine optimization project, not a setting. **itch (butler + decompression fallback) handles 147 MB fine**, so it stays the web home until/unless we do that shrink. Verdict: park Vercel; it's a "later, if we optimize" item.

## 🏰 Deeper world (committed local, fleet-verified)
- **VFX Parade upgraded** — orbit/angle + Front/Side/Top/45 + auto-spin + **element filter (jump to "Fire")** + the **full ~466 Spells Pack** (restored after the lean web build). Dig the fireball gems via Dev tools -> VFX Parade.
- **Tree aura + castle tower glow** — procedural, clone-safe, slider-tunable (`ff.hubambientvfx`).
- **Castle moat + 4 drawbridges** first-pass (`ff.castlemoat`) — boundary + exits + chokepoints + clean navmesh links. Footprint-shrink + functional N/E/W seams specced in `docs/CASTLE_MOAT_DESIGN_NOTE.md` (WO-509 follow).
- **Grey-box blocker FIXED** — it was `CastleBarracks` parked at (6,0,4) in your spawn->tree corridor; moved to (16,0,-4), off the path. Polyperfect materials re-fixed (grey -> tan).
- **Fleet verify: clean** — 0 softlocks / 0 talk-route violations / 0 dialogue No-node / 8-8 runs. (Pre-existing known noise only: EnvTreeFix mis-scope, V2 navmesh/seam, headless panel-open.)

## 💰 Monetization — honest research is in (`docs/PRODUCTS.md` + `docs/MARKET_RESEARCH.md`)
- **3 of 4 tool ideas: SKIP** — Tripo->Mixamo (Unity Humanoid + free UniRig already cover it), Offset Forge (free MIT repo exists), VFX Parade (packs ship free browsers). Building them is cheap but they won't sell.
- **The ONE real-margin play:** package YOUR lived methodology of running an AI agent fleet to ship a real game — as a **war-story teardown/cohort**, not a template pack (templates are crushed by free alternatives). It's the one thing that can't be cloned: your experience + judgment.
- **Cheapest validation (this week):** one public teardown post + a waitlist. If people sign up, it's a product. I can draft the post + landing copy whenever — you bring the stories.

## 📋 Captured for deliberate builds (not built blind)
- **WO-513** coordinated orc family (gang/flank/surround)
- **WO-514** tower cap (perf + anti-boxing-in) + Population->Saved Echoes->SP + the siege-AI insight (mobs should target towers, not only you)
- **WO-509 / moat note** — functional N/E/W seams + footprint-shrink (slider-tunable)
- HUD glyph clarity (the `+`=Iron / `>>`=quest-tab placeholders)

## Local git state
4 feature commits + 3 doc commits, all **local only** (not pushed). Run `git log --oneline -10` to see them.

---
**TL;DR:** It's live on itch — go play it in a browser. Vercel's still too big (needs a real art shrink, parked). The town is deeper, the blocker's gone, and you have an honest, focused path to your first dollar. Great session. ☀️
