using Content.Shared.Heretic;

namespace Content.Server.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticBladeComponent : Component
{
    [DataField] public HereticPath? Path;
}
