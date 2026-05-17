using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities.Psionics;

public sealed class PyrokinesisPowerSystem : SharedPyrokinesisPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, PyrokinesisPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, PyrokinesisPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, PyrokinesisPowerComponent component, PyrokinesisPowerActionEvent args)
    {
        // do nothing
    }
}
