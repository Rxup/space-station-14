using Content.Server.Backmen.Disease;
using Content.Shared.Backmen.Disease;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Disease;

[TestFixture]
[TestOf(typeof(DiseaseSystem))]
public sealed class DiseaseTest
{
    [Test]
    public async Task AddAllDiseases()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entManager = server.ResolveDependency<IEntityManager>();
        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var diseaseSystem = entSysManager.GetEntitySystem<DiseaseSystem>();

        var sickEntity = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            sickEntity = entManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            if(!entManager.HasComponent<DiseaseCarrierComponent>(sickEntity))
                Assert.Fail("MobHuman has not DiseaseCarrierComponent");
        });


        foreach (var diseaseProto in protoManager.EnumeratePrototypes<DiseasePrototype>())
        {
            await server.WaitAssertion(() =>
            {
                diseaseSystem.TryAddDisease(sickEntity, diseaseProto);
            });
            await server.WaitIdleAsync();
            server.RunTicks(5);
        }

        await pair.CleanReturnAsync();
    }
}
