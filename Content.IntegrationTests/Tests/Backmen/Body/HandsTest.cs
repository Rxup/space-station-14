using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Tools.Innate;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class HandsTest
{
    [Test]
    public async Task InnateToolTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var handsSys = server.EntMan.System<HandsSystem>();
        var compFactory = server.ResolveDependency<IComponentFactory>();


        var testMap = await pair.CreateTestMap();

        //Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        foreach (var proto in prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            var skip = false;
            InnateToolComponent toolComponent = null;
            await server.WaitAssertion(() =>
            {
                if (!proto.TryGetComponent(out toolComponent, compFactory))
                    skip = true;
            });

            if(skip)
                continue;

            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(proto.ID, testMap.MapCoords);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var handCount = handsSys.EnumerateHands(dummy).Count();
                Assert.That(handCount, Is.GreaterThanOrEqualTo(toolComponent.Tools.Count), $"hands {proto.ID}");
                server.EntMan.DeleteEntity(dummy);
            });
        }


        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllSpeciesHaveLegs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var bodySys = server.EntMan.System<BodySystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var bodyComp = server.EntMan.GetComponent<BodyComponent>(dummy);
                var legs = bodyComp.LegEntities;
                var legsCount = bodySys.GetBodyPartCount(dummy, BodyPartType.Leg);
                Assert.That(legsCount, Is.EqualTo(legs.Count));
                Assert.That(legsCount, Is.GreaterThanOrEqualTo(2), $"legs {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }

        await pair.CleanReturnAsync();
    }
    [Test]
    public async Task AllSpeciesHaveHands()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var handsSys = server.EntMan.System<HandsSystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var handCount = handsSys.EnumerateHands(dummy).Count();
                Assert.That(handCount, Is.GreaterThanOrEqualTo(2), $"hands {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }

        await pair.CleanReturnAsync();
    }
}
