using Content.Shared.Popups;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Impstation.Revenant.EntitySystems;

public abstract partial class SharedRevealRevenantOnCollideSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevealRevenantOnCollideComponent, StartCollideEvent>(OnCollideStart);
    }

    private void OnCollideStart(EntityUid uid, RevealRevenantOnCollideComponent comp, StartCollideEvent args)
    {
        if (!HasComp<RevenantComponent>(args.OtherEntity))
            return;

        if (!string.IsNullOrEmpty(comp.PopupText)
            && !_status.HasStatusEffect(args.OtherEntity, RevenantStatusEffects.Corporeal))
        {
            _popup.PopupClient(
                Loc.GetString(comp.PopupText, ("revealer", uid), ("revenant", args.OtherEntity)),
                args.OtherEntity,
                args.OtherEntity);
        }

        _status.TryAddStatusEffectDuration(args.OtherEntity, RevenantStatusEffects.Corporeal, comp.RevealTime);

        if (comp.StunTime != null)
            _stun.TryAddStunDuration(args.OtherEntity, comp.StunTime.Value);
    }
}
