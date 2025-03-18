using Content.Server.Backmen.Explosion.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Explosion.Components;

/// <summary>
/// Grenades that, when triggered, explode into decals.
/// </summary>
[RegisterComponent, Access(typeof(DecalGrenadeSystem))]
public sealed partial class DecalGrenadeComponent : Component
{
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// The kinds of decals to spawn on explosion.
    /// </summary>
    [DataField]
    public List<EntProtoId> DecalPrototypes = new();

    /// <summary>
    /// The number of decals to spawn upon explosion.
    /// </summary>
    [DataField]
    public int DecalCount = 25;

    /// <summary>
    /// The radius in which decals will spawn around the explosion center.
    /// </summary>
    [DataField]
    public float DecalRadius = 3f;

    public string? GetRandomDecal()
    {
        if (DecalPrototypes == null || DecalPrototypes.Count == 0)
            return null;

        if (_random == null)
            return DecalPrototypes[0];

        return DecalPrototypes[_random.Next(DecalPrototypes.Count)];
    }
}
