using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;

namespace Content.Client.Backmen.Abilities.Psionics;

public sealed class PsychokinesisPowerSystem : SharedPsychokinesisPowerSystem
{
    protected override void EnsurePowerActions(EntityUid uid, PsychokinesisPowerComponent component)
    {
        // do nothing
    }

    protected override void RemovePowerActions(EntityUid uid, PsychokinesisPowerComponent component)
    {
        // do nothing
    }

    protected override void HandlePowerUse(EntityUid uid, PsychokinesisPowerComponent component, PsychokinesisPowerActionEvent args)
    {
        // do nothing
    }
}
