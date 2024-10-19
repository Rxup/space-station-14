using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Item;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Telescope;

public abstract class SharedTelescopeSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<EyeOffsetChangedEvent>(OnEyeOffsetChanged);
        SubscribeLocalEvent<TelescopeComponent, GotUnequippedHandEvent>(OnUnequip);
        SubscribeLocalEvent<TelescopeComponent, HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<TelescopeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<TelescopeComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent.Comp.LastEntity, out EyeComponent? eye) || ent.Comp.LastEntity == ent && TerminatingOrDeleted(ent))
            return;

        SetOffset((ent.Comp.LastEntity.Value, eye), Vector2.Zero, ent);
    }

    private void OnHandDeselected(Entity<TelescopeComponent> ent, ref HandDeselectedEvent args)
    {
        if (!TryComp(args.User, out EyeComponent? eye))
            return;

        SetOffset((args.User, eye), Vector2.Zero, ent);
    }

    private void OnUnequip(Entity<TelescopeComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!TryComp(args.User, out EyeComponent? eye))
            return;

        if (!HasComp<ItemComponent>(ent.Owner))
            return;

        SetOffset((args.User, eye), Vector2.Zero, ent);
    }

    public TelescopeComponent? GetRightTelescope(EntityUid? ent)
    {
        TelescopeComponent? telescope = null;

        if (TryComp<HandsComponent>(ent, out var hands) &&
            hands.ActiveHandEntity.HasValue &&
            TryComp<TelescopeComponent>(hands.ActiveHandEntity, out var handTelescope))
        {
            telescope = handTelescope;
        }
        else if (TryComp<TelescopeComponent>(ent, out var entityTelescope))
        {
            telescope = entityTelescope;
        }

        return telescope;
    }

    private void OnEyeOffsetChanged(EyeOffsetChangedEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ent)
            return;

        if (!TryComp(ent, out EyeComponent? eye))
            return;

        var telescope = GetRightTelescope(ent);

        if (telescope == null)
            return;

        SetOffset((ent, eye), msg.Offset, telescope);
    }

    private void SetOffset(Entity<EyeComponent> ent, Vector2 msgOffset, TelescopeComponent telescope)
    {
        telescope.LastEntity = ent;
        var offset = Vector2.Lerp(ent.Comp.Offset, msgOffset, telescope.LerpAmount);

        if (TryComp(ent, out CameraRecoilComponent? recoil))
        {
            recoil.BaseOffset = offset;
            if (recoil.CurrentKick != Vector2.Zero)
            {
                _eye.SetOffset(ent, msgOffset + recoil.CurrentKick, ent);
                return;
            }
            _eye.SetOffset(ent, offset, ent);
        }
        else
        {
            _eye.SetOffset(ent, offset, ent);
        }
    }

    public void SetParameters(Entity<TelescopeComponent?> ent, float? divisor = null, float? lerpAmount = null)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;

        var telescope = ent.Comp;

        telescope.Divisor = divisor ?? telescope.Divisor;
        telescope.LerpAmount = lerpAmount ?? telescope.LerpAmount;

        Dirty(ent.Owner, telescope);
    }
}

[Serializable, NetSerializable]
public sealed class EyeOffsetChangedEvent : EntityEventArgs
{
    public Vector2 Offset;
}
