using Content.Shared.Backmen.Tools.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Tools.Components;

namespace Content.Shared.Backmen.Tools;

/// <summary>
/// Blocks tool and anchor interactions on structures that must not be modified in the field.
/// </summary>
public sealed class DisableToolUseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DisableToolUseComponent, ToolUseAttemptEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<DisableToolUseComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<DisableToolUseComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
    }

    private void OnToolUseAttempt(Entity<DisableToolUseComponent> ent, ref ToolUseAttemptEvent args)
    {
        if (HasAnyRestriction(ent.Comp))
            args.Cancel();
    }

    private void OnAnchorAttempt(Entity<DisableToolUseComponent> ent, ref AnchorAttemptEvent args)
    {
        if (ent.Comp.Anchoring)
            args.Cancel();
    }

    private void OnUnanchorAttempt(Entity<DisableToolUseComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (ent.Comp.Anchoring)
            args.Cancel();
    }

    private static bool HasAnyRestriction(DisableToolUseComponent component)
    {
        return component.Anchoring
               || component.Prying
               || component.Screwing
               || component.Cutting
               || component.Welding
               || component.Pulsing
               || component.Slicing
               || component.Sawing
               || component.Honking
               || component.Rolling
               || component.Digging;
    }
}
