using Content.Server.NPC.Systems;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Components;

/// <summary>
/// This is used for an NPC that constantly tries to use an action on a given target.
/// </summary>
[RegisterComponent, Access(typeof(NPCUseActionOnTargetSystem))]
public sealed partial class NPCUseActionOnTargetComponent : Component
{
    /// <summary>
    /// HTN blackboard key for the target entity
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// Backmen-EDIT-Start
    /// <summary>
    /// Action that's going to attempt to be used.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId<EntityWorldTargetActionComponent>> ActionId = new();

    [DataField]
    public EntityUid? ActionEnt;

    /// <summary>
    /// Minimum delay between attempts to use ANY action.
    /// </summary>
    [DataField]
    public TimeSpan UseActionDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Time of the last attempt to use ANY action.
    /// </summary>
    [DataField]
    public TimeSpan LastUseAnyActionTime;

    [DataField]
    public List<EntityUid> ActionEntities = new();
    /// Backmen-EDIT-End
}
