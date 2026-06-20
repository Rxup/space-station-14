using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.Shared.Backmen.Arachne;

public static class ArachneClothingStencilVisuals
{
    public const string MaskSprite = "Backmen/Mobs/Customization/anytaur_masking_helpers.rsi";

    public static ArachneClothingStencilState GetState(Sex? sex, ClothingMask mask) =>
        (sex, mask) switch
        {
            (Sex.Male, ClothingMask.NoMask) => ArachneClothingStencilState.MaleNone,
            (Sex.Male, ClothingMask.UniformTop) => ArachneClothingStencilState.MaleTop,
            (Sex.Male, _) => ArachneClothingStencilState.MaleFull,
            (Sex.Female, ClothingMask.NoMask) => ArachneClothingStencilState.FemaleNone,
            (Sex.Female, ClothingMask.UniformTop) => ArachneClothingStencilState.FemaleTop,
            (Sex.Female, _) => ArachneClothingStencilState.FemaleFull,
            (_, ClothingMask.NoMask) => ArachneClothingStencilState.UnisexNone,
            (_, ClothingMask.UniformTop) => ArachneClothingStencilState.UnisexTop,
            _ => ArachneClothingStencilState.UnisexFull,
        };

    public static bool TryGetLayerData(ArachneClothingStencilState state, out PrototypeLayerData data)
    {
        data = default!;

        if (state == ArachneClothingStencilState.Hidden)
        {
            data = new PrototypeLayerData
            {
                RsiPath = MaskSprite,
                Visible = false,
            };
            return true;
        }

        var (prefix, suffix) = state switch
        {
            ArachneClothingStencilState.MaleNone => ("male_", "none"),
            ArachneClothingStencilState.MaleTop => ("male_", "top"),
            ArachneClothingStencilState.MaleFull => ("male_", "full"),
            ArachneClothingStencilState.FemaleNone => ("female_", "none"),
            ArachneClothingStencilState.FemaleTop => ("female_", "top"),
            ArachneClothingStencilState.FemaleFull => ("female_", "full"),
            ArachneClothingStencilState.UnisexNone => ("unsexed_", "none"),
            ArachneClothingStencilState.UnisexTop => ("unsexed_", "top"),
            ArachneClothingStencilState.UnisexFull => ("unsexed_", "full"),
            _ => default,
        };

        if (suffix == null)
            return false;

        data = new PrototypeLayerData
        {
            Shader = "StencilMask",
            RsiPath = MaskSprite,
            State = $"{prefix}{suffix}",
            Visible = true,
        };
        return true;
    }
}
