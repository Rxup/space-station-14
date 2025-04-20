using Content.Client.Backmen.StationAI.UI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Silicons.StationAi;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Serilog;

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

        var aiSystem = EntMan.System<SharedStationAiSystem>();

        if (!aiSystem.TryGetCore(Owner, out var ai) ||
            ai.Comp?.RemoteEntity == null)
        {
            Logger.ErrorS("AICameraListBoundUserInterface","AI Eye component not found");
            //Close();
            return;
        }

        Window = new AICameraList(gridUid, Owner, ai.Comp.RemoteEntity.Value);
        Window.OpenCentered();
        Window.OnClose += Close;
        Window.WarpToCamera += WindowOnWarpToCamera;
        Window.Refresh.OnPressed += RefreshOnOnPressed;
    }

    private void RefreshOnOnPressed(BaseButton.ButtonEventArgs obj)
    {
        SendMessage(new EyeCamRequest());
    }

    private void WindowOnWarpToCamera(NetEntity obj)
    {
        SendMessage(new EyeMoveToCam { Entity = EntMan.GetNetEntity(Owner), Uid = obj });
    }

    public override void Update()
    {
        Window?.UpdateCameras();
        base.Update();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        Window?.UpdateCameras();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Window?.Dispose();
    }
}
