using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities.Psionics;

public sealed class TelegnosisPowerSystem : SharedTelegnosisPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, TelegnosisPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, TelegnosisPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
    {
        // do nothing
    }
}
