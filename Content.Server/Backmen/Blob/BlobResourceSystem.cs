using Content.Server.Backmen.Blob.Components;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;

namespace Content.Server.Backmen.Blob;

public sealed class BlobResourceSystem : EntitySystem
{
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    private EntityQuery<BlobTileComponent> _blobTile;
    private EntityQuery<BlobCoreComponent> _blobCore;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobResourceComponent, BlobSpecialGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobResourceComponent, BlobNodePulseEvent>(OnPulsed);

        _blobTile = GetEntityQuery<BlobTileComponent>();
        _blobCore = GetEntityQuery<BlobCoreComponent>();
    }

    private void OnPulsed<T>(EntityUid uid, BlobResourceComponent component, T args)
    {
        if (!_blobTile.TryComp(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        if (!_blobCore.TryComp(blobTileComponent.Core, out var blobCoreComponent) ||
            blobCoreComponent.Observer == null)
            return;

        _popup.PopupEntity(Loc.GetString("blob-get-resource", ("point", component.PointsPerPulsed)),
            uid,
            blobCoreComponent.Observer.Value,
            PopupType.LargeGreen);

        var points = component.PointsPerPulsed;

        if (blobCoreComponent.CurrentChem == BlobChemType.RegenerativeMateria)
        {
            points += 1;
        }

        if (_blobCoreSystem.ChangeBlobPoint(blobTileComponent.Core.Value, points))
        {
            _popup.PopupClient(Loc.GetString("blob-get-resource", ("point", points)),
                uid,
                blobCoreComponent.Observer.Value,
                PopupType.LargeGreen);
        }
    }
}
