using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities.Psionics;

public sealed class NoosphericZapPowerSystem : SharedNoosphericZapPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, NoosphericZapPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, NoosphericZapPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, NoosphericZapPowerComponent component, NoosphericZapPowerActionEvent args)
    {
        // do nothing
    }
}
