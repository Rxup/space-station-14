using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Gatherable;

/// <summary>
/// Lets the energy pickaxe instantly gather lavaland ore rock when wielded.
/// </summary>
public sealed partial class BackmenEnergyPickaxeGatherSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> EnergyPickaxeTag = "NFEnergyPickaxe";

    [Dependency] private GatherableSystem _gatherable = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GatherableComponent, AttackedEvent>(OnAttacked);
    }

    private void OnAttacked(Entity<GatherableComponent> gatherable, ref AttackedEvent args)
    {
        var tool = args.Used;

        if (!_tagSystem.HasTag(tool, EnergyPickaxeTag))
            return;

        if (!TryComp<WieldableComponent>(tool, out var wieldable) || !wieldable.Wielded)
            return;

        var mapUid = Transform(gatherable).MapUid;
        if (mapUid is not { } map || !HasComp<LavalandMapComponent>(map))
            return;

        _gatherable.Gather(gatherable, args.User);
    }
}
