using Content.Server.Antag.Components;
using Content.Server.Backmen.Blob.NPC.BlobPod;
using Content.Server.Backmen.Cloning.Components;
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
public sealed class LanguageTest
{
    [Test]
    public async Task RoleCanUnderstandTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = false,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var client = pair.Client;

        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var sys = (LanguageSystem)server.System<SharedLanguageSystem>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        var testMap = await pair.CreateTestMap();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        await server.WaitAssertion(() =>
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

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FontsTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var prototypeManager = client.ResolveDependency<IPrototypeManager>();

        await client.WaitAssertion(() =>
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
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LocalTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var prototypeManager = client.ResolveDependency<IPrototypeManager>();

        await client.WaitAssertion(() =>
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
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlobZombie()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true, DummyTicker = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var zBlob = server.EntMan.System<BlobPodSystem>();
        var zDest = server.EntMan.System<SharedDestructibleSystem>();

        var human = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            human = server.EntMan.Spawn("MobHuman",map.MapCoords);
            var pod = server.EntMan.Spawn("MobBlobPod", map.MapCoords);

            Assert.That(
                zBlob.Zombify((pod, server.EntMan.GetComponent<BlobPodComponent>(pod)), human),
                Is.True);
            var lang = server.EntMan.GetComponent<LanguageSpeakerComponent>(human);
            Assert.That(lang.CurrentLanguage, Is.EqualTo("Blob"), $"Language {human} is not a blob");
            Assert.That(lang.UnderstoodLanguages.Contains("Blob"), Is.True, $"Language {human} is not understood blob");
            Assert.That(lang.SpokenLanguages.Contains("Blob"), Is.True, $"Language {human} is not spoken blob");
            Assert.That(lang.SpokenLanguages.Count, Is.EqualTo(1), $"Language {human} is not only blob");

            zDest.DestroyEntity(pod);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var lang = server.EntMan.GetComponent<LanguageSpeakerComponent>(human);
            Assert.That(lang.CurrentLanguage, Is.Not.EqualTo("Blob"), $"Language {human} is a blob");
            Assert.That(lang.UnderstoodLanguages.Contains("Blob"), Is.False, $"Language {human} is understood blob not in blob zombie");
            Assert.That(lang.SpokenLanguages.Contains("Blob"), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
