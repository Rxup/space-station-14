using System.Linq;
using Content.Server.Administration;
using Content.Server.Forensics;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class StationRecordCommand : ToolshedCommand
{
    private StationRecordsSystem? _recordsSystem;
    private InventorySystem? _inventory;

    [CommandImplementation("adduser")]
    public EntityUid Add(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ValueRef<ICommonSession> playerRef,
        [CommandArgument] Prototype<JobPrototype> job)
    {
        var player = playerRef.Evaluate(ctx);
        if (player is null || player.AttachedEntity is null ||
            !TryComp<StationRecordsComponent>(input, out var recordsComponent))
        {
            ctx.ReportError(new NotForServerConsoleError());
            return input;
        }

        var playerUid = player.AttachedEntity.Value;

        if (
            !TryComp<HumanoidAppearanceComponent>(playerUid, out var humanoidAppearanceComponent) ||
            !TryComp<MetaDataComponent>(playerUid, out var metaDataComponent)
        )
            return input;

        _recordsSystem ??= GetSys<StationRecordsSystem>();
        _inventory ??= GetSys<InventorySystem>();

        foreach (var item in _inventory.GetHandOrInventoryEntities(playerUid))
        {
            if (!TryComp(item, out PdaComponent? pda) || !TryComp<IdCardComponent>(pda.ContainedId, out var id))
                continue;

            TryComp<DnaComponent>(playerUid, out var dnaComponent);
            TryComp<FingerprintComponent>(playerUid, out var fingerprintComponent);

            _recordsSystem.CreateGeneralRecord(input,
                pda.ContainedId,
                metaDataComponent.EntityName,
                humanoidAppearanceComponent.Age,
                humanoidAppearanceComponent.Species,
                humanoidAppearanceComponent.Gender,
                job.Id,
                dnaComponent?.DNA,
                fingerprintComponent?.Fingerprint,
                new HumanoidCharacterProfile(
                    metaDataComponent.EntityName,
                    "",
                    humanoidAppearanceComponent.Species,
                    "",
                    humanoidAppearanceComponent.Age,
                    humanoidAppearanceComponent.Sex,
                    humanoidAppearanceComponent.Gender,
                    new HumanoidCharacterAppearance
                    {
                        HairStyleId = "",
                        HairColor = default,
                        FacialHairStyleId = "",
                        FacialHairColor = default,
                        EyeColor = default,
                        SkinColor = default,
                        Markings = []
                    },
                    SpawnPriorityPreference.None,
                    new Dictionary<ProtoId<JobPrototype>, JobPriority>
                    {
                        { SharedGameTicker.FallbackOverflowJob, JobPriority.High }
                    },
                    PreferenceUnavailableMode.SpawnAsOverflow,
                    [],
                    [],
                    new Dictionary<string, RoleLoadout>()
                ),
                recordsComponent
            );
        }

        return input;
    }

    [CommandImplementation("adduser")]
    public IEnumerable<EntityUid> Add(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] ValueRef<ICommonSession> playerRef,
        [CommandArgument] Prototype<JobPrototype> job
    )
        => input.Select(x => Add(ctx, x, playerRef, job));


    [CommandImplementation("remuser")]
    public EntityUid Rem(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ValueRef<ICommonSession> playerRef
    )
    {
        var player = playerRef.Evaluate(ctx);
        if (
            player is null ||
            player.AttachedEntity is null ||
            !TryComp<MetaDataComponent>(player.AttachedEntity.Value, out var metaDataComponent) ||
            !TryComp<StationRecordsComponent>(input, out var recordsComponent))
        {
            ctx.ReportError(new NotForServerConsoleError());
            return input;
        }

        var playerUid = player.AttachedEntity.Value;

        _recordsSystem ??= GetSys<StationRecordsSystem>();
        _inventory ??= GetSys<InventorySystem>();

        // when adding a record that already exists use the old one
        // this happens when respawning as the same character
        if (_recordsSystem.GetRecordByName(input, metaDataComponent.EntityName, recordsComponent) is { } id)
        {
            _recordsSystem.RemoveRecord(new StationRecordKey(id, input), recordsComponent);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities(playerUid))
        {
            if (TryComp(item, out PdaComponent? pda) &&
                TryComp(pda.ContainedId, out StationRecordKeyStorageComponent? keyStorage) &&
                keyStorage.Key is { } key &&
                _recordsSystem.TryGetRecord(key, out GeneralStationRecord? record))
            {
                if (TryComp(playerUid, out DnaComponent? dna) &&
                    dna.DNA != record.DNA)
                {
                    continue;
                }

                if (TryComp(playerUid, out FingerprintComponent? fingerPrint) &&
                    fingerPrint.Fingerprint != record.Fingerprint)
                {
                    continue;
                }

                _recordsSystem.RemoveRecord(key, recordsComponent);
            }
        }

        return input;
    }

    [CommandImplementation("remuser")]
    public IEnumerable<EntityUid> Rem(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] ValueRef<ICommonSession> playerRef
    )
        => input.Select(x => Rem(ctx, x, playerRef));
}
