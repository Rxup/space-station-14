using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Flesh;

public sealed partial class FleshPudgeThrowWormActionEvent : WorldTargetActionEvent
{

}

public sealed partial class FleshPudgeAcidSpitActionEvent : WorldTargetActionEvent
{

}

public sealed partial class FleshPudgeAbsorbBloodPoolActionEvent : InstantActionEvent
{

}

public sealed partial class FleshWormJumpActionEvent : WorldTargetActionEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistDevourDoAfterEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistInfectionDoAfterEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistInsulatedImmunityMutationEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistPressureImmunityMutationEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistFlashImmunityMutationEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistColdTempImmunityMutationEvent : SimpleDoAfterEvent
{

}

public sealed partial class FleshCultistAcidSpitActionEvent : WorldTargetActionEvent
{

}

public sealed partial class FleshCultistShopActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistBladeActionEvent : InstantActionEvent
{

}


public sealed partial class FleshCultistClawActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistFistActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistSpikeHandGunActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistArmorActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistSpiderLegsActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistBreakCuffsActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistAdrenalinActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistCreateFleshHeartActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistThrowWormActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistAbsorbBloodPoolActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistDevourActionEvent : EntityTargetActionEvent
{

}

public sealed partial class FleshCultistInfectionActionEvent : EntityTargetActionEvent
{

}



