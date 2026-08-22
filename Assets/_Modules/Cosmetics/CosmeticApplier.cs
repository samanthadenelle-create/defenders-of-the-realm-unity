// =============================================================================
// CosmeticApplier — THE ONE APPEARANCE OWNER FOR A PURCHASED COSMETIC.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Cosmetics (same asmdef as GlimmerCurrencyService / CosmeticCatalog).
//
// ── THE DEFECT THIS FILE EXISTS TO CLOSE (2026-08-21, WO-992 finding) ────────
// Until today `ApplyCosmetic` was DEFINED HERE AND CALLED NOWHERE, and the GUID
// of this component sat on ZERO prefabs and ZERO scenes (raw-byte scan of all 12
// binary scenes). Equipping a cosmetic wrote a state flag
// (GlimmerCurrencyService.Equip) and changed NOTHING the player could see.
//
// That is not a dead-code curiosity, it is a paid-goods failure:
//   • Glimmer is earned in play (TierSystem.cs:189, Enemy.cs:3356,
//     DailyQuestRewardBridge, WaveFeedbackDirector.cs:143),
//   • spent in play (GlimmerCurrencyService.cs:137, BattlePassManager.cs:175),
//   • and SOLD FOR REAL MONEY (packs.json grants 25 glimmer with Hearth Spark,
//     50 with Starter's Hand).
// A player could pay cash, buy a skin in CosmeticShopPanel, equip it, and see
// nothing change. Nothing in the repo asserted otherwise, which is exactly how
// it shipped — see CosmeticApplyRegression [cosmetic-apply], added with this fix.
//
// ── ONE OWNER, NOT A SECOND SPAWNER (CLAUDE.md §7, the EchoWorldPresence rule) ─
// This component is the ONLY thing in the project that turns "cosmetic X is
// equipped" into "this object looks different". It does NOT spawn bodies, does
// NOT fight HeroArmorVisual / HeroBodySwapper / GearVisualApplier for ownership
// of the hero mesh, and does NOT keep its own copy of equip state. It attaches
// to a host that ALREADY owns its body, reads the equip state from
// GlimmerCurrencyService, and re-decorates whatever renderers that host is
// currently showing. When the host rebuilds its body it calls RefreshOn(host)
// and this re-resolves from scratch — the body owner stays the body owner.
//
// ── APPLY PRECEDENCE (first hit wins; every miss is TRACED, never silent) ────
//   1. materialOverride   — Inspector-assigned material, full swap.
//   2. prefabOverride     — Inspector-assigned replacement model.
//   3. def.MeshPath       — the catalog's OWN authored Resources key. cosmetics.json
//                           has carried `meshPath` since the pet-aether-twilight row
//                           was authored, and CosmeticDef DROPPED IT ON THE FLOOR
//                           (no field existed) until this change.
//   4. convention path    — Resources/<ResourceFolderFor(category)>/<id>.
//   5. previewColor tint  — the last-resort "you bought something and it shows"
//                           pass, applied through a MaterialPropertyBlock.
//
// ⚠ THE OWNER DECISION THIS CODE DOES NOT MAKE FOR HER. Step 5 tints the host's
// renderers flat to the shop swatch, because that is the only visual the DATA can
// pay for today: CosmeticDef ships previewColor and (on one row) meshPath, and
// Resources/Cosmetics/Pets/ is an EMPTY FOLDER — no cosmetic art exists in the
// tree at all. A flat swatch is honest but it is a PLACEHOLDER LOOK, so it is a
// per-host switch (`allowPreviewTintFallback`) and every use of it logs a Warn
// naming the exact asset path that would replace it. Ship art at those paths and
// step 3/4 takes over with ZERO code change.
//
// ── WHY A PROPERTY BLOCK AND NOT `renderer.material.color` ──────────────────
// The old code did `meshRenderer.material` (which INSTANTIATES) and restored by
// reassigning sharedMaterial — leaking every instance it made, and stamping over
// whatever material pipeline the body owner had just run (RetargetMaterialsToUrp,
// ApplyExtractedTexture, the Paladin/KnightV3 texture-wins passes). A
// MaterialPropertyBlock is per-renderer, allocation-free after the first frame,
// SRP-batcher friendly, and UNDOES CLEANLY (clear the block) — which is what lets
// ResetToDefault actually restore rather than approximate.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Cosmetics
{
    /// <summary>
    /// Applies the player's equipped cosmetic (material swap, prefab swap, VFX
    /// attach, or preview tint) to whatever renderers its host is currently
    /// showing. Attach with <see cref="Attach"/>; the host re-drives it with
    /// <see cref="RefreshOn"/> whenever it rebuilds its body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CosmeticApplier : MonoBehaviour
    {
        // ── Binding: WHICH cosmetic slot this host wears ──────────────────────
        [Header("Binding")]
        [Tooltip("Cosmetic category this host wears: hero / pet / village. Matches CosmeticDef.Category.")]
        public string category;

        [Tooltip("Which member of the category this host is: knight / mage / ranger / ice-wolf / ... " +
                 "Matches CosmeticDef.AppliesTo. Empty = accept any cosmetic in the category.")]
        public string appliesTo;

        [Tooltip("Allow the last-resort flat preview-colour tint when no cosmetic ART asset resolves. " +
                 "Turn OFF to make an artless cosmetic a no-op (with a Warn) instead of a flat swatch.")]
        public bool allowPreviewTintFallback = true;

        // ── Inspector references (all optional; resolved at runtime when null) ─
        [Header("References")]
        [Tooltip("Explicit renderer to decorate. Leave null to decorate every renderer under this host.")]
        public MeshRenderer  meshRenderer;

        [Tooltip("The default model root to hide when a prefab override is applied.")]
        public GameObject    defaultModel;

        [Tooltip("Parent transform for prefab-override and VFX instantiation. Defaults to this transform.")]
        public Transform     attachmentPoint;

        // ── Per-cosmetic art overrides (Inspector-assigned, optional) ─────────
        [Header("Art Overrides (highest precedence)")]
        [Tooltip("Material to swap to. Outranks every other path when assigned.")]
        public Material      materialOverride;

        [Tooltip("Replacement model prefab. Outranks the catalog meshPath when assigned.")]
        public GameObject    prefabOverride;

        [Tooltip("VFX prefab to attach at the attachment point.")]
        public GameObject    vfxPrefab;

        // ── Runtime state ─────────────────────────────────────────────────────
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private GameObject   _currentOverrideModel;
        private GameObject   _currentVfx;
        private string       _equippedCosmeticId;
        private bool         _tinted;
        private bool         _subscribed;

        /// <summary>Shader colour property names probed in order: URP first, then built-in.</summary>
        private static readonly string[] ColorProperties = { "_BaseColor", "_Color" };

        // =====================================================================
        //  Static seam — how a body owner installs and re-drives this
        // =====================================================================

        /// <summary>
        /// Installs (or re-binds) the one applier on <paramref name="host"/> and refreshes it.
        /// Idempotent: calling it again on a host that already has one re-binds and refreshes
        /// rather than adding a second. Returns null only for a null host.
        /// </summary>
        public static CosmeticApplier Attach(GameObject host, string category, string appliesTo,
                                             bool allowPreviewTint = true)
        {
            if (host == null)
            {
                FlowTrace.Warn("Cosmetics", "Attach: null host — no applier installed.");
                return null;
            }

            var applier = host.GetComponent<CosmeticApplier>();
            if (applier == null) applier = host.AddComponent<CosmeticApplier>();

            applier.category  = category;
            applier.appliesTo = appliesTo;
            applier.allowPreviewTintFallback = allowPreviewTint;
            applier.Refresh();
            return applier;
        }

        /// <summary>
        /// Re-drives the applier on <paramref name="host"/>, if it has one. Body owners call this
        /// the moment they REBUILD the visible body (an armour swap, a re-skin) — the renderer set
        /// this component decorated a frame ago may no longer exist. Safe on a host with no applier.
        /// </summary>
        public static void RefreshOn(GameObject host)
        {
            if (host == null) return;
            var applier = host.GetComponent<CosmeticApplier>();
            applier?.Refresh();
        }

        /// <summary>
        /// The Resources folder a category's cosmetic art lives under, e.g. "Cosmetics/Pets".
        /// <para>⚠ The PET value is PINNED BY LIVE CODE: PetDeployer.TryLoadPetMesh loads
        /// <c>Resources.Load&lt;GameObject&gt;("Cosmetics/Pets/" + equippedId)</c> and CANNOT call this
        /// method — DeNelle.Pets does not reference DeNelle.Cosmetics (it reaches the wallet by
        /// reflection for exactly that reason). Changing "Pets" here without changing that literal
        /// re-creates the duplicated-constant failure CLAUDE.md catalogues in §2/§5/§16.
        /// CosmeticApplyRegression [cosmetic-apply] asserts the two still agree.</para>
        /// </summary>
        public static string ResourceFolderFor(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;
            switch (category.Trim().ToLowerInvariant())
            {
                case "hero":    return "Cosmetics/Heroes";
                case "pet":     return "Cosmetics/Pets";
                case "village": return "Cosmetics/Village";
                default:        return null;
            }
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            var svc = GlimmerCurrencyService.Instance;
            if (svc == null) return;          // bootstraps BeforeSceneLoad; Refresh re-tries.
            svc.Changed += Refresh;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var svc = GlimmerCurrencyService.Instance;
            if (svc != null) svc.Changed -= Refresh;
            _subscribed = false;
        }

        // =====================================================================
        //  Public API
        // =====================================================================

        /// <summary>The cosmetic id currently applied, or null when none is.</summary>
        public string EquippedCosmeticId => _equippedCosmeticId;

        /// <summary>How many renderers this applier is currently decorating. Oracle surface.</summary>
        public int DecoratedRendererCount => _renderers.Count;

        /// <summary>True when the last apply landed as a preview-colour tint (placeholder art).</summary>
        public bool UsingPreviewTint => _tinted;

        /// <summary>Binds this applier to a category + member without refreshing.</summary>
        public void Bind(string cosmeticCategory, string cosmeticAppliesTo)
        {
            category  = cosmeticCategory;
            appliesTo = cosmeticAppliesTo;
        }

        /// <summary>
        /// Re-reads the equipped cosmetic for this host's category and applies it — or clears
        /// back to default when nothing (valid) is equipped. This is the ONE entry point; every
        /// trigger (equip change, body rebuild, enable) funnels through it, so the applied look
        /// can never disagree with the saved equip state.
        /// </summary>
        public void Refresh()
        {
            using var _ = FlowTrace.Enter("Cosmetics", $"Refresh(category='{category}') on '{name}'");

            Subscribe();   // late-bootstrapped service — pick the subscription up as soon as it exists.

            if (string.IsNullOrEmpty(category))
            {
                FlowTrace.Warn("Cosmetics", $"Refresh: '{name}' has no category bound — nothing to resolve.");
                return;
            }

            var svc = GlimmerCurrencyService.Instance;
            if (svc == null)
            {
                FlowTrace.Warn("Cosmetics",
                    $"Refresh: GlimmerCurrencyService.Instance is null — '{name}' keeps its default look. " +
                    "The service bootstraps BeforeSceneLoad; a null here means it was destroyed or never ran.");
                return;
            }

            string id = svc.EquippedFor(category);
            if (string.IsNullOrEmpty(id))
            {
                ResetToDefault();
                FlowTrace.Step("Cosmetics", $"Refresh: nothing equipped in '{category}' — '{name}' shows its default.");
                return;
            }

            var def = CosmeticCatalog.Find(id);
            if (def == null)
            {
                ResetToDefault();
                FlowTrace.Fail("Cosmetics",
                    $"Refresh: equipped id '{id}' is NOT in cosmetics.json — the player owns something the " +
                    "catalog cannot describe, so nothing can be applied. Check the catalog dual-copy " +
                    "(StreamingAssets + Resources) for a dropped row.");
                return;
            }

            // A cosmetic names WHAT it re-skins (mage / ice-wolf / wall-tier-2). A host bound to a
            // specific member ignores a cosmetic aimed at a different one — that is what stops the
            // Frostfall KNIGHT skin from landing on the mage.
            if (!MatchesHost(def))
            {
                ResetToDefault();
                FlowTrace.Step("Cosmetics",
                    $"Refresh: equipped '{id}' targets appliesTo='{def.AppliesTo}' but '{name}' is " +
                    $"'{appliesTo}' — not this host's skin, default kept.");
                return;
            }

            ApplyCosmetic(def);
        }

        /// <summary>
        /// Applies a cosmetic by catalog id. No-op (traced) on an empty id or a catalog miss.
        /// </summary>
        public void ApplyCosmetic(string cosmeticId)
        {
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyCosmetic(id='{cosmeticId}') on '{name}'");
            if (string.IsNullOrEmpty(cosmeticId))
            {
                FlowTrace.Warn("Cosmetics", "ApplyCosmetic: empty cosmetic id — no-op.");
                return;
            }

            var def = CosmeticCatalog.Find(cosmeticId);
            if (def == null)
            {
                FlowTrace.Fail("Cosmetics", $"ApplyCosmetic: id '{cosmeticId}' not found in catalog — nothing applied.");
                return;
            }

            ApplyCosmetic(def);
        }

        /// <summary>
        /// Applies a cosmetic the caller already resolved. Runs the full precedence ladder
        /// (material override → prefab override → catalog meshPath → convention path → preview tint).
        /// </summary>
        public void ApplyCosmetic(CosmeticDef cosmetic)
        {
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyCosmetic(def='{cosmetic?.Id}') on '{name}'");
            if (cosmetic == null)
            {
                FlowTrace.Warn("Cosmetics", "ApplyCosmetic(def): null CosmeticDef — no-op.");
                return;
            }

            _equippedCosmeticId = cosmetic.Id;
            ResolveRenderers();

            bool art = ApplyArtModel(cosmetic);   // steps 2-4: a real replacement body
            ApplyMaterial(cosmetic, art);         // steps 1 + 5: material swap, else preview tint
            ApplyVfx();

            FlowTrace.Step("Cosmetics",
                $"ApplyCosmetic: '{cosmetic.DisplayName}' applied to '{name}' " +
                $"(renderers={_renderers.Count}, artModel={art}, previewTint={_tinted}).");
        }

        /// <summary>
        /// Restores the host to its default look: clears every property block this applier set,
        /// destroys any override model and VFX, and re-shows the default model.
        /// </summary>
        public void ResetToDefault()
        {
            ClearTint();

            if (_currentOverrideModel != null)
            {
                Destroy(_currentOverrideModel);
                _currentOverrideModel = null;
            }

            RestoreDefaultModel();

            if (_currentVfx != null)
            {
                Destroy(_currentVfx);
                _currentVfx = null;
            }

            _equippedCosmeticId = null;
        }

        // =====================================================================
        //  Renderer resolution
        // =====================================================================

        /// <summary>
        /// Rebuilds the renderer set FROM SCRATCH every apply. This is what makes the component
        /// survive a body rebuild it does not own: HeroArmorVisual can swap the entire skinned mesh
        /// out from under it and the next Refresh simply decorates whatever is there now.
        /// Counts SkinnedMeshRenderer as well as MeshRenderer — the hero and every pet body are
        /// SKINNED, which is why the old [RequireComponent(MeshRenderer)] shape could never have
        /// worked on either of them.
        /// </summary>
        private void ResolveRenderers()
        {
            _renderers.Clear();

            if (meshRenderer != null)
            {
                _renderers.Add(meshRenderer);
                return;
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r is ParticleSystemRenderer) continue;   // VFX are not skin.
                if (_currentOverrideModel != null && r.transform.IsChildOf(_currentOverrideModel.transform))
                    continue;                                // the override model brings its own look.
                _renderers.Add(r);
            }

            if (_renderers.Count == 0)
                FlowTrace.Warn("Cosmetics",
                    $"ResolveRenderers: '{name}' has NO renderer under it — an equipped cosmetic cannot " +
                    "reach anything the player sees. The host was probably bound before its body was built.");
        }

        private bool MatchesHost(CosmeticDef def)
        {
            if (def == null) return false;
            if (string.IsNullOrEmpty(appliesTo)) return true;         // host accepts the whole category
            if (string.IsNullOrEmpty(def.AppliesTo)) return true;     // cosmetic targets the whole category
            return string.Equals(def.AppliesTo, appliesTo, System.StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        //  Step 2-4 — a real replacement model
        // =====================================================================

        /// <summary>
        /// Instantiates the cosmetic's replacement model, if one exists. Returns true when a
        /// verified, RENDERING replacement is now on the host.
        /// </summary>
        private bool ApplyArtModel(CosmeticDef def)
        {
            GameObject prefab = prefabOverride;
            string source = "Inspector prefabOverride";

            if (prefab == null)
            {
                // The catalog's own authored key. cosmetics.json has carried meshPath since the
                // pet-aether-twilight row was written; CosmeticDef had no field for it, so it was
                // parsed and discarded on every load.
                if (!string.IsNullOrEmpty(def.MeshPath))
                {
                    prefab = Resources.Load<GameObject>(def.MeshPath);
                    source = $"catalog meshPath '{def.MeshPath}'";
                    if (prefab == null)
                        FlowTrace.Warn("Cosmetics",
                            $"ApplyArtModel: '{def.Id}' authors meshPath '{def.MeshPath}' but NOTHING loads from " +
                            $"Resources/{def.MeshPath}. FIX (asset): ship exactly one GameObject there.");
                }
            }

            if (prefab == null)
            {
                string folder = ResourceFolderFor(def.Category);
                if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(def.Id))
                {
                    string key = folder + "/" + def.Id;
                    prefab = Resources.Load<GameObject>(key);
                    source = $"convention path '{key}'";
                    if (prefab == null)
                        FlowTrace.Warn("Cosmetics",
                            $"ApplyArtModel: no cosmetic art for '{def.Id}' at Resources/{key} — " +
                            "falling through to the preview-colour placeholder. FIX (asset, not code): " +
                            $"ship the skin at Resources/{key} and it is picked up with no code change.");
                }
            }

            if (prefab == null) return false;

            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyArtModel('{prefab.name}' via {source}) on '{name}'");

            // Tear down any prior override FIRST — the default is still visible, so no blank flash.
            if (_currentOverrideModel != null)
            {
                Destroy(_currentOverrideModel);
                _currentOverrideModel = null;
            }

            var parent = attachmentPoint != null ? attachmentPoint : transform;

            // GUARDED instantiate. The pre-2026-08-21 order hid the default model BEFORE an
            // unguarded Instantiate, so a throw left a PERMANENTLY INVISIBLE object with no rollback.
            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab, parent);
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyArtModel: Instantiate threw for '{prefab.name}': {ex.GetType().Name}: {ex.Message} — " +
                    "keeping the default model (never an invisible object).");
                RestoreDefaultModel();
                return false;
            }

            if (instance == null)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyArtModel: Instantiate returned null for '{prefab.name}' — keeping the default model.");
                RestoreDefaultModel();
                return false;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // RENDER-VERIFY: the replacement must actually carry a mesh, so the default is never
            // hidden for something that shows nothing.
            if (!OverrideRenders(instance))
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyArtModel: override '{prefab.name}' has no visible renderer — dropping it and " +
                    "keeping the default model (never an invisible object).");
                Destroy(instance);
                RestoreDefaultModel();
                return false;
            }

            // Confirmed renderable — only NOW is it safe to hide the default.
            _currentOverrideModel = instance;
            if (defaultModel != null) defaultModel.SetActive(false);

            FlowTrace.Step("Cosmetics",
                $"ApplyArtModel: '{prefab.name}' instantiated + renders ({source}); default model hidden.");
            return true;
        }

        /// <summary>True when the instance carries at least one renderer with a real mesh.</summary>
        private static bool OverrideRenders(GameObject instance)
        {
            if (instance == null) return false;
            foreach (var mr in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return true;
            }
            foreach (var sr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sr != null && sr.sharedMesh != null) return true;
            }
            return false;
        }

        /// <summary>Re-enables the default model — the never-invisible fallback.</summary>
        private void RestoreDefaultModel()
        {
            if (defaultModel != null && !defaultModel.activeSelf)
                defaultModel.SetActive(true);
        }

        // =====================================================================
        //  Step 1 + 5 — material swap, else the preview-colour placeholder
        // =====================================================================

        private void ApplyMaterial(CosmeticDef def, bool artModelApplied)
        {
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyMaterial('{def?.Id}') on '{name}'");

            if (materialOverride != null)
            {
                ClearTint();
                int swapped = 0;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    r.sharedMaterial = materialOverride;
                    if (r.sharedMaterial != null) swapped++;
                }
                if (swapped == 0)
                    FlowTrace.Fail("Cosmetics",
                        $"ApplyMaterial: material override for '{def?.Id}' reached NO renderer on '{name}'.");
                else
                    FlowTrace.Step("Cosmetics",
                        $"ApplyMaterial: swapped {swapped} renderer(s) to '{materialOverride.name}' for '{def?.Id}'.");
                return;
            }

            if (artModelApplied)
            {
                // Real art won. Tinting it would stamp over the very look that was shipped.
                ClearTint();
                FlowTrace.Step("Cosmetics",
                    $"ApplyMaterial: '{def?.Id}' resolved a real art model — NOT tinting over it.");
                return;
            }

            if (!allowPreviewTintFallback)
            {
                ClearTint();
                FlowTrace.Warn("Cosmetics",
                    $"ApplyMaterial: '{def?.Id}' has no art asset and this host has the preview-tint " +
                    "fallback DISABLED — the equipped cosmetic changes nothing the player can see.");
                return;
            }

            ApplyPreviewTint(def);
        }

        /// <summary>
        /// The placeholder pass: push the shop swatch onto every renderer through a
        /// MaterialPropertyBlock, and READ IT BACK (owner directive 2026-06-19 — "anything that
        /// renders can be broken; read back the applied tint"). A shader with no colour slot
        /// silently drops the write, and the read-back turns that into a named Warn instead of a
        /// wrong colour nobody can explain.
        /// </summary>
        private void ApplyPreviewTint(CosmeticDef def)
        {
            if (_renderers.Count == 0)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyPreviewTint: '{def?.Id}' equipped but '{name}' exposes NO renderer — " +
                    "the purchase is invisible. This is the WO-992 defect signature.");
                return;
            }

            Color want = def != null ? def.PreviewUnityColor : Color.white;
            var block = new MaterialPropertyBlock();
            int applied = 0;
            int noSlot = 0;

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                string prop = ResolveColorProperty(r);
                if (prop == null) { noSlot++; continue; }

                r.GetPropertyBlock(block);
                block.SetColor(prop, want);
                r.SetPropertyBlock(block);

                // READ-BACK VERIFY on the live renderer, not on our own local copy.
                var check = new MaterialPropertyBlock();
                r.GetPropertyBlock(check);
                Color got = check.GetColor(prop);
                bool took = Mathf.Approximately(got.r, want.r) &&
                            Mathf.Approximately(got.g, want.g) &&
                            Mathf.Approximately(got.b, want.b);
                if (took) applied++;
                else
                    FlowTrace.Warn("Cosmetics",
                        $"ApplyPreviewTint: tint for '{def?.Id}' did NOT read back on '{r.name}' " +
                        $"(wanted {want}, got {got}, property '{prop}').");
            }

            _tinted = applied > 0;

            if (applied == 0)
                FlowTrace.Fail("Cosmetics",
                    $"ApplyPreviewTint: '{def?.Id}' reached ZERO renderers on '{name}' " +
                    $"({noSlot} had no colour property) — the player sees no change.");
            else
                FlowTrace.Step("Cosmetics",
                    $"ApplyPreviewTint: '{def?.Id}' -> {want} on {applied}/{_renderers.Count} renderer(s) " +
                    $"of '{name}' (verified by read-back). PLACEHOLDER LOOK — ships real art at " +
                    $"Resources/{ResourceFolderFor(def?.Category)}/{def?.Id}.");
        }

        /// <summary>
        /// The colour property this renderer's shader actually exposes, or null when it has none.
        /// URP's `_BaseColor` first, built-in `_Color` second — probing rather than assuming is what
        /// makes the read-back above meaningful instead of a tautology.
        /// </summary>
        private static string ResolveColorProperty(Renderer r)
        {
            var mat = r != null ? r.sharedMaterial : null;
            if (mat == null) return null;
            for (int i = 0; i < ColorProperties.Length; i++)
                if (mat.HasProperty(ColorProperties[i])) return ColorProperties[i];
            return null;
        }

        /// <summary>Clears every property block this applier set. Full, allocation-cheap undo.</summary>
        private void ClearTint()
        {
            if (!_tinted) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
            _tinted = false;
        }

        // =====================================================================
        //  VFX
        // =====================================================================

        private void ApplyVfx()
        {
            if (vfxPrefab == null) return;

            if (_currentVfx != null)
            {
                Destroy(_currentVfx);
                _currentVfx = null;
            }

            var parent = attachmentPoint != null ? attachmentPoint : transform;
            _currentVfx = Instantiate(vfxPrefab, parent);
            if (_currentVfx == null)
            {
                FlowTrace.Fail("Cosmetics", $"ApplyVfx: Instantiate returned null for '{vfxPrefab.name}'.");
                return;
            }
            _currentVfx.transform.localPosition = Vector3.zero;
            _currentVfx.transform.localRotation = Quaternion.identity;
        }
    }
}
