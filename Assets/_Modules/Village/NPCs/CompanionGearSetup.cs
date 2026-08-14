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
using DeNelle.Core.Diagnostics;
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
            using var _ = FlowTrace.Enter("CompanionGear", $"Apply class={heroClass}");
            GearGrant grant = GrantFor(heroClass);
            try
            {
                GearLoadout loadout = ResolveLoadout();
                if (loadout != null)
                {
                    FlowTrace.Step("CompanionGear",
                        $"resolved GearLoadout on '{loadout.name}' — equipping armor='{grant.ArmorId}' weapon='{grant.WeaponId}'.");

                    // G (§12): each equip is GUARDED SEPARATELY. The owner symptom was
                    // EquipWeapon throwing AFTER EquipArmor succeeded under ONE coarse
                    // try/catch — leaving the companion ARMOR-ONLY with the failure silently
                    // swallowed. Isolating each equip means a weapon failure never discards
                    // the armor result (and vice-versa), and each is Fail-rolled-up on throw.
                    bool armorOk = false, weaponOk = false;
                    FlowTrace.Try("CompanionGear", $"EquipArmorById '{grant.ArmorId}'", () =>
                    {
                        loadout.EquipArmorById(grant.ArmorId);
                        armorOk = true;
                    });
                    FlowTrace.Try("CompanionGear", $"EquipWeaponById '{grant.WeaponId}'", () =>
                    {
                        loadout.EquipWeaponById(grant.WeaponId);
                        weaponOk = true;
                    });

                    // V (§12): VERIFY the equip actually TOOK. EquipById can return without
                    // throwing yet leave the slot null (id not in catalog / level-gated /
                    // resolve miss) — the exact "armor-only" symptom. Read the slots back and
                    // Fail-loud on any miss so a capture pinpoints which piece never landed,
                    // rather than the companion silently rendering half-equipped.
                    bool armorTook  = loadout.EquippedArmor  != null;
                    bool weaponTook = loadout.EquippedWeapon != null;
                    FlowTrace.Step("CompanionGear",
                        $"equip verify: armorCall={armorOk} armorSlot={armorTook} " +
                        $"weaponCall={weaponOk} weaponSlot={weaponTook} " +
                        $"(EquippedArmor='{loadout.EquippedArmor?.id ?? "<null>"}', EquippedWeapon='{loadout.EquippedWeapon?.id ?? "<null>"}').");

                    if (!armorTook)
                        FlowTrace.Fail("CompanionGear",
                            $"armor '{grant.ArmorId}' did NOT take (EquippedArmor null after EquipArmorById call={armorOk}) — companion may render without armor.");
                    if (!weaponTook)
                        FlowTrace.Fail("CompanionGear",
                            $"weapon '{grant.WeaponId}' did NOT take (EquippedWeapon null after EquipWeaponById call={weaponOk}) — companion may be left ARMOR-ONLY (the owner symptom).");

                    // R (§12): the equip pieces that DID land stay — we never roll the whole
                    // grant back on a single-slot miss (a half-grant beats no grant). The
                    // flourish still plays so the beat reads as an event regardless.

                    // A small flourish on the hero so the change reads as an event.
                    Vector3 at = loadout.transform.position + Vector3.up * 1.2f;
                    FlowTrace.Try("CompanionGear", "play gear-up VFX", () => VFXManager.Play(VFXType.Impact_Heal, at));
                    FlowTrace.Try("CompanionGear", "play gear-up SFX", () => GameSfx.PlayBuildingUpgrade());
                }
                else
                {
                    // U (§12): was Debug.LogWarning (doesn't roll up). No hero loadout = the
                    // grant can't equip at all — a Fail so a capture flags it.
                    FlowTrace.Fail("CompanionGear",
                        "no hero GearLoadout found (ResolveLoadout returned null) — skipping equip (toast still shown).");
                }

                // "+Iron Plate Armor" style HUD popup (armor is the headline piece).
                FlowTrace.Try("CompanionGear", "show gear-grant toast",
                    () => GearGrantToast.Show(grant.ArmorLabel, grant.WeaponLabel));
            }
            catch (Exception ex)
            {
                // U (§12): the coarse catch was a Debug.LogWarning swallow (§12-forbidden — it
                // never rolls up to the break-log). Promote to FlowTrace.Fail so any escape
                // from the guarded steps above self-reports loud, with the type + stack tail.
                FlowTrace.Fail("CompanionGear",
                    $"Apply threw past the per-step guards: {ex.GetType().Name}: {ex.Message}");
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
            using var _ = FlowTrace.Enter("CompanionGear", "ResolveLoadout");
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var root = player.transform.root != null ? player.transform.root.gameObject : player;
                var lo = root.GetComponentInChildren<GearLoadout>();
                if (lo == null)
                {
                    // G (§12): lazily add the loadout (same as HeroAbilities). Guarded so a
                    // failed AddComponent self-reports instead of throwing into the caller's
                    // catch unnamed; lo stays null -> caller Fails on the null loadout.
                    FlowTrace.Try("CompanionGear", "AddComponent<GearLoadout>",
                        () => lo = root.AddComponent<GearLoadout>());
                    // WO-976 sibling (registry LOW, deliberately ADVISORY): `result=ok` is a non-null
                    // check on an AddComponent that essentially cannot return null, so it is NOT
                    // coverage — it is a breadcrumb that the lazy-add branch was taken. Left in place
                    // and labelled rather than dressed up: the real failure handling is the
                    // FlowTrace.Try above (a throw self-reports) plus the caller's Fail on a null
                    // loadout. Do not read this line as verification.
                    FlowTrace.Step("CompanionGear",
                        $"no GearLoadout on '{root.name}' — lazily added (ADVISORY, not a verify: addReturned={(lo != null ? "non-null" : "<null>")}).");
                }
                else
                {
                    FlowTrace.Step("CompanionGear", $"found existing GearLoadout on '{lo.name}'.");
                }
                return lo;
            }

            var fallback = UnityEngine.Object.FindAnyObjectByType<GearLoadout>();
            if (fallback == null)
                FlowTrace.Warn("CompanionGear",
                    "no 'Player'-tagged hero and no GearLoadout anywhere in the scene — equip will be skipped.");
            return fallback;
        }
    }
}
