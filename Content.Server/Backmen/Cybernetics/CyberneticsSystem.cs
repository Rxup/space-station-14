using Content.Shared.Backmen.Surgery.Body;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Body;
using Content.Shared.Emp;

namespace Content.Server.Backmen.Cybernetics;

internal sealed class CyberneticsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemovedEvent>(OnEmpDisabledRemoved);
    }

    private void OnEmpPulse(Entity<CyberneticsComponent> cyberEnt, ref EmpPulseEvent ev)
    {
        if (cyberEnt.Comp.Disabled)
            return;

        ev.Affected = true;
        ev.Disabled = true;
        cyberEnt.Comp.Disabled = true;

        if (!HasComp<OrganComponent>(cyberEnt))
            return;

        var disableEvent = new OrganEnableChangedEvent(false);
        RaiseLocalEvent(cyberEnt, ref disableEvent);
    }

    private void OnEmpDisabledRemoved(Entity<CyberneticsComponent> cyberEnt, ref EmpDisabledRemovedEvent ev)
    {
        if (!cyberEnt.Comp.Disabled)
            return;

        cyberEnt.Comp.Disabled = false;

        if (!HasComp<OrganComponent>(cyberEnt))
            return;

        var enableEvent = new OrganEnableChangedEvent(true);
        RaiseLocalEvent(cyberEnt, ref enableEvent);
    }
}
