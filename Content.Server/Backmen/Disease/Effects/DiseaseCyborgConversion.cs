using Content.Server.Humanoid;
using Content.Server.Repairable;
using Content.Shared.Backmen.Disease;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

public sealed partial class DiseaseCyborgConversion : DiseaseEffect
{
    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseCyborgConversion>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    private void DiseaseCyborgConversion(Entity<DiseaseCarrierComponent> ent,
        ref DiseaseEffectArgs<DiseaseCyborgConversion> args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (TryComp<MobThresholdsComponent>(ent, out var thresholdsComponent))
        {
            (thresholdsComponent as dynamic).AllowRevives = true;
        }

        var repairableComponent = EnsureComp<RepairableComponent>(ent);
        repairableComponent.AllowSelfRepair = false;
        repairableComponent.SelfRepairPenalty = 3f;
        repairableComponent.FuelCost = 50;
        repairableComponent.DoAfterDelay = 8;

        _disease.CureDisease(ent, args.Disease);
        if (TryComp<HumanoidAppearanceComponent>(ent, out var appearanceComponent))
        {
            _appearanceSystem.SetSex(ent, Sex.Female, true, appearanceComponent);
            _appearanceSystem.SetSkinColor(ent, Color.Red, true, true, appearanceComponent);
            _appearanceSystem.SetTTSVoice(ent, "Baya", appearanceComponent);
            if (appearanceComponent.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var tails))
            {
                foreach (var marking in tails.ToArray())
                {
                    _appearanceSystem.RemoveMarking(ent, marking.MarkingId);
                }
            }

            if (appearanceComponent.MarkingSet.Markings.TryGetValue(MarkingCategories.HeadTop, out var headtop))
            {
                foreach (var marking in headtop.ToArray())
                {
                    _appearanceSystem.RemoveMarking(ent, marking.MarkingId);
                }
            }

            _appearanceSystem.AddMarking(ent, "LongEarsWide", Color.Red, true, true, appearanceComponent);
            _appearanceSystem.AddMarking(ent, "MothAntennasFeathery", Color.Red, true, true, appearanceComponent);
            _appearanceSystem.AddMarking(ent, "TailSuccubus", Color.Red, true, true, appearanceComponent);
            appearanceComponent.Age = 1;
            appearanceComponent.EyeColor = Color.Red;
            appearanceComponent.Gender = Gender.Epicene;
            _metaDataSystem.SetEntityDescription(ent,
                "Рободьявол. Кажется, это можно починить сваркой даже если оно умерло");
            Dirty(ent, appearanceComponent);
            _popup.PopupPredicted("Кажется у вас в теле что-то поменялось....", ent, null, PopupType.LargeCaution);
        }
    }
}
