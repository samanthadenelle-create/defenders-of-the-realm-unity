// =============================================================================
// HeroBodySwapper — at scene start, swap the hero's placeholder Wizard body
// for the FBX matching the player's chosen HeroClass (Knight / Ranger / Mage).
// -----------------------------------------------------------------------------
// VillageSceneBuilder bakes the scene with a Wizard placeholder (since the
// builder runs at edit time and doesn't see runtime state). This component
// runs at runtime, reads GameStateService.State.HeroClass, and if the choice
// isn't Mage it loads the matching FBX from Resources/Heroes/<slug>.fbx,
// destroys the old "HeroBody" child, and instantiates the new mesh in its
// place. Idempotent — only runs once per scene.
//
// Resources/Heroes/<slug>.fbx is the canonical pickup path because Resources
// is auto-included in player builds (no Addressables wiring needed for the
// hand-imported Tripo FBXs).
// =============================================================================

using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroBodySwapper : MonoBehaviour
    {
        private const float TargetHeightMeters = 2.0f;
        // Global animator playback multiplier for the hero (Mixamo clips run fast).
        // 0.5 = half speed. Tune here.
        private const float HeroAnimSpeed = 0.5f;

        private void Start()
        {
            HeroClass cls = ResolveHeroClass();
            string slug = SlugFor(cls);
            if (slug == null) return;             // Mage = default body, no swap.

            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab == null)
            {
                // DEF-229 loud-failure guard: a chosen class with NO body asset must shout, not
                // silently leave the baked placeholder (the "wrong/old hero" symptom). The owner
                // sees the PLACEHOLDER (Wizard), reads it as "the wrong model spawned", and the
                // root cause (a missing/mis-named Resources/Heroes/<slug>.fbx) stays hidden. Error
                // names the exact class → slug → expected path so it is actionable at a glance.
                Debug.LogError(
                    "[HeroBodySwapper] DEF-229 — chosen hero " + cls + " maps to slug '" + slug +
                    "' but Resources/Heroes/" + slug + ".fbx is MISSING. The placeholder body is " +
                    "being kept, which reads in-game as 'the wrong/old hero spawned'. Import the " +
                    "matching Humanoid FBX to Resources/Heroes/" + slug + ".fbx (animationType=3).");
                return;
            }

            // Remove the old body if one exists, but FIRST snapshot the
            // Animator's runtimeAnimatorController so the new body keeps the
            // Walk / Cast states. Tripo FBXs import without an Animator on
            // some configs — when null we still try to assign the controller
            // to a freshly-added Animator on the new body root.
            RuntimeAnimatorController controllerSnapshot = null;
            var old = transform.Find("HeroBody");
            if (old != null)
            {
                var oldAnim = old.GetComponentInChildren<Animator>();
                if (oldAnim != null) controllerSnapshot = oldAnim.runtimeAnimatorController;
                Destroy(old.gameObject);
            }

            // DEF-221: build the body through the ONE shared factory — the same
            // VisualFactory.Skin path the companion (StoryCompanionInjector) and every
            // enemy (EnemyFactory) already use — instead of the bespoke load/instantiate/
            // fit/seat/strip block the hero was the LAST holdout for. Skin handles
            // Instantiate + FitHeight + SeatOnGround (feet at the root, supersedes the
            // old NormalizeHeight) + StripColliders. Hero-specific wiring (forward yaw,
            // animator, ability kit, class texture/tint) layers on top below.
            // Knight stands 20% taller than the 2 m baseline (owner 2026-05-30). The hero
            // keeps its OWN material pass (RetargetMaterialsToUrp + ApplyExtractedTexture
            // + ApplyClassTint), so the factory's Tripo fix stays OFF (no double-process).
            float targetH = (cls == HeroClass.Knight) ? TargetHeightMeters * 1.2f : TargetHeightMeters;
            // FORWARD CORRECTION (WO-174): every hero body imports facing +X; rotate the
            // authored +X onto the root's +Z so facing == heading (no moonwalk).
            // DEF-232: pass the yaw to Skin as LocalRotation so it is applied BEFORE the
            // fit/seat pass. The OLD code rotated the body AFTER Skin had already centred the
            // (off-pivot) bounds over the root at identity — so the -90° swung the mesh to the
            // side of the root. The camera follows the ROOT, so the body appeared offset to her
            // right and "pivoted in place" instead of translating. Rotating first, then seating,
            // centres the visible body dead-on over the root the camera follows.
            const float ForwardYaw = -90f;   // +X (authored forward) → +Z (root forward)
            var body = VisualFactory.Skin(transform, prefab, new SkinOptions
            {
                FitHeight = targetH,
                StripColliders = true,
                SeatOnGround = true,
                FixTripoMaterials = false,
                LocalRotation = Quaternion.Euler(0f, ForwardYaw, 0f),
            });
            if (body == null) return;
            body.name = "HeroBody";

            // DEF-232 spawn-time validation: the camera (SmartMobileCamera) follows the hero
            // ROOT by tag; HeroLocomotion drives that same root. The visible body must sit
            // centred over the root (no horizontal offset) or the camera frames empty space
            // beside her. Log LOUDLY if Skin left the body off-centre so this regression is
            // caught at the source instead of re-surfacing as a "camera stays to her right" bug.
            ValidateBodyCentredOverRoot(body);

            StripRigidbodies(body);
            // Tripo FBXs embed a CAMERA / AudioListener; left in, the hero body's camera
            // fights the VillageCamera (runtime camera-soup, 2026-05-25). Strip both so
            // only the hero's follow camera drives the display.
            foreach (var cam in body.GetComponentsInChildren<Camera>(true))
                if (cam != null) Destroy(cam);
            foreach (var al in body.GetComponentsInChildren<AudioListener>(true))
                if (al != null) Destroy(al);
            RetargetMaterialsToUrp(body);
            // Owner 2026-05-25 "the rig exists, the animation exists" — wire the
            // REAL Walk/Cast animation. The per-class controller is generated by
            // HeroAnimatorSetup (Defenders → Animation → Setup <Class> Animator),
            // which extracts the FBX's embedded NLA takes (Walk = longer take,
            // Cast = shorter) and builds an Idle/Walk/Cast machine on the Speed
            // (float) + Cast (trigger) params HeroLocomotion/HeroAbilities drive.
            //
            // The OLD path snapshotted the *placeholder* body's controller — but
            // the baked placeholder (Wizard.fbx) is MISSING, so its Animator and
            // the snapshot were always null and this whole block was skipped,
            // leaving the hero un-animated (the "sliding statue"). Load the
            // controller directly from Resources/Heroes/<slug>.controller instead;
            // fall back to any carried-over snapshot only if that asset is absent.
            var controller = Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)
                             ?? controllerSnapshot;

            var anim = body.GetComponentInChildren<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            // CRITICAL (WO-174 "no walk / statue"): the per-class controllers are
            // built from HUMANOID Mixamo/iClone clips (HeroAnimatorFactory). A
            // Humanoid clip can ONLY pose the rig through an Avatar — with no
            // Avatar the Animator stays frozen in its bind/T-pose no matter what
            // Speed we feed it (the "sliding statue"). The FBX prefab's root
            // Animator normally carries its generated Humanoid avatar; but Tripo
            // exports sometimes instantiate WITHOUT an Animator (then AddComponent
            // above yields an avatar-less one), or with the avatar dropped. Pull
            // the avatar off the source FBX prefab and assign it whenever the
            // live Animator is missing one, so retarget always binds.
            if (anim.avatar == null || !anim.avatar.isValid)
            {
                var prefabAnim = prefab.GetComponentInChildren<Animator>();
                if (prefabAnim != null && prefabAnim.avatar != null && prefabAnim.avatar.isValid)
                    anim.avatar = prefabAnim.avatar;
            }
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogWarning(
                    "[HeroBodySwapper] No controller at Resources/Heroes/" + slug +
                    ".controller — run Defenders → Animation → Setup " + slug +
                    " Animator. Hero will not animate.");
            }
            // applyRootMotion=false: the Walk clip's baked root curves would fight
            // HeroLocomotion's Slerp on the parent and the hero would never appear
            // to turn. Root motion off → HeroLocomotion owns movement/rotation; the
            // clip only drives the visible mesh.
            anim.applyRootMotion = false;
            // Owner 2026-05-30: Mixamo clips play too fast on the hero. Scale global
            // animator playback to half speed. Works regardless of which controller is
            // loaded (it's a multiplier on all clip playback). Tune via HeroAnimSpeed.
            anim.speed = HeroAnimSpeed;
            // Keep animating even when the follow camera frames just past the hero
            // edge, else Unity freezes the rig and it T-poses on re-entry to view.
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.keepAnimatorStateOnDisable = true;
            // Rebind reconnects bone references after the FBX instantiate finishes,
            // else the new mesh T-poses for ~10 frames after a swap.
            anim.Rebind();
            if (controller != null)
            {
                bool hasSpeedParam = false;
                foreach (var p in anim.parameters)
                {
                    if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float)
                    { hasSpeedParam = true; break; }
                }
                if (!hasSpeedParam)
                {
                    Debug.LogWarning(
                        "[HeroBodySwapper] Controller '" + controller.name +
                        "' has no Speed (float) parameter — Walk transitions won't fire.");
                }
                else
                {
                    anim.SetFloat("Speed", 0f); // open in Idle, not mid-Walk
                }
            }
            // ALWAYS re-cache the _animator field on BOTH HeroLocomotion (drives
            // Speed → Walk) and HeroAbilities (fires Cast). This was previously
            // gated on the (always-null) snapshot AND only touched HeroLocomotion,
            // which is the core reason the hero never animated. Their private
            // fields still point at the destroyed placeholder animator; reflection
            // write because the fields are private.
            // DEF-221: the BODY slug and the ABILITY slug can differ. The Cleric loads
            // its own body/controller (slug "Cleric") but fires the Mage loadout until a
            // dedicated cleric/healer kit lands — so route its abilities to "Mage".
            string abilitySlug = (cls == HeroClass.Cleric) ? "Mage" : slug;
            int recached = 0;
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n != "HeroLocomotion" && n != "HeroAbilities") continue;
                var f = mb.GetType().GetField("_animator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { f.SetValue(mb, anim); recached++; }
                // WO-36: bind the hero class on HeroAbilities so a Knight/Ranger
                // casts its OWN abilities.json loadout. The field defaulted to
                // "mage" and was never reassigned, so every class fired the Mage
                // kit. slug is "Knight"/"Ranger"/"Mage"; SetHeroClass lower-cases it.
                if (n == "HeroAbilities")
                    mb.GetType().GetMethod("SetHeroClass",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                      ?.Invoke(mb, new object[] { abilitySlug });
            }
            Debug.Log($"[HeroBodySwapper] Animator wired: controller=" +
                      $"{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, " +
                      $"avatar={(anim.avatar != null ? anim.avatar.name : "none")}, " +
                      $"clips={anim.runtimeAnimatorController?.animationClips?.Length ?? 0}, " +
                      $"re-cached {recached} component(s) (HeroLocomotion/HeroAbilities).");
            // Owner 2026-05-20: Tripo's Send To Unity feature extracted the
            // Knight basecolor PNG. Copied into Resources/Textures/Knight.png
            // so the runtime can apply the real texture rather than the
            // stale class tint. Same hook works for Ranger if/when its
            // Send To Unity export lands.
            ApplyExtractedTexture(body, cls);
            // Safety net: if Tripo's embedded textures didn't extract, paint
            // the body with a class tint so the mesh isn't solid white.
            ApplyClassTint(body, cls);
            Debug.Log("[HeroBodySwapper] Swapped hero body to " + slug + ".fbx");
        }

        private static HeroClass ResolveHeroClass()
        {
            var svc = GameStateService.Instance;
            if (svc == null) return HeroClass.Mage;
            var opt = svc.State?.HeroClass.ToNullable();
            return opt ?? HeroClass.Mage;
        }

        /// <summary>
        /// Tripo FBXs come in with Phong / Lambert / Blinn materials that URP
        /// can't render — the mesh shows as a transparent magenta ghost. Walk
        /// the renderers, pull each material's diffuse / normal / emission
        /// (whichever property names happen to be present on the source), and
        /// rebuild under URP/Lit. Falls through to URP/Simple Lit (cheaper on
        /// mobile) then Standard as a final safety net so the hero is never
        /// rendered with a null shader.
        /// </summary>
        private static void RetargetMaterialsToUrp(GameObject body)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");
            if (litShader == null)
            {
                Debug.LogWarning("[HeroBodySwapper] No URP/Lit or Standard shader available — material fix skipped.");
                return;
            }

            int converted = 0, skipped = 0;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    // Already a URP shader? Preserve — upstream art may have
                    // intentionally placed URP/Unlit on emissive accents etc.
                    string srcShaderName = src.shader != null ? src.shader.name : "";
                    if (srcShaderName.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                    { skipped++; continue; }

                    // Diffuse / base texture — try every common property name.
                    Texture baseTex = null;
                    if (src.HasProperty("_MainTex"))      baseTex = src.GetTexture("_MainTex");
                    if (baseTex == null && src.HasProperty("_BaseMap"))       baseTex = src.GetTexture("_BaseMap");
                    if (baseTex == null && src.HasProperty("_BaseColorMap")) baseTex = src.GetTexture("_BaseColorMap");

                    // Base colour — try URP first, then Built-in.
                    Color baseColor = Color.white;
                    if (src.HasProperty("_BaseColor"))      baseColor = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color"))     baseColor = src.GetColor("_Color");

                    // Normal + emission, if the source authored them.
                    Texture normalTex = null;
                    if (src.HasProperty("_BumpMap"))                   normalTex = src.GetTexture("_BumpMap");
                    if (normalTex == null && src.HasProperty("_NormalMap")) normalTex = src.GetTexture("_NormalMap");
                    Texture emissionTex = null;
                    Color   emissionColor = Color.black;
                    if (src.HasProperty("_EmissionMap"))   emissionTex   = src.GetTexture("_EmissionMap");
                    if (src.HasProperty("_EmissionColor")) emissionColor = src.GetColor("_EmissionColor");

                    // Tiling / offset for whichever map slot held the diffuse.
                    Vector2 tiling = Vector2.one, offset = Vector2.zero;
                    if (src.HasProperty("_MainTex"))
                    { tiling = src.GetTextureScale("_MainTex"); offset = src.GetTextureOffset("_MainTex"); }
                    else if (src.HasProperty("_BaseMap"))
                    { tiling = src.GetTextureScale("_BaseMap"); offset = src.GetTextureOffset("_BaseMap"); }

                    var newMat = new Material(litShader);
                    newMat.name = (src.name ?? "Tripo") + " (URP)";
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", baseColor);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color",     baseColor);
                    if (baseTex != null)
                    {
                        if (newMat.HasProperty("_BaseMap"))
                        { newMat.SetTexture("_BaseMap", baseTex); newMat.SetTextureScale("_BaseMap", tiling); newMat.SetTextureOffset("_BaseMap", offset); }
                        if (newMat.HasProperty("_MainTex"))
                        { newMat.SetTexture("_MainTex", baseTex); newMat.SetTextureScale("_MainTex", tiling); newMat.SetTextureOffset("_MainTex", offset); }
                    }
                    if (normalTex != null && newMat.HasProperty("_BumpMap"))
                    {
                        newMat.SetTexture("_BumpMap", normalTex);
                        newMat.EnableKeyword("_NORMALMAP");
                    }
                    if (emissionTex != null || emissionColor.maxColorComponent > 0.001f)
                    {
                        if (newMat.HasProperty("_EmissionColor")) newMat.SetColor("_EmissionColor", emissionColor);
                        if (emissionTex != null && newMat.HasProperty("_EmissionMap"))
                            newMat.SetTexture("_EmissionMap", emissionTex);
                        newMat.EnableKeyword("_EMISSION");
                        newMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    // DEF-6: Tripo FBXs frequently export with inverted face
                    // normals. URP's default back-face cull (Cull=2) renders
                    // the inside surfaces, producing the "dark purple spiky
                    // creature" seen in screenshots 1-3. Disabling culling
                    // (Cull=0 = Off) renders both sides so the mesh is visible
                    // regardless of which way its normals point.
                    if (newMat.HasProperty("_Cull"))
                        newMat.SetFloat("_Cull", 0f); // 0 = Off (double-sided)

                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.15f);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic",   0f);
                    mats[i] = newMat;
                    converted++;
                }
                r.sharedMaterials = mats;
            }
            Debug.Log("[HeroBodySwapper] RetargetMaterialsToUrp: converted=" +
                      converted + ", skipped (already URP)=" + skipped);
        }

        private static string SlugFor(HeroClass cls) => cls switch
        {
            HeroClass.Knight => "Knight",
            HeroClass.Ranger => "Ranger",
            // Mage now has its own paid-for Tripo FBX at Resources/Heroes/Mage.fbx
            // (24 MB) per docs/port-notes/tripo-asset-pipeline.md. Swap in like
            // the other two; HeroAnimatorSetup writes Mage.controller alongside.
            HeroClass.Mage   => "Mage",
            // DEF-221: the Cleric now has a DEDICATED body (Resources/Heroes/Cleric.fbx,
            // from People/Human/human_Cleric) + Cleric.controller (a copy of the Mage
            // caster kit). It still fires the Mage ability LOADOUT — abilitySlug below
            // routes Cleric → "Mage" for SetHeroClass — until a cleric/healer kit exists.
            HeroClass.Cleric => "Cleric",
            _ => null,
        };

        // (Removed NeedsForwardFlip — the per-class -Z/+Z guess was superseded by
        // the single consistent +X→+Z ForwardYaw correction at swap time, WO-174.)

        /// <summary>
        /// Loads a per-class basecolor PNG out of Resources/Textures/
        /// (extracted via Tripo's Send-To-Unity flow on the owner's side)
        /// and assigns it to every URP material on the body. When this
        /// returns true the subsequent <see cref="ApplyClassTint"/> is a
        /// no-op for that material (it preserves textures).
        /// </summary>
        private static void ApplyExtractedTexture(GameObject body, HeroClass cls)
        {
            string texPath = cls switch
            {
                // WO-35: Knight removed. Both available Knight atlases (Resources
                // Textures/Knight.png AND the FBM knight_basecolor.JPEG) are the same
                // red-splatter Tripo grunge variant — binding either gives the dirty
                // blood-spattered armour the owner flagged. Returning null lets
                // ApplyClassTint paint the clean steel tint (0.78,0.80,0.86) instead.
                // DEF-229 (2026-06-03): the Ranger body is now the CC5/CC_Base adult
                // archer (InstaLOD-remeshed: ONE combined mesh + ONE baked PBR atlas),
                // imported Humanoid by PeopleCharacterImporter.ImportRangerCC5 into
                // Resources/Heroes/Ranger.fbx with its baked diffuse copied to
                // Resources/Heroes/Ranger_tex/. The combined bake is a single atlas, so
                // painting that one diffuse onto every body slot (skin/body/tongue) is
                // correct — each slot samples its UV region. Repointed off the retired
                // Tripo "archer v2" basecolor (Textures/Ranger) to the CC5 bake so the
                // selected archer reads as the adult ranger, not the old spiky youth.
                HeroClass.Ranger => "Heroes/Ranger_tex/remesh_12_combined_Bake_Diffuse",
                _ => null,
            };
            if (string.IsNullOrEmpty(texPath)) return;
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex == null) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    // White base colour so the texture's actual colours show
                    // through (otherwise the class tint would multiply on top
                    // and dirty the basecolor).
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", Color.white);
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// If the URP material on the body still ended up white (Tripo's
        /// embedded textures sometimes don't survive import), paint each
        /// material with a class tint so the body reads coloured anyway.
        /// Skipped when a real diffuse texture is already bound (e.g. the
        /// extracted basecolor PNG from Tripo's Send-To-Unity flow).
        /// </summary>
        private static void ApplyClassTint(GameObject body, HeroClass cls)
        {
            Color tint = cls switch
            {
                HeroClass.Knight => new Color(0.78f, 0.80f, 0.86f),   // steel
                HeroClass.Ranger => new Color(0.40f, 0.50f, 0.34f),   // hunter / leaf green
                _                => new Color(0.60f, 0.45f, 0.85f),   // mage fallback
            };

            // Owner 2026-05-26: forcing a flat tint "lost the Knight's colour" — it
            // cleared the textured detail. Restore the original rule: paint the tint
            // ONLY when a material has no diffuse texture. That preserves every real
            // texture — the Ranger's fresh v2 basecolor, the Mage's embedded texture,
            // and whatever the Knight imported — and the tint is just the fallback so
            // an untextured mesh still reads coloured instead of solid white.
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex != null) continue;   // preserve real textures
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", tint);
                }
                r.sharedMaterials = mats;
            }
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Owner 2026-05-20 ("archer appears halfway under surface"): the
            // Tripo Ranger FBX pivots near the mesh centre, so after scaling
            // the lower half drops below the hero root's Y=0. Recompute the
            // post-scale bounds and lift the body so the feet land at y=0.
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            float feetOffset = b2.min.y - go.transform.position.y;
            if (feetOffset < 0f)
                go.transform.localPosition -= new Vector3(0f, feetOffset, 0f);
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
        }

        private static void StripRigidbodies(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) Destroy(rb);
        }

        /// <summary>
        /// DEF-232 guard: assert the swapped body's render bounds sit centred (XZ) over the
        /// hero ROOT — the transform the follow camera tracks and HeroLocomotion drives. A
        /// horizontal offset is the exact "camera stays to her right / body pivots in place"
        /// regression (an off-pivot mesh rotated after seating). Logs LOUDLY so it is caught at
        /// the spawn source rather than re-surfacing as a camera complaint, and re-centres as a
        /// runtime safety net so the player is never left with the body off to one side.
        /// </summary>
        private void ValidateBodyCentredOverRoot(GameObject body)
        {
            if (body == null) return;
            var rends = body.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 root = transform.position;
            float dx = b.center.x - root.x;
            float dz = b.center.z - root.z;
            float planarOffset = Mathf.Sqrt(dx * dx + dz * dz);

            // ~0.35 m tolerance: a hand/weapon prop can pull the encapsulated centre slightly
            // off the body's own midline without it reading as "offset to the side".
            const float MaxPlanarOffset = 0.35f;
            if (planarOffset > MaxPlanarOffset)
            {
                Debug.LogError(
                    "[HeroBodySwapper] DEF-232 — swapped body '" + body.name + "' is OFF-CENTRE " +
                    planarOffset.ToString("F2") + "m from the hero root (dx=" + dx.ToString("F2") +
                    ", dz=" + dz.ToString("F2") + "). The follow camera tracks the ROOT, so the " +
                    "body would appear offset to one side and 'pivot in place'. Re-centring as a " +
                    "safety net — investigate the mesh pivot / Skin seating.");
                // Safety-net re-centre: shift the body so its bounds centre sits over the root's XZ.
                body.transform.position -= new Vector3(dx, 0f, dz);
            }
        }
    }
}
