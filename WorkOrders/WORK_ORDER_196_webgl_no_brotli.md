<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-196: Rebuild WebGL without Brotli for itch.io

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (itch.io build broken — Brotli loading error)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** WO-211  
**Can Run In Parallel:** None — **DO THIS FIRST**  

---

## Problem

WebGL build deployed to itch.io fails to load:
```
Uncaught undefined at WebGL.framework.js.br9
```

**Root cause:** The `build-webgl.ps1` script output Brotli-compressed files (`.wasm.br`, `.js.br`, `.data.br`). These require HTTP headers (`Content-Encoding: br`) to decompress in the browser. **itch.io doesn't serve these headers** — it's a static file host, not Vercel. The browser tries to double-compress or fails to load the `.br` files.

**Vercel was the original plan**, but Brotli compression was pushed to itch.io anyway. itch.io needs uncompressed or standard gzip.

---

## Solution

Rebuild `Builds/WebGL/` **without Brotli compression**:

```powershell
# In C:\Users\Kayden-Laptop\Documents\defenders-unity\
Remove-Item -Recurse -Force Builds/WebGL
& .\build-webgl.ps1 -NoBrotli
```

The `-NoBrotli` flag (or equivalent in your build script) outputs:
- `WebGL.wasm` (uncompressed or gzip)
- `WebGL.js` (uncompressed or gzip)
- `WebGL.data` (uncompressed or gzip)

No `.br` files. Delete any lingering `vercel.json` from the output since it won't be used.

---

## Files to touch

- **Delete:** `Builds/WebGL/vercel.json` (Brotli headers, not needed for itch.io)
- **Build:** `Builds/WebGL/` (all contents)
- **Result:** Commit the uncompressed WebGL folder

---

## Acceptance criteria

- [ ] `build-webgl.ps1` runs with `-NoBrotli` (or inherent default)
- [ ] No `.wasm.br`, `.js.br`, or `.data.br` files in output
- [ ] `Builds/WebGL/index.html` + `Build/` folder present + no `.br` files
- [ ] `Builds/WebGL/vercel.json` deleted
- [ ] New zip: `WebGL_noBrotli.zip` of `Builds/WebGL/` contents
- [ ] Commit + push

---

## Post-build (UI will handle)

1. **Delete** current itch.io upload (`WebGL.zip`)
2. **Upload** new `WebGL_noBrotli.zip` to itch.io
3. **Test:** Browser loads game, presses F1 for dev portal

---

## Why

itch.io is a static file host. It doesn't parse HTTP response headers for pre-compressed content. Brotli `.br` files need either:
- A server that sends `Content-Encoding: br` (Vercel ✓, itch.io ✗)
- OR uncompressed files

Uncompressed = 186 MB initial download (vs. 45 MB Brotli on Vercel). itch.io can handle it; first-load hit is acceptable for testing phase.

---

**Notes for CLI:**
- Check if `build-webgl.ps1` has a flag or if you need to modify the Unity build settings to disable Brotli.
- Paste the output folder name + any build errors → I'll advise on next steps.
