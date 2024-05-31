using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Uses a <see cref="LoadoutEffectGroupPrototype"/> prototype as a singular effect that can be re-used.
/// </summary>
public sealed partial class GroupLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public ProtoId<LoadoutEffectGroupPrototype> Proto;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, LoadoutPrototype proto, ICommonSession session, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason) // Corvax-Sponsors
    {
        var effectsProto = collection.Resolve<IPrototypeManager>().Index(Proto);

        foreach (var effect in effectsProto.Effects)
        {
            if (!effect.Validate(profile, loadout, proto, session, collection, out reason))
                return false;
        }

        reason = null;
        return true;
    }
}
