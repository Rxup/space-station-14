namespace Content.Server.Backmen.Cloning;

[RegisterComponent]
public sealed partial class MetempsychoticMachineComponent : Component
{
    /// <summary>
    /// Chance you will spawn as a humanoid instead of a non humanoid.
    /// </summary>
    [DataField("humanoidBaseChance")]
    public float HumanoidBaseChance = 0.75f;

    [DataField("karmaBonus")]
    public float KarmaBonus = 0.25f;
}
