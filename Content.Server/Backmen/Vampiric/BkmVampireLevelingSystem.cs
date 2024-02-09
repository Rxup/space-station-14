using Content.Server.Backmen.Vampiric.Role;
using Content.Server.Mind;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Slippery;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Vampiric;

public sealed class BkmVampireLevelingSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmVampireComponent, VampireShopActionEvent>(OnOpenShop);
        SubscribeLocalEvent<BkmVampireComponent, VampireStoreEvent>(OnShopBuyPerk);
        SubscribeLocalEvent<BkmVampireComponent, RefreshMovementSpeedModifiersEvent>(OnApplySprint);
    }

    private void OnApplySprint(Entity<BkmVampireComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.SprintLevel == 0)
        {
            return;
        }

        switch (ent.Comp.SprintLevel)
        {
            case 1:
                args.ModifySpeed(2f, 1f);
                break;
            case 2:
                args.ModifySpeed(3f, 1f);
                break;
            case 3:
                args.ModifySpeed(3.4f,1f);
                break;
            case 4:
                args.ModifySpeed(3.8f,1f);
                break;
            case 5:
                args.ModifySpeed(4.2f,1f);
                break;
        }
    }

    private void OnOpenShop(Entity<BkmVampireComponent> ent, ref VampireShopActionEvent args)
    {
        if (!TryComp<StoreComponent>(ent, out var store))
            return;
        _store.ToggleUi(ent, ent, store);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string VmpShop = "VmpShop";

    public void InitShop(Entity<BkmVampireComponent> ent)
    {
        _actions.AddAction(ent, VmpShop);
        var store = EnsureComp<StoreComponent>(ent);
        store.RefundAllowed = false;
        store.Categories.Add("VapmireT0");
        store.CurrencyWhitelist.Add(ent.Comp.CurrencyPrototype);
    }

    [ValidatePrototypeId<PolymorphPrototype>]
    private const string BVampieBat = "BVampieBat";

    [ValidatePrototypeId<PolymorphPrototype>]
    private const string BVampieMouse = "BVampieMouse";

    private void OnShopBuyPerk(Entity<BkmVampireComponent> ent, ref VampireStoreEvent args)
    {
        _adminLogger.Add(LogType.StorePurchase, LogImpact.Medium,
            $"{ToPrettyString(ent):entity} vpm leveling buy {args.BuyType}");
        switch (args.BuyType)
        {
            case VampireStoreType.Tier1Upgrade:
                UnlockTier(ent, 1);
                break;
            case VampireStoreType.Tier2Upgrade:
                UnlockTier(ent, 2);
                break;
            case VampireStoreType.Tier3Upgrade:
                UnlockTier(ent, 3);
                break;
            case VampireStoreType.MakeNewVamp:
                _actions.AddAction(ent, ref ent.Comp.ActionNewVamp, ent.Comp.NewVamp);
#if !DEBUG
                _actions.SetCooldown(ent.Comp.ActionNewVamp, TimeSpan.FromMinutes(5));
#endif
                break;
            case VampireStoreType.SkillMouse1:
                _polymorph.CreatePolymorphAction(BVampieBat, (ent, EnsureComp<PolymorphableComponent>(ent)));
                break;
            case VampireStoreType.SkillMouse2:
                _polymorph.CreatePolymorphAction(BVampieMouse, (ent, EnsureComp<PolymorphableComponent>(ent)));
                break;
            case VampireStoreType.Sprint1:
                ent.Comp.SprintLevel = 1;
                _speedModifier.RefreshMovementSpeedModifiers(ent);
                break;
            case VampireStoreType.Sprint2:
                ent.Comp.SprintLevel = 2;
                _speedModifier.RefreshMovementSpeedModifiers(ent);
                break;
            case VampireStoreType.Sprint3:
                ent.Comp.SprintLevel = 3;
                _speedModifier.RefreshMovementSpeedModifiers(ent);
                break;
            case VampireStoreType.Sprint4:
                ent.Comp.SprintLevel = 4;
                _speedModifier.RefreshMovementSpeedModifiers(ent);
                break;
            case VampireStoreType.Sprint5:
                ent.Comp.SprintLevel = 5;
                _speedModifier.RefreshMovementSpeedModifiers(ent);
                break;
            case VampireStoreType.NoSlip:
                EnsureComp<NoSlipComponent>(ent);
                break;
            case VampireStoreType.DispelPower:
                EnsureComp<DispelPowerComponent>(ent);
                break;
            case VampireStoreType.IgnitePower:
                EnsureComp<PyrokinesisPowerComponent>(ent);
                break;
            case VampireStoreType.RegenPower:
                EnsureComp<PsionicRegenerationPowerComponent>(ent);
                break;
            case VampireStoreType.ZapPower:
                EnsureComp<NoosphericZapPowerComponent>(ent);
                break;
            case VampireStoreType.PsiInvisPower:
                EnsureComp<PsionicInvisibilityPowerComponent>(ent);
                break;
        }
    }


    public void UnlockTier(Entity<BkmVampireComponent> ent, int tier)
    {
        var store = EnsureComp<StoreComponent>(ent);
        store.Categories.Add("VapmireT" + tier);

        if (!_mindSystem.TryGetMind(ent, out var mindId, out var mind) ||
            !TryComp<VampireRoleComponent>(mindId, out var vmpRole))
        {
            return; // no mind? skip;
        }

        vmpRole.Tier = Math.Max(vmpRole.Tier, tier);
    }

    public void AddCurrency(Entity<BkmVampireComponent> ent, FixedPoint2 va)
    {
        _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
                { { ent.Comp.CurrencyPrototype, va } },
            ent);
    }
}
