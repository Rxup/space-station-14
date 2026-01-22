using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._White.BackStab;

public sealed class BackStabSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    private static readonly SoundSpecifier BackstabSound =
        new SoundPathSpecifier("/Audio/_Goobstation/Weapons/Effects/guillotine.ogg");

    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private DamageTypePrototype _damageType = default!;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BackStabComponent, MeleeHitEvent>(HandleHit);
        _damageType = _prototypeManager.Index(Slash);
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    private void HandleHit(Entity<BackStabComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.DamageMultiplier < 1f || !args.IsHit || args.HitEntities.Count != 1)
            return;

        var target = args.HitEntities[0];

        if (!TryBackstab(target, args.User, ent.Comp.Tolerance))
            return;

        var total = args.BaseDamage.GetTotal();

        var damage = total * ent.Comp.DamageMultiplier;

        args.BonusDamage += new DamageSpecifier(_damageType, damage - total);
    }

    public bool TryBackstab(EntityUid target,
        EntityUid user,
        Angle tolerance,
        bool showPopup = true,
        bool playSound = true,
        bool alwaysBackstabLaying = false)
    {
        if (target == user || !_mobStateQuery.HasComp(target))
            return false;

        if (alwaysBackstabLaying && _standing.IsDown(target))
        {
            BackstabEffects(user, target, showPopup, playSound);
            return true;
        }

        var xform = Transform(target);
        var userXform = Transform(user);

        var v1 = -_transform.GetWorldRotation(xform).ToWorldVec();
        var v2 = _transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(xform);
        var angle = CalculateAngle(v1, v2);
        if (angle > tolerance.Theta)
            return false;

        BackstabEffects(user, target, showPopup, playSound);
        return true;
    }

    private static float CalculateAngle(Vector2 v1, Vector2 v2)
    {
        var dot = Vector2.Dot(v1, v2);
        var magProduct = v1.Length() * v2.Length();

        if (magProduct == 0f)
            return 0f;

        var cosTheta = Math.Clamp(dot / magProduct, -1f, 1f);
        return MathF.Acos(cosTheta);
    }

    private void BackstabEffects(EntityUid user, EntityUid target, bool showPopup = true, bool playSound = true)
    {
        if (showPopup)
            _popup.PopupPredicted(Loc.GetString("backstab-message"), target, user, PopupType.LargeCaution);

        if (playSound)
            _audio.PlayPredicted(BackstabSound,target, user);
    }
}
