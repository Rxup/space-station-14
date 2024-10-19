using Content.Shared.Backmen.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseDeathCure : DiseaseCure
{
    public override string CureText()
    {
        return Loc.GetString("diagnoser-cure-death");
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseDeathCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    private void DiseaseDeathCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseDeathCure> args)
    {
        if(args.Handled)
            return;

        args.Handled = true;

        if (!_mobStateQuery.TryGetComponent(ent, out var mobStateComponent))
            return;

        if (_mobStateSystem.IsIncapacitated(ent, mobStateComponent))
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
