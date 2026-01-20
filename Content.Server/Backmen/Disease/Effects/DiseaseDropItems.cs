using Content.Shared.Backmen.Disease;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Makes items drop from hands with a chance
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseDropItems : DiseaseEffect
{
    /// <summary>
    /// Chance per tick to drop an item from hands (0-1)
    /// </summary>
    [DataField("dropChance")]
    public float DropChance = 0.1f;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseDropItems>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    private void DiseaseDropItems(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseDropItems> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;

        if (!_random.Prob(args.DiseaseEffect.DropChance))
            return;

        if (!TryComp<HandsComponent>(args.DiseasedEntity, out var hands))
            return;

        Entity<HandsComponent?> handsEntity = (args.DiseasedEntity, hands);

        // Try to drop from active hand first, then any hand
        if (hands.ActiveHandId != null && _handsSystem.TryGetHeldItem(handsEntity, hands.ActiveHandId, out _))
        {
            _handsSystem.TryDrop(handsEntity, hands.ActiveHandId, checkActionBlocker: false);
            return;
        }

        // Drop from any hand
        foreach (var handId in hands.Hands.Keys)
        {
            if (_handsSystem.TryGetHeldItem(handsEntity, handId, out _))
            {
                _handsSystem.TryDrop(handsEntity, handId, checkActionBlocker: false);
                break;
            }
        }
    }
}
