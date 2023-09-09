﻿using System.Linq;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Backmen.Psionics;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

/// <summary>
/// Forces a mind swap on all non-insulated potential psionic entities.
/// </summary>
internal sealed class MassMindSwapRule : StationEventSystem<MassMindSwapRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;

    protected override void Started(EntityUid uid, MassMindSwapRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> psionicPool = new();
        List<EntityUid> psionicActors = new();

        var query = EntityQueryEnumerator<PotentialPsionicComponent, MobStateComponent>();
        while (query.MoveNext(out var psion, out _, out _))
        {
            if (_mobStateSystem.IsAlive(psion) && !HasComp<PsionicInsulationComponent>(psion))
            {
                psionicPool.Add(psion);

                if (HasComp<ActorComponent>(psion))
                {
                    // This is so we don't bother mindswapping NPCs with NPCs.
                    psionicActors.Add(psion);
                }
            }
        }

        // Shuffle the list of candidates.
        _random.Shuffle(psionicPool);

        var q1 = new Queue<EntityUid>(psionicPool.ToList());

        while (q1.TryDequeue(out var actor))
        {
            if (HasComp<MindSwappedComponent>(actor))
            {
                psionicPool.Remove(actor);
                continue;
            }
            var q2 = new Queue<EntityUid>(psionicPool.ToList());
            while (q2.TryDequeue(out var other))
            {
                if (!_mindSwap.Swap(actor, other))
                {
                    continue;
                }
                if (!component.IsTemporary)
                {
                    _mindSwap.GetTrapped(actor);
                    _mindSwap.GetTrapped(other);
                }
                psionicPool.Remove(actor);
                psionicPool.Remove(other);
                break;
            }
        }
    }
}
