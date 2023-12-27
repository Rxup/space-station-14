using Content.Server.Chat.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Backmen.Speech.RandomBark;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Speech.RandomBark
{
    public sealed class RandomBarkSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly EntityManager _entities = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RandomBarkComponent, ComponentInit>(OnInit);
        }

        public void OnInit(EntityUid uid, RandomBarkComponent barker, ComponentInit args)
        {
            barker.BarkAccumulator = _random.NextFloat(barker.MinTime, barker.MaxTime)*barker.BarkMultiplier;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var barker in EntityQuery<RandomBarkComponent>())
            {
                barker.BarkAccumulator -= frameTime;
                if (barker.BarkAccumulator <= 0)
                {
                    barker.BarkAccumulator = _random.NextFloat(barker.MinTime, barker.MaxTime)*barker.BarkMultiplier;
                    if (_entities.TryGetComponent<MindContainerComponent>(barker.Owner, out var actComp))
                    {
                        if (actComp.HasMind)
                        {
                            return;
                        }
                    }

                    _chat.TrySendInGameICMessage(barker.Owner, _random.Pick(barker.Barks), InGameICChatType.Speak, barker.Chatlog ? ChatTransmitRange.Normal : ChatTransmitRange.HideChat);
                }
            }
        }
    }
}
