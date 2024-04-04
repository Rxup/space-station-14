using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Jobs;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinDarkenSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private const double MoverJobTime = 0.005;
    private readonly JobQueue _moveJobQueue = new(MoverJobTime);

    public override void Initialize()
    {
        base.Initialize();

        _activePointLight = GetEntityQuery<PointLightComponent>();
    }

    public void ResetLight(EntityUid uid, PointLightComponent light, ShadowkinLightComponent sLight)
    {
        sLight.AttachedEntity = EntityUid.Invalid;

        if (sLight.OldRadiusEdited)
            _light.SetRadius(uid, sLight.OldRadius, light);
        sLight.OldRadiusEdited = false;

        if (sLight.OldEnergyEdited)
            _light.SetEnergy(uid, sLight.OldEnergy, light);
        sLight.OldEnergyEdited = false;
    }


    private EntityQuery<PointLightComponent> _activePointLight;

    public void ProcessLight(EntityUid uid, MapCoordinates transform, HashSet<Entity<ShadowkinLightComponent>> lightQuery, ShadowkinDarkSwappedComponent shadowkin)
    {
        var darkened = new List<EntityUid>();
        // Add all lights in range to the list if not already there
        foreach (var entity in lightQuery)
        {
            if (!darkened.Contains(entity) && _activePointLight.HasComponent(entity))
                darkened.Add(entity);
        }

        // Randomize the list to avoid bias
        _random.Shuffle(darkened);
        shadowkin.DarkenedLights = darkened;

        var playerPos = transform.Position;

        foreach (var light in shadowkin.DarkenedLights.ToArray())
        {
            var lightPos = _transform.GetWorldPosition(light);
            var pointLight = _activePointLight.GetComponent(light);

            // Not a light we should affect
            if (!TryComp(light, out ShadowkinLightComponent? shadowkinLight))
                continue;
            // Not powered, undo changes
            if (TryComp(light, out PoweredLightComponent? powered) && !powered.On)
            {
                ResetLight(light, pointLight, shadowkinLight);
                continue;
            }


            // If the light isn't attached to an entity, attach it to this Shadowkin
            if (shadowkinLight.AttachedEntity == EntityUid.Invalid ||
                TerminatingOrDeleted(shadowkinLight.AttachedEntity))
            {
                shadowkinLight.AttachedEntity = uid;
            }

            // Check if the light is being updated by the correct Shadowkin
            // Prevents horrible flickering when the light is in range of multiple Shadowkin
            if (shadowkinLight.AttachedEntity != EntityUid.Invalid &&
                shadowkinLight.AttachedEntity != uid)
            {
                shadowkin.DarkenedLights.Remove(light);
                continue;
            }

            // 3% chance to remove the attached entity so it can become another Shadowkin's light
            if (shadowkinLight.AttachedEntity == uid)
            {
                if (_random.Prob(0.03f))
                    shadowkinLight.AttachedEntity = EntityUid.Invalid;
            }


            // If we haven't edited the radius yet, save the old radius
            if (!shadowkinLight.OldRadiusEdited)
            {
                shadowkinLight.OldRadius = pointLight.Radius;
                shadowkinLight.OldRadiusEdited = true;
            }

            if (!shadowkinLight.OldEnergyEdited)
            {
                shadowkinLight.OldEnergy = pointLight.Energy;
                shadowkinLight.OldEnergyEdited = true;
            }

            var distance = (lightPos - playerPos).Length();
            var radius = distance * 2f;
            var energy = distance * 0.8f;

            // Set new radius based on distance
            if (shadowkinLight.OldRadiusEdited && radius > shadowkinLight.OldRadius)
                radius = shadowkinLight.OldRadius;
            if (shadowkinLight.OldRadiusEdited && radius < shadowkinLight.OldRadius * 0.20f)
                radius = shadowkinLight.OldRadius * 0.20f;

            // Set new energy based on distance
            if (shadowkinLight.OldEnergyEdited && energy > shadowkinLight.OldEnergy)
                energy = shadowkinLight.OldEnergy;
            if (shadowkinLight.OldEnergyEdited && energy < shadowkinLight.OldEnergy * 0.20f)
                energy = shadowkinLight.OldEnergy * 0.20f;

            // Put changes into effect
            _light.SetRadius(light, radius);
            _light.SetEnergy(light, energy, pointLight);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _moveJobQueue.Process();

        var q = EntityQueryEnumerator<ShadowkinDarkSwappedComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var shadowkin, out var xform))
        {
            // Don't do anything if this Shadowkin isn't darkening
            if (!shadowkin.Darken)
                continue;

            // Cooldown
            shadowkin.DarkenAccumulator -= frameTime;
            if (shadowkin.DarkenAccumulator > 0f)
                continue;
            shadowkin.DarkenAccumulator += shadowkin.DarkenRate;

            _moveJobQueue.EnqueueJob(new ShadowkinLightJob(this, _transform, _lookup, (uid, shadowkin, xform), MoverJobTime));
        }
    }
}
