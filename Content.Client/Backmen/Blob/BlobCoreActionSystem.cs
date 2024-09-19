using System.Numerics;
using Content.Client.Weapons.Melee;
using Content.Shared.Backmen.Blob;
using Robust.Client.Audio;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen;

public sealed class BlobCoreActionSystem : SharedBlobCoreActionSystem
{
    [Dependency] private readonly MeleeWeaponSystem _meleeWeaponSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BlobAttackEvent>(OnBlobAttack);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string Animation = "WeaponArcPunch";

    private void OnBlobAttack(BlobAttackEvent ev)
    {
        var user = GetEntity(ev.BlobEntity);
        if(!user.IsValid())
            return;

        _meleeWeaponSystem.DoLunge(user, user, Angle.Zero, ev.Position, Animation, false);
    }
}
