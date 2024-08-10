namespace Content.Server.Backmen.Psionics;

[RegisterComponent]
public sealed partial class PsionicBonusChanceComponent : Component
{
    [DataField("multiplier"), ViewVariables(VVAccess.ReadWrite)]
    public float Multiplier = 1f;
    [DataField("flatBonus"), ViewVariables(VVAccess.ReadWrite)]
    public float FlatBonus = 0;

    /// <summary>
    /// Whether we should warn the user they are about to receive psionics.
    /// It's here because AddComponentSpecial can't overwrite a component, and this is very role dependent.
    /// </summary>
    [DataField("warn")]
    public bool Warn = false;
}
