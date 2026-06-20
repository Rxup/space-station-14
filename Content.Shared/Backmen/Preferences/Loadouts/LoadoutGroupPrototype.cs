using Content.Shared.Humanoid;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared.Preferences.Loadouts;

public sealed partial class LoadoutGroupPrototype
{
    /// <summary>
    /// If set, <see cref="MinLimit"/> only applies when the character has one of these sexes.
    /// </summary>
    [DataField]
    public List<Sex> MinLimitSexes = new();

    /// <summary>
    /// Returns the effective minimum for the given profile.
    /// </summary>
    public int GetMinLimit(HumanoidCharacterProfile profile)
    {
        if (MinLimitSexes.Count == 0)
            return MinLimit;

        return MinLimitSexes.Contains(profile.Sex) ? MinLimit : 0;
    }
}
