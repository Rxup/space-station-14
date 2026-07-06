using System.Linq;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;

namespace Content.Shared.Backmen.Surgery.Body.Organs;

public sealed partial class OrganEffectSystem
{
    partial void OnOrganComponentsModifySpaceAnimal(
        Entity<OrganComponent> organEnt,
        ref OrganComponentsModifyEvent ev)
    {
        if (!TryComp<SpaceAnimalOrganComponent>(organEnt, out var space))
            return;

        // start-backmen: space-animal-organs
        if (ev.Add && HasComp<HumanoidProfileComponent>(ev.Body))
        {
            var oldCap = organEnt.Comp.IntegrityCap;
            if (oldCap > FixedPoint2.Zero)
            {
                var scale = space.HumanIntegrityCap / oldCap;
                foreach (var (severity, threshold) in organEnt.Comp.IntegrityThresholds.ToList())
                    organEnt.Comp.IntegrityThresholds[severity] = threshold * scale;
            }

            organEnt.Comp.IntegrityCap = space.HumanIntegrityCap;
            if (organEnt.Comp.OrganIntegrity > space.HumanIntegrityCap)
                organEnt.Comp.OrganIntegrity = space.HumanIntegrityCap;
            Dirty(organEnt, organEnt.Comp);
        }
        // end-backmen: space-animal-organs
    }
}
