using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Body;

/// <summary>
/// Internal organs use tiny RSI states; once removed from a body they are hard to spot on the floor.
/// </summary>
public sealed partial class LooseOrganVisualSystem : EntitySystem
{
    private static readonly Vector2 SmallLooseScale = new(3f, 3f);
    private static readonly Vector2 InternalLooseScale = new(2f, 2f);

    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> SmallCategories =
    [
        "Eyes",
        "Ears",
        "Brain",
        "Tongue",
        "Appendix",
    ];

    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> InternalCategories =
    [
        "Lungs",
        "Heart",
        "Stomach",
        "Liver",
        "Kidneys",
    ];

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, ComponentStartup>(OnOrganStartup);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganRemoved);
    }

    private void OnOrganStartup(Entity<OrganComponent> ent, ref ComponentStartup args) =>
        UpdateLooseScale(ent);

    private void OnOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args) =>
        UpdateLooseScale(ent);

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args) =>
        UpdateLooseScale(ent);

    private void UpdateLooseScale(Entity<OrganComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (ent.Comp.Body != null)
        {
            _sprite.SetScale((ent, sprite), Vector2.One);
            return;
        }

        if (ent.Comp.Category is not { } category)
            return;

        var scale = SmallCategories.Contains(category)
            ? SmallLooseScale
            : InternalCategories.Contains(category)
                ? InternalLooseScale
                : Vector2.One;

        _sprite.SetScale((ent, sprite), scale);
    }
}
