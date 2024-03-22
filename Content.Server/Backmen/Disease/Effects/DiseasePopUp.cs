using Content.Shared.Backmen.Disease;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using JetBrains.Annotations;

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
}
public enum PopupRecipients
{
    Pvs,
    Local
}

public sealed partial class DiseaseEffectSystem
{
    private void DiseasePopUp(DiseaseEffectArgs args, DiseasePopUp ds)
    {
        if (ds.Type == PopupRecipients.Local)
            _popup.PopupEntity(Loc.GetString(ds.Message), args.DiseasedEntity, args.DiseasedEntity, ds.VisualType);
        else if (ds.Type == PopupRecipients.Pvs)
            _popup.PopupEntity(Loc.GetString(ds.Message, ("person", Identity.Entity(args.DiseasedEntity, EntityManager))), args.DiseasedEntity, ds.VisualType);
    }
}
