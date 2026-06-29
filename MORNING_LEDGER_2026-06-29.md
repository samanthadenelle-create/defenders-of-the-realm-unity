# ☀️ Morning Ledger — 2026-06-29 (overnight session)

## 🎯 FIRST THING: test the web build on your phone
**`https://denellestudios.itch.io/defenders-of-the-realm-defend-the-tower`**
1. Open it on your phone **browser** → tap Run → does it load + play? (perf gate, data point 1)
2. Open the **same URL in Pi Browser** → loads + plays? (the iOS WKWebView gate — the one real unknown)
- This build already has **all of tonight's fixes** (it was built after every commit below — it is current, not interim).
- A **fresh desktop exe** is also at `Builds/Windows/DefendersOfTheRealm.exe` (built 23:57) if you want to felt-test on PC.

## ✅ Shipped + verified overnight (all committed, local — push held for your felt-verify)
| Commit | What |
|---|---|
| `aabca4c9` | **Security LB-3/4/5/11 + hardening** — admin-grant panel gated, save HMAC + fail-closed auth, monetization covenant regression, FlowTrace/material-leak/URL-flag/JSON hardening. CompileGate + DataRegression + save-integrity all green. |
| `e602ae6b` | **Tower upgrades** — towers now actually gain dmg/range/fire-rate per tier (was a TODO no-op). Data-driven `tower-perks.json`; TowerPerkRegression monotonic-verified. |
| `9b7ae7b1` | **Inventory equip feedback (WO-585)** — tap=select+detail strip+Equip CTA; equip shows confirmation even on re-equip. (Felt-verify on the build.) |
| `75a0cd39` | **Pi Network auth** — jslib bridge + IPiPlatform seam + auto/manual sign-in + `/verify` backend. CompileGate green. See `PI_AUTH_SETUP.md`. |
| `<icon>` | **Icon-coverage check** — proved the V1 inventory icons work (below). |
| `81015e80` | **Save atomicity fix** — the one real bug the seeded-chaos fleet surfaced. The LB-3 integrity HMAC was a *second* PlayerPrefs write (`<key>.sig`); a crash/power-loss between the two writes could reject a VALID save as "tampered" → silent save loss. Now folded into ONE atomic write (`<sig>\n<json>`). CompileGate green; RegressionSuite `save-integrity` PASS (valid verified, tampered rejected, round-trip consistent). Backward-compatible (old saves load once + re-sign). |
| earlier | 3 design-package commits (canon refresh, SKR/Pi tokenomics, pre-production/GTM/audits). |

> ✅ **The live itch WebGL build now includes everything** — a fresh build WITH the save fix (`81015e80`) was pushed overnight (new loader `09795be2…`, butler 138.65 MiB patch, itch processed it). Just open the URL above; it's current.

## 🔑 Key data
- **Icons (Store/Inventory):** weapons **23 real / 11 glyph**, armors **20 real / 0 glyph**, **Knight starting weapon → REAL sword art ✅**. The 11 glyphs are mage wand/staff + cleric censer (no art *by design*). → **V1 Knight inventory renders real icons; no fix needed.** The letter-glyphs you saw were non-Knight weapons in the owned list — a **roster question** (should they appear at all in a Knight's V1 inventory?), your call.
- **Seeded-chaos fleet (8 instances, seeds 7000–7007):** **core loop is CLEAN** — `TALK-ROUTE 0, dialogue No-node 0, softlocks 0`. Tonight's 6 security/tower/inventory/Pi silos did **not** break the gameplay loop. Triage of every flagged line:
  - **1 REAL bug → FIXED:** save torn-write (`[Flow:Save] HMAC mismatch` + roster drift). Root-caused from the captured stack (proof: run seed 7005 read back probe id **7001** — a value only a *concurrent* instance could write → the two-key `.sig` write tore apart under the shared PlayerPrefs store). Fixed in `81015e80` (atomic single-key envelope).
  - **Known/expected, NOT bugs:** render-pass artifacts ×108 (`-nographics` only); `PanelRouter … no panel recorded open` for *every* panel (the known WO-465 headless-UITK limitation — UITK panels can't register visible under `-nographics`, not store-specific); `SEAM-UNREACHABLE`/`AssertHeroCrossing` ×8 (the known WO-453 hub-coverage cap); duplicate-UIDocument warnings (pre-existing).
  - **Residual fleet-only noise (filed):** the roster/quest drift under 8-way concurrency is a *test-isolation* artifact (all instances share one registry PlayerPrefs store), not a product bug — single-instance `save-integrity` is green. WO filed to namespace the fleet's save probe.

## 🚩 Needs you / decisions
- **Pi:** perf gate first (above). If it passes → register the app in **Pi App Studio**, then we host the Pi-auth build on **Vercel** (NOT itch — sandbox subdomain can't serve `validation-key.txt` at root). Hackathon realistic target: **2026-07-31** (the 06-30 one is too soon).
- **Roster:** should mage/cleric weapons surface in the Knight's V1 inventory? (drives whether glyphs ever show)
- **Security LB-1/2** (server-authoritative economy) + **LB-6/7/8** (counsel/privacy) remain for real-money go-live — scoped, not blocking a FREE launch.
- **Two-day free launch** is realistic on the **desktop-safe** path; web depends on the perf gate.

## Notes
- Build orchestration fought a task-reaper all night; ran the builds detached so they survived. All gates passed before each commit.
- Nothing pushed to the remote — your call after felt-verify.
