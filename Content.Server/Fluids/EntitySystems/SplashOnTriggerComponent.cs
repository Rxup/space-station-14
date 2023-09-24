using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Fluids.EntitySystems;

[RegisterComponent]
internal sealed partial class SplashOnTriggerComponent : Component
{
    [DataField("splashReagents")] public List<ReagentQuantity> SplashReagents = new()
    {
    };
}
