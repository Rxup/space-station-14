using Content.Shared.Backmen.Input;
using Content.Shared.Backmen.Weapons.Ranged;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Input;

namespace Content.Client.Backmen.Weapons.Ranged;

public sealed partial class PumpActionSystem : SharedPumpActionSystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void OnExamined(Entity<PumpActionComponent> ent, ref ExaminedEvent args)
    {
        if (!_input.TryGetKeyBinding(CMKeyFunctions.CMUniqueAction, out var bind))
        {
            args.PushMarkup(Loc.GetString("cm-gun-pump-examine"), 1);
            return;
        }

        args.PushMarkup(Loc.GetString("cm-gun-pump-examine-key", ("key", bind.GetKeyString())), 1);
    }

    protected override void OnAttemptShoot(Entity<PumpActionComponent> ent, ref AttemptShootEvent args)
    {
        base.OnAttemptShoot(ent, ref args);

        if (!ent.Comp.Pumped)
        {
            var message = _input.TryGetKeyBinding(CMKeyFunctions.CMUniqueAction, out var bind)
                ? Loc.GetString("cm-gun-pump-fail-key", ("key", bind.GetKeyString()))
                : Loc.GetString("cm-gun-pump-fail");
            _popup.PopupClient(message, args.User, args.User);
        }
    }
}
