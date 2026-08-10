// =============================================================================
// ResourceGainPopup — the income-pop FACADE over the damage-number pool (WO-953).
// -----------------------------------------------------------------------------
// HISTORY: this used to be its own MonoBehaviour stack — a per-call
// `new GameObject` + TextMeshPro + Destroy(1.6s) world popup, i.e. a SECOND
// floating-text system living beside DamageNumberSpawner's pooled one.
// OWNER RULING (WO-953, 2026-08-10, verbatim): "we can use the same item that
// spawns the damage points." ONE pool, one owner — so this class is now a thin
// static forwarder into DamageNumberSpawner.SpawnResourceGain (pooled, billboard,
// rise/fade, burst-MERGED per resource so a dump/tick burst can never spam).
//
// The public Spawn(worldPos, message, tint) signature is UNCHANGED so the
// existing income callers (MineNode.SpawnGainPopup, HarvestSite's tick,
// ResourceCollector.Collect) keep compiling verbatim — the visual language for
// Economy income stays unified, it just stopped owning its own stack.
// =============================================================================
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Static facade for floating "+N &lt;resource&gt;" income text. Forwards into the
    /// pooled <see cref="DamageNumberSpawner"/> (WO-953: one floating-text pool, one
    /// owner). Keep calling this from income paths — never spawn world text directly.
    /// </summary>
    public static class ResourceGainPopup
    {
        /// <summary>Vertical lift the old stack applied internally — preserved so every
        /// caller's popup keeps its felt on-screen height.</summary>
        private const float LiftY = 1.2f;

        /// <summary>
        /// Spawn a floating resource-gain popup at <paramref name="worldPos"/>.
        /// A "+N Name" message routes through the pooled, per-resource-merged gain
        /// path; any other message shows as a plain pooled label (never dropped).
        /// </summary>
        /// <param name="worldPos">Base world position (usually above pet or structure).</param>
        /// <param name="message">"+5 Wood" or similar.</param>
        /// <param name="tint">Resource-themed color (redundant channel — the words carry the meaning).</param>
        public static void Spawn(Vector3 worldPos, string message, Color tint)
        {
            if (string.IsNullOrEmpty(message)) return;

            FlowTrace.Once("Feedback", "gain-facade",
                "ResourceGainPopup now forwards into the DamageNumberSpawner pool (WO-953 one-pool ruling; the separate TMP stack is retired).");

            if (TryParseGain(message, out int amount, out string label))
            {
                DamageNumberSpawner.SpawnResourceGain(amount, label, worldPos + Vector3.up * LiftY, tint);
                return;
            }

            // Not a "+N Name" gain (e.g. a status phrase) — still pooled, never a
            // bespoke GameObject, and never silently dropped.
            FlowTrace.Throttle("Feedback", "gain-freeform", 5f,
                $"ResourceGainPopup message '{message}' is not '+N Name' shaped -- shown as a plain pooled label (no merge).");
            DamageNumberSpawner.SpawnLabel(message, worldPos + Vector3.up * LiftY, tint, scale: 1.05f);
        }

        /// <summary>
        /// Parses a "+N Name" income message into its amount + resource label
        /// (e.g. "+5 Wood" -&gt; 5, "Wood"; "+12 Aether Crystals" -&gt; 12, "Aether Crystals").
        /// False for anything else (no '+', no positive integer, or no label) —
        /// public + pure so the headless oracle can pin it.
        /// </summary>
        public static bool TryParseGain(string message, out int amount, out string resourceLabel)
        {
            amount = 0;
            resourceLabel = null;
            if (string.IsNullOrEmpty(message) || message[0] != '+') return false;

            int i = 1;
            long value = 0;
            while (i < message.Length && char.IsDigit(message[i]))
            {
                value = value * 10 + (message[i] - '0');
                if (value > int.MaxValue) return false;
                i++;
            }
            if (i == 1 || value <= 0) return false;               // no digits / zero gain
            if (i >= message.Length || message[i] != ' ') return false;

            string label = message.Substring(i + 1).Trim();
            if (label.Length == 0) return false;

            amount = (int)value;
            resourceLabel = label;
            return true;
        }
    }
}
