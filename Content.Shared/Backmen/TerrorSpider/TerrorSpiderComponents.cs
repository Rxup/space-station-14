using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.TerrorSpider;

/// <summary>
/// Tracks the number of web collisions for an entity, used for stealth calculations.
/// </summary>
[RegisterComponent]
public sealed partial class StealthOnWebComponent : Component
{
    /// <summary>
    /// The number of spider webs this entity is currently colliding with.
    /// </summary>
    [DataField]
    public int Collisions = 0;
}

/// <summary>
/// Component indicating an entity is currently holding a terror spider egg.
/// Tracks progress towards hatching.
/// </summary>
[RegisterComponent]
public sealed partial class EggHolderComponent : Component
{
    /// <summary>
    /// Counter representing the time until the egg hatches.
    /// </summary>
    [DataField]
    public int Counter = 0;
}

/// <summary>
/// Marker component indicating an entity has an egg implanted.
/// </summary>
[RegisterComponent]
public sealed partial class HasEggHolderComponent : Component { }

/// <summary>
/// Component indicating the entity is a Terror Spider Princess.
/// </summary>
[RegisterComponent]
public sealed partial class TerrorPrincessComponent : Component { }

/// <summary>
/// Action to inject a terror spider egg into target entity.
/// </summary>
public sealed partial class EggInjectionEvent : EntityTargetActionEvent { }

/// <summary>
/// DoAfter event for the egg injection action.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class EggInjectionDoAfterEvent : SimpleDoAfterEvent { }

/// <summary>
/// UiKey for egg laying user interface.
/// </summary>
[Serializable, NetSerializable]
public enum EggsLayingUiKey : byte
{
    Key
}

/// <summary>
/// Action to trigger the egg laying user interface for a Terror Princess.
/// </summary>
public sealed partial class EggsLayingEvent : InstantActionEvent { }

/// <summary>
/// Message sent from the client to the server when an egg is chosen in the egg laying UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class EggsLayingBuiMsg : BoundUserInterfaceMessage
{
    public EntProtoId Egg { get; set; }
}

/// <summary>
/// State data for the egg laying user interface.
/// </summary>
[Serializable, NetSerializable]
public sealed class EggsLayingBuiState : BoundUserInterfaceState { }
