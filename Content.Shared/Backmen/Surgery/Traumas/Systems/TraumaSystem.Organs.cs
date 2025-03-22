using System.Linq;
using Content.Shared.Body.Organ;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private void InitOrgans()
    {
        SubscribeLocalEvent<OrganComponent, OrganDamageSeverityChanged>(OnOrganSeverityChanged);
        SubscribeLocalEvent<OrganComponent, OrganIntegrityChangedEvent>(SomeOtherShitHappensHere);
    }

    #region Event handling

    private void OnOrganSeverityChanged(EntityUid organ, OrganComponent comp, OrganDamageSeverityChanged args)
    {
        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        if (comp.Body != null && _consciousness.TryGetNerveSystem(comp.Body.Value, out var nerveSys))
        {
            // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
            _pain.TryAddPainModifier(nerveSys.Value, nerveSys.Value, "OrganDestroyed", 20f, time: TimeSpan.FromSeconds(12f));

            _audio.PlayPvs(comp.OrganDestroyedSound, comp.Body.Value);
        }

        _body.RemoveOrgan(organ, comp);
        QueueDel(organ);
    }

    private void SomeOtherShitHappensHere(EntityUid organ, OrganComponent comp, OrganIntegrityChangedEvent args)
    {
        // When we get to this, make some organs work worse based on their state (of course you won't be able to breathe THAT good when your lung is pierced by a .50cal)
    }

    #endregion

    #region Public API

    public bool ApplyDamageToOrgan(EntityUid organ, FixedPoint2 severity, OrganComponent? organComp = null)
    {
        if (!Resolve(organ, ref organComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(organComp.OrganIntegrity - severity, 0, organComp.IntegrityCap);
        if (organComp.OrganIntegrity == newIntegrity)
            return false;

        var ev = new OrganIntegrityChangedEvent((organ, organComp), organComp.OrganIntegrity, newIntegrity);
        RaiseLocalEvent(organ, ref ev, true);

        organComp.OrganIntegrity = newIntegrity;

        UpdateOrganIntegrity(organ, organComp);
        Dirty(organ, organComp);

        return true;
    }

    public bool TryCreateOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers.Add((identifier, effectOwner), severity);
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TrySetOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = severity;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryChangeOrganDamageModifier(EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryGetValue((identifier, effectOwner), out var value))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = value + change;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryRemoveOrganDamageModifier(EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.Remove((identifier, effectOwner)))
            return false;

        UpdateOrganIntegrity(uid, organ);
        return true;
    }

    #endregion

    #region Private API

    private void UpdateOrganIntegrity(EntityUid uid, OrganComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;
        organ.OrganIntegrity = FixedPoint2.Clamp(organ.IntegrityModifiers
                .Aggregate((FixedPoint2) 0, (current, modifier) => current + modifier.Value),
                0,
                organ.IntegrityCap);

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganIntegrityChangedEvent((uid, organ), oldIntegrity, organ.OrganIntegrity);
            RaiseLocalEvent(uid, ref ev, true);

            // TODO: might want also to raise the event on the woundable.
        }

        var nearestSeverity = organ.OrganSeverity;
        foreach (var (severity, value) in organ.IntegrityThresholds.OrderByDescending(kv => kv.Value))
        {
            if (organ.OrganIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != organ.OrganSeverity)
        {
            var ev = new OrganDamageSeverityChanged((uid, organ), organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev, true);

            // TODO: might want also to raise the event on the woundable.
        }
        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    #endregion
}
