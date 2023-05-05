using Content.Server.Damage.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Tools;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Content.Shared.Tag;
using Content.Shared.Doors.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Interaction;
using Content.Server.Doors.Systems;
using Content.Server.Tools.Systems;
using Content.Server.Wires;
using Content.Server.Power.Components;
using Content.Server.Light.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.SubFloor;
using Content.Server.SurveillanceCamera;
using Content.Server.Construction.Components;
using Content.Server.Damage.Components;
using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Arrivals;

[RegisterComponent]
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

        SubscribeLocalEvent<ArrivalsProtectComponent, MapInitEvent>(OnMapInit, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, ComponentStartup>(OnStartup, after: new[]{ typeof(ArrivalsSystem)});

        SubscribeLocalEvent<ArrivalsSourceComponent, ComponentStartup>(OnArrivalsStartup, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsShuttleComponent, ComponentAdd>(OnArrivalsStartup2, after: new[]{ typeof(ArrivalsSystem)});



        SubscribeLocalEvent<ArrivalsProtectComponent, InteractUsingEvent>(OnInteractUsing, before: new []{typeof(DoorSystem), typeof(WiresSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, WeldableAttemptEvent>(OnWeldAttempt, before: new []{typeof(DoorSystem), typeof(WiresSystem)});



    }

    private void OnStartup(EntityUid uid, ArrivalsProtectComponent component, ComponentStartup args)
    {
        EnsureComp<GodmodeComponent>(uid);
        RemCompDeferred<DamageableComponent>(uid);
        RemCompDeferred<MovedByPressureComponent>(uid);
    }

    private void OnMapInit(EntityUid uid, ArrivalsProtectComponent component, MapInitEvent args)
    {
        _godmodeSystem.EnableGodmode(uid);
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
        if(TryComp<DoorComponent>(uid, out var DoorComp)){
            DoorComp.PryingQuality = "None";
            EnsureComp<ArrivalsProtectComponent>(uid);

            if(HasComp<AirlockComponent>(uid)){
                _tagSystem.TryAddTag(uid,"EmagImmune");
            }
        }else if(_tagSystem.HasAnyTag(uid,"GasVent", "GasScrubber")){
            EnsureComp<ArrivalsProtectComponent>(uid);
            RemCompDeferred<VentCritterSpawnLocationComponent>(uid);
        }else if( // basic elements
            _tagSystem.HasAnyTag(uid,"Wall","Window") ||
            HasComp<PoweredLightComponent>(uid) ||
            HasComp<CableComponent>(uid) ||
            HasComp<ApcComponent>(uid) ||
            HasComp<PowerSupplierComponent>(uid) ||
            HasComp<PowerNetworkBatteryComponent>(uid) ||
            HasComp<SurveillanceCameraComponent>(uid) ||
            HasComp<SubFloorHideComponent>(uid)
        ){
            EnsureComp<ArrivalsProtectComponent>(uid);
        }
    }

    private void RecursiveGodmode(EntityQuery<TransformComponent> transformQuery, EntityUid uid){

        try{
            ProcessGodmode(uid);
        }catch(KeyNotFoundException){
            //ignore
        }


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
