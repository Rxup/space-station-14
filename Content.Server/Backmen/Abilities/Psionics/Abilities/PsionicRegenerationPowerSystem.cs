using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Examine;
using Robust.Server.Audio;
using static Content.Shared.Examine.ExamineSystemShared;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PsionicRegenerationPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, PsionicRegenerationPowerActionEvent>(OnPowerUsed);

        SubscribeLocalEvent<PsionicRegenerationPowerComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<PsionicRegenerationPowerComponent, PsionicRegenerationDoAfterEvent>(OnDoAfter);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionPsionicRegeneration = "ActionPsionicRegeneration";

    private void OnInit(EntityUid uid, PsionicRegenerationPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.PsionicRegenerationPowerAction, ActionPsionicRegeneration);

        if (_actions.TryGetActionData(component.PsionicRegenerationPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.PsionicRegenerationPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsionicRegenerationPowerAction;
    }

    private void OnPowerUsed(EntityUid uid, PsionicRegenerationPowerComponent component, PsionicRegenerationPowerActionEvent args)
    {
        if(args.Handled)
            return;
        var ev = new PsionicRegenerationDoAfterEvent(_gameTiming.CurTime);
        var doAfterArgs = new DoAfterArgs(EntityManager, uid, component.UseDelay, ev, uid);

        _doAfterSystem.TryStartDoAfter(doAfterArgs, out var doAfterId);

        component.DoAfter = doAfterId;

        _popupSystem.PopupEntity(Loc.GetString("psionic-regeneration-begin", ("entity", uid)),
            uid,
            // TODO: Use LoS-based Filter when one is available.
            Filter.Pvs(uid).RemoveWhereAttachedEntity(entity => !_examine.InRangeUnOccluded(uid, entity, ExamineRange, null)),
            true,
            PopupType.Medium);

        _audioSystem.PlayPvs(component.SoundUse, uid, AudioParams.Default.WithVolume(8f).WithMaxDistance(1.5f).WithRolloffFactor(3.5f));
        _psionics.LogPowerUsed(uid, "psionic regeneration");
        args.Handled = true;
    }

    private void OnShutdown(EntityUid uid, PsionicRegenerationPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.PsionicRegenerationPowerAction);
    }

    private void OnDispelled(EntityUid uid, PsionicRegenerationPowerComponent component, DispelledEvent args)
    {
        if (component.DoAfter == null)
            return;

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        args.Handled = true;
    }

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
        solution.AddReagent("PsionicRegenerationEssence", FixedPoint2.New(component.EssenceAmount * percentageComplete));
        _bloodstreamSystem.TryAddToChemicals(uid, solution, stream);
    }
}


