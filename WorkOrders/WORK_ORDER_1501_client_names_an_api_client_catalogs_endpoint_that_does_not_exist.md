# WO-1501: the client names /api/client-catalogs, which does not exist, behind a flag no code registers

**Status:** READY TO IMPLEMENT
**Silo:** `Assets/_Modules/Core/Data/RemoteCatalogService.cs` + `api/`.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1501 -> 1502 in the same edit).

## 1. EVIDENCE

```
RemoteCatalogService.cs:114   targets /api/client-catalogs   -- no such route exists under api/
RemoteCatalogService.cs:174   Enabled requires SpecFor("catalog.remoteEnabled")
                              -- that spec is registered NOWHERE
```

So the service is dormant behind a flag nothing can turn on, pointed at a 404. It is the WO-1331 remote-retune
seam, half-built: the client half exists, the server half was never written, and the flag that would reveal
the mismatch cannot be set.

Dormant is not harmless - WO-1474 (Echo rates) is blocked on exactly this seam being real.

## 2. FIX SHAPE

Pick ONE and say which in the RESULT:

- **Implement**: add the `api/client-catalogs` route serving the canonical JSON, register
  `catalog.remoteEnabled`, and prove one catalog retunes end to end without a client build.
- **Delete**: remove `RemoteCatalogService` and the flag reference, and record in WO-1331 that the seam is not
  built, so the next lane does not plan against it.

Implement is the better outcome given WO-1474 and WO-1331 both want it; delete is acceptable and honest.

## 3. WHAT NOT TO DO
- Do not leave it dormant with a comment. A dead path that reads as a live seam is what produced this ticket.

## 4. ACCEPTANCE
- [ ] Either a live route with an end-to-end retune proven (request/response quoted), or the client path
      deleted and WO-1331 annotated.
- [ ] `node --test` green across `test/` if the route lands, with a case for it.
- [ ] `REGRESSION_OK n/n` on a fresh log.
