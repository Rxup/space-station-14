using Content.Server.Body.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Content.Server.Popups;
using Robust.Shared.Physics.Events;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.GibOnCollide;

public sealed class GibOnCollideSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GibOnCollideComponent, StartCollideEvent>(OnStartCollide);
    }

    [ValidatePrototypeId<DamageContainerPrototype>]
    private const string BiologicalDamageContainerPrototype = "Biological";

    private void OnStartCollide(EntityUid uid, GibOnCollideComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (_gameTiming.CurTime < component.LastGibTime + component.GibCooldown)
            return;


        if (component.GibOnlyAlive
            || !TryComp<MobStateComponent>(otherUid, out var mobState)
            || !_mobStateSystem.IsAlive(otherUid, mobState)
            || !TryComp<DamageableComponent>(otherUid, out var damageable)
            || damageable.DamageContainerID != BiologicalDamageContainerPrototype)
        {
            return;
        }

        if (!TryComp<BodyComponent>(otherUid, out var body))
        {
            return;
        }

        _body.GibBody(otherUid, body: body, gibOrgans: false);

        _audioSystem.PlayPvs(component.GibSound, uid);

        if (!string.IsNullOrEmpty(component.GibMessage))
        {
            _popupSystem.PopupEntity(component.GibMessage, otherUid, PopupType.Large);
        }

        component.LastGibTime = _gameTiming.CurTime;

        RaiseLocalEvent(otherUid, new GibOnCollideAttemptEvent(otherUid, uid));
        RaiseLocalEvent(uid, new GibOnCollideAttemptEvent(otherUid, uid));
    }
}
