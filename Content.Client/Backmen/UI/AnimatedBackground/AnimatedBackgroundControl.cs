using System.Linq;
using Content.Shared.Backmen.Lobby;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.UI.AnimatedBackground;

public sealed class AnimatedBackgroundControl : TextureRect
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private ResPath _rsiPath = new("/Textures/Backmen/LobbyScreens/native.rsi");
    public RSI? _RSI;
    private const int States = 1;

    private IRenderTexture? _buffer;

    private readonly float[] _timer = new float[States];
    private readonly float[][] _frameDelays = new float[States][];
    private readonly int[] _frameCounter = new int[States];
    private readonly Texture[][] _frames = new Texture[States][];

    public AnimatedBackgroundControl()
    {
        IoCManager.InjectDependencies(this);

        InitializeStates();
    }

    private void InitializeStates()
    {
        _RSI ??= _resourceCache.GetResource<RSIResource>(_rsiPath).RSI;

        for (var i = 0; i < States; i++)
        {
            if (!_RSI.TryGetState((i + 1).ToString(), out var state))
                continue;

            _frames[i] = state.GetFrames(RsiDirection.South);
            _frameDelays[i] = state.GetDelays();
            _frameCounter[i] = 0;
        }
    }

    public void SetRSI(ResPath path)
    {
        _rsiPath = path;
        _RSI = null;
        InitializeStates();
    }
    public void SetRSI(RSI? rsi)
    {
        _RSI = rsi;
        InitializeStates();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = 0; i < _frames.Length; i++)
        {
            var delays = _frameDelays[i];
            if (delays.Length == 0)
                continue;

            _timer[i] += args.DeltaSeconds;

            var currentFrameIndex = _frameCounter[i];

            if (!(_timer[i] >= delays[currentFrameIndex]))
                continue;

            _timer[i] -= delays[currentFrameIndex];
            _frameCounter[i] = (currentFrameIndex + 1) % _frames[i].Length;
            Texture = _frames[i][_frameCounter[i]];
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_buffer is null)
            return;

        handle.DrawTextureRect(_buffer.Texture, PixelSizeBox);
    }

    protected override void Resized()
    {
        base.Resized();
        _buffer?.Dispose();
        _buffer = null;
        _buffer = _clyde.CreateRenderTarget(PixelSize, RenderTargetColorFormat.Rgba8Srgb);
    }

    protected override void ExitedTree()
    {
        _buffer?.Dispose();
        _buffer = null;
        _RSI = null;
    }

    public void RandomizeBackground()
    {
        _prototypeManager.TryGetRandom<AnimatedLobbyScreenPrototype>(_random, out var backgroundsProto);
        _rsiPath = ((AnimatedLobbyScreenPrototype?)backgroundsProto)?.Path ?? new("/Textures/Backmen/LobbyScreens/native.rsi");
        _RSI = null;
        InitializeStates();
    }
}
