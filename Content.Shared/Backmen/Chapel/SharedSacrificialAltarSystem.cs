using Content.Shared.Backmen.Chapel.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Backmen.Chapel;

public abstract class SharedSacrificialAltarSystem : EntitySystem
{
    [Dependency]
    private readonly SharedCuffableSystem _cuffable = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SacrificialAltarComponent, StrapAttemptEvent>(OnStrappedEvent);
        SubscribeLocalEvent<SacrificialAltarComponent, UnstrapAttemptEvent>(OnUnstrappedEvent);
        SubscribeLocalEvent<SacrificialAltarComponent, GetVerbsEvent<AlternativeVerb>>(AddSacrificeVerb);
    }

    private void OnUnstrappedEvent(EntityUid uid, SacrificialAltarComponent component, ref UnstrapAttemptEvent args)
    {
        if (
            TryComp<CuffableComponent>(args.Buckle, out var cuff) &&
            _cuffable.IsCuffed((args.Buckle, cuff)))
        {
            args.Cancelled = true;
            return;
        }
    }

    private void OnStrappedEvent(EntityUid uid, SacrificialAltarComponent component, ref StrapAttemptEvent args)
    {
        /*
        if (args.User == args.Buckle)
        {
            args.Cancelled = true;
            return;
        }*/
    }

    private void AddSacrificeVerb(EntityUid uid, SacrificialAltarComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.DoAfter != null)
            return;

        if (!TryComp<StrapComponent>(uid, out var strap))
            return;

        EntityUid? sacrificee = null;

        foreach (var entity in strap.BuckledEntities) // mm yes I love hashsets which can't be accessed via index
        {
            sacrificee = entity;
        }

        if (sacrificee == null)
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                AttemptSacrifice(args.User, sacrificee.Value, uid, component);
            },
            Text = Loc.GetString("altar-sacrifice-verb"),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    protected virtual void AttemptSacrifice(EntityUid agent,
        EntityUid patient,
        EntityUid altar,
        SacrificialAltarComponent? component = null)
    {

    }
}
