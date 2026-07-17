using Content.Shared.ActionBlocker;
using Content.Shared.Backmen.Weapons.Common;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Backmen.Weapons.Ranged;

public abstract partial class SharedPumpActionSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        // start-backmen: unique action verb
        SubscribeLocalEvent<PumpActionComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        // end-backmen: unique action verb
        SubscribeLocalEvent<PumpActionComponent, ExaminedEvent>(OnExamined, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<PumpActionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<PumpActionComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PumpActionComponent, UniqueActionEvent>(OnUniqueAction);
    }

    // start-backmen: unique action verb
    private void OnGetVerbs(Entity<PumpActionComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_actionBlocker.CanInteract(args.User, args.Target))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => RaiseLocalEvent(target, new UniqueActionEvent(user)),
            Text = Loc.GetString("ui-options-function-cm-unique-action"),
        });
    }
    // end-backmen: unique action verb

    protected virtual void OnExamined(Entity<PumpActionComponent> ent, ref ExaminedEvent args)
    {
        // TODO RMC14 the server has no idea what this keybind is supposed to be for the client
        args.PushMarkup(Loc.GetString("cm-gun-pump-examine"), 1);
    }

    protected virtual void OnAttemptShoot(Entity<PumpActionComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.Pumped)
            args.Cancelled = true;
    }

    private void OnGunShot(Entity<PumpActionComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.Pumped = false;
        DirtyField(ent, ent.Comp, nameof(PumpActionComponent.Pumped));
    }

    private void OnUniqueAction(Entity<PumpActionComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(ent.Owner, ref ammo);

        if (ammo.Count <= 0)
        {
            _popup.PopupClient(Loc.GetString("cm-gun-no-ammo-message"), args.UserUid, args.UserUid);
            args.Handled = true;
            return;
        }

        if (!ent.Comp.Running || ent.Comp.Pumped)
            return;

        ent.Comp.Pumped = true;
        DirtyField(ent, ent.Comp, nameof(PumpActionComponent.Pumped));

        args.Handled = true;

        _audio.PlayPredicted(ent.Comp.Sound, ent, args.UserUid);
    }
}
