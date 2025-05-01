// Shitmed Change Start
using Content.Shared.Backmen.Smoking.Components;
using Content.Shared.Smoking;

namespace Content.Shared.Backmen.Smoking.Systems;

public abstract class BkmSharedMatchstickSystem : EntitySystem
{
    public virtual bool SetState(Entity<BkmMatchstickComponent> ent, SmokableState state)
    {
        if (ent.Comp.CurrentState == state)
            return false;

        ent.Comp.CurrentState = state;
        Dirty(ent);
        return true;
    }
}
// Shitmed Change End
