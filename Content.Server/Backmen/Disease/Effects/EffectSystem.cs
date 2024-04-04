using System.Linq;
using Content.Server.Backmen.Disease.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Effects;
using Content.Shared.Interaction;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

public sealed partial class DiseaseEffectSystem : SharedDiseaseEffectSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseAddComponent>>(DiseaseAddComponent);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseAdjustReagent>>(DiseaseAdjustReagent);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseGenericStatusEffect>>(DiseaseGenericStatusEffect);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseHealthChange>>(DiseaseHealthChange);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseasePolymorph>>(DiseasePolymorph);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseasePopUp>>(DiseasePopUp);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseSnough>>(DiseaseSnough);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseVomit>>(DiseaseVomit);
        SubscribeLocalEvent<DiseaseCarrierComponent, DiseaseEffectArgs<DiseaseCyborgConversion>>(DiseaseCyborgConversion);

        SubscribeLocalEvent<DiseaseInfectionSpreadEvent>(OnSpreadEvent);
    }

    private const double SpreadJobTime = 0.005;
    private readonly JobQueue _spreadJobQueue = new(SpreadJobTime);

    private readonly HashSet<Entity<DiseaseCarrierComponent>> _diseaseCarrierSpread = new();
    private void OnSpreadEvent(DiseaseInfectionSpreadEvent ev)
    {
        _spreadJobQueue.EnqueueJob(new DiseaseInfectionSpread(ev,this,SpreadJobTime));
    }

    public void DoSpread(EntityUid uid, DiseasePrototype disease, float range)
    {
        _diseaseCarrierSpread.Clear();
        var pos = _transform.GetMapCoordinates(uid);
        _lookup.GetEntitiesInRange(pos, range, _diseaseCarrierSpread, LookupFlags.Uncontained);
        foreach (var entity in _diseaseCarrierSpread)
        {
            if (entity.Owner == uid)
                continue;
            var tarPos = _transform.GetMapCoordinates(entity);
            if (!_interactionSystem.InRangeUnobstructed(pos, tarPos, range))
                continue;

            _disease.TryInfect(entity, disease, 0.3f);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _spreadJobQueue.Process();
    }
}
