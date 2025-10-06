using Robust.Shared.Serialization;

namespace Content.Shared.Eye
{
    [Flags]
    [FlagsFor(typeof(VisibilityMaskLayer))]
    public enum VisibilityFlags : int
    {
        None = 0,
        Normal = 1 << 0,
        Ghost = 1 << 1, // Observers and revenants.
        Subfloor = 1 << 2, // Pipes, disposal chutes, cables etc. while hidden under tiles. Can be revealed with a t-ray.
        PsionicInvisibility = 1 << 3, // backmen: psionic,
        DarkSwapInvisibility = 1 << 4, // backmen: shadowkin
        Admin = 1 << 5, // Reserved for admins in stealth mode and admin tools.
    }
}
