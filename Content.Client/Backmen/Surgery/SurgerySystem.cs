using Content.Shared.Backmen.Surgery;

namespace Content.Client.Backmen.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    public event Action? OnStep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SurgeryUiRefreshEvent>(OnRefresh);
    }

    private void OnRefresh(SurgeryUiRefreshEvent ev)
    {
        OnStep?.Invoke();
    }
}
