// ReSharper disable once CheckNamespace
using Content.Server.Gatherable.Components;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    private static readonly ProtoId<TagPrototype> EnergyPickaxeTag = "NFEnergyPickaxe";

    private bool TryBackmenEnergyPickaxeGather(Entity<GatherableComponent> gatherable, ref AttackedEvent args)
    {
        var tool = args.Used;

        if (!_tagSystem.HasTag(tool, EnergyPickaxeTag))
            return false;

        if (!TryComp<WieldableComponent>(tool, out var wieldable) || !wieldable.Wielded)
            return false;

        var mapUid = Transform(gatherable).MapUid;
        if (mapUid is not { } map || !HasComp<LavalandMapComponent>(map))
            return false;

        Gather(gatherable, args.User);
        return true;
    }
}
