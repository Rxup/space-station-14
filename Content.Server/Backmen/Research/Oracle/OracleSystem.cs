using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Research.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Mobs.Components;
using Content.Server.Chat.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Botany;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Chat;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityTable;
using Content.Shared.Interaction.Events;
using Content.Shared.Materials;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Research.Oracle;

public sealed class OracleSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;


    [ValidatePrototypeId<ReagentPrototype>]
    public readonly IReadOnlyList<ProtoId<ReagentPrototype>> RewardReagents = new ProtoId<ReagentPrototype>[]
    {
        "LotophagoiOil", "LotophagoiOil", "LotophagoiOil", "LotophagoiOil", "LotophagoiOil", "Wine", "Blood", "Ichor"
    };

    [ViewVariables(VVAccess.ReadWrite)]
    public readonly IReadOnlyList<LocId> DemandMessages = new LocId[]
    {
        "oracle-demand-1",
        "oracle-demand-2",
        "oracle-demand-3",
        "oracle-demand-4",
        "oracle-demand-5",
        "oracle-demand-6",
        "oracle-demand-7",
        "oracle-demand-8",
        "oracle-demand-9",
        "oracle-demand-10",
        "oracle-demand-11",
        "oracle-demand-12"
    };

    public readonly IReadOnlyList<string> RejectMessages = new[]
    {
        "ἄγνοια",
        "υλικό",
        "ἀγνωσία",
        "γήινος",
        "σάκλας"
    };

    [ValidatePrototypeId<EntityPrototype>]
    public readonly IReadOnlyList<EntProtoId> BlacklistedProtos = new EntProtoId[]
    {
        "MobTomatoKiller",
        "Drone",
        "QSI",
        "HandTeleporter",
        "BluespaceBeaker",
        "ClothingBackpackHolding",
        "ClothingBackpackSatchelHolding",
        "ClothingBackpackDuffelHolding",
        "TrashBagOfHolding",
        "BluespaceCrystal",
        "InsulativeHeadcage",
        "CrystalNormality",
        "BodyBagFolded",
        "BodyBag",
        "LockboxDecloner",
        "MopBucket",
        "FoodOnionRed",
        "FoodGatfruit",
        "TargetHuman",
        "TargetSyndicate",
        "TargetClown",
        "Beaker",
        "LargeBeaker",
        "CryostasisBeaker",
        "Dropper",
        "Syringe",
        "ChemistryEmptyBottle01",
        "DrinkMug",
        "DrinkMugMetal",
        "DrinkGlass",
        "Bucket",
        "SprayBottle",
        "MegaSprayBottle",
        "ShellTranquilizer",
        "ShellSoulbreaker",
        "FireExtinguisher",
        "ClothingBackpackWaterTank",

        // Mech non-items and items (mech stuff is all expensive)
        "RipleyHarness",
        "RipleyLArm",
        "RipleyLLeg",
        "RipleyRLeg",
        "RipleyRArm",
        "RipleyChassis",
        "MechEquipmentGrabber",
    };

    [ValidatePrototypeId<EntityTablePrototype>]
    private const string ResearchDisk5000 = "MaintToolsTable";

    [ValidatePrototypeId<EntityPrototype>]
    private const string CrystalNormality = "CrystalNormality";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var q = EntityQueryEnumerator<OracleComponent>();
        while (q.MoveNext(out var owner, out var oracle))
        {
            oracle.Accumulator += frameTime;
            oracle.BarkAccumulator += frameTime;
            if (oracle.BarkAccumulator >= oracle.BarkTime.TotalSeconds)
            {
                oracle.BarkAccumulator = 0;
                var message = Loc.GetString(_random.Pick(DemandMessages), ("item", oracle.DesiredPrototype.Name))
                    .ToUpper();
                _chat.TrySendInGameICMessage(owner, message, InGameICChatType.Speak, false);
            }

            if (oracle.Accumulator >= oracle.ResetTime.TotalSeconds)
            {
                oracle.LastDesiredPrototype = oracle.DesiredPrototype;
                NextItem(oracle);
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OracleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<OracleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<OracleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<OracleComponent, SuicideEvent>(OnSuicide);
    }

    private void OnSuicide(Entity<OracleComponent> ent, ref SuicideEvent args)
    {
        var xform = Transform(ent);
        var spawnPos = new EntityCoordinates(xform.Coordinates.EntityId,
            xform.Coordinates.Position + xform.LocalRotation.ToWorldVec());

        Spawn(ResearchDisk5000, spawnPos);

        DispenseLiquidReward(ent);

        var i = _random.Next(1, 4);

        while (i != 0)
        {
            EntityManager.SpawnEntity(CrystalNormality, spawnPos);
            i--;
        }

        NextItem(ent.Comp);
    }

    private void OnInit(EntityUid uid, OracleComponent component, ComponentInit args)
    {
        NextItem(component);
    }

    private void OnInteractHand(EntityUid uid, OracleComponent component, InteractHandEvent args)
    {
        if (!HasComp<PotentialPsionicComponent>(args.User) || HasComp<PsionicInsulationComponent>(args.User))
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var message = Loc.GetString("oracle-current-item", ("item", component.DesiredPrototype.Name));

        var messageWrap = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
            ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")),
            ("message", message));

        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Telepathic,
            message,
            messageWrap,
            uid,
            false,
            actor.PlayerSession.Channel,
            Color.PaleVioletRed);

        if (component.LastDesiredPrototype != null)
        {
            var message2 = Loc.GetString("oracle-previous-item", ("item", component.LastDesiredPrototype.Name));
            var messageWrap2 = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
                ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")),
                ("message", message2));

            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Telepathic,
                message2,
                messageWrap2,
                uid,
                false,
                actor.PlayerSession.Channel,
                Color.PaleVioletRed);
        }
    }

    private void OnInteractUsing(EntityUid uid, OracleComponent component, InteractUsingEvent args)
    {
        if (HasComp<MobStateComponent>(args.Used))
            return;

        if (!TryComp<MetaDataComponent>(args.Used, out var meta))
            return;

        if (meta.EntityPrototype == null)
            return;

        var validItem = CheckValidity(meta.EntityPrototype, component.DesiredPrototype);

        var nextItem = true;

        if (component.LastDesiredPrototype != null &&
            CheckValidity(meta.EntityPrototype, component.LastDesiredPrototype))
        {
            nextItem = false;
            validItem = true;
            component.LastDesiredPrototype = null;
        }

        if (!validItem)
        {
            if (!HasComp<RefillableSolutionComponent>(args.Used))
                _chat.TrySendInGameICMessage(uid, _random.Pick(RejectMessages), InGameICChatType.Speak, true);
            return;
        }

        QueueDel(args.Used);
        var pos = Transform(args.User).Coordinates;

        foreach (var item in _entityTable
                     .GetSpawns(_prototypeManager.Index<EntityTablePrototype>(ResearchDisk5000).Table))
        {
            Spawn(item, pos);
        }

        DispenseLiquidReward(uid);

        if (nextItem)
            NextItem(component);
    }

    private bool CheckValidity(EntityPrototype given, EntityPrototype target)
    {
        // 1: directly compare Names
        // name instead of ID because the oracle asks for them by name
        // this could potentially lead to like, labeller exploits maybe but so far only mob names can be fully player-set.
        if (given.Name == target.Name)
            return true;

        return false;
    }

    private void DispenseLiquidReward(EntityUid uid)
    {
        if (!_solutionSystem.TryGetSolution(uid,
                OracleComponent.SolutionName,
                out var fountainEnt,
                out var fountainSol))
            return;

        var allReagents = _prototypeManager.EnumeratePrototypes<ReagentPrototype>()
            .Where(x => !x.Abstract)
            .Select(x => x.ID)
            .ToList();

        var amount = 20 + _random.Next(1, 30) + (_glimmerSystem.Glimmer / 10f);
        amount = (float) Math.Round(amount);

        var sol = new Solution();
        string reagent;

        if (_random.Prob(0.2f))
        {
            reagent = _random.Pick(allReagents);
        }
        else
        {
            reagent = _random.Pick(RewardReagents);
        }

        sol.AddReagent(reagent, amount);

        _solutionSystem.TryMixAndOverflow(fountainEnt.Value, sol, fountainSol.MaxVolume, out var overflowing);

        if (overflowing != null && overflowing.Volume > 0)
            _puddleSystem.TrySpillAt(uid, overflowing, out var _);
    }

    private void NextItem(OracleComponent component)
    {
        component.Accumulator = 0;
        component.BarkAccumulator = 0;
        var protoString = GetDesiredItem();
        if (_prototypeManager.TryIndex<EntityPrototype>(protoString, out var proto))
            component.DesiredPrototype = proto;
        else
            Log.Error("Oracle can't index prototype " + protoString);
    }

    private string GetDesiredItem()
    {
        return _random.Pick(GetAllProtos());
    }

    public List<string> GetAllProtos()
    {
        var allTechs = _prototypeManager.EnumeratePrototypes<TechnologyPrototype>();
        var allRecipes = new List<string>();

        foreach (var tech in allTechs)
        {
            foreach (var recipe in tech.RecipeUnlocks)
            {
                var recipeProto = _prototypeManager.Index(recipe);
                if (recipeProto.Result != null)
                    allRecipes.Add(recipeProto.Result);
            }
        }

        var allPlants = _prototypeManager.EnumeratePrototypes<SeedPrototype>()
            .Select(x => x.ProductPrototypes[0])
            .Where( x=>!x.StartsWith("FloorTile"))
            .ToList();
        var allProtos = allRecipes.Concat(allPlants).ToList();
        foreach (var proto in BlacklistedProtos)
        {
            allProtos.Remove(proto);
        }

        return allProtos;
    }

    public bool GibBody(EntityUid uid, EntityUid item, OracleComponent? oracleComponent = null)
    {
        if (!Resolve(uid, ref oracleComponent, false))
        {
            return false;
        }
        _body.GibBody(item, false);
        _appearance.SetData(uid, RecyclerVisuals.Bloody, true);

        var xform = Transform(uid);
        var spawnPos = new EntityCoordinates(xform.Coordinates.EntityId,
            xform.Coordinates.Position + xform.LocalRotation.ToWorldVec());

        Spawn(ResearchDisk5000, spawnPos);

        DispenseLiquidReward(uid);

        var i = _random.Next(1, 4);

        while (i != 0)
        {
            EntityManager.SpawnEntity(CrystalNormality, spawnPos);
            i--;
        }

        NextItem(oracleComponent);

        return true;
    }
}
