# WORK ORDER 1326 — RESULT

**Status:** FIXED (instrumentation + oracle). **Owner felt-verifies and CLOSES** (CLAUDE.md §13).
**Worked:** 2026-09-02. **Silo:** Content Delivery + Art pipeline.
**Owner ruling owed:** one — see §6. The owner is red/green colourblind; §6 is stated in WORDS and
asks about *behaviour*, never about a hue.

---

## 1. The headline: the ticket's premise was backwards, and the measurement says so

The WO reasoned that WebGL is the healthy control and Android/Windows carry a defect. **The opposite
is true.** The Pi/WebGL build looks right because it is **missing** the wolf's base-colour texture;
the APK and the exe look grey because they **have** it, and the authored coat map is near-greyscale.

> **The healthy-looking target is the DEGRADED one.** Nothing was wrong with the two "broken" targets'
> material pipeline — they bind exactly the asset they were told to bind.

This is why every candidate the ticket listed was checked and every one came back clean (§4).

---

## 2. The proving comparison, verbatim

### 2a. Cross-payload presence — the divergence itself
Three shipped payloads from ONE source tree, probed by raw byte search for the authored file stem
(the one probe that works across all three container layouts):

```
[parity] repo root: D:\eoa
[parity] authored base-map stems considered: 579
[parity] Android  104 map(s) present
[parity] WebGL    82 map(s) present
[parity] Windows  100 map(s) present
[parity]   wolf_color                                   present=Android,Windows  ABSENT=WebGL
PAYLOAD_TEXTURE_PARITY_FAIL 26 map(s) diverge across 3 payload(s)
```

The per-payload receipts behind that line:

```
Builds/Windows/DefendersOfTheRealm_Data/resources.assets     wolf_color   x1
Builds/Windows/DefendersOfTheRealm_Data/resources.assets     Simple Wolf  x1
Builds/Android/DefendersOfTheRealm.apk
    assets/bin/Data/df1e97358d9ba6b44b9c740deae49e55         wolf_color   x1
    assets/bin/Data/818b265b42cbb35419be79713bf332c6         Simple Wolf  x1
Builds/WebGL/Build/e1e3fcce12ebd0ccca5a2cf9a6c72035.data.unityweb
    (brotli-decompressed, 209,534,992 bytes)
        wolf_ao      x1   <- length-prefixed object record  b'\x07\x00\x00\x00wolf_ao\x00'
        wolf_normal  x1   <- length-prefixed object record  b'\x0b\x00\x00\x00wolf_normal\x00\x00'
        wolf_body    x1
        wolf_color   x0   <- ABSENT
        Simple Wolf  x0   <- ABSENT
```

Control for the probe (so "absent" is not a broken search): in the same WebGL payload,
`WeaponSmith_basecolor` x1, `Armourer_basecolor` x1, `Coyote_Mesh_Bake_Pbr_Diffuse` x1,
`Frosthowl` x2, `enemy_outpost` x9. The scan finds base maps; it does not find this one.

### 2b. The broken target's own runtime line — it binds the map correctly
From the Windows player's log (`.../LocalLow/DeNelle/Echoes of Elarion/Player-prev.log`, run whose
`Mono path[0] = 'D:/EoA/Builds/Windows/DefendersOfTheRealm_Data/Managed'`), line 45943:

```
[Flow:TripoMatFix]   albedo BOUND on 'ice-wolf(Clone)' renderer 'wolf_body' slot 0: material='Simple Wolf (URP)' tex='wolf_color' tint=(0.81,0.91,1.00).
[Flow:TripoMatFix]   ice-wolf(Clone): VERIFY OK — all 1 slot(s) on a URP shader (no magenta/error); 0 slot(s) with NO albedo bound.
```

`tint=(0.81,0.91,1.00)` is `#cfe9ff`, the `ice-wolf` `"tint"` authored in
`Assets/Resources/Data/Canonical/pets.json` — an exact match, so the path is confirmed end to end.

### 2c. The asset the broken targets bind is a grey coat
Pixel measurement of `Assets/Animals/Low Poly Animals/Textures/wolf_color.png` (1024x1024, RGBA),
opaque pixels only:

```
wolf_color.png   mean saturation 0.091   p90 0.099
                 dominant clusters (160,160,160) (128,128,128) (128,160,160) (160,192,192)
Frosthowl.png    mean saturation 0.469   p90 0.705      <- the Echo's portrait art, for contrast
```

### 2d. Therefore
| Target | `wolf_color` in payload | What `TripoMaterialFixer` resolves | What the player sees |
|---|---|---|---|
| WebGL / Pi | **absent** | `tex = null`, so `_fallbackTint` **is** the albedo | clean icy-blue wolf — "perfectly colored" |
| Windows exe | present | grey map bound, tint **multiplies** it down | grey wolf |
| Android APK | present | grey map bound, tint **multiplies** it down | grey wolf |

