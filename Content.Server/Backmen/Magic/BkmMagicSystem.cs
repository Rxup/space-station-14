using Content.Server.Magic;
using Content.Shared.Backmen.Magic;
using Content.Shared.Backmen.Magic.Events;
using Content.Shared.Damage;
using Content.Shared.Magic;
using Content.Shared.Magic.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Magic;

public sealed class BkmMagicSystem : SharedBkmMagicSystem
{
    [Dependency]
    private readonly SharedAudioSystem _audio = default!;
    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpellbookComponent, CanUseMagicEvent>(OnDoAfter, before: [typeof(MagicSystem)]);
    }

    private readonly SoundSpecifier _sizzleSoundPath = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
    private readonly DamageSpecifier _damageOnUntrainedUse = new DamageSpecifier(){DamageDict = {{"Burn", 50}}};

    private void OnDoAfter(Entity<SpellbookComponent> ent, ref CanUseMagicEvent args)
    {
        if (HasComp<SpellbookUserComponent>(args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("spellbook-sizzle"), args.User);

        _audio.PlayPvs(_sizzleSoundPath, args.User);
        DamageableSystem.TryChangeDamage(args.User, _damageOnUntrainedUse, true, origin: ent);
    }
}
