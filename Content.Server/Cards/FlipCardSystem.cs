using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Content.Shared.Cards;

namespace Content.Server.Cards;


public sealed class FlipCardSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<FlipCardComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, FlipCardComponent component, UseInHandEvent args)
    {
        component.Flipped = !component.Flipped;
        _appearanceSystem.SetData(uid, CardsVisual.Visual, component.Flipped ? CardsVisual.Flipped : CardsVisual.Normal);
    }
}