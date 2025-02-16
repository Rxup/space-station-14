using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Flesh;

public sealed class TransformInFleshPudgeOnDeathSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;
    //[Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformInFleshPudgeOnDeathComponent, MobStateChangedEvent>(OnDamageChanged);
    }


    private void OnDamageChanged(EntityUid uid, TransformInFleshPudgeOnDeathComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Dead or MobState.Critical))
            return;

        if (HasComp<ZombieComponent>(uid))
            return;

        if (TryComp<HumanoidAppearanceComponent>(uid, out var huApComp))
        {
            var golem = Spawn(component.FleshPudgeId, Transform(uid).Coordinates);
            if (TryComp<MindContainerComponent>(uid, out var mindComp))
                _mindSystem.TransferTo(mindComp.Mind!.Value, golem, false);
            //mindComp.Mind?.TransferTo(golem, ghostCheckOverride: true);

            _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-user", ("EntityTransform", golem)),
                golem, golem, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-others",
                    ("Entity", uid), ("EntityTransform", golem)), golem, Filter.PvsExcept(golem),
                true, PopupType.LargeCaution);

            var xform = Transform(uid);
            var coordinates = xform.Coordinates;
            var audio = AudioParams.Default.WithVariation(0.025f);
            _audio.PlayPvs(component.TransformSound, uid, audio);

            if (TryComp<ContainerManagerComponent>(uid, out var container))
            {
                foreach (var cont in _container.GetAllContainers(uid, container).ToArray())
                {
                    foreach (var ent in cont.ContainedEntities.ToArray())
                    {
                        {
                            if (HasComp<BodyPartComponent>(ent))
                                continue;

                            _container.Remove(ent, cont, force: true, destination: coordinates);
                            //cont.Remove(ent, EntityManager, force: true);
                            //_transform.SetCoordinates(ent, coordinates);
                            _randomHelper.RandomOffset(ent, 0.25f);
                        }
                    }
                }
            }

            if (TryComp<BloodstreamComponent>(uid, out var bloodstream) && bloodstream.BloodSolution != null)
            {
                var tempSol = new Solution() { MaxVolume = 5 };

                tempSol.AddSolution(bloodstream.BloodSolution.Value.Comp.Solution, _prototypeManager);

                if (_puddleSystem.TrySpillAt(uid, tempSol.SplitSolution(50), out var puddleUid))
                {
                    if (TryComp<DnaComponent>(uid, out var dna))
                    {
                        var comp = EnsureComp<ForensicsComponent>(puddleUid);
                        comp.DNAs.Add(dna.DNA);
                    }
                }
            }

            QueueDel(uid);

        }
    }
}
