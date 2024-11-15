using Content.Shared.Actions;
using Content.Shared.ADT.Mech.Components;
using Content.Shared.Mech;
using Content.Shared.Mech.EntitySystems;

namespace Content.Shared.ADT.Mech.EntitySystems;

public abstract class SharedMechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechOverloadComponent, SetupMechUserEvent>(SetupOverloadUser);
        SubscribeLocalEvent<MechPhazeComponent, SetupMechUserEvent>(SetupPhaseUser);
        SubscribeLocalEvent<MechPhazeComponent, ComponentStartup>(StartupPhaze);
    }

    private void SetupOverloadUser(EntityUid uid, MechOverloadComponent comp, ref SetupMechUserEvent args)
    {
        var pilot = args.Pilot;
        _actions.AddAction(pilot, ref comp.MechOverloadActionEntity, comp.MechOverloadAction, uid);
    }

    private void SetupPhaseUser(EntityUid uid, MechPhazeComponent comp, ref SetupMechUserEvent args)
    {
        var pilot = args.Pilot;
        _actions.AddAction(pilot, ref comp.MechPhazeActionEntity, comp.MechPhazeAction, uid);
    }

    private void StartupPhaze(EntityUid uid, MechPhazeComponent comp, ComponentStartup args)
    {
        _appearance.SetData(uid, MechPhazingVisuals.Phazing, false);
    }
}
