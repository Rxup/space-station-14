using Content.Shared.Backmen.Disease;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Plays a popup on the host's transform.
/// Supports passing the host's entity metadata
/// in PVS ones with {$person}
/// </summary>
[UsedImplicitly]
public sealed partial class DiseasePopUp : DiseaseEffect
{
    [DataField("message")]
    public string Message = "disease-sick-generic";

    [DataField("type")]
    public PopupRecipients Type = PopupRecipients.Local;

    [DataField("visualType")]
    public PopupType VisualType = PopupType.Small;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseasePopUp>(ent, disease, this);
    }
}
public enum PopupRecipients
{
    Pvs,
    Local
}

public sealed partial class DiseaseEffectSystem
{
    private void DiseasePopUp(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseasePopUp> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;
        if (args.DiseaseEffect.Type == PopupRecipients.Local)
            _popup.PopupEntity(Loc.GetString(args.DiseaseEffect.Message), args.DiseasedEntity, args.DiseasedEntity, args.DiseaseEffect.VisualType);
        else if (args.DiseaseEffect.Type == PopupRecipients.Pvs)
            _popup.PopupEntity(Loc.GetString(args.DiseaseEffect.Message, ("person", Identity.Entity(args.DiseasedEntity, EntityManager))), args.DiseasedEntity, args.DiseaseEffect.VisualType);
    }
}
