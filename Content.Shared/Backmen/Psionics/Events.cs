using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Psionics.Events;

[Serializable, NetSerializable]
public sealed partial class PsionicRegenerationDoAfterEvent : DoAfterEvent
{
    [DataField("startedAt", required: true)]
    public TimeSpan StartedAt;

    private PsionicRegenerationDoAfterEvent()
    {
    }

    public PsionicRegenerationDoAfterEvent(TimeSpan startedAt)
    {
        StartedAt = startedAt;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class GlimmerWispDrainDoAfterEvent : SimpleDoAfterEvent
{
}

public sealed partial class PsionicInvisibilityPowerActionEvent : InstantActionEvent {}
public sealed partial class PsionicInvisibilityPowerOffActionEvent : InstantActionEvent {}
public sealed partial class HairballActionEvent : InstantActionEvent {}
public sealed partial class EatMouseActionEvent : InstantActionEvent {}
public sealed partial class MetapsionicPowerActionEvent : InstantActionEvent {}
public sealed partial class TelegnosisPowerReturnActionEvent : InstantActionEvent {}
public sealed partial class TelegnosisPowerActionEvent : InstantActionEvent {}
public sealed partial class PsionicRegenerationPowerActionEvent : InstantActionEvent {}
public sealed partial class NoosphericZapPowerActionEvent : EntityTargetActionEvent {}
public sealed partial class DispelPowerActionEvent : EntityTargetActionEvent {}

public sealed partial class DispelledEvent : HandledEntityEventArgs {}
public sealed partial class MindSwapPowerActionEvent : EntityTargetActionEvent
{

}

public sealed partial class MindSwapPowerReturnActionEvent : InstantActionEvent
{

}
public sealed partial class PyrokinesisPowerActionEvent : EntityTargetActionEvent {}
public sealed partial class PsychokinesisPowerActionEvent : WorldTargetActionEvent {}

[RegisterComponent, NetworkedComponent]
public sealed partial class MetapsionicVisibleComponent : Component
{

}
