using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Server.ADT.Mech.Equipment.Components;

/// <summary>
/// Дрель меха. Позволяет эффективно копать руду.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechDrillComponent : Component
{
    /// <summary>
    /// Стоимость единичного использования дрели.
    /// </summary>
    [DataField("drillEnergyDelta")]
    public float DrillEnergyDelta = -10;

    /// <summary>
    /// Урон, наносимый цели.
    /// </summary>
    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier DamageToDrilled = default!;

    /// <summary>
    /// Модификатор скорости для использования дрели не по руде.
    /// </summary>
    [DataField("drillSpeedMultilire")]
    public float DrillSpeedMultilire = 50f;

    /// <summary>
    /// Звук дрели
    /// </summary>
    [DataField("drillSound")]
    public SoundSpecifier DrillSound = new SoundPathSpecifier("/Audio/ADT/Mecha/mecha_drill.ogg");

    public AudioComponent? AudioStream;

    public CancellationTokenSource? Token;
}

