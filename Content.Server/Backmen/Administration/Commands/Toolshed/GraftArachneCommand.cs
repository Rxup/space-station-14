using System.Diagnostics;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

/// <summary>
/// Admin testing helper: amputate human legs and graft a full arachne body (cephalothorax, abdomen, eight legs).
/// </summary>
[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class GraftArachneCommand : ToolshedCommand
{
    private BkmBodySharedSystem? _bodySys;
    private BodySystem? _organBody;
    private OrganRelationInitializerSystem? _organRelations;
    private SharedVisualBodySystem? _visualBody;

    [CommandImplementation]
    public EntityUid? GraftArachne(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        if (!EntityManager.HasComponent<BodyComponent>(input))
        {
            ctx.ReportError(new NotBodyError());
            return null;
        }

        _bodySys ??= GetSys<BkmBodySharedSystem>();

        if (!_bodySys.BodySupportsArachneGraft(input))
        {
            ctx.ReportError(new FlatOrgansError());
            return null;
        }

        GraftArachne(input);
        return input;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> GraftArachne(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input)
    {
        return input.Select(x => GraftArachne(ctx, x)).Where(x => x is not null).Select(x => x!.Value);
    }

    private void GraftArachne(EntityUid body)
    {
        _bodySys ??= GetSys<BkmBodySharedSystem>();
        _organBody ??= GetSys<BodySystem>();
        _organRelations ??= GetSys<OrganRelationInitializerSystem>();

        if (EntityManager.TryGetComponent(body, out BodyComponent? bodyComp))
        {
            foreach (var category in new ProtoId<OrganCategoryPrototype>[] { "LegLeft", "LegRight" })
            {
                if (_organBody.TryGetOrganByCategory((body, bodyComp), category, out var leg))
                    _bodySys.RemoveOrgan(leg);
            }
        }

        TryInsertGraft(body, "BioSynthArachneFront", "ArachneFront");
        TryInsertGraft(body, "BioSynthArachneAbdomen", "ArachneAbdomen");
        InsertSpiderLegs(body, SurgeryBodyPartMapping.SpiderLegLeftSlots, "BioSynthSpiderLegLeft");
        InsertSpiderLegs(body, SurgeryBodyPartMapping.SpiderLegRightSlots, "BioSynthSpiderLegRight");

        if (EntityManager.TryGetComponent(body, out bodyComp))
        {
            _organRelations.WireGraftRelationships((body, bodyComp));
            _bodySys.SyncLegEntitiesForBody((body, bodyComp));
            ResyncOrganSexAfterGraft(body);
        }
    }

    /// <summary>
    /// Grafting does not change <see cref="HumanoidProfileComponent.Sex"/>, but organ layer states must be refreshed.
    /// </summary>
    private void ResyncOrganSexAfterGraft(EntityUid body)
    {
        if (!EntityManager.TryGetComponent(body, out HumanoidProfileComponent? humanoid)
            || !EntityManager.TryGetComponent(body, out VisualBodyComponent? visualBody))
        {
            return;
        }

        _visualBody ??= EntityManager.System<SharedVisualBodySystem>();

        if (!_visualBody.TryGatherMarkingsData((body, visualBody), null, out var profiles, out var markings, out var applied))
            return;

        profiles.TryGetValue("Torso", out var torsoProfile);
        profiles.TryGetValue("Head", out var headProfile);

        _visualBody.ApplyProfile(body, new OrganProfileData
        {
            Sex = humanoid.Sex,
            SkinColor = torsoProfile.SkinColor,
            EyeColor = headProfile.EyeColor,
        });
    }

    private void TryInsertGraft(
        EntityUid body,
        EntProtoId graftId,
        ProtoId<OrganCategoryPrototype> category)
    {
        if (!EntityManager.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        if (_organBody!.TryGetOrganByCategory((body, bodyComp), category, out _))
            return;

        var graft = EntityManager.SpawnEntity(graftId, MapCoordinates.Nullspace);
        _bodySys!.InsertOrganIntoBody(body, graft);
    }

    private void InsertSpiderLegs(
        EntityUid body,
        ProtoId<OrganCategoryPrototype>[] slots,
        EntProtoId legGraftId)
    {
        if (!EntityManager.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        foreach (var slot in slots)
        {
            if (_organBody!.TryGetOrganByCategory((body, bodyComp), slot, out _))
                continue;

            var leg = EntityManager.SpawnEntity(legGraftId, MapCoordinates.Nullspace);
            _organBody.SetOrganCategory(leg, slot);
            _bodySys!.InsertOrganIntoBody(body, leg);
        }
    }
}

public record struct NotBodyError : IConError
{
    public FormattedMessage DescribeInner() =>
        FormattedMessage.FromUnformatted(Loc.GetString("graftarachne-error-no-body"));

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}

public record struct FlatOrgansError : IConError
{
    public FormattedMessage DescribeInner() =>
        FormattedMessage.FromUnformatted(Loc.GetString("graftarachne-error-flat-organs"));

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
