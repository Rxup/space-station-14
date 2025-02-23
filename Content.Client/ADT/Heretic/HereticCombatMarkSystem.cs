using Content.Shared.Heretic;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Heretic;

public sealed partial class HereticCombatMarkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticCombatMarkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HereticCombatMarkComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<HereticCombatMarkComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.LayerMapTryGet(0, out var l))
        {
            sprite.LayerSetState(l, ent.Comp.Path.ToString().ToLower());
            return;
        }

        var rsi = new SpriteSpecifier.Rsi(new ResPath("ADT/Heretic/combat_marks.rsi"), ent.Comp.Path.ToString().ToLower());
        var layer = sprite.AddLayer(rsi);

        sprite.LayerMapSet(0, layer);
        sprite.LayerSetShader(layer, "unshaded");
    }
    private void OnShutdown(Entity<HereticCombatMarkComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!sprite.LayerMapTryGet(0, out var layer))
            return;

        sprite.RemoveLayer(layer);
    }
}
