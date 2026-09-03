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
    /// The named seats WO-1343 built but deliberately left UNBOUND, each waiting on exactly one
    /// owner tag in the VFX Caster. Every constant here is EMPTY on purpose - filling one in from
    /// this seat would be the creative pick the rule forbids.
    /// </summary>
    public static class HeldVfxKeys
    {
        /// <summary>
        /// THE FOOT OF THE TREE OF LIFE (WO-1343 Ask 1). Owner ask, verbatim: "one for the foot of
        /// the tree of life, to go with the other one" - ADDITIVE, alongside the existing
        /// <c>TreeofLifeAura_Aura</c> FireFlies loop, which is untouched.
        /// <para>
        /// (S) HELD, AND HELD FOR A SPECIFIC REASON. She tagged <c>atfootprintoftree_Aura</c> ->
        /// <c>Aura_Nature.prefab</c>. Within the hour that row read
        /// <c>Assets/Resources/VFX/Death/Elite_Death.prefab</c> instead, with a spurious sibling
        /// <c>atfootprintoftree_Impact</c> pointing at the same death effect - written while she was
        /// tagging a BOSS DEATH, not a tree. Wiring that row verbatim would seat a death EXPLOSION
        /// at the base of the Heart of Elarion. Wiring <c>Aura_Nature</c> back in on her behalf
        /// would be substituting a prefab for her, which is the same rule broken from the other
        /// side. So the seat is built, the key is EMPTY, and she retags.
        /// </para>
        /// </summary>
        public const string TreeOfLifeFootAura = "";

        /// <summary>
        /// BOSS DEATH (WO-1343 follow-up). Owner ask, verbatim: "added Elite death to boss death",
        /// intending <c>Assets/Resources/VFX/Death/Elite_Death.prefab</c>.
        /// <para>
        /// (S) HELD BECAUSE THERE IS NO BOSS-DEATH KEY TO MAP. <c>VfxManualPicks.json</c> contains
        /// no boss-death row at all: her intent landed on <c>atfootprintoftree_Aura</c> /
        /// <c>atfootprintoftree_Impact</c> instead. Inventing a key name here and pointing it at
        /// Elite_Death would be this seat authoring her tag. The hook is at the seam and waits.
        /// </para>
        /// </summary>
        public const string BossDeath = "";
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
