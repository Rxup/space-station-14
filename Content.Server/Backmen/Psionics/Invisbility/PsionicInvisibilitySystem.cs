using System.Linq;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Psionics;

public sealed partial class PsionicInvisibilitySystem : EntitySystem
{
    [Dependency] private VisibilitySystem _visibilitySystem = default!;
    [Dependency] private PsionicInvisibilityPowerSystem _invisSystem = default!;
    [Dependency] private NpcFactionSystem _npcFactonSystem = default!;
    [Dependency] private SharedEyeSystem _sharedEyeSystem = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Masking
        SubscribeLocalEvent<PotentialPsionicComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentInit>(OnInsulInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentShutdown>(OnInsulShutdown);

        // Visibility mask event
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, GetVisMaskEvent>(OnGetVisMask2);

        // Layer
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentInit>(OnInvisInit);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentShutdown>(OnInvisShutdown);

        // PVS Stuff
        SubscribeLocalEvent<PsionicallyInvisibleComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnGetVisMask2(Entity<PsionicallyInvisibleComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
    }

    private void OnGetVisMask(ref GetVisMaskEvent args)
    {
        if (HasComp<GhostComponent>(args.Entity))
        {
            args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
            return;
        }

        // Entities without PotentialPsionicComponent can see psionic invisibility
        if (!HasComp<PotentialPsionicComponent>(args.Entity))
        {
            args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
            return;
        }

        // Entities with PsionicInsulationComponent can see psionic invisibility
        if (_statusEffects.TryEffectsWithComp<PsionicInsulationComponent>(args.Entity, out var insul))
        {
            if (insul.Any(effect => effect.Comp1.LifeStage >= ComponentLifeStage.Stopping))
            {
                return;
            }

            args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
        }
    }

    private void OnInit(EntityUid uid, PotentialPsionicComponent component, ComponentInit args)
    {
        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }

    private static readonly ProtoId<NpcFactionPrototype> PsionicInterloper = "PsionicInterloper";
    private static readonly ProtoId<NpcFactionPrototype> GlimmerMonster = "GlimmerMonster";

    private void OnInsulInit(EntityUid uid, PsionicInsulationComponent component, ComponentInit args)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        if (HasComp<PsionicInvisibilityUsedComponent>(uid))
            RemCompDeferred<PsionicInvisibilityUsedComponent>(uid);

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

        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }

    private void OnInsulShutdown(EntityUid uid, PsionicInsulationComponent component, ComponentShutdown args)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        _sharedEyeSystem.RefreshVisibilityMask(uid);

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

        _sharedEyeSystem.RefreshVisibilityMask(uid);
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
        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }
    private void OnEntInserted(EntityUid uid, PsionicallyInvisibleComponent component, EntInsertedIntoContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }

    private void OnEntRemoved(EntityUid uid, PsionicallyInvisibleComponent component, EntRemovedFromContainerMessage args)
    {
        DirtyEntity(args.Entity);
    }
}
