using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Arrivals.CentComm;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.CentComm;

[TestFixture]
[TestOf(typeof(CentCommSpawnSystem))]
public sealed class CentCommSpawnTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
        InLobby = true,
    };

    private static void AssertJobsMatch(
        IReadOnlyDictionary<ProtoId<JobPrototype>, int[]> actual,
        IReadOnlyDictionary<ProtoId<JobPrototype>, int[]> expected)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count));

        foreach (var (job, slots) in expected)
        {
            Assert.That(actual.TryGetValue(job, out var actualSlots), Is.True, $"Missing job {job}");
            Assert.That(actualSlots, Is.EqualTo(slots));
        }
    }

    /// <summary>
    /// The pool always provides one connected client in the lobby. Dummy sessions make up the rest.
    /// </summary>
    private async Task PrepareReadyPlayers(int readyPlayerCount)
    {
        Assert.That(readyPlayerCount, Is.GreaterThanOrEqualTo(1));

        var ticker = Server.System<GameTicker>();

        await Server.AddDummySessions(readyPlayerCount - 1);
        await Pair.RunTicksSync(10);

        ticker.ToggleReadyAll(true);
        Assert.That(ticker.ReadyPlayerCount(), Is.EqualTo(readyPlayerCount));
    }

    private async Task PrepareConnectedPlayers(int connectedCount, int readyCount)
    {
        Assert.That(connectedCount, Is.GreaterThanOrEqualTo(readyCount));
        Assert.That(readyCount, Is.GreaterThanOrEqualTo(1));

        var ticker = Server.System<GameTicker>();
        var playerMan = Server.ResolveDependency<IPlayerManager>();

        await Server.AddDummySessions(connectedCount - 1);
        await Pair.RunTicksSync(10);

        ticker.ToggleReadyAll(false);

        var ready = 0;
        foreach (var session in playerMan.Sessions)
        {
            ticker.ToggleReady(session, ready < readyCount);
            ready++;
        }

        Assert.That(playerMan.PlayerCount, Is.EqualTo(connectedCount));
        Assert.That(ticker.ReadyPlayerCount(), Is.EqualTo(readyCount));
    }

    private async Task<EntityUid> SpawnCentCommStation()
    {
        EntityUid station = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            station = Server.EntMan.SpawnEntity("NanotrasenCentralCommand", MapCoordinates.Nullspace);
        });

        await Server.WaitRunTicks(1);
        return station;
    }

    [TestCase(5)]
    [TestCase(19)]
    public async Task backmen_CentCommJobs_LowPop_ClearsJobSlots(int playerCount)
    {
        await PrepareReadyPlayers(playerCount);
        var station = await SpawnCentCommStation();

        await Server.WaitAssertion(() =>
        {
            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);
            var jobs = Server.EntMan.GetComponent<StationJobsComponent>(station);

            Assert.That(director.isLowPop, Is.True);
            Assert.That(jobs.SetupAvailableJobs, Is.Empty);
            Assert.That(jobs.JobList, Is.Empty);
            Assert.That(jobs.TotalJobs, Is.Zero);
        });
    }

    [TestCase(20)]
    [TestCase(25)]
    [TestCase(39)]
    public async Task backmen_CentCommJobs_MedPop_SetsMedJobSlots(int playerCount)
    {
        await PrepareReadyPlayers(playerCount);
        var station = await SpawnCentCommStation();

        await Server.WaitAssertion(() =>
        {
            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);

            Assert.That(director.isLowPop, Is.False);
            AssertJobsMatch(
                Server.EntMan.GetComponent<StationJobsComponent>(station).SetupAvailableJobs,
                director.SetupMedAvailableJobs);
        });
    }

    [TestCase(40)]
    [TestCase(45)]
    public async Task backmen_CentCommJobs_HighPop_SetsHighJobSlots(int playerCount)
    {
        await PrepareReadyPlayers(playerCount);
        var station = await SpawnCentCommStation();

        await Server.WaitAssertion(() =>
        {
            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);

            Assert.That(director.isLowPop, Is.False);
            AssertJobsMatch(
                Server.EntMan.GetComponent<StationJobsComponent>(station).SetupAvailableJobs,
                director.SetupHighAvailableJobs);
        });
    }

    [Test]
    public async Task backmen_CentCommJobs_ReadyPlayerCount_IgnoresUnreadyConnections()
    {
        await PrepareConnectedPlayers(connectedCount: 45, readyCount: 5);
        var station = await SpawnCentCommStation();

        await Server.WaitAssertion(() =>
        {
            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);
            var jobs = Server.EntMan.GetComponent<StationJobsComponent>(station);

            Assert.That(director.isLowPop, Is.True);
            Assert.That(jobs.SetupAvailableJobs, Is.Empty);
            Assert.That(jobs.JobList, Is.Empty);
            Assert.That(jobs.TotalJobs, Is.Zero);
        });
    }

    [Test]
    public async Task backmen_CentCommJobs_RoundStarting_ReconfiguresFromReadyPlayers()
    {
        var ticker = Server.System<GameTicker>();
        var centCommSpawn = Server.System<CentCommSpawnSystem>();
        var playerMan = Server.ResolveDependency<IPlayerManager>();

        await PrepareReadyPlayers(45);
        var station = await SpawnCentCommStation();

        await Server.WaitAssertion(() =>
        {
            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);
            var jobs = Server.EntMan.GetComponent<StationJobsComponent>(station);

            Assert.That(director.isLowPop, Is.False);
            AssertJobsMatch(jobs.SetupAvailableJobs, director.SetupHighAvailableJobs);
        });

        ticker.ToggleReadyAll(false);

        var ready = 0;
        foreach (var session in playerMan.Sessions)
        {
            ticker.ToggleReady(session, ready < 5);
            ready++;
        }

        Assert.That(ticker.ReadyPlayerCount(), Is.EqualTo(5));

        await Server.WaitAssertion(() =>
        {
            centCommSpawn.TriggerRoundStartingJobConfiguration();

            var director = Server.EntMan.GetComponent<StationCentCommDirectorComponent>(station);
            var jobs = Server.EntMan.GetComponent<StationJobsComponent>(station);

            Assert.That(director.isLowPop, Is.True);
            Assert.That(jobs.SetupAvailableJobs, Is.Empty);
            Assert.That(jobs.JobList, Is.Empty);
            Assert.That(jobs.TotalJobs, Is.Zero);
        });
    }
}
