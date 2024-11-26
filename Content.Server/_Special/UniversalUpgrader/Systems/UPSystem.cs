
using Content.Server._Special.UniversalUpgrader.Components;
using Content.Shared.Interaction;

namespace Content.Server._Special.UniversalUpgrader.Systems;

public sealed class UPSystem  : EntitySystem
{

    [Dependency] private readonly EntityManager _ent = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UPComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnInteract(Entity<UPComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { Valid: true } target)
            return;
        if (entity.Comp.ProtoWhitelist != null && HasComp<MetaDataComponent>(target))
        {
            var z = _ent.GetComponent<MetaDataComponent>(target);
            if (z.EntityPrototype!.ID != entity.Comp.ProtoWhitelist)
                return;
        }

        Type? g = Type.GetType(entity.Comp.componentName);

        if (g != null && _ent.TryGetComponent(target, g, out var comp))
        {

            var h = comp.GetType().GetField(entity.Comp.upgradeName);

            if (h != null)
            {

                if (h.FieldType == typeof(int)) h.SetValue(comp,(int) entity.Comp.upgradeValue);
                if (h.FieldType == typeof(float)) h.SetValue(comp,(int) entity.Comp.upgradeValue);

            }
            entity.Comp.usable -= 1;
            if (entity.Comp.usable < 0) _ent.QueueDeleteEntity(entity);

        }
    }

}
