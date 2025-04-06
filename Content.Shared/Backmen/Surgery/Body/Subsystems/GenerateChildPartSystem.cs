using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using System.Numerics;
using Content.Shared.Backmen.Surgery.Wounds;

namespace Content.Shared.Backmen.Surgery.Body.Subsystems;

public sealed class GenerateChildPartSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenerateChildPartComponent, WoundableAttachedEvent>(OnWoundableAttached);
        SubscribeLocalEvent<GenerateChildPartComponent, WoundableDetachedEvent>(OnWoundableDetached);
    }

    private void OnWoundableAttached(EntityUid uid, GenerateChildPartComponent component, ref WoundableAttachedEvent args)
    {
        CreatePart(uid, component);
    }

    private void OnWoundableDetached(EntityUid uid, GenerateChildPartComponent component, ref WoundableDetachedEvent args)
    {
        if (_net.IsServer)
            QueueDel(component.ChildPart);
    }

    private void CreatePart(EntityUid uid, GenerateChildPartComponent component)
    {
        if (!TryComp(uid, out BodyPartComponent? partComp)
            || partComp.Body is null
            || component.Active)
            return;

        // I pinky swear to also move this to the server side properly next update :)
        if (!_net.IsServer)
            return;

        var childPart = Spawn(component.Id, new EntityCoordinates(partComp.Body.Value, Vector2.Zero));

        if (!TryComp(childPart, out BodyPartComponent? childPartComp))
            return;

        // TODO: Refactor this fucking shit
        var slotName = _bodySystem.GetSlotFromBodyPart(childPartComp);
        _bodySystem.TryCreatePartSlot(uid, slotName, childPartComp.PartType, out var _);
        _bodySystem.AttachPart(uid, slotName, childPart, partComp, childPartComp);
        component.ChildPart = childPart;
        component.Active = true;
        Dirty(childPart, childPartComp);
    }
}
