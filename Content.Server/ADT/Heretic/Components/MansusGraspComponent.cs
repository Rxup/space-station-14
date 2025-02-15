using Content.Server.Heretic.EntitySystems;
using Content.Shared.Heretic;

namespace Content.Server.Heretic.Components;

[RegisterComponent, Access(typeof(MansusGraspSystem))]
public sealed partial class MansusGraspComponent : Component
{
    [DataField] public HereticPath? Path = null;
}
