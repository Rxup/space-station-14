using Content.Server.Atmos.Rotting;
using Content.Shared.Atmos.Rotting;

namespace Content.Server.Atmos.Rotting;

public partial class RottingSystem
{
    /// <summary>
    /// Copies perish/rot progression from a corpse onto an extracted organ.
    /// </summary>
    public void TransferRotToOrgan(EntityUid source, EntityUid organ, TimeSpan? rotAfterOverride = null)
    {
        if (TerminatingOrDeleted(organ) || TerminatingOrDeleted(source))
            return;

        var perishable = EnsureComp<PerishableComponent>(organ);
        perishable.ForceRotProgression = true;
        perishable.RotNextUpdate = _timing.CurTime + perishable.PerishUpdateRate;

        if (rotAfterOverride is { } rotAfter)
            perishable.RotAfter = rotAfter;

        if (TryComp<PerishableComponent>(source, out var sourcePerishable))
        {
            perishable.RotAccumulator = sourcePerishable.RotAccumulator;
            perishable.Stage = sourcePerishable.Stage;
        }

        Dirty(organ, perishable);

        if (!TryComp<RottingComponent>(source, out var sourceRotting))
            return;

        var rotting = EnsureComp<RottingComponent>(organ);
        rotting.TotalRotTime = sourceRotting.TotalRotTime;
        rotting.NextRotUpdate = _timing.CurTime + rotting.RotUpdateRate;
        Dirty(organ, rotting);
    }

    public PerishableComponent? StartOrganHarvestPerish(EntityUid organ, TimeSpan rotAfter)
    {
        if (TerminatingOrDeleted(organ))
            return null;

        var perishable = EnsureComp<PerishableComponent>(organ);
        perishable.ForceRotProgression = true;
        perishable.RotAfter = rotAfter;
        perishable.RotNextUpdate = _timing.CurTime + perishable.PerishUpdateRate;
        Dirty(organ, perishable);
        return perishable;
    }
}
