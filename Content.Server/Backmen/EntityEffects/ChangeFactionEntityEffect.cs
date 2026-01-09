using Content.Server.Backmen.NPC;
using Content.Shared.EntityEffects;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.EntityEffects;

public sealed partial class ChangeFactionEntityEffect : EntityEffect
{
    [DataField(required: true)] public ProtoId<NpcFactionPrototype> NewFaction;

    [DataField] public float Duration = 0f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var cf = args.EntityManager.System<ChangeFactionStatusEffectSystem>();
        cf.TryChangeFaction(args.TargetEntity, NewFaction, out _, Duration);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-faction", ("faction", NewFaction));
}
