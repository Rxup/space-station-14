using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlobObserverComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsProcessingMoveEvent;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Core = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool CanMove = true;

    [ViewVariables(VVAccess.ReadOnly)]
    public BlobChemType SelectedChemId = BlobChemType.ReactiveSpines;

    [DataField("actionHelpBlob")]
    public EntityUid? ActionHelpBlob = null;
    [DataField("actionSwapBlobChem")]
    public EntityUid? ActionSwapBlobChem = null;
    [DataField("actionTeleportBlobToCore")]
    public EntityUid? ActionTeleportBlobToCore = null;
    [DataField("actionTeleportBlobToNode")]
    public EntityUid? ActionTeleportBlobToNode = null;
    [DataField("actionCreateBlobFactory")]
    public EntityUid? ActionCreateBlobFactory = null;
    [DataField("actionCreateBlobResource")]
    public EntityUid? ActionCreateBlobResource = null;
    [DataField("actionCreateBlobNode")]
    public EntityUid? ActionCreateBlobNode = null;
    [DataField("actionCreateBlobbernaut")]
    public EntityUid? ActionCreateBlobbernaut = null;
    [DataField("actionSplitBlobCore")]
    public EntityUid? ActionSplitBlobCore = null;
    [DataField("actionSwapBlobCore")]
    public EntityUid? ActionSwapBlobCore = null;
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapComponentState : ComponentState
{
    public BlobChemType SelectedChem;
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly Dictionary<BlobChemType, Color> ChemList;
    public readonly BlobChemType SelectedChem;

    public BlobChemSwapBoundUserInterfaceState(Dictionary<BlobChemType, Color> chemList, BlobChemType selectedId)
    {
        ChemList = chemList;
        SelectedChem = selectedId;
    }
}

[Serializable, NetSerializable]
public sealed class BlobChemSwapPrototypeSelectedMessage : BoundUserInterfaceMessage
{
    public readonly BlobChemType SelectedId;

    public BlobChemSwapPrototypeSelectedMessage(BlobChemType selectedId)
    {
        SelectedId = selectedId;
    }
}

[Serializable, NetSerializable]
public enum BlobChemSwapUiKey : byte
{
    Key
}


public sealed partial class BlobCreateFactoryActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobCreateResourceActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobCreateNodeActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobCreateBlobbernautActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobSplitCoreActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobSwapCoreActionEvent : WorldTargetActionEvent
{

}

public sealed partial class BlobToCoreActionEvent : InstantActionEvent
{

}

public sealed partial class BlobToNodeActionEvent : InstantActionEvent
{

}

public sealed partial class BlobHelpActionEvent : InstantActionEvent
{

}

public sealed partial class BlobSwapChemActionEvent : InstantActionEvent
{

}

