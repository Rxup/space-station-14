using Content.Shared._Backmen.Input;
using Content.Shared._Backmen.Weapons.Ranged;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Input;

namespace Content.Client._Backmen.Weapons.Ranged;

public sealed class PumpActionSystem : SharedPumpActionSystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void OnExamined(Entity<PumpActionComponent> ent, ref ExaminedEvent args)
    {
        if (!_input.TryGetKeyBinding(CMKeyFunctions.CMUniqueAction, out var bind))
            return;

        args.PushMarkup($"[bold]Нажми на [color=cyan]необычное взаимодействие[/color] (по стандарту \"R\") чтобы зарядить перед выстрелом.[/bold]", 1);
    }

    protected override void OnAttemptShoot(Entity<PumpActionComponent> ent, ref AttemptShootEvent args)
    {
        base.OnAttemptShoot(ent, ref args);

        if (!ent.Comp.Pumped)
        {
            var message = _input.TryGetKeyBinding(CMKeyFunctions.CMUniqueAction, out var bind)
                ? $"Сначала тебе нужно зарядить это оружие с помощью {bind.GetKeyString()}!"
                : "Сначала тебе нужно зарядить это оружие!";
            _popup.PopupClient(message, args.User, args.User);
        }
    }
}
