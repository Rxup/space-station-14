using Content.Server.Backmen.Flesh;

namespace Content.Server.Backmen.Objectives;

[RegisterComponent]
public sealed partial class CreateFleshHeartConditionComponent : Component
{
    public bool IsFleshHeartFinale(FleshHeartComponent comp)
    {
        return comp.State == FleshHeartSystem.HeartStates.Disable;
    }
}
