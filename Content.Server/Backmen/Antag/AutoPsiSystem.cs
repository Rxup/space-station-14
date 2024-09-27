using Content.Server.Antag;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Antag;

public sealed class AutoPsiSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoPsiComponent, MindAddedMessage>(OnMindAdded);
    }


    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultSuperPsiRule = "SuperPsiRule";

    private void OnMindAdded(Entity<AutoPsiComponent> ent, ref MindAddedMessage args)
    {
        RemCompDeferred<AutoPsiComponent>(ent);
        _antag.ForceMakeAntag<AutoPsiComponent>(args.Mind.Comp.Session, DefaultSuperPsiRule);
    }
}
