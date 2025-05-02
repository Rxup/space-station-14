using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.TableSlam;

[RegisterComponent]
public sealed partial class PostTabledComponent : Component
{
    [DataField]
    public TimeSpan PostTabledShovableTime = TimeSpan.Zero;

    [DataField]
    public float ParalyzeChance = 0.35f;
}
