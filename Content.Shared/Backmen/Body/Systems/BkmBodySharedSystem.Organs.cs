using System.Diagnostics.CodeAnalysis;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Damage.Components;
using Robust.Shared.Containers;

// Shitmed Change

namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    // Shitmed Change Start

    private void InitializeOrgans()
    {
        SubscribeLocalEvent<OrganComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganComponent, OrganEnableChangedEvent>(OnOrganEnableChanged);
    }

    private void OnMapInit(Entity<OrganComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.OnAdd is not null || ent.Comp.OnRemove is not null)
            EnsureComp<OrganEffectComponent>(ent);
    }

    // Shitmed Change End

    private void AddOrgan(
        Entity<OrganComponent> organEnt,
        EntityUid bodyUid,
        EntityUid parentPartUid)
    {
        organEnt.Comp.Body = bodyUid;
        organEnt.Comp.BodyPart = parentPartUid;
        var addedEv = new OrganAddedEvent(parentPartUid);
        RaiseLocalEvent(organEnt, ref addedEv);

        if (organEnt.Comp.Body is not null)
        {
            organEnt.Comp.OriginalBody = organEnt.Comp.Body; // Shitmed Change
            var addedInBodyEv = new OrganAddedToBodyEvent(bodyUid, parentPartUid);
            RaiseLocalEvent(organEnt, ref addedInBodyEv);
            var organEnabledEv = new OrganEnableChangedEvent(true);
            RaiseLocalEvent(organEnt, ref organEnabledEv);
        }

        // Shitmed Change Start
        if (TryComp(parentPartUid, out DamageableComponent? damageable)
            && Damageable.GetTotalDamage((parentPartUid, damageable)) > 200)
            TrySetOrganUsed(organEnt, true, organEnt.Comp);
        // Shitmed Change End

        Dirty(organEnt, organEnt.Comp);
    }

    private void RemoveOrgan(Entity<OrganComponent> organEnt, EntityUid parentPartUid)
    {
        var removedEv = new OrganRemovedEvent(parentPartUid);
        RaiseLocalEvent(organEnt, ref removedEv);

        if (organEnt.Comp.Body is { Valid: true } bodyUid)
        {
            // Shitmed Change Start
            organEnt.Comp.OriginalBody = organEnt.Comp.Body;
            var organDisabledEv = new OrganEnableChangedEvent(false);
            RaiseLocalEvent(organEnt, ref organDisabledEv);
            // Shitmed Change End
            var removedInBodyEv = new OrganRemovedFromBodyEvent(bodyUid, parentPartUid);
            RaiseLocalEvent(organEnt, ref removedInBodyEv);
        }

        if (parentPartUid is { Valid: true }
            && TryComp(parentPartUid, out DamageableComponent? damageable)
            && Damageable.GetTotalDamage((parentPartUid, damageable)) > 200)
            TrySetOrganUsed(organEnt, true, organEnt.Comp);

        organEnt.Comp.Body = null;
        organEnt.Comp.BodyPart = null;
        Dirty(organEnt, organEnt.Comp);
    }

    /// <summary>
    /// Removes the organ from its body container if present.
    /// </summary>
    public bool RemoveOrgan(EntityUid organId, OrganComponent? organ = null)
    {
        if (!Resolve(organId, ref organ, false))
            return false;

        if (organ.Body is { Valid: true } bodyUid
            && TryComp<BodyComponent>(bodyUid, out var bodyComp)
            && bodyComp.Organs != null
            && bodyComp.Organs.Contains(organId))
        {
            if (Containers.Remove(organId, bodyComp.Organs, force: true))
                return true;
        }

        if (Containers.TryGetContainingContainer((organId, null, null), out var container))
        {
            var parent = container.Owner;

            if (TryComp<BodyComponent>(parent, out var bodyFromContainer) && bodyFromContainer.Organs == container)
            {
                if (Containers.Remove(organId, container, force: true))
                    return true;
            }
        }

        if (organ.Body is { Valid: true } fallbackBody && !TerminatingOrDeleted(fallbackBody))
        {
            RemoveOrgan((organId, organ), fallbackBody);
            return true;
        }

        return false;
    }

    public bool InsertOrganIntoBody(
        EntityUid bodyId,
        EntityUid organId,
        BodyComponent? body = null,
        OrganComponent? organ = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false)
            || !Resolve(organId, ref organ, logMissing: false)
            || body!.Organs == null)
            return false;

        if (organ!.Body == bodyId && body.Organs.Contains(organId))
            return true;

        if (organ.Body is { } currentBody
            && TryComp<BodyComponent>(currentBody, out var currentBodyComp)
            && currentBodyComp.Organs is { } currentOrgans
            && currentOrgans.Contains(organId))
        {
            if (!Containers.Remove((organId, null, null), currentOrgans, force: true))
                return false;
        }
        else if (Containers.TryGetContainingContainer((organId, null, null), out var container))
        {
            if (!Containers.Remove((organId, null, null), container, force: true))
                return false;
        }

        return Containers.Insert(organId, body.Organs);
    }

    /// <summary>
    /// Returns a list of Entity<<see cref="T"/>, <see cref="OrganComponent"/>>
    /// for each organ of the body
    /// </summary>
    /// <typeparam name="T">The component that we want to return</typeparam>
    /// <param name="entity">The body to check the organs of</param>
    public List<Entity<T, OrganComponent>> GetBodyOrganEntityComps<T>(
        Entity<BodyComponent?> entity)
        where T : IComponent
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<Entity<T, OrganComponent>>();

        var query = GetEntityQuery<T>();
        var list = new List<Entity<T, OrganComponent>>(3);
        foreach (var organ in GetBodyOrgans(entity.Owner, entity.Comp))
        {
            if (query.TryGetComponent(organ.Id, out var comp))
                list.Add((organ.Id, comp, organ.Component));
        }

        return list;
    }

    /// <summary>
    ///     Tries to get a list of ValueTuples of <see cref="T"/> and OrganComponent on each organs
    ///     in the given body.
    /// </summary>
    /// <param name="uid">The body entity id to check on.</param>
    /// <param name="comps">The list of components.</param>
    /// <param name="body">The body to check for organs on.</param>
    /// <typeparam name="T">The component to check for.</typeparam>
    /// <returns>Whether any were found.</returns>
    public bool TryGetBodyOrganEntityComps<T>(
        Entity<BodyComponent?> entity,
        [NotNullWhen(true)] out List<Entity<T, OrganComponent>>? comps)
        where T : IComponent
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
        {
            comps = null;
            return false;
        }

        comps = GetBodyOrganEntityComps<T>(entity);

        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    // Shitmed Change Start

    public bool TrySetOrganUsed(EntityUid organId, bool used, OrganComponent? organ = null)
    {
        if (!Resolve(organId, ref organ)
            || organ.Used == used)
            return false;

        organ.Used = used;
        Dirty(organId, organ);
        return true;
    }

    private void OnOrganEnableChanged(Entity<OrganComponent> organEnt, ref OrganEnableChangedEvent args)
    {
        if (!organEnt.Comp.CanEnable && args.Enabled)
            return;

        organEnt.Comp.Enabled = args.Enabled;

        if (args.Enabled)
            EnableOrgan(organEnt);
        else
            DisableOrgan(organEnt);

        if (organEnt.Comp.Body is { Valid: true } bodyEnt)
            RaiseLocalEvent(organEnt, new OrganComponentsModifyEvent(bodyEnt, args.Enabled));

        Dirty(organEnt, organEnt.Comp);
    }

    private void EnableOrgan(Entity<OrganComponent> organEnt)
    {
        if (!TryComp(organEnt.Comp.Body, out BodyComponent? body))
            return;

        var ev = new OrganEnabledEvent(organEnt);
        RaiseLocalEvent(organEnt, ref ev);
    }

    private void DisableOrgan(Entity<OrganComponent> organEnt)
    {
        if (!TryComp(organEnt.Comp.Body, out BodyComponent? body))
            return;

        var ev = new OrganDisabledEvent(organEnt);
        RaiseLocalEvent(organEnt, ref ev);
    }

    // Shitmed Change End
}
