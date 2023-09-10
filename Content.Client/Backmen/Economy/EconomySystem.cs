using Content.Shared.Backmen.Economy;
using Robust.Shared.GameStates;

namespace Content.Client.Backmen.Economy;

public sealed class EconomySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankAccountComponent, ComponentHandleState>(OnBalanceChange);
    }

    private void OnBalanceChange(EntityUid uid, BankAccountComponent component, ref ComponentHandleState args)
    {
        if (args.Next is not BankAccountStateComponent state)
        {
            return;
        }

        component.Balance = state.Balance;
    }
}
