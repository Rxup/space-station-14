using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.ComponentsRegistry;

[RegisterComponent]
public sealed partial class GrantComponentsStatusEffectComponent : Component
{
    [DataField(required: true)]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();
}
