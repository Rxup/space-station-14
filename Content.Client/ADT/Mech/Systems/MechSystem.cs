using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;

namespace Content.Client.Mech;

/// <inheritdoc/>
public sealed partial class MechSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private void InitializeADT()
    {
        SubscribeLocalEvent<MechComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<MechComponent, MechEquipmentDestroyedEvent>(OnEquipmentDestroyed);
    }

    private void OnMechEntry(EntityUid uid, MechComponent component, MechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        var player = _playerManager.LocalSession;
        var playerEntity = player?.AttachedEntity;

        _audio.PlayPredicted(component.MechEntrySound, uid, playerEntity);
    }
    private void OnEquipmentDestroyed(EntityUid uid, MechComponent component, MechEquipmentDestroyedEvent args)
    {
        var player = _playerManager.LocalSession;
        var playerEntity = player?.AttachedEntity;

        _audio.PlayPredicted(component.MechEntrySound, uid, playerEntity);
    }
}
