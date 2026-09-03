# WORK ORDER 1326 - The wolf is correctly coloured in the Pi/WebGL build and GREY in the APK and the exe

**Status:** FIXED
> Owner ruling 2026-09-02, verbatim: *"flat coat"* (and *"alduin is the wolf"*) — the coat map is now
> suppressed for this species on every target via an authored `flatCoat` row in `pets.json`, so all
> three builds paint the body with the species tint alone and agree; no art, no texture touched.
> RCA (unchanged) INVERTED the premise: `wolf_color` is ABSENT from the WebGL payload and PRESENT in the Windows
> and Android ones, so Pi looks right only because the (near-greyscale) coat map never shipped there —
> 26 base maps diverge; instrumentation + a parity oracle landed, the colour itself is an owner ruling.
> ⚠ The other **25** diverging maps remain OPEN and untouched: the owner has ruled on the wolf alone.
**Silo / Lane:** Content Delivery + Art pipeline (Addressables / platform texture overrides)
**Type:** EXISTING (built, renders correctly on ONE target)
**Minted:** 2026-09-02 (CLI) from a live owner observation across three builds.
**Severity:** P2 visible - a grey enemy on the two targets that matter most.

## Owner report (the whole value of this ticket)

> *"in the Pi build the wolf is perfectly colored, not in the apk or exe"*

## Why this is a strong ticket and not a vague one

It is a **DIFFERENTIAL**, and the differential is the diagnosis. One source tree, three build
targets, and the asset renders CORRECTLY on WebGL while failing on Android and StandaloneWindows64.

That rules out, without a single line of code being read:
- the source mesh, prefab and material assignment (identical across targets),
- the catalog/address wiring (same addresses, same authored rows),
- anything about the wolf's own authoring.

What it leaves is a **per-target divergence**, and there are only a few places one can live:
1. **Platform texture importer overrides** - an Android (ASTC/ETC2) or Standalone (DXT/BC) override
   that the WebGL platform does not carry. ⚠ Note `docs/releases/GOOGLE_PLAY_RC_2026-08-30.md`
   records a *"conservative Android texture pass"* that **reduced 65 eligible overrides** before the
   final rebuild. That pass is the leading candidate and must be checked FIRST.
2. **A texture missing from the Android/Windows bundle but present in the WebGL one** - the bundles
   are built per target and their contents can genuinely diverge.
3. **Shader variant stripping** per target, leaving the material on a fallback that samples no map.
4. **A max-texture-size / compression setting** that resolved the basecolor to nothing on the
   compressed targets.

## ⛔ INSTRUMENT AND MEASURE FIRST - do not guess (CLAUDE.md sec.12)

Static reading LOCATES; it never CONCLUDES. This ticket is unusually cheap to prove because the
healthy target is sitting right there as a control. **Diff the two, do not theorise about either.**

Concretely, before any edit:
- Find the wolf's material(s) and the texture(s) they sample. **Search by the common TOKEN**
  (`basecolor`, `diffuse`, `albedo`), NEVER by the name you expect - a name-first search can only
  confirm a guess, it cannot discover (memory `search-by-token-not-by-name`).
- Read the `.meta` for each of those textures and compare the `platformSettings` blocks for
  `Android`, `Standalone` and `WebGL` side by side. Quote them.
- Compare what the Android bundle and the WebGL bundle actually CONTAIN for that asset
  (`ServerData/Android` vs `ServerData/WebGL`). The parity log already proves both are hosted, so
  presence on the CDN is NOT the question - CONTENT is.

## Related captures (same family, already in the inbox)

- seq 4666, owner: `"no color"`
- `"The Echo hollow lost its color"`

If the root is a platform override or a stripped pass, it is very unlikely to affect only the wolf.
**Determine the BLAST RADIUS before fixing** - report how many other assets share the failing
setting. A one-asset patch to a systemic cause is the wrong fix.

## Acceptance

- [ ] The proving comparison is quoted verbatim: the differing setting or the differing bundle
      content, named per target.
- [ ] The blast radius is stated as a number, not an impression.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] An oracle pins the invariant if one is expressible (e.g. no enemy basecolor may be stripped on
      a platform where it is present on another). Prove it RED first.
- [ ] ⛔ **Owner felt-verifies on the device and CLOSES. The CLI does not close** (CLAUDE.md sec.13).
      A colour defect is a felt defect - and the owner is red/green colourblind, so describe the
      state in WORDS and never ask her to judge a hue.

## What NOT to touch

- Do not "fix" this by re-authoring the wolf's material or textures. The asset is proven good - it
  renders correctly on WebGL today.
- Do not run an R2 push or a content build. That is the lead's lane.
- Do not widen into the Synty duplicate-address work (WO-1305) or the enemy family resolution
  already landed in `95b75cf75`.
