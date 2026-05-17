namespace Content.Shared.Backmen.Abilities.Psionics.Events;

public readonly struct PsiActionToggleEvent
{
    public EntityUid Owner { get; }
    public bool Toggle { get; }

    public PsiActionToggleEvent(EntityUid owner, bool toggle)
    {
        Owner = owner;
        Toggle = toggle;
    }
}
