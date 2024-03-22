using Content.Server.Polymorph.Systems;
using Content.Shared.Audio;
using Content.Shared.Backmen.Disease;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Disease.Effects;

[UsedImplicitly]
public sealed partial class DiseasePolymorph : DiseaseEffect
{
    [DataField("polymorphId", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<PolymorphPrototype> PolymorphId = default!;

    [DataField("polymorphSound")]
    [ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? PolymorphSound;

    [DataField("polymorphMessage")]
    [ViewVariables(VVAccess.ReadWrite)]
    public string? PolymorphMessage;
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void DiseasePolymorph(DiseaseEffectArgs args, DiseasePolymorph ds)
    {
        var polyUid = _polymorph.PolymorphEntity(args.DiseasedEntity, ds.PolymorphId);

        if (ds.PolymorphSound != null && polyUid != null)
        {
            _audio.PlayPvs(ds.PolymorphSound, polyUid.Value, AudioParams.Default.WithVariation(0.2f));
        }
        if (ds.PolymorphMessage != null && polyUid != null)
            _popup.PopupEntity(Loc.GetString(ds.PolymorphMessage), polyUid.Value, polyUid.Value, PopupType.Large);
    }
}
