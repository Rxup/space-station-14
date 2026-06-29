using System.Numerics;
using Content.Shared.Backmen.Damage;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.EntityHealthBar;

/// <summary>
/// Yeah a lot of this is duplicated from doafters.
/// Not much to be done until there's a generic HUD system
/// </summary>
public sealed partial class BkmEntityHealthBarOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobStateSystem;
    private readonly MobThresholdSystem _mobThresholdSystem;
    private readonly DamageableSystem _damageable;
    private readonly BackmenDamageModelSystem _backmenDamageModel;
    private readonly ShaderInstance _shader;
    private readonly SharedInteractionSystem _interaction;

    [Dependency] private IPlayerManager _playerManager = default!;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public List<string> DamageContainers = new();

    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";

    public BkmEntityHealthBarOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entManager.System<SharedTransformSystem>();
        _mobStateSystem = _entManager.System<MobStateSystem>();
        _mobThresholdSystem = _entManager.System<MobThresholdSystem>();
        _damageable = _entManager.System<DamageableSystem>();
        _backmenDamageModel = _entManager.System<BackmenDamageModelSystem>();
        _interaction = _entManager.System<SharedInteractionSystem>();

        _shader = _protoManager.Index<ShaderPrototype>(Unshaded).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity == null)
        {
            return;
        }

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        const float scale = 1f;
        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3x2.CreateRotation(-((float)rotation.Theta));
        handle.UseShader(_shader);

        // start-backmen: health-ui
        var q = _entManager.AllEntityQueryEnumerator<MobThresholdsComponent, MobStateComponent, DamageableComponent>();
        while (q.MoveNext(out var owner, out var thresholds, out var mob, out var dmg))
        {
            if (!xformQuery.TryGetComponent(owner, out var xform) ||
                xform.MapID != args.MapId)
            {
                continue;
            }

            if (!_backmenDamageModel.MatchesDamageContainerFilter(owner, DamageContainers))
                continue;
        // end-backmen: health-ui

            if (!_interaction.InRangeUnobstructed(_playerManager.LocalEntity.Value, owner, range: 30f, collisionMask: CollisionGroup.Opaque))
                continue;


            var worldPosition = _transform.GetWorldPosition(xform);
            var worldMatrix = Matrix3x2.CreateTranslation(worldPosition);

            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);

            handle.SetTransform(matty);

            var yOffset = spriteQuery.TryGetComponent(owner, out var sprite) ? sprite.Bounds.Height + 19f : 1f;

            var position = new Vector2(-24 / 2f / EyeManager.PixelsPerMeter,
                yOffset / EyeManager.PixelsPerMeter);

            // Draw the underlying bar texture
            //handle.DrawTexture(_barTexture, position);
            // we are all progressing towards death every day
            (float ratio, bool inCrit) deathProgress = CalcProgress(owner, mob, dmg, thresholds);


            var color = GetProgressColor(deathProgress.ratio, deathProgress.inCrit);

            handle.DrawCircle(position, 0.1f + 0.03f, Color.Gray, false);
            DrawProgressCircle(handle, position, 0.1f, color, deathProgress.ratio);

            // Hardcoded width of the progress bar because it doesn't match the texture.
            /*
            const float startX = 2f;
            const float endX = 22f;

            var xProgress = (endX - startX) * deathProgress.ratio + startX;

            var box = new Box2(new Vector2(startX, 3f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 4f) / EyeManager.PixelsPerMeter);
            box = box.Translated(position);
            handle.DrawRect(box, color);
            */
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawProgressCircle(DrawingHandleWorld handleWorld, Vector2 position, float radius, Color color, float progress)
    {
        const int segments = 64;


        if (progress >= 1)
        {
            handleWorld.DrawCircle(position, radius, color);
            return;
        }

        // Вычисление количества вершин для заполненной части
        var filledVerticesCount = (int)Math.Ceiling(segments * progress);
        if (filledVerticesCount is <= 0 or >= segments)
            return;

        var filledBuffer = new Vector2[filledVerticesCount + 1];

        filledBuffer[0] = position; // Центральная вершина

        for (var i = 1; i <= filledVerticesCount; i++)
        {
            var angle = i / (float) segments * MathHelper.TwoPi;
            var pos = new Vector2(MathF.Sin(angle), MathF.Cos(angle));

            filledBuffer[i] = position + pos * radius;
        }

        // Рисование заполненной части
        handleWorld.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, filledBuffer, color);

    }

    /// <summary>
    /// Returns a ratio between 0 and 1, and whether the entity is in crit.
    /// </summary>
    private (float, bool) CalcProgress(EntityUid uid, MobStateComponent component, DamageableComponent dmg, MobThresholdsComponent thresholds)
    {
        var totalDamage = _damageable.GetTotalDamage((uid, dmg));
        // start-backmen: health-ui
        if (_entManager.TryGetComponent<ConsciousnessComponent>(uid, out var consciousness))
        {
            if (_mobStateSystem.IsAlive(uid, component))
            {
                var ratio = Math.Clamp(
                    1 - ((consciousness.Cap - consciousness.Consciousness) / (consciousness.Cap - consciousness.Threshold)).Float(),
                    0,
                    1);
                return (ratio, false);
            }

            if (_mobStateSystem.IsCritical(uid, component))
            {
                var ratio = Math.Clamp(
                    1 - ((consciousness.Threshold - consciousness.Consciousness) / consciousness.Threshold).Float(),
                    0,
                    1);
                return (ratio, true);
            }
        }
        else
        // end-backmen: health-ui
        if (_mobStateSystem.IsAlive(uid, component))
        {
            if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Critical, out var threshold, thresholds))
                return (1, false);

            var ratio = 1 - ((FixedPoint2)(totalDamage / threshold)).Float();
            return (ratio, false);
        }

        if (_mobStateSystem.IsCritical(uid, component))
        {
            if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold, thresholds) ||
                !_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold, thresholds))
            {
                return (1, true);
            }

            var ratio = 1 -
                    ((totalDamage - critThreshold) /
                    (deadThreshold - critThreshold)).Value.Float();

            return (ratio, true);
        }

        return (0, true);
    }

    public static Color GetProgressColor(float progress, bool crit)
    {
        if (progress >= 1.0f)
        {
            return new Color(0f, 1f, 0f);
        }
        // lerp
        if (!crit)
        {
            var hue = (5f / 18f) * progress;
            return Color.FromHsv(new Vector4(hue, 1f, 0.75f, 1f));
        }
        else
        {
            return Color.Red;
        }
    }
}
