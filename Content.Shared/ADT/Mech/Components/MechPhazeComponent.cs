using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Mech.Components;

/// <summary>
/// Added to mech to allow it to phaze
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechPhazeComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? MechPhazeActionEntity;

    [DataField]
    public EntProtoId MechPhazeAction = "ActionMechPhaze";

    /// <summary>
    /// The change in energy per second.
    /// </summary>
    [DataField("energyDelta")]
    public float EnergyDelta = -40; 

    /// <summary>
    /// The sound played when a mech is entered phaze
    /// </summary>
    [DataField("phazingSound")]
    public SoundSpecifier PhazingSound = new SoundPathSpecifier("/Audio/ADT/Mecha/mecha_drill.ogg");

    /// <summary>
    /// Имя спрайта из rsi файла, используемого при фазировании
    /// </summary>
    [DataField]
    public string PhazingState = "phazon-phase";

    /// <summary>
    /// Имя спрайта из rsi файла, используемого при обычном состоянии
    /// </summary>
    [DataField]
    public string State = "phazon";


    [ViewVariables(VVAccess.ReadWrite)]
    public bool Phazed = false;

    public float Accumulator = 0f;
}

