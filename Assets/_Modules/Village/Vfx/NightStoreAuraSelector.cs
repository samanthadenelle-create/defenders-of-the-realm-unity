// =============================================================================
// NightStoreAuraSelector - WO-1343 Ask 2 + Ask 3. WHICH aura the Night Store
// wears right now, and WHAT the 30-minute clock does to it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// This type DECIDES. It never spawns. The one spawn owner for the Night Store's
// aura is and remains RealmStoreBeacon.StartNearAura -> VFXManager.PlayKey, and
// nothing here acquires a handle, holds a pool slot, or knows what a prefab is.
// (CLAUDE.md s7 / memory sequenced-vfx-special-cases-for-special-events: one
// owner per presence, and a selection policy is not a second spawner.)
//
// -----------------------------------------------------------------------------
// THE OWNER'S ASKS, AND WHY THE ANSWER IS A TUNABLE
// -----------------------------------------------------------------------------
// (2) verbatim: "then the one night realm or night store is to replace the
//     current one on the night store. its to be random when in town every 30~min"
// (2b) verbatim, later the same session: "i added another option for REalm store,
//     not sure which will be best"  -> a SECOND tagged candidate, Store_Aura.
// (3) verbatim: "there is a set of spells called aura ... can we use these slowly
//     one after another instead at the night store if the other one doesnt look
//     good"
//
// She has said out loud that she cannot pick yet. So all of it ships in ONE build
// and the choice is a DATABASE ROW, per her standing ruling (2026-09-02, verbatim):
// "be smart, dont make it need a code change, make it tweakable from a db call" /
// "i have been screaming this for months." A rebuild is ~30 minutes; a row is ~40
// seconds, and she can make the call on the device with the thing in front of her.
//
// -----------------------------------------------------------------------------
// (S) THE OWNER TAGS; THIS FILE NEVER PICKS.
// -----------------------------------------------------------------------------
// Memory vfx-map-owner-tags-no-creative-pick. Every key below is either
//   * a key SHE TAGGED in the Caster (NightStoreoption_Aura, Store_Aura), or
//   * the key ALREADY SHIPPING at this site (store.beacon.near), or
//   * a prefab FILE NAME read off the folder listing she screenshotted.
// No prefab was chosen, substituted or rescaled here. The seven family names are a
// directory listing, not a shortlist - and a family member she has not tagged in
// the Caster does not resolve and is SKIPPED, BY NAME, in the trace. Rotation
// therefore cannot invent art she never approved. Her first pick stays the
// DEFAULT; the second candidate is never promoted by this code's judgement.
//
// She is red/green colourblind: nothing in this policy carries meaning by hue.
// The mode is reported as a WORD in every trace line, never as a colour.
//
// -----------------------------------------------------------------------------
// (S) WHAT THE 30-MINUTE CLOCK MEANS - AND IT MEANS TWO DIFFERENT THINGS
// -----------------------------------------------------------------------------
// MEASURED, not assumed (the prefabs were counted, CLAUDE.md s12):
//     top_down_starfall_line_blue : 11 ParticleSystems, ALL looping:0  -> BURST
//     Loot_flicker                :  8 ParticleSystems, ALL looping:0  -> BURST
//     Aura_Arcane (the family)    :  5 ParticleSystems, ALL looping:1  -> CONTINUOUS
//
// So her isLoop:false on BOTH store candidates is CORRECT, and there is no flag
// conflict to work around at this site. A burst on a ~30-minute clock is not a
// broken loop - it is a periodic "look over here" pulse, which is a coherent shape
// for a storefront. The two candidate KINDS therefore give the same knob two honest
// meanings, and the code says which one is running:
//
//     BURST modes (her two tagged candidates)  -> the cadence RE-FIRES the burst.
//     ROTATE mode (the continuous Aura_* set)  -> the cadence ADVANCES to the next
//                                                 aura, which then plays until the
//                                                 next tick. One at a time; never
//                                                 two stacked ("slowly one after
//                                                 another" is a walk, not a shuffle).
//     LEGACY mode (the ring this build replaced) -> continuous; the cadence is inert.
//
// -----------------------------------------------------------------------------
// (S) THE NO-ROW INVARIANT (WO-1343 acceptance; RemoteTunables.cs header).
// -----------------------------------------------------------------------------
//     NO ROW, NO NETWORK, NO PARSE, NO SERVER  =>  THIS BUILD'S BEHAVIOUR, EXACTLY.
// Every knob read below goes through RemoteTunables.Int, which answers the shipping
// default for an absent row, an unreachable server, a malformed value and an
// unregistered key alike, and never throws. The shipped defaults are:
//     mode        = TaggedStarfall   (HER FIRST PICK; rotation is OFF)
//     cadence     = 30 minutes
//     family mask = all seven (127)  - inert unless mode is RotateFamily
//     burstRepeat = 0 s              - OFF; one burst per cadence, her spec literally
// so an offline player gets exactly the aura she tagged, pulsing every 30 minutes,
// and Ask 3 does not exist for that player until she turns it on.
//
// FlowTrace tag "NightStoreAura". Permanent (CLAUDE.md s12) - never stripped.
// ASCII only.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ops;

