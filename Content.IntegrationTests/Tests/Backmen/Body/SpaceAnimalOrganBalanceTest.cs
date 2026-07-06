using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.Rotting;
using Content.Server.Backmen.Body.Systems;
using Content.Server.Backmen.Surgery.Trauma.Systems;
using Content.Server.Medical;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class SpaceAnimalOrganBalanceTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId SpaceLungs = "OrganSpaceAnimalLungs";
    private static readonly EntProtoId SpaceHeart = "OrganSpaceAnimalHeart";
    private static readonly EntProtoId LungsImmunity = "StatusEffectSpaceLungsImmunity";
    private static readonly EntProtoId HeartImmunity = "StatusEffectSpaceHeartImmunity";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SpaceLungs_InHumanBody_GrantImmunityStatusEffect()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var human = entMan.SpawnEntity(MobHuman, map.MapCoords);
            var lungs = entMan.SpawnEntity(SpaceLungs, map.MapCoords);

            Assert.That(bodySys.InsertOrganIntoBody(human, lungs), Is.True);
            Assert.That(entMan.HasComponent<StatusEffectContainerComponent>(human), Is.True);

            var statusSys = entMan.System<StatusEffectsSystem>();
            Assert.That(statusSys.TryGetStatusEffect(human, LungsImmunity, out _), Is.True);
        });
    }

    [Test]
    public async Task DestroyedSpaceLungs_RemoveImmunityAndShowAnalyzerAlert()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var trauma = entMan.System<ServerTraumaSystem>();
            var human = entMan.SpawnEntity(MobHuman, map.MapCoords);
            var lungs = entMan.SpawnEntity(SpaceLungs, map.MapCoords);

            Assert.That(bodySys.InsertOrganIntoBody(human, lungs), Is.True);

            trauma.SetOrganSeverity(lungs, OrganSeverity.Destroyed);

            var organ = entMan.GetComponent<OrganComponent>(lungs);
            Assert.That(organ.Enabled, Is.False);

            var statusSys = entMan.System<StatusEffectsSystem>();
            Assert.That(statusSys.TryGetStatusEffect(human, LungsImmunity, out _), Is.False);

            var analyzer = entMan.System<HealthAnalyzerSystem>();
            var state = analyzer.GetHealthAnalyzerUiState(human);
            Assert.That(state.OrganAlerts, Is.Not.Null);
            Assert.That(state.OrganAlerts!.Any(a => a.Category == "Lungs"), Is.True);
        });
    }

    [Test]
    public async Task HumanBody_CapsSpaceOrganIntegrity()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var human = entMan.SpawnEntity(MobHuman, map.MapCoords);
            var lungs = entMan.SpawnEntity(SpaceLungs, map.MapCoords);

            Assert.That(bodySys.InsertOrganIntoBody(human, lungs), Is.True);

            var organ = entMan.GetComponent<OrganComponent>(lungs);
            Assert.That(organ.IntegrityCap, Is.EqualTo(FixedPoint2.New(8)));
        });
    }

    [Test]
    public async Task ExtractFromRottingCorpse_DamagesOrganIntegrity()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var rottingSys = entMan.System<RottingSystem>();
            var mobStateSys = entMan.System<MobStateSystem>();
            var human = entMan.SpawnEntity(MobHuman, map.MapCoords);
            var heart = entMan.SpawnEntity(SpaceHeart, map.MapCoords);

            Assert.That(bodySys.InsertOrganIntoBody(human, heart), Is.True);

            rottingSys.TransferRotToOrgan(human, human);
            entMan.EnsureComponent<RottingComponent>(human);
            mobStateSys.ChangeMobState(human, MobState.Dead);

            var organBefore = entMan.GetComponent<OrganComponent>(heart).OrganIntegrity;
            Assert.That(bodySys.RemoveOrgan(heart, entMan.GetComponent<OrganComponent>(heart)), Is.True);

            var organAfter = entMan.GetComponent<OrganComponent>(heart);
            Assert.That(organAfter.OrganIntegrity, Is.LessThan(organBefore));
            Assert.That(entMan.HasComponent<PerishableComponent>(heart), Is.True);
        });
    }

    [Test]
    public async Task HarvestDamage_AppliesIntegrityLoss()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var lungs = entMan.SpawnEntity(SpaceLungs, map.MapCoords);
            var organ = entMan.GetComponent<OrganComponent>(lungs);
            var cap = organ.IntegrityCap;

            var ev = new OrganHarvestDamageEvent(0.35f);
            entMan.EventBus.RaiseLocalEvent(lungs, ref ev);

            organ = entMan.GetComponent<OrganComponent>(lungs);
            Assert.That(organ.OrganIntegrity, Is.EqualTo(cap * 0.35f));
            Assert.That(entMan.GetComponent<PerishableComponent>(lungs).ForceRotProgression, Is.True);
        });
    }

    [Test]
    public async Task SpaceHeart_InHumanBody_GrantPressureImmunity()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var human = entMan.SpawnEntity(MobHuman, map.MapCoords);
            var heart = entMan.SpawnEntity(SpaceHeart, map.MapCoords);

            Assert.That(bodySys.InsertOrganIntoBody(human, heart), Is.True);
            Assert.That(entMan.HasComponent<PressureImmunityComponent>(human), Is.True);
        });
    }
}
