# WORK ORDER 1124 — The APK builds its Addressables content for whatever target the editor was last on

**Status:** IMPLEMENTED 2026-08-20 — AWAITING PO CLOSE (§13). **All five acceptance criteria met.**
`BuildSeekerApk` now switches the active target to Android BEFORE the content build, passes
`BuildTarget.Android` to a new `EnsureBuilt(caller, BuildTarget?)` overload that HARD-FAILS on a
mismatch, and asserts `ServerData/Android/catalog_<bundleVersion>.bin` afterwards. §5.1 was proven by
REPRODUCING the failing case — the editor was genuinely on `StandaloneWindows64` and the log reads
`active target is 'StandaloneWindows64' — switching to Android` → `ADDRESSABLES_CONTENT_OK ... target=Android`
→ `ANDROID_CATALOG_OK ... catalog_2026.08.20.332839.bin`. §5.2 proven: `EnsureBuilt(expected=iOS)`
`correctly REJECTED while active=Android`. §5.3: `R2_PARITY_FAILED` now **exit 3**s — it previously only
wrote "DO NOT INSTALL" into a status file, which is advice, not a gate. New registered suite
`AndroidContentTargetRegression` (`ANDROID_CONTENT_TARGET_OK`). Gates: `COMPILE_GATE_OK`; DataRegression
**210/214** with the 4 known-red baseline and nothing new.
**Minted:** 2026-08-19 (CLI seat) — banner bumped 1124 → 1125 in the SAME edit
**Lane:** Release tooling. `Assets/Editor/AndroidBuild.cs` + `Assets/Editor/AddressablesContentBuild.cs`
+ `run-unity-method.ps1` / `overnight-apk-build.ps1`. No gameplay code, no scenes.
**Priority:** **HIGH — this is a store-push blocker.** It ships an APK whose content the CDN does not host.
**Provenance:** observed live, 2026-08-19 16:02-16:12 (CLI seat), on a clean gate-green tree.

---

## 1. WHAT HAPPENED, MEASURED

A Seeker APK was built via `overnight-apk-build.ps1` → `DeNelle.Editor.AndroidBuild.BuildSeekerApk`.
It reported success and it is a real, fresh, 476 MB APK stamped `2026.08.19.332462`.

**Its Addressables content was built for `StandaloneWindows64`.**

```
ServerData/Android/              newest catalog = catalog_2026.08.19.331367.bin   (2026-08-18 21:47)
ServerData/StandaloneWindows64/  newest catalog = catalog_2026.08.19.332462.bin   (2026-08-19 16:12)
```

`python tools/r2_sync.py --push ServerData` then uploaded **175.9 MB of Windows bundles** and printed
`R2_PUSH_OK 6 uploaded, 22 unchanged`. Every marker in the chain was green.

The device would have asked for `Android/catalog_2026.08.19.332462.bin`, which does not exist in the
bucket → **no buildings, no enemies**, silently, on a build that gated clean.

## 2. ROOT CAUSE — an ordering bug, not a missing step

`AndroidBuild.BuildSeekerApk` (`Assets/Editor/AndroidBuild.cs:72`) does the right thing in the wrong
order. It calls `AddressablesContentBuild.EnsureBuilt("AndroidBuild")` — WO-974 added that deliberately,
and its comment is correct: *"build Addressables content EXPLICITLY. Without this the bundles are rebuilt
only if an uncommitted per-machine Editor preference happens to say so."*

But `EnsureBuilt` runs **before** `BuildPipeline.BuildPlayer`, and it is `BuildPlayer` that switches the
active build target to Android. **Addressables builds for the ACTIVE target**, so content lands in
whichever platform folder the editor happened to be on — here `StandaloneWindows64`, left over from a
desktop build.

So the build is correct only by luck: it works when the editor was already on Android, and ships broken
content when it was not. **WO-974 closed the "content was never built" hole and left the "content was
built for the wrong platform" hole open.** Both produce the same symptom — a runtime that resolves
nothing — which is why this survived.

Related known trap, the mirror of this one (memory `desktop-build-after-android-target`): a desktop build
run after an Android build needs `-buildTarget Win64` or SBP/Addressables fails.

## 3. THE FIX

**Force the active target to Android BEFORE the content build, inside `BuildSeekerApk`** — not in a
wrapper script, so it holds for every caller (menu item, batchmode, CI, the ship chain):

```
EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
```
then `EnsureBuilt`, then `BuildPlayer`. A switch is a no-op when already on Android, so the fast path
stays fast.

**And make it impossible to be wrong silently** — this is the half that matters more than the switch:

1. `AddressablesContentBuild.EnsureBuilt` must **log the target it is building for and the folder it
   wrote to**, and **FAIL** if that target does not match the caller's intended platform. Pass the
   expected target in; a builder that cannot state which platform it just built for is the bug.
2. `AndroidBuild` must assert, after the content build, that
   `ServerData/Android/catalog_<bundleVersion>.bin` **exists**, and abort with a named error if not.
   The version is already stamped by `ApplyVersionStamp`, so this is a file-exists check against a known
   name — cheap, and it catches every future variant of this.
3. `tools/r2_sync.py --push ServerData` must **refuse to push a platform folder whose catalog version
   does not match the APK just built**, or at minimum print the version and platform of everything it
   uploads. Today it prints byte counts, which is why 175 MB of the wrong platform read as success.

## 4. WHY THE EXISTING GATES DID NOT CATCH IT

- `COMPILE_GATE_OK` — green. It compiles scripts; it knows nothing about platforms.
- `APK_OK` — green. It checks that an `.apk` file exists and reports its size.
- `R2_PUSH_OK 6 uploaded (175.9 MB)` — green, and **actively misleading**: it proves an upload happened,
  not that the APK's content is what got uploaded.
- **PROD-011's parity gate is the one that would have caught this** — it parses the built catalog's
  remote `m_InternalId`s and diffs them against the bucket. It shipped (`1eec315c7`) and produced
  `R2_PARITY_OK` on a live build, **but it was not run in this chain.** Wiring it into
  `overnight-apk-build.ps1` / `morning-ship-chain.ps1` as a mandatory post-push step is arguably the
  whole fix; §3 is what stops the bad content existing in the first place.

## 5. ACCEPTANCE CRITERIA

1. Build an APK from an editor whose active target is **Win64** and prove the content lands in
   `ServerData/Android/` with a catalog matching the APK's `bundleVersion`. This is the exact failing
   case; a fix that is only tested from an already-Android editor proves nothing.
2. Deliberately break it — point the build at the wrong platform — and prove the new assert **FAILS**.
   A gate that does not fail the known-bad state is not a gate.
3. `R2_PARITY_OK` runs as a mandatory step in the APK chain, after the push, before any install.
4. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` (read the count off the marker).

## 6. WHAT NOT TO DO

- **Do not "fix" this by remembering to switch the target by hand.** The whole failure is that a human
  step was assumed. It must be in `BuildSeekerApk`.
- Do not re-point Addressables to local or re-group anything. `m_DisableCatalogUpdateOnStart: 0` means
  installed APKs adopt the remote catalog at launch — re-pointing makes buildings invisible for every
  existing player, and re-grouping rehashes bundles into a full re-download for everyone. That ruling
  stands; do not re-litigate it.
- Do not delete the Windows bundles already pushed — they are what the desktop player resolves.