One line of code (`if (_hasFallbackTint) col = _fallbackTint;` in `TripoMaterialFixer.Run`) produces
two opposite looks, and which one you get is decided purely by whether a texture reached the payload.
A tint multiplied onto a bound map can only remove saturation; it can never add any.

---

## 3. Blast radius — as a number

**26 base-colour maps diverge across the three shipped payloads.** Not one.

- **22** present on Android **and** Windows, **absent** from WebGL. `wolf_color` is one of them; so is
  `tripo_mat_f9576211_Pbr_Diffuse`, which is the **flame-pup pet's** diffuse — the same failure one
  species over. Also `Paladin_diffuse`, `assetstore_fantasy_knight_head_diffuse`,
  `assetstore_fantasy_mage_body_diffuse`, `assetstore_fantasy_mace_diffuse`,
  `assetstore_fantasy_staff_heroes_diffuse`, `enchantedtree3dmodel_basecolor`,
  `raggedfantasydwarf3dmodel_basecolor`, `T_Merchant_Base_color`, `Grass_Albedo`,
  `Stoneback_Rock_BaseColor`, `Rock Icicle_Albedo`, `Tome_Dark_Albedo`, `Decal 8_Crater_Albedo`,
  `FireFlyAlbedo`, `GoopStreakAlbedo`, `RippleAlbedo`, `SandDecalAlbedo`, `WoodBulletHoleAlbedo`,
  `Mushroom_Albedo_Purple`, `Mushroom_Albedo_Yellow`.
- **4** present on Android **and** WebGL, **absent** from Windows: `Orc_Mage_basecolor`,
  `Orc_Tank_basecolor`, `Orc_Warrior_basecolor`, `TreeofLife_basecolor`. The exe is missing three
  orc base maps the other two targets ship — a second, opposite-direction instance of the same class,
  found by the same sweep.

Wider context, same measurement, all authored texture stems rather than base maps only: **621**
texture names ship in the Windows player payload and are absent from the WebGL one (mostly UI
atlases, which is expected content reduction). The 26 above are the subset that decides how a
*model* looks, which is why the base-map subset is the number that binds.

---

## 4. Every candidate the ticket named, checked and cleared (with the measurement)

1. **Platform texture importer overrides — the ticket's leading candidate. FALSE, measured.**
   `wolf_color.png.meta` carries `overridden: 0` on **both** `Android` and `Standalone`; only the
   `WebGL` block is `overridden: 1` (maxTextureSize 512, textureFormat 29 = DXT5Crunched,
   crunchedCompression 1). The `GOOGLE_PLAY_RC_2026-08-30.md` "conservative Android texture pass that
   reduced 65 eligible overrides" **never touched this texture** — 0 of the 65 apply. Project-wide
   the override census is Android 2253/7286 overridden, Standalone 48/7300, WebGL 7184/7236; every
   Android override is format `50` (ASTC_6x6, from commit `b53b034ba`) or `48` (ASTC_4x4), both fully
   colour-bearing. No importer setting strips colour on any target.
2. **A texture missing from a bundle.** Not applicable — the wolf is not addressable. It loads via
   `Resources.Load<GameObject>("Pets/ice-wolf")` (`PetDeployer.TryLoadPetMesh`), and the prefab
   references `Assets/Animals/Low Poly Animals/Simple Wolf/wolf.fbx` + `Simple Wolf.mat`. `grep`
   over `Assets/AddressableAssetsData/AssetGroups/*.asset` returns **zero** wolf addresses.
