using Robust.Shared.Audio.Systems;
using Content.Server.Mech.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Throwing;
using Content.Shared.ADT.Mech.Equipment.Components;


namespace Content.Server.ADT.Mech.Equipment.EntitySystems;

/// <summary>
/// Handles everything for mech catapult.
/// </summary>
public sealed class MechCatapultSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechCatapultComponent, AfterInteractUsingEvent>(OnInteract);
    }

    private void OnInteract(EntityUid uid, MechCatapultComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MechComponent>(args.User, out var mech))
            return;
        if (!_mech.TryChangeEnergy(args.User, -50, mech))
            return;
        _throwing.TryThrow(args.User, args.ClickLocation, 4f);
        _audio.PlayPvs("/Audio/Mecha/mech_shield_deflect.ogg", uid);
        args.Handled = true;
    }
}
