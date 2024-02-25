using System.Linq;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Backmen.Reinforcement;
using Content.Shared.Backmen.Reinforcement.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Reinforcement;

public sealed class ReinforcementSystem : SharedReinforcementSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ReinforcementConsoleComponent>(ReinforcementConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<ChangeReinforcementMsg>(OnKeySelected);
            subs.Event<BriefReinforcementUpdate>(OnBriefUpdate);
            subs.Event<CallReinforcementStart>(OnStartCall);
        });
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string Spawner = "ReinforcementSpawner";

    private void OnStartCall(Entity<ReinforcementConsoleComponent> ent, ref CallReinforcementStart args)
    {
        if (ent.Comp.IsActive)
        {
            return;
        }

        if (ent.Comp.Members.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-list"),ent, args.Session, PopupType.LargeCaution);
            return;
        }
        if (ent.Comp.Brief.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-brief"),ent, args.Session, PopupType.LargeCaution);
            return;
        }

        ent.Comp.IsActive = true;

        // todo: spawn new ghost roles and forward to station spawn with record and roles
        //throw new NotImplementedException();

        UpdateUserInterface(ent);
    }

    private void OnBriefUpdate(Entity<ReinforcementConsoleComponent> ent, ref BriefReinforcementUpdate args)
    {
        if (ent.Comp.IsActive)
        {
            return;
        }

        ent.Comp.Brief = args.Brief ?? string.Empty;
        //UpdateUserInterface(ent);
    }

    private void OnKeySelected(Entity<ReinforcementConsoleComponent> ent, ref ChangeReinforcementMsg args)
    {
        if (ent.Comp.IsActive || args.Id == null)
        {
            return;
        }
        var id = args.Id.Value;

        var cur = ent.Comp.Members.Where(x => x.Id == id).ToArray();
        if (cur.Length == args.Count)
        {
            return; // no changes?
        }

        if (cur.Length > args.Count) // minus
        {
            foreach (var rowRecord in cur.Take((int)(cur.Length - args.Count)))
            {
                ent.Comp.Members.Remove(rowRecord);
            }
        }
        else //plus
        {
            for (var i = 0; i < (args.Count-cur.Length); i++)
            {
                ent.Comp.Members.Add(new ReinforcementRowRecord()
                {
                    Id = id
                });
            }
        }
        //UpdateUserInterface(ent);
    }

    private void UpdateUserInterface<T>(Entity<ReinforcementConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<ReinforcementConsoleComponent> uid)
    {
        var msg = new UpdateReinforcementUi();
        msg.Brief = uid.Comp.Brief;
        msg.IsActive = uid.Comp.IsActive;

        if (TryComp<MetaDataComponent>(uid.Comp.CalledBy, out var calledBy))
        {
            msg.CalledBy = calledBy.EntityName;
        }

        foreach (var member in uid.Comp.Members)
        {
            if (member.Owner.Valid)
            {
                var state = TryComp<MobStateComponent>(member.Owner, out var mobState) ? mobState.CurrentState : MobState.Dead;
                msg.Members.Add((member.Id, GetNetEntity(member.Owner), member.Name, state));
            }
            else
            {
                msg.Members.Add((member.Id, NetEntity.Invalid, member.Name, MobState.Invalid));
            }
        }

        _ui.TrySetUiState(uid, ReinforcementConsoleKey.Key, msg);
    }
}

