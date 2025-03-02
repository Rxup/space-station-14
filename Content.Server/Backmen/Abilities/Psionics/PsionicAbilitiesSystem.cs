using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Server.EUI;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PsionicAbilitiesSystem : SharedPsionicAbilitiesSystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicAwaitingPlayerComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }


    private void OnPlayerAttached(EntityUid uid, PsionicAwaitingPlayerComponent component, PlayerAttachedEvent args)
    {
        if (TryComp<PsionicBonusChanceComponent>(uid, out var bonus) && bonus.Warn == true)
            _euiManager.OpenEui(new AcceptPsionicsEui(uid, this), args.Player);
        else
            AddRandomPsionicPower(uid);
        RemCompDeferred<PsionicAwaitingPlayerComponent>(uid);
    }

    public void AddPsionics(EntityUid uid, bool warn = false)
    {
        if (Deleted(uid))
            return;

        if (HasComp<PsionicComponent>(uid))
            return;

        if (!_mindSystem.TryGetMind(uid, out var mindId,out var mind))
        {
            EnsureComp<PsionicAwaitingPlayerComponent>(uid);
            return;
        }

        if (!_mindSystem.TryGetSession(mind, out var client))
            return;

        if (warn && TryComp<ActorComponent>(uid, out var actor))
            _euiManager.OpenEui(new AcceptPsionicsEui(uid, this), client);
        else
            AddRandomPsionicPower(uid);
    }

    public void AddPsionics(EntityUid uid, string powerComp)
    {
        if (Deleted(uid))
            return;

        if (HasComp<PsionicComponent>(uid))
            return;

        AddComp<PsionicComponent>(uid);

        var newComponent = (Component) _componentFactory.GetComponent(powerComp);
        AddComp(uid, newComponent);
    }

    [ValidatePrototypeId<WeightedRandomPrototype>]
    private const string RandomPsionicPowerPool = "RandomPsionicPowerPool";

    public void AddRandomPsionicPower(EntityUid uid)
    {
        AddComp<PsionicComponent>(uid);

        if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(RandomPsionicPowerPool, out var pool))
        {
            Log.Error("Can't index the random psionic power pool!");
            return;
        }

        // uh oh, stinky!
        var newComponent = (Component) _componentFactory.GetComponent(pool.Pick());
        EntityManager.AddComponent(uid, newComponent);

        _glimmerSystem.Glimmer += _random.Next(1, 5);
    }

    public void RemovePsionics(EntityUid uid, bool noEffect = false)
    {
        if (!TryComp<PsionicComponent>(uid, out var psionic))
            return;

        if (!psionic.Removable)
            return;

        if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(RandomPsionicPowerPool, out var pool))
        {
            Log.Error("Can't index the random psionic power pool!");
            return;
        }

        foreach (var compName in pool.Weights.Keys)
        {
            // component moment
            var comp = _componentFactory.GetComponent(compName);
            if (EntityManager.TryGetComponent(uid, comp.GetType(), out var psionicPower))
                RemComp(uid, psionicPower);
        }
        if (psionic.PsionicAbility != null)
            _actionsSystem.RemoveAction(uid, psionic.PsionicAbility);

        if(!noEffect)
            _statusEffectsSystem.TryAddStatusEffect(uid, "Stutter", TimeSpan.FromMinutes(5), false, "StutteringAccent");

        RemCompDeferred<PsionicComponent>(uid);
    }
}
