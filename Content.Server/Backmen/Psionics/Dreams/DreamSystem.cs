using Content.Shared.Dataset;
using Content.Shared.Bed.Sleep;
using Content.Server.Chat.Managers;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Psionics.Dreams
{
    public sealed class DreamsSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        private float _accumulator = 0f;
        private float _updateRate = 15f;

        public readonly IReadOnlyList<string> DreamSetPrototypes = new[]
        {
            "adjectives",
            "names_first",
            "verbs",
        };

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            _accumulator += frameTime;
            if (_accumulator < _updateRate)
                return;

            _accumulator -= _updateRate;
            _updateRate = _random.NextFloat(10f, 30f);

            var q = EntityQueryEnumerator<SleepingComponent, ActorComponent>();
            while (q.MoveNext(out var owner, out var sleeper, out var actor))
            {
                var setName = _random.Pick(DreamSetPrototypes);

                if (!_prototypeManager.TryIndex<DatasetPrototype>(setName, out var set))
                    return;

                var msg = _random.Pick(set.Values) + "..."; //todo... does the seperator need loc?

                var messageWrap = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
                    ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")), ("message", msg));

                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Telepathic,
                msg, messageWrap, owner, false, actor.PlayerSession.Channel, Color.PaleVioletRed);
            }
        }
    }
}
