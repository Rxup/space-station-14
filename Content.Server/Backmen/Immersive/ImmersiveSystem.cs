using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Backmen.Telescope;
using Content.Shared.Administration;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Immersive;

public sealed class ImmersiveSystem : EntitySystem
{
    [Dependency] private readonly SharedTelescopeSystem _telescope = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;


    private bool _immersiveEnabled;
    private const float TelescopeDivisor = 0.4f; // 2 tiles further than normal
    private const float TelescopeLerpAmount = 0.1f; // Looks nice.

    private EntityQuery<ContentEyeComponent> _eyeQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImmersiveComponent, MapInitEvent>(OnPlayerSpawn);

        Subs.CVar(_configurationManager, CCVars.ImmersiveEnabled, OnValueChanged, true);

        _console.RegisterCommand("setImmersive_bkm", SetImmersiveCommand);
        _eyeQuery = GetEntityQuery<ContentEyeComponent>();
    }

    private void OnValueChanged(bool value)
    {
        _immersiveEnabled = value;
        if (value)
        {
            OnStarted();
        }
        else
        {
            Ended();
        }
    }

    [AdminCommand(AdminFlags.Fun)]
    private void SetImmersiveCommand(IConsoleShell shell, string str, string[] args)
    {
        _configurationManager.SetCVar(CCVars.ImmersiveEnabled, !_immersiveEnabled);
        shell.WriteLine($"Immersive set in {_immersiveEnabled}");
        _adminLog.Add(LogType.AdminMessage,
            LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} immersive set in {_immersiveEnabled}");
    }

    private void OnStarted()
    {
        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent>();

        while (humans.MoveNext(out var entity, out _, out _))
        {
            AddTelescope(entity, TelescopeDivisor, TelescopeLerpAmount);
        }
    }

    private void Ended()
    {
        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent, TelescopeComponent>();
        while (humans.MoveNext(out var entity, out _, out _, out _))
        {
            RemCompDeferred<TelescopeComponent>(entity);
        }
    }

    private void AddTelescope(EntityUid human, float divisor, float lerpAmount)
    {
        _telescope.SetParameters((human, EnsureComp<TelescopeComponent>(human)), divisor, lerpAmount);
    }

    private void OnPlayerSpawn(Entity<ImmersiveComponent> ent, ref MapInitEvent ev)
    {
        if (!_immersiveEnabled)
            return;

        if (!_eyeQuery.HasComp(ent))
            return;

        AddTelescope(ent, TelescopeDivisor, TelescopeLerpAmount);
    }
}
