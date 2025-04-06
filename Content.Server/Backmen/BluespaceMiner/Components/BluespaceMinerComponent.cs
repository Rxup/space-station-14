using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.BluespaceMining
{
    [RegisterComponent]
    public sealed partial class BluespaceMinerComponent : Component
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("entityTable")]
        public List<string> EntityTable = new();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("spawnAmount")]
        public int SpawnAmount = 1;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("spawnInterval")]
        public float SpawnInterval = 10f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxTemperature")]
        public float MaxTemperature = 283.15f; // 10C

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minTemperature")]
        public float MinTemperature = 193.15f; // -80C

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("heatPerSecond")]
        public float HeatPerSecond = 100f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("explosionThreshold")]
        public float ExplosionThreshold = 373.15f; // 100C

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("explosionRange")]
        public float ExplosionRange = 4f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float NextSpawnTime;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsActive = false;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("requiresPower")]
        public bool RequiresPower = true;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool NeedsResync = true;
    }
}
