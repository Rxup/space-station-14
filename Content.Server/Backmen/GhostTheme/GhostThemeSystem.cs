using System.Linq;
using Content.Corvax.Interfaces.Server;
using Content.Server.Mind;
using Content.Shared.Backmen.GhostTheme;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Players;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Content.Server.Backmen.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly IServerSponsorsManager _sponsorsMgr = default!; // Corvax-Sponsors
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;


    private Dictionary<NetUserId, ProtoId<GhostThemePrototype>> _cachedGhosts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<RequestGhostThemeEvent>(OnPlayerSelectGhost);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _cachedGhosts.Clear();
    }

    private void OnPlayerSelectGhost(RequestGhostThemeEvent msg, EntitySessionEventArgs args)
    {
#if DEBUG
        if (!_sponsorsMgr.TryGetPrototypes(args.SenderSession.UserId, out var items))
        {
            items = new List<string>();
            items.Add("tier1");
            items.Add("tier2");
            items.Add("tier01");
            items.Add("tier02");
            items.Add("tier03");
            items.Add("tier04");
            items.Add("tier05");
        }
#else
        if (!_sponsorsMgr.TryGetPrototypes(args.SenderSession.UserId, out var items))
            return;

        if (!items.Contains(msg.Ghost.Id))
            return;
#endif

        if (!_prototypeManager.TryIndex(msg.Ghost, out _))
            return;

        _cachedGhosts[args.SenderSession.UserId] = msg.Ghost;


        if (args.SenderSession.AttachedEntity != null && args.SenderSession.AttachedEntity.Value.IsValid() && HasComp<GhostComponent>(args.SenderSession.AttachedEntity))
        {
            var comp = EnsureComp<GhostThemeComponent>(args.SenderSession.AttachedEntity.Value);
            comp.GhostTheme = msg.Ghost.Id;
            Dirty(args.SenderSession.AttachedEntity.Value, comp);
        }

        if (_mindSystem.TryGetMind(args.SenderSession, out var mindId, out var mind) && mind.IsVisitingEntity && HasComp<GhostComponent>(mind.VisitingEntity))
        {
            var comp = EnsureComp<GhostThemeComponent>(mind.VisitingEntity.Value);
            comp.GhostTheme = msg.Ghost.Id;
            Dirty(mind.VisitingEntity.Value, comp);
        }

    }


    private void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        if (!(_cachedGhosts.TryGetValue(args.Player.UserId, out var value) && _prototypeManager.TryIndex(value, out var ghostThemePrototype)))
        {
            if (!_sponsorsMgr.TryGetGhostTheme(args.Player.UserId, out var ghostTheme) ||
                !_prototypeManager.TryIndex(ghostTheme, out ghostThemePrototype)
               )
            {
                return;
            }
        }

        foreach (var entry in ghostThemePrototype!.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }

        EnsureComp<GhostThemeComponent>(uid).GhostTheme = ghostThemePrototype.ID;
    }
}
