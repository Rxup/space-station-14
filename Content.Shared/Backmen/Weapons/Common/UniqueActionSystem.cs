using Content.Shared.Backmen.Input;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Weapons.Common;

public sealed partial class UniqueActionSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private IGameTiming _timing = default!; // backmen: unique action prediction

    public override void Initialize()
    {
        SubscribeLocalEvent<UniqueActionComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        // start-backmen: unique action prediction
        SubscribeAllEvent<RequestUniqueActionEvent>(OnRequestUniqueAction);
        // end-backmen: unique action prediction

        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMUniqueAction,
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is { } userUid)
                            RequestUniqueAction(userUid); // backmen: unique action prediction
                    },
                    handle: false))
            .Register<UniqueActionSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<UniqueActionSystem>();
    }

    private void OnGetVerbs(Entity<UniqueActionComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_actionBlockerSystem.CanInteract(args.User, args.Target))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => TryUniqueAction(user, ent.Owner),
            Text = Loc.GetString("ui-options-function-cm-unique-action"),
        });
    }

    // start-backmen: unique action prediction
    private void RequestUniqueAction(EntityUid userUid)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RaisePredictiveEvent(new RequestUniqueActionEvent());
    }

    private void OnRequestUniqueAction(RequestUniqueActionEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } userUid)
            return;

        TryUniqueAction(userUid);
    }
    // end-backmen: unique action prediction

    private void TryUniqueAction(EntityUid userUid)
    {
        if (!_handsSystem.TryGetActiveItem(userUid, out var item))
            return;

        TryUniqueAction(userUid, item.Value);
    }

    private void TryUniqueAction(EntityUid userUid, EntityUid targetUid)
    {
        if (!_actionBlockerSystem.CanInteract(userUid, targetUid))
            return;

        RaiseLocalEvent(targetUid, new UniqueActionEvent(userUid));
    }
}

// start-backmen: unique action prediction
/// <summary>
/// Client→server predicted request to perform the held item's unique action (e.g. pump/cock a gun).
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestUniqueActionEvent : EntityEventArgs;
// end-backmen: unique action prediction
