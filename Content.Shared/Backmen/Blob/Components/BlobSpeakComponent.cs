﻿using Content.Shared.Backmen.Language;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobSpeakComponent : Component
{
    [DataField]
    public ProtoId<LanguagePrototype> Language = "Blob";

    [DataField, AutoNetworkedField]
    public ProtoId<RadioChannelPrototype> Channel = "Hivemind";

    /// <summary>
    /// Hide entity name
    /// </summary>
    [DataField]
    public bool OverrideName = true;

    [DataField]
    public LocId Name = "speak-vv-blob";
}
