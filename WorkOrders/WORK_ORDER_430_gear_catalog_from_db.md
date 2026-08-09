# WORK ORDER 430 — Weapons/Armor catalog: seed JSON → DB, pull from DB (with local fallback)

> STALE: 2026-08-09 — §"The architecture reality" says the `api/*.js` backend "lives in a SEPARATE repo,
> not in `C:\EoA`". Both halves are wrong now: `api/` is **git-tracked IN THIS repo** (see `KEY_FACTS.md`
> "Backend / web"), and the repo root is **machine-dependent** (`C:\eoa` / `D:\eoa`) so no doc may name it.

**Status: SPEC / NEEDS DECISION + BACKEND REPO.** Not safe to blind-build in the Unity repo alone.
**Requested:** owner 2026-06-16 overnight — "load json to db for weapons and armor and have them
pull from db."
**Supersedes the parked note:** earlier this session we decided to *keep* the local-JSON catalog for
the demo ("maybe we don't load from DB"). This WO reverses that **only if** the fallback below keeps
the WebGL demo stable. Read §"Decision needed" before starting.

## The architecture reality (verified from code)
- The backend is a **Vercel serverless REST API** (`api/game/load.js` etc.), configured by env vars;
  see `Assets/_Modules/Core/State/ServerConfig.cs` (deserialized from the `config` block of
  `api/game/load`) and `PersistenceBridge` / `BackendAuthConfig`. It is **NOT a database the client
  connects to directly** — and it MUST stay that way: a WebGL build cannot open a DB socket, but it
  CAN `UnityWebRequest` a REST endpoint. So "pull from DB" = **fetch a REST endpoint** that reads the DB.
- **The `api/*.js` backend lives in a SEPARATE repo**, not in `C:\EoA`. The table + endpoint + the
  one-time "load json → db" seed all live there. This Unity repo can only build the **client half**.

## Two halves
### A. Backend repo (NOT this repo) — owner / backend session
1. Add `weapons` + `armor` tables (columns mirror `WeaponDef` / `ArmorDef` in `GearCatalog.cs`).
2. **Seed** them from the canonical JSON (`Assets/.../Canonical/weapons.json` + `armor.json`) — this is
   the literal "load json to db" step. A one-off script or migration; keep the JSON as the source of
   truth that the seed is generated from, so they never drift.
3. Add read endpoints, e.g. `GET /api/catalog/weapons` + `/api/catalog/armor`, returning the same
   shape the client already parses (`{ weapons: [...] }` / `{ armor: [...] }`). Cache/CDN them — the
   catalog is static-ish, so edge-cache to keep the WebGL demo fast.

### B. Unity client (THIS repo) — mechanical once the endpoint exists
- `GearCatalog.cs` already loads via `CanonicalJson.Read(path)` → `JsonConvert`. Add a **DB-first,
  JSON-fallback** path that is **non-breaking**:
  1. On boot, async `UnityWebRequest` the catalog endpoints (base URL from the existing backend config).
  2. On success → parse into the same `List<WeaponDef>/List<ArmorDef>` and use it.
  3. On failure / timeout / WebGL offline → **fall back to the committed local JSON** (today's exact
     behavior). The demo therefore NEVER hard-depends on the network for gear.
  - Keep `GearCatalog` SYNCHRONOUS callers working: prime the cache from local JSON immediately, then
    swap to DB data when the async fetch returns (fire a `GearCatalog.OnReloaded` so open panels refresh).
    Do NOT make `BestWeapon`/`FindWeapon` block on the network.

## WebGL / demo caveats (why this is gated on a decision)
- Adds a network dependency to gear. Mitigated by the local-JSON fallback — but the fallback must be
  bullet-proof (timeout small, parse-guarded) or a slow endpoint stalls the equip/store screens.
- The committed local JSON stays in the build as the fallback, so build size is unchanged.
- If the DB and JSON drift, players on fallback see different gear than DB players — keep JSON as the
  seed source so they can't drift.

## Decision needed (owner)
1. Do we want the **runtime** to pull from DB for the demo, or just **seed the DB now** and flip the
   client later? Recommendation: **seed the DB (A) now; land the client fallback path (B) but keep
   local-JSON authoritative for the demo** until the endpoint is proven fast + cached. Lowest risk,
   still "in the DB," demo stays stable.
2. Confirm the backend repo + who executes half A.

## What was deliberately NOT done tonight
- No client code written: building a fetch against a non-existent endpoint would be inert (always
  falls back) and risks a demo-breaking network dependency if mis-tuned. Per the work-order protocol
  + "don't smuggle structural changes / quality not fast," this is specced for a deliberate pass, not
  blind-built at 1am. The other three overnight items (direct-harvest mapping, base harvest nodes,
  static OuterWorld seam) WERE landed — see the session summary.
