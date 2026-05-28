// =============================================================================
// AddressablesGroupConfig — ScriptableObject typed asset-reference registry.
// -----------------------------------------------------------------------------
// One instance lives at Assets/Configs/Addressables/<GroupName>Config.asset.
// Any system that needs to load Addressable assets holds a [SerializeField]
// reference to the appropriate config, then calls LoadAsync<T> on the
// AssetReference field — never hardcoding an address string.
//
// Create via: right-click → Defenders/Addressables → Group Config
//
// Usage example:
//   [SerializeField] private TowersGroupConfig _towersConfig;
//   var handle = Addressables.LoadAssetAsync<GameObject>(_towersConfig.BlastTowerBase);
//
// Each concrete group config inherits from AddressablesGroupConfigBase and adds
// typed AssetReference fields for its content. The base class carries no data —
// it exists so the skin picker / profiler can hold a generic reference.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DeNelle.Core.AssetDelivery
{
    // ── Base ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Abstract base for all Addressables group config ScriptableObjects.
    /// Derive a concrete class per group (Towers, Pets, Heroes, VFX, Audio).
    /// </summary>
    public abstract class AddressablesGroupConfigBase : ScriptableObject
    {
        [Tooltip("Human-readable group name for debug displays.")]
        [SerializeField] public string GroupName = "Unnamed Group";
    }

    // ── Towers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>Towers-Base</c> and
    /// <c>Towers-Skins</c> Addressables groups.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/Towers Group Config",
                     fileName = "TowersGroupConfig")]
    public sealed class TowersGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Base Tower Prefabs (Towers-Base group)")]
        public AssetReferenceGameObject ArcaneTowerBase;
        public AssetReferenceGameObject FrostTowerBase;
        public AssetReferenceGameObject FlameTowerBase;
        public AssetReferenceGameObject ArrowTowerBase;

        [Header("Empowerment VFX (Towers-Empowerment group)")]
        public AssetReferenceGameObject ManaSurgeNovaPrefab;
        public AssetReferenceGameObject ManaSurgeAuraPrefab;
        public AssetReferenceGameObject GlacialCoreNovaPrefab;
        public AssetReferenceGameObject GlacialCoreAuraPrefab;
        public AssetReferenceGameObject EternalEmberNovaPrefab;
        public AssetReferenceGameObject EternalEmberAuraPrefab;
        public AssetReferenceGameObject TrueAimNovaPrefab;
        // TrueAim has no persistent aura (Physical element — intentional per spec)

        [Header("Tower Skins (Towers-Skins group) — address list per tower type")]
        [Tooltip("Each entry is an Addressables address for a skin asset (Material, Texture2D, or prefab). Assigned in Inspector.")]
        public List<AssetReference> ArcaneTowerSkins  = new List<AssetReference>();
        public List<AssetReference> FrostTowerSkins   = new List<AssetReference>();
        public List<AssetReference> FlameTowerSkins   = new List<AssetReference>();
        public List<AssetReference> ArrowTowerSkins   = new List<AssetReference>();
    }

    // ── Pets ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>Pets-Base</c> and
    /// <c>Pets-Skins</c> Addressables groups.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/Pets Group Config",
                     fileName = "PetsGroupConfig")]
    public sealed class PetsGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Base Pet Prefabs (Pets-Base group)")]
        public AssetReferenceGameObject FlamePupBase;
        public AssetReferenceGameObject AetherSpriteBase;
        // Add additional pets as the roster grows.

        [Header("Pet Skins (Pets-Skins group)")]
        public List<AssetReference> FlamePupSkins    = new List<AssetReference>();
        public List<AssetReference> AetherSpriteSkins = new List<AssetReference>();
    }

    // ── Heroes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>Heroes</c> Addressables group.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/Heroes Group Config",
                     fileName = "HeroesGroupConfig")]
    public sealed class HeroesGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Hero Base Models (Heroes group)")]
        public AssetReferenceGameObject BlaiseMaleBase;
        public AssetReferenceGameObject BlaiseFemaleBase;

        [Header("Hero Skins")]
        public List<AssetReference> BlaiseSkins = new List<AssetReference>();
    }

    // ── VFX ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>VFX</c> Addressables group.
    /// Corresponds to entries in <c>VFXCatalog</c>. Use the Addressables label
    /// <c>"vfx-combat"</c> for batch pre-warming at wave start.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/VFX Group Config",
                     fileName = "VFXGroupConfig")]
    public sealed class VFXGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Projectile VFX")]
        public AssetReferenceGameObject ProjectileTowerArcane;
        public AssetReferenceGameObject CastMageCharge;

        [Header("Impact VFX")]
        public AssetReferenceGameObject ImpactPhysical;
        public AssetReferenceGameObject ImpactAether;
        public AssetReferenceGameObject ImpactExplosionAether;
        public AssetReferenceGameObject ImpactFire;
        public AssetReferenceGameObject ImpactIce;

        [Header("Empowerment VFX (also listed in TowersGroupConfig for convenience)")]
        public List<AssetReference> EmpowermentNovas = new List<AssetReference>();
        public List<AssetReference> EmpowermentAuras = new List<AssetReference>();
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>Audio-Music</c> and
    /// <c>Audio-SFX</c> Addressables groups. Consumed by <c>AudioService</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/Audio Group Config",
                     fileName = "AudioGroupConfig")]
    public sealed class AudioGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Music (Audio-Music group — import as Streaming)")]
        public AssetReference ThemeVillage;
        public AssetReference ThemeWave;
        public AssetReference ThemeVictory;
        public AssetReference ThemeDungeon;

        [Header("SFX — pre-warm on scene load (Audio-SFX group)")]
        public AssetReference SfxTowerUpgrade;
        public AssetReference SfxTowerBuild;
        public AssetReference SfxWaveStart;
        public AssetReference SfxWaveClear;
        public AssetReference SfxPurchaseSuccess;
        public AssetReference SfxButtonClick;
        public AssetReference SfxEmpowerTower;

        [Header("SFX — load on demand")]
        public List<AssetReference> SfxCombatHits = new List<AssetReference>();
    }

    // ── Marketplace ───────────────────────────────────────────────────────────

    /// <summary>
    /// Typed asset references for the <c>Marketplace</c> Addressables group.
    /// Loaded when the player opens the PackStore.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Addressables/Marketplace Group Config",
                     fileName = "MarketplaceGroupConfig")]
    public sealed class MarketplaceGroupConfig : AddressablesGroupConfigBase
    {
        [Header("Pack Store UI Assets")]
        public AssetReference PackStoreUxml;
        public AssetReference PackStoreUss;

        [Header("Pack Artwork — one per pack (Hearth Spark → Founder's Vow)")]
        public List<AssetReference> PackArtwork = new List<AssetReference>();
    }
}
