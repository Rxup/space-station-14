using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities;

public sealed class MindSwapPowerSystem : SharedMindSwapPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, MindSwapPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, MindSwapPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, MindSwapPowerComponent component, MindSwapPowerActionEvent args)
    {
        // do nothing
    }
}
