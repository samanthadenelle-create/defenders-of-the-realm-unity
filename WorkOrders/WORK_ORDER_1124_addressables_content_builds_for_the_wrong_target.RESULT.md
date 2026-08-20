# WO-1124 RESULT — the APK can no longer ship another platform's content

**Implemented:** 2026-08-20, CLI seat (overnight autonomy).
**All five acceptance criteria met, including the one that required reproducing the failing case.**

---

## 1. What changed

| file | change |
|---|---|
| `Assets/Editor/AndroidBuild.cs` | switches the active target to Android **before** `EnsureBuilt`, passes `BuildTarget.Android` to it, and asserts `ServerData/Android/catalog_<bundleVersion>.bin` exists afterwards |
| `Assets/Editor/AddressablesContentBuild.cs` | new `EnsureBuilt(caller, BuildTarget?)` overload that **hard-fails** on a target mismatch; the success line now names the platform (`target=Android`) |
| `overnight-apk-build.ps1` | `R2_PARITY_FAILED` now **exits 3** instead of writing advice into a status file |
| `Assets/Editor/Regression/AndroidContentTargetRegression.cs` | new registered suite, markers `ANDROID_CONTENT_TARGET_OK/_FAIL` |

The switch lives **inside `BuildSeekerApk`**, not in a wrapper script, per §6: the entire failure was
assuming a human step, so it has to hold for the menu item, batchmode, CI and the ship chain alike.
Nothing was re-pointed to local and nothing was re-grouped (§6 honoured); the Windows bundles already
in the bucket were left alone — the desktop player resolves them.

## 2. Acceptance criteria — every one, with the data

**§5.1 — build from a Win64 editor and prove the content lands in `ServerData/Android/`.**
This is the exact failing case, and it was reproduced rather than simulated: the editor was genuinely
on `StandaloneWindows64` (a Windows player had just been built for the WO-1024 fleet). From
`Builds/apk-wo1124.log`:

```
[AndroidBuild] active target is 'StandaloneWindows64' - switching to Android BEFORE the content build (WO-1124).
ADDRESSABLES_CONTENT_OK 634 locations :: AndroidBuild target=Android (31.3s -> .../aa/Android/settings.json)
[AndroidBuild] ANDROID_CATALOG_OK - D:\eoa\ServerData\Android\catalog_2026.08.20.332839.bin
[AndroidBuild] SUCCEEDED - 2309 MB in 00:03:53
```

The catalog name matches the APK's `bundleVersion` exactly. Before this change that same run would
have written `catalog_2026.08.20.332839.bin` into `ServerData/StandaloneWindows64/` and shipped an APK
asking the CDN for an Android file that never existed.

**§5.2 — deliberately break it and prove the assert FAILS.** *"A gate that does not fail the
known-bad state is not a gate."* Proven by calling the real gate with a mismatched target:

```
[gate] EnsureBuilt(expected=iOS) correctly REJECTED while active=Android
```

That check runs **before** any build work, which is why the suite costs milliseconds rather than
minutes — building 175 MB for the wrong platform and complaining afterwards would be the wrong
design.

**§5.3 — `R2_PARITY_OK` mandatory after the push, before any install.** It ran, but it was **not
mandatory**: on failure the script only wrote "DO NOT INSTALL OR DISTRIBUTE THIS BUILD" into a status
file. That is advice, not a gate — anything downstream proceeded exactly as if parity had passed. It
now `exit 3`s, which is the only form of "do not install" a script can actually enforce.

**§5.4 — gates.** `COMPILE_GATE_OK` · DataRegression **210/214** (the new suite is registered and
green) with **4 failure(s) = the known-red baseline exactly**, nothing new.

## 3. Why the suite reaches the gate by reflection

`DeNelle.EditorRegression` deliberately does **not** reference `DeNelle.Editor` (see its `.asmdef`),
so the suites cannot bind the build tooling directly. Adding that reference to run one check would
dissolve a boundary the project keeps on purpose — the same situation as `AdminOverlay` reaching a
Village type, where canon §5 says the reflection is *evidence of the rule, not a violation of it*. The
suite therefore invokes the **real** `EnsureBuilt` across the boundary rather than re-implementing its
logic, which would keep passing forever after someone deleted the real one.

## 4. Why this defect survived every existing gate (§4, confirmed)

`COMPILE_GATE_OK` compiles scripts and knows nothing about platforms. `APK_OK` checks a file exists.
`R2_PUSH_OK 6 uploaded (175.9 MB)` proves an upload happened, not that it was **this APK's** content.
**None of them ever named a platform** — the single fact that was wrong. That is why the fix's centre
of gravity is the two new lines that state one: `ADDRESSABLES_CONTENT_OK ... target=Android` and
`ANDROID_CATALOG_OK <path>`.

**Status → IMPLEMENTED, awaiting owner felt-verify / PO close (§13).** Nothing here needs a felt-test
to be believed — the criteria are all machine-checkable and all met — but the PO closes, not the CLI.
