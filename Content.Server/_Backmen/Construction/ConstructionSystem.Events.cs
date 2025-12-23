using Content.Shared.Construction.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    private bool CanBuild(EntityUid user, ConstructionPrototype? target)
    {
        var targetEv = new BuildAttemptEvent(user, target);
        RaiseLocalEvent(targetEv);

        return !targetEv.Cancelled;
    }
}

public sealed class BuildAttemptEvent : CancellableEntityEventArgs
{
    public BuildAttemptEvent(EntityUid uid, ConstructionPrototype? target)
    {
        Uid = uid;
        Target = target;
    }

    public EntityUid Uid { get; }
    public ConstructionPrototype? Target { get; }
}
