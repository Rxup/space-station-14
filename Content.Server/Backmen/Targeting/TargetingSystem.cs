using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Mobs;

namespace Content.Server.Backmen.Targeting;
public sealed partial class TargetingSystem : SharedTargetingSystem
{
    [Dependency] private WoundSystem _woundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TargetChangeEvent>(OnTargetChange);
        SubscribeLocalEvent<TargetingComponent, MobStateChangedEvent>(OnMobStateChange);
    }

    private void OnTargetChange(TargetChangeEvent message, EntitySessionEventArgs args)
    {
        if (!TryComp<TargetingComponent>(GetEntity(message.Uid), out var target))
            return;

        target.Target = SharedTargetingSystem.NormalizeTarget(message.BodyPart);
        DirtyField(GetEntity(message.Uid), target, nameof(TargetingComponent.Target));
    }

    private void OnMobStateChange(EntityUid uid, TargetingComponent component, MobStateChangedEvent args)
    {
        // Revival is handled by the server, so we're keeping all of this here.
        var changed = false;

        if (args.NewMobState == MobState.Dead)
        {
            foreach (var part in GetValidParts())
            {
                component.BodyStatus[part] = WoundableSeverity.Loss;
                changed = true;
            }
        }
        else if (args is { OldMobState: MobState.Dead, NewMobState: MobState.Alive or MobState.Critical })
        {
            component.BodyStatus = _woundSystem.GetWoundableStatesOnBodyPainFeels(uid);
            changed = true;
        }

        if (!changed)
            return;

        DirtyField(uid, component, nameof(TargetingComponent.BodyStatus));
        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(uid)), uid);
    }
}
