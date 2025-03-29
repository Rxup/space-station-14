using Robust.Shared.Serialization;

namespace Content.Shared.Eye
{
    [Flags]
    [FlagsFor(typeof(VisibilityMaskLayer))]
    public enum VisibilityFlags : int
    {
        None   = 0,
        Normal = 1 << 0,
        Ghost  = 1 << 1,
        Subfloor = 1 << 2,
        PsionicInvisibility = 1 << 3, // backmen: psionic,
        DarkSwapInvisibility = 1 << 4, // backmen: shadowkin
        
    }
}