namespace DeNelle.Village
{
    /// <summary>What the Night Store's aura seat is currently driven by.</summary>
    public enum NightStoreAuraMode
    {
        /// <summary>SHIPPED DEFAULT. Her FIRST tagged candidate, <c>NightStoreoption_Aura</c>
        /// (top_down_starfall_line_blue). A burst; the cadence re-fires it.</summary>
        TaggedStarfall = 0,

        /// <summary>Her SECOND tagged candidate, <c>Store_Aura</c> (Loot_flicker), added when she
        /// said "not sure which will be best". Also a burst; the cadence re-fires it.</summary>
        TaggedLootFlicker = 1,

        /// <summary>Ask 3. Walk the continuous <c>Aura_*</c> family, one at a time, folder order.
        /// The cadence ADVANCES the walk rather than re-firing anything.</summary>
        RotateFamily = 2,

        /// <summary>The aura this build REPLACED (<c>store.beacon.near</c>). Continuous; the
        /// cadence is inert. A one-row undo of the whole swap.</summary>
        LegacyBeaconRing = 3,
    }

    /// <summary>What the cadence tick DOES in the current mode. Reported in every trace line so a
    /// capture never leaves the reader inferring whether a tick should have changed anything.</summary>
    public enum NightStoreCadenceMeaning
    {
        /// <summary>Re-fire the same one-shot burst.</summary>
        RefireBurst = 0,
        /// <summary>Advance to the next continuous aura in the rotation.</summary>
        AdvanceRotation = 1,
        /// <summary>Nothing: the selection is a continuous loop that is already playing.</summary>
        Inert = 2,
    }

    /// <summary>
    /// Pure, static, transport-free selection policy for the Night Store aura seat.
    /// Decides a KEY; spawns nothing. Headlessly drivable by a regression oracle
    /// (no MonoBehaviour, no scene, no VFXManager required) - see
    /// <see cref="SelectKey"/>'s <c>canPlay</c> parameter.
    /// </summary>
    public static class NightStoreAuraSelector
    {
        /// <summary>FlowTrace system tag for the whole night-store aura lane.</summary>
        public const string Sys = "NightStoreAura";

        // =====================================================================
        //  THE KEYS. Every one of these is HERS or ALREADY SHIPPING.
        // =====================================================================

        /// <summary>
        /// OWNER-TAGGED, verbatim from <c>Assets/Editor/VfxManualPicks.json</c>:
        /// <c>NightStoreoption_Aura</c> -&gt;
        /// <c>Assets/Resources/VFX/Aura/top_down_starfall_line_blue.prefab</c>, isLoop FALSE,
        /// scale 1.0. HER FIRST PICK and the shipped default. Mapped VERBATIM; never swapped,
        /// rescaled or re-pointed.
        /// </summary>
        public const string OwnerTaggedKey = "NightStoreoption_Aura";

        /// <summary>
        /// OWNER-TAGGED, verbatim: <c>Store_Aura</c> -&gt;
        /// <c>Assets/Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_flicker.prefab</c>, isLoop
        /// FALSE, scale 1.0. Her SECOND candidate ("not sure which will be best"). It is
        /// reachable by ONE row and is deliberately NOT the default - promoting it would be this
        /// code making the creative choice she reserved for herself.
        /// </summary>
        public const string OwnerTaggedAltKey = "Store_Aura";

        /// <summary>
        /// The aura this work order REPLACED at the Night Store: the Marker8 safe-zone ground
        /// ring (<c>store.beacon.near</c> -&gt;
        /// <c>Assets/Resources/VFX/Markers/Marker8_SafeZoneLoop.prefab</c>). Kept reachable as
        /// <see cref="NightStoreAuraMode.LegacyBeaconRing"/> so undoing the swap is one row,
        /// not a rebuild. Still the constant on <see cref="RealmStoreBeacon"/>.
        /// </summary>
        public const string LegacyBeaconKey = RealmStoreBeacon.NearAuraKey;

