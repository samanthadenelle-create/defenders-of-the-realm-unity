# WORK ORDER 944 — Placing: the item's title pins STATIC at the top of the screen

**Status:** IMPLEMENTED (same hour as the flag — capture-verified 2026-08-09 22:32; owner felt-verify pending)
**Minted:** 2026-08-09 (number from the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 944 -> 945 in the SAME edit)
**Lane:** Build HUD presentation (`BuildHudController.cs` only).
**Provenance:** owner F8 seq 2250, 2026-08-09 22:24, flagged live in the fresh 22:11 build (verbatim):
*"can we make the title of the item pin staticl maybe at the top of the screen"*. Captured during a
pet-house placement (`[Flow:BuildHud] placed -> returned to carousel` in the same trace).

## 1. The change

The name+cost pill currently FOLLOWS the ghost's projected screen point (the one remaining
follower after D14). The owner wants it STATIC: pin the pill top-centre of the screen during
Placing — a fixed band like every other piece of the WO-1010 chrome.

- Fixed pixels at the 1920x1080 reference; top-centre, clear of the corner Done's hit pad.
- The pill keeps everything it already carries: name + cost, and the appended blocked reason in
  words (colourblind law unchanged).
- This removes the LAST follow behaviour — `docs/UI_PLAYBOOK.md` §8's own preferred answer
  ("if a control does not have to follow, do not make it follow"). The follow/clamp code path
  retires; the OK-verdict state logic stays.
- The capture case that proves edge-clamping (`BuildGhostChips_edgeclamp`) loses its subject —
  note it in WO-942's scope (the case should assert the STATIC position instead).

## 2. Acceptance

- [ ] During Placing the title pill sits top-centre, never moves with the ghost.
- [ ] Blocked reason still appends in words; verdict behaviour unchanged.
- [ ] `COMPILE_GATE_OK` + capture re-run; PLACE PNGs show the pinned pill; brace/NUL clean.
- [ ] Owner felt-verify (she is the one who asked mid-play).
