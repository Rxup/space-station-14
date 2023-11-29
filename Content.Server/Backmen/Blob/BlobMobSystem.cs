using Content.Server.Backmen.Blob.Fluids.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Backmen.Blob.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Blob;

public sealed class BlobMobSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SmokeSystem _smokeSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMobComponent, BlobMobGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobMobComponent, AttackAttemptEvent>(OnBlobAttackAttempt);
        SubscribeLocalEvent<SmokeOnTriggerComponent, TriggerEvent>(HandleSmokeTrigger);
    }

    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args)
    {
        _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
    }

    private void OnBlobAttackAttempt(EntityUid uid, BlobMobComponent component, AttackAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<BlobTileComponent>(args.Target) && !HasComp<BlobMobComponent>(args.Target))
            return;

        // TODO: Move this to shared
        _popupSystem.PopupCursor(Loc.GetString("blob-mob-attack-blob"), uid, PopupType.Large);
        args.Cancel();
    }


    private void HandleSmokeTrigger(EntityUid uid, SmokeOnTriggerComponent comp, TriggerEvent args)
    {
        var xform = Transform(uid);
        var smokeEnt = Spawn("Smoke", xform.Coordinates);
        var smoke = EnsureComp<SmokeComponent>(smokeEnt);
        var colored = EnsureComp<BlobSmokeColorComponent>(smokeEnt);
        colored.Color = comp.SmokeColor;
        //colored.SmokeColor = comp.SmokeColor;
        Dirty(smokeEnt,smoke);
        var solution = new Solution();
        foreach (var reagent in comp.SmokeReagents)
        {
            solution.AddReagent(reagent.Reagent, reagent.Quantity);
        }
        _smokeSystem.StartSmoke(smokeEnt, solution, comp.Time, comp.SpreadAmount, smoke);
        _audioSystem.PlayPvs(comp.Sound, xform.Coordinates, AudioParams.Default.WithVariation(0.125f));
        args.Handled = true;
    }
}
