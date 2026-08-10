using Content.Shared.Backmen.Psionics;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Psionics.Invisbility;

/// <summary>
/// Allows an entity to become psionically invisible when touching certain entities.
/// </summary>
public sealed partial class PsionicInvisibleContactsSystem : EntitySystem
{
    private static readonly TimeSpan TileCheckInterval = TimeSpan.FromSeconds(0.25);
    private static readonly EntProtoId StatusEffectPsionicWebCamouflage = "StatusEffectPsionicWebCamouflage";
    private static readonly EntProtoId StatusEffectPsionicInvisibility = "StatusEffectPsionicInvisibility";

    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private TimeSpan _nextTileCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicInvisibleContactsComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<PsionicInvisibleContactsComponent, EndCollideEvent>(OnEntityExit);

        SubscribeLocalEvent<PsionicWebCamouflageComponent, StatusEffectAppliedEvent>(OnWebCamouflageApplied);
        SubscribeLocalEvent<PsionicWebCamouflageComponent, StatusEffectRemovedEvent>(OnWebCamouflageRemoved);

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTileCheck)
            return;

        _nextTileCheck = _timing.CurTime + TileCheckInterval;

        var query = EntityQueryEnumerator<PsionicInvisibleContactsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var onContact = comp.Stages > 0;
            if (comp.TileBased)
                onContact |= IsIntersectingWhitelisted(uid, comp, xform.Coordinates);

            if (onContact)
                ApplyCamouflage(uid);
            else
                RemoveCamouflage(uid);
        }
    }

    private void OnEntityEnter(EntityUid uid, PsionicInvisibleContactsComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;
        var ourEntity = args.OurEntity;

        if (!_whitelist.IsValid(component.Whitelist, otherUid))
            return;

        // This will go up twice per web hit, since webs also have a flammable fixture.
        // It goes down twice per web exit, so everything's fine.
        ++component.Stages;

        ApplyCamouflage(ourEntity);
    }

    private void OnEntityExit(EntityUid uid, PsionicInvisibleContactsComponent component, ref EndCollideEvent args)
    {
        var otherUid = args.OtherEntity;
        var ourEntity = args.OurEntity;

        if (!_whitelist.IsValid(component.Whitelist, otherUid))
            return;

        if (--component.Stages > 0)
            return;

        if (component.TileBased && IsIntersectingWhitelisted(ourEntity, component, Transform(ourEntity).Coordinates))
            return;

        RemoveCamouflage(ourEntity);
    }

    private bool IsIntersectingWhitelisted(EntityUid uid, PsionicInvisibleContactsComponent component, EntityCoordinates coords)
    {
        foreach (var ent in _lookup.GetEntitiesIntersecting(coords))
        {
            if (ent == uid)
                continue;

            if (_whitelist.IsValid(component.Whitelist, ent))
                return true;
        }

        return false;
    }

    private bool IsWebCamouflaged(EntityUid uid)
    {
        return _statusEffects.HasStatusEffect(uid, StatusEffectPsionicWebCamouflage);
    }

    private bool AlreadyPsionicallyInvisible(EntityUid uid)
    {
        return HasComp<PsionicallyInvisibleComponent>(uid) ||
               _statusEffects.HasEffectComp<PsionicallyInvisibleComponent>(uid);
    }

    private void ApplyCamouflage(EntityUid uid)
    {
        if (IsWebCamouflaged(uid) || AlreadyPsionicallyInvisible(uid))
            return;

        _statusEffects.TrySetStatusEffectDuration(uid, StatusEffectPsionicWebCamouflage);
    }

    private void RemoveCamouflage(EntityUid uid)
    {
        if (!IsWebCamouflaged(uid))
            return;

        _statusEffects.TryRemoveStatusEffect(uid, StatusEffectPsionicWebCamouflage);
    }

    private void OnWebCamouflageApplied(Entity<PsionicWebCamouflageComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var stealth = EnsureComp<StealthComponent>(args.Target);
        _stealth.SetVisibility(args.Target, ent.Comp.StealthVisibility, stealth);
    }

    private void OnWebCamouflageRemoved(Entity<PsionicWebCamouflageComponent> ent, ref StatusEffectRemovedEvent args)
    {
        // Don't strip stealth from active power invisibility.
        if (_statusEffects.HasStatusEffect(args.Target, StatusEffectPsionicInvisibility))
            return;

        if (!TryComp<StealthComponent>(args.Target, out var stealth))
            return;

        _stealth.SetVisibility(args.Target, 1f, stealth);
        RemComp<StealthComponent>(args.Target);
    }
}
