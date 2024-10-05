using Content.Server.Backmen.Blob.NPC.BlobPod;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Systems;
using Content.Shared.Destructible;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Language;

[TestFixture]
[TestOf(typeof(SharedLanguageSystem))]
public sealed class LanguageTest
{
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
