using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Destructible;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Tools.Innate;

/// <summary>
///     Spawns a list unremovable tools in hands if possible. Used for drones,
///     borgs, or maybe even stuff like changeling armblades!
/// </summary>
public sealed partial class InnateToolSystem : EntitySystem
{
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> InnateDontDeleteTag = "InnateDontDelete";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InnateToolComponent, MapInitEvent>(OnMapInit, after: [typeof(InitialBodySystem)]);
        SubscribeLocalEvent<InnateToolComponent, InitialBodySpawnedEvent>(OnInitialBodySpawned);
        SubscribeLocalEvent<InnateToolComponent, HandCountChangedEvent>(OnHandCountChanged);
        SubscribeLocalEvent<InnateToolComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<InnateToolComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnMapInit(EntityUid uid, InnateToolComponent component, MapInitEvent args)
    {
        if (component.Tools.Count == 0)
            return;

        component.ToSpawn = EntitySpawnCollection.GetSpawns(component.Tools, _robustRandom);
        // start-backmen: innate-tool-auto-hands
        EnsureHandsForTools(uid, component);
        // end-backmen: innate-tool-auto-hands
        TryFillHands(uid, component);
    }

    private void OnInitialBodySpawned(EntityUid uid, InnateToolComponent component, InitialBodySpawnedEvent args)
    {
        // start-backmen: innate-tool-auto-hands
        EnsureHandsForTools(uid, component);
        // end-backmen: innate-tool-auto-hands
        TryFillHands(uid, component);
    }

    private void OnHandCountChanged(EntityUid uid, InnateToolComponent component, HandCountChangedEvent args)
    {
        TryFillHands(uid, component);
    }

    private void TryFillHands(EntityUid uid, InnateToolComponent component)
    {
        while (component.ToSpawn.Count > 0)
        {
            if (!TrySpawnOneInHand(uid, component))
                break;
        }
    }

    // start-backmen: innate-tool-auto-hands
    private void EnsureHandsForTools(EntityUid uid, InnateToolComponent component)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        var requiredHands = component.Tools.Count;
        var handIndex = 3;
        var handCount = 0;
        foreach (var _ in _sharedHandsSystem.EnumerateHands((uid, hands)))
            handCount++;

        while (handCount < requiredHands)
        {
            string handId;
            do
            {
                handId = $"innate-{handIndex++}";
            } while (_sharedHandsSystem.TryGetHand((uid, hands), handId, out _));

            _sharedHandsSystem.AddHand((uid, hands), handId, HandLocation.Middle);
            handCount++;
        }
    }
    // end-backmen: innate-tool-auto-hands

    private bool TrySpawnOneInHand(EntityUid uid, InnateToolComponent component)
    {
        if (component.ToSpawn.Count == 0)
            return false;

        var spawnCoord = Transform(uid).Coordinates;
        var toSpawn = component.ToSpawn[0];

        var item = Spawn(toSpawn, spawnCoord);
        AddComp<UnremoveableComponent>(item);
        if (!_sharedHandsSystem.TryPickupAnyHand(uid, item, checkActionBlocker: false))
        {
            QueueDel(item);
            return false;
        }

        component.ToSpawn.RemoveAt(0);
        component.ToolUids.Add(item);
        return true;
    }

    private void OnShutdown(EntityUid uid, InnateToolComponent component, ComponentShutdown args)
    {
        foreach (var tool in component.ToolUids)
        {
            RemComp<UnremoveableComponent>(tool);
        }
    }

    private void OnDestroyed(EntityUid uid, InnateToolComponent component, DestructionEventArgs args)
    {
        Cleanup(uid, component);
    }

    public void Cleanup(EntityUid uid, InnateToolComponent component)
    {
        foreach (var tool in component.ToolUids)
        {
            if (_tagSystem.HasTag(tool, InnateDontDeleteTag))
            {
                RemComp<UnremoveableComponent>(tool);
            }
            else
            {
                Del(tool);
            }

            if (TryComp<HandsComponent>(uid, out var hands))
            {
                foreach (var hand in hands.Hands.Keys)
                {
                    _sharedHandsSystem.TryDrop((uid, hands), hand, checkActionBlocker: false);
                }
            }
        }

        component.ToolUids.Clear();
    }
}
