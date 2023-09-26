﻿using Content.Shared.Backmen.Laundry;
using Content.Shared.Destructible;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Laundry;

public sealed class LaundrySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedWashingMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SharedWashingMachineComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<SharedWashingMachineComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<SharedWashingMachineComponent, EntRemovedFromContainerMessage>(OnContainerModified);

    }

    private void OnMapInit(EntityUid uid, SharedWashingMachineComponent component, MapInitEvent args)
    {
        if (!_containerSystem.TryGetContainer(uid, "storagebase", out var container))
            return;

        _appearanceSystem.SetData(uid, StorageVisuals.HasContents, container.ContainedEntities.Count > 0);
    }

    private void OnBreak(EntityUid uid, SharedWashingMachineComponent component, BreakageEventArgs args)
    {
        _appearanceSystem.SetData(uid, WashingMachineVisualState.Broken, true);
    }

    private void OnContainerModified(EntityUid uid, SharedWashingMachineComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == "storagebase")
            _appearanceSystem.SetData(uid, StorageVisuals.HasContents, args.Container.ContainedEntities.Count > 0);
    }
}
