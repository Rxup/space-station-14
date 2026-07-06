using System.Diagnostics.CodeAnalysis;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Keeps space-animal organ immunity status effects in sync with organ enable/severity.
/// </summary>
public sealed partial class SpaceAnimalOrganStatusEffectSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceAnimalOrganComponent, OrganComponentsModifyEvent>(OnComponentsModify);
    }

    private void OnComponentsModify(Entity<SpaceAnimalOrganComponent> ent, ref OrganComponentsModifyEvent args)
    {
        if (!TryComp<OrganComponent>(ent, out var organ))
            return;

        if (!TryResolveOrganBody(ent, organ, out var body))
            body = args.Body;

        SyncStatusEffect(organ, ent.Comp, body, organ.OrganSeverity);
    }

    private bool TryResolveOrganBody(
        EntityUid organUid,
        OrganComponent organ,
        [NotNullWhen(true)] out EntityUid body)
    {
        if (organ.Body is { } organBody)
        {
            body = organBody;
            return true;
        }

        if (_containers.TryGetContainingContainer((organUid, null, null), out var container)
            && HasComp<BodyComponent>(container.Owner))
        {
            body = container.Owner;
            return true;
        }

        body = default;
        return false;
    }

    private void SyncStatusEffect(
        OrganComponent organ,
        SpaceAnimalOrganComponent space,
        EntityUid body,
        OrganSeverity severity)
    {
        var effectProto = GetStatusEffect(organ, space);
        if (effectProto == null)
            return;

        if (organ.Enabled && severity != OrganSeverity.Destroyed)
        {
            _statusEffects.TrySetStatusEffectDuration(body, effectProto.Value);
            return;
        }

        RemoveStatusEffect(body, effectProto.Value);
    }

    private void RemoveStatusEffect(EntityUid body, EntProtoId effectProto)
    {
        if (!_statusEffects.TryGetStatusEffect(body, effectProto, out var effect)
            || TerminatingOrDeleted(effect.Value))
        {
            return;
        }

        Del(effect.Value);
    }

    private static EntProtoId? GetStatusEffect(OrganComponent organ, SpaceAnimalOrganComponent space)
    {
        if (space.LungsStatusEffect is { } lungs)
            return lungs;

        if (space.HeartStatusEffect is { } heart)
            return heart;

        return null;
    }
}
