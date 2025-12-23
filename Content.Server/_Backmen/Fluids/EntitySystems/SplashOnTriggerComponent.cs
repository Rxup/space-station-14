using Content.Shared.Chemistry.Reagent;

namespace Content.Server._Backmen.Fluids.EntitySystems;

[RegisterComponent]
internal sealed partial class SplashOnTriggerComponent : Component
{
    [DataField("splashReagents")] public List<ReagentQuantity> SplashReagents = new()
    {
    };
}
