using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Audio.Systems;

public sealed class EchoEffectSystem : EntitySystem
{
    [Dependency] private readonly EchoEffectsSystem _effectsManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<AudioPresetPrototype> EchoEffectPreset = "SewerPipe";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AudioComponent, ComponentInit>(OnInit, before: [typeof(SharedAudioSystem)]);
    }

    private void OnInit(Entity<AudioComponent> ent, ref ComponentInit args)
    {
        StartEcho(ent);
    }

    public void StartEcho(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype>? preset = null)
    {
        if (!_timing.IsFirstTimePredicted || !Exists(sound) || sound.Comp.Global )
            return;

        _effectsManager.TryAddEffect(sound, preset ?? EchoEffectPreset);
    }
}
