using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Backmen.Chat;
using Content.Shared.Chat;
using Content.Shared.Popups;

namespace Content.Shared.Heretic;

public abstract class SharedHereticAbilitySystem : EntitySystem
{

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;

    protected EntityQuery<HereticComponent> _hereticQuery;
    protected EntityQuery<GhoulComponent> _ghoulQuery;

    public override void Initialize()
    {
        base.Initialize();

        _hereticQuery = GetEntityQuery<HereticComponent>();
        _ghoulQuery = GetEntityQuery<GhoulComponent>();

        SubscribeLocalEvent<HereticActionComponent, ActionAttemptEvent>(OnActionAttempt);
    }

    private void OnActionAttempt(Entity<HereticActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
        {
            return;
        }
        if (!_hereticQuery.TryComp(ent, out var comp))
        {
            args.Cancelled = true;
            return;
        }
        if (!comp.CodexActive)
        {
            args.Cancelled = true;
            return;
        }

        var ev = new CheckMagicItemEvent();
        RaiseLocalEvent(ent, ev);
        if (!ev.Handled)
        {
            args.Cancelled = true;
            _popup.PopupPredicted(Loc.GetString("heretic-ability-fail-magicitem"), ent, ent);
            return;
        }

        // shout the spell out
        if (!string.IsNullOrWhiteSpace(ent.Comp.MessageLoc))
            TrySendInGameMessage(ent.Comp, ent);
    }

    protected virtual void TrySendInGameMessage(HereticActionComponent comp, EntityUid ent)
    {

    }
}
