using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

/// <summary>
/// Data for <see cref="StatusEffectEssenceRegen"/> on the status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RevenantRegenModifierStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<NetEntity> Witnesses = new();

    [DataField, AutoNetworkedField]
    public int NewHaunts;
}
