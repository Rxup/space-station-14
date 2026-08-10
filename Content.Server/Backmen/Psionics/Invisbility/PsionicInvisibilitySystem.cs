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
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
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
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId StatusEffectPsionicInvisibility = "StatusEffectPsionicInvisibility";

    public override void Initialize()
    {
        base.Initialize();
        // Masking
        SubscribeLocalEvent<PotentialPsionicComponent, ComponentInit>(OnInit);
        // Primary path: insulation is on the status-effect entity, Applied/Removed carry the real mob.
        SubscribeLocalEvent<PsionicInsulationComponent, StatusEffectAppliedEvent>(OnInsulApplied);
        SubscribeLocalEvent<PsionicInsulationComponent, StatusEffectRemovedEvent>(OnInsulRemoved);
        // Fallback: direct component on a mob, or AppliedTo already set at ComponentInit.
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentInit>(OnInsulInit);
        SubscribeLocalEvent<PsionicInsulationComponent, ComponentShutdown>(OnInsulShutdown);

        // Visibility mask event
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisMask);
        // Fallback: component directly on the mob (admin abuse / web contacts)
        SubscribeLocalEvent<PsionicallyInvisibleComponent, GetVisMaskEvent>(OnGetVisMaskDirect);

        // Layer: primary path applies to StatusEffect AppliedTo; fallback keeps ComponentInit/Shutdown.
        SubscribeLocalEvent<PsionicallyInvisibleComponent, StatusEffectAppliedEvent>(OnInvisApplied);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, StatusEffectRemovedEvent>(OnInvisRemoved);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentInit>(OnInvisInit);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, ComponentShutdown>(OnInvisShutdown);

        // PVS: dirty inserted/removed entities when the container owner is psionically invisible
        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    /// <summary>
    /// Fallback when <see cref="PsionicallyInvisibleComponent"/> is on the eye entity itself.
    /// </summary>
    private void OnGetVisMaskDirect(Entity<PsionicallyInvisibleComponent> ent, ref GetVisMaskEvent args)
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

        // Self is invisible via status effect (component lives on the effect entity)
        if (_statusEffects.HasEffectComp<PsionicallyInvisibleComponent>(args.Entity))
        {
            args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
            return;
        }

        // Entities with PsionicInsulationComponent can see psionic invisibility
        if (_statusEffects.TryEffectsWithComp<PsionicInsulationComponent>(args.Entity, out var insul))
        {
            if (insul.All(effect => effect.Comp1.LifeStage >= ComponentLifeStage.Stopping))
                return;

            args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
        }
    }

    private void OnInit(EntityUid uid, PotentialPsionicComponent component, ComponentInit args)
    {
        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }

    private static readonly ProtoId<NpcFactionPrototype> PsionicInterloper = "PsionicInterloper";
    private static readonly ProtoId<NpcFactionPrototype> GlimmerMonster = "GlimmerMonster";

    private void OnInsulApplied(Entity<PsionicInsulationComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ApplyInsulation(args.Target, ent.Comp);
    }

    private void OnInsulRemoved(Entity<PsionicInsulationComponent> ent, ref StatusEffectRemovedEvent args)
    {
        // Clears stale PsionicInvisibility eye bit after QueueDel (tinfoil unequip / cryptobiolin / anti-psi).
        RemoveInsulation(args.Target, ent.Comp);
    }

    private void OnInsulInit(EntityUid uid, PsionicInsulationComponent component, ComponentInit args)
    {
        // Status-effect spawn usually has AppliedTo unset yet — StatusEffectApplied handles that.
        if (!TryResolveInsulationTarget(uid, out var target))
            return;

        ApplyInsulation(target, component);
    }

    private void OnInsulShutdown(EntityUid uid, PsionicInsulationComponent component, ComponentShutdown args)
    {
        if (!TryResolveInsulationTarget(uid, out var target))
            return;

        RemoveInsulation(target, component);
    }

    /// <summary>
    /// Status-effect entity → AppliedTo mob. Direct component on a mob → uid itself.
    /// </summary>
    private bool TryResolveInsulationTarget(EntityUid uid, out EntityUid target)
    {
        if (TryComp<StatusEffectComponent>(uid, out var status))
        {
            if (status.AppliedTo is not { } applied)
            {
                target = default;
                return false;
            }

            target = applied;
            return true;
        }

        target = uid;
        return true;
    }

    private void ApplyInsulation(EntityUid uid, PsionicInsulationComponent component)
    {
        if (!HasComp<PotentialPsionicComponent>(uid))
            return;

        if (HasComp<PsionicInvisibilityUsedComponent>(uid) ||
            _statusEffects.HasStatusEffect(uid, StatusEffectPsionicInvisibility))
            _invisSystem.TryCancelInvisibility(uid);

        if (TryComp<NpcFactionMemberComponent>(uid, out var npcFactionMemberComponent))
        {
            Entity<NpcFactionMemberComponent?> factionEnt = (uid, npcFactionMemberComponent);
            if (_npcFactonSystem.IsMember(factionEnt, PsionicInterloper))
            {
                component.SuppressedFactions.Add(PsionicInterloper);
                _npcFactonSystem.RemoveFaction(factionEnt, PsionicInterloper);
            }

            if (_npcFactonSystem.IsMember(factionEnt, GlimmerMonster))
            {
                component.SuppressedFactions.Add(GlimmerMonster);
                _npcFactonSystem.RemoveFaction(factionEnt, GlimmerMonster);
            }
        }

        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }

    private void RemoveInsulation(EntityUid uid, PsionicInsulationComponent component)
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

    private void OnInvisApplied(Entity<PsionicallyInvisibleComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ApplyPsionicInvisLayer(args.Target);
    }

    private void OnInvisRemoved(Entity<PsionicallyInvisibleComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemovePsionicInvisLayer(args.Target);
    }

    private void OnInvisInit(EntityUid uid, PsionicallyInvisibleComponent component, ComponentInit args)
    {
        // Status-effect spawn: AppliedTo may be unset; StatusEffectApplied handles the real target.
        if (HasComp<StatusEffectComponent>(uid))
            return;

        // Fallback: admin abuse / spider-web contacts / map prototypes put the component on the mob.
        ApplyPsionicInvisLayer(uid);
    }

    private void OnInvisShutdown(EntityUid uid, PsionicallyInvisibleComponent component, ComponentShutdown args)
    {
        if (HasComp<StatusEffectComponent>(uid))
            return;

        RemovePsionicInvisLayer(uid);
    }

    private void ApplyPsionicInvisLayer(EntityUid uid)
    {
        Entity<VisibilityComponent?> vis = (uid, EnsureComp<VisibilityComponent>(uid));
        _visibilitySystem.AddLayer(vis, (int) VisibilityFlags.PsionicInvisibility, false);
        _visibilitySystem.RemoveLayer(vis, (int) VisibilityFlags.Normal, false);
        _visibilitySystem.RefreshVisibility(uid, visibilityComponent: vis);

        _sharedEyeSystem.RefreshVisibilityMask(uid);
    }

    private void RemovePsionicInvisLayer(EntityUid uid)
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

    private bool IsPsionicallyInvisible(EntityUid uid)
    {
        return HasComp<PsionicallyInvisibleComponent>(uid) ||
               _statusEffects.HasEffectComp<PsionicallyInvisibleComponent>(uid);
    }

    private void OnEntInserted(EntInsertedIntoContainerMessage args)
    {
        if (!IsPsionicallyInvisible(args.Container.Owner))
            return;

        DirtyEntity(args.Entity);
    }

    private void OnEntRemoved(EntRemovedFromContainerMessage args)
    {
        if (!IsPsionicallyInvisible(args.Container.Owner))
            return;

        DirtyEntity(args.Entity);
    }
}
