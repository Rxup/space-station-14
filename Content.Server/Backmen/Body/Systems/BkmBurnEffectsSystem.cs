using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Shared visual/audio effects for burning body parts and mobs to ash.
/// </summary>
public sealed partial class BkmBurnEffectsSystem : EntitySystem
{
    public static readonly EntProtoId Ash = "Ash";

    private static readonly SoundSpecifier BurnSound =
        new SoundCollectionSpecifier("MeatLaserImpact");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;

    public void SpawnAshAt(EntityCoordinates coordinates)
    {
        Spawn(Ash, coordinates);
    }

    public void SpawnAshAt(MapCoordinates coordinates)
    {
        Spawn(Ash, coordinates);
    }

    public void PlayBurnSound(EntityUid atEntity)
    {
        _audio.PlayPvs(BurnSound, atEntity);
    }

    public void PopupBodyBurn(EntityUid body, EntityCoordinates? coordinates = null)
    {
        var coords = coordinates ?? _transform.GetMoverCoordinates(body);
        var identity = Identity.Entity(body, EntityManager);
        _popup.PopupCoordinates(
            Loc.GetString("bodyburn-text-others", ("name", identity)),
            coords,
            PopupType.LargeCaution);
    }

    public void PopupPartBurn(EntityUid part, EntityCoordinates? coordinates = null)
    {
        var coords = coordinates ?? _transform.GetMoverCoordinates(part);
        var identity = Identity.Entity(part, EntityManager);
        _popup.PopupCoordinates(
            Loc.GetString("partburn-text-others", ("name", identity)),
            coords,
            PopupType.LargeCaution);
    }
}
