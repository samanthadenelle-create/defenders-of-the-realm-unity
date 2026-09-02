// =============================================================================
// GateTraversalInjector — retired compatibility seam.
// Main_Castle_Overworld is one continuous scene. Its four open gates are ordinary
// ground passages, not navigation boundaries. Runtime links and proximity warps
// made those passages sticky and hid geometry defects behind a teleport.
// =============================================================================

using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class GateTraversalInjector
    {
        /// <summary>Compatibility no-op for old probes/editor tooling.</summary>
        public static void BuildGateWarps()
        {
            FlowTrace.Step("GateTraversal",
                "continuous merged-world gates active — zero NavMeshLinks and zero hero warps authored.");
        }
    }
}
