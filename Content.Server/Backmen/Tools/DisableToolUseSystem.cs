using Content.Server.Backmen.Tools.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Tools.Components;

namespace Content.Server.Backmen.Tools;

public sealed class DisableToolUseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DisableToolUseComponent, ToolUseAttemptEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<DisableToolUseComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<DisableToolUseComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
    }

    private void OnToolUseAttempt(EntityUid uid, DisableToolUseComponent component, ToolUseAttemptEvent args)
    {
        if (HasAnyRestriction(component))
            args.Cancel();
    }

    private void OnAnchorAttempt(EntityUid uid, DisableToolUseComponent component, AnchorAttemptEvent args)
    {
        if (component.Anchoring)
            args.Cancel();
    }

    private void OnUnanchorAttempt(EntityUid uid, DisableToolUseComponent component, UnanchorAttemptEvent args)
    {
        if (component.Anchoring)
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
