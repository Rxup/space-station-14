using Content.Shared.Backmen.Disease;
using JetBrains.Annotations;

namespace Content.Client.Backmen.Disease.UI;

[UsedImplicitly]
public sealed class VaccineMachineBoundUserInterface : BoundUserInterface
{
    private VaccineMachineMenu? _machineMenu;

    public VaccineMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _machineMenu = new VaccineMachineMenu(this);

        _machineMenu.OnClose += Close;

        _machineMenu.OnServerSelectionButtonPressed += _ =>
        {
            SendMessage(new VaccinatorServerSelectionMessage());
        };

        _machineMenu.OpenCentered();
        _machineMenu?.PopulateBiomass(Owner);
    }

    public void CreateVaccineMessage(string disease, int amount)
    {
        SendMessage(new CreateVaccineMessage(disease, amount));
    }

    public void RequestSync()
    {
        SendMessage(new VaccinatorSyncRequestMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case VaccineMachineUpdateState msg:
                _machineMenu?.UpdateLocked(msg.Locked);
                _machineMenu?.PopulateDiseases(msg.Diseases);
                _machineMenu?.PopulateBiomass(Owner);
                _machineMenu?.UpdateCost(msg.BiomassCost);
                _machineMenu?.UpdateServerConnection(msg.HasServer);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _machineMenu?.Dispose();
    }
}
