using Content.Shared.StatusEffectNew;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// Base system for accents that should apply both directly and when relayed through other entities.
/// </summary>
public abstract class RelayAccentSystem<T> : EntitySystem where T : Component
{
    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<T, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<T, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccent(Entity<T> ent, ref AccentGetEvent args)
    {
        ApplyAccent(args.Entity, ent.Comp, args);
    }

    private void OnAccentRelayed(Entity<T> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        ApplyAccent(args.Args.Entity, ent.Comp, args.Args);
    }

    /// <summary>
    /// Applies the accent to <paramref name="args"/>. Default implementation only transforms the message text.
    /// Override when the accent needs to mutate other <see cref="AccentGetEvent"/> fields.
    /// </summary>
    protected virtual void ApplyAccent(EntityUid speaker, T comp, AccentGetEvent args)
    {
        args.Message = AccentuateInternal(speaker, comp, args.Message);
    }

    /// <summary>
    /// Transforms accented speech text.
    /// </summary>
    protected abstract string AccentuateInternal(EntityUid uid, T comp, string message);
}
