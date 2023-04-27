using Content.Server.Damage.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Tools;
using JetBrains.Annotations;
using Content.Shared.Tools.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Content.Shared.Tag;
using Content.Shared.Doors.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Interaction;
using Content.Server.Doors.Systems;
using Content.Server.Tools.Systems;
using Content.Server.Wires;

namespace Content.Server.Backmen.Arrivals;

[RegisterComponent, Access(typeof(ArrivalsProtectSystem))]
public sealed class ArrivalsProtectComponent : Component
{

}


[UsedImplicitly]
public sealed class ArrivalsProtectSystem : EntitySystem
{
    [Dependency] private readonly GodmodeSystem _godmodeSystem = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArrivalsSourceComponent,ComponentStartup>(OnArrivalsStartup, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsShuttleComponent,ComponentAdd>(OnArrivalsStartup2, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, InteractUsingEvent>(OnInteractUsing, before: new []{typeof(DoorSystem), typeof(WiresSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, WeldableAttemptEvent>(OnWeldAttempt, before: new []{typeof(DoorSystem), typeof(WiresSystem)});

    }

    private void OnWeldAttempt(EntityUid uid, ArrivalsProtectComponent component, WeldableAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInteractUsing(EntityUid uid, ArrivalsProtectComponent component, InteractUsingEvent args)
    {
        args.Handled = true;
    }

    private void OnArrivalsStartup2(EntityUid uid, ArrivalsShuttleComponent component, ComponentAdd args)
    {
        ProcceGrid(uid);
    }

    private void OnArrivalsStartup(EntityUid uid, ArrivalsSourceComponent component, ComponentStartup args)
    {
        ProcceGrid(uid);
    }

    private void ProcceGrid(EntityUid uid){
        EntityUid Grid;
        if(_mapManager.IsGrid(uid)){
            Grid = uid;
        }else if(_mapManager.IsMap(uid)){
            return;
        }else{
            Grid = Transform(uid).GridUid ?? EntityUid.Invalid;
        }
        if(!Grid.IsValid()){
            return;
        }

        var transformQuery = GetEntityQuery<TransformComponent>();

        RecursiveGodmode(transformQuery, Grid);
    }

    private void ProcessGodmode(EntityUid uid){
        if(_tagSystem.HasAnyTag(uid,"Wall","Window")){
            _godmodeSystem.EnableGodmode(uid);
        }else if(TryComp<DoorComponent>(uid, out var DoorComp)){
            _godmodeSystem.EnableGodmode(uid);
            DoorComp.PryingQuality = "None";
            EnsureComp<ArrivalsProtectComponent>(uid);

            if(TryComp<AirlockComponent>(uid, out var airlockComponent)){
                _tagSystem.TryAddTag(uid,"EmagImmune");
            }
        }
    }

    private void RecursiveGodmode(EntityQuery<TransformComponent> transformQuery, EntityUid uid){

        ProcessGodmode(uid);

        var enumerator = transformQuery.GetComponent(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            RecursiveGodmode(transformQuery, child.Value);
        }
    }

    private void OnArrivalsStartup1(EntityUid uid, ArrivalsSourceComponent component, ComponentStartup args)
    {
        ProcceGrid(uid);
    }
}
