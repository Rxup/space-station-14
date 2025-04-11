﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.RandomMetadata;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen;

[TestFixture]
[TestOf(typeof(RandomMetadataSystem))]
public sealed partial class LocaleRu
{
    [GeneratedRegex(@"^[IА-Яа-яЁёЙй\s0-9\-\'""\.\,!\ ]+$", RegexOptions.Compiled)]
    private static partial Regex GeneratedRegex();

    private static readonly string[] IgnoreList =
    [
        "NamesBorg"
    ];

    [Test]
    public async Task RandomNameMustBeRuTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var proto = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();
        var mdSys = server.EntMan.System<RandomMetadataSystem>();

        var fails = new List<string>();

        await server.WaitAssertion(() =>
        {
            foreach (var entProto in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if(entProto.ID is
                   "FoodCookieFortune"
                   )
                    continue;
                if (!entProto.TryGetComponent<RandomMetadataComponent>(out var component, compFactory))
                {
                    continue;
                }

                if (component.NameSegments != null &&
                    !component.NameSegments.Any(x=> IgnoreList.Contains(x.Id, StringComparer.OrdinalIgnoreCase)))
                {
                    var test = mdSys.GetRandomFromSegments(component.NameSegments, component.NameFormat);
                    if (!GeneratedRegex().IsMatch(test))
                    {
                        fails.Add($"ID: {entProto.ID} NameSegments - {test}");

                    }
                }

                if (component.DescriptionSegments != null &&
                    !component.DescriptionSegments.Any(x=>IgnoreList.Contains(x.Id, StringComparer.OrdinalIgnoreCase)))
                {
                    var test = mdSys.GetRandomFromSegments(component.DescriptionSegments, component.DescriptionFormat);

                    if (!GeneratedRegex().IsMatch(test))
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
