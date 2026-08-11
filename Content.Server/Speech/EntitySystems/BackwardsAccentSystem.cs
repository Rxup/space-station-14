using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems
{
    // start-backmen: relay-accents
    public sealed partial class BackwardsAccentSystem : RelayAccentSystem<BackwardsAccentComponent>
    {
        public string Accentuate(string message)
        {
            var arr = message.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        protected override string AccentuateInternal(EntityUid uid, BackwardsAccentComponent comp, string message)
        {
            return Accentuate(message);
        }
    }
    // end-backmen: relay-accents
}
