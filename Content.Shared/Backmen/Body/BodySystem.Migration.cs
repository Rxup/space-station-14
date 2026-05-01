using System.Linq;
using Content.Shared.Backmen.Body;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    //[Dependency] private readonly EntityQuery<OrganComponent> _organQuery = default!;

    [Obsolete]
    public (BodyPartType partType, BodyPartSymmetry symmetry)? ConvertTargetBodyPart(TargetBodyPart targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Chest => (BodyPartType.Chest, BodyPartSymmetry.None),
            //TargetBodyPart.Groin => (BodyPartType.Groin, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => null
        };
    }

    [Obsolete]
    public (BodyPartType partType, BodyPartSymmetry symmetry)? ConvertTargetBodyPart(string category)
    {
        return category switch
        {
            "Head" => (BodyPartType.Head, BodyPartSymmetry.None),
            "Torso" => (BodyPartType.Chest, BodyPartSymmetry.None),
            //TargetBodyPart.Groin => (BodyPartType.Groin, BodyPartSymmetry.None),
            "ArmLeft" => (BodyPartType.Arm, BodyPartSymmetry.Left),
            "HandLeft" => (BodyPartType.Hand, BodyPartSymmetry.Left),
            "ArmRight" => (BodyPartType.Arm, BodyPartSymmetry.Right),
            "HandRight" => (BodyPartType.Hand, BodyPartSymmetry.Right),
            "LegLeft" => (BodyPartType.Leg, BodyPartSymmetry.Left),
            "FootLeft" => (BodyPartType.Foot, BodyPartSymmetry.Left),
            "LegRight" => (BodyPartType.Leg, BodyPartSymmetry.Right),
            "FootRight" => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => null
        };
    }

    public IEnumerable<EntityUid> GetBodyChildrenOfType(EntityUid ent, BodyPartType type, BodyComponent comp, BodyPartSymmetry symmetry)
    {
        foreach (var part in comp.Organs?.ContainedEntities  ?? [])
        {
            if(!_organQuery.TryComp(ent, out var organComp) || organComp.Category is null)
                continue;
            var info = ConvertTargetBodyPart(organComp.Category);
            if(info is null)
                continue;
            if (info.Value.partType == type && info.Value.symmetry == symmetry)
                yield return part;
        }
    }

    public IEnumerable<EntityUid> GetBodyChildren(EntityUid entity, BodyComponent? body = null)
    {
        if (!_bodyQuery.Resolve(entity, ref body))
            return [];
        return body.Organs?.ContainedEntities.Where(HasComp<VisualOrganComponent>) ?? [];
    }

    public IEnumerable<EntityUid> GetBodyOrgans(EntityUid entity, BodyComponent? body = null)
    {
        if (!_bodyQuery.Resolve(entity, ref body))
            return [];
        return body.Organs?.ContainedEntities.Where(x=>!HasComp<VisualOrganComponent>(x)) ?? [];
    }
}
