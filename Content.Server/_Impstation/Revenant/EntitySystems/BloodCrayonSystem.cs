using Content.Server.Crayon;
using Content.Server.Popups;
using Content.Server._Impstation.Revenant.Components;
using Content.Server.Revenant.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Revenant.Components;

namespace Content.Server._Impstation.Revenant.EntitySystems;

public sealed partial class BloodCrayonSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RevenantSystem _revenant = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCrayonComponent, AfterInteractEvent>(OnCrayonUse, before: [typeof(CrayonSystem)]);
    }

    private void OnCrayonUse(EntityUid uid, BloodCrayonComponent comp, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RevenantComponent>(args.User, out var revenant))
            return;

        if (!_revenant.ChangeEssenceAmount(args.User, -revenant.BloodWritingCost, allowDeath: false))
        {
            _popup.PopupEntity(Loc.GetString("revenant-not-enough-essence"), uid, args.User);
            args.Handled = true;
        }
    }
}
