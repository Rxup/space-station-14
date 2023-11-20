using Content.Shared.Backmen.Psionics;
using Content.Shared.Eui;
using Content.Server.EUI;
using Content.Server.Backmen.Abilities.Psionics;

namespace Content.Server.Backmen.Psionics
{
    public sealed class AcceptPsionicsEui : BaseEui
    {
        private readonly PsionicAbilitiesSystem _psionicsSystem;
        private readonly EntityUid _entity;

        public AcceptPsionicsEui(EntityUid entity, PsionicAbilitiesSystem psionicsSys)
        {
            _entity = entity;
            _psionicsSystem = psionicsSys;
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            if (msg is not AcceptPsionicsChoiceMessage choice ||
                choice.Button == AcceptPsionicsUiButton.Deny)
            {
                Close();
                return;
            }
            if (!_entity.IsValid())
            {
                Close();
                return;
            }

            _psionicsSystem.AddRandomPsionicPower(_entity);
            Close();
        }
    }
}
