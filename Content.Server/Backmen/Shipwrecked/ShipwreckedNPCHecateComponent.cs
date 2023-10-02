using Content.Server.Backmen.NPC.Events;
using Content.Server.Backmen.NPC.Prototypes;

namespace Content.Server.Backmen.Shipwrecked;

[RegisterComponent]
public sealed partial class ShipwreckedNPCHecateComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public ShipwreckedRuleComponent? Rule;

    [ViewVariables(VVAccess.ReadWrite)] public HashSet<EntityUid> GunSafe = new ();

    [ViewVariables(VVAccess.ReadWrite)]
    public bool UnlockedSafe;

    [ViewVariables(VVAccess.ReadWrite)] public HashSet<EntityUid> EngineBayDoor = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public bool UnlockedEngineBay;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Launching;
}

[Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedHecateAskGeneratorUnlockEvent : NPCConversationEvent
{
    [DataField("accessGranted", required: true)]
    public NPCResponse AccessGranted { get; private set; } = default!;
}

[Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedHecateAskWeaponsUnlockEvent : NPCConversationEvent
{

}

[Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedHecateAskWeaponsEvent : NPCConversationEvent
{
    [DataField("beforeUnlock", required: true)]
    public NPCResponse BeforeUnlock { get; private set; } = default!;

    [DataField("afterUnlock", required: true)]
    public NPCResponse AfterUnlock { get; private set; } = default!;
}

[Access(typeof(ShipwreckedRuleSystem))]
public abstract partial class ShipwreckedHecateAskStatusOrLaunchEvent : NPCConversationEvent
{
    [DataField("needConsole", required: true)]
    public NPCResponse NeedConsole { get; private set; } = default!;

    [DataField("needGenerator", required: true)]
    public NPCResponse NeedGenerator { get; private set; } = default!;

    [DataField("needThrusters", required: true)]
    public NPCResponse NeedThrusters { get; private set; } = default!;

}

[Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedHecateAskStatusEvent : ShipwreckedHecateAskStatusOrLaunchEvent
{
    [DataField("allGreenFirst", required: true)]
    public NPCResponse AllGreenFirst { get; private set; } = default!;

    [DataField("allGreenAgain", required: true)]
    public NPCResponse AllGreenAgain { get; private set; } = default!;
}

[Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedHecateAskLaunchEvent : ShipwreckedHecateAskStatusOrLaunchEvent
{
    [DataField("launch", required: true)]
    public NPCResponse Launch { get; private set; } = default!;
}
