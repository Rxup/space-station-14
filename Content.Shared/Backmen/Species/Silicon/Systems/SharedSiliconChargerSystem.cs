using Content.Shared.Backmen.Silicon;
using Content.Shared.Power;
using Content.Shared.Storage.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Silicon.Charge;

public sealed class SharedSiliconChargerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconChargerComponent, StorageAfterOpenEvent>(HandleStateOpen);
        SubscribeLocalEvent<SiliconChargerComponent, StorageAfterCloseEvent>(HandleStateClose);
    }


    /// <summary>
    ///     Updates the state of the charger when it's open or closed.
    /// </summary>
    private void HandleStateOpen(EntityUid uid, SiliconChargerComponent component, ref StorageAfterOpenEvent _)
    {
        UpdateState(uid, component);
    }

    /// <inheritdoc cref="HandleStateOpen"/>
    private void HandleStateClose(EntityUid uid, SiliconChargerComponent component, ref StorageAfterCloseEvent _)
    {
        UpdateState(uid, component);
    }

    /// <summary>
    ///     Updates the visual and auditory state of the charger based on if it's active, and/or open.
    /// </summary>
    public void UpdateState(EntityUid uid, SiliconChargerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Active)
        {
            _appearance.SetData(uid, PowerDeviceVisuals.VisualState, SiliconChargerVisualState.Charging);

            // If we're in prediction, return since Client doesn't have the information needed to handle this.
            // Didn't seem to matter in practice, but probably for the best.
            if (_timing.InPrediction)
                return;

            if (component.SoundLoop != null && component.SoundStream == null)
                component.SoundStream = _audio.PlayPvs(component.SoundLoop, uid)?.Entity;
        }
        else
        {
            var state = SiliconChargerVisualState.Normal;

            if (EntityManager.TryGetComponent<SharedEntityStorageComponent>(uid, out var storageComp) &&
                storageComp.Open)
                state = SiliconChargerVisualState.NormalOpen;

            _appearance.SetData(uid, PowerDeviceVisuals.VisualState, state);

            // If we're in prediction, return since Client doesn't have the information needed to handle this.
            // Didn't seem to matter in practice, but probably for the best.
            if (_timing.InPrediction)
                return;

            component.SoundStream ??= _audio.Stop(component.SoundStream);
            component.SoundStream = null; //?????????
        }
    }
}
