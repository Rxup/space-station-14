using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared.Alert;
using Content.Shared.Backmen.Changeling.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Changeling;

public sealed partial class ChangelingSystem : EntitySystem
{

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
        SubscribeLocalEvent<ChangelingComponent, GetStatusIconsEvent>(GetChanglingIcon);
    }

    [ValidatePrototypeId<AlertPrototype>]
    private const string ChangelingChemicals = "ChangelingChemicals";

    [ValidatePrototypeId<AlertPrototype>]
    private const string ChangelingBiomass = "ChangelingBiomass";

    private void OnUpdateAlert(EntityUid uid, ChangelingComponent comp, ref UpdateAlertSpriteEvent args)
    {
        var stateNormalized = 0f;

        // hardcoded because uhh umm i don't know. send help.
        switch (args.Alert.AlertKey.AlertType)
        {
            case ChangelingChemicals:
                stateNormalized = (int)(comp.Chemicals / comp.MaxChemicals * 18);
                break;

            case ChangelingBiomass:
                stateNormalized = (int)(comp.Biomass / comp.MaxBiomass * 16);
                break;
            default:
                return;
        }
        args.SpriteViewEnt.Comp.LayerSetState(AlertVisualLayers.Base, $"{stateNormalized}");
    }

    private void GetChanglingIcon(Entity<ChangelingComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.IsTransponder && _prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
