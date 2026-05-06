using Content.Server.Antag.Components;
using Content.Server.Backmen.Blob.NPC.BlobPod;
using Content.Server.Backmen.Cloning.Components;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Language;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Backmen.Language.Systems;
using Content.Shared.Destructible;
using Content.Shared.Ghost.Roles.Components;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Language;

[TestFixture]
[TestOf(typeof(SharedLanguageSystem))]
public sealed class LanguageTest : GameTest
{
    private static PoolSettings PsNoDummyTicker => new()
    {
        Dirty = false,
        DummyTicker = false,
        Connected = true,
    };

    private static PoolSettings PsDirtyNoDummyTicker => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
    };

    [Test]
    [PairConfig(nameof(PsNoDummyTicker))]
    public async Task RoleCanUnderstandTest()
    {
        var entMan = Server.EntMan;
        var ticker = Server.System<GameTicker>();
        var sys = (LanguageSystem)Server.System<SharedLanguageSystem>();
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var compFactory = Server.ResolveDependency<IComponentFactory>();

        var testMap = await Pair.CreateTestMap();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        await Server.WaitAssertion(() =>
        {
            foreach (var entProto in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if(entProto.ID is "GhostRoleTestEntity" or "SpawnPointReturnToMenu")
                    continue;

                EntityUid ent;
                if (
                    entProto.TryGetComponent<GhostRoleAntagSpawnerComponent>(out _, compFactory) ||
                    entProto.TryGetComponent<CloningAppearanceComponent>(out _, compFactory)
                    )
                {
                    continue;
                }
                if (entProto.TryGetComponent<GhostRoleMobSpawnerComponent>(out var ghostRoleMobSpawnerComponent, compFactory))
                {
                    ent = entMan.Spawn(ghostRoleMobSpawnerComponent.Prototype, testMap.MapCoords);
                }
                else if (entProto.TryGetComponent<GhostRoleComponent>(out var ghostRoleComponent, compFactory))
                {
                    ent = entMan.Spawn(entProto.ID, testMap.MapCoords);
                }
                else
                {
                    continue;
                }
                Assert.That(entMan.HasComponent<LanguageSpeakerComponent>(ent), Is.True, $"{entMan.ToPrettyString(ent)} does not have a language speaker component");
                Assert.That(entMan.HasComponent<LanguageKnowledgeComponent>(ent), Is.True, $"{entMan.ToPrettyString(ent)} does not have a language knowledge");
                Assert.That(sys.CanUnderstand(ent, "TauCetiBasic"), $"{entMan.ToPrettyString(ent)} does not understand TauCetiBasic");
                entMan.DeleteEntity(ent);
            }
        });
    }

    [Test]
    public async Task FontsTest()
    {
        var prototypeManager = Client.ResolveDependency<IPrototypeManager>();

        await Client.WaitAssertion(() =>
        {
            foreach (var languagePrototype in prototypeManager.EnumeratePrototypes<LanguagePrototype>())
            {
                if(languagePrototype.SpeechOverride.FontId == null)
                    continue;

                Assert.That(prototypeManager.HasIndex<FontPrototype>(languagePrototype.SpeechOverride.FontId),
                    Is.True,
                    $"Font {languagePrototype.SpeechOverride.FontId} does not exist");
            }
        });
    }

    [Test]
    public async Task LocalTest()
    {
        var prototypeManager = Client.ResolveDependency<IPrototypeManager>();

        await Client.WaitAssertion(() =>
        {
            foreach (var languagePrototype in prototypeManager.EnumeratePrototypes<LanguagePrototype>())
            {
                Assert.That(languagePrototype.Name,
                    Is.Not.EqualTo($"language-{languagePrototype.ID}-name"),
                    $"LocId language-{languagePrototype.ID}-name does not exist");
                Assert.That(languagePrototype.Description,
                    Is.Not.EqualTo($"language-{languagePrototype.ID}-description"),
                    $"LocId language-{languagePrototype.ID}-description does not exist");
            }
        });
    }

    [Test]
    [PairConfig(nameof(PsDirtyNoDummyTicker))]
    public async Task BlobZombie()
    {
        var map = await Pair.CreateTestMap();
        var zBlob = Server.EntMan.System<BlobPodSystem>();
        var zDest = Server.EntMan.System<SharedDestructibleSystem>();

        var human = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            human = Server.EntMan.Spawn("MobHuman",map.MapCoords);
            var pod = Server.EntMan.Spawn("MobBlobPod", map.MapCoords);

            Assert.That(
                zBlob.Zombify((pod, Server.EntMan.GetComponent<BlobPodComponent>(pod)), human),
                Is.True);
            var lang = Server.EntMan.GetComponent<LanguageSpeakerComponent>(human);
            Assert.That(lang.CurrentLanguage, Is.EqualTo("Blob"), $"Language {human} is not a blob");
            Assert.That(lang.UnderstoodLanguages.Contains("Blob"), Is.True, $"Language {human} is not understood blob");
            Assert.That(lang.SpokenLanguages.Contains("Blob"), Is.True, $"Language {human} is not spoken blob");
            Assert.That(lang.SpokenLanguages.Count, Is.EqualTo(1), $"Language {human} is not only blob");

            zDest.DestroyEntity(pod);
        });
        await Server.WaitRunTicks(10);
        await Server.WaitAssertion(() =>
        {
            var lang = Server.EntMan.GetComponent<LanguageSpeakerComponent>(human);
            Assert.That(lang.CurrentLanguage, Is.Not.EqualTo("Blob"), $"Language {human} is a blob");
            Assert.That(lang.UnderstoodLanguages.Contains("Blob"), Is.False, $"Language {human} is understood blob not in blob zombie");
            Assert.That(lang.SpokenLanguages.Contains("Blob"), Is.False);
        });
    }
}
