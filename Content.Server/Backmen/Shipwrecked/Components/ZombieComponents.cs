namespace Content.Server.Backmen.Shipwrecked.Components;

[RegisterComponent]
[Access(typeof(NPCZombieSystem))]
public sealed partial class ZombieWakeupOnTriggerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public EntityUid? ToZombify;
}

[RegisterComponent]
[Access(typeof(NPCZombieSystem))]
public sealed partial class ZombieSurpriseComponent : Component
{

}

[RegisterComponent]
[Access(typeof(NPCZombieSystem))]
public sealed partial class ZombifiedOnSpawnComponent : Component
{
    [DataField("isBoss")]
    public bool IsBoss = false;
}
