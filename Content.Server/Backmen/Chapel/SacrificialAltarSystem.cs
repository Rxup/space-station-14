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

    private void OnDoAfter(EntityUid uid, SacrificialAltarComponent component, SacrificeDoAfterEvent args)
    {
        _audioSystem.Stop(component.SacrificeStingStream,component.SacrificeStingStream);
        component.DoAfter = null;

        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        // note: we checked this twice in case they could have gone SSD in the doafter time.
        if (!TryComp<ActorComponent>(args.Args.Target.Value, out var actor))
            return;

        if (!_mindSystem.TryGetMind(args.Args.Target.Value, out var mindId, out var mind))
            return;

        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(args.Args.User):player} sacrificed {ToPrettyString(args.Args.Target.Value):target} on {ToPrettyString(uid):altar}");

        if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(component.RewardPool, out var pool))
            return;

        var chance = HasComp<BibleUserComponent>(args.Args.User) ? component.RewardPoolChanceBibleUser : component.RewardPoolChance;

        if (_robustRandom.Prob(chance))
            Spawn(pool.Pick(), Transform(uid).Coordinates);

        int i = _robustRandom.Next(component.BluespaceRewardMin, component.BlueSpaceRewardMax);

        while (i > 0)
        {
            Spawn("MaterialBluespace1", Transform(uid).Coordinates);
            i--;
        }

        int reduction = _robustRandom.Next(component.GlimmerReductionMin, component.GlimmerReductionMax);
        _glimmerSystem.Glimmer -= reduction;

        if (actor.PlayerSession.ContentData()?.Mind != null)
        {
            var trap = Spawn(component.TrapPrototype, Transform(uid).Coordinates);
            _mindSystem.TransferTo(mindId, trap);

            if (TryComp<SoulCrystalComponent>(trap, out var crystalComponent))
                crystalComponent.TrueName = Name(args.Args.Target.Value);

            _metaDataSystem.SetEntityName(trap, Loc.GetString("soul-entity-name", ("trapped", args.Args.Target)));
            _metaDataSystem.SetEntityDescription(trap, Loc.GetString("soul-entity-name", ("trapped", args.Args.Target)));
        }

        if (TryComp<BodyComponent>(args.Args.Target, out var body))
        {
            _bodySystem.GibBody(args.Args.Target.Value, false, body, false);
        }
        else
        {
            QueueDel(args.Args.Target.Value);
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
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-self"), altar, agent, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        // you need psionic OR bible user
        if (!HasComp<PsionicComponent>(agent) && !HasComp<BibleUserComponent>(agent))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-user"), altar, agent, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        // and no golems or familiars or whatever should be sacrificing
        if (!HasComp<HumanoidAppearanceComponent>(agent))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-user-humanoid"), altar, agent, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (!HasComp<PsionicComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target", ("target", patient)), altar, agent, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (!HasComp<HumanoidAppearanceComponent>(patient) && !HasComp<MetempsychosisKarmaComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target-humanoid", ("target", patient)), altar, agent, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (!HasComp<ActorComponent>(patient))
        {
            _popups.PopupEntity(Loc.GetString("altar-failure-reason-target-ssd", ("target", patient)), altar, agent, Shared.Popups.PopupType.SmallCaution);
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
