using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Conditions;

public sealed partial class PressureThreshold : EntityEffectCondition
{
    [DataField]
    public bool WorksOnLavaland;

    [DataField]
    public float Min = float.MinValue;

    [DataField]
    public float Max = float.MaxValue;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<TransformComponent>(args.TargetEntity, out var transform))
            return false;

        if (WorksOnLavaland && args.EntityManager.HasComponent<LavalandMapComponent>(transform.MapUid))
            return true;

        var mix = args.EntityManager.System<SharedAtmosphereSystem>().GetTileMixture((args.TargetEntity, transform));
        var pressure = mix?.Pressure ?? 0f;
        return pressure >= Min && pressure <= Max;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-pressure-threshold",
            ("min", Min),
            ("max", Max));
    }
}
