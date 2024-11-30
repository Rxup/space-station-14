using System.Linq;
using Content.Server.DoAfter;
using Content.Server._Special.UniversalUpgrader.Components;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;

namespace Content.Server._Special.UniversalUpgrader.Systems;

public sealed class UPSystem  : EntitySystem
{

    [Dependency] private readonly EntityManager _ent = default!;
    [Dependency] private readonly IComponentFactory _compFact = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UPComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnInteract(Entity<UPComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { Valid: true } target)
            return;
        if (entity.Comp.ProtoWhitelist != "" && HasComp<MetaDataComponent>(target))
        {
            var z = _ent.GetComponent<MetaDataComponent>(target);
            if (!entity.Comp.ProtoWhitelist.Split(" ").Contains(z.EntityPrototype!.ID))
                return;
        }

        var test = _compFact.GetRegistration(entity.Comp.componentName);

        if (_ent.TryGetComponent(target, test.Type, out var comp))
        {
            var t = entity.Comp.upgradeName.Split(' ').Length;
            for (int i = 0; i < t; i++)
            {
                var un = entity.Comp.upgradeName.Split(' ')[i];
                var uv = entity.Comp.upgradeValue.Split(' ')[i];
                var h = comp.GetType().GetField(un);

                if (h != null && uv != null)
                {
                    h.SetValue(h.FieldType, uv );
                }
            }

            entity.Comp.usable -= 1;
            if (entity.Comp.usable < 0) _ent.QueueDeleteEntity(entity);

        }
    }

}


