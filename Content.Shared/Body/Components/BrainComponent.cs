using Content.Shared.Body.Systems;

namespace Content.Shared.Body;

[RegisterComponent, Access(typeof(BrainSystem))]
public sealed partial class BrainComponent : Component;
