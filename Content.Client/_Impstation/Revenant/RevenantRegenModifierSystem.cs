using System.Numerics;
using Content.Shared._Impstation.Revenant;
using Content.Shared.Revenant;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._Impstation.Revenant;

public sealed partial class RevenantRegenModifierSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly SpriteSpecifier _witnessIndicator = new SpriteSpecifier.Texture(new ResPath("Interface/Actions/scream.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RevenantHauntWitnessEvent>(OnWitnesses);
    }

    private void OnWitnesses(RevenantHauntWitnessEvent args)
    {
        foreach (var witness in args.Witnesses)
        {
            var ent = GetEntity(witness);
            if (!TryComp<SpriteComponent>(ent, out var sprite))
                continue;

            var layer = _sprite.AddLayer((ent, sprite), _witnessIndicator);
            _sprite.LayerMapSet((ent, sprite), RevenantWitnessVisuals.Key, layer);
            _sprite.LayerSetOffset((ent, sprite), layer, new Vector2(0, 0.8f));
            _sprite.LayerSetScale((ent, sprite), layer, new Vector2(0.65f, 0.65f));

            Timer.Spawn(TimeSpan.FromSeconds(5), () =>
            {
                if (Exists(ent) && TryComp<SpriteComponent>(ent, out sprite))
                    _sprite.RemoveLayer((ent, sprite), RevenantWitnessVisuals.Key);
            });
        }
    }
}

public enum RevenantWitnessVisuals : byte
{
    Key
}
