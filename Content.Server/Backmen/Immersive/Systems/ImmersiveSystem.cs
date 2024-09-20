using System.Numerics;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._White.Telescope;
using Content.Shared.Administration;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Immersive.Systems;

public sealed class ImmersiveSystem : EntitySystem
{
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedTelescopeSystem _telescope = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    private bool _immersiveEnabled = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public float EyeModifier = 0.9f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float TelescopeDivisor = 0.25f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float TelescopeLerpAmount = 0.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);

        Subs.CVar(_configurationManager, CCVars.ImmersiveEnabled, OnValueChanged, true);

        _console.RegisterCommand("setImmersive_bkm", SetImmersiveCommand);
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
    private void SetImmersiveCommand(IConsoleShell shell, string argstr, string[] args)
    {
        _configurationManager.SetCVar(CCVars.ImmersiveEnabled, !_immersiveEnabled);
        shell.WriteLine($"Immersive set in {_immersiveEnabled}");
        _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} immersive set in {_immersiveEnabled}");
    }

    private void OnStarted()
    {
        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent>();

        while (humans.MoveNext(out var entity, out _, out var eye))
        {
            SetEyeZoom((entity, eye), EyeModifier);
            AddTelescope(entity, TelescopeDivisor, TelescopeLerpAmount);
        }
    }

    private void Ended()
    {
        var humans = EntityQueryEnumerator<HumanoidAppearanceComponent, ContentEyeComponent>();
        while (humans.MoveNext(out var entity, out _, out var eye))
        {
            SetEyeZoom((entity, eye), 1f);
            RemCompDeferred<TelescopeComponent>(entity);
        }
    }

    private void SetEyeZoom(Entity<ContentEyeComponent?> human, float modifier)
    {
        if (!Resolve(human, ref human.Comp))
            return;

        var vec = new Vector2(modifier);
        _eye.SetMaxZoom(human, vec, human.Comp);
        _eye.SetZoom(human, vec, eye: human.Comp);
    }

    private void AddTelescope(EntityUid human, float divisor, float lerpAmount)
    {
        _telescope.SetParameters((human, EnsureComp<TelescopeComponent>(human)), divisor, lerpAmount);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<ContentEyeComponent>(ev.Mob))
            return;

        if (!_immersiveEnabled)
            return;

        SetEyeZoom(ev.Mob, EyeModifier);
        AddTelescope(ev.Mob, TelescopeDivisor, TelescopeLerpAmount);
    }
}
