using Content.Shared.Backmen.Disease;
using Content.Shared.Body;
using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Repairable;
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
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private MetaDataSystem _metaDataSystem = default!;

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
        if (TryComp<HumanoidProfileComponent>(ent, out var humanoid))
        {
            var appearance = new HumanoidCharacterAppearance(Color.Red, Color.Red, new());
            var profile = new HumanoidCharacterProfile(
                MetaData(ent).EntityName,
                MetaData(ent).EntityDescription ?? string.Empty,
                humanoid.Species,
                1,
                Sex.Female,
                Gender.Epicene,
                appearance,
                SpawnPriorityPreference.None,
                new Dictionary<ProtoId<JobPrototype>, JobPriority>(),
                PreferenceUnavailableMode.SpawnAsOverflow,
                [],
                [],
                new Dictionary<string, RoleLoadout>())
            {
                Voice = "Baya",
            };

            _humanoidProfile.ApplyProfileTo((ent, humanoid), profile);
            _visualBody.ApplyProfileTo((ent, TryComp<VisualBodyComponent>(ent, out var visualBody) ? visualBody : null), profile);

            var tts = EnsureComp<TTSComponent>(ent);
            tts.VoicePrototypeId = "Baya";
            Dirty(ent, tts);

            _metaDataSystem.SetEntityDescription(ent,
                "Рободьявол. Кажется, это можно починить сваркой даже если оно умерло");
            _popup.PopupPredicted("Кажется у вас в теле что-то поменялось....", ent, null, PopupType.LargeCaution);
        }
    }
}
