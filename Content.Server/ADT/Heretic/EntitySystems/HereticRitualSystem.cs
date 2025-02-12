using Content.Server.Heretic.Components;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.Heretic;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System.Text;
using System.Linq;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Examine;

namespace Content.Server.Heretic.EntitySystems;

public sealed partial class HereticRitualSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _series = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HereticKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public SoundSpecifier RitualSuccessSound = new SoundPathSpecifier("/Audio/ADT/Heretic/castsummon.ogg");

    public HereticRitualPrototype GetRitual(ProtoId<HereticRitualPrototype>? id)
    {
        if (id == null) throw new ArgumentNullException();
        return _proto.Index<HereticRitualPrototype>(id);
    }

    /// <summary>
    ///     Try to perform a selected ritual
    /// </summary>
    /// <returns> If the ritual succeeded or not </returns>
    public bool TryDoRitual(EntityUid performer, EntityUid platform, ProtoId<HereticRitualPrototype> ritualId)
    {
        // here i'm introducing locals for basically everything
        // because if i access stuff directly shit is bound to break.
        // please don't access stuff directly from the prototypes or else shit will break.
        // regards

        if (!TryComp<HereticComponent>(performer, out var hereticComp))
            return false;

        var rit = _series.CreateCopy((HereticRitualPrototype) GetRitual(ritualId).Clone(), notNullableOverride: true);
        var lookup = _lookup.GetEntitiesInRange(platform, .75f);

        var missingList = new List<string>();
        var toDelete = new List<EntityUid>();

        // check for all conditions
        // this is god awful but it is that it is
        var behaviors = rit.CustomBehaviors ?? new();
        var requiredNames = rit.RequiredEntityNames?.ToDictionary(e => e.Key, e => e.Value) ?? new();
        var requiredTags = rit.RequiredTags?.ToDictionary(e => e.Key, e => e.Value) ?? new();

        foreach (var behavior in behaviors)
        {
            var ritData = new RitualData(performer, platform, ritualId, EntityManager);

            if (!behavior.Execute(ritData, out var missingStr))
            {
                if (missingStr != null)
                    _popup.PopupEntity(missingStr, platform, performer);
                return false;
            }
        }

        foreach (var look in lookup)
        {
            // check for matching entity names
            foreach (var name in requiredNames)
            {
                if (Name(look) == name.Key)
                {
                    requiredNames[name.Key] -= 1;

                    // prevent deletion of more items than needed
                    if (requiredNames[name.Key] >= 0)
                        toDelete.Add(look);
                }
            }

            // check for matching tags
            foreach (var tag in requiredTags)
            {
                if (!TryComp<TagComponent>(look, out var tags))
                    continue;
                var ltags = tags.Tags;

                if (ltags.Contains(tag.Key))
                {
                    requiredTags[tag.Key] -= 1;

                    // prevent deletion of more items than needed
                    if (requiredTags[tag.Key] >= 0)
                        toDelete.Add(look);
                }
            }
        }

        // add missing names
        foreach (var name in requiredNames)
            if (name.Value > 0)
                missingList.Add(name.Key);

        // add missing tags
        foreach (var tag in requiredTags)
            if (tag.Value > 0)
                missingList.Add(tag.Key);

        // are we missing anything?
        if (missingList.Count > 0)
        {
            // we are! notify the performer about that!
            var sb = new StringBuilder();
            for (int i = 0; i < missingList.Count; i++)
            {
                // makes a nice, list, of, missing, items.
                if (i != missingList.Count - 1)
                    sb.Append($"{missingList[i]}, ");
                else sb.Append(missingList[i]);
            }

            _popup.PopupEntity(Loc.GetString("heretic-ritual-fail-items", ("itemlist", sb.ToString())), platform, performer);
            return false;
        }

        // yay! ritual successfull!

        // reset fields to their initial values
        // BECAUSE FOR SOME REASON IT DOESN'T FUCKING WORK OTHERWISE!!!

        // finalize all of the custom ones
        foreach (var behavior in behaviors)
        {
            var ritData = new RitualData(performer, platform, ritualId, EntityManager);
            behavior.Finalize(ritData);
        }

        // ya get some, ya lose some
        foreach (var ent in toDelete)
            QueueDel(ent);

        // add stuff
        var output = rit.Output ?? new();
        foreach (var ent in output.Keys)
            for (int i = 0; i < output[ent]; i++)
                Spawn(ent, Transform(platform).Coordinates);

        if (rit.OutputEvent != null)
            RaiseLocalEvent(performer, rit.OutputEvent, true);

        if (rit.OutputKnowledge != null)
            _knowledge.AddKnowledge(performer, hereticComp, (ProtoId<HereticKnowledgePrototype>) rit.OutputKnowledge);

        return true;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticRitualRuneComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<HereticRitualRuneComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HereticRitualRuneComponent, ExaminedEvent>(OnExamine);
    }

    private void OnInteract(Entity<HereticRitualRuneComponent> ent, ref InteractHandEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var heretic))
            return;

        if (heretic.KnownRituals.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ritual-norituals"), args.User, args.User);
            return;
        }

        if (heretic.ChosenRitual == null)
            heretic.ChosenRitual = heretic.KnownRituals[0];

        else if (heretic.ChosenRitual != null)
        {
            var index = heretic.KnownRituals.FindIndex(m => m == heretic.ChosenRitual) + 1;

            if (index >= heretic.KnownRituals.Count)
                index = 0;

            heretic.ChosenRitual = heretic.KnownRituals[index];
        }

        var ritualName = Loc.GetString(GetRitual(heretic.ChosenRitual).Name);
        _popup.PopupEntity(Loc.GetString("heretic-ritual-switch", ("name", ritualName)), args.User, args.User);
    }
    private void OnInteractUsing(Entity<HereticRitualRuneComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var heretic))
            return;

        if (!TryComp<MansusGraspComponent>(args.Used, out var grasp))
            return;

        if (heretic.ChosenRitual == null)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ritual-noritual"), args.User, args.User);
            return;
        }

        if (!TryDoRitual(args.User, ent, (ProtoId<HereticRitualPrototype>) heretic.ChosenRitual))
            return;

        _audio.PlayPvs(RitualSuccessSound, ent, AudioParams.Default.WithVolume(-3f));
        Spawn("HereticRuneRitualAnimation", Transform(ent).Coordinates);
    }

    private void OnExamine(Entity<HereticRitualRuneComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<HereticComponent>(args.Examiner, out var h))
            return;

        var ritual = h.ChosenRitual != null ? GetRitual(h.ChosenRitual).Name : null;
        var name = ritual != null ? Loc.GetString(ritual) : "None";
        args.PushMarkup(Loc.GetString("heretic-ritualrune-examine", ("rit", name)));
    }
}
