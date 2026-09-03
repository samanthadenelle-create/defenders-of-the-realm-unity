// =============================================================================
// HeldVfxHook - a NAMED, WIRED, DELIBERATELY UNBOUND VFX seat.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village   (WO-1343)
//
// -----------------------------------------------------------------------------
// (S) WHAT A "HELD" HOOK IS, AND WHY IT IS BETTER THAN NOT WIRING ONE
// -----------------------------------------------------------------------------
// The standing rule is memory vfx-map-owner-tags-no-creative-pick: the OWNER tags
// a VFX key in the Caster; the CLI maps key -> named hook VERBATIM and NEVER picks,
// substitutes or rescales a prefab. An ambiguous or suspect tag is HELD, not filled
// with a plausible guess.
//
// "Held" has historically meant "we wrote nothing", which quietly costs a whole
// round trip: she retags, and it still needs a code change, a gate, a build and a
// commit before she can see it. A HeldVfxHook closes that gap. The SEAT is built
// now - the position, the lifecycle, the trace, the call site - and only the KEY is
// left for her. When she tags it, it is a DATA change and the effect appears with
// no code touched at all.
//
// So a held hook is not a stub. It is the finished half of the work: everything
// except the one decision that is hers to make.
//
// -----------------------------------------------------------------------------
// (S) A HELD HOOK NEVER GUESSES, AND SAYS SO OUT LOUD
// -----------------------------------------------------------------------------
// Play() resolves ONLY the exact key it is given. It has no fallback key, no
// "nearest match", no default prefab. If the key is unset or has no catalog row it
// plays NOTHING and emits ONE line naming the seat, the key it wanted, and the fact
// that the absence is DELIBERATE. That line is the whole point: without it, "she
// tagged it and nothing happened" and "nobody has tagged it yet" look identical in
// a capture, and CLAUDE.md s12 exists because that ambiguity costs felt-test rounds.
//
// It also never spawns a pool or a second owner - it is a thin, traced pass-through
// to VFXManager.PlayKey, which remains the ONE spawn owner (CLAUDE.md s7).
//
// FlowTrace tag "HeldVfx". Permanent (CLAUDE.md s12) - never stripped.
// ASCII only.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The named seats WO-1343 built and deliberately left UNBOUND until the owner ruled.
    /// <para>
    /// (S) BOTH SEATS ARE NOW BOUND, AND THIS FILE IS THE ONE PLACE THEY ARE NAMED. She retagged
    /// on 2026-09-03 and the rows in <c>Assets/Editor/VfxManualPicks.json</c> now carry her picks.
    /// These constants are the SINGLE definition point for the two keys - nothing else in the game
    /// may write either key as a bare literal, which is pinned by
    /// <c>NightStoreAuraSelectionRegression</c> Case 6. Filling one in from this seat WITHOUT her
    /// word would still be the creative pick the rule forbids
    /// (memory vfx-map-owner-tags-no-creative-pick); the difference today is that the word exists.
    /// </para>
    /// </summary>
    public static class HeldVfxKeys
    {
        /// <summary>
        /// THE FOOT OF THE TREE OF LIFE (WO-1343 Ask 1). Owner ask, verbatim: "one for the foot of
        /// the tree of life, to go with the other one" - ADDITIVE, alongside the existing
        /// <c>TreeofLifeAura_Aura</c> FireFlies loop, which is untouched: BOTH play.
        /// <para>
        /// (S) BOUND 2026-09-03 TO HER RE-TAG. The row reads
        /// <c>atfootprintoftree_Aura -> Assets/Spells Pack/Particles/Prefabs/Auras/Aura_Nature.prefab</c>,
        /// <c>isLoop=true</c> (correct for an ambient aura). The seat was held because the VFX Caster
        /// had silently overwritten that same row with <c>Elite_Death.prefab</c> while she was
        /// tagging a BOSS DEATH, and had invented a sibling <c>_Impact</c> row she never authored.
        /// She has since re-pointed the Aura row and DELETED the invented sibling, so the row is
        /// hers again and the seat is wired to it verbatim - no prefab chosen here, no rescale
        /// (the call site passes scale 0 = the catalog row's own DefaultScale).
        /// </para>
        /// </summary>
        public const string TreeOfLifeFootAura = "atfootprintoftree_Aura";

        /// <summary>
        /// BOSS DEATH (WO-1343 follow-up). Owner ask, verbatim: "added Elite death to boss death";
        /// ruling verbatim: "both get Elite_Death, name it BossDeath_Impact".
        /// <para>
        /// (S) BOUND 2026-09-03 TO A KEY SHE NAMED HERSELF. The row reads
        /// <c>BossDeath_Impact -> Assets/Resources/VFX/Death/Elite_Death.prefab</c>,
        /// <c>isLoop=false</c> (correct for a death burst - do not "fix" it). Elite death and boss
        /// death SHARE the one effect deliberately; that is her ruling, not a fallback.
        /// </para>
        /// <para>
        /// COVERAGE, STATED NOT IMPLIED: this key is consumed by <c>EliteVFXController.OnEliteDeath</c>
        /// under <c>isBoss</c>, which covers the Enemy-tier bosses (the <c>_def.Boss</c> stat block).
        /// <c>DragonBoss</c> (Syndrath the Devourer) is a separate class with its own <c>Die()</c> and
        /// does NOT route through that component; whether the apex boss shares the effect is HER
        /// call and no second seat is added for it here.
        /// </para>
        /// </summary>
        public const string BossDeath = "BossDeath_Impact";
    }

    /// <summary>
    /// Plays a held VFX seat's key, or - far more usefully - says precisely why it did not.
    /// A thin traced pass-through to <see cref="VFXManager.PlayKey"/>; never a second spawner.
    /// </summary>
    public static class HeldVfxHook
    {
        /// <summary>FlowTrace system tag for every held seat.</summary>
        public const string Sys = "HeldVfx";

        /// <summary>
        /// Fire the seat named <paramref name="seatName"/> with catalog key <paramref name="key"/>
        /// at <paramref name="position"/>. Returns the loop handle when the row is a loop, and null
        /// for a one-shot, an empty key, or an unresolved key - the caller is never expected to
        /// distinguish those from the return value, because the trace already did.
        /// </summary>
        /// <param name="seatName">Human name of the seat, e.g. "tree-of-life foot".</param>
        /// <param name="key">The owner-tagged catalog key. Empty = still held.</param>
        /// <param name="position">World seat position, traced verbatim so a mis-seat is visible.</param>
        /// <param name="parent">Optional transform to follow.</param>
        /// <param name="scale">Uniform scale. 0 = the catalog row's own DefaultScale. ⛔ Callers
        /// pass 0 unless a PRE-EXISTING seat already had a scale - nothing rescales an owner
        /// prefab under this ticket.</param>
        /// <param name="heldReason">One sentence: WHY this seat has no key yet. Printed when the
        /// key is empty, so the capture explains itself without anyone opening a work order.</param>
        public static VFXHandle Play(string seatName, string key, Vector3 position,
                                     Transform parent = null, float scale = 0f,
                                     string heldReason = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                FlowTrace.Once(Sys, "held:" + seatName,
                    "SEAT '" + seatName + "' is WIRED and DELIBERATELY UNBOUND at " +
                    position.ToString("F2") + ": no owner-tagged key yet, so NOTHING was played and " +
                    "nothing was substituted for it. " +
                    (string.IsNullOrEmpty(heldReason)
                        ? "Awaiting an owner tag in the VFX Caster."
                        : heldReason) +
                    " When she tags it, filling in the key constant is a ONE-LINE DATA CHANGE - the " +
                    "seat, the position, the lifecycle and this trace already exist.");
                return null;
            }

            bool resolvable = VFXManager.CanPlayKey(key);
            var handle = VFXManager.PlayKey(key, position, Quaternion.identity, parent, null, scale);

            if (!resolvable)
            {
                FlowTrace.Throttle(Sys, "unresolved:" + seatName + ":" + key, 30f,
                    "SEAT '" + seatName + "': key '" + key + "' is BOUND but did NOT resolve - the " +
                    "HovlVfxCatalog has no row for it, or the row's prefab is null (pack not " +
                    "imported?). Nothing drew at " + position.ToString("F2") + ". This is a " +
                    "TAG/CATALOG problem, not a missing hook: the hook ran. Re-generate the catalog " +
                    "(Defenders/VFX/Generate Hovl VFX Catalog) or check the key spelling against " +
                    "Assets/Editor/VfxManualPicks.json.");
                return null;
            }

            FlowTrace.Step(Sys,
                "SEAT '" + seatName + "': key '" + key + "' played at " + position.ToString("F2") +
                " (parent=" + (parent == null ? "none" : parent.name) +
                ", scale=" + (scale <= 0f ? "catalog default" : scale.ToString("0.##")) +
                ", handle=" + (handle == null ? "null - one-shot row, burst auto-returned" : "held - loop row") + ").");
            return handle;
        }
    }
}
