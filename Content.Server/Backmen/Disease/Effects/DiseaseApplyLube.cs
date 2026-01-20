using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Lube;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Makes the diseased entity apply lube to items when touching them
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseApplyLube : DiseaseEffect
{
    /// <summary>
    /// Chance to apply lube when touching an item (0-1)
    /// </summary>
    [DataField("lubeChance")]
    public float LubeChance = 0.3f;

    /// <summary>
    /// Number of slips the lubed item will have
    /// </summary>
    [DataField("slips")]
    public int Slips = 3;

    /// <summary>
    /// Slip strength
    /// </summary>
    [DataField("slipStrength")]
    public float SlipStrength = 2.0f;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseApplyLube>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void DiseaseApplyLube(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseApplyLube> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;

        // Add component that will handle interaction events and store effect parameters
        var component = EnsureComp<DiseaseLubeHandsComponent>(args.DiseasedEntity);
        component.LubeChance = args.DiseaseEffect.LubeChance;
        component.Slips = args.DiseaseEffect.Slips;
        component.SlipStrength = args.DiseaseEffect.SlipStrength;
    }
}

public sealed partial class DiseaseLubeHandsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseaseLubeHandsComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<DiseaseLubeHandsComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { Valid: true } target)
            return;

        // DiseaseLubeHandsComponent should only apply to entities with ItemComponent
        if (!HasComp<ItemComponent>(target))
            return;

        // Don't apply to already lubed items
        if (HasComp<LubedComponent>(target))
            return;

        if (!_random.Prob(entity.Comp.LubeChance))
            return;

        // Apply lube
        var lubed = EnsureComp<LubedComponent>(target);
        lubed.SlipsLeft = entity.Comp.Slips;
        lubed.SlipStrength = entity.Comp.SlipStrength;

        _popup.PopupEntity(Loc.GetString("disease-lube-applied"), entity, entity, PopupType.Small);
    }
}
