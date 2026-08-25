# WORK ORDER 1199 - RESULT: guarded deploy-chain development slice

**Status:** PARTIAL - steps 1-8 code landed; live production and rollback proofs remain open.

## Landed

Commit `6a343fbdebe5ff1c3707b53e5c3a3fc089a7b281` adds:

- `tools/command-centre.ps1`, a PowerShell 5.1 production chain that judges fresh compile, regression, R2, and schema markers before deployment;
- rollback-target capture before promotion;
- preview deployment and protected candidate-index byte verification;
- production promotion of the same deployment id that was inspected and byte-proven;
- bounded production-alias polling for both promotion and rollback;
- a database-writing post-deploy proof and automatic verified rollback on its failure;
- named refusals and non-zero exits for failed gates and rollback outcomes;
- `test/command-centre.capture.test.ps1`, pinning complete native stderr/stdout capture.

The returned `vercel curl --no-color` blocker is removed. The candidate index is deleted before fetch, the fetch exit code is required to be zero, and a new output file must exist before bytes are read or hashed; a stale file can no longer manufacture `STEP_5_OK`.

This slice does not implement the later live-store, revenue-view, promotion-scheduling, or server-driven banner sections of the wider ticket.

## Local evidence

- `COMMAND_CENTRE_CAPTURE_OK stderr=2 stdout=1 exit=0` from `test/command-centre.capture.test.ps1`.
- PowerShell parser clean for the chain and capture test at lead verification.
- Source inspection confirmed the stale-index deletion, fetch exit/file checks, and absence of `--no-color` on `vercel curl`.
- `git diff --check` clean before landing.

## Still open - ops-owned, do not close WO-1199

A credentialed production executor must capture and retain evidence for:

1. one complete successful gate, preview, byte-proof, promotion, alias-poll, and database-write proof run;
2. `Builds/PROD_ROLLBACK.txt` written before promotion and naming the prior production deployment;
3. a deliberately failed pre-promotion gate refusing promotion with a named step, marker, and log;
4. a deliberately failed post-deploy database proof automatically promoting the captured rollback id, polling until production resolves to it, and exiting non-zero;
5. a secret scan of the run output proving no credential appears in logs, arguments, or committed files.

These are intentionally not inferred from source or the local capture harness. The success path and both required RED directions touch live Vercel/production state and remain the acceptance boundary.
