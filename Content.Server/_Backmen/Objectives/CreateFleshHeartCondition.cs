using Content.Server._Backmen.Flesh;

namespace Content.Server._Backmen.Objectives;

[RegisterComponent]
public sealed partial class CreateFleshHeartConditionComponent : Component
{
    public bool IsFleshHeartFinale(FleshHeartComponent comp)
    {
        return comp.State == FleshHeartSystem.HeartStates.Disable;
    }
}