3. **Shader variant stripping.** Cleared: `GraphicsSettings.asset` has `m_LightmapStripping: 0`,
   `m_FogStripping: 0`, `m_InstancingStripping: 0`, and the same `m_CustomRenderPipeline` guid
   (`e5e96b82…`) on all three quality tiers. The Windows log's `VERIFY OK — all 1 slot(s) on a URP
   shader (no magenta/error)` is positive proof the shader survived on the "broken" target.
4. **Quality tier / max-size.** Cleared: `SeekerBootstrap.Init` sends WebGL and Windows to the **same**
   `Desktop` tier (non-Android, non-mobile), evidenced in the exe's own log —
   `[SeekerBootstrap] device='ROG STRIX G16CHR…' platform=WindowsPlayer isSeeker=False -> tier='Desktop'`.
   Two targets on an identical tier cannot diverge because of it.
5. **Stale build under test.** Cleared: the albedo-pin fix `0fb7055cc` landed 2026-08-30 01:48 and the
   wolf body itself `f52963659` on 2026-08-10; the exe is 2026-09-02 06:05 and the APK 2026-09-02
   19:33. Both postdate both.
6. **Incidental, worth the lead's eye:** 87 of the 102 non-catalog files in `ServerData/WebGL` are
   byte-identical filenames to `ServerData/StandaloneWindows64` ones. Bundle names are content-hashed,
   so identical names across targets means identical content — i.e. the WebGL bundle folder is still
   largely holding Windows content. That is the shape `b649917ae` ("WebGL built WINDOWS content —
   occurrence five, and six was already in the tree") describes. **Not the cause of this ticket** (the
   wolf is not addressable) and deliberately not widened into here.

---

## 5. What changed

| File | Change | Brace / syntax check |
|---|---|---|
| `Assets/_Modules/Pets/PetDeployer.cs` | The ice-wolf albedo pin no longer passes a null straight into `SetForcedSourceTexture`. A pin that resolves nothing is now a `FlowTrace.Fail` naming the parity tool; a pin that resolves logs the texture it pinned. The RCA is recorded in-code at the seam. | `BALANCED clean` |
| `tools/payload-texture-parity.py` | **NEW.** The oracle (§6). | `python parses OK`, `clean` |
| `WorkOrders/WORK_ORDER_1326_*.md` | Status → FIXED with the one-line inversion note. | n/a |

Nothing else. No `.unity` scene touched, no reflection added, no asmdef edited.

---

## 6. The oracle, proven RED first

`tools/payload-texture-parity.py` pins the invariant the WO asked for: **a base-colour map present in
one shipped player payload must be present in every other shipped player payload.** It judges nothing
about how a texture looks — colour is the owner's call, presence is ours. It needs no Unity: it reads
the built payloads directly (Windows `*_Data`, the APK's `assets/bin/Data/`, and the brotli-wrapped
WebGL `.data`), and it is marker-judged, never exit-code-judged.

**RED proof — the mutation, run both ways** (`python tools/payload-texture-parity.py --self-test`):

```
[self-test] GREEN case (same maps in both payloads):
PAYLOAD_TEXTURE_PARITY_OK 2 map(s) verified across 2 payload(s)
[self-test] RED case (one payload missing wolf_color):
[parity]   wolf_color                                   present=A  ABSENT=B
PAYLOAD_TEXTURE_PARITY_FAIL 1 map(s) diverge across 2 payload(s)
[self-test] PASS - the oracle distinguishes parity from divergence.
```

The mutation is the removal of `wolf_color` from one payload's map set — the exact defect. GREEN
before it, RED after it, and it recovers GREEN when the map is restored. A checker that only ever
prints FAIL is worth nothing and one that only ever prints OK is worse
(memory `prove-the-success-path-not-just-the-refusal`), so both directions are exercised.

**RED on the real tree, today:** the run in §2a. It named `wolf_color` unprompted, which is the
strongest available evidence that it would have caught this ticket before the owner saw it.

⚠ It is a **`tools/` pre-ship check, deliberately NOT registered in the regression suite**, because it
is red against the current artifacts and a red suite entry would block every unrelated lane. The
lead's call whether it joins the `R2_PARITY_OK`-class markers in the ship chain once the 26 are
resolved.

---

## 7. Gates

Not run, by instruction: no Unity batchmode gate, no content build, no R2 push, no commit, no
`git add`. Those are the lead's lane. `COMPILE_GATE_OK` and `REGRESSION_OK <n>/<n> suites` on fresh
logs are therefore **still owed** on the `PetDeployer.cs` edit before anything ships.

---

## 8. What is owed from the owner — one ruling, stated in words

The wolf's authored coat map is a **neutral grey-to-grey-blue** image: mean colour intensity 0.09 on a
0–1 scale where 0 is pure grey, with 90% of its pixels at 0.10 or below. The pale species tint
`#cfe9ff` from `pets.json` is *multiplied* over it, and multiplying can only take colour away, never
add it. So:

- On Pi you are seeing **tint only** — one flat, clean, pale icy body, no coat pattern.
- On the APK and the exe you are seeing **the painted coat**, which is a grey-blue wolf with visible
  fur shading, dimmed slightly by that tint.

Both are "working"; they are two different looks, and the build target is currently choosing between
them by accident. **Which one is Aldwin?** Answer in behaviour terms if that is easier — *"the flat
clean one"* vs *"the one with fur markings you can make out"*.

- If **flat/clean** is Aldwin: the fix is to stop binding that coat map for this body on every target,
  and all three match immediately with no art work.
- If **fur markings** is Aldwin: the coat map must reach the WebGL payload (build-side), and a
  separate art pass decides whether the coat is repainted. That pass is not started without your word.

**Do not close this ticket on the RCA. Close it on the felt-test of whichever look you pick.**
