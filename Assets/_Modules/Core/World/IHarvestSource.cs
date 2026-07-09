// =============================================================================
// IHarvestSource — registered harvest faucet (CoC collector spine, WO-656).
// Assembly: DeNelle.Core
// =============================================================================

namespace DeNelle.Core.World
{
    /// <summary>
    /// A harvest source that accrues into a pending buffer before collection.
    /// Implemented by hub/outpost <see cref="DeNelle.Village.Buildings.Progression.ResourceCollector"/>.
    /// </summary>
    public interface IHarvestSource
    {
        string SourceId { get; }
        bool IsActive { get; }
        double PendingAmount { get; }
        double Capacity { get; }
    }
}