        /// <summary>
        /// The seven prefabs in <c>Assets/Spells Pack/Particles/Prefabs/Auras/</c>, in the folder's
        /// own alphabetical order. This is a DIRECTORY LISTING transcribed, not a shortlist and not
        /// a ranking - she screenshotted the folder and asked for "these ... slowly one after
        /// another". Nothing here decides which of them looks good.
        /// <para>
        /// (!) NONE OF THESE SEVEN CARRIES A CATALOG KEY TODAY. <c>VfxCasterLibraryIndex.json</c>
        /// lists all seven with <c>"key":""</c> / <c>"catalogued":false</c>, and
        /// <see cref="HovlVfxCatalog"/> is keyed by HER tag - so a prefab she has not tagged is
        /// not in the build and cannot be played. Rotation therefore SKIPS any member that does
        /// not resolve, names it in the trace, and falls back to her tagged key rather than
        /// leaving the store bare. When she tags them in the Caster they join the rotation with
        /// NO code change.
        /// </para>
        /// </summary>
        public static readonly string[] AuraFamilyPrefabNames =
        {
            "Aura_Arcane", "Aura_Dark", "Aura_Fire", "Aura_Ice",
            "Aura_Light",  "Aura_Nature", "Aura_Storm",
        };

        /// <summary>All seven family bits set. The shipped <see cref="FamilyMask"/> default.</summary>
        public const int AuraFamilyFullMask = 127;

        /// <summary>Highest valid <see cref="NightStoreAuraMode"/> ordinal.</summary>
        public const int MaxModeOrdinal = (int)NightStoreAuraMode.LegacyBeaconRing;

        // =====================================================================
        //  THE KNOBS. Four rows on the EXISTING rail; no second mechanism.
        // =====================================================================

        /// <summary>Resolved mode. Out-of-range values fall back to the shipped default, loudly.</summary>
        public static NightStoreAuraMode Mode
        {
            get
            {
                int raw = RemoteTunables.Int(RemoteTunables.KeyVfxNightStoreAuraMode);
                if (raw < 0 || raw > MaxModeOrdinal)
                {
                    FlowTrace.Throttle(Sys, "badmode:" + raw, 30f,
                        "night-store aura mode " + raw + " is not one of 0=TaggedStarfall / " +
                        "1=TaggedLootFlicker / 2=RotateFamily / 3=LegacyBeaconRing - IGNORED, " +
                        "falling back to the shipping default TaggedStarfall (her first tagged " +
                        "key '" + OwnerTaggedKey + "'). Fix the row; nothing is broken meanwhile.");
                    return NightStoreAuraMode.TaggedStarfall;
                }
                return (NightStoreAuraMode)raw;
            }
        }

        /// <summary>
        /// Seconds between cadence ticks. Clamped to 1 minute .. 24 hours: a cadence of zero would
        /// tick every frame and a negative one would never fire, and neither is a thing a human
        /// means to type on a phone.
        /// </summary>
        public static float CadenceSeconds
        {
            get
            {
                int minutes = RemoteTunables.Int(RemoteTunables.KeyVfxNightStoreAuraCadenceMin);
                if (minutes < 1) minutes = 1;
                if (minutes > 1440) minutes = 1440;
                return minutes * 60f;
            }
        }

        /// <summary>
        /// Which family members the rotation may select, as a bitmask over
        /// <see cref="AuraFamilyPrefabNames"/> (bit 0 = Aura_Arcane .. bit 6 = Aura_Storm).
        /// A mask is used rather than a string list so this rides the EXISTING integer rail
        /// instead of growing the tunables a new value kind - "take that one out of the rotation"
        /// is then a single number, with no code change and no schema change.
        /// A mask of 0 (or a nonsense value) means "no family members", which
        /// <see cref="SelectKey"/> reports and answers by falling back to her tagged key.
        /// </summary>
        public static int FamilyMask
            => RemoteTunables.Int(RemoteTunables.KeyVfxNightStoreAuraFamilyMask) & AuraFamilyFullMask;

        /// <summary>
        /// Seconds between EXTRA re-fires of a burst INSIDE one cadence period. 0 = OFF, and 0 is
        /// what ships: one burst per cadence tick, which is her spec read literally. It exists
        /// because "every 30~min" is a number she may want to feel out on the device - if one pulse
        /// per half hour reads as nothing at all, this makes the pulse a slow heartbeat without a
        /// rebuild. Clamped to 0..600. Meaningless (and ignored) in the two continuous modes.
        /// </summary>
        public static float BurstRepeatSeconds
        {
            get
            {
                int seconds = RemoteTunables.Int(RemoteTunables.KeyVfxNightStoreAuraBurstRepeatSec);
                if (seconds < 0) seconds = 0;
                if (seconds > 600) seconds = 600;
                return seconds;
            }
        }

