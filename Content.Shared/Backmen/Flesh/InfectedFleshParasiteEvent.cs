using Content.Shared.Actions;

namespace Content.Shared.Backmen.Flesh;

public readonly struct EntityInfectedFleshParasiteEvent
{
    public readonly EntityUid Target;

    public EntityInfectedFleshParasiteEvent(EntityUid target)
    {
        Target = target;
    }
};

public sealed partial class ZombifySelfActionEvent : InstantActionEvent { };
