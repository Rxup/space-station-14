using Content.Shared.Actions;
using Content.Server.NPC.Events;
using Content.Server.NPC.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Psionics.NPC
{
    public sealed class PsionicNPCCombatSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NoosphericZapPowerComponent, NPCSteeringEvent>(ZapCombat);
        }

        private void ZapCombat(EntityUid uid, NoosphericZapPowerComponent component, ref NPCSteeringEvent args)
        {
            if (component.NoosphericZapPowerAction?.Event == null)
                return;

            if (component.NoosphericZapPowerAction!.Cooldown.HasValue && component.NoosphericZapPowerAction?.Cooldown.Value.End > _timing.CurTime)
                return;

            if (!TryComp<NPCRangedCombatComponent>(uid, out var combat))
                return;

            if (_actions.ValidateEntityTarget(uid, combat.Target, component.NoosphericZapPowerAction!))
            {
                var ev = component.NoosphericZapPowerAction!.Event;
                ev.Performer = uid;
                ev.Target = combat.Target;

                _actions.PerformAction(uid, null, component.NoosphericZapPowerAction!, ev, _timing.CurTime, false);
            }
        }
    }
}
