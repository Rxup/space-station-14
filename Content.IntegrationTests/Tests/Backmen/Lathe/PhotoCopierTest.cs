using System.Collections.Generic;
using Content.Server.Cargo.Systems;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Research.Prototypes;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Backmen.Lathe;

[TestFixture]
public sealed class PhotoCopierTest
{

    [Test]
    public async Task PhotoCopierintTest()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var proto = server.ProtoMan;
        var  resMan = server.ResolveDependency< IResourceManager>();

        var fails = new List<string>();

        await server.WaitAssertion(() =>
        {
            var recipes = proto.EnumeratePrototypes<PaperworkFormPrototype>();

            foreach (var item   in recipes)
            {
                var txt = resMan.ContentFileReadText(item.Text).ReadToEnd();
                var msg = new FormattedMessage();
                msg.AddMarkupPermissive(txt, out var err);
                if (!string.IsNullOrEmpty(err))
                {
                    fails.Add(item.ID  + " " + item.Text + " " + err);
                }
            }
        });

        if (fails.Count > 0)
        {
            var msg = string.Join("\n", fails) + "\n" + "Ошибка в форме документа";
            Assert.Fail(msg);
        }

        await pair.CleanReturnAsync();
    }
}
