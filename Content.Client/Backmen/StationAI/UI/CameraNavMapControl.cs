using Content.Client.Pinpointer.UI;

namespace Content.Client.Backmen.StationAI.UI;

public sealed class CameraNavMapControl : NavMapControl
{
    public CameraNavMapControl() : base()
    {
        // Set colors
        TileColor = new Color(30, 57, 67);
        WallColor = new Color(102, 164, 217);
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));
    }
}
