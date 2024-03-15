using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.Strip.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Physics.Pull;
using Robust.Shared.Console;

namespace Content.Shared.Backmen.StationAI;

public abstract class SharedStationAISystem : EntitySystem
{
    public const float CameraEyeRange = 10f;

    public override void Initialize()
    {
        base.Initialize();


        SubscribeLocalEvent<StationAIComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, IsEquippingAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, IsUnequippingAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<StationAIComponent, ChangeDirectionAttemptEvent>(OnUpdateCanMove);

        SubscribeLocalEvent<StationAIComponent, StrippingSlotButtonPressed>(OnStripEvent);
    }

    public static IEnumerable<CompletionOption> StationAiComponents(string text, IEntityManager? entManager = null)
    {
        IoCManager.Resolve(ref entManager);

        var query = entManager.AllEntityQueryEnumerator<StationAIComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out _, out var metadata))
        {
            if (!entManager.TryGetNetEntity(uid, out var netEntity, metadata: metadata))
                continue;

            if(entManager.HasComponent<AIEyeComponent>(uid))
                continue;

            var netString = netEntity.Value.ToString();

            if (!netString.StartsWith(text))
                continue;

            yield return new CompletionOption(netString, metadata.EntityName);
        }
    }


    private void OnAttempt(EntityUid uid, StationAIComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnUpdateCanMove(EntityUid uid, StationAIComponent component, CancellableEntityEventArgs args)
    {
        if(!HasComp<AIEyeComponent>(uid))
            args.Cancel();
    }

    private void OnStripEvent(EntityUid uid, Component component, StrippingSlotButtonPressed args)
    {
        return;
    }
}
