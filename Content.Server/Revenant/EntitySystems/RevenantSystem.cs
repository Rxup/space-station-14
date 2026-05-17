using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Store.Systems;
using Content.Shared.Alert;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Store.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Revenant.EntitySystems;

public sealed partial class RevenantSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _status = default!;
    [Dependency] private SharedInteractionSystem _interact = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private VisibilitySystem _visibility = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<RevenantComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<RevenantComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RevenantComponent, StatusEffectAddedEvent>(OnRevenantStunned);
        SubscribeLocalEvent<RevenantComponent, StatusEffectEndedEvent>(OnRevenantStunEndAttempt);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(_ => MakeVisible(true));

        SubscribeLocalEvent<RevenantComponent, GetVisMaskEvent>(OnRevenantGetVis);

        InitializeAbilities();
    }

    private void OnRevenantGetVis(Entity<RevenantComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Ghost;
    }

    private void OnStartup(EntityUid uid, RevenantComponent component, ComponentStartup args)
    {
        ChangeEssenceAmount(uid, 0, component);

        _appearance.SetData(uid, RevenantVisuals.Corporeal, false);
        _appearance.SetData(uid, RevenantVisuals.Harvesting, false);
        _appearance.SetData(uid, RevenantVisuals.Stunned, false);

        if (_ticker.RunLevel == GameRunLevel.PostRound && TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Ghost, false);
            _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibility.RefreshVisibility(uid, visibility);
        }

        _eye.RefreshVisibilityMask(uid);
    }

    private void OnRevenantStunned(Entity<RevenantComponent> ent, ref StatusEffectAddedEvent args)
    {
        if (args.Key == "Stun")
            _appearance.SetData(ent, RevenantVisuals.Stunned, true);
    }

    private void OnRevenantStunEndAttempt(Entity<RevenantComponent> ent, ref StatusEffectEndedEvent args)
    {
        if (args.Key == "Stun")
            _appearance.SetData(ent, RevenantVisuals.Stunned, false);
    }

    private void OnExamine(EntityUid uid, RevenantComponent component, ExaminedEvent args)
    {
        if (args.Examiner == args.Examined)
        {
            args.PushMarkup(Loc.GetString("revenant-essence-amount",
                ("current", component.Essence.Int()), ("max", component.EssenceRegenCap.Int())));
        }
    }

    private void OnDamage(EntityUid uid, RevenantComponent component, DamageChangedEvent args)
    {
        if (!HasComp<CorporealComponent>(uid) || args.DamageDelta == null)
            return;

        var essenceDamage = args.DamageDelta.GetTotal().Float() * component.DamageToEssenceCoefficient * -1;
        ChangeEssenceAmount(uid, essenceDamage, component);
    }

    public bool ChangeEssenceAmount(EntityUid uid, FixedPoint2 amount, RevenantComponent? component = null, bool allowDeath = true, bool regenCap = false)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!allowDeath && component.Essence + amount <= 0)
            return false;

        component.Essence += amount;

        if (regenCap)
            component.Essence = FixedPoint2.Min(component.Essence, component.EssenceRegenCap);

        Dirty(uid, component);

        if (TryComp<StoreComponent>(uid, out var store))
            _store.UpdateUserInterface(uid, uid, store);

        _alerts.ShowAlert(uid, component.EssenceAlert);

        if (component.Essence <= 0)
        {
            component.Essence = 0;
            ClearStatusEffects(uid);

            var stasisObj = Spawn(component.SpawnOnDeathPrototype, Transform(uid).Coordinates);
            AddComp(stasisObj, new RevenantStasisComponent(component.StasisTime, uid));

            if (_mind.TryGetMind(uid, out var mindId, out _))
                _mind.TransferTo(mindId, stasisObj);

            _transform.DetachEntity(uid);
            _meta.SetEntityPaused(uid, true);
        }

        return true;
    }

    private void ClearStatusEffects(EntityUid uid)
    {
        if (!TryComp<StatusEffectContainerComponent>(uid, out var container) || container.ActiveStatusEffects == null)
            return;

        foreach (var effect in container.ActiveStatusEffects.ContainedEntities.ToArray())
            Del(effect);
    }

    private bool TryUseAbility(EntityUid uid, RevenantComponent component, FixedPoint2 abilityCost, Vector2 debuffs)
    {
        if (component.Essence <= abilityCost)
        {
            _popup.PopupEntity(Loc.GetString("revenant-not-enough-essence"), uid, uid);
            return false;
        }

        var tileref = _turf.GetTileRef(Transform(uid).Coordinates);
        if (tileref != null)
        {
            if (_physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("revenant-in-solid"), uid, uid);
                return false;
            }
        }

        ChangeEssenceAmount(uid, -abilityCost, component, false);

        _status.TryAddStatusEffectDuration(uid, RevenantStatusEffects.Corporeal, TimeSpan.FromSeconds(debuffs.Y));
        _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(debuffs.X));

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.ResetDynamics(uid, physics);

        return true;
    }

    public void MakeVisible(bool visible)
    {
        var query = EntityQueryEnumerator<RevenantComponent, VisibilityComponent>();
        while (query.MoveNext(out var uid, out _, out var vis))
        {
            if (visible)
            {
                _visibility.AddLayer((uid, vis), (int) VisibilityFlags.Normal, false);
                _visibility.RemoveLayer((uid, vis), (int) VisibilityFlags.Ghost, false);
            }
            else
            {
                _visibility.AddLayer((uid, vis), (int) VisibilityFlags.Ghost, false);
                _visibility.RemoveLayer((uid, vis), (int) VisibilityFlags.Normal, false);
            }

            _visibility.RefreshVisibility(uid, vis);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RevenantComponent>();
        while (query.MoveNext(out var uid, out var rev))
        {
            rev.Accumulator += frameTime;

            if (rev.Accumulator <= 1)
                continue;

            rev.Accumulator -= 1;

            if (rev.Essence >= rev.EssenceRegenCap)
                continue;

            var essence = rev.EssencePerSecond;

            if (_status.TryGetStatusEffect(uid, RevenantStatusEffects.EssenceRegen, out var regenEnt)
                && TryComp<RevenantRegenModifierStatusEffectComponent>(regenEnt, out var regen))
            {
                essence += rev.HauntEssenceRegenPerWitness * regen.NewHaunts;
            }

            ChangeEssenceAmount(uid, essence, rev, regenCap: true);
        }
    }
}
