using Content.Shared.Backmen.Surgery;

namespace Content.Client.Backmen.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    public event Action? OnRefresh;

    public override void Update(float frameTime)
    {
        OnRefresh?.Invoke();
    }
}
