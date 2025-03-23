﻿using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Backmen.Audio;

[Serializable, NetSerializable]
public sealed class EchoEffectEvent : EntityEventArgs
{
    public NetEntity Target { get; set; }
    public NetEntity Effect { get; set; }
}

public sealed class EchoEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EchoEffectEvent>(OnApplyEffect);
    }

    private void OnApplyEffect(EchoEffectEvent ev)
    {
        if (TryGetEntity(ev.Target, out var sound) && TryGetEntity(ev.Effect, out var effect))
        {
            if (!HasAtmosphere(sound.Value))
            {
                _audio.SetVolume(sound.Value, 0);
                return;
            }

            _audio.SetAuxiliary(sound.Value, Comp<AudioComponent>(sound.Value), effect);
        }
    }

    private bool HasAtmosphere(EntityUid entity)
    {
        var xForm = Transform(entity);
        if (xForm.GridUid == null)
            return false;

        // Получаем GasTileOverlayComponent для текущей сетки
        if (!EntityManager.TryGetComponent<GasTileOverlayComponent>(xForm.GridUid, out var gasTileOverlay))
            return false;

        // Получаем данные о газах на тайле
        var tileIndices = _mapSystem.WorldToTile(entity, xForm.GridUid.Value, xForm.WorldPosition);
        var chunkIndices = GetChunkIndices(tileIndices);

        if (!gasTileOverlay.Chunks.TryGetValue(chunkIndices, out var chunk))
            return false;

        // Получаем GasMixture для текущего тайла
        var atmosphereSystem = EntitySystem.Get<SharedAtmosphereSystem>();
        var gasMixture = atmosphereSystem.GasMixture(xForm.GridUid.Value, tileIndices);

        // Проверяем, что давление больше нуля
        return gasMixture?.Pressure > 0;
    }

    private Vector2i GetChunkIndices(Vector2i tileIndices)
    {
        return new Vector2i(
            tileIndices.X / SharedGasTileOverlaySystem.ChunkSize,
            tileIndices.Y / SharedGasTileOverlaySystem.ChunkSize
        );
    }

    private static readonly Dictionary<ProtoId<AudioPresetPrototype>, EntityUid> CachedEffects = new();

    public bool TryAddEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!HasAtmosphere(sound.Owner))
            return false;

        if (!CachedEffects.TryGetValue(preset, out var effect) && !TryCreateEffect(preset, out effect))
            return false;

        if (_net.IsServer)
        {
            RaiseNetworkEvent(new EchoEffectEvent()
            {
                Target = GetNetEntity(sound),
                Effect = GetNetEntity(effect)
            },
                Filter.Pvs(sound));
        }
        else
        {
            _audio.SetAuxiliary(sound, sound, effect);
        }

        return true;
    }

    public bool TryCreateEffect(ProtoId<AudioPresetPrototype> preset, out EntityUid effectSound)
    {
        effectSound = default;

        if (!_prototype.TryIndex(preset, out var prototype))
            return false;

        var effect = _audio.CreateEffect();
        var auxiliary = _audio.CreateAuxiliary();
        _audio.SetEffectPreset(effect.Entity, effect.Component, prototype);
        _audio.SetEffect(auxiliary.Entity, auxiliary.Component, effect.Entity);

        if (!Exists(auxiliary.Entity) || !CachedEffects.TryAdd(preset, auxiliary.Entity))
            return false;

        effectSound = auxiliary.Entity;

        return true;
    }
}
