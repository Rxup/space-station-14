using Content.Server.Backmen.Cloning;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Content.Shared.Body.Components;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Buckle.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Server.Bible.Components;
using Content.Server.Stunnable;
using Content.Server.DoAfter;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Backmen.Soul;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Chapel;
using Content.Shared.Backmen.Chapel.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Backmen.Soul;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Players;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Chapel;

public sealed class SacrificialAltarSystem : SharedSacrificialAltarSystem
{
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SacrificialAltarComponent, SacrificeDoAfterEvent>(OnDoAfter);

    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string MaterialBluespace = "MaterialBluespace1";
    private void OnDoAfter(EntityUid uid, SacrificialAltarComponent component, SacrificeDoAfterEvent args)
    {
        _audioSystem.Stop(component.SacrificeStingStream,component.SacrificeStingStream);
        component.DoAfter = null;

        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        if (!_mindSystem.TryGetMind(target, out var mindId, out var mind))
            return;

        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(args.Args.User):player} sacrificed {ToPrettyString(target):target} on {ToPrettyString(uid):altar}");

        var pool = _prototypeManager.Index(component.RewardPool);

        var chance = HasComp<BibleUserComponent>(args.Args.User) ? component.RewardPoolChanceBibleUser : component.RewardPoolChance;

        var pos = Transform(uid).Coordinates;

        if (_robustRandom.Prob(chance))
            Spawn(pool.Pick(), pos);

        var i = _robustRandom.Next(component.BluespaceRewardMin, component.BlueSpaceRewardMax);

        while (i > 0)
        {
            Spawn(MaterialBluespace, pos);
            i--;
        }

        int reduction = _robustRandom.Next(component.GlimmerReductionMin, component.GlimmerReductionMax);
        _glimmerSystem.Glimmer -= reduction;

        var trap = Spawn(component.TrapPrototype, pos);
        _mindSystem.TransferTo(mindId, trap, mind: mind);

        if (TryComp<SoulCrystalComponent>(trap, out var crystalComponent))
            crystalComponent.TrueName = Name(target);

        _metaDataSystem.SetEntityName(trap, Loc.GetString("soul-entity-name", ("trapped", target)));
        _metaDataSystem.SetEntityDescription(trap, Loc.GetString("soul-entity-name", ("trapped", target)));

        if (TryComp<BodyComponent>(target, out var body))
        {
            _bodySystem.GibBody(target, false, body, true);
        }
        else
        {
            QueueDel(target);
        }
    }

    protected override void AttemptSacrifice(EntityUid agent, EntityUid patient, EntityUid altar, SacrificialAltarComponent? component = null)
    {
        if (!Resolve(altar, ref component))
            return;

        if (component.DoAfter != null)
            return;

        // can't sacrifice yourself
        if (agent == patient)
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-self"), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        // you need psionic OR bible user
        if (!(HasComp<PsionicComponent>(agent) || HasComp<BibleUserComponent>(agent) || HasComp<GhostComponent>(agent)))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-user"), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        // and no golems or familiars or whatever should be sacrificing
        if (!(HasComp<HumanoidAppearanceComponent>(agent) || HasComp<GhostComponent>(agent)))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-user-humanoid"), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (!HasComp<PsionicComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target", ("target", patient)), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (!HasComp<HumanoidAppearanceComponent>(patient) && !HasComp<MetempsychosisKarmaComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target-humanoid", ("target", patient)), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (!HasComp<ActorComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target-ssd", ("target", patient)), altar, agent, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (HasComp<BibleUserComponent>(agent))
        {
            if (component.StunTime == null || _timing.CurTime > component.StunTime)
            {
                _stunSystem.TryParalyze(patient, component.SacrificeTime + TimeSpan.FromSeconds(1), true);
                component.StunTime = _timing.CurTime + component.StunCD;
            }
        }

        _popups.PopupEntity(Loc.GetString("altar-popup", ("user", agent), ("target", patient)), altar, Shared.Popups.PopupType.LargeCaution);

        component.SacrificeStingStream = _audioSystem.PlayPvs(component.SacrificeSoundPath, altar);

        var ev = new SacrificeDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, agent, (float) component.SacrificeTime.TotalSeconds, ev, altar, target: patient, used: altar)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(args, out var doAfterId);
        component.DoAfter = doAfterId;
    }
}
