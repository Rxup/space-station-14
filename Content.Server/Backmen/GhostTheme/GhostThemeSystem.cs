using Content.Server.Corvax.Sponsors;
using Content.Shared.Backmen.GhostTheme;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Backmen.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private SponsorsManager _sponsorsManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        if (!_sponsorsManager.TryGetInfo(args.Player.UserId, out var sponsorInfo) ||
            sponsorInfo.GhostTheme == null ||
            !_prototypeManager.TryIndex<GhostThemePrototype>(sponsorInfo.GhostTheme, out var ghostThemePrototype)
           )
        {
            return;
        }
        foreach (var entry in ghostThemePrototype!.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp, true);
        }

        EnsureComp<GhostThemeComponent>(uid).GhostTheme = sponsorInfo.GhostTheme;

    }
}
