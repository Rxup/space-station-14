using System.Linq;
using Content.Server.Ghost.Components;
using Content.Server.Magic;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Server.Stunnable;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Backmen.Species.Shadowkin.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinDarkSwapSystem : EntitySystem
{
    [Dependency] private readonly ShadowkinPowerSystem _power = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly ShadowkinDarkenSystem _darken = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MagicSystem _magic = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly EyeSystem _eye = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private EntityQuery<PsionicsDisabledComponent> _activePsionicsDisabled;
    private EntityQuery<StaminaComponent> _activeStamina;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinDarkSwapPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShadowkinDarkSwapPowerComponent, ComponentShutdown>(Shutdown);

        SubscribeLocalEvent<ShadowkinDarkSwapPowerComponent, ShadowkinDarkSwapEvent>(DarkSwap);

        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, ComponentStartup>(OnInvisStartup);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, ComponentShutdown>(OnInvisShutdown);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, MoveEvent>(OnMoveInInvis);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, DamageChangedEvent>(OnDamageInInvis);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, DispelledEvent>(OnDispelled);

        _activePsionicsDisabled = GetEntityQuery<PsionicsDisabledComponent>();
        _activeStamina = GetEntityQuery<StaminaComponent>();
    }

    private void OnDispelled(Entity<ShadowkinDarkSwappedComponent> ent, ref DispelledEvent args)
    {
        RemCompDeferred<ShadowkinDarkSwappedComponent>(ent);
        _stunSystem.TryParalyze(ent, TimeSpan.FromSeconds(5), true);
    }

    private void OnDamageInInvis(Entity<ShadowkinDarkSwappedComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        RemCompDeferred<ShadowkinDarkSwappedComponent>(ent);
        _stunSystem.TryParalyze(ent, TimeSpan.FromSeconds(3), false);
    }

    private void OnMoveInInvis(Entity<ShadowkinDarkSwappedComponent> ent, ref MoveEvent args)
    {
        if (!args.OldPosition.IsValid(EntityManager) ||
            !args.NewPosition.IsValid(EntityManager) ||
            !args.OldPosition.TryDistance(EntityManager, args.NewPosition, out var distance))
        {
            RemCompDeferred<ShadowkinDarkSwappedComponent>(ent);
            return;
        }

        if (distance == 0)
            return;

        if (!_activeStamina.TryGetComponent(ent, out var staminaComponent))
        {
            RemCompDeferred<ShadowkinDarkSwappedComponent>(ent);
            return;
        }

        _stamina.TakeStaminaDamage(ent, Math.Abs(distance), staminaComponent, visual: false, source: ent, chaosDamage: true);
        staminaComponent.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(staminaComponent.Cooldown);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ShadowkinDarkSwap = "ShadowkinDarkSwap";

    private void OnInit(Entity<ShadowkinDarkSwapPowerComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.ShadowkinDarkSwapAction, ShadowkinDarkSwap);
    }

    private void Shutdown(EntityUid uid, ShadowkinDarkSwapPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ShadowkinDarkSwapAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;

        var q = EntityQueryEnumerator<StaminaComponent, ShadowkinDarkSwappedComponent, StatusEffectsComponent>();
        while (q.MoveNext(out var uid, out var stamina, out var comp, out var statusEffectsComponent))
        {
            if (stamina.Critical || _activePsionicsDisabled.HasComponent(uid))
            {
                RemCompDeferred<ShadowkinDarkSwappedComponent>(uid);
                _stunSystem.TryParalyze(uid, TimeSpan.FromSeconds(5), true, statusEffectsComponent);
                continue;
            }

            if (currentTime > comp.NextStaminaDmg)
            {
                _stamina.TakeStaminaDamage(uid, 6, stamina, uid, chaosDamage: true);
                comp.NextStaminaDmg = currentTime + TimeSpan.FromSeconds(2);
                stamina.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(2);
                Dirty(uid,stamina);
            }
        }
    }

    private void DarkSwap(EntityUid uid, ShadowkinDarkSwapPowerComponent component, ShadowkinDarkSwapEvent args)
    {
        // Need power to drain power
        if (!HasComp<ShadowkinComponent>(args.Performer))
            return;

        // Don't activate abilities if handcuffed
        // TODO: Something like the Psionic Headcage to disable powers for Shadowkin
        if (HasComp<HandcuffComponent>(args.Performer) || HasComp<PsionicInsulationComponent>(args.Performer))
            return;


        var hasComp = HasComp<ShadowkinDarkSwappedComponent>(args.Performer);

        SetDarkened(
            args.Performer,
            !hasComp,
            !hasComp,
            !hasComp,
            true,
            args.StaminaCostOn,
            args.PowerCostOn,
            args.SoundOn,
            args.VolumeOn,
            args.StaminaCostOff,
            args.PowerCostOff,
            args.SoundOff,
            args.VolumeOff,
            args
        );

        _magic.Speak(args, false);
    }


    public void SetDarkened(
        EntityUid performer,
        bool addComp,
        bool invisible,
        bool pacify,
        bool darken,
        float staminaCostOn,
        float powerCostOn,
        SoundSpecifier soundOn,
        float volumeOn,
        float staminaCostOff,
        float powerCostOff,
        SoundSpecifier soundOff,
        float volumeOff,
        ShadowkinDarkSwapEvent? args
    )
    {
        var ent = GetNetEntity(performer);
        var ev = new ShadowkinDarkSwapAttemptEvent(performer);
        RaiseLocalEvent(ev);
        if (ev.Cancelled)
            return;

        if (addComp)
        {
            var needReturnPacify = HasComp<PacifiedComponent>(performer);
            var comp = EnsureComp<ShadowkinDarkSwappedComponent>(performer);
            comp.Invisible = invisible;
            comp.Pacify = pacify;
            comp.Darken = darken;
            comp.NeedReturnPacify = needReturnPacify;

            RaiseNetworkEvent(new ShadowkinDarkSwappedEvent(ent, true), performer);

            _audio.PlayPvs(soundOn, performer, AudioParams.Default.WithVolume(volumeOn));

            _power.TryAddPowerLevel(performer, -powerCostOn);
            _stamina.TakeStaminaDamage(performer, staminaCostOn);
        }
        else
        {
            RemComp<ShadowkinDarkSwappedComponent>(performer);
            RaiseNetworkEvent(new ShadowkinDarkSwappedEvent(ent, false), performer);

            _audio.PlayPvs(soundOff, performer, AudioParams.Default.WithVolume(volumeOff));

            _power.TryAddPowerLevel(performer, -powerCostOff);
            _stamina.TakeStaminaDamage(performer, staminaCostOff);
        }

        if (args != null)
            args.Handled = true;
    }


    private void OnInvisStartup(EntityUid uid, ShadowkinDarkSwappedComponent component, ComponentStartup args)
    {
        if (component.Pacify)
            EnsureComp<PacifiedComponent>(uid);

        if (component.Invisible)
        {
            SetVisibility(uid, true);
            SuppressFactions(uid, true);
        }
    }

    private void OnInvisShutdown(EntityUid uid, ShadowkinDarkSwappedComponent component, ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(uid))
        {
            if(!component.NeedReturnPacify) // не должны снимать цифизм -_-
                RemComp<PacifiedComponent>(uid);

            if (component.Invisible)
            {
                SetVisibility(uid, false);
                SuppressFactions(uid, false);
            }
        }
        
        component.Darken = false;

        foreach (var light in component.DarkenedLights.ToArray())
        {
            if (!TryComp<PointLightComponent>(light, out var pointLight) ||
                !TryComp<ShadowkinLightComponent>(light, out var shadowkinLight))
                continue;

            _darken.ResetLight(light, pointLight, shadowkinLight);
        }

        component.DarkenedLights.Clear();
    }


    public void SetVisibility(EntityUid uid, bool set)
    {
        // We require the visibility component for this to work
        var visibility = EnsureComp<VisibilityComponent>(uid);

        Entity<VisibilityComponent?> ent = (uid, visibility);

        if (set) // Invisible
        {
            // Allow the entity to see DarkSwapped entities
            if (TryComp(uid, out EyeComponent? eye))
                _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int) VisibilityFlags.DarkSwapInvisibility, eye);

            // Make other entities unable to see the entity unless also DarkSwapped
            _visibility.AddLayer(ent, (int) VisibilityFlags.DarkSwapInvisibility, false);
            _visibility.RemoveLayer(ent, (int) VisibilityFlags.Normal, false);
            _visibility.RefreshVisibility(uid);

            // If not a ghost, add a stealth shader to the entity
            if (!HasComp<GhostComponent>(uid))
                _stealth.SetVisibility(uid, 0.8f, EnsureComp<StealthComponent>(uid));
        }
        else // Visible
        {
            // Remove the ability to see DarkSwapped entities
            if (TryComp(uid, out EyeComponent? eye))
                _eye.SetVisibilityMask(uid, eye.VisibilityMask & ~(int) VisibilityFlags.DarkSwapInvisibility, eye);
            // Make other entities able to see the entity again
            _visibility.RemoveLayer(ent, (int) VisibilityFlags.DarkSwapInvisibility, false);
            _visibility.AddLayer(ent, (int) VisibilityFlags.Normal, false);
            _visibility.RefreshVisibility(uid);

            // Remove the stealth shader from the entity
            //if (!HasComp<GhostComponent>(uid))
            RemComp<StealthComponent>(uid);
            // Just to be sure...
            var stealth =  EnsureComp<StealthComponent>(uid);
            _stealth.SetVisibility(uid, 1f, stealth);
            RemComp<StealthComponent>(uid);
        }
    }

    /// <summary>
    ///     Remove existing factions on the entity and move them to the power component to add back when removed from The Dark
    /// </summary>
    /// <param name="uid">Entity to modify factions for</param>
    /// <param name="set">Add or remove the factions</param>
    public void SuppressFactions(EntityUid uid, bool set)
    {
        // We require the power component to keep track of the factions
        if (!TryComp<ShadowkinDarkSwapPowerComponent>(uid, out var component))
            return;

        if (set)
        {
            if (!TryComp<NpcFactionMemberComponent>(uid, out var factions))
                return;

            // Copy the suppressed factions to the power component
            component.SuppressedFactions = factions.Factions.Select(x=>x.Id).ToList();

            // Remove the factions from the entity
            foreach (var faction in factions.Factions)
            {
                _factions.RemoveFaction(uid, faction);
            }

            // Add status factions for The Dark to the entity
            foreach (var faction in component.AddedFactions)
            {
                _factions.AddFaction(uid, faction);
            }
        }
        else
        {
            // Remove the status factions from the entity
            foreach (var faction in component.AddedFactions)
            {
                _factions.RemoveFaction(uid, faction);
            }

            // Add the factions back to the entity
            foreach (var faction in component.SuppressedFactions)
            {
                _factions.AddFaction(uid, faction);
            }

            component.SuppressedFactions.Clear();
        }
    }
}
