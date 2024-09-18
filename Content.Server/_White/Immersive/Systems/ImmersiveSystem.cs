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

    protected override void Added(EntityUid uid, ImmersiveComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var q = QueryActiveRules();
        while (q.MoveNext(out var owner, out var activeGameRuleComponent, out var comp, out var gameRuleComponent))
        {
            if (owner != uid)
            {
                GameTicker.EndGameRule(uid); // должен быть уникальным!
                return;
            }
        }
    }

    protected override void Started(EntityUid uid, ImmersiveComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        OnStarted(component);
    }

    private void OnStarted(ImmersiveComponent component)
    {
        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent>();

        while (humans.MoveNext(out var entity, out _, out var eye))
        {
            SetEyeZoom((entity, eye), component.EyeModifier);
            AddTelescope(entity, component.TelescopeDivisor, component.TelescopeLerpAmount);
        }
    }

    private void SetEyeZoom(Entity<ContentEyeComponent?> human, float modifier)
    {
        if(!Resolve(human, ref human.Comp))
            return;

        var vec = new Vector2(modifier);
        _eye.SetMaxZoom(human, vec, human.Comp);
        _eye.SetZoom(human, vec, eye: human.Comp);
    }

    private void AddTelescope(EntityUid human, float divisor, float lerpAmount)
    {
        _telescope.SetParameters((human, EnsureComp<TelescopeComponent>(human)), divisor, lerpAmount);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<ContentEyeComponent>(ev.Mob))
            return;

        if(!QueryActiveRules().MoveNext(out _, out var immersive, out _))
            return;

        SetEyeZoom(ev.Mob, immersive.EyeModifier);
        AddTelescope(ev.Mob, immersive.TelescopeDivisor, immersive.TelescopeLerpAmount);
    }


    protected override void Ended(EntityUid uid, ImmersiveComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent>();
        while (humans.MoveNext(out var entity, out _, out var eye))
        {
            SetEyeZoom((entity,eye), 1f);
            RemCompDeferred<TelescopeComponent>(entity);
        }
    }
}
