# RESULT — WO-974 Addressables content build seam

**Status:** DONE — 2026-08-15 (verified + hardened)

## Already present

`AddressablesContentBuild.EnsureBuilt` called from Desktop (Win/WebGL), Android, WebGL build entry points.

## This pass

Each call now **aborts the player build** (`EditorApplication.Exit(1)`) when `EnsureBuilt` returns false — content failure cannot ship a green hollow player.
