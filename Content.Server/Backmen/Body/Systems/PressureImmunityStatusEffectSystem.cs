using Content.Server.Atmos.Components;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Server.Backmen.Body.Systems;

public sealed class PressureImmunityStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<PressureImmunityComponent>(args.Target);
    }

    private void OnRemoved(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<StatusEffectContainerComponent>(args.Target, out var container))
        {
            RemComp<PressureImmunityComponent>(args.Target);
            return;
        }

        foreach (var effect in container.ActiveStatusEffects?.ContainedEntities ?? [])
        {
            if (effect == ent.Owner)
                continue;

            if (HasComp<PressureImmunityStatusEffectComponent>(effect))
                return;
        }

        RemComp<PressureImmunityComponent>(args.Target);
    }
}
