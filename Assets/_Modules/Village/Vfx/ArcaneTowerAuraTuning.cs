// =============================================================================
// ArcaneTowerAuraTuning - WO-1346. The "softly" half of the owner's five-word spec,
// expressed as ONE number that can move from the database instead of from a rebuild.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// HER TAG, VERBATIM (Assets/Editor/VfxManualPicks.json):
//     key        ArcaneTower_Aura
//     prefabPath Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_electric.prefab
//     isLoop     true      scale 1.0
// Her spec, verbatim: "arcane tower vfx (after built) softly".
//
// -----------------------------------------------------------------------------
// WHAT THIS FILE IS AND IS NOT.
//
// It is NOT a prefab pick. The key -> prefab mapping is HERS and lives in her JSON;
// nothing here reads a prefab path, chooses one, substitutes one or rescales one
// (memory vfx-map-owner-tags-no-creative-pick). Fog_electric.prefab is NOT touched
// on disk - it is a shared pack asset and editing it would silently change every
// other user of it.
//
// It IS the intensity dial. "Softly" is HER instruction about how loud the effect
// should be, so honouring it is following her tag rather than overriding it - but
// the EXACT value is a judgement she can only make with the phone in her hand.
// Standing owner ruling, 2026-09-02, verbatim: "be smart, dont make it need a code
// change, make it tweakable from a db call" / "i have been screaming this for
// months." So the value ships subdued and rides the existing tunables rail.
//
// -----------------------------------------------------------------------------
// WHY EMISSION DENSITY IS THE DIAL, and not alpha or scale.
//
//   * It is COLOURBLIND-SAFE. The owner is red/green colourblind and no effect may
//     carry meaning by hue. "Fewer particles" reads identically in greyscale; a
//     tint or an alpha wash does not.
//   * It is POOL-SAFE, which alpha is not. VFXManager hands out POOLED instances
//     and caches each ParticleSystem's authored start colour; a hand-written alpha
//     stamped onto an instance would ride that instance back into the pool and dim
//     the NEXT user of the same slot. VfxLoopModulator (WO-888) captures a pristine
//     baseline on first touch and is Restored from BOTH pool-return ends
//     (VFXHandle.Stop and VFXManager.ReturnToPool), so a modulated instance can
//     never reach the pool dirty. It is the sanctioned instance-level dial and it
//     already exists - this file adds no second mechanism.
//   * It does not touch her scale: 1.0 stays 1.0.
//
// -----------------------------------------------------------------------------
// THE NO-ROW INVARIANT (the one that outranks everything else here):
//     NO ROW, NO NETWORK, NO PARSE, NO SERVER, KEY NOT YET REGISTERED
//         => EXACTLY <see cref="SoftEmissionDefaultPct"/>.
// There is no path through SoftEmissionMul() that can answer anything else when
// the knob is absent, and none of them throws.
//
// ⭐ THE KEY IS NOT YET IN RemoteTunables.Registry, AND THAT IS DELIBERATE.
// WO-1343's agent is editing RemoteTunables.cs and the five other tunable sources
// in the same tree at the same time, and the WO-1346 fence is explicit: report a
// needed registry row rather than edit the same file. So this reader asks
// RemoteTunables.SpecFor first and only reads the knob once somebody has
// registered it. Until then it answers the shipping default WITHOUT emitting the
// "UNREGISTERED tunable key" caller-bug line, because reading an unregistered key
// answers 0 - and 0 emission is an INVISIBLE aura, i.e. exactly the failure this
// guard exists to prevent. The moment the six rows land the knob goes live with no
// further code change here. The six sources are enumerated in the WO-1346 RESULT.
//
// ASCII only. FlowTrace tag "TowerVfx". Never strip it (CLAUDE.md section 12).
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ops;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The owner-tagged Arcane Tower ambient aura key, and the tunable "softly"
    /// intensity applied to the spawned INSTANCE (never to the prefab).
    /// </summary>
    public static class ArcaneTowerAuraTuning
    {
        /// <summary>FlowTrace system tag for this lane.</summary>
        public const string Sys = "TowerVfx";

        /// <summary>
        /// HER KEY, verbatim from Assets/Editor/VfxManualPicks.json. Present here for
        /// readers and oracles ONLY - the live call site in ArcaneTower.Awake passes the
        /// same value as a STRING LITERAL on purpose, because VfxAuraDifferentiationRegression
        /// source-lints that literal out of the file with a regex and a const would read as
        /// a missing aura key and red the build gate.
        /// </summary>
        public const string AuraKey = "ArcaneTower_Aura";

        /// <summary>
        /// THE SHIPPING VALUE for "softly": percent of the prefab's AUTHORED particle
        /// emission density the tower's aura plays at. 100 would be the pack's own
        /// intensity; this is deliberately well under it because she asked for soft.
        /// <para>
        /// It is a starting point, not a verdict. She felt-verifies on device and moves
        /// it with a row - which is the entire reason it is a knob and not a constant.
        /// </para>
        /// </summary>
        public const int SoftEmissionDefaultPct = 45;

        /// <summary>
        /// Wire key for the intensity knob. Int, PERCENT. NOT yet registered in
        /// RemoteTunables.Registry - see this file's header for why, and the WO-1346
        /// RESULT for the six rows that turn it on.
        /// </summary>
        public const string KeyArcaneTowerAuraSoftPct = "vfx.arcaneTowerAuraSoftPct";

        /// <summary>
        /// The emission multiplier to apply to the spawned aura instance. 1.0 would be
        /// the pack's authored density; the shipped answer is
        /// <see cref="SoftEmissionDefaultPct"/> percent of it.
        /// <para>
        /// NEVER throws. Answers the shipping default for an unregistered key, an absent
        /// row, an unreachable server, a malformed value and an offline player alike.
        /// Clamped to 1..200 percent so neither a typo nor a fat-fingered row can make
        /// the aura INVISIBLE (0) - an invisible aura is indistinguishable from a broken
        /// one, and this effect is authored subtle enough already.
        /// </para>
        /// </summary>
        public static float SoftEmissionMul()
        {
            int pct = SoftEmissionDefaultPct;
            string provenance = "built-in default (knob not registered yet)";

            // Ask, do NOT assume. RemoteTunables.Int on an unregistered key answers 0 and
            // logs a caller bug - and 0 emission is an invisible aura, so the spec probe
            // is the guard rather than a nicety.
            if (RemoteTunables.SpecFor(KeyArcaneTowerAuraSoftPct) != null)
            {
                pct = RemoteTunables.Int(KeyArcaneTowerAuraSoftPct);
                provenance = "RemoteTunables (" + RemoteTunables.TableProvenance + ")";
            }

            int clamped = Mathf.Clamp(pct, 1, 200);
            if (clamped != pct)
            {
                FlowTrace.Warn(Sys,
                    "ArcaneTower aura softness " + pct + "% is outside the safe band 1..200 - " +
                    "clamped to " + clamped + "%. A 0 here would be an INVISIBLE aura, which reads " +
                    "as a broken effect rather than a soft one. Fix the row; nothing is broken meanwhile.");
            }

            FlowTrace.Once(Sys, "arcane-aura-soft=" + clamped,
                "ArcaneTower ambient aura softness = " + clamped + "% of the pack's authored emission " +
                "density, from " + provenance + " (shipping default " + SoftEmissionDefaultPct + "%). " +
                "Key '" + KeyArcaneTowerAuraSoftPct + "'. The prefab is NOT modified - this is an " +
                "instance-level VfxLoopModulator dial that is restored on pool return.");

            return clamped / 100f;
        }
    }
}
