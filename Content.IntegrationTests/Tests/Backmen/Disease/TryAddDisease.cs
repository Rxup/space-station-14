using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Disease;
using Content.Shared.Backmen.Disease;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Disease;

[TestFixture]
[TestOf(typeof(DiseaseSystem))]
public sealed class DiseaseTest : GameTest
{
    [Test]
    public async Task AddAllDiseases()
    {
        var protoManager = Server.ResolveDependency<IPrototypeManager>();
        var entManager = Server.ResolveDependency<IEntityManager>();
        var entSysManager = Server.ResolveDependency<IEntitySystemManager>();
        var diseaseSystem = entSysManager.GetEntitySystem<DiseaseSystem>();

        var sickEntity = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            sickEntity = entManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            if(!entManager.HasComponent<DiseaseCarrierComponent>(sickEntity))
                Assert.Fail("MobHuman has not DiseaseCarrierComponent");
        });


        foreach (var diseaseProto in protoManager.EnumeratePrototypes<DiseasePrototype>())
        {
            await Server.WaitAssertion(() =>
            {
                diseaseSystem.TryAddDisease(sickEntity, diseaseProto.ID);
            });
            await Server.WaitIdleAsync();
            Server.RunTicks(5);
            await Server.WaitAssertion(() =>
            {
                if(!entManager.HasComponent<DiseasedComponent>(sickEntity))
                    Assert.Fail("MobHuman has not DiseasedComponent");
            });
            if (!entManager.TryGetComponent<DiseaseCarrierComponent>(sickEntity, out var diseaseCarrierComponent))
            {
                Assert.Fail("MobHuman has not DiseaseCarrierComponent");
            }

            if (diseaseCarrierComponent.Diseases.All(x => x.ID != diseaseProto.ID))
            {
                Assert.Fail("Disease not apply");
            }
            await Server.WaitAssertion(() =>
            {
                diseaseSystem.CureDisease((sickEntity,diseaseCarrierComponent), diseaseProto.ID);
            });
            await Server.WaitIdleAsync();
            Server.RunTicks(1);
            await Server.WaitAssertion(() =>
            {
                if (diseaseCarrierComponent.Diseases.Any(x => x.ID == diseaseProto.ID))
                {
                    Assert.Fail("Disease not remove");
                }

                var hasNotImmuny = diseaseCarrierComponent.PastDiseases.All(x => x != diseaseProto.ID);
                if (hasNotImmuny && diseaseProto.Infectious)
                {
                    Assert.Fail("Disease Infectious immunu not apply");
                }
                if (!hasNotImmuny && !diseaseProto.Infectious)
                {
                    Assert.Fail("Disease Not Infectious immunu apply");
                }
            });
        }
    }
}
