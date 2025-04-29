using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Silicons.Borgs;

public abstract class SharedBorgSwitchableSubtypeSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, BorgSelectSubtypeMessage>(OnSubtypeSelection);
    }

    private void OnSubtypeSelection(Entity<BorgSwitchableSubtypeComponent> ent, ref BorgSelectSubtypeMessage args)
    {
        SetSubtype(ent, args.Subtype);
    }


    private void OnComponentInit(Entity<BorgSwitchableSubtypeComponent> ent, ref ComponentInit args)
    {
        if(ent.Comp.BorgSubtype == null)
            return;

        SetAppearanceFromSubtype(ent, ent.Comp.BorgSubtype.Value);
    }

    protected virtual void SetAppearanceFromSubtype(Entity<BorgSwitchableSubtypeComponent> ent, ProtoId<BorgSubtypePrototype> subtype)
    {
    }

    private void SetSubtype(Entity<BorgSwitchableSubtypeComponent> ent, ProtoId<BorgSubtypePrototype> subtype)
    {
        if (!Prototypes.HasIndex(subtype))
        {
            return;
        }

        ent.Comp.BorgSubtype = subtype;
        RaiseLocalEvent(ent, new BorgSubtypeChangedEvent(subtype));
    }
}

public struct BorgSubtypeChangedEvent(ProtoId<BorgSubtypePrototype> subtype)
{
    public ProtoId<BorgSubtypePrototype> Subtype { get; } = subtype;
}
