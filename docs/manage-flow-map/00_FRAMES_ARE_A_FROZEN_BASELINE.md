# ⚠ THE PNGs IN THIS FOLDER ARE A FROZEN BASELINE. THEY ARE NOT THIS ROUND'S CAPTURE.

**Banner added 2026-09-06 (WO-1444). Do not delete the images; do not treat them as current.**

## What these files are

The 21 `ManageFlow_*.png` here were captured at **09:17 on 2026-09-06**, run `Builds/flowmap1`, and they
are the evidence base that `MAP.md`, `MAP.original.md`, `FIX.md` and **every** `WorkOrders/ManageRedesign/
WO-200x` file cites in its *"Measured facts this set is consistent with"* block. They are a dated,
point-in-time ledger and CLAUDE.md §15 freezes them: they are kept, not refreshed.

## Why this banner exists

`CAPTURE_LOOP_GOAL.md` step 3 said **"Output: `docs/manage-flow-map/`"**. That was never true.
`UICaptureLaunch.OutDir` is **`Builds/ui-capture/`** and no code in this repo has ever written a frame
into `docs/`. So on 2026-09-06 a capture ran at 14:59, wrote 21 fresh frames to `Builds/ui-capture/`,
and **these images did not move** — leaving a directory of *pre-redesign* screens sitting under a
filename convention that says "current", in the exact folder the standing loop tells a reader to open.

Anyone comparing a post-WO-2001 build against this folder would have been comparing against a build
from **before** the redesign and concluding it matched.

## Where the CURRENT frames are

```
Builds/ui-capture/ManageFlow_<TAB>_<state>_2670x1200.png     (BUILD / ARMY / RESEARCH)
Builds/ui-capture/Manage{Build,Army,Research,ResearchSchool}_<w>x<h>.png
```

Read the frame count off the log, never off a doc:

```
CAPTURE_LEDGER_SWEPT MANAGE_FLOW_MAP deleted=<n> expected=<n>
CAPTURE_LEDGER_CLOSED MANAGE_FLOW_MAP expected=<n> present=<n> failures=0
```

Those runs now **sweep before they shoot** and **hash afterwards**, so a frame that could not be
re-taken is absent (`CAPTURE_LEDGER_MISSING`) rather than stale, and two filenames holding one image
is a `CAPTURE_LEDGER_DUPLICATE` failure. Nothing in `Builds/ui-capture/` can outlive the run that
owns its name. Nothing sweeps THIS folder, which is why it needs a banner instead.

## The naming here is also historical

These filenames use the **four legacy tabs** (`Defense`, `Buildings`, `Troops`, `Research`) and a `Hub`
frame. WO-2001 collapsed the screen to **three** tabs — `ManageScreenVM.LegacyTabOf` maps *both* Defense
and Buildings to `ManageTabId.Build` — and deleted the hub chooser. So `ManageFlow_Defense_*` and
`ManageFlow_Buildings_*` in this folder are two names for one screen, and `ManageFlow_Hub_*` shows a
launcher the player can no longer reach. That is fine for a frozen record; it is not a target shape.
