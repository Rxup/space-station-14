using Content.Server.UserInterface;
using Content.Shared.Backmen.Economy.Eftpos;
using Content.Shared.FixedPoint;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Economy.Eftpos;

    [RegisterComponent]
    [ComponentReference(typeof(SharedEftposComponent))]
    [Access(typeof(EftposSystem))]
    public sealed class EftposComponent : SharedEftposComponent
    {
        [ViewVariables] private BoundUserInterface? UserInterface => Owner.GetUIOrNull(EftposUiKey.Key);

        [ViewVariables] public FixedPoint2? Value { get; set; } = null;
        [ViewVariables] public string? LinkedAccountNumber { get; set; } = null;
        [ViewVariables(VVAccess.ReadOnly)] public string? LinkedAccountName { get; set; } = null;
        [ViewVariables, DataField("canChangeAccountNumber")] public bool CanChangeAccountNumber { get; } = true;
        [ViewVariables] public EntityUid? LockedBy { get; set; } = null;

        [DataField("presetAccountNumber")] private string? _PresetAccountNumber = null;
        [DataField("presetAccountName")] private string? _PresetAccountName = null;

        [ViewVariables(VVAccess.ReadOnly)] public string? CurrencyType { get; set; }
        [DataField("soundApply")]
        // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
        public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/Backmen/Machines/chime.ogg");
        [DataField("soundDeny")]
        // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
        public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Backmen/Machines/buzz-sigh.ogg");

        protected override void Initialize()
        {
            base.Initialize();
            Owner.EnsureComponentWarn<ServerUserInterfaceComponent>();
            InitPresetValues();
        }
        private void InitPresetValues()
        {
            if (_PresetAccountNumber != null)
                LinkedAccountNumber = _PresetAccountNumber;
            if (_PresetAccountName != null)
                LinkedAccountName = _PresetAccountName;
        }
        public void UpdateUserInterface(EftposBoundUserInterfaceState state)
        {
            if (!Initialized || UserInterface == null)
                return;

            UserInterfaceSystem.SetUiState(UserInterface, state);
        }

    }
