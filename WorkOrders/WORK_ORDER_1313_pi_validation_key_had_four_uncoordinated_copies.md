# WORK ORDER 1313 — The Pi validation key lived in four uncoordinated copies; the tracked one was 7 weeks stale

**Status:** DONE
**Silo:** Web / Pi
**Minted:** 2026-09-02 (CLI) while investigating the owner's Pi validation failure.
**Severity:** P1 — every WebGL build silently reverted the live Pi validation key.

## Owner report, verbatim

> *"were you able to tell why Pi was failing? ... seems to fail to validate in thier tool"*
> *"im guessing match on r2"*

## What was actually true — measured, not theorised

The key existed in FOUR places, and they did not agree:

| copy | key | mtime |
|---|---|---|
| **served live** (`echoes-of-elarion.vercel.app/validation-key.txt`) | `79ec2d03…2c146dbd` | — |
| `Builds/WebGL/validation-key.txt` | `79ec2d03…2c146dbd` | 2026-09-01 05:31 |
| `Assets/WebGLTemplates/Pi/validation-key.txt` (**the tracked source**) | `fef2a223…d43e6743` | **2026-07-14** |
| `Builds/Distribution/(Pi)/validation-key.txt` | `fef2a223…d43e6743` | 2026-08-30 |
| `.vercel/output/static/validation-key.txt` | `fef2a223…d43e6743` | 2026-08-28 |

**The direction is the opposite of the intuitive one.** The *build output* was correct and the
*tracked template* was stale — someone hand-placed the current key into `Builds/WebGL/` on 09-01 and
deployed it, without promoting it back into the template.

## Why this was load-bearing, and why it had to be fixed BEFORE any build

Unity **overwrites `Builds/WebGL/` from `Assets/WebGLTemplates/Pi/` on every WebGL build.** So the
next build would have replaced the live, working `79ec2d03` key with the July `fef2a223` one, and the
next Vercel deploy would have served the stale key. The act of rebuilding to fix Pi would itself have
broken Pi validation — and it would have looked like the rebuild caused a *new* failure.

⚠ **This is the same duplicated-state class as CLAUDE.md sec.2's stale WO block and sec.5's retired
dependency table:** a value copied into a second location, where every copy silently goes stale.

## What was NOT the cause — ruled out with captured data, so nobody re-theorises it

The owner's hypothesis was an R2 mismatch. **It is ruled out:**
- `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=228` on a fresh log (`Builds/r2-parity.log`, 2026-09-02 03:34).
- The live `ServerData/WebGL` catalog + bundles return **HTTP 200** from the CDN.
- R2 serves `Access-Control-Allow-Origin: *`, so a browser cross-origin fetch is permitted. **CORS is not the problem.**
- (`catalog.json` 404s on every target — that is EXPECTED. Real catalogs are `catalog_<version>.bin`.)

## What was done

Promoted the live key to all four locations, byte-verified (`cmp` against the served file; 128 bytes,
no trailing newline) before promoting.

## Follow-up that is NOT done — read this before closing the class

1. **`.vercel/output/static/` is a landmine.** It is a Build-Output-API artifact from 08-28. `vercel.json`
   sets `outputDirectory: Builds/WebGL`, so a normal deploy ignores it — but a `--prebuilt` deploy would
   serve THAT tree instead, and it silently held the stale key. Consider deleting it outright.
2. **There is no regression guarding this.** A test asserting the template key equals the deployed key
   cannot run offline. The honest guard is narrower: assert the four in-repo copies are byte-identical.
3. **The portal is still the unverified authority.** Nobody has confirmed `79ec2d03` is what the Pi
   Developer Portal shows. Syncing to the LIVE value is strictly non-regressive (it preserves today's
   behaviour), but if the portal shows a third key, all four copies are wrong together.

---

# ⚠ CLOSED 2026-09-02 — DOMAIN VALIDATION WAS NEVER FAILING. Stop investigating it.

**Owner, decisive:** *"it validated otherwise wouldnt show as published"* — the Pi Developer Portal
shows the app as **Published**, with the App Link `https://echoes-of-elarion.vercel.app/` registered
and greyed out ("cannot be changed after registration"). Publication REQUIRES validation to have
passed. So the key was accepted.

Corroborated independently at the registered URL:
```
https://echoes-of-elarion.vercel.app/validation-key.txt -> 200, 79ec2d03...2c146dbd, 128 chars
https://echoes-of-elarion.vercel.app/                   -> 200, 26,443 bytes, unity-canvas present
```

## Two dead ends recorded so no future session re-walks them

1. **`echoesofelarions6578.pinet.com` serving a Next.js shell is NOT a misconfiguration.** It responds
   `x-middleware-rewrite: /tpa/echoesofelarions6578/`, `x-powered-by: Next.js`, title "Echoes of
   Elarion", and 404s `/validation-key.txt`. That is **Pi's own wrapper page**. I read it as a broken
   portal setting and said so; the App Link was correct all along. Pi validates at the APP LINK, not
   at the pinet host.
2. **The `fef2a223` vs `79ec2d03` question is MOOT.** It mattered only under the theory that validation
   was failing. It was not. (`fef2a223` is still served by the stale `defenders-webgl` project — that
   remains worth retiring under WO-1316, but it is not a Pi blocker.)

## What the owner's ORIGINAL report actually was

> *"it did before but now i cant get the authentication to work"*

**AUTHENTICATION, not validation.** The diagnosed cause stands unchanged and is fixed in WO-1317: the
client shipped `PiInit(sandbox=True)` against a MAINNET portal app. Nothing in this closure weakens
that finding.

**The one remaining proof:** a Pi Browser session on the phone showing `PiInit(sandbox=False)` followed
by `Signed in as ...`. Every session captured on 2026-09-02 so far is desktop Chrome on the vercel URL
(`device='Chrome ...'`, `WebGL host is not Pi Browser`), which cannot exercise Pi auth at all.
