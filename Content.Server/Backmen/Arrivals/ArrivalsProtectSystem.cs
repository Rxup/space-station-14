using Content.Server.Damage.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Tag;
using Content.Shared.Doors.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Interaction;
using Content.Server.Doors.Systems;
using Content.Server.Wires;
using Content.Server.Power.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.SubFloor;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.Construction;
using Content.Server.Emp;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Wires;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Prying.Components;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Arrivals;

public sealed partial class ArrivalsProtectSystem : SharedArrivalsProtectSystem
{
    [Dependency] private GodmodeSystem _godmodeSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private ApcSystem _apcSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArrivalsProtectGridComponent, MapInitEvent>(OnGridMapInit, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, MapInitEvent>(OnMapInit, after: new[]{ typeof(ArrivalsSystem)});

        SubscribeLocalEvent<ArrivalsProtectComponent, ComponentStartup>(OnStartup, after: new[]{ typeof(ArrivalsSystem)});

        SubscribeLocalEvent<ArrivalsSourceComponent, ComponentStartup>(OnArrivalsStartup, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsShuttleComponent, ComponentAdd>(OnArrivalsStartup2, after: new[]{ typeof(ArrivalsSystem)});

        SubscribeLocalEvent<ArrivalsProtectComponent, InteractUsingEvent>(OnInteractUsing, before: new []{typeof(DoorSystem), typeof(WiresSystem), typeof(CableSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, WeldableAttemptEvent>(OnWeldAttempt, before: new []{typeof(DoorSystem), typeof(WiresSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, ApcToggleMainBreakerAttemptEvent>(OnToggleApc, before: new[]{ typeof(EmpSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, BeforePryEvent>(OnTryPry);

        SubscribeLocalEvent<BuildAttemptEvent>(OnBuildAttemptEvent);
        SubscribeLocalEvent<ArrivalsProtectComponent, LinkAttemptEvent>(OnLinkAttempt);

        // start-backmen: arrivals-protect
        SubscribeLocalEvent<ArrivalsProtectComponent, WiresActionAttemptEvent>(OnWiresActionAttempt);
        // end-backmen: arrivals-protect
    }

    // start-backmen: arrivals-protect
    private void OnWiresActionAttempt(Entity<ArrivalsProtectComponent> ent, ref WiresActionAttemptEvent args)
    {
        if (args.Action == WiresAction.Mend)
            return;

        args.Cancelled = true;
    }
    // end-backmen: arrivals-protect

    private void OnGridMapInit(Entity<ArrivalsProtectGridComponent> ent, ref MapInitEvent args)
    {
        var q = AllEntityQuery<TransformComponent>();
        while (q.MoveNext(out var uid, out var transformComponent))
        {
            if(transformComponent.GridUid != ent.Owner)
                continue;

            if(ArrivalsProtectGridQuery.HasComp(uid))
                continue;

            EnsureComp<ArrivalsProtectComponent>(uid);
        }
    }

    private void OnTryPry(Entity<ArrivalsProtectComponent> ent, ref BeforePryEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLinkAttempt(EntityUid uid, ArrivalsProtectComponent component, LinkAttemptEvent args)
    {
        if (args.User == null) // AutoLink (and presumably future external linkers) have no user.
            return;
        args.Cancel();
    }

    private void OnToggleApc(EntityUid uid, ArrivalsProtectComponent component, ref ApcToggleMainBreakerAttemptEvent args)
    {
        args.Cancelled = true;
        if (!TryComp<ApcComponent>(uid, out var apcComponent))
        {
            return;
        }

        if (!apcComponent.MainBreakerEnabled)
        {
            _apcSystem.ApcToggleBreaker(uid,apcComponent);
        }
        // apcComponent.HasAccess = false;
    }

    private void OnBuildAttemptEvent(BuildAttemptEvent ev)
    {
        var grid = Transform(ev.Uid).GridUid;
        if (grid == null)
        {
            return;
        }

        if (ArrivalsProtectGridQuery.HasComp(grid.Value))
        {
            ev.Cancel();
        }
    }

    private void OnStartup(EntityUid uid, ArrivalsProtectComponent component, ComponentStartup args)
    {
        //EnsureComp<GodmodeComponent>(uid);
        //RemCompDeferred<DamageableComponent>(uid);
        //RemCompDeferred<MovedByPressureComponent>(uid);
        ProcessGodmode(uid);
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
        ProcessGrid(uid);
    }

    private void OnArrivalsStartup(EntityUid uid, ArrivalsSourceComponent component, ComponentStartup args)
    {
        ProcessGrid(uid);
    }

    private void ProcessGrid(EntityUid uid)
    {
        EntityUid grid;
        if(HasComp<MapGridComponent>(uid))
        {
            grid = uid;
        }
        else if(HasComp<MapComponent>(uid))
        {
            return;
        }
        else
        {
            grid = Transform(uid).GridUid ?? EntityUid.Invalid;
        }
        if(!grid.IsValid())
        {
            return;
        }

        EnsureComp<ArrivalsProtectGridComponent>(grid);

        var transformQuery = GetEntityQuery<TransformComponent>();

        RecursiveGodmode(transformQuery, grid);
    }

    private static readonly ProtoId<TagPrototype> EmagImmune = "EmagImmune";
    private static readonly ProtoId<TagPrototype> GasVent = "GasVent";
    private static readonly ProtoId<TagPrototype> GasScrubber = "GasScrubber";
    private static readonly ProtoId<TagPrototype> Wall = "Wall";
    private static readonly ProtoId<TagPrototype> Window = "Window";

    private void ProcessGodmode(EntityUid uid)
    {
        if (TryComp<GasMixerComponent>(uid, out var gasMinerComponent))
        {
            (gasMinerComponent as dynamic).Enabled = true;
        }
        if (TryComp<GasPressurePumpComponent>(uid, out var gasPressurePumpComponent))
        {
            gasPressurePumpComponent.Enabled = true;
        }

        if(TryComp<DoorComponent>(uid, out var doorComp))
        {
            //doorComp.PryingQuality = "None";
            EnsureComp<ArrivalsProtectComponent>(uid);

            if(HasComp<AirlockComponent>(uid))
            {
                _tagSystem.TryAddTag(uid,EmagImmune);
            }
        }
        else if(_tagSystem.HasAnyTag(uid,GasVent, GasScrubber))
        {
            EnsureComp<ArrivalsProtectComponent>(uid);
            RemCompDeferred<VentCritterSpawnLocationComponent>(uid);
        }
        else if( // basic elements
                _tagSystem.HasAnyTag(uid,Wall,Window) ||
                HasComp<PoweredLightComponent>(uid) ||
                HasComp<ApcComponent>(uid) ||
                HasComp<PowerSupplierComponent>(uid) ||
                HasComp<PowerNetworkBatteryComponent>(uid) ||
                HasComp<SurveillanceCameraComponent>(uid) ||
                HasComp<SubFloorHideComponent>(uid) ||
                HasComp<CableComponent>(uid) ||
                HasComp<GravityGeneratorComponent>(uid)
               )
        {
            EnsureComp<ArrivalsProtectComponent>(uid);
        }
    }

    private void RecursiveGodmode(EntityQuery<TransformComponent> transformQuery, EntityUid uid)
    {

        try
        {
            ProcessGodmode(uid);
        }
        catch(KeyNotFoundException)
        {
            //ignore
        }


        var enumerator = transformQuery.GetComponent(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            RecursiveGodmode(transformQuery, child);
        }
    }

    private void OnArrivalsStartup1(EntityUid uid, ArrivalsSourceComponent component, ComponentStartup args)
    {
        ProcessGrid(uid);
    }
}
