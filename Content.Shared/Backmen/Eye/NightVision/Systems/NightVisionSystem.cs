using Content.Shared.Backmen.Eye.NightVision.Components;
using Content.Shared.Inventory;
using JetBrains.Annotations;

namespace Content.Shared.Backmen.Eye.NightVision.Systems;

public sealed class NightVisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    [PublicAPI]
    public void UpdateIsNightVision(EntityUid uid, NightVisionComponent? nightvisionable = null)
    {
        if (!Resolve(uid, ref nightvisionable, false))
            return;

        var old = nightvisionable.IsNightVision;


        var ev = new CanVisionAttemptEvent();
        RaiseLocalEvent(uid, ev);
        nightvisionable.IsNightVision = ev.NightVision;

        if (old == nightvisionable.IsNightVision)
            return;

        var changeEv = new NightVisionnessChangedEvent(nightvisionable.IsNightVision);
        RaiseLocalEvent(uid, ref changeEv);
        Dirty(nightvisionable);
    }
}

[ByRefEvent]
public record struct NightVisionnessChangedEvent(bool NightVision);


public sealed class CanVisionAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public bool NightVision => Cancelled;
    public SlotFlags TargetSlots => SlotFlags.EYES | SlotFlags.MASK | SlotFlags.HEAD;
}
