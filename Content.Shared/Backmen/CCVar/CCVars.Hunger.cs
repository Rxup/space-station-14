using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

public sealed partial class CCVars
{
    /*
     * Hunger CVars
     */

    /// <summary>
    /// Maximum pain that starvation alone can apply.
    /// Set below PainCap (250) in server config so hunger by itself is not lethal.
    /// 0 disables the CVar cap and uses the per-entity HungerComponent value.
    /// </summary>
    public static readonly CVarDef<float> HungerStarvingPainMax =
        CVarDef.Create("hunger.starving_pain_max", 250f, CVar.SERVER | CVar.REPLICATED);
}
