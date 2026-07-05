using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Disables organ effects when integrity reaches Destroyed; re-enables on recovery.
/// </summary>
public sealed class OrganSeveritySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganDamageSeverityChanged>(OnSeverityChanged);
    }

    private void OnSeverityChanged(Entity<OrganComponent> ent, ref OrganDamageSeverityChanged args)
    {
        if (args.NewSeverity == OrganSeverity.Destroyed)
        {
            var ev = new OrganEnableChangedEvent(false);
            RaiseLocalEvent(ent, ref ev);
            return;
        }

        if (args.OldSeverity == OrganSeverity.Destroyed)
        {
            var ev = new OrganEnableChangedEvent(true);
            RaiseLocalEvent(ent, ref ev);
        }
    }
}
