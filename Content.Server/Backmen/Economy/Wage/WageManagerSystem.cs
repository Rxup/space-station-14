using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Economy.Wage;

    public sealed class WageManagerSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;
        private static List<Payout> PayoutsList = new();
        public bool WagesEnabled { get; private set; }
        //private void SetEnabled(bool value) => WagesEnabled = value;
        private void SetEnabled(bool value)
        {
            WagesEnabled = value;
        }
        public override void Initialize()
        {
            base.Initialize();
            _configurationManager.OnValueChanged(Shared.Backmen.CCVar.CCVars.EconomyWagesEnabled, SetEnabled, true);
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        }

        private void OnCleanup(RoundRestartCleanupEvent ev)
        {
            PayoutsList.Clear();
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _configurationManager.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.EconomyWagesEnabled, SetEnabled);
        }
        public void Payday()
        {
            foreach (var payout in PayoutsList)
            {
                _bankManagerSystem.TryTransferFromToBankAccount(
                    payout.FromAccountNumber,
                    payout.FromAccountPin,
                    payout.ToAccountNumber,
                    payout.PayoutAmount);
            }
        }
        public bool TryAddAccountToWagePayoutList(BankAccountComponent bankAccount, JobPrototype jobPrototype)
        {
            if (jobPrototype.WageDepartment == null || !_prototypeManager.TryIndex(jobPrototype.WageDepartment, out DepartmentPrototype? department))
                return false;
            if (!_bankManagerSystem.TryGetBankAccount(department.AccountNumber.ToString(), out var departmentBankAccount))
                return false;
            var newPayout = new Payout(
                departmentBankAccount.AccountNumber,
                departmentBankAccount.AccountPin,
                bankAccount.AccountNumber,
                jobPrototype.Wage);
            PayoutsList.Add(newPayout);
            return true;
        }
        private sealed class Payout
        {
            public string FromAccountNumber { get; }
            public string FromAccountPin { get; }
            public string ToAccountNumber { get; }
            public FixedPoint2 PayoutAmount { get; }
            public Payout(string fromAccountNumber, string fromAccountPin, string toAccountNumber, FixedPoint2 payoutAmount)
            {
                FromAccountNumber = fromAccountNumber;
                FromAccountPin = fromAccountPin;
                ToAccountNumber = toAccountNumber;
                PayoutAmount = payoutAmount;
            }
        }
    }
