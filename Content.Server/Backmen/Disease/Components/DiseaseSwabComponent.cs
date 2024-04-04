﻿using Content.Shared.Backmen.Disease;

namespace Content.Server.Backmen.Disease.Components;

/// <summary>
/// For mouth swabs used to collect and process
/// disease samples.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseSwabComponent : Component
{
    /// <summary>
    /// How long it takes to swab someone.
    /// </summary>
    [DataField("swabDelay")]
    public float SwabDelay = 2f;
    /// <summary>
    /// If this swab has been used
    /// </summary>
    public bool Used = false;
    /// <summary>
    /// The disease prototype currently on the swab
    /// </summary>
    [ViewVariables]
    public DiseasePrototype? Disease;
}
