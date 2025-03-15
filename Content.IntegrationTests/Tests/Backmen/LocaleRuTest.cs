using System.Collections.Generic;
using System.Text.RegularExpressions;
using Content.Server.RandomMetadata;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen;

[TestFixture]
[TestOf(typeof(RandomMetadataSystem))]
public sealed class LocaleRu
{
    [Test]
    public async Task RandomNameMustBeRuTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = false,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var client = pair.Client;


        var proto = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();
        var mdSys = server.EntMan.System<RandomMetadataSystem>();

        var fails = new List<string>();

        var regex = new Regex(@"^[IА-Яа-яЁё\s0-9\-\'""]+$", RegexOptions.Compiled);

        await server.WaitAssertion(() =>
        {
            foreach (var entProto in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if(entProto.ID is "BorgChassisGeneric" or "BorgChassisMedical")
                    continue;
                if (!entProto.TryGetComponent<RandomMetadataComponent>(out var component, compFactory))
                {
                    continue;
                }

                if (component.NameSegments != null)
                {
                    var test = mdSys.GetRandomFromSegments(component.NameSegments, component.NameSeparator);
                    if (!regex.IsMatch(test))
                    {
                        fails.Add($"ID: {entProto.ID} NameSegments - {test}");

                    }
                }

                if (component.DescriptionSegments != null)
                {
                    var test = mdSys.GetRandomFromSegments(component.DescriptionSegments, component.DescriptionSeparator);

                    if (!regex.IsMatch(test))
                    {
                        fails.Add($"ID: {entProto.ID} DescriptionSegments - {test}");
                    }
                }
            }
        });
        if (fails.Count > 0)
        {
            var msg = string.Join("\n", fails) + "\n" + "Проверь имена entity!";
            Assert.Fail(msg);
        }

        await pair.CleanReturnAsync();
    }
}
