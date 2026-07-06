using Content.Server.Atmos.Rotting;
using Content.Server.Backmen.Surgery.Trauma.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// When any organ is surgically removed from a dead/rotting body, transfer rot and apply integrity damage.
/// </summary>
public sealed partial class OrganRotOnExtractSystem : EntitySystem
{
    public const float DefaultExtractRotDamageFraction = 0.2f;

    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private ServerTraumaSystem _trauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganGotRemoved);
    }

    private void OnOrganGotRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        var body = args.Target;

        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(body))
            return;

        if (ent.Comp.Category is { } category && SurgeryBodyPartMapping.IsExternalCategory(category))
            return;

        if (HasComp<BkmDetachedBrainProtectionComponent>(ent))
            return;

        if (TryComp<MobStateComponent>(body, out var mobState) && mobState.CurrentState != MobState.Dead)
            return;

        var rotFactor = GetRotFactor(body);
        if (rotFactor <= 0)
            return;

        // start-backmen: space-animal-organs
        _rotting.TransferRotToOrgan(body, ent);

        var fraction = TryComp<SpaceAnimalOrganComponent>(ent, out var space)
            ? space.HarvestDamageFraction
            : DefaultExtractRotDamageFraction;

        var damage = ent.Comp.IntegrityCap * fraction * rotFactor;
        if (damage <= FixedPoint2.Zero)
            return;

        if (!_trauma.TrySetOrganDamageModifier(ent, damage, ent, "ExtractRot", ent.Comp))
            _trauma.TryAddOrganDamageModifier(ent, damage, ent, "ExtractRot", ent.Comp);
        // end-backmen: space-animal-organs
    }

    private float GetRotFactor(EntityUid body)
    {
        if (HasComp<RottingComponent>(body))
            return 1f;

        if (!TryComp<PerishableComponent>(body, out var perishable) || perishable.RotAfter <= TimeSpan.Zero)
            return 0f;

        if (perishable.RotAccumulator <= TimeSpan.Zero)
            return 0f;

        return Math.Clamp((float) (perishable.RotAccumulator.TotalSeconds / perishable.RotAfter.TotalSeconds), 0f, 1f);
    }
}
