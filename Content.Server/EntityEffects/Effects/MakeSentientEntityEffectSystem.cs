using Content.Server.Backmen.Language;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Speech.Components;
using Content.Shared.Backmen.Language;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Makes this entity sentient. Allows ghost to take it over if it's not already occupied.
/// Optionally also allows this entity to speak.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class MakeSentientEntityEffectSystem : EntityEffectSystem<MetaDataComponent, MakeSentient>
{
// start-backmen: language
    private static readonly ProtoId<LanguagePrototype> GlobalHuman = "TauCetiBasic";
    [Dependency] private readonly LanguageSystem _languageSystem = default!;
// end-backmen: language

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<MakeSentient> args)
    {
        // Let affected entities speak normally to make this effect different from, say, the "random sentience" event
        // This also works on entities that already have a mind
        // We call this before the mind check to allow things like player-controlled mice to be able to benefit from the effect
        if (args.Effect.AllowSpeech)
        {
            RemComp<ReplacementAccentComponent>(entity);
            // TODO: Make MonkeyAccent a replacement accent and remove MonkeyAccent code-smell.
            RemComp<MonkeyAccentComponent>(entity);

            // start-bakcmen: language
            _languageSystem.AddLanguage(entity.Owner, GlobalHuman, true, true);
            _languageSystem.SetLanguage(entity.Owner, GlobalHuman);
            // end-backmen: language
        }

        // Stops from adding a ghost role to things like people who already have a mind
        if (TryComp<MindContainerComponent>(entity, out var mindContainer) && mindContainer.HasMind)
            return;

        // Don't add a ghost role to things that already have ghost roles
        if (TryComp(entity, out GhostRoleComponent? ghostRole))
            return;

        ghostRole = AddComp<GhostRoleComponent>(entity);
        EnsureComp<GhostTakeoverAvailableComponent>(entity);

        ghostRole.RoleName = entity.Comp.EntityName;
        ghostRole.RoleDescription = Loc.GetString("ghost-role-information-cognizine-description");
    }
}
