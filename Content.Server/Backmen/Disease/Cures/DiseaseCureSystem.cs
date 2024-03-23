using Content.Server.Bed.Components;
using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly DiseaseSystem _disease = default!;
    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<HealOnBuckleComponent> _healOnBuckleQuery;
    private EntityQuery<SleepingComponent> _sleepingComponentQuery;
    private EntityQuery<BloodstreamComponent> _bloodstreamQuery;
    private EntityQuery<TemperatureComponent> _temperatureQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseCureArgs<DiseaseBedrestCure>>(DiseaseBedrestCure);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseCureArgs<DiseaseBodyTemperatureCure>>(DiseaseBodyTemperatureCure);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseCureArgs<DiseaseJustWaitCure>>(DiseaseJustWaitCure);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseCureArgs<DiseaseReagentCure>>(DiseaseReagentCure);

        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _healOnBuckleQuery = GetEntityQuery<HealOnBuckleComponent>();
        _sleepingComponentQuery = GetEntityQuery<SleepingComponent>();
        _bloodstreamQuery = GetEntityQuery<BloodstreamComponent>();
        _temperatureQuery = GetEntityQuery<TemperatureComponent>();
    }
}
