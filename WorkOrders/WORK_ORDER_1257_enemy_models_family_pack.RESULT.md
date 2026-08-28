# WO-1257 Result — Enemy family content packing

## Verdict

Implemented and device-tested on the connected Seeker. The ticket is FIXED and awaits owner felt-test before closure.

## Source and build

- Packing/runtime implementation: `e0fd3e02f`
- Tester measurement probe: `2631b94e9`
- Startup deadlock corrections discovered during device testing: `dd3d9691b`, `b3467513`
- APK: `D:\eoa\Builds\Android\DefendersOfTheRealm.apk`
- Version: `2026.08.28.345445` (`versionCode=345445`)
- APK size: 507,596,741 bytes
- Defines: `TESTER_BUILD;WO1257_CONTENT_PROBE`
- Catalog: `catalog_2026.08.28.345445`

## Gates

- Compile: `COMPILE_GATE_OK`
- Full data regression: `REGRESSION_OK 318/318 suites`
- Content upload: `R2_PUSH_OK`
- Catalog/object verification: `R2_PARITY_OK 54 object(s) verified`
- Installed with `adb install -r`; application data was not cleared.

## Measured Seeker pull

Fresh runtime marker:

`WO1257_HOLLOW_PULL_OK beforeBytes=27637049 afterBytes=0 bundles=[enemy_art_assets_enemyfam-hollow_a0f90790cb845d17bd1cf3cc2d06bc3b.bundle, enemy_art_assets_enemyfam-shared_b9772cdf0b5da0c0f914a65d51d6ea0c.bundle, enemy_controllers_assets_enemyfam-hollow_e070d2b6dd29bc8500d4c8bd8cb71d6f.bundle, enemy_models_assets_enemyfam-hollow_5fa9d35d355d4d397698fae955573809.bundle, enemy_textures_assets_enemyfam-hollow_0e79ddbc813a1a4200d0b00d8035c964.bundle]`

The dependency list contains hollow art, controller, model and texture bundles plus the intentionally shared enemy-art bundle. It contains no orc, troll, or boss bundle. `afterBytes=0` proves the requested dependencies were resident after the download completed.

Current R2 hollow payload sizes reported by parity verification:

- model: 24.80 MB
- art: 1.14 MB
- controller: 0.40 MB
- texture: 0.02 MB
- shared art: 0.17 MB

There is no current catalog entry named `enemy_models_assets_all_*`.

## Device evidence

- Marker transcript: `Logs/device/wo1257-hollow-pull.txt`
- Screenshot: `Logs/device/wo1257-hollow-proof.png`
- Runtime stayed responsive on Title at approximately 59 FPS after the pull.
- The app was force-stopped after evidence capture to avoid unnecessary battery use.

## RCA found during acceptance

Two prior tester builds exposed startup black screens caused by synchronous Addressables catalog probes during bootstrap. Audio, VFX, hero, and hero-texture loaders now resolve resident Resources first and inspect only initialized in-memory resource locators. The registered `AudioStartupBoundedRegression` now covers all four loaders and passed in the 318-suite run.
