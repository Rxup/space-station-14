using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    private bool CanBuild(EntityUid user, ConstructionPrototype? target, EntityCoordinates? location = null)
    {
        var targetEv = new BuildAttemptEvent(user, target, location);
        RaiseLocalEvent(targetEv);

        return !targetEv.Cancelled;
    }
}

public sealed class BuildAttemptEvent : CancellableEntityEventArgs
{
    public BuildAttemptEvent(EntityUid uid, ConstructionPrototype? target, EntityCoordinates? location = null)
    {
        Uid = uid;
        Target = target;
        Location = location;
    }

    public EntityUid Uid { get; }
    public ConstructionPrototype? Target { get; }
    public EntityCoordinates? Location { get; }
}
