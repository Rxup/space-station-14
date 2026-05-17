using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities.Psionics;

public sealed class DispelPowerSystem : SharedDispelPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, DispelPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, DispelPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, DispelPowerComponent component, DispelPowerActionEvent args)
    {
        // do nothing
    }
}
