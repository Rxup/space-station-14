using Content.Shared.Atmos.Rotting;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Rotting;

public partial class RottingSystem
{
  public void TransferRotToDetachedBody(EntityUid source, EntityUid bundle)
  {
    var perishable = EnsureComp<PerishableComponent>(bundle);
    perishable.ForceRotProgression = true;
    perishable.RotNextUpdate = _timing.CurTime + perishable.PerishUpdateRate;

    if (TryComp<PerishableComponent>(source, out var sourcePerishable))
    {
      perishable.RotAccumulator = sourcePerishable.RotAccumulator;
      perishable.Stage = sourcePerishable.Stage;
    }

    Dirty(bundle, perishable);

    if (!TryComp<RottingComponent>(source, out var sourceRotting))
      return;

    var rotting = EnsureComp<RottingComponent>(bundle);
    rotting.TotalRotTime = sourceRotting.TotalRotTime;
    rotting.NextRotUpdate = _timing.CurTime + rotting.RotUpdateRate;
    Dirty(bundle, rotting);
  }
}
