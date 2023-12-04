using Content.Corvax.Interfaces.Server;
using Content.Shared.Backmen.GhostTheme;
using Content.Shared.Ghost;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Backmen.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    private IServerSponsorsManager? _sponsorsMgr; // Corvax-Sponsors
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsMgr); // Corvax-Sponsors
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        if (_sponsorsMgr == null)
        {
            return;
        }
        if (!_sponsorsMgr.TryGetGhostTheme(args.Player.UserId, out var ghostTheme) ||
            !_prototypeManager.TryIndex<GhostThemePrototype>(ghostTheme, out var ghostThemePrototype)
           )
        {
            return;
        }
        foreach (var entry in ghostThemePrototype!.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }

        EnsureComp<GhostThemeComponent>(uid).GhostTheme = ghostTheme;

    }
}
