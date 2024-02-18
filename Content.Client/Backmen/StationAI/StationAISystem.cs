using System.Linq;
using Content.Client.Backmen.CartridgeLoader.Cartridges;
using Content.Client.Backmen.StationAI.UI;
using Content.Client.Storage.Components;
using Content.Client.UserInterface.Fragments;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.UI;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);
        SubscribeLocalEvent<AIEyeComponent, AfterAutoHandleStateEvent>(OnCamUpdate);
    }

    private void OnCamUpdate(Entity<AIEyeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var userInterface) ||
            !userInterface.OpenInterfaces.TryGetValue(AICameraListUiKey.Key, out var ui1) ||
            ui1 is not AICameraListBoundUserInterface ui)
        {
            return;
        }
        ui.Update();
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        var core = ent;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            if (eye.AiCore == null)
            {
                args.Cancel();
                return;
            }

            core = eye.AiCore.Value;
        }

        if (!core.Owner.Valid)
        {
            args.Cancel();
            return;
        }

        if (args.Target != null && Transform(core).GridUid != Transform(args.Target.Value).GridUid)
        {
            args.Cancel();
            return;
        }


        if (HasComp<ItemComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (HasComp<EntityStorageComponent>(args.Target))
        {
            args.Cancel();
            return;
        }
    }
}
