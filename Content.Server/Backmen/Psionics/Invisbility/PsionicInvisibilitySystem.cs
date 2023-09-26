using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.Vehicle.Components;
using Content.Server.NPC.Systems;
using Content.Server.Psionics;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Eye;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Psionics;

public sealed class PsionicInvisibilitySystem : EntitySystem
{
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly PsionicInvisibilityPowerSystem _invisSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactonSystem = default!;
    [Dependency] private readonly SharedEyeSystem _sharedEyeSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        /// Masking
        SubscribeLocalEvent<PotentialPsionicComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentInit>(OnInsulInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentShutdown>(OnInsulShutdown);
        SubscribeLocalEvent<EyeComponent, MapInitEvent>(OnEyeInit);

        /// Layer
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentInit>(OnInvisInit);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentShutdown>(OnInvisShutdown);

        // PVS Stuff
        SubscribeLocalEvent<PsionicallyInvisibleComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnInit(EntityUid uid, PotentialPsionicComponent component, ComponentInit args)
    {
        SetCanSeePsionicInvisiblity(uid, false);
    }

    private void OnInsulInit(EntityUid uid, PsionicInsulationComponent component, ComponentInit args)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        if (HasComp<PsionicInvisibilityUsedComponent>(uid))
            _invisSystem.ToggleInvisibility(uid);

        if (_npcFactonSystem.ContainsFaction(uid, "PsionicInterloper"))
        {
            component.SuppressedFactions.Add("PsionicInterloper");
            _npcFactonSystem.RemoveFaction(uid, "PsionicInterloper");
        }

        if (_npcFactonSystem.ContainsFaction(uid, "GlimmerMonster"))
        {
            component.SuppressedFactions.Add("GlimmerMonster");
            _npcFactonSystem.RemoveFaction(uid, "GlimmerMonster");
        }

        SetCanSeePsionicInvisiblity(uid, true);
    }

    private void OnInsulShutdown(EntityUid uid, PsionicInsulationComponent component, ComponentShutdown args)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        SetCanSeePsionicInvisiblity(uid, false);

        if (!HasComp<PsionicComponent>(uid))
        {
            component.SuppressedFactions.Clear();
            return;
        }

        foreach (var faction in component.SuppressedFactions)
        {
            _npcFactonSystem.AddFaction(uid, faction);
        }
        component.SuppressedFactions.Clear();
    }

    private void OnInvisInit(EntityUid uid, PsionicallyInvisibleComponent component, ComponentInit args)
    {
        var visibility = EntityManager.EnsureComponent<VisibilityComponent>(uid);

        _visibilitySystem.AddLayer(uid, visibility, (int) VisibilityFlags.PsionicInvisibility, false);
        _visibilitySystem.RemoveLayer(uid, visibility, (int) VisibilityFlags.Normal, false);
        _visibilitySystem.RefreshVisibility(uid, visibilityComponent: visibility);

        SetCanSeePsionicInvisiblity(uid, true);
    }


    private void OnInvisShutdown(EntityUid uid, PsionicallyInvisibleComponent component, ComponentShutdown args)
    {
        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibilitySystem.RemoveLayer(uid, visibility, (int) VisibilityFlags.PsionicInvisibility, false);
            _visibilitySystem.AddLayer(uid, visibility, (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibilityComponent: visibility);
        }
        if (HasComp<PotentialPsionicComponent>(uid) && !HasComp<PsionicInsulationComponent>(uid))
            SetCanSeePsionicInvisiblity(uid, false);
    }

    private void OnEyeInit(EntityUid uid, EyeComponent component, MapInitEvent args)
    {
        if (HasComp<PotentialPsionicComponent>(uid) || HasComp<VehicleComponent>(uid))
            return;

        SetCanSeePsionicInvisiblity(uid, true);
    }
    private void OnEntInserted(EntityUid uid, PsionicallyInvisibleComponent component, EntInsertedIntoContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }

    private void OnEntRemoved(EntityUid uid, PsionicallyInvisibleComponent component, EntRemovedFromContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }

    public void SetCanSeePsionicInvisiblity(EntityUid uid, bool set)
    {
        if (!TryComp<EyeComponent>(uid, out var eye))
            return;

        if (set)
        {
            _sharedEyeSystem.SetVisibilityMask(uid,  eye.VisibilityMask | (int) VisibilityFlags.PsionicInvisibility, eye);
        }
        else
        {
            _sharedEyeSystem.SetVisibilityMask(uid,  eye.VisibilityMask &~ (int) VisibilityFlags.PsionicInvisibility, eye);
        }
    }
}
