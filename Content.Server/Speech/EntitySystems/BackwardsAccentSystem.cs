using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class BackwardsAccentSystem : EntitySystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<BackwardsAccentComponent, AccentGetEvent>(OnAccent);
            SubscribeLocalEvent<BackwardsAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
        }

        public string Accentuate(string message)
        {
            var arr = message.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        private void OnAccentRelayed(Entity<BackwardsAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        {
            args.Args.Message = Accentuate(args.Args.Message);
        }

        private void OnAccent(Entity<BackwardsAccentComponent> ent, ref AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
