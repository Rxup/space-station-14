using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Body.OrganRelations;

public sealed class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTargetingSystem _targeting = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private static readonly ResPath ScalpelIcon =
        new("/Textures/_Shitmed/Objects/Specific/Medical/Surgery/scalpel.rsi/scalpel.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BkmDetachedBodyComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BkmDetachedBodyComponent, BeforeDamageChangedEvent>(OnBeforeBundleDamage);
    }

    /// <summary>
    /// Bundle damage hits contained organs, not the shell's flat <see cref="DamageableComponent"/>.
    /// </summary>
    private void OnBeforeBundleDamage(Entity<BkmDetachedBodyComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs == null || body.Organs.Count == 0)
            return;

        args.Cancelled = true;

        if (!_net.IsServer)
            return;

        var root = ent.Comp.RootOrgan;
        if (root is not { } rootOrgan || TerminatingOrDeleted(rootOrgan))
        {
            var gibEv = new GibDetachedBundleRequestEvent();
            RaiseLocalEvent(ent, ref gibEv);
            return;
        }

        if (TryComp<WoundableComponent>(rootOrgan, out var woundable))
            _wounds.GetWoundsChanged(rootOrgan, args.Origin, args.Damage, component: woundable);
        else
            _damageable.ChangeDamage(rootOrgan, args.Damage, origin: args.Origin);
    }

    /// <summary>
    /// Server-only. Called when an organ is inserted into a detached body bundle.
    /// </summary>
    public void OnOrganInserted(Entity<BkmDetachedBodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_net.IsServer || ent.Comp.RootOrgan != null)
            return;

        ent.Comp.RootOrgan = args.Entity;
        Dirty(ent, ent.Comp);
        UpdateMetadata(ent);
    }

    /// <summary>
    /// Server-only. Called when an organ is removed from a detached body bundle.
    /// </summary>
    public void OnOrganRemoved(Entity<BkmDetachedBodyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_net.IsServer)
            return;

        if (ent.Comp.RootOrgan == args.Entity)
        {
            ent.Comp.RootOrgan = null;
            Dirty(ent, ent.Comp);
        }

        if (TryComp<BodyComponent>(ent, out var body)
            && body.Organs != null
            && body.Organs.Count == 0)
        {
            QueueDel(ent);
            return;
        }

        UpdateMetadata(ent);
    }

    private void UpdateMetadata(Entity<BkmDetachedBodyComponent> ent)
    {
        if (ent.Comp.RootOrgan is not { } root || TerminatingOrDeleted(root))
            return;

        var partName = Name(root);
        _metaData.SetEntityName(ent, partName);
        _metaData.SetEntityDescription(ent, Loc.GetString("bkm-detached-body-desc", ("part", partName)));
    }

    private void OnExamined(Entity<BkmDetachedBodyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("bkm-detached-body-examine-intro"));

        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            args.PushMarkup(Loc.GetString("bkm-detached-body-examine-organ-line", ("organ", Name(organ))));
        }
    }

    private void OnGetVerbs(Entity<BkmDetachedBodyComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<BodyComponent>(ent, out var body)
            || body.Organs == null
            || body.Organs.ContainedEntities.Count == 0)
        {
            return;
        }

        var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString("bkm-detached-body-examine-header"));
        msg.PushNewline();

        foreach (var organ in body.Organs.ContainedEntities)
        {
            var organName = Name(organ);
            msg.AddMarkupPermissive(Loc.GetString("bkm-detached-body-examine-organ-header", ("organ", organName)));
            msg.PushNewline();

            if (TryComp<WoundableComponent>(organ, out var woundable) && woundable.IntegrityCap > 0)
            {
                var integrity = (woundable.WoundableIntegrity / woundable.IntegrityCap * 100).Float();
                msg.AddMarkupPermissive(Loc.GetString("bkm-detached-body-examine-integrity",
                    ("integrity", integrity.ToString("F0"))));
                msg.PushNewline();
            }

            var ev = new SurgeryToolExaminedEvent(msg);
            RaiseLocalEvent(organ, ref ev);
        }

        _examine.AddDetailedExamineVerb(args,
            ent.Comp,
            msg,
            Loc.GetString("bkm-detached-body-examinable-verb-text"),
            ScalpelIcon.CanonPath,
            Loc.GetString("bkm-detached-body-examinable-verb-message"));
    }

    /// <summary>
    /// Finds an organ inside a detached body bundle that matches the surgery part being reattached.
    /// </summary>
    public bool TryGetMatchingOrgan(
        EntityUid tool,
        BodyPartType part,
        BodyPartSymmetry? symmetry,
        out EntityUid organ)
    {
        organ = default;

        if (!TryComp<BkmDetachedBodyComponent>(tool, out _)
            || !TryComp<BodyComponent>(tool, out var body)
            || body.Organs == null)
        {
            return false;
        }

        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (_targeting.MatchesBodyPartType(contained, part, symmetry))
            {
                organ = contained;
                return true;
            }
        }

        return false;
    }
}
