using Content.Shared.StatusEffect;
using Content.Shared.Backmen.CCVar;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Electrocution;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Psionics;

public sealed class PsionicsSystem : SharedPsionicsSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilitiesSystem = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwapPowerSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactonSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;


    /// <summary>
    /// Unfortunately, since spawning as a normal role and anything else is so different,
    /// this is the only way to unify them, for now at least.
    /// </summary>
    Queue<Entity<PotentialPsionicComponent>> _rollers = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var roller in _rollers)
        {
            RollPsionics(roller, false);
        }
        _rollers.Clear();
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PotentialPsionicComponent, MapInitEvent>(OnStartup);

        //SubscribeLocalEvent<PotentialPsionicComponent, MobStateChangedEvent>(OnDeathGasp);

        SubscribeLocalEvent<PsionicComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicComponent, ComponentRemove>(OnRemove);




        _mindSwapped = GetEntityQuery<MindSwappedComponent>();
    }

    protected override void RemovePsionics(Entity<PotentialPsionicComponent?> ent)
    {
        if (!PotentialPsionicQuery.Resolve(ent, ref ent.Comp, false))
            return;

        _psionicAbilitiesSystem.RemovePsionics(ent, true);
        _rollers.Enqueue(ent!);
    }

    protected override void AddPsionics(Entity<PotentialPsionicComponent?> ent)
    {
        if (!PotentialPsionicQuery.Resolve(ent, ref ent.Comp, false))
            return;

        _psionicAbilitiesSystem.AddPsionics(ent, false);
    }

    protected override bool UndoMindSwap(EntityUid entity)
    {
        if (!_mindSwapped.TryComp(entity, out var swapped))
            return false;

        _mindSwapPowerSystem.Swap(entity, swapped.OriginalEntity, true);
        return true;
    }


    private void OnStartup(Entity<PotentialPsionicComponent> ent, ref MapInitEvent args)
    {
        if (PsionicQuery.HasComp(ent))
            return;

        _rollers.Enqueue(ent);
    }

    private EntityQuery<MindSwappedComponent> _mindSwapped;

/*
    private void OnDeathGasp(EntityUid uid, PotentialPsionicComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        string message;

        switch (_glimmerSystem.GetGlimmerTier())
        {
            case GlimmerTier.Critical:
                message = Loc.GetString("death-gasp-high", ("ent", Identity.Entity(uid, EntityManager)));
                break;
            case GlimmerTier.Dangerous:
                message = Loc.GetString("death-gasp-medium", ("ent", Identity.Entity(uid, EntityManager)));
                break;
            default:
                message = Loc.GetString("death-gasp-normal", ("ent", Identity.Entity(uid, EntityManager)));
                break;
        }

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, true, ignoreActionBlocker: true);
    }
*/
    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string FactionGlimmerMonster = "GlimmerMonster";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string FactionPsionic = "PsionicInterloper";

    private void OnInit(EntityUid uid, PsionicComponent component, ComponentInit args)
    {
        if (!component.Removable)
            return;

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factions))
            return;

        Entity<NpcFactionMemberComponent?> ent = (uid,factions);

        if (_npcFactonSystem.IsMember(ent, FactionGlimmerMonster))
            return;

        _npcFactonSystem.AddFaction(ent, FactionPsionic);
    }

    private void OnRemove(EntityUid uid, PsionicComponent component, ComponentRemove args)
    {
        _npcFactonSystem.RemoveFaction(uid, FactionPsionic);
    }

    public void RollPsionics(Entity<PotentialPsionicComponent> ent, bool applyGlimmer = true,
        float multiplier = 1f)
    {
        if (HasComp<PsionicComponent>(ent))
            return;

        if (!_cfg.GetCVar(CCVars.PsionicRollsEnabled))
            return;

        var chance = ent.Comp.Chance;
        var warn = false;
        if (TryComp<PsionicBonusChanceComponent>(ent, out var bonus))
        {
            chance += bonus.FlatBonus;
            chance *= bonus.Multiplier;
            warn = bonus.Warn;
        }

        if (applyGlimmer)
            chance += ((float) _glimmerSystem.Glimmer / 1000);

        chance *= multiplier;

        chance = Math.Clamp(chance, 0, 1);

        if (_random.Prob(chance))
            _psionicAbilitiesSystem.AddPsionics(ent, warn);
    }

    public void RerollPsionics(Entity<PotentialPsionicComponent?> ent, float bonusMuliplier = 1f)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Rerolled)
            return;

        RollPsionics(ent!, multiplier: bonusMuliplier);
        ent.Comp.Rerolled = true;
        Dirty(ent);
    }
}
