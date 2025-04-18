using Content.Client.Backmen.StationAI.UI;
using Content.Shared.Backmen.StationAI.Components;
using JetBrains.Annotations;

namespace Content.Client.Backmen.StationAI;

[UsedImplicitly]
public sealed class AICameraListBoundUserInterface : BoundUserInterface
{
    public AICameraList? Window;

    public AICameraListBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        Window?.Close();
        EntityUid? gridUid = null;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;
        }

        Window = new AICameraList(gridUid, Owner);
        Window.OpenCentered();
        Window.OnClose += Close;
        Window.WarpToCamera += WindowOnWarpToCamera;
    }

    private void WindowOnWarpToCamera(NetEntity obj)
    {
        SendMessage(new EyeMoveToCam { Entity = EntMan.GetNetEntity(Owner), Uid = obj });
    }

    public override void Update()
    {
        Window?.UpdateCameras();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Window?.Dispose();
    }
}
