using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Conditions;

/// <inheritdoc cref="EntityConditionSystem{T, TCon}"/>
public sealed partial class PressureThresholdEntityConditionSystem : EntityConditionSystem<TransformComponent, PressureThreshold>
{
    [Dependency] private readonly SharedAtmosphereSystem _atmosphere = default!;

    protected override void Condition(Entity<TransformComponent> entity, ref EntityConditionEvent<PressureThreshold> args)
    {
        if (args.Condition.WorksOnLavaland && HasComp<LavalandMapComponent>(entity.Comp.MapUid))
        {
            args.Result = true;
            return;
        }

        var mix = _atmosphere.GetTileMixture(entity.AsNullable());
        var pressure = mix?.Pressure ?? 0f;
        args.Result = pressure >= args.Condition.Min && pressure <= args.Condition.Max;
    }
}

public sealed partial class PressureThreshold : EntityConditionBase<PressureThreshold>
{
    [DataField]
    public bool WorksOnLavaland;

    [DataField]
    public float Min = float.MinValue;

    [DataField]
    public float Max = float.MaxValue;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-pressure-threshold",
            ("min", Min),
            ("max", Max));
    }
}
