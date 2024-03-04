using Content.Server.Administration.Managers;
using Content.Server.Damage.Systems;
using Content.Server.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Content.Shared.Tag;
using Content.Shared.Doors.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Interaction;
using Content.Server.Doors.Systems;
using Content.Server.Wires;
using Content.Server.Power.Components;
using Content.Server.Light.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.SubFloor;
using Content.Server.SurveillanceCamera;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.Construction;
using Content.Server.Emp;
using Content.Server.Gravity;
using Content.Server.Power.EntitySystems;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;

namespace Content.Server.Backmen.Arrivals;

[RegisterComponent]
public sealed partial class ArrivalsProtectComponent : Component
{

}

public sealed class ArrivalsProtectSystem : SharedArrivalsProtectSystem
{
    [Dependency] private readonly GodmodeSystem _godmodeSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ApcSystem _apcSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArrivalsProtectComponent, MapInitEvent>(OnMapInit, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, ComponentStartup>(OnStartup, after: new[]{ typeof(ArrivalsSystem)});

        SubscribeLocalEvent<ArrivalsSourceComponent, ComponentStartup>(OnArrivalsStartup, after: new[]{ typeof(ArrivalsSystem)});
        SubscribeLocalEvent<ArrivalsShuttleComponent, ComponentAdd>(OnArrivalsStartup2, after: new[]{ typeof(ArrivalsSystem)});



        SubscribeLocalEvent<ArrivalsProtectComponent, InteractUsingEvent>(OnInteractUsing, before: new []{typeof(DoorSystem), typeof(WiresSystem), typeof(CableSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, WeldableAttemptEvent>(OnWeldAttempt, before: new []{typeof(DoorSystem), typeof(WiresSystem)});
        SubscribeLocalEvent<ArrivalsProtectComponent, ApcToggleMainBreakerAttemptEvent>(OnToggleApc, before: new[]{ typeof(EmpSystem)});

        SubscribeLocalEvent<BuildAttemptEvent>(OnBuildAttemptEvent);
        SubscribeLocalEvent<ArrivalsProtectComponent, LinkAttemptEvent>(OnLinkAttempt);
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
        apcComponent.HasAccess = false;
    }

    private void OnBuildAttemptEvent(BuildAttemptEvent ev)
    {
        var grid = Transform(ev.Uid).GridUid;
        if (grid == null)
        {
            return;
        }

        if (HasComp<ArrivalsProtectGridComponent>(grid.Value))
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
        if(_mapManager.IsGrid(uid))
        {
            grid = uid;
        }
        else if(_mapManager.IsMap(uid))
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
                _tagSystem.TryAddTag(uid,"EmagImmune");
            }
        }
        else if(_tagSystem.HasAnyTag(uid,"GasVent", "GasScrubber"))
        {
            EnsureComp<ArrivalsProtectComponent>(uid);
            RemCompDeferred<VentCritterSpawnLocationComponent>(uid);
        }
        else if( // basic elements
                _tagSystem.HasAnyTag(uid,"Wall","Window") ||
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
