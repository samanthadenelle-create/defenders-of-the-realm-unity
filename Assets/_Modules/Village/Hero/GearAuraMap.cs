// =============================================================================
// GearAuraMap - WO-888: pure resolver, equipped item -> a persistent AURA VFXType.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The registry (section 6c) calls this "the Aura beat sourced from an ITEM, not a cast":
// gear grants a persistent presence. This file is the ONE reader that turns authored
// item DATA into an aura type - exactly the shape WeaponVfxMap.ElementalOnHitKey already
// uses for on-hit elemental impacts, so there is no per-item branching anywhere else and
// a future element-branded weapon lights up with no code change.
//
// PURE: no MonoBehaviour, no I/O, no service calls. Deterministic and null-safe.
//
// ## WHAT IS REFUSED, AND WHY REFUSING IS THE CORRECT OUTCOME
//
// A gear aura is a PERSISTENT Family-A loop. Handing a loop handle to art that is not
// actually continuous produces an effect that fires ONCE and then holds one of the 20
// global loop slots showing nothing until the item is unequipped. The player sees a
// weapon that "hums" for half a second and then goes dead, and the loop budget silently
// shrinks. So a row is only served when the CATALOGUED ART measures continuous:
//
//   fire        -> Aura_Flame        SERVED. Lana Fire_medium: a disabled root shell over
//                                    a child emitting 15/sec on loop. VFXCatalogGenerator
//                                    calls this out by name as one of the four rows whose
//                                    loop flag survives only because the shared derivation
//                                    falls THROUGH the disabled shell (see its comment at
//                                    the IsLoop derivation). Genuinely continuous.
//   ice / frost -> Aura_Ice          SERVED. Lana Fog_frost, a drifting fog loop. The
//                                    registry's own "snow gap" approximation (section 8
//                                    item 1: ship the drift-motion approximation now).
//   arcane      -> REFUSED           Aura_EnemyCaster (Lana Orbs_electric) is named in
//                                    VFXCatalogGenerator as one of three rows that are
//                                    "rate-0 + single burst but declared isLoop: true" -
//                                    the art is a BURST. The registry's "arcane weapon hum"
//                                    needs a continuous recipe that does not exist yet.
//                                    Refused rather than faked into a dead loop slot.
//   lightning   -> REFUSED           Registry section 8 item 8 keeps lightning procedural
//                                    to avoid a gitignored Legacy-folder dependency; there
//                                    is no committed continuous lightning aura to hold.
//   others      -> none              holy / water / earth / nature / poison: HELD. No
//                                    owner-tagged continuous aura recipe. Same discipline
//                                    as WeaponVfxMap's HELD elements - add a case the day
//                                    the art is tagged, never a substitution.
//
// Every refusal returns its reason so the call site can trace it once instead of going
// quiet, which is how "my fire sword has no aura" gets diagnosed from a log rather than
// from a code read.
//
// ## WHY FIRE IS STILL Aura_Flame AFTER THE 2026-08-08 SURVEY (do not re-derive this)
//
// The owner asked, for the flaming-blade read: "I wanna use something like the VFX prefabs that
// are coming out of Unity. There are some good ones we can use." The tree WAS surveyed and the
// candidate is real and specific:
//   Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/
//       TinyFlames.prefab  (guid ea4fb55d0227e604386f44f905cfe7c0)
// It is already CATALOGUED as the key "PP_TinyFlames" in Resources/VFX/HovlVfxCatalog.asset
// (IsLoop:1, Recolorable:1, PoolSize:6), its material is URP-shaded (same shader guid as the
// working Lana particle materials), and it is a single clean flame layer with NO smoke child -
// so wiring it would be one call to VFXManager.PlayKey with no new plumbing.
//
// IT IS NOT WIRED, for two reasons, and BOTH are the owner's call to overrule:
//   1. THE PACK IS GITIGNORED. `git check-ignore` resolves the prefab to .gitignore:453
//      ("Assets/UnityTechnologies/"). It exists on this machine and NOT in the repo, so any build
//      made from a clean checkout resolves the catalog row to a null Prefab and PlayKey spawns
//      NOTHING - a flame blade with no flame, silently. That is the invisible-effect class this
//      project keeps paying for, one step subtler than a wrong path.
//   2. THIS EXACT SWAP WAS ALREADY RULED ON AND REFUSED, with measurements, in
//      Assets/Editor/ParticlePackVfxBatchBuilder.cs ("WO-889 Aura_Ice / Aura_Flame ... REPOINTS:
//      REFUSED"): layer count 5 vs 1, the incumbent already derives continuous, and "trading
//      tracked art for untracked art is the WO-785 exposure moving backwards". That note ends
//      "A human must rule explicitly if the thinner pack look is wanted anyway."
//
// So the 2026-08-08 work fixed the SEATING and the LAYER MIX of the incumbent recipe instead
// (GearAura: blade-length emitter above the grip, smoke + ground-shell layers muted) rather than
// swapping in art the repo does not carry. If the owner wants TinyFlames anyway, the decision is
// hers and the cost is that Assets/UnityTechnologies/ must be un-gitignored and committed first.
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// Pure resolver: an equipped <see cref="WeaponDef"/> / <see cref="AccessoryDef"/> to the
    /// persistent aura <see cref="VFXType"/> it grants (or none, with a reason).
    /// </summary>
    public static class GearAuraMap
    {
        /// <summary>The accessory `aura` tag that grants the heal relic's restoration aura.</summary>
        public const string HealAuraTag = "heal";

        /// <summary>
        /// The aura a weapon's elemental brand grants on its weapon socket, or false with a
        /// reason. Data-driven off <see cref="WeaponDef.element"/> - the same field
        /// WeaponVfxMap.ElementalOnHitKey reads - so one authored string drives both the
        /// on-hit burst and the idle aura and they can never disagree about the element.
        /// </summary>
        public static bool TryWeaponAura(WeaponDef w, out VFXType type, out string why)
        {
            type = VFXType.None;

            string element = Normalize(w != null ? w.element : null);
            if (element.Length == 0)
            {
                why = "no elemental brand on the equipped weapon";
                return false;
            }

            switch (element)
            {
                // Fire smoulder on the blade (registry 6c, reusing the 6d Aura_Flame recipe
                // at a faint tier). The only element any authored weapon carries today
                // (knight_flameblade, weapons.json - verified at source 2026-08-05).
                case "fire":
                    type = VFXType.Aura_Flame;
                    why  = "fire brand -> Aura_Flame (continuous: Lana Fire_medium, 15/sec loop child)";
                    return true;

                // Frost chill drift (registry 6c + the ratified section 8 item 1 snow-gap approximation).
                case "ice":
                case "frost":
                case "freeze":
                case "freezing":
                    type = VFXType.Aura_Ice;
                    why  = "frost brand -> Aura_Ice (continuous: Lana Fog_frost drift)";
                    return true;

                // REFUSED - see the header. The catalogued arcane aura art is a BURST.
                case "arcane":
                    why = "arcane brand HELD: the catalogued Aura_EnemyCaster art (Lana Orbs_electric) " +
                          "measures rate-0 + single burst - holding it as a loop would show one pop and " +
                          "then occupy a loop slot showing nothing. Needs a continuous arcane recipe.";
                    return false;

                case "lightning":
                case "electric":
                case "electricity":
                case "thunder":
                    why = "lightning brand HELD: registry section 8 item 8 keeps lightning procedural " +
                          "rather than take a gitignored Legacy-folder dependency; no committed " +
                          "continuous lightning aura exists to hold.";
                    return false;

                default:
                    why = "element '" + element + "' has no owner-tagged continuous aura recipe (HELD, " +
                          "never substituted - same discipline as WeaponVfxMap's held elements)";
                    return false;
            }
        }

        /// <summary>
        /// The aura an equipped accessory grants on the BODY. Data-driven off the optional
        /// <see cref="AccessoryDef.aura"/> tag; an untagged accessory grants nothing, which is
        /// every row in accessories.json today (verified at source 2026-08-05). Tagging one row
        /// with "aura": "heal" lights the restoration aura up with no code change - the tag is
        /// a CREATIVE call and belongs to the owner, so it is held, not guessed at here.
        /// Both slots are offered; the first tagged one wins (a hero wears one relic aura).
        /// </summary>
        public static bool TryBodyAura(AccessoryDef ring, AccessoryDef amulet,
                                       out VFXType type, out string why)
        {
            if (TryOne(amulet, out type, out why)) return true;   // amulet reads as the worn relic first
            if (TryOne(ring,   out type, out why)) return true;

            type = VFXType.None;
            why  = "no equipped accessory carries an `aura` tag (no accessories.json row is tagged yet)";
            return false;
        }

        private static bool TryOne(AccessoryDef a, out VFXType type, out string why)
        {
            type = VFXType.None;
            why  = null;

            string tag = Normalize(a != null ? a.aura : null);
            if (tag.Length == 0) return false;

            switch (tag)
            {
                case HealAuraTag:
                case "healaura":
                case "restoration":
                    type = VFXType.Aura_ItemHeal;
                    why  = "accessory '" + (a.id ?? "?") + "' aura='" + tag +
                           "' -> Aura_ItemHeal (continuous: RisingSteam low held, reuses the heal " +
                           "RISING language so the relic reads as restoration by motion, not by hue)";
                    return true;

                default:
                    why = "accessory '" + (a.id ?? "?") + "' carries an unrecognised aura tag '" + tag +
                          "' - held rather than substituted with a near-miss recipe";
                    return false;
            }
        }

        private static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : s.Trim().ToLowerInvariant();
    }
}
