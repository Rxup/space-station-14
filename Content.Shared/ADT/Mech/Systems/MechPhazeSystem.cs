using Content.Shared.Actions;
using Content.Shared.Mech.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics.Systems;
using System.Linq;
using Robust.Shared.Physics;
using Content.Shared.ADT.Mech.Components;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// Handles all of the interactions, UI handling, and items shennanigans for <see cref="MechComponent"/>
/// </summary>
public sealed class MechPhazeSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechPhazeComponent, MechPhazeEvent>(OnTogglePhaze);
        SubscribeLocalEvent<MechPhazeComponent, MechEjectPilotEvent>(OnEjectPilotEvent);
    }

    private void OnTogglePhaze(EntityUid uid, MechPhazeComponent comp, MechPhazeEvent args)
    {
        if (args.Handled)
            return;
        TogglePhaze(uid);
    }
    private void OnEjectPilotEvent(EntityUid uid, MechPhazeComponent comp, MechEjectPilotEvent args)
    {
        if (comp.Phazed)
            args.Handled = true;
    }

    private void TogglePhaze(EntityUid uid, MechPhazeComponent? comp = null, MechComponent? mech = null)
    {
        if (!Resolve(uid, ref comp))
            return;
        if (!Resolve(uid, ref mech))
            return;

        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            var fixture = fixtures.Fixtures.First();
            var collisionGroup = comp.Phazed ? CollisionGroup.MidImpassable : CollisionGroup.GhostImpassable;
            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int)collisionGroup, fixtures);
            comp.Phazed = !comp.Phazed;
            if (_netMan.IsServer)
                _appearance.SetData(uid, MechPhazingVisuals.Phazing, comp.Phazed);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_netMan.IsServer)
            return;

        var query = EntityQueryEnumerator<MechPhazeComponent>();
        while (query.MoveNext(out var uid, out var phaze))
        {
            if (!phaze.Phazed)
                continue;

            phaze.Accumulator += frameTime;
            if (phaze.Accumulator < 1f)
                continue;
            phaze.Accumulator = 0f;

            if (TryComp<MechComponent>(uid, out var mech))
            {
                _mech.TryChangeEnergy(uid, phaze.EnergyDelta);
                if (mech.Energy <= 0)
                    TogglePhaze(uid);
            }
        }
    }
}

public sealed partial class MechPhazeEvent : InstantActionEvent
{
}

[NetSerializable, Serializable]
public enum MechPhazingVisuals : byte
{
    Phazing,
}
