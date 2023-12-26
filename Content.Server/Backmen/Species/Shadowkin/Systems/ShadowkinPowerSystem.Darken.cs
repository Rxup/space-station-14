using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinDarkenSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;


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


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shadowkins = EntityQueryEnumerator<ShadowkinDarkSwappedComponent>();

        while (shadowkins.MoveNext(out var uid, out var shadowkin))
        {
            // Don't do anything if this Shadowkin isn't darkening
            if (!shadowkin.Darken)
                continue;

            var transform = Transform(uid);

            // Cooldown
            shadowkin.DarkenAccumulator -= frameTime;
            if (shadowkin.DarkenAccumulator > 0f)
                continue;
            shadowkin.DarkenAccumulator += shadowkin.DarkenRate;


            var darkened = new List<EntityUid>();
            // Get all lights in range
            var lightQuery = _lookup.GetEntitiesInRange(transform.MapID, transform.WorldPosition, shadowkin.DarkenRange, flags: LookupFlags.StaticSundries)
                .Where(x => HasComp<ShadowkinLightComponent>(x) && HasComp<PointLightComponent>(x));

            // Add all lights in range to the list if not already there
            foreach (var entity in lightQuery)
            {
                if (!darkened.Contains(entity))
                    darkened.Add(entity);
            }

            // Randomize the list to avoid bias
            _random.Shuffle(darkened);
            shadowkin.DarkenedLights = darkened;

            var playerPos = Transform(uid).WorldPosition;

            foreach (var light in shadowkin.DarkenedLights.ToArray())
            {
                var lightPos = Transform(light).WorldPosition;
                var pointLight = Comp<PointLightComponent>(light);


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
                if (shadowkinLight.AttachedEntity == EntityUid.Invalid)
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
    }
}
