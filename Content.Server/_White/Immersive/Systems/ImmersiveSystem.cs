using System.Numerics;
using Content.Server._White.Immersive.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._White.Telescope;
using Content.Shared.Humanoid;
using Content.Shared.GameTicking.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._White.Immersive;

public sealed class ImmersiveSystem : GameRuleSystem<ImmersiveComponent>
{

    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedTelescopeSystem _telescope = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    protected override void Started(EntityUid uid, ImmersiveComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        OnStarted(component);
    }

    private void OnStarted(ImmersiveComponent component)
    {
        var humans = EntityQuery<HumanoidAppearanceComponent>();

        foreach (var human in humans)
        {
            var entity = human.Owner;

            if (!HasComp<ContentEyeComponent>(entity))
                continue;

            SetEyeZoom(entity, component.EyeModifier);
            AddTelescope(entity, component.TelescopeDivisor, component.TelescopeLerpAmount);
        }
    }

    private void SetEyeZoom(EntityUid human, float modifier)
    {
        _eye.SetMaxZoom(human, new Vector2(modifier));
        _eye.SetZoom(human, new Vector2(modifier));
    }

    private void AddTelescope(EntityUid human, float divisor, float lerpAmount)
    {
        var telescope = EnsureComp<TelescopeComponent>(human);

        _telescope.SetParameters((human, telescope), divisor, lerpAmount);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<ContentEyeComponent>(ev.Mob))
            return;

        var query = EntityQueryEnumerator<ImmersiveComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleEntity, out var Immersive, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(ruleEntity, gameRule))
                continue;

            SetEyeZoom(ev.Mob, Immersive.EyeModifier);
            AddTelescope(ev.Mob, Immersive.TelescopeDivisor, Immersive.TelescopeLerpAmount);
        }
    }


    protected override void Ended(EntityUid uid, ImmersiveComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        var humans = EntityQuery<HumanoidAppearanceComponent>();

        foreach (var human in humans)
        {
            var entity = human.Owner;

            if (!HasComp<ContentEyeComponent>(entity))
                continue;

            SetEyeZoom(entity, 1f);

            RemComp<TelescopeComponent>(entity);
        }
    }
}
