using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Traitor.Uplink.SurplusBundle;

/// <summary>
///     Fill crate with a random wizard items.
/// </summary>
[RegisterComponent]
public sealed partial class WizardSurplusBundleComponent : Component
{
    /// <summary>
    ///     Total price of all content inside bundle.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("totalPrice")]
    public int TotalPrice = 20;

    /// <summary>
    ///     The preset that will be used to get all the listings.
    ///     Currently just defaults to the basic uplink.
    /// </summary>
    [DataField("storePreset")]
    public ProtoId<StorePresetPrototype> StorePreset = "WizardStorePresetUplink";
}
