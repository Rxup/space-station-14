using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(false)]
public sealed partial class BlobObserverComponent : Component, IAntagStatusIconComponent
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsProcessingMoveEvent;

    [ViewVariables(VVAccess.ReadOnly),AutoNetworkedField]
    public EntityUid? Core = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool CanMove = true;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public BlobChemType SelectedChemId = BlobChemType.ReactiveSpines;

    public override bool SendOnlyToOwner => true;

    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "BlobFaction";
    public bool IconVisibleToGhost { get; set; } = true;
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

