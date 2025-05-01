using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Temperature.Components;
using Content.Shared.Alert;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Temperature;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

// ReSharper disable once CheckNamespace
namespace Content.Server.Temperature.Systems;

public sealed partial class TemperatureSystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private EntityQuery<TemperatureComponent> _temperatureQuery;
    private EntityQuery<TemperatureConductorComponent> _conductorQuery;

    public float TemperatureSpeedup { get; private set; }

    private void InitializeBkm()
    {
        Subs.CVar(_cfg, CCVars.TemperatureSpeedup, value => TemperatureSpeedup = value, true);

        _temperatureQuery = GetEntityQuery<TemperatureComponent>();
        _conductorQuery = GetEntityQuery<TemperatureConductorComponent>();

        SubscribeLocalEvent<TemperatureExamineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AddTemperatureOnTriggerComponent, TriggerEvent>(OnTriggered);

        SubscribeLocalEvent<BodyPartComponent, OnTemperatureChangeEvent>(OnPartTempChange);
        SubscribeLocalEvent<BodyComponent, BodyTemperatureUpdateEvent>(OnBodyUpdateTemperature);
    }

    private void OnExamined(Entity<TemperatureExamineComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<TemperatureComponent>(ent, out var temp))
            return;

        args.PushMarkup(Loc.GetString("temperature-examine-markup-surface", ("temp", temp.CurrentTemperature)));

        if (TryComp<InternalTemperatureComponent>(ent, out var internalTemp))
            args.PushMarkup(Loc.GetString("temperature-examine-markup-internal", ("temp", internalTemp.Temperature)));
    }

    private void OnTriggered(Entity<AddTemperatureOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (!TryComp<TemperatureComponent>(ent, out var temp))
            return;

        ChangeHeat(ent, ent.Comp.HeatAmount, ent.Comp.IgnoreResistance, temp);
    }

    private void OnBodyUpdateTemperature(Entity<BodyComponent> ent, ref BodyTemperatureUpdateEvent args)
    {
        UpdateAlert(ent.Owner);
    }

    private void OnPartTempChange(Entity<BodyPartComponent> ent, ref OnTemperatureChangeEvent args)
    {
        bool isHot;
        float threshold;
        float idealTemp;

        if (!TryComp<TargetingComponent>(ent.Comp.Body, out var targeting) ||
            !_temperatureQuery.TryComp(ent.Comp.Body, out var temperature))
            return;

        var targetPart = _body.GetTargetBodyPart(ent);

        if (targetPart == null)
            return;

        if (!ent.Comp.Enabled)
        {
            targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Disabled;
            return;
        }

        var body = ent.Comp.Body.Value;

        if (TryComp<ThermalRegulatorComponent>(body, out var regulator) &&
            regulator.NormalBodyTemperature > temperature.ColdDamageThreshold &&
            regulator.NormalBodyTemperature < temperature.HeatDamageThreshold)
        {
            idealTemp = regulator.NormalBodyTemperature;
        }
        else
        {
            idealTemp = (temperature.ColdDamageThreshold + temperature.HeatDamageThreshold) / 2;
        }

        if (args.CurrentTemperature <= idealTemp)
        {
            threshold = temperature.ColdDamageThreshold;
            isHot = false;
        }
        else
        {
            threshold = temperature.HeatDamageThreshold;
            isHot = true;
        }

        // Calculates a scale where 1.0 is the ideal temperature and 0.0 is where temperature damage begins
        // The cold and hot scales will differ in their range if the ideal temperature is not exactly halfway between the thresholds
        var tempScale = (args.CurrentTemperature - threshold) / (idealTemp - threshold);
        switch (tempScale)
        {
            case <= 0f:
                if (isHot)
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Overheated;
                else
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Freezing;
                break;

            case <= 0.4f:
                if (isHot)
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Heated;
                else
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Cooling;
                break;

            case <= 0.66f:
                if (isHot)
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.MinorHeat;
                else
                    targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.MinorCold;
                break;

            case > 0.66f:
                targeting.TemperatureBodyStatus[targetPart.Value] = TemperatureSeverity.Normal;
                break;
        }

        RaiseLocalEvent(body, new BodyTemperatureUpdateEvent());
    }

    private void UpdateBkm(float frameTime)
    {
        UpdateDebug(frameTime);
        UpdateBodyHeat(frameTime);
    }

    private void UpdateDebug(float frameTime)
    {
        var query = EntityQueryEnumerator<DebugTermometherComponent, TemperatureComponent, TemperatureConductorComponent>();
        var updateList = new List<Entity<TemperatureComponent, TemperatureConductorComponent>>();
        while (query.MoveNext(out var uid, out _, out var temperature, out var internalTemp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            updateList.Add((uid, temperature, internalTemp));
        }

        if (updateList.Count == 0)
            return;

        if (updateList.Count == 2)
        {
            ConductTemperature(updateList[0].Owner, updateList[1].Owner, frameTime);
            return;
        }

        var ent = _random.PickAndTake(updateList);
        ConductTemperature(ent.Owner, updateList, frameTime);
    }

    private void UpdateBodyHeat(float frameTime)
    {
        var query = EntityQueryEnumerator<BodyComponent, TemperatureComponent>();

        while (query.MoveNext(out var uid, out var body, out var temperature))
        {
            var rootPart = _body.GetRootPartOrNull(uid, body);
            if (rootPart == null)
                continue;

            var children = _body.GetBodyPartChildren(rootPart.Value.Entity).ToList();

            var updateList = new List<Entity<TemperatureComponent, TemperatureConductorComponent>>();
            foreach (var part in children)
            {
                if (!_temperatureQuery.TryComp(part.Id, out var temp) ||
                    !_conductorQuery.TryComp(part.Id, out var conductor))
                    continue;

                updateList.Add((part.Id, temp, conductor));
            }

            if (updateList.Count == 0)
                continue;

            // Body temperature is the middle of all it's body parts temperature.
            var middleTemperature = updateList.Sum(part => part.Comp1.CurrentTemperature) / updateList.Count;
            temperature.CurrentTemperature = middleTemperature;

            // Conduct temperature, starting from the root part
            var rootPartEnt = updateList.Find(x => x.Owner == rootPart.Value.Entity);
            updateList.Remove(rootPartEnt);

            if (TerminatingOrDeleted(rootPartEnt))
                continue;

            ConductTemperature(rootPartEnt.Owner, updateList, frameTime);

            // Then all parts transfer their heat RECURSIVELY!!!!!
            foreach (var part in updateList)
            {
                ReCurseConductBodyParts(part, frameTime);
            }
        }
    }

    private void ReCurseConductBodyParts(
        Entity<TemperatureComponent, TemperatureConductorComponent> parentPart,
        float frameTime)
    {
        var children = _body.GetBodyPartChildren(parentPart).ToList();

        var updateList = new List<Entity<TemperatureComponent, TemperatureConductorComponent>>();
        foreach (var part in children)
        {
            if (!_temperatureQuery.TryComp(part.Id, out var temp) ||
                !_conductorQuery.TryComp(part.Id, out var conductor))
                continue;

            updateList.Add((part.Id, temp, conductor));
        }

        updateList.Remove(parentPart);
        ConductTemperature(parentPart.Owner, updateList, frameTime);

        // Then all parts transfer their heat RECURSIVELY!!!!!
        foreach (var part in updateList)
        {
            ReCurseConductBodyParts(part, frameTime);
        }
    }

    private bool TryChangeBodyHeat(
        Entity<TemperatureComponent?, BodyComponent?> body,
        float heatAmount,
        bool ignoreHeatResistance = false,
        TargetBodyPart parts = TargetBodyPart.All)
    {
        if (!Resolve(body, ref body.Comp1, ref body.Comp2, false))
            return false;

        var targetParts = _body.ConvertTargetBodyParts(parts).ToList();
        // If we target just one body part then we multiply the heat by 10 times. Because funny balance.
        float multiplier = SharedTargetingSystem.GetValidParts().Length / targetParts.Count;

        // TODO: Handling flags with TargetBodyPart is hilariously shitty.
        foreach (var targetPart in targetParts)
        {
            var typeParts = _body.GetBodyChildrenOfType(body, targetPart.Type, body, targetPart.Symmetry);

            foreach (var (part, _) in typeParts)
            {
                if (!TryComp<TemperatureComponent>(part, out var partTemp))
                    continue;

                ChangeHeat(part, heatAmount * multiplier, ignoreHeatResistance, partTemp);
            }
        }

        return true;
    }

    private bool TryForceChangeBodyHeat(
        Entity<TemperatureComponent?, BodyComponent?> body,
        float temp,
        TargetBodyPart parts = TargetBodyPart.All)
    {
        if (!Resolve(body, ref body.Comp1, ref body.Comp2, false))
            return false;

        var targetParts = _body.ConvertTargetBodyParts(parts).ToList();

        // TODO: Handling flags with TargetBodyPart is hilariously shitty.
        foreach (var targetPart in targetParts)
        {
            var typeParts = _body.GetBodyChildrenOfType(body, targetPart.Type, body, targetPart.Symmetry);

            foreach (var (part, _) in typeParts)
            {
                if (!TryComp<TemperatureComponent>(part, out var partTemp))
                    continue;

                // This is fine. (Probably...) (Totally not evil recursion at all)
                ForceChangeTemperature(part, temp, partTemp);
            }
        }

        return true;
    }

    /// <summary>
    /// Conducts temperature between two bodies.
    /// </summary>
    public void ConductTemperature(
        Entity<TemperatureComponent?, TemperatureConductorComponent?> bodyA,
        Entity<TemperatureComponent?, TemperatureConductorComponent?> bodyB,
        float frameTime)
    {
        if (!Resolve(bodyA, ref bodyA.Comp1, ref bodyA.Comp2) ||
            !Resolve(bodyB, ref bodyB.Comp1, ref bodyB.Comp2))
            return;

        var tempA = bodyA.Comp1;
        var tempB = bodyB.Comp1;
        var internalA = bodyA.Comp2;
        var internalB = bodyA.Comp2;

        // Don't do anything if they equalised
        var deltaTemp = Math.Abs(tempA.CurrentTemperature - tempB.CurrentTemperature);
        if (deltaTemp < 0.1f)
            return;

        // Calculate all required math separately, for A and B.
        // Heat flow in W/m^2 as per fourier's law in 1D.
        var qA = internalA.Conductivity * deltaTemp / internalA.Thickness;
        var qB = internalB.Conductivity * deltaTemp / internalB.Thickness;

        // Colliding area will be the smallest one
        var area = Math.Min(internalA.Area, internalB.Area);

        // Convert to actual energy
        var joulesA = qA * area * frameTime;
        var joulesB = qB * area * frameTime;

        // Pick the smallest energy flow
        var energyExchanged = Math.Min(joulesA, joulesB);

        // If body B is actually hotter, then we invert how heat flows
        if (tempB.CurrentTemperature > tempA.CurrentTemperature)
            energyExchanged *= -1;

        energyExchanged *= TemperatureSpeedup;

        // Exchange heat between two bodies
        // Ignore resistance because we don't want to lose energy here
        ChangeHeat(bodyA, -energyExchanged, true, bodyA.Comp1);
        ChangeHeat(bodyB, energyExchanged, true, bodyB.Comp1);
    }

    /// <summary>
    /// Conducts temperature between one body and a list of bodies that are connected to the first one.
    /// </summary>
    public void ConductTemperature(
        Entity<TemperatureComponent?, TemperatureConductorComponent?> bodyA,
        List<Entity<TemperatureComponent, TemperatureConductorComponent>> bodies,
        float frameTime)
    {
        if (!Resolve(bodyA, ref bodyA.Comp1, ref bodyA.Comp2))
            return;

        // Calculate heat flow for all bodies
        var originalTemp = bodyA.Comp1.CurrentTemperature;
        var heatFlowDict = new Dictionary<EntityUid, float>();

        if (bodies.Count == 0)
            return;

        foreach (var bodyB in bodies)
        {
            // Calculate all required math for every body
            var deltaTemp = Math.Abs(bodyA.Comp1.CurrentTemperature - bodyB.Comp1.CurrentTemperature);

            // Don't do anything if they equalised
            if (deltaTemp < 0.1f)
                return;

            // Heat flow in W/m^2 as per fourier's law in 1D.
            var qA = bodyA.Comp2.Conductivity * originalTemp / bodyA.Comp2.Thickness;
            var qB = bodyB.Comp2.Conductivity * deltaTemp / bodyB.Comp2.Thickness;

            // Colliding area will be the smallest one
            var area = Math.Min(bodyA.Comp2.Area, bodyB.Comp2.Area);

            var joulesA = qA * area * frameTime;
            var joulesB = qB * area * frameTime;

            // Pick the smallest energy flow
            var heatFlow = Math.Min(joulesA, joulesB);

            heatFlowDict.Add(bodyB, heatFlow);
        }

        foreach (var (bodyUid, heatFlow) in heatFlowDict)
        {
            var energyExchanged = heatFlow;
            var bodyB = bodies.Find(x => x.Owner == bodyUid);
            if (TerminatingOrDeleted(bodyB))
                continue; // Some oopsie happened while trying to find the entity

            // If body B is actually hotter, we invert how heat flows
            if (bodyB.Comp1.CurrentTemperature > originalTemp)
                energyExchanged *= -1;

            energyExchanged *= TemperatureSpeedup;

            // Exchange heat between two bodies
            // Ignore resistance because we don't want to lose energy here
            ChangeHeat(bodyA, -energyExchanged, true, bodyA.Comp1);
            ChangeHeat(bodyB, energyExchanged, true, bodyB.Comp1);
        }
    }

    /// <summary>
    /// TODO: this shit is extremely evil. Need to raise OnTemperatureChanged event once on every part temperature update
    /// </summary>
    private void UpdateAlert(Entity<AlertsComponent?, TemperatureComponent?> body)
    {
        if (!Resolve(body, ref body.Comp1, false))
            return;

        ProtoId<AlertPrototype> type;
        float threshold;
        float idealTemp;

        if (!Resolve(body, ref body.Comp2, false))
        {
            _alerts.ClearAlertCategory(body, TemperatureAlertCategory);
            return;
        }

        var temperature = body.Comp2;

        if (TryComp<ThermalRegulatorComponent>(body, out var regulator) &&
            regulator.NormalBodyTemperature > temperature.ColdDamageThreshold &&
            regulator.NormalBodyTemperature < temperature.HeatDamageThreshold)
        {
            idealTemp = regulator.NormalBodyTemperature;
        }
        else
        {
            idealTemp = (temperature.ColdDamageThreshold + temperature.HeatDamageThreshold) / 2;
        }

        if (temperature.CurrentTemperature <= idealTemp)
        {
            type = temperature.ColdAlert;
            threshold = temperature.ColdDamageThreshold;
        }
        else
        {
            type = temperature.HotAlert;
            threshold = temperature.HeatDamageThreshold;
        }

        // Calculates a scale where 1.0 is the ideal temperature and 0.0 is where temperature damage begins
        // The cold and hot scales will differ in their range if the ideal temperature is not exactly halfway between the thresholds
        var tempScale = (temperature.CurrentTemperature - threshold) / (idealTemp - threshold);
        switch (tempScale)
        {
            case <= 0f:
                _alerts.ShowAlert(body, type, 3);
                break;

            case <= 0.4f:
                _alerts.ShowAlert(body, type, 2);
                break;

            case <= 0.66f:
                _alerts.ShowAlert(body, type, 1);
                break;

            case > 0.66f:
                _alerts.ClearAlertCategory(body, TemperatureAlertCategory);
                break;
        }
    }
}
