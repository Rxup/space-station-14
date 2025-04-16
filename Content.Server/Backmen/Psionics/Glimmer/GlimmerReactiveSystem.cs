using Content.Server.Backmen.Audio;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.Power.Components;
using Content.Server.Electrocution;
using Content.Server.Lightning;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Shared.Audio;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Psionics.Glimmer;
using Content.Shared.Verbs;
using Content.Shared.StatusEffect;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Construction.Components;
using Content.Shared.MassMedia.Components;
using Content.Shared.MassMedia.Systems;
using Content.Shared.Power;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Psionics.Glimmer
{
    public sealed class GlimmerReactiveSystem : EntitySystem
    {
        [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly ElectrocutionSystem _electrocutionSystem = default!;
        [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _sharedAmbientSoundSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly LightningSystem _lightning = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
        [Dependency] private readonly AnchorableSystem _anchorableSystem = default!;
        [Dependency] private readonly SharedDestructibleSystem _destructibleSystem = default!;
        //[Dependency] private readonly GhostSystem _ghostSystem = default!;
        //[Dependency] private readonly RevenantSystem _revenantSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly SharedPointLightSystem _pointLightSystem = default!;
        [Dependency] private readonly GameTicker _ticker = default!;


        public float Accumulator = 0;
        public const float UpdateFrequency = 15f;
        public float BeamCooldown = 3;
        public GlimmerTier LastGlimmerTier = GlimmerTier.Minimal;
        public bool GhostsVisible = false;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);

            SubscribeLocalEvent<GlimmerReactiveComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<GlimmerReactiveComponent, ComponentRemove>(OnComponentRemove);
            SubscribeLocalEvent<GlimmerReactiveComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<GlimmerReactiveComponent, GlimmerTierChangedEvent>(OnTierChanged);
            SubscribeLocalEvent<GlimmerReactiveComponent, GetVerbsEvent<AlternativeVerb>>(AddShockVerb);
            SubscribeLocalEvent<GlimmerReactiveComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<GlimmerReactiveComponent, DestructionEventArgs>(OnDestroyed);
            SubscribeLocalEvent<GlimmerReactiveComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        }

        /// <summary>
        /// Update relevant state on an Entity.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="component"></param>
        /// <param name="currentGlimmerTier"></param>
        /// <param name="glimmerTierDelta">The number of steps in tier
        /// difference since last update. This can be zero for the sake of
        /// toggling the enabled states.</param>
        private void UpdateEntityState(EntityUid uid,
            GlimmerReactiveComponent component,
            GlimmerTier currentGlimmerTier,
            int glimmerTierDelta)
        {
            var isEnabled = true;

            if (component.RequiresApcPower)
            {
                if (TryComp(uid, out ApcPowerReceiverComponent? apcPower))
                    isEnabled = apcPower.Powered;
            }

            _appearanceSystem.SetData(uid, GlimmerReactiveVisuals.GlimmerTier,
                isEnabled ? currentGlimmerTier : GlimmerTier.Minimal);

            // update ambient sound
            if (TryComp(uid, out GlimmerSoundComponent? glimmerSound)
                && TryComp(uid, out AmbientSoundComponent? ambientSoundComponent)
                && glimmerSound.GetSound(currentGlimmerTier, out SoundSpecifier? spec))
            {
                if (spec != null)
                    _sharedAmbientSoundSystem.SetSound(uid, spec, ambientSoundComponent);
            }

            if (component.ModulatesPointLight)
            {
                if (_pointLightSystem.TryGetLight(uid, out var pointLight))
                {
                    _pointLightSystem.SetEnabled(uid, isEnabled && currentGlimmerTier != GlimmerTier.Minimal,
                        pointLight);

                    // The light energy and radius are kept updated even when off
                    // to prevent the need to store additional state.
                    //
                    // Note that this doesn't handle edge cases where the
                    // PointLightComponent is removed while the
                    // GlimmerReactiveComponent is still present.
                    _pointLightSystem.SetEnergy(uid, glimmerTierDelta * component.GlimmerToLightEnergyFactor,
                        pointLight);
                    _pointLightSystem.SetRadius(uid, glimmerTierDelta * component.GlimmerToLightRadiusFactor,
                        pointLight);
                }
            }
        }

        /// <summary>
        /// Track when the component comes online so it can be given the
        /// current status of the glimmer tier, if it wasn't around when an
        /// update went out.
        /// </summary>
        private void OnMapInit(EntityUid uid, GlimmerReactiveComponent component, MapInitEvent args)
        {
            if (component.RequiresApcPower && !HasComp<ApcPowerReceiverComponent>(uid))
            {
                Log.Warning($"{ToPrettyString(uid)} had RequiresApcPower set to true but no ApcPowerReceiverComponent was found on init.");
            }

            if (component.ModulatesPointLight && !HasComp<PointLightComponent>(uid))
                Log.Warning($"{ToPrettyString(uid)} had ModulatesPointLight set to true but no PointLightComponent was found on init.");

            UpdateEntityState(uid, component, LastGlimmerTier, (int) LastGlimmerTier);
        }

        /// <summary>
        /// Reset the glimmer tier appearance data if the component's removed,
        /// just in case some objects can temporarily become reactive to the
        /// glimmer.
        /// </summary>
        private void OnComponentRemove(EntityUid uid, GlimmerReactiveComponent component, ComponentRemove args)
        {
            UpdateEntityState(uid, component, GlimmerTier.Minimal, -1 * (int) LastGlimmerTier);
        }

        /// <summary>
        /// If the Entity has RequiresApcPower set to true, this will force an
        /// update to the entity's state.
        /// </summary>
        private void OnPowerChanged(EntityUid uid, GlimmerReactiveComponent component, ref PowerChangedEvent args)
        {
            if (component.RequiresApcPower)
                UpdateEntityState(uid, component, LastGlimmerTier, 0);
        }

        /// <summary>
        ///     Enable / disable special effects from higher tiers.
        /// </summary>
        private void OnTierChanged(EntityUid uid,
            GlimmerReactiveComponent component,
            GlimmerTierChangedEvent args)
        {
            if (!TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                return;

            if (args.CurrentTier >= GlimmerTier.Dangerous)
            {
                if (!Transform(uid).Anchored)
                    AnchorOrExplode(uid);

                receiver.PowerDisabled = false;
                receiver.NeedsPower = false;
            }
            else
            {
                receiver.NeedsPower = true;
            }
        }

        private void AddShockVerb(EntityUid uid,
            GlimmerReactiveComponent component,
            GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                return;

            if (receiver.NeedsPower)
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    _sharedAudioSystem.PlayPvs(component.ShockNoises, args.User);
                    _electrocutionSystem.TryDoElectrocution(args.User, null, _glimmerSystem.Glimmer / 200,
                        TimeSpan.FromSeconds((float) _glimmerSystem.Glimmer / 100), false);
                },
                Icon =
                    new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
                Text = Loc.GetString("power-switch-component-toggle-verb"),
                Priority = -3
            };
            args.Verbs.Add(verb);
        }

        private void OnDamageChanged(EntityUid uid, GlimmerReactiveComponent component, DamageChangedEvent args)
        {
            if (args.Origin == null)
                return;

            if (!_random.Prob((float) _glimmerSystem.Glimmer / 1000))
                return;

            var tier = _glimmerSystem.GetGlimmerTier();
            if (tier < GlimmerTier.High)
                return;
            Beam(uid, args.Origin.Value, tier);
        }

        [ValidatePrototypeId<EntityPrototype>] private const string MaterialEssence = "MaterialBluespaceEssence1";
        private void OnDestroyed(EntityUid uid, GlimmerReactiveComponent component, DestructionEventArgs args)
        {
            Spawn(MaterialEssence, Transform(uid).Coordinates);

            var tier = _glimmerSystem.GetGlimmerTier();
            if (tier < GlimmerTier.High)
                return;

            var totalIntensity = (float) (_glimmerSystem.Glimmer * 2);
            var slope = 11 - _glimmerSystem.Glimmer / 100f;
            var maxIntensity = 20;

            var removed = (float) _glimmerSystem.Glimmer * _random.NextFloat(0.1f, 0.15f);
            _glimmerSystem.Glimmer -= (int) removed;
            BeamRandomNearProber(uid, _glimmerSystem.Glimmer / 350, _glimmerSystem.Glimmer / 50f);
            _explosionSystem.QueueExplosion(uid, "Default", totalIntensity, slope, maxIntensity);
        }

        private void OnUnanchorAttempt(EntityUid uid, GlimmerReactiveComponent component,
            UnanchorAttemptEvent args)
        {
            if (_glimmerSystem.GetGlimmerTier() >= GlimmerTier.Dangerous)
            {
                _sharedAudioSystem.PlayPvs(component.ShockNoises, args.User);
                _electrocutionSystem.TryDoElectrocution(args.User, null, _glimmerSystem.Glimmer / 200,
                    TimeSpan.FromSeconds((float) _glimmerSystem.Glimmer / 100), false);
                args.Cancel();
            }
        }

        [ValidatePrototypeId<StatusEffectPrototype>]
        private const string Electrocution = "Electrocution";

        private readonly HashSet<Entity<IComponent>> _entitySet = new();
        private readonly List<EntityUid> _entities = new();
        public void BeamRandomNearProber(EntityUid prober, int targets, float range = 10f)
        {
            var pos = Transform(prober).Coordinates.ToMap(EntityManager,_transformSystem);
            _entitySet.Clear();
            _entityLookupSystem.GetEntitiesInRange(typeof(StatusEffectsComponent),pos.MapId,pos.Position, range, _entitySet);
            _entityLookupSystem.GetEntitiesInRange(typeof(GlimmerReactiveComponent),pos.MapId,pos.Position, range, _entitySet);
            _entities.Clear();

            foreach (var target in _entitySet)
            {
                if (target.Comp is StatusEffectsComponent comp)
                {
                    if (!comp.AllowedEffects.Contains(Electrocution))
                    {
                        continue;
                    }
                }
                _entities.Add(target);
            }

            _random.Shuffle(_entities);
            foreach (var target in _entities)
            {
                if (targets <= 0)
                    return;

                Beam(prober, target, _glimmerSystem.GetGlimmerTier(), false);
                targets--;
            }
        }

        [ValidatePrototypeId<EntityPrototype>]
        private const string SuperchargedLightning = "SuperchargedLightning";
        [ValidatePrototypeId<EntityPrototype>]
        private const string HyperchargedLightning = "HyperchargedLightning";
        [ValidatePrototypeId<EntityPrototype>]
        private const string ChargedLightning = "ChargedLightning";

        private void Beam(EntityUid prober, EntityUid target, GlimmerTier tier, bool obeyCd = true)
        {
            if (obeyCd && BeamCooldown != 0)
                return;

            if (TerminatingOrDeleted(prober) || TerminatingOrDeleted(target))
                return;

            var lxform = Transform(prober).Coordinates;
            var txform = Transform(target).Coordinates;

            if (!lxform.TryDistance(EntityManager, txform, out var distance))
                return;

            if (distance > (_glimmerSystem.Glimmer / 100f))
                return;

            var beamproto = tier switch
            {
                GlimmerTier.Dangerous => SuperchargedLightning,
                GlimmerTier.Critical => HyperchargedLightning,
                _ => ChargedLightning
            };

            _lightning.ShootLightning(prober, target, beamproto);
            BeamCooldown += 3f;
        }

        private void AnchorOrExplode(EntityUid uid)
        {
            var xform = Transform(uid);
            if (xform.Anchored)
                return;

            if (!TryComp<PhysicsComponent>(uid, out var physics))
                return;

            var coordinates = xform.Coordinates;
            var gridUid = xform.GridUid;

            if (TryComp<MapGridComponent>(gridUid, out var grid))
            {
                var tileIndices = _mapSystem.TileIndicesFor(gridUid.Value, grid, coordinates);

                if (_anchorableSystem.TileFree(grid, tileIndices, physics.CollisionLayer, physics.CollisionMask) &&
                    _transformSystem.AnchorEntity(uid, xform))
                {
                    return;
                }
            }

            // Wasn't able to get a grid or a free tile, so explode.
            _destructibleSystem.DestroyEntity(uid);
        }

        private void Reset(RoundRestartCleanupEvent args)
        {
            Accumulator = 0;

            // It is necessary that the GlimmerTier is reset to the default
            // tier on round restart. This system will persist through
            // restarts, and an undesired event will fire as a result after the
            // start of the new round, causing modulatable PointLights to have
            // negative Energy if the tier was higher than Minimal on restart.
            LastGlimmerTier = GlimmerTier.Minimal;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            Accumulator += frameTime;
            BeamCooldown = Math.Max(0, BeamCooldown - frameTime);

            if (Accumulator > UpdateFrequency)
            {
                var currentGlimmerTier = _glimmerSystem.GetGlimmerTier();


                if (currentGlimmerTier != LastGlimmerTier)
                {
                    var glimmerTierDelta = (int) currentGlimmerTier - (int) LastGlimmerTier;
                    var ev = new GlimmerTierChangedEvent(LastGlimmerTier, currentGlimmerTier, glimmerTierDelta);

                    var reactiv = EntityQueryEnumerator<GlimmerReactiveComponent>();
                    while (reactiv.MoveNext(out var owner, out var reactive))
                    {
                        UpdateEntityState(owner, reactive, currentGlimmerTier, glimmerTierDelta);
                        RaiseLocalEvent(owner, ev);
                    }
                    var wrappedMessage = Loc.GetString(
                        "glimmer-change-notification",
                        ("last", LastGlimmerTier),
                        ("now", currentGlimmerTier),
                        ("delta", glimmerTierDelta > 0 ? $"+{glimmerTierDelta}" : glimmerTierDelta)
                    );
                    LastGlimmerTier = currentGlimmerTier;

                    var stationNews = EntityQueryEnumerator<StationNewsComponent>();
                    while (stationNews.MoveNext(out var station, out var stationNewsComponent))
                    {
                        var article = new NewsArticle();
                        article.Author = Loc.GetString("ent-SophicScribe");
                        article.Title = wrappedMessage;
                        article.Content = currentGlimmerTier switch
                        {
                            GlimmerTier.Minimal => Loc.GetString("glimmer-reading-minimal"),
                            GlimmerTier.Low => Loc.GetString("glimmer-reading-low"),
                            GlimmerTier.Moderate => Loc.GetString("glimmer-reading-moderate"),
                            GlimmerTier.High => Loc.GetString("glimmer-reading-high"),
                            GlimmerTier.Dangerous => Loc.GetString("glimmer-reading-dangerous"),
                            _ => Loc.GetString("glimmer-reading-critical"),
                        };
                        article.ShareTime = _ticker.RoundDuration();
                        stationNewsComponent.Articles.Add(article);
                        var args = new NewsArticlePublishedEvent(article);
                        var query = EntityQueryEnumerator<NewsReaderCartridgeComponent>();
                        while (query.MoveNext(out var readerUid, out _))
                        {
                            RaiseLocalEvent(readerUid, ref args);
                        }
                    }
                }

                var reactives = EntityQueryEnumerator<GlimmerReactiveComponent,TransformComponent>();
                while (reactives.MoveNext(out var owner, out _, out var transformComponent))
                {
                    if (currentGlimmerTier == GlimmerTier.Critical)
                        BeamRandomNearProber(owner, 1, 12);

                    if (currentGlimmerTier is GlimmerTier.Critical or GlimmerTier.Dangerous && !transformComponent.Anchored)
                        AnchorOrExplode(owner);

                }

                /*
                if (currentGlimmerTier == GlimmerTier.Critical)
                {
                    //_ghostSystem.MakeVisible(true);
                    //_revenantSystem.MakeVisible(true);
                    //GhostsVisible = true;
                    var reactives = EntityQueryEnumerator<SharedGlimmerReactiveComponent>();
                    while (reactives.MoveNext(out var owner, out _))
                    {
                        BeamRandomNearProber(owner, 1, 12);
                    }
                }
                else if (GhostsVisible == true)
                {
                    //_ghostSystem.MakeVisible(false);
                    //_revenantSystem.MakeVisible(false);
                    //GhostsVisible = false;
                }*/

                Accumulator = 0;
            }
        }
    }
}
