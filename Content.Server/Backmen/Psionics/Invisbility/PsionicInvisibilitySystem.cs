using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Backmen.Eye;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Eye;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
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
        // Masking
        SubscribeLocalEvent<PotentialPsionicComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentInit>(OnInsulInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentShutdown>(OnInsulShutdown);
        SubscribeLocalEvent<EyeMapInit>(OnEyeInit);

        // Layer
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

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string PsionicInterloper = "PsionicInterloper";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string GlimmerMonster = "GlimmerMonster";

    private void OnInsulInit(EntityUid uid, PsionicInsulationComponent component, ComponentInit args)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        if (HasComp<PsionicInvisibilityUsedComponent>(uid))
            _invisSystem.ToggleInvisibility(uid);

        if (TryComp<NpcFactionMemberComponent>(uid, out var npcFactionMemberComponent))
        {
            Entity<NpcFactionMemberComponent?> ent = (uid, npcFactionMemberComponent);
            if (_npcFactonSystem.IsMember(ent, PsionicInterloper))
            {
                component.SuppressedFactions.Add(PsionicInterloper);
                _npcFactonSystem.RemoveFaction(ent, PsionicInterloper);
            }

            if (_npcFactonSystem.IsMember(ent, GlimmerMonster))
            {
                component.SuppressedFactions.Add(GlimmerMonster);
                _npcFactonSystem.RemoveFaction(ent, GlimmerMonster);
            }
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
        Entity<VisibilityComponent?> vis = (uid, EnsureComp<VisibilityComponent>(uid));
        _visibilitySystem.AddLayer(vis, (int) VisibilityFlags.PsionicInvisibility, false);
        _visibilitySystem.RemoveLayer(vis, (int) VisibilityFlags.Normal, false);
        _visibilitySystem.RefreshVisibility(uid, visibilityComponent: vis);

        SetCanSeePsionicInvisiblity(uid, true);
    }


    private void OnInvisShutdown(EntityUid uid, PsionicallyInvisibleComponent component, ComponentShutdown args)
    {
        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            Entity<VisibilityComponent?> vis = (uid, visibility);
            _visibilitySystem.RemoveLayer(vis, (int) VisibilityFlags.PsionicInvisibility, false);
            _visibilitySystem.AddLayer(vis, (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibilityComponent: visibility);
        }
        if (HasComp<PotentialPsionicComponent>(uid) && !HasComp<PsionicInsulationComponent>(uid))
            SetCanSeePsionicInvisiblity(uid, false);
    }

    private void OnEyeInit(EyeMapInit args)
    {
        if (HasComp<PotentialPsionicComponent>(args.Target)) //|| HasComp<VehicleComponent>(args.Target)
            return;

        SetCanSeePsionicInvisiblity(args.Target, true, args.Target.Comp);
    }
    private void OnEntInserted(EntityUid uid, PsionicallyInvisibleComponent component, EntInsertedIntoContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }

    private void OnEntRemoved(EntityUid uid, PsionicallyInvisibleComponent component, EntRemovedFromContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }

    public void SetCanSeePsionicInvisiblity(EntityUid uid, bool set, EyeComponent? eye = null)
    {
        if (!Resolve(uid, ref eye, false))
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
