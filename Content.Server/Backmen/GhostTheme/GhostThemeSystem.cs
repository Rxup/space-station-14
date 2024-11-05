using Content.Corvax.Interfaces.Shared;
using Content.Shared.Backmen.GhostTheme;
using Content.Shared.Ghost;
using Robust.Server.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Content.Server.Backmen.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly ISharedSponsorsManager _sponsorsMgr = default!; // Corvax-Sponsors
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IServerNetConfigurationManager _netConfigManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        var prefGhost = _netConfigManager.GetClientCVar(args.Player.Channel, Shared.Backmen.CCVar.CCVars.SponsorsSelectedGhost);
        {
#if DEBUG
            if (!_sponsorsMgr.TryGetServerPrototypes(args.Player.UserId, out var items))
            {
                items = new List<string>();
                foreach (var ghostProto in IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<GhostThemePrototype>())
                {
                    items.Add(ghostProto.ID);
                }
            }
            if (!items.Contains(prefGhost))
            {
                prefGhost = "";
            }
#else
            if (!_sponsorsMgr.TryGetServerPrototypes(args.Player.UserId, out var items) || !items.Contains(prefGhost))
            {
                prefGhost = "";
            }
#endif
        }

        GhostThemePrototype? ghostThemePrototype = null;
        if (string.IsNullOrEmpty(prefGhost) || !_prototypeManager.TryIndex<GhostThemePrototype>(prefGhost, out ghostThemePrototype))
        {
            if (!_sponsorsMgr.TryGetGhostTheme(args.Player.UserId, out var ghostTheme) ||
                !_prototypeManager.TryIndex(ghostTheme, out ghostThemePrototype)
               )
            {
                return;
            }
        }

        foreach (var entry in ghostThemePrototype.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }

        EnsureComp<GhostThemeComponent>(uid).GhostTheme = ghostThemePrototype.ID;
    }
}
