# WO-1578: Production WebGL deployment ships without /privacy and /terms pages

**Status:** READY TO IMPLEMENT
**Minted:** 2026-09-07 (web legal-pages copy fix; number from
CLI_LANES_WO_NUMBERS.md main-line banner, bumped 1578 -> 1579 in same edit)
**Silo:** Build / gates (tooling only - no gameplay, no scene, no content)
**Lane:** Web build output. File-disjoint from gameplay lanes.

---

## 1. The defect (proven)

The production build chain copies legal pages AFTER the production deployment:
- `build-webgl.ps1` (THE content build) copies nothing.
- `command-centre.ps1` step 5 (player build) copies nothing.
- `command-centre.ps1` step 6 (production deploy) ships the built tree with no /privacy or /terms.
- `web-ship.ps1` (~lines 85, 277-295) copies legal pages AFTER production deploy completes.

Result: defenders-of-the-realm-v2 served 404 on /privacy until manual production redeploy.

Evidence: `Builds/vercel-deploy-production.log` (no pages logged); manual PROD redeploy restored them.

## 2. Proposal

`build-webgl.ps1` (the canonical content build, step 5) copies `site/privacy.html` and
`site/terms.html` into `Builds/WebGL/` before the output is deployed.
`command-centre.ps1` step 2 (R2 parity) adds a check: before promoting the production domain,
verify `/privacy` and `/terms` exist in the candidate build output.

## 3. Acceptance criteria

1. `build-webgl.ps1` copies site/privacy.html → Builds/WebGL/privacy.html.
2. `build-webgl.ps1` copies site/terms.html → Builds/WebGL/terms.html.
3. Step 2 (R2 parity) verifies both files exist before emitting R2_PARITY_OK.
4. If either file is missing, emit R2_PARITY_LEGAL_MISSING and withhold OK.
5. Fresh log shows the copy operations and the verification query.
6. A fresh production build and deploy to staging recovers the files.

## 4. Scope guards

- Do NOT modify site/ files themselves.
- Do NOT add logic to web-ship.ps1; it runs AFTER production.
- Do NOT gate the STAGING deploy on legal pages; production only.
- Do NOT hardcode paths; use repo-root-relative paths via environment.

---

*Provenance: minted 2026-09-07 from overnight web ship. Evidence: Builds/vercel-deploy-production.log, manual verify showing 404.*
