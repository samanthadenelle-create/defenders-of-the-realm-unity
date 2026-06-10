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

using DeNelle.Core.Combat; // ActorAnimator for post-swap battle ready / full anim drive (Village + World)
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroBodySwapper : MonoBehaviour
    {
        private const float TargetHeightMeters = 1.75f;
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
            // Knight stands slightly taller (~1.80m) than the 1.75m human baseline (was 20% on old 2m).
            // The hero keeps its OWN material pass (RetargetMaterialsToUrp + ApplyExtractedTexture
            // + ApplyClassTint), so the factory's Tripo fix stays OFF (no double-process).
            float targetH = (cls == HeroClass.Knight) ? TargetHeightMeters * 1.0286f : TargetHeightMeters;
            // FORWARD CORRECTION (WO-174): every hero body imports facing +X; rotate the
            // authored +X onto the root's +Z so facing == heading (no moonwalk).
            // DEF-232: pass the yaw to Skin as LocalRotation so it is applied BEFORE the
            // fit/seat pass. The OLD code rotated the body AFTER Skin had already centred the
            // (off-pivot) bounds over the root at identity — so the -90° swung the mesh to the
            // side of the root. The camera follows the ROOT, so the body appeared offset to her
            // right and "pivoted in place" instead of translating. Rotating first, then seating,
            // centres the visible body dead-on over the root the camera follows.
            // FORWARD-YAW for current Tripo + AccuRIG pipeline (all classes re-rigged 2026-06-06).
            // The meshes export facing +X; +90f (or -90f — trial both) rotates the authored
            // forward onto the locomotion root's +Z so that when HeroLocomotion does
            // LookRotation(velocity) the visual faces the travel direction (no sidestep/moonwalk).
            // WO-326: -90f is the proven value — the companion path skins the SAME
            // Resources/Heroes FBXs with -90f (StoryCompanionInjector.cs:160) and reads
            // correct. +90f put the hero ~90 degrees right of travel.
            float forwardYaw = -90f;
            var body = VisualFactory.Skin(transform, prefab, new SkinOptions
            {
                FitHeight = targetH,
                StripColliders = true,
                SeatOnGround = true,
                FixTripoMaterials = false,
                LocalRotation = Quaternion.Euler(0f, forwardYaw, 0f),
            });
            if (body == null) return;
            body.name = "HeroBody";

            // DEF-232 spawn-time validation: the camera (SmartMobileCamera) follows the hero
            // ROOT by tag; HeroLocomotion drives that same root. The visible body must sit
            // centred over the root (no horizontal offset) or the camera frames empty space
            // beside her. Log LOUDLY if Skin left the body off-centre so this regression is
            // caught at the source instead of re-surfacing as a "camera stays to her right" bug.
            ValidateBodyCentredOverRoot(body);

            // DEF-235: plant the feet on the ground. VisualFactory.SeatOnGround drops the body
            // so its RENDER-BOUNDS base (b.min.y) sits at the root's y. For the CC5/AccuRIG hero
            // bodies the lowest *visible* skinned vertex (the soles) sits ABOVE that bounds base
            // — bind-pose padding leaves the renderer-bounds floor a few cm below the real feet —
            // so the hero reads as "floating a tiny bit". Measure the TRUE lowest posed vertex in
            // world space and nudge the body DOWN by exactly that gap so the soles touch root.y.
            // Y-only (orthogonal to DEF-232's XZ centring) and hero-scoped (does NOT touch the
            // global VisualFactory.SeatOnGround, which enemies/structures rely on).
            PlantFeetOnGround(body);

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

            // DEF-232 (facing) self-correct — replaces the hard-coded -90° guess above for any body
            // whose authored forward ISN'T +X. Needs the valid Humanoid avatar ensured just above, so
            // it runs here (not in the FORWARD CORRECTION block). No-op for already-aligned bodies.
            AlignBodyFacingToRoot(body, anim);
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

            // EquipmentController — activates the real KayKit weapon-mesh equip on the
            // swapped hero. It lives on the hero ROOT (same object as GearLoadout): its
            // CacheRig() finds the Animator via transform.Find("HeroBody") (the body we
            // just swapped/rebound), and Awake/OnEnable resolve GearLoadout via
            // GetComponent<GearLoadout>() — BOTH only resolve from the root. Ensure the
            // GearLoadout exists FIRST (lazy-add, same idiom as line ~274 below) so the
            // controller's OnEnable reads a live loadout and equips on enable. Guard so a
            // re-run never double-adds ([DisallowMultipleComponent] would also reject it).
            var equipLoadout = GetComponent<GearLoadout>() ?? gameObject.AddComponent<GearLoadout>();
            equipLoadout.Refresh();
            if (GetComponent<EquipmentController>() == null)
                gameObject.AddComponent<EquipmentController>();

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
            // DEF: the Ranger/Archer fires arrows via the projectile system but held
            // NOTHING — give him a visible bow. Bow goes in the LEFT (off/bow) hand
            // since HeroAimIK draws with the RightHand IK goal. Cosmetic only; the
            // component null-guards a missing bone and never blocks the hero.
            // Attachment: uses LeftHand bone (via GetBoneTransform); sizing via
            // HeroBowAttachment.BowHeldLength + NormalizeInto (reviewed/fixed to 0.92m).
            if (cls == HeroClass.Ranger)
                HeroBowAttachment.AttachTo(gameObject, body);

            // Default gear + visuals (Chunk default gear + shop): ensure GearLoadout so
            // level-1 catalog entries (knight sword+plate, ranger bow+leather, mage staff+robes,
            // cleric mace+light) are applied at spawn. Then attach visuals via the applier
            // (sword/staff/mace on RightHand; bow skipped in applier -- ranger uses the
            // dedicated HeroBowAttachment on LeftHand to avoid duplicate back+held).
            // Re-apply later from shop equip flows. Applier now uses corrected scales/pos/euler
            // proportional to ~1.8m hero (see GearVisualApplier for exact values).
            var loadout = GetComponent<GearLoadout>() ?? gameObject.AddComponent<GearLoadout>();
            loadout.Refresh();
            GearVisualApplier.Apply(body.transform, loadout);

            // Ensure the guarded ActorAnimator is on the hero root (finds the Animator on the
            // new "HeroBody" child). This wires the full shared animation pipeline (same as
            // DTT/PatriciaLight): SetLocomotion (speed for idle/move), SetCombatStance (battle
            // ready idle stance vs casual), PlayAttack/PlayCast (class-specific), PlayHit,
            // Die. HeroLocomotion/Abilities already resolve and drive it; re-attach here post-swap
            // so Village + World scenes get correct hero anims (Idle/BattleReady, Movement,
            // Attack/Cast per class, Hit, Death) without falling back to T-pose or legacy only.
            var actor = GetComponent<ActorAnimator>() ?? gameObject.AddComponent<ActorAnimator>();
            actor.SetCombatStance(true); // battle ready / engagement stance for village threats/waves

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
            // DEF-267: separate the two body pipelines.
            //  • CC5 bodies (Ranger / Cleric) = ONE combined mesh + ONE baked PBR atlas, imported
            //    with materialImportMode=None → renderer slots come in null/default. The right fix
            //    is to build ONE URP/Lit from the single atlas and REPLACE every slot.
            //  • Legacy Tripo bodies (Mage / Knight) = multi-material exports whose slots DO import
            //    (materialImportMode=2) but with NO base-color texture bound, so they render GREY in
            //    the build (the DEF-267 symptom: "URP-but-untextured"). Their Send-To-Unity diffuse
            //    PNG lives in Resources/Heroes/Textures/*. PAINT that diffuse onto the EXISTING slots
            //    in place — replacing them all with one shared material would collapse the body's
            //    separate UV islands onto a single material and mis-sample. Each slot already maps
            //    its own region of the single Tripo atlas, so painting the same atlas onto each is
            //    correct and preserves the per-slot setup.
            // WO-286: ALL four heroes are now re-rigged single-atlas meshes (AccuRIG,
            // 2026-06-06). Each FBX shipped EXTRA texture sets (Knight a stray
            // remesh_*_Bake, Ranger a Motion_Dummy_Female PBR set) plus metallic/normal
            // maps the import bound into the slots — that's the blotchy purple-camo Mage +
            // patchy-red Knight (wrong albedo + a normal/metallic read as colour). The
            // "paint _BaseMap onto existing slots" path LEFT those stray maps in place, so
            // it never cleaned up. Build ONE fresh URP/Lit from JUST the real basecolor and
            // REPLACE every slot (no metallic/normal assigned → no colour-space artefact).
            // Each slot's own UVs still sample the single atlas correctly. So: combined
            // path for ALL classes now, not only the old CC5 Ranger/Cleric.
            bool isCc5Combined = true;
            string texPath = cls switch
            {
                // DEF-267: the Mage is a LEGACY Tripo body. The build log showed Mage rendering
                // GREY because (a) its FBX material remap points at an EXTERNAL material that
                // doesn't resolve in the build → no _BaseMap, and (b) NO texPath existed here so
                // ApplyExtractedTexture returned early and never bound a diffuse. Bind the Mage's
                // basecolor atlas from the NORMAL Resources/Heroes/Textures/ folder. We use a
                // normal folder (not the sibling *.fbm/ extraction) deliberately: the working
                // Ranger/Cleric pipeline copies its atlas into a plain Heroes/<x>_tex/ folder
                // because textures inside an FBX's *.fbm import-artifact folder are NOT reliably
                // Resources.Load-able in a player build. Heroes/Textures/* is a guaranteed-loadable
                // plain folder. This is the same wizard atlas, just from the reliable location.
                // WO-286: the re-rigged Mage mesh's own basecolor is the "witch" atlas
                // (it shipped base-color only); the old "wizard" path bound a different
                // mesh's texture → wrong colors. Point at the mesh's own basecolor.
                // FIX (2026-06-06): the PROJECT Mage is a legacy Tripo mesh — its UV-matched
                // atlas is its OWN embedded diffuse (Mage.fbm/tripo_mat_9b343081, copied into
                // Textures/), NOT the actors/ dwarf basecolor (a different mesh). CC5 heroes
                // (Ranger/Cleric) use the actors originals; Tripo heroes use their own.
                HeroClass.Mage => "Heroes/Textures/tripo_mat_9b343081_Pbr_Diffuse",
                // DEF-267: the Knight is also a LEGACY Tripo body whose FBX material remap doesn't
                // resolve → grey. WO-35 had returned null (to dodge a blood-splatter look the owner
                // disliked), but that left the Knight GREY in the build, which is worse. Bind the
                // clean medieval-knight atlas from the plain (reliably loadable) Heroes/Textures/
                // folder — textured beats grey for go-live. ApplyClassTint's steel fallback now
                // only fires on any slot this atlas genuinely doesn't cover.
                // WO-286: the re-rigged Knight mesh ships TWO sets — its real
                // knight_basecolor and a stray remesh_12_combined_Bake raw bake. Bind the
                // knight basecolor (the old medievalknight path was a different mesh).
                // PARKED (2026-06-06): the Knight FBX is a broken combo — its mesh material
                // is 'tripo_mat_18e58c3e', which has NO matching atlas in-project (tripo_1afe2e5e,
                // remesh_12, knight_basecolor, HumanTank all read wrong). This needs a CLEAN
                // Knight model re-import, not a texture pick.
                // WO-330 (2026-06-08): returning null here is what produces the DTT/Village
                // CYAN SILHOUETTE. When texPath is null, the CC5 `baked` material below is never
                // built, so the Knight's combined-mesh renderer slots — which import NULL/default
                // on the AccuRIG single-atlas FBX — are never replaced and render as URP's bright
                // cyan/magenta error shader (a missing-material fallback). ApplyClassTint can't
                // rescue it because it only colours EXISTING non-null materials, never a null slot.
                // Bind the clean medieval-knight atlas (same one the COMPANION Knight already uses
                // successfully via StoryCompanionInjector.BindClassDiffuse) so `baked` is built and
                // replaces the null slots with a lit, textured material. Textured-but-imperfect
                // beats an unlit cyan blob for go-live; a clean Knight re-import can repoint later.
                HeroClass.Knight => "Heroes/Textures/medievalknight3dmodel_basecolor",
                // DEF-229 (2026-06-03): the Ranger body is now the CC5/CC_Base adult
                // archer (InstaLOD-remeshed: ONE combined mesh + ONE baked PBR atlas),
                // imported Humanoid by PeopleCharacterImporter.ImportRangerCC5 into
                // Resources/Heroes/Ranger.fbx with its baked diffuse copied to
                // Resources/Heroes/Ranger_tex/. The combined bake is a single atlas, so
                // painting that one diffuse onto every body slot (skin/body/tongue) is
                // correct — each slot samples its UV region. Repointed off the retired
                // Tripo "archer v2" basecolor (Textures/Ranger) to the CC5 bake so the
                // selected archer reads as the adult ranger, not the old spiky youth.
                // FIX (2026-06-06): bind the Ranger's ORIGINAL basecolor from the source actor's
                // .fbm (ranger.fbm/ranger_basecolor) copied into Textures/ — the model's own
                // shipped atlas. (Archer_basecolor read dark; it was a different/old archer bake.)
                HeroClass.Ranger => "Heroes/Textures/ranger_basecolor",
                // DEF-232/229 (2026-06-03): the Cleric (Healer/Elara body) is now the
                // owner's fresh CC5/CC_Base adult cleric, imported Humanoid by
                // PeopleCharacterImporter.ImportClericCC5 with its baked basecolor copied
                // to Resources/Heroes/Cleric_tex/. Bind that diffuse so the Cleric reads
                // textured (was falling through to a flat class tint before).
                // WO-286: the re-rigged Cleric is the only clean set — its own basecolor
                // is Cleric_basecolor (the old HumanCleric_tex path was the prior CC5 body).
                HeroClass.Cleric => "Heroes/Textures/Cleric_basecolor",
                _ => null,
            };
            if (string.IsNullOrEmpty(texPath)) return;
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[HeroBodySwapper] DEF-267 baked/basecolor diffuse not found at Resources/" +
                                 texPath + " for " + cls + " — body will fall back to a class tint.");
                return;
            }

            // For CC5 combined bodies: build ONE URP/Lit from the single baked atlas and assign it
            // to every slot (slots came in null/default). Runtime-built URP/Lit renders in WebGL
            // (RetargetMaterialsToUrp uses the same path).
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");
            Material baked = (isCc5Combined && lit != null) ? new Material(lit) { name = cls + " (CC5 baked)" } : null;
            if (baked != null)
            {
                if (baked.HasProperty("_BaseMap"))   baked.SetTexture("_BaseMap", tex);
                if (baked.HasProperty("_MainTex"))   baked.SetTexture("_MainTex", tex);
                if (baked.HasProperty("_BaseColor")) baked.SetColor("_BaseColor", Color.white);
                if (baked.HasProperty("_Color"))     baked.SetColor("_Color", Color.white);

                // Make Knight metallic armor as specified (vibrant steel).
                if (cls == HeroClass.Knight)
                {
                    if (baked.HasProperty("_Metallic"))   baked.SetFloat("_Metallic", 0.7f);
                    if (baked.HasProperty("_Smoothness")) baked.SetFloat("_Smoothness", 0.5f);
                }
            }

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    if (baked != null) r.sharedMaterial = baked;
                    continue;
                }
                for (int i = 0; i < mats.Length; i++)
                {
                    if (baked != null)
                    {
                        // For all (unified CC5 path): always replace slots with the baked material that has
                        // the correct class basecolor atlas bound to _BaseMap and white _BaseColor.
                        // This ensures vibrant correct textures regardless of what the FBX import remapped
                        // (or left null). Stray metallic/normal from import are discarded (they were causing
                        // color artefacts like patchy red/purple).
                        mats[i] = baked;
                    }
                    else if (mats[i] != null)
                    {
                        // Legacy Tripo (Mage/Knight): paint the diffuse onto the EXISTING URP slot.
                        // These slots are already URP/Lit (RetargetMaterialsToUrp ran first) but with
                        // no _BaseMap bound → grey. Bind the atlas and clear any dark/zero _BaseColor
                        // so the texture (not a leftover tint) drives the look.
                        if (mats[i].HasProperty("_BaseMap")) mats[i].SetTexture("_BaseMap", tex);
                        if (mats[i].HasProperty("_MainTex")) mats[i].SetTexture("_MainTex", tex);
                        if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.white);
                        if (mats[i].HasProperty("_Color"))     mats[i].SetColor("_Color", Color.white);
                    }
                }
                r.sharedMaterials = mats;
            }
            Debug.Log("[HeroBodySwapper] DEF-267 ApplyExtractedTexture: " + cls +
                      " diffuse '" + texPath + "' bound (mode=" + (isCc5Combined ? "CC5-replace" : "Tripo-paint") + ").");
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
                // DEF-231: the Knight (Grom) is a LEGACY Tripo body with NO baked
                // basecolor (ApplyExtractedTexture returns null for Knight), so this
                // flat tint IS his armour colour. The old steel (0.78,0.80,0.86) read
                // dark/muddy/desaturated in scene light. Lift the value to a brighter,
                // cleaner cool steel (keep it cool — blue-biased, NOT muddy brown) so the
                // armour reads crisp without going flat-white.
                HeroClass.Knight => new Color(0.92f, 0.94f, 1.00f),   // bright cool steel
                HeroClass.Ranger => new Color(0.40f, 0.50f, 0.34f),   // hunter / leaf green
                _                => new Color(0.60f, 0.45f, 0.85f),   // mage fallback
            };

            // Owner 2026-05-26: forcing a flat tint "lost the Knight's colour" — it
            // cleared the textured detail. Restore the original rule: paint the tint
            // ONLY when a material has no diffuse texture. That preserves every real
            // texture — the Ranger's fresh v2 basecolor, the Mage's embedded texture,
            // and whatever the Knight imported — and the tint is just the fallback so
            // an untextured mesh still reads coloured instead of solid white.
            // WO-330 anti-cyan: a CC5/AccuRIG combined mesh can import its renderer slots as
            // NULL (or Unity's Default-Material), which URP renders as a bright cyan/magenta
            // UNLIT error shape — the "solid cyan silhouette". A null slot must be REPLACED with
            // a real URP/Lit material (tinted), not skipped, or the hero stays cyan. Build the
            // lit shader once for that case.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    // WO-330: a null/default slot is the cyan culprit — give it a fresh lit,
                    // tinted material so it is never left on URP's error/default shader.
                    if (m == null)
                    {
                        if (lit == null) continue;
                        var fill = new Material(lit) { name = cls + " (tint fill)" };
                        if (fill.HasProperty("_BaseColor")) fill.SetColor("_BaseColor", tint);
                        if (fill.HasProperty("_Color"))     fill.SetColor("_Color", tint);
                        if (cls == HeroClass.Knight)
                        {
                            if (fill.HasProperty("_Metallic"))   fill.SetFloat("_Metallic", 0.35f);
                            if (fill.HasProperty("_Smoothness")) fill.SetFloat("_Smoothness", 0.45f);
                        }
                        mats[i] = fill;
                        continue;
                    }
                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex != null) continue;   // preserve real textures
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", tint);
                    // DEF-231 (KNIGHT ONLY): the tinted Knight armour was reading dark
                    // because the runtime URP material's matte 0.15 smoothness / 0 metallic
                    // gives it no specular lift, so flat steel sits dim in scene light. Give
                    // the untextured Knight a touch of metallic + higher smoothness so the
                    // armour catches a soft highlight and reads as crisp steel — NOT a mirror
                    // (kept well under chrome) and NOT muddy. Scoped to Knight so Mage/Ranger/
                    // Cleric keep their existing matte look.
                    if (cls == HeroClass.Knight)
                    {
                        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0.35f);
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
                    }
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

        /// <summary>
        /// DEF-232 (facing) self-correct. The legacy fix rotated EVERY hero body by a hard-coded
        /// -90° on the assumption all meshes import facing +X. That's a guessed Euler: a body that
        /// imports facing some other axis is left angled, so the character STRAFES across the screen
        /// ("walks at 45°") even though HeroLocomotion drives the ROOT dead-straight (proven by
        /// world-coords). Here we DERIVE the body's true forward from the skeleton — the hip-to-hip
        /// vector is the character's lateral axis on EVERY humanoid rig, so forward = right × up is
        /// rig-independent (no guess) — then rotate the body by only the RESIDUAL needed to match the
        /// root's heading. Residual + 5° deadzone ⇒ an already-aligned body (the working heroes) gets
        /// a NO-OP; this can ONLY fix a misaligned body, never disturb a correct one. Non-humanoid /
        /// unmappable rigs fall back to the base seat untouched.
        /// </summary>
        private void AlignBodyFacingToRoot(GameObject body, Animator anim)
        {
            if (body == null || anim == null) return;
            if (!anim.isHuman || anim.avatar == null || !anim.avatar.isValid) return;

            var leftLeg  = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var rightLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (leftLeg == null || rightLeg == null) return;     // can't derive — keep the base seat

            // Character RIGHT = left-hip → right-hip (laterally placed on every humanoid), ground-projected.
            Vector3 rightDir = rightLeg.position - leftLeg.position;
            rightDir.y = 0f;
            if (rightDir.sqrMagnitude < 1e-6f) return;           // degenerate pose — bail safely
            rightDir.Normalize();

            // FORWARD = right × up (Unity left-handed: Cross((1,0,0),(0,1,0)) == (0,0,1)).
            Vector3 bodyForward = Vector3.Cross(rightDir, Vector3.up);

            Vector3 rootForward = transform.forward;
            rootForward.y = 0f;
            if (rootForward.sqrMagnitude < 1e-6f) rootForward = Vector3.forward;
            rootForward.Normalize();

            float curYaw = Mathf.Atan2(bodyForward.x, bodyForward.z) * Mathf.Rad2Deg;
            float tgtYaw = Mathf.Atan2(rootForward.x, rootForward.z) * Mathf.Rad2Deg;
            float delta  = Mathf.DeltaAngle(curYaw, tgtYaw);

            const float DeadzoneDeg = 5f;
            if (Mathf.Abs(delta) <= DeadzoneDeg)
            {
                Debug.Log($"[HeroBodySwapper] facing OK — body within {delta:F1}° of root heading; no correction.");
                return;
            }

            // Rotate the body about its OWN origin (position preserved) to face the root heading.
            body.transform.rotation = Quaternion.AngleAxis(delta, Vector3.up) * body.transform.rotation;
            Debug.Log($"[HeroBodySwapper] facing self-correct: skeleton forward {curYaw:F0}° vs root {tgtYaw:F0}° → rotated body {delta:F0}° (derive-from-skeleton, replaces guessed -90°).");

            // A yaw on an off-pivot mesh can shift its bounds — re-centre XZ and re-plant feet so the
            // body stays dead-on the root the camera follows.
            ValidateBodyCentredOverRoot(body);
            PlantFeetOnGround(body);
        }

        /// <summary>
        /// DEF-235: drop the swapped body so the lowest VISIBLE skinned vertex (the feet/soles)
        /// rests exactly at the hero root's Y. VisualFactory.SeatOnGround already aligned the
        /// RENDER-BOUNDS base to root.y, but a SkinnedMeshRenderer's bounds carry bind-pose
        /// padding — the bounds floor sits a few cm BELOW the actual posed soles — so the feet
        /// hover. We bake each skinned mesh into its current pose, walk its world-space vertices
        /// to find the true lowest point, and nudge the body DOWN by (lowestVertexY - root.y).
        ///
        /// Y-ONLY (does not touch XZ, so it never disturbs DEF-232's centring) and HERO-SCOPED
        /// (VisualFactory.SeatOnGround stays unchanged, so enemies/structures are unaffected).
        /// Clamped: only ever corrects a small UPWARD float — it never LIFTS the body, and it
        /// never sinks more than MaxDrop, so a bad measurement (e.g. a stray low vert on a cape)
        /// can't bury the hero under the terrain.
        /// </summary>
        private void PlantFeetOnGround(GameObject body)
        {
            if (body == null) return;

            float rootY = transform.position.y;
            float lowestY = float.PositiveInfinity;
            bool measured = false;

            // Prefer skinned meshes baked into their CURRENT pose — the static prefab mesh is in
            // bind pose and its lowest vertex won't match the posed soles. Bake into a scratch
            // mesh, transform each vertex to world space, and track the minimum Y.
            var skinned = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null && skinned.Length > 0)
            {
                var baked = new Mesh();
                foreach (var smr in skinned)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    smr.BakeMesh(baked); // baked is in the SMR's local space, current pose
                    var verts = baked.vertices;
                    Transform t = smr.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float wy = t.TransformPoint(verts[i]).y;
                        if (wy < lowestY) lowestY = wy;
                    }
                    measured = true;
                }
                Object.Destroy(baked);
            }

            // Fallback for any non-skinned (static MeshRenderer) body: walk its shared meshes'
            // vertices directly in world space. Rare for the hero, but keeps this robust.
            if (!measured)
            {
                foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    var verts = mf.sharedMesh.vertices;
                    Transform t = mf.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float wy = t.TransformPoint(verts[i]).y;
                        if (wy < lowestY) lowestY = wy;
                    }
                    measured = true;
                }
            }

            if (!measured || float.IsInfinity(lowestY)) return;

            // gap > 0 means the soles float ABOVE the ground (the DEF-235 symptom). gap <= 0 means
            // the feet already touch or sit below — leave those alone (never LIFT the body; sinking
            // is handled by SeatOnGround / terrain follow elsewhere).
            float gap = lowestY - rootY;
            const float MinFloat = 0.001f; // ignore sub-mm noise
            const float MaxDrop  = 0.30f;  // never sink more than 30 cm on a bad measurement
            if (gap <= MinFloat) return;
            float drop = Mathf.Min(gap, MaxDrop);
            body.transform.position -= new Vector3(0f, drop, 0f);
            Debug.Log("[HeroBodySwapper] DEF-235 planted feet: lowered body " +
                      drop.ToString("F3") + "m (measured float gap " + gap.ToString("F3") + "m).");
        }
    }
}
