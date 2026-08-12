using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Impstation.Revenant.EntitySystems;

public sealed partial class HauntedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HauntedComponent, ExaminedEvent>(OnHauntedExamined);
        SubscribeLocalEvent<MobStateComponent, ExaminedEvent>(OnMobExamined);

        SubscribeLocalEvent<HauntedStatusEffectComponent, StatusEffectAppliedEvent>(OnHauntedStatusApplied);
        SubscribeLocalEvent<HauntedStatusEffectComponent, StatusEffectRemovedEvent>(OnHauntedStatusRemoved);
    }

    private void OnHauntedStatusApplied(Entity<HauntedStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<HauntedComponent>(args.Target);
    }

    private void OnHauntedStatusRemoved(Entity<HauntedStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<HauntedComponent>(args.Target);
    }

    private void OnHauntedExamined(EntityUid uid, HauntedComponent comp, ExaminedEvent args)
    {
        if (!HasComp<RevenantComponent>(args.Examiner))
            return;

        args.PushMarkup(
            $"[color=mediumpurple]{Loc.GetString("revenant-already-haunted", ("target", uid))}[/color]");
    }

    private void OnMobExamined(EntityUid uid, MobStateComponent comp, ExaminedEvent args)
    {
        if (!HasComp<RevenantComponent>(args.Examiner))
            return;

        if (HasComp<RevenantComponent>(uid) || HasComp<HauntedComponent>(uid))
            return;

        args.PushMarkup(
            $"[color=mediumpurple]{Loc.GetString("revenant-can-haunt", ("target", uid))}[/color]");
    }
}
