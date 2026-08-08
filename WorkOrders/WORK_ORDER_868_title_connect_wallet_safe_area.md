> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 29c80b0b; SafeAreaInset.cs is a new file in the tree.
> The previous Status line read "Status: READY TO IMPLEMENT (small - can ride with any other 08-04 WO)" and was wrong; the board understated this.

# WORK ORDER 868 — Title: safe-area inset for "Connect Wallet" (+ do-NOT-crop the art)

**Status:** DONE
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** HUD/UI — the title/main-menu overlay. **WO#:** UI-seat block; **868**=this.
**Source:** `docs/ui-review/2026-08-04-seeker/README.md` §4 + `01-title-screen.png` (Seeker).

---

## 1. Bug — Connect Wallet is clipped off the top-right corner
`Connect Wallet` runs off the screen edge (not a capture crop). On a device with rounded corners + a camera cutout
this is worse. **Fix: apply a safe-area inset** so the button sits inside the device safe area (use Unity
`Screen.safeArea`; inset the top-right anchor by the safe-area margin). Verify the whole button is on-screen and
tappable at 2340×1080 on the Seeker.

## 2. ⚠ Do NOT "fix" the side bars by cover/crop
Thin black bars remain at left/right because the source art is **1.49 ratio vs the Seeker's 2.17**. Filling
edge-to-edge needs **new artwork at ~2340×1080 (owner/art), NOT a code change.**
**Do NOT switch to cover/crop** — the owner ruled **fit-to-screen on 2026-07-16**, and the **title text is baked into
the art**, so cropping cuts the title off. The pillarbox stays until new wide art lands. This WO is ONLY the safe-area
inset for the button.

## 3. Acceptance
- [ ] On the Seeker: `Connect Wallet` is fully on-screen (inside the safe area), clear of the rounded corner / cutout.
- [ ] The title art is STILL fit-to-screen (pillarboxed) — NOT cover/cropped. `CompileGate` green.

## 4. Do NOT
- Do NOT change the title art scaling mode to cover/crop (cuts the baked-in title text; owner ruling 2026-07-16).
- Do NOT author code to "fill" the bars — that's a new-wide-art task for the owner, out of scope here.
