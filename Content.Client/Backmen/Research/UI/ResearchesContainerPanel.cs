using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using System.Linq;
using System.Numerics;

namespace Content.Client.Backmen.Research.UI;

/// <summary>
/// UI element for visualizing technologies prerequisites
/// </summary>
public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    private static readonly Color LineColor = Color.FromHex("#9EB8D8");

    public ResearchesContainerPanel()
    {
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        // start-backmen: rnd-auto-layout
        var items = Children.OfType<FancyResearchConsoleItem>().ToDictionary(x => x.Prototype.ID);

        foreach (var dependent in items.Values)
        {
            if (dependent.Prototype.TechnologyPrerequisites.Count <= 0)
                continue;

            foreach (var prereqId in dependent.Prototype.TechnologyPrerequisites)
            {
                if (!items.TryGetValue(prereqId, out var prerequisite))
                    continue;

                DrawPrerequisiteLine(handle, prerequisite, dependent);
            }
        }
        // end-backmen: rnd-auto-layout
    }

    // start-backmen: rnd-auto-layout
    private static void DrawPrerequisiteLine(
        DrawingHandleScreen handle,
        FancyResearchConsoleItem prerequisite,
        FancyResearchConsoleItem dependent)
    {
        var start = new Vector2(
            prerequisite.PixelPosition.X + prerequisite.PixelWidth / 2f,
            prerequisite.PixelPosition.Y + prerequisite.PixelHeight);
        var end = new Vector2(
            dependent.PixelPosition.X + dependent.PixelWidth / 2f,
            dependent.PixelPosition.Y);

        if (Math.Abs(start.X - end.X) < 1f)
        {
            handle.DrawLine(start, end, LineColor);
            return;
        }

        var midY = (start.Y + end.Y) / 2f;
        handle.DrawLine(start, new Vector2(start.X, midY), LineColor);
        handle.DrawLine(new Vector2(start.X, midY), new Vector2(end.X, midY), LineColor);
        handle.DrawLine(new Vector2(end.X, midY), end, LineColor);
    }
    // end-backmen: rnd-auto-layout
}
