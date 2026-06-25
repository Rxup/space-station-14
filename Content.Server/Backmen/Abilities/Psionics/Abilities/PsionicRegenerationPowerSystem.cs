using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Server.Audio;
using static Content.Shared.Examine.ExamineSystemShared;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class PsionicRegenerationPowerSystem : StatusEffectGrantedPowerSystem<PsionicRegenerationPowerComponent, PsionicRegenerationPowerActionEvent>
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ExamineSystemShared _examine = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, PsionicRegenerationDoAfterEvent>(OnDoAfter);
    }

    private readonly EntProtoId ActionPsionicRegeneration = "ActionPsionicRegeneration";

    protected override void EnsurePowerActions(EntityUid uid, PsionicRegenerationPowerComponent component)
    {
        _actions.AddAction(uid, ref component.PsionicRegenerationPowerAction, ActionPsionicRegeneration);

        var actionEnt = _actions.GetAction(component.PsionicRegenerationPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.PsionicRegenerationPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsionicRegenerationPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, PsionicRegenerationPowerComponent component)
    {
        _actions.RemoveAction(uid, component.PsionicRegenerationPowerAction);
    }

    protected override void HandlePowerUse(EntityUid uid, PsionicRegenerationPowerComponent component, PsionicRegenerationPowerActionEvent args)
    {
        if (args.Handled)
            return;
        var ev = new PsionicRegenerationDoAfterEvent(_gameTiming.CurTime);
        var doAfterArgs = new DoAfterArgs(EntityManager, uid, component.UseDelay, ev, uid);

        _doAfterSystem.TryStartDoAfter(doAfterArgs, out var doAfterId);

        component.DoAfter = doAfterId;

        _popupSystem.PopupEntity(Loc.GetString("psionic-regeneration-begin", ("entity", args.Performer)),
            args.Performer,
            // TODO: Use LoS-based Filter when one is available.
            Filter.Pvs(args.Performer).RemoveWhereAttachedEntity(entity => !_examine.InRangeUnOccluded(args.Performer, entity, ExamineRange, null)),
            true,
            PopupType.Medium);

        _audioSystem.PlayPvs(component.SoundUse, args.Performer, AudioParams.Default.WithVolume(8f).WithMaxDistance(1.5f).WithRolloffFactor(3.5f));
        _psionics.LogPowerUsed(args.Performer, "psionic regeneration");
        args.Handled = true;
    }

    private void OnDispelled(EntityUid uid, PsionicRegenerationPowerComponent component, DispelledEvent args)
    {
        if (component.DoAfter == null)
            return;

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        args.Handled = true;
    }

    private static readonly ProtoId<ReagentPrototype> PsionicRegenerationEssence = "PsionicRegenerationEssence";

    private void OnDoAfter(EntityUid uid, PsionicRegenerationPowerComponent component, PsionicRegenerationDoAfterEvent args)
    {
        component.DoAfter = null;

        if (!TryComp<BloodstreamComponent>(uid, out var stream))
            return;

        // DoAfter has no way to run a callback during the process to give
        // small doses of the reagent, so we wait until either the action
        // is cancelled (by being dispelled) or complete to give the
        // appropriate dose. A timestamp delta is used to accomplish this.
        var percentageComplete = Math.Min(1f, (_gameTiming.CurTime - args.StartedAt).TotalSeconds / component.UseDelay);

        var solution = new Solution();
        solution.AddReagent(PsionicRegenerationEssence, FixedPoint2.New(component.EssenceAmount * percentageComplete));
        _bloodstreamSystem.TryAddToBloodstream((uid, stream), solution);
    }
}


