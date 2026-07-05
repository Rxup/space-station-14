using System.Linq;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Surgery.Body.Organs;

public sealed partial class OrganEffectSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

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

        var effectProto = GetSpaceOrganStatusEffect(organEnt.Comp, space);
        if (effectProto == null)
            return;

        if (ev.Add
            && organEnt.Comp.Enabled
            && organEnt.Comp.OrganSeverity != OrganSeverity.Destroyed)
        {
            _statusEffects.TrySetStatusEffectDuration(ev.Body, effectProto.Value);
        }
        else
        {
            _statusEffects.TryRemoveStatusEffect(ev.Body, effectProto.Value);
        }
        // end-backmen: space-animal-organs
    }

    private static EntProtoId? GetSpaceOrganStatusEffect(OrganComponent organ, SpaceAnimalOrganComponent space)
    {
        if (organ.Category == "Heart" && space.HeartStatusEffect is { } heart)
            return heart;

        if (organ.Category == "Lungs" && space.LungsStatusEffect is { } lungs)
            return lungs;

        return null;
    }
}
