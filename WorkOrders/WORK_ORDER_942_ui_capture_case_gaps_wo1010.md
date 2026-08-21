# WORK ORDER 942 — UI capture harness: two capture-case gaps left by the WO-1010 pass

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-09 (number from the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 941 -> 943 in the SAME edit as this mint and WO-941's)
**Lane:** Editor/regression tooling only (`Assets/Editor/UICaptureLaunch.cs` + at most one regression). No player code.
**Provenance:** found while judging the 2026-08-09 22:04 capture run against the WO-1010 wireframe.

---

## 1. The gaps

1. **`BuildGhostChips_padon_*` is byte-identical to `BuildGhostChips_edgeclamp_*`** (88555 / 108491 / 118402 bytes at the three sizes — the identical-file-size tell from `docs/UI_PLAYBOOK.md` §13.1). The `padon` case predates WO-1010 D12: there is no toggle any more, and the auto-showing analog stick (`BuildHudController.BuildNudgePad` -> `ElarionUiKit.BuildAnalogStick`) may not construct in edit mode, so the case photographs nothing distinct and proves nothing. Per §13: a capture that cannot draw the thing it captures launders an unverified state as verified.
2. **The D17 sprite-path invalid state (confirm chip dim to alpha 0.35 + `interactable=false`) has no assertion.** The worded refusal on the pill is captured (BuildGhostChips_blocked) but the chip's dim state is not measured anywhere; a regression on the icon's color/alpha at invalid, or a rewritten blocked-case capture note, closes it.

## 2. Acceptance criteria

- [ ] The `padon` case either (a) genuinely renders the nudge stick (assert its host is ACTIVE and its rect on-screen, edit-mode-safe) so its bytes differ from `edgeclamp`, or (b) is renamed/retired into `KnownUncapturable` WITH the reason string (an unexplained exemption is indistinguishable from an oversight — `UiCaptureCoverageRegression.cs` contract).
- [ ] An assertion exists for the sprite-path invalid verdict (icon alpha/interactable at `TrackGhost(valid:false)`), or the case documents why only a device screencap can prove it.
- [ ] `UI_CAPTURE_OK` + `UI_CAPTURE_FIDELITY_OK` still green; no real coverage removed.

## 3. What NOT to touch

- The shipped BuildHud code paths (they are correct; the CAPTURE CASES are what lag).
- The identical-size tell in the playbook — it worked; this WO exists because of it.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `UICaptureLaunch.cs:2525` — both capture gaps open. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