        // =====================================================================
        //  SELECTION - pure. Give it a mode, a mask, a tick index and a resolver.
        // =====================================================================

        /// <summary>What a cadence tick does in <paramref name="mode"/>. See the file header.</summary>
        public static NightStoreCadenceMeaning CadenceMeaningFor(NightStoreAuraMode mode)
        {
            switch (mode)
            {
                case NightStoreAuraMode.RotateFamily:     return NightStoreCadenceMeaning.AdvanceRotation;
                case NightStoreAuraMode.LegacyBeaconRing: return NightStoreCadenceMeaning.Inert;
                default:                                  return NightStoreCadenceMeaning.RefireBurst;
            }
        }

        /// <summary>
        /// The candidate catalog keys for one family member, in the order they are tried.
        /// Both forms are MECHANICAL derivations of the prefab's file name - the bare name, and
        /// the name with the Caster's <c>_Aura</c> role suffix, which is exactly the shape
        /// <c>VfxCasterWindow.TagSelected</c> writes (<c>baseName + "_" + role</c>). No third form
        /// is guessed: if she tags a family member under some other base name, the FlowTrace names
        /// the candidates that were tried, and the fix is her tag - never a code change here.
        /// </summary>
        public static string[] FamilyCandidateKeys(int familyIndex)
        {
            if (familyIndex < 0 || familyIndex >= AuraFamilyPrefabNames.Length)
                return Array.Empty<string>();
            string name = AuraFamilyPrefabNames[familyIndex];
            return new[] { name, name + "_Aura" };
        }

        /// <summary>True when bit <paramref name="familyIndex"/> is set in <paramref name="mask"/>.</summary>
        public static bool IsFamilyMemberEnabled(int mask, int familyIndex)
            => familyIndex >= 0
               && familyIndex < AuraFamilyPrefabNames.Length
               && (mask & (1 << familyIndex)) != 0;

