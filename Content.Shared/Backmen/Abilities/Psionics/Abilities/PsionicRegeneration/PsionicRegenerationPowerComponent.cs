using Robust.Shared.Audio;
using Content.Shared.DoAfter;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PsionicRegenerationPowerComponent : Component
{
    [DataField("doAfter")]
    public DoAfterId? DoAfter;

    [DataField("essence"), ViewVariables(VVAccess.ReadWrite)]
    public float EssenceAmount = 20;

    [DataField("useDelay"), ViewVariables(VVAccess.ReadWrite)]
    public float UseDelay = 8f;

    [DataField("soundUse")]
    public SoundSpecifier SoundUse = new SoundPathSpecifier("/Audio/Nyanotrasen/heartbeat_fast.ogg");

    public EntityUid? PsionicRegenerationPowerAction = null;
}
