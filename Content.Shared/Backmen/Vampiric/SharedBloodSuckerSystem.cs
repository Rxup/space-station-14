using Content.Shared.Backmen.Vampiric.Components;
using Content.Shared.HealthExaminable;
using JetBrains.Annotations;

namespace Content.Shared.Backmen.Vampiric;

[UsedImplicitly]
public abstract class SharedBloodSuckerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodSuckedComponent, HealthBeingExaminedEvent>(OnHealthExamined);
    }

    private void OnHealthExamined(EntityUid uid, BloodSuckedComponent component, HealthBeingExaminedEvent args)
    {
        args.Message.PushNewline();
        args.Message.TryAddMarkup(Loc.GetString("bloodsucked-health-examine", ("target", uid)), out _);
    }
}
