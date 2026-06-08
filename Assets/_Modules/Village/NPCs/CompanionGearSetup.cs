// =============================================================================
// CompanionGearSetup — WO-364 hero gear-up beat (rides the wave-3 companion join).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// After the Wave-3 companion + Echo intro (ElaraWaveThreeJoin), the companion
// offers to outfit the HERO with class-appropriate gear. This static helper owns
// the gear-by-class table + the actual equip/visual/feedback so the dialogue file
// stays a thin orchestrator (same division as EchoTutorialUI / CompanionDialogue).
//
// ── What it does (Apply) ──────────────────────────────────────────────────────
//   1. Resolve the hero's GearLoadout (the gear v1 model on the hero root — the
//      SAME component HeroAbilities lazily attaches; it owns EquipWeaponById /
//      EquipArmorById, which update stats AND re-apply visuals via
//      GearVisualApplier). We add it if the hero has none yet so this works on
//      every hero with no scene/builder change.
//   2. Pick a weapon id + armor id by the hero's class from GearTable (existing
//      catalog ids in weapons.json / armor.json). Manual equip ignores the level
//      req (it's a story grant, not a level unlock), so a level-1 hero still gets
//      the proper piece.
//   3. Equip both -> the hero model updates (GearVisualApplier re-attaches the
//      weapon/armor accents to the body bones; the bow path is handled by
//      HeroBowAttachment as usual). A small heal-burst VFX + an equip SFX sell it.
//   4. Pop a non-blocking "+<Armor>" / "+<Weapon>" HUD toast (GearGrantToast,
//      mirrors EchoTutorialUI — code-built uGUI, WebGL-safe, no UXML).
//
// FORGE VISIT: kept OPTIONAL + SIMPLE. We do NOT build a walk-to-forge pathing
// cutscene (fragile, out of scope) — both player choices (visit the forge / "I'm
// already equipped") auto-equip IN PLACE; the choice only flavours the line. A
// real walk-to-forge sequence is flagged as a polish follow-up.
//
// Isolation/safety: lives in DeNelle.Village. Every step is null-guarded and the
// public entry is wrapped by the caller's try/catch. ASCII-only strings.
// =============================================================================

using System;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The gear-by-class grant applied when the wave-3 companion outfits the hero.
    /// Pure helper: <see cref="Apply"/> resolves the hero, equips the class gear via
    /// the existing <see cref="GearLoadout"/>, and fires VFX/SFX + a HUD toast.
    /// </summary>
    public static class CompanionGearSetup
    {
        /// <summary>A class's starter grant: catalog ids + the display labels shown
        /// in the toast + dialogue (labels are decoupled from ids so we can read
        /// "Iron Plate Armor" while equipping the catalog's armor piece).</summary>
        public struct GearGrant
        {
            public string WeaponId;
            public string ArmorId;
            public string WeaponLabel;
            public string ArmorLabel;
        }

        /// <summary>
        /// Gear-by-class table (WO-364). Ids resolve against weapons.json / armor.json;
        /// manual equip ignores the level req so the grant always lands.
        ///   Knight  -> Iron Longsword (knight_iron) + Elarion Plate (armor_plate)
        ///   Ranger  -> Hunter's Shortbow (ranger_starter) + Tanned Leather (armor_leather)
        ///   Mage    -> Oakheart Staff (mage_oak) + Wanderer's Cloth (armor_cloth)
        ///   Cleric  -> Squire's Blade (knight_starter) + Tanned Leather (armor_leather)
        ///   default -> Squire's Blade + Tanned Leather
        /// </summary>
        public static GearGrant GrantFor(HeroClass cls)
        {
            switch (cls)
            {
                case HeroClass.Knight:
                    return new GearGrant
                    {
                        WeaponId = "knight_iron",  WeaponLabel = "Iron Sword",
                        ArmorId  = "armor_plate",  ArmorLabel  = "Iron Plate Armor",
                    };
                case HeroClass.Ranger:
                    return new GearGrant
                    {
                        WeaponId = "ranger_starter", WeaponLabel = "Hunter's Bow",
                        ArmorId  = "armor_leather",  ArmorLabel  = "Leather Vest",
                    };
                case HeroClass.Mage:
                    return new GearGrant
                    {
                        WeaponId = "mage_oak",      WeaponLabel = "Oak Staff",
                        ArmorId  = "armor_cloth",   ArmorLabel  = "Cloth Robe",
                    };
                default: // Cleric + any future class -> sensible leather + sword
                    return new GearGrant
                    {
                        WeaponId = "knight_starter", WeaponLabel = "Iron Sword",
                        ArmorId  = "armor_leather",  ArmorLabel  = "Leather Vest",
                    };
            }
        }

        /// <summary>
        /// Outfit the hero with the grant for <paramref name="heroClass"/>: equip the
        /// weapon + armor on the hero's <see cref="GearLoadout"/> (updating the model),
        /// then play a small VFX/SFX flourish and pop a HUD toast. Returns the applied
        /// grant (for the dialogue follow-up line). Fully null-guarded; never throws.
        /// </summary>
        public static GearGrant Apply(HeroClass heroClass)
        {
            GearGrant grant = GrantFor(heroClass);
            try
            {
                GearLoadout loadout = ResolveLoadout();
                if (loadout != null)
                {
                    // Manual equip = a story grant: updates stats AND re-applies the
                    // visual (sword on hand / staff / bow via HeroBowAttachment, armor
                    // accents) through GearVisualApplier — the model updates.
                    loadout.EquipArmorById(grant.ArmorId);
                    loadout.EquipWeaponById(grant.WeaponId);

                    // A small flourish on the hero so the change reads as an event.
                    Vector3 at = loadout.transform.position + Vector3.up * 1.2f;
                    try { VFXManager.Play(VFXType.Impact_Heal, at); } catch { /* VFX optional */ }
                    try { GameSfx.PlayBuildingUpgrade(); } catch { /* SFX optional */ }
                }
                else
                {
                    Debug.LogWarning("[CompanionGearSetup] No hero GearLoadout found — skipping equip (toast still shown).");
                }

                // "+Iron Plate Armor" style HUD popup (armor is the headline piece).
                GearGrantToast.Show(grant.ArmorLabel, grant.WeaponLabel);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CompanionGearSetup] Gear-up error (continuing): {ex.Message}");
            }
            return grant;
        }

        /// <summary>
        /// Find the hero's <see cref="GearLoadout"/>. Prefers the "Player"-tagged hero
        /// (CLAUDE.md §7); lazily adds the component if the hero has none yet (same as
        /// HeroAbilities does). Falls back to any GearLoadout in the scene.
        /// </summary>
        private static GearLoadout ResolveLoadout()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var root = player.transform.root != null ? player.transform.root.gameObject : player;
                var lo = root.GetComponentInChildren<GearLoadout>();
                if (lo == null) lo = root.AddComponent<GearLoadout>();
                return lo;
            }

            return UnityEngine.Object.FindObjectOfType<GearLoadout>();
        }
    }
}
