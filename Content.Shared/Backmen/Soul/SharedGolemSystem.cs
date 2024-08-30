using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Backmen.Soul;

public abstract class SharedGolemSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();

        // I can think of better ways to handle this, but they require API changes upstream.
        SubscribeLocalEvent<GolemComponent, ShotAttemptedEvent>(OnAttemptShoot);
        SubscribeLocalEvent<SoulCrystalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<SoulCrystalComponent> ent, ref ExaminedEvent args)
    {
        if(!args.IsInDetailsRange)
            return;
        if(!_pointLight.TryGetLight(ent, out var light))
            return;
        args.PushText(light.Enabled ? Loc.GetString("golem-soul-have") : Loc.GetString("golem-soul-no-have"));
    }

    private void OnAttemptShoot(EntityUid uid, GolemComponent component, ref ShotAttemptedEvent args)
    {
        Popup.PopupClient(Loc.GetString("golem-no-using-guns-popup"), uid, uid);
        args.Cancel();
    }
}
