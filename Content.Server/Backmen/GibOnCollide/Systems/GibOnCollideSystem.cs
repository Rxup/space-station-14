using Content.Server.Backmen.Body.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Content.Server.Popups;
using Robust.Shared.Physics.Events;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.GibOnCollide;

public sealed partial class GibOnCollideSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private BkmBodySystem _body = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GibOnCollideComponent, StartCollideEvent>(OnStartCollide);
    }

    private static readonly ProtoId<DamageContainerPrototype> BiologicalDamageContainerPrototype = "Biological";

    private void OnStartCollide(EntityUid uid, GibOnCollideComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (_gameTiming.CurTime < component.LastGibTime + component.GibCooldown)
            return;


        if (component.GibOnlyAlive)
        {
            if (!TryComp<MobStateComponent>(otherUid, out var mobState)
                || !_mobStateSystem.IsAlive(otherUid, mobState))
                return;

            if (!TryComp<InjurableComponent>(otherUid, out var injurable)
                || injurable.DamageContainer?.Id != BiologicalDamageContainerPrototype.Id)
                return;
        }

        if (!TryComp<BodyComponent>(otherUid, out var body))
            return;

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
