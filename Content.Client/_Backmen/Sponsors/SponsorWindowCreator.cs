using Content.Client._Backmen.SponsorManager.UI;
using Content.Corvax.Interfaces.Client;

namespace Content.Client._Backmen.Sponsors;

public sealed class SponsorWindowCreator : ISponsorWindowCreator
{
    private SponsorWindow? _window;
    public void OpenWindow()
    {
        _window ??= new SponsorWindow();

        if (_window.IsOpen)
        {
            _window.Close();
        }
        _window.OpenCentered();
    }
}
