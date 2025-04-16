using Content.Server.Body.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Content.Server.Popups;
using Robust.Shared.Physics.Events;
using Content.Shared.Body.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.GibOnCollide;

public sealed class GibOnCollideSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly BodySystem _body = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GibOnCollideComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, GibOnCollideComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (_gameTiming.CurTime < component.LastGibTime + component.GibCooldown)
            return;

        BodyComponent? body = null;

        if (component.GibOnlyAlive)
        {
            if (!TryComp<MobStateComponent>(otherUid, out var mobState) ||
                mobState.CurrentState == MobState.Dead ||
                !TryComp<BodyComponent>(otherUid, out body))
            {
                return;
            }
        }
        else
        {
            if (!TryComp<BodyComponent>(otherUid, out body))
            {
                return;
            }
        }

        if (body != null)
        {
            _body.GibBody(otherUid, false);

            _audioSystem.PlayPvs(component.GibSound, uid);

            if (!string.IsNullOrEmpty(component.GibMessage))
            {
                _popupSystem.PopupEntity(component.GibMessage, otherUid, PopupType.Large);
            }

            component.LastGibTime = _gameTiming.CurTime;

            RaiseLocalEvent(otherUid, new GibOnCollideAttemptEvent(otherUid, uid));
        }
    }
}
