using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Server.ADT.Mech.Equipment.Components;

/// <summary>
/// Снаряжение меха, занимающее один слот и меняющее модификаторы
/// урона меха на свои.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechArmorComponent : Component
{
    /// <summary>
    /// Новые модификаторы
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// Здесь хранятся основные модификаторы урона меха
    /// </summary>
    public DamageModifierSet? OriginalModifiers;
}
