using Content.Shared.Body.Events;
using Content.Shared.Humanoid;
using Content.Shared.Medical;
using JetBrains.Annotations;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    private void InitializeRelay()
    {
        // start-backmen: body
        SubscribeLocalEvent<BodyComponent, ApplyMetabolicMultiplierEvent>(OnRelayApplyMetabolicMultiplier);
        // end-backmen: body
        SubscribeLocalEvent<BodyComponent, TryVomitEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, Gibbing.BeingGibbedEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, ApplyOrganProfileDataEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, ApplyOrganMarkingsEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, OrganCopyAppearanceEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, HumanoidLayerVisibilityChangedEvent>(RefRelayBodyEvent);
    }

    // start-backmen: body
    private void OnRelayApplyMetabolicMultiplier(Entity<BodyComponent> ent, ref ApplyMetabolicMultiplierEvent args)
    {
        RelayEvent(ent, ref args);
        RelayApplyMetabolicMultiplierToHierarchy(ent, ref args);
    }

    partial void RelayApplyMetabolicMultiplierToHierarchy(Entity<BodyComponent> ent, ref ApplyMetabolicMultiplierEvent args);
    // end-backmen: body
    private void RefRelayBodyEvent<T>(EntityUid uid, BodyComponent component, ref T args) where T : struct
    {
        RelayEvent((uid, component), ref args);
    }

    private void RelayBodyEvent<T>(EntityUid uid, BodyComponent component, T args) where T : class
    {
        RelayEvent((uid, component), args);
    }

    /// <summary>
    /// Relays the given event to organs within a body.
    /// </summary>
    /// <param name="ent">The body to relay the event within</param>
    /// <param name="args">The event to relay</param>
    /// <typeparam name="T">The type of the event</typeparam>
    [PublicAPI]
    public void RelayEvent<T>(Entity<BodyComponent> ent, ref T args) where T : struct
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
        args = ev.Args;
    }

    /// <summary>
    /// Relays the given event to organs within a body.
    /// </summary>
    /// <param name="ent">The body to relay the event within</param>
    /// <param name="args">The event to relay</param>
    /// <typeparam name="T">The type of the event</typeparam>
    [PublicAPI]
    public void RelayEvent<T>(Entity<BodyComponent> ent, T args) where T : class
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
    }
}

/// <summary>
/// Event wrapper for events being relayed to organs within a body.
/// </summary>
[ByRefEvent]
public record struct BodyRelayedEvent<TEvent>(Entity<BodyComponent> Body, TEvent Args);