        /// <summary>
        /// THE ONE SELECTION FUNCTION. Answers the catalog key the Night Store seat should be
        /// wearing, and NEVER returns null or empty - the floor is always her FIRST tagged key, so
        /// a broken mask, an empty rotation and an entirely untagged family all degrade to the
        /// behaviour this build ships rather than to a bare store.
        /// <para>
        /// Pure and headlessly testable: <paramref name="canPlay"/> is injected (the runtime
        /// passes <c>VFXManager.CanPlayKey</c>; the oracle passes its own predicate), so the
        /// policy is provable with no scene, no pool and no VFXManager.
        /// </para>
        /// </summary>
        /// <param name="mode">Resolved <see cref="Mode"/>.</param>
        /// <param name="mask">Resolved <see cref="FamilyMask"/>.</param>
        /// <param name="tickIndex">Monotonic cadence-tick counter. Ask 3 is "slowly one after
        /// another", so rotation walks the enabled members in FOLDER ORDER by this index -
        /// ordered, never a shuffle, and exactly one at a time.</param>
        /// <param name="canPlay">Key -&gt; "does this resolve to a real prefab right now".
        /// Null = assume everything resolves (used only where order, not resolution, is asserted).</param>
        /// <param name="why">One-sentence provenance for the trace. Never null.</param>
        public static string SelectKey(NightStoreAuraMode mode, int mask, int tickIndex,
                                       Func<string, bool> canPlay, out string why)
        {
            if (mode == NightStoreAuraMode.LegacyBeaconRing)
            {
                why = "mode=LegacyBeaconRing - the continuous ring this build REPLACED (Marker8 " +
                      "safe-zone loop). The cadence is INERT here: it is already a loop. Set " +
                      RemoteTunables.KeyVfxNightStoreAuraMode + "=0 to go back to the owner-tagged '" +
                      OwnerTaggedKey + "'.";
                return LegacyBeaconKey;
            }

            if (mode == NightStoreAuraMode.TaggedLootFlicker)
            {
                why = "mode=TaggedLootFlicker - her SECOND tagged candidate '" + OwnerTaggedAltKey +
                      "' (Loot_flicker), selected by row. A one-shot burst: the cadence RE-FIRES it. " +
                      "This is not the shipped default - she has not picked between the two.";
                return OwnerTaggedAltKey;
            }

            if (mode == NightStoreAuraMode.RotateFamily)
            {
                int enabled = 0;
                for (int i = 0; i < AuraFamilyPrefabNames.Length; i++)
                    if (IsFamilyMemberEnabled(mask, i)) enabled++;

                if (enabled == 0)
                {
                    why = "mode=RotateFamily but " + RemoteTunables.KeyVfxNightStoreAuraFamilyMask +
                          "=" + mask + " enables NO family member - falling back to the owner-tagged '" +
                          OwnerTaggedKey + "'. Set the mask to " + AuraFamilyFullMask +
                          " for all seven; the store is not bare meanwhile.";
                    return OwnerTaggedKey;
                }

                // Walk the ENABLED members in folder order starting at the tick index. One at a
                // time, ordered, never stacked - "slowly one after another" is a walk, not a shuffle.
                int startOrdinal = tickIndex < 0 ? 0 : tickIndex % enabled;
                for (int step = 0; step < enabled; step++)
                {
                    int wanted = (startOrdinal + step) % enabled;
                    int familyIndex = FamilyIndexForOrdinal(mask, wanted);
                    if (familyIndex < 0) continue;

                    var candidates = FamilyCandidateKeys(familyIndex);
                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (canPlay != null && !canPlay(candidates[c])) continue;
                        why = "mode=RotateFamily tick=" + tickIndex + " -> family member '" +
                              AuraFamilyPrefabNames[familyIndex] + "' resolved as catalog key '" +
                              candidates[c] + "' (" + enabled + " member(s) enabled by mask " +
                              mask + "). Continuous effect: it holds until the next cadence tick.";
                        return candidates[c];
                    }

                    FlowTrace.Throttle(Sys, "family-unresolved:" + AuraFamilyPrefabNames[familyIndex], 60f,
                        "rotation SKIPPED family member '" + AuraFamilyPrefabNames[familyIndex] +
                        "': neither candidate key resolved ('" + string.Join("', '", candidates) +
                        "'). That prefab has no owner tag in the VFX Caster yet, so it is not in " +
                        "the build's catalog and cannot be played. Tag it and it joins the rotation " +
                        "with NO code change. Nothing was substituted for it.");
                }

                why = "mode=RotateFamily but NONE of the " + enabled + " enabled family member(s) " +
                      "resolved to a catalog key - none of the Aura_* prefabs is owner-tagged yet. " +
                      "Falling back to the owner-tagged '" + OwnerTaggedKey +
                      "', so the store keeps an aura she chose rather than going bare.";
                return OwnerTaggedKey;
            }

            why = "mode=TaggedStarfall (THE SHIPPED DEFAULT) - her first tagged key, played " +
                  "verbatim. A one-shot burst: the cadence RE-FIRES it. This is today's behaviour " +
                  "with no database row present.";
            return OwnerTaggedKey;
        }

        /// <summary>The family index of the Nth ENABLED member, or -1. Folder order throughout.</summary>
        private static int FamilyIndexForOrdinal(int mask, int ordinal)
        {
            int seen = 0;
            for (int i = 0; i < AuraFamilyPrefabNames.Length; i++)
            {
                if (!IsFamilyMemberEnabled(mask, i)) continue;
                if (seen == ordinal) return i;
                seen++;
            }
            return -1;
        }

        /// <summary>
        /// The whole selection configuration on ONE line, so a felt-test capture always says which
        /// configuration produced it. Same reasoning as RemoteTunables.LogConfiguration: a run whose
        /// configuration cannot be reconstructed afterwards is a wasted run.
        /// </summary>
        public static void LogConfiguration(string why)
        {
            Guard.Try(Sys, "log night-store aura configuration", () =>
            {
                var mode = Mode;
                FlowTrace.Step(Sys,
                    "CONFIG (" + (why ?? "?") + "): mode=" + mode +
                    "  cadenceTickMeans=" + CadenceMeaningFor(mode) +
                    "  cadence=" + (CadenceSeconds / 60f).ToString("0.#") + "min" +
                    "  familyMask=" + FamilyMask +
                    "  burstRepeat=" + BurstRepeatSeconds.ToString("0.#") + "s" +
                    "  candidateA='" + OwnerTaggedKey + "'" +
                    "  candidateB='" + OwnerTaggedAltKey + "'" +
                    "  legacy='" + LegacyBeaconKey + "'" +
                    (mode == NightStoreAuraMode.TaggedStarfall
                        ? " || SHIPPED DEFAULT - rotation is OFF and this is her first tag, unchanged."
                        : " || NOT the shipped default - quote this line in any felt-test report."));
            });
        }
    }
}
