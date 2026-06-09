using Content.Shared.Backmen.Psionics;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Psionics.Invisbility;

/// <summary>
/// Allows an entity to become psionically invisible when touching certain entities.
/// </summary>
public sealed partial class PsionicInvisibleContactsSystem : EntitySystem
{
    private const float WebStealthVisibility = 0.33f;
    private static readonly TimeSpan TileCheckInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<PsionicallyInvisibleComponent> _psiInvisible;
    private TimeSpan _nextTileCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicInvisibleContactsComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<PsionicInvisibleContactsComponent, EndCollideEvent>(OnEntityExit);

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));

        _psiInvisible = GetEntityQuery<PsionicallyInvisibleComponent>();
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
            if (!comp.TileBased)
                continue;

            var onWeb = IsIntersectingWhitelisted(uid, comp, xform.Coordinates);

            if (onWeb)
            {
                ApplyCamouflage(uid);
                continue;
            }

            if (comp.Stages == 0)
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

        if (!_psiInvisible.HasComp(ourEntity))
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

    private void ApplyCamouflage(EntityUid uid)
    {
        if (_psiInvisible.HasComp(uid))
            return;

        EnsureComp<PsionicallyInvisibleComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, WebStealthVisibility, stealth);
    }

    private void RemoveCamouflage(EntityUid uid)
    {
        if (!_psiInvisible.HasComp(uid))
            return;

        RemComp<PsionicallyInvisibleComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 1f, stealth);
        RemComp<StealthComponent>(uid);
    }
}
