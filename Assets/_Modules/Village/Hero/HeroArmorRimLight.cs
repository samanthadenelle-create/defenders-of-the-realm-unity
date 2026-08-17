// =============================================================================
// HeroArmorRimLight — WO-543: applies the ArmorVfxMap rim-light glow to the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The armor/accessory VFX channel's APPLIER (ArmorVfxMap is the pure resolver).
// Lazily attached to the hero by GearLoadout (mirrors AegisSetEffect) so every
// hero gets it with no builder/scene change. On each gear change it:
//   1. resolves the DOMINANT rarity across the equipped armor + ring + amulet,
//   2. applies the rim color/intensity to the hero SkinnedMeshRenderer(s) via a
//      MaterialPropertyBlock (no material instancing — cheap + reversible),
//   3. on the LEGENDARY apex, plays the slow "Burst_rings" particle if present.
//
// FULLY GUARDED (§12): a hero with no SkinnedMeshRenderer (not yet in scene / a
// non-rigged test body) logs via FlowTrace and is a no-op — never an NRE. Rim is
// driven through emission so it reads as a glow; common rarity (intensity 0)
// CLEARS the block so removing legendary gear drops the glow.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroArmorRimLight : MonoBehaviour
    {
        // URP/Lit emission property + a base-color tint fallback so the glow reads
        // even when the material's emission keyword is off (the MPB still tints).
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private GearLoadout _gear;
        private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
        private MaterialPropertyBlock _mpb;
        private bool _burstActive;

        private void Awake()
        {
            _gear = GetComponent<GearLoadout>();
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Re-resolve the rim-light from the current loadout and apply it to the hero mesh.
        /// Called by GearLoadout.ApplyStats on every equip/unequip. Safe to call repeatedly.
        /// </summary>
        public void Refresh()
        {
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            ArmorDef armor      = _gear != null ? _gear.EquippedArmor  : null;
            AccessoryDef ring   = _gear != null ? _gear.EquippedRing   : null;
            AccessoryDef amulet = _gear != null ? _gear.EquippedAmulet : null;

            ArmorVfxProfile vfx = ArmorVfxMap.Resolve(armor, ring, amulet);

            FlowTrace.Step("ArmorVfx",
                $"RESOLVE armor='{armor?.id ?? "<none>"}' ring='{ring?.id ?? "<none>"}' " +
                $"amulet='{amulet?.id ?? "<none>"}' -> rim=({vfx.RimColor.r:0.00},{vfx.RimColor.g:0.00}," +
                $"{vfx.RimColor.b:0.00}) intensity={vfx.RimIntensity:0.00} burst={vfx.LegendaryBurst}");

            Apply(vfx);
        }

        private void Apply(ArmorVfxProfile vfx)
        {
            EnsureRenderers();
            if (_renderers.Count == 0)
            {
                // No mesh yet (hero not fully spawned / non-rigged test body) — not an error.
                FlowTrace.Step("ArmorVfx", "APPLY skipped — no SkinnedMeshRenderer on hero (mesh not ready).");
                return;
            }

            Color emission = vfx.RimColor * Mathf.Max(0f, vfx.RimIntensity);
            int applied = 0;
            foreach (var smr in _renderers)
            {
                if (smr == null) continue;
                smr.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, emission);
                smr.SetPropertyBlock(_mpb);
                applied++;
            }

            FlowTrace.Step("ArmorVfx",
                $"APPLY rim emission=({emission.r:0.00},{emission.g:0.00},{emission.b:0.00}) " +
                $"to {applied} renderer(s).");

            UpdateLegendaryBurst(vfx.LegendaryBurst);
        }

        // Optional slow apex particle. Guarded load through the VfxAssetLoader seam
        // (Addressables-first, Resources-fallback) — an absent prefab is a graceful no-op
        // (a Debug-free skip), never a hard dependency on a specific VFX asset.
        private GameObject _burstInstance;

        private void UpdateLegendaryBurst(bool active)
        {
            if (active == _burstActive) return;
            _burstActive = active;

            if (active)
            {
                Guard.Try("ArmorVfx", "spawn legendary Burst_rings", () =>
                {
                    // Addressables-first / Resources-fallback seam (VfxAssetLoader) on the VFX key.
                    // The bare "Burst_rings" second try is a ROOT-Resources key (NOT under VFX/), so
                    // it stays a raw Resources.Load — routing it through the seam would query an
                    // address the VFX grouper never registers. See VfxAssetLoader KEY CONVENTION.
                    var prefab = DeNelle.Core.VfxAssetLoader.LoadVfxPrefab("VFX/Burst_rings");
                    if (prefab == null) prefab = Resources.Load<GameObject>("Burst_rings");
                    if (prefab != null)
                    {
                        _burstInstance = Instantiate(prefab, transform);
                        _burstInstance.transform.localPosition = Vector3.up * 1.0f;
                        FlowTrace.Step("ArmorVfx", "legendary Burst_rings particle attached to hero.");
                    }
                    else
                    {
                        FlowTrace.Step("ArmorVfx", "legendary apex reached — 'VFX/Burst_rings' found via NEITHER " +
                            "Addressables NOR Resources (VfxAssetLoader tried both), and no root-Resources 'Burst_rings' " +
                            "either (glow-only).");
                    }
                });
            }
            else if (_burstInstance != null)
            {
                Destroy(_burstInstance);
                _burstInstance = null;
            }
        }

        private void EnsureRenderers()
        {
            if (_renderers.Count > 0)
            {
                // Drop any destroyed renderers (body swap) and re-scan if all are gone.
                _renderers.RemoveAll(r => r == null);
                if (_renderers.Count > 0) return;
            }
            GetComponentsInChildren(true, _renderers);
        }
    }
}